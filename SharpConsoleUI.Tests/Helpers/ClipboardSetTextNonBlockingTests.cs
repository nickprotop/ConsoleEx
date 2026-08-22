using System.Diagnostics;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Tests that <see cref="ClipboardHelper.SetText"/> does not block the calling thread on external
/// clipboard-tool process I/O (issue #42: copy spawning clip.exe on the UI thread tripped the
/// main-loop "unresponsive" watchdog). The synchronous observable behaviour — in-process buffer and
/// OSC 52 emission — must be preserved so existing callers and tests stay deterministic.
/// </summary>
[Collection("EnvSerial")]
public class ClipboardSetTextNonBlockingTests : IDisposable
{
	private readonly List<string> _emitted = new();

	public ClipboardSetTextNonBlockingTests()
	{
		ClipboardHelper.RegisterOsc52Emitter(s => _emitted.Add(s));
		ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
		TerminalCapabilities.SetOsc52Override(null);
	}

	public void Dispose()
	{
		// Let queued Windows clipboard writes finish before the next test runs: otherwise one test's
		// in-flight copy lands during another's read and makes it pass (or fail) for the wrong reason.
		ClipboardHelper.DrainPendingWritesForTests();
		ClipboardHelper.RegisterOsc52Emitter(null);
		ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
		TerminalCapabilities.SetOsc52Override(null);
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
	}

