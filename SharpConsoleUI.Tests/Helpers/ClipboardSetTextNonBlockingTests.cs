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
		ClipboardHelper.RegisterOsc52Emitter(null);
		ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
		TerminalCapabilities.SetOsc52Override(null);
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
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
}