	/// <summary>
	/// A copy must be readable immediately on Windows, even when the system clipboard cannot be
	/// reached at all.
	/// </summary>
	/// <remarks>
	/// The Windows read path used to go straight to the system clipboard and, on failure, to
	/// <c>powershell.exe Get-Clipboard</c> — never to the in-process buffer. Two things then produced
	/// an empty result from a fresh copy: the Win32 write is queued to the ThreadPool, so a read
	/// right after a copy can beat it; and where there is no window station (a service, a CI runner)
	/// neither the Win32 read nor powershell returns anything. CI caught this: the pre-existing
	/// round-trip test failed on windows-latest with Expected "ASCII 中文 …" / Actual "".
	///
	/// <para>Runs on every platform, but is only a REGRESSION GATE on Windows. On Linux/macOS the
	/// powershell fallback throws (no such binary) and <see cref="ClipboardHelper.GetText"/>'s outer
	/// catch already returns the buffer, so this passes with or without the fix. On Windows
	/// powershell exists and returns an empty string, which the old code returned verbatim — that is
	/// the failure CI saw, and what this pins.</para>
	/// </remarks>
	[Fact]
	public void WindowsBackend_GetText_FallsBackToBuffer_WhenSystemClipboardUnavailable()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.WindowsClip);
		// Distinct from every other test's payload: a shared string can be supplied by another test's
		// in-flight write, which would make this pass without exercising the fallback at all.
		const string sample = "fallback ASCII 中文 Привет 🚀 naïve";

		var sw = Stopwatch.StartNew();
		ClipboardHelper.SetText(sample);
		string read = ClipboardHelper.GetText();
		sw.Stop();

		Assert.Equal(sample, read);

		// And it must be prompt: answering from the buffer must not spawn powershell.exe, whose
		// per-read cost is the stall the native path exists to avoid (issue #42).
		Assert.True(sw.ElapsedMilliseconds < 250,
			$"copy+read took {sw.ElapsedMilliseconds}ms; expected the in-process path, not a process spawn.");
	}

	/// <summary>Repeated copies keep reading back the most recent value, not a stale one.</summary>
	[Fact]
	public void WindowsBackend_GetText_TracksSuccessiveCopies()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.WindowsClip);

		foreach (var value in new[] { "first", "second", "third \u4e2d\u6587" })
		{
			ClipboardHelper.SetText(value);
			Assert.Equal(value, ClipboardHelper.GetText());
		}
	}

	/// <summary>
	/// The counter must actually observe a process-spawning read, or the assertion above proves nothing.
	/// </summary>
	[Fact]
	public void ProcessReadCount_CountsAProcessBackedRead()
	{
		// An external-tool backend shells out on read. Which tool exists varies by machine; the count
		// is incremented before the process is started, so it rises either way.
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xclip);
		ClipboardHelper.ResetProcessReadCountForTests();

		try { _ = ClipboardHelper.GetText(); }
		catch { /* the tool may not exist here — the counter is what is under test */ }

		Assert.True(ClipboardHelper.ProcessReadCount > 0,
			"a process-backed read was not counted; the native-path assertion would be vacuous.");
	}

	[Fact]
	public void SetText_InProcessBuffer_WrittenSynchronously()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText("immediate");
		// No await/sleep: the value must be readable on the very next line.
		Assert.Equal("immediate", ClipboardHelper.GetText());
	}

	[Fact]
	public void SetText_Osc52_EmittedSynchronously()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		TerminalCapabilities.SetOsc52Override(true);
		ClipboardHelper.SetText("osc");
		Assert.Single(_emitted);
		Assert.Contains("\x1b]52;c;", _emitted[0]);
	}

	[Fact]
	public void SetText_ReturnsPromptly_EvenWhenBackendIsAProcess()
	{
		// Force a process-backed backend. The tool itself may be absent on CI — that's fine: the
		// point is that SetText dispatches the (potentially slow/blocking) process work off-thread
		// and returns to the caller immediately, never blocking on Process.Start/WaitForExit.
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xclip);
		var sw = Stopwatch.StartNew();
		ClipboardHelper.SetText("bg");
		sw.Stop();

		// Generous ceiling: well under the per-process ProcessTimeoutMs (1000ms) and the watchdog
		// stale threshold (2000ms). A synchronous Process.Start + WaitForExit would blow past this.
		Assert.True(sw.ElapsedMilliseconds < 250,
			$"SetText blocked the caller for {sw.ElapsedMilliseconds}ms; expected off-thread dispatch.");

		// The in-process mirror is still set synchronously regardless of the external tool's fate.
		Assert.Equal("bg", ClipboardHelper.GetText());
	}

	// Issue #42 (cross-platform): the Ctrl+V paste read must never block the UI thread. On Unix the read
	// spawns xclip/wl-paste/pbpaste, which can stall — GetTextWithTimeout bounds that and falls back to the
	// in-process buffer. Force a process backend (the tool may be absent on CI, which only makes the read
	// fail faster) and assert the call returns well within the watchdog window.
	[Fact]
	public void GetTextWithTimeout_ReturnsPromptly_EvenWhenBackendIsAProcess()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText("buffered");          // seed the in-process fallback
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xclip); // now a process-backed read

		var sw = Stopwatch.StartNew();
		string result = ClipboardHelper.GetTextWithTimeout(200);
		sw.Stop();

		// Must return within ~the timeout, never the 1000ms ProcessTimeoutMs or the 2000ms watchdog threshold.
		Assert.True(sw.ElapsedMilliseconds < 600,
			$"GetTextWithTimeout blocked for {sw.ElapsedMilliseconds}ms; expected bounded off-thread read.");
		// On a box with a real xclip it returns the actual clipboard; on CI without it, the buffered fallback.
		Assert.NotNull(result);
	}

	[Fact]
	public void GetTextWithTimeout_InProcessBackend_ReturnsBufferInstantly()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText("instant");
		Assert.Equal("instant", ClipboardHelper.GetTextWithTimeout());
	}

	// Issue #42: on Windows the read path used to spawn `powershell.exe Get-Clipboard` synchronously on
	// the UI thread — seconds of cold-start, which tripped the unresponsive watchdog on a paste/second copy.
	// The native Win32 path (CF_UNICODETEXT) is in-process and instant. This verifies a copy→read round-trip
	// through the REAL Windows clipboard returns promptly and preserves Unicode (CJK/Cyrillic/emoji), which is
	// what regressed in #42. Windows-only: the native backend can't run on Linux/macOS CI.
	[Fact]
	public void Windows_NativeClipboard_RoundTripsUnicode_Promptly()
	{
		if (!OperatingSystem.IsWindows())
			return; // native CF_UNICODETEXT path is Windows-only

		ClipboardHelper.ForceBackendForTests(ClipboardBackend.WindowsClip);
		const string sample = "ASCII 中文 Привет 🚀 naïve";

		var sw = Stopwatch.StartNew();
		ClipboardHelper.SetText(sample);
		string read = ClipboardHelper.GetText();
		sw.Stop();

		Assert.Equal(sample, read);
		// No powershell/clip.exe spawn: a full write+read must be far under the watchdog threshold.
		Assert.True(sw.ElapsedMilliseconds < 250,
			$"Windows clipboard write+read took {sw.ElapsedMilliseconds}ms; expected the in-process Win32 path.");
	}

	/// <summary>
	/// Repeated reads on the Windows backend must stay on the in-process Win32 path.
	/// </summary>
	/// <remarks>
	/// Issue #42: the SECOND copy hung because the intervening paste's <c>GetText()</c> spawned
	/// powershell on the UI thread. This covers the read side.
	///
	/// <para>Asserts WHICH path ran, via <c>ProcessReadCount</c>, rather than inferring it from a
	/// stopwatch. The timing version needed 20 live round-trips against the real system clipboard —
	/// machine-global state shared with everything else on the box, and with every other test — to
	/// make the difference measurable. A process spawn is now counted directly, so a handful of reads
	/// prove the same thing, and the assertion fails for the right reason instead of on a slow
	/// machine.</para>
	/// </remarks>
	[Fact]
	public void Windows_NativeClipboard_RepeatedReads_StayOnTheNativePath()
	{
		if (!OperatingSystem.IsWindows())
			return;

		ClipboardHelper.ForceBackendForTests(ClipboardBackend.WindowsClip);
		ClipboardHelper.SetText("中文测试内容");
		ClipboardHelper.ResetProcessReadCountForTests();

		for (int i = 0; i < 3; i++)
			_ = ClipboardHelper.GetText();

		Assert.Equal(0, ClipboardHelper.ProcessReadCount);
	}
}
