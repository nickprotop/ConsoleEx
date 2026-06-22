// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Identifies the clipboard backend in use.
	/// </summary>
	public enum ClipboardBackend
	{
		/// <summary>Not yet detected.</summary>
		Unknown,
		/// <summary>wl-copy / wl-paste (Wayland native).</summary>
		WlClipboard,
		/// <summary>xclip (X11).</summary>
		Xclip,
		/// <summary>xsel (X11).</summary>
		Xsel,
		/// <summary>pbcopy / pbpaste (macOS).</summary>
		Pbcopy,
		/// <summary>clip.exe / powershell Get-Clipboard (Windows).</summary>
		WindowsClip,
		/// <summary>In-process buffer — no system clipboard tool found.</summary>
		InternalFallback
	}

	/// <summary>Controls whether OSC 52 clipboard escapes are emitted on copy.</summary>
	public enum Osc52Mode
	{
		/// <summary>Emit when the terminal/session is believed to support OSC 52 (default).</summary>
		Auto,
		/// <summary>Always emit OSC 52, regardless of detection.</summary>
		Enabled,
		/// <summary>Never emit OSC 52 (local tools / internal buffer only).</summary>
		Disabled
	}

	/// <summary>
	/// Cross-platform clipboard helper for console applications.
	/// On Linux tries wl-clipboard, xclip, xsel.
	/// Falls back to an in-process buffer when no external tool is available.
	/// Operations are best-effort and will not throw on failure.
	/// </summary>
	public static class ClipboardHelper
	{
		private const int ProcessTimeoutMs = 1000;

		private static ClipboardBackend _backend = ClipboardBackend.Unknown;
		private static readonly object _lock = new();
		private static string _internalBuffer = string.Empty;

		private static volatile Action<string>? _osc52Emitter;

		/// <summary>
		/// Registers the delegate used to emit an OSC 52 sequence to the terminal. Called by the
		/// console driver at startup so the static helper can reach the terminal output stream
		/// without taking a driver dependency. Pass null to unregister (on driver shutdown).
		/// </summary>
		internal static void RegisterOsc52Emitter(Action<string>? emitter) => _osc52Emitter = emitter;

		/// <summary>Controls OSC 52 emission on copy. Default <see cref="Osc52Mode.Auto"/>.</summary>
		public static Osc52Mode Osc52Mode { get; set; } = Osc52Mode.Auto;

		/// <summary>Maximum base64 payload size for OSC 52; larger copies skip OSC 52.</summary>
		public static int MaxOsc52Bytes { get; set; } = Osc52.DefaultMaxBytes;

		private static void TryEmitOsc52(string text)
		{
			var emitter = _osc52Emitter;
			if (emitter == null) return;

			bool enabled = Osc52Mode switch
			{
				Osc52Mode.Enabled => true,
				Osc52Mode.Disabled => false,
				_ => TerminalCapabilities.SupportsOsc52
			};
			if (!enabled) return;

			try
			{
				var seq = Osc52.BuildSequence(text, tmuxWrap: TerminalCapabilities.IsTmux, MaxOsc52Bytes);
				if (seq != null)
					emitter(seq);
			}
			catch
			{
				// Best-effort: OSC 52 failure must never break copy; local path still runs.
			}
		}

		/// <summary>
		/// The clipboard backend currently in use.
		/// Triggers detection on first access.
		/// </summary>
		public static ClipboardBackend Backend
		{
			get
			{
				EnsureDetected();
				return _backend;
			}
		}

		/// <summary>
		/// Copies the specified text to the system clipboard.
		/// </summary>
		/// <remarks>
		/// The external-tool backends (clip.exe, pbcopy, xclip, xsel, wl-copy) spawn a process and
		/// wait on it. That blocking I/O is dispatched to a background thread so a copy never stalls
		/// the UI/render thread — on Windows, spawning <c>clip.exe</c> per copy was slow enough to
		/// trip the main-loop "unresponsive" watchdog (issue #42). OSC 52 emission and the in-process
		/// fallback are cheap and stay synchronous, so callers and tests observe them immediately.
		/// </remarks>
		public static void SetText(string text)
		{
			text ??= string.Empty;

			TryEmitOsc52(text);
			EnsureDetected();

			// Mirror the value into the in-process buffer synchronously so GetText() reflects the
			// copy immediately (and tests stay deterministic) regardless of the external tool's timing.
			lock (_lock) { _internalBuffer = text; }

			switch (_backend)
			{
				case ClipboardBackend.WindowsClip:
					// Windows: write via the Win32 clipboard API (CF_UNICODETEXT) — UTF-16 native, so
					// CJK/Cyrillic round-trip without the clip.exe code-page guessing the old path needed.
					// Run it OFF the UI thread: OpenClipboard can block or fail when another process holds the
					// clipboard (Windows clipboard-history, the terminal's own paste handling, AV), so a copy
					// right after a paste could otherwise stall the UI thread and trip the watchdog
					// (issue #42: "UI unresponsive after multiple copies"). The value is already mirrored into
					// the in-process buffer synchronously above, so GetText() is correct immediately regardless
					// of the background write's timing. Falls back to clip.exe if the native write fails.
					ThreadPool.QueueUserWorkItem(_ =>
					{
						if (!TryWriteWindowsClipboard(text))
							WriteToExternalTool(ClipboardBackend.WindowsClip, text);
					});
					break;
				case ClipboardBackend.Pbcopy:
				case ClipboardBackend.WlClipboard:
				case ClipboardBackend.Xclip:
				case ClipboardBackend.Xsel:
					// External tools that block on a child process run off the UI thread.
					var backend = _backend;
					ThreadPool.QueueUserWorkItem(_ => WriteToExternalTool(backend, text));
					break;
				case ClipboardBackend.InternalFallback:
				default:
					// Already written to the in-process buffer above.
					break;
			}
		}

		/// <summary>
		/// Runs the blocking external clipboard tool for <paramref name="backend"/>. Invoked on a
		/// background thread by <see cref="SetText"/>; failures fall back silently (the value is
		/// already in the in-process buffer).
		/// </summary>
		private static void WriteToExternalTool(ClipboardBackend backend, string text)
		{
			try
			{
				switch (backend)
				{
					case ClipboardBackend.WindowsClip:
						// clip.exe does NOT accept UTF-8: it reads stdin as the OEM/ANSI code page,
						// so forcing UTF-8 garbles non-Latin-1 text (Cyrillic, CJK). Instead write
						// UTF-16LE with a leading BOM (FF FE) — the strongest signal clip.exe's
						// IsTextUnicode detection honors — so all scripts round-trip correctly.
						RunProcessWithUtf16BomInput("clip", text);
						break;
					case ClipboardBackend.Pbcopy:
						RunProcessWithInput("pbcopy", text);
						break;
					case ClipboardBackend.WlClipboard:
						RunProcessWithInput("wl-copy", text);
						break;
					case ClipboardBackend.Xclip:
						RunProcessWithInput("xclip", text, "-selection", "clipboard");
						break;
					case ClipboardBackend.Xsel:
						RunProcessWithInput("xsel", text, "--clipboard", "--input");
						break;
				}
			}
			catch
			{
				// External tool failed at runtime — the in-process buffer already holds the value.
			}
		}

		/// <summary>
		/// Returns the current text content of the system clipboard, or empty string on failure.
		/// </summary>
		public static string GetText()
		{
			EnsureDetected();
			try
			{
				return _backend switch
				{
					// Windows: read via the Win32 clipboard API (CF_UNICODETEXT) — in-process and instant,
					// so a paste never spawns powershell.exe Get-Clipboard on the UI thread (issue #42: that
					// spawn was slow enough to trip the unresponsive-watchdog). UTF-16 native, so CJK/Cyrillic
					// round-trip without code-page re-encoding. Falls back to powershell only if it returns null.
					ClipboardBackend.WindowsClip =>
						TryReadWindowsClipboard() ?? RunProcessRead("powershell.exe", "-command", "Get-Clipboard"),
					ClipboardBackend.Pbcopy =>
						RunProcessRead("pbpaste"),
					ClipboardBackend.WlClipboard =>
						RunProcessRead("wl-paste", "--no-newline"),
					ClipboardBackend.Xclip =>
						RunProcessRead("xclip", "-selection", "clipboard", "-o"),
					ClipboardBackend.Xsel =>
						RunProcessRead("xsel", "--clipboard", "--output"),
					_ =>
						GetInternalBuffer()
				};
			}
			catch
			{
				return GetInternalBuffer();
			}
		}

		/// <summary>
		/// Returns the clipboard text without ever blocking the caller for more than
		/// <paramref name="timeoutMs"/> milliseconds. The Windows path is the in-process Win32 read (instant),
		/// but the Unix backends spawn a child process (<c>xclip -o</c> / <c>wl-paste</c> / <c>pbpaste</c>) that
		/// can stall — running that on the UI thread is the same watchdog hazard as the Windows copy was
		/// (issue #42). This runs the read on a background thread and, if it does not complete in time, returns
		/// the in-process buffer so a paste can never hang the render loop. Use this from UI-thread paste paths;
		/// <see cref="GetText"/> remains the (possibly blocking) full-fidelity read for non-UI callers/tests.
		/// </summary>
		public static string GetTextWithTimeout(int timeoutMs = 200)
		{
			EnsureDetected();

			// Windows native read and the in-process fallback are already instant — no thread hop needed.
			if (_backend is ClipboardBackend.WindowsClip or ClipboardBackend.InternalFallback)
				return GetText();

			// Unix process-backed read: do it off-thread, bounded by the timeout.
			string? result = null;
			var done = new ManualResetEventSlim(false);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try { result = GetText(); }
				catch { /* best-effort; fall through to buffer */ }
				finally { done.Set(); }
			});

			if (done.Wait(timeoutMs) && result != null)
				return result;

			// Timed out (or the read failed): serve the last value the app itself copied, never block.
			return GetInternalBuffer();
		}

		private static string GetInternalBuffer()
		{
			lock (_lock) { return _internalBuffer; }
		}

		/// <summary>
		/// Forces a specific backend, bypassing platform auto-detection. Intended for tests so that
		/// clipboard round-trips can be made hermetic (use <see cref="ClipboardBackend.InternalFallback"/>
		/// to route through the in-process buffer and avoid touching the real system clipboard).
		/// </summary>
		internal static void ForceBackendForTests(ClipboardBackend backend)
		{
			lock (_lock)
			{
				_backend = backend;
				if (backend == ClipboardBackend.InternalFallback)
					_internalBuffer = string.Empty;
			}
		}

		private static void EnsureDetected()
		{
			if (_backend != ClipboardBackend.Unknown)
				return;

			lock (_lock)
			{
				if (_backend != ClipboardBackend.Unknown)
					return;

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					_backend = ClipboardBackend.WindowsClip;
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					_backend = ClipboardBackend.Pbcopy;
				}
				else
				{
					// Linux / FreeBSD — try Wayland first, then X11 tools
					if (CanRun("wl-copy", "--version"))
						_backend = ClipboardBackend.WlClipboard;
					else if (CanRun("xclip", "-version"))
						_backend = ClipboardBackend.Xclip;
					else if (CanRun("xsel", "--version"))
						_backend = ClipboardBackend.Xsel;
					else
						_backend = ClipboardBackend.InternalFallback;
				}
			}
		}

		private static bool CanRun(string cmd, params string[] args)
		{
			try
			{
				var psi = new ProcessStartInfo(cmd)
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				foreach (var arg in args)
					psi.ArgumentList.Add(arg);
				using var p = Process.Start(psi);
				if (p == null) return false;
				p.WaitForExit(ProcessTimeoutMs);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void RunProcessWithInput(string cmd, string input, params string[] args)
		{
			var psi = new ProcessStartInfo(cmd)
			{
				RedirectStandardInput = true,
				StandardInputEncoding = System.Text.Encoding.UTF8,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			foreach (var arg in args)
				psi.ArgumentList.Add(arg);
			using var p = Process.Start(psi);
			if (p == null) return;
			p.StandardInput.Write(input);
			p.StandardInput.Close();
			p.WaitForExit(ProcessTimeoutMs);
		}

		/// <summary>
		/// Writes <paramref name="input"/> to the process stdin as UTF-16LE with a leading BOM
		/// (FF FE) and no implicit transcoding. Used for Windows <c>clip.exe</c>, which reads its
		/// stdin as the OEM/ANSI code page unless a UTF-16 BOM marks the stream as Unicode.
		/// </summary>
		private static void RunProcessWithUtf16BomInput(string cmd, string input)
		{
			var psi = new ProcessStartInfo(cmd)
			{
				RedirectStandardInput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using var p = Process.Start(psi);
			if (p == null) return;
			// Write the raw bytes ourselves: BOM + UTF-16LE. Going through the BaseStream avoids
			// the StreamWriter re-encoding to the console code page.
			byte[] payload = BuildUtf16BomBytes(input);
			using (var stdin = p.StandardInput.BaseStream)
			{
				stdin.Write(payload, 0, payload.Length);
				stdin.Flush();
			}
			p.WaitForExit(ProcessTimeoutMs);
		}

		/// <summary>
		/// Builds the byte payload written to <c>clip.exe</c>: a UTF-16LE byte-order mark (FF FE)
		/// followed by the text in UTF-16LE. Pure byte logic with no I/O, so the encoding decision
		/// is unit-testable on any platform (the actual <c>clip.exe</c> pipe is Windows-only).
		/// </summary>
		/// <param name="text">The text to encode; null is treated as empty.</param>
		internal static byte[] BuildUtf16BomBytes(string? text)
		{
			byte[] body = System.Text.Encoding.Unicode.GetBytes(text ?? string.Empty);
			byte[] payload = new byte[2 + body.Length];
			payload[0] = 0xFF;
			payload[1] = 0xFE;
			Buffer.BlockCopy(body, 0, payload, 2, body.Length);
			return payload;
		}

		private static string RunProcessRead(string cmd, params string[] args)
		{
			var psi = new ProcessStartInfo(cmd)
			{
				RedirectStandardOutput = true,
				StandardOutputEncoding = System.Text.Encoding.UTF8,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			foreach (var arg in args)
				psi.ArgumentList.Add(arg);
			using var p = Process.Start(psi);
			if (p == null) return string.Empty;
			string result = p.StandardOutput.ReadToEnd();
			p.WaitForExit(ProcessTimeoutMs);
			return result;
		}

		#region Windows native clipboard (Win32 API)

		// Mirrors Terminal.Gui's proven Windows clipboard path: talk to the Win32 clipboard API directly
		// (CF_UNICODETEXT) instead of spawning clip.exe / powershell.exe. In-process and microsecond-fast,
		// so neither copy nor paste can stall the UI thread (issue #42), and UTF-16 is the clipboard's native
		// text format so every script round-trips without code-page guessing. [DllImport] to system DLLs is
		// AOT-safe (unlike Expression.Compile), so this keeps IsAotCompatible intact.

		private const uint CF_UNICODETEXT = 13;
		private const uint GMEM_MOVEABLE = 0x0002;

		// OpenClipboard fails (returns false) when another process holds the clipboard — extremely common
		// right after a paste, or with Windows clipboard-history / the terminal's own clipboard use. Retry a
		// few times with a short sleep, like Terminal.Gui does, instead of giving up on the first contention.
		// Both call sites run OFF the UI thread (the write is queued to the ThreadPool; the read goes through
		// GetTextWithTimeout's background path), so this brief blocking never stalls the render loop.
		private const int ClipboardOpenRetries = 8;
		private const int ClipboardOpenRetryDelayMs = 25;

		/// <summary>
		/// Opens the Windows clipboard, retrying briefly while it is held by another process. Returns false if
		/// it could not be acquired within the retry budget (caller then falls back / returns null). Windows-only.
		/// </summary>
		private static bool OpenClipboardWithRetry()
		{
			for (int attempt = 0; attempt < ClipboardOpenRetries; attempt++)
			{
				if (OpenClipboard(nint.Zero))
					return true;
				Thread.Sleep(ClipboardOpenRetryDelayMs);
			}
			return false;
		}

		/// <summary>
		/// Reads CF_UNICODETEXT from the Windows clipboard via the Win32 API. Returns the text, or
		/// <c>null</c> on any failure (so the caller can fall back to the powershell path). Never throws.
		/// </summary>
		private static string? TryReadWindowsClipboard()
		{
			if (!OperatingSystem.IsWindows())
				return null;

			try
			{
				if (!OpenClipboardWithRetry())
					return null;
				try
				{
					nint handle = GetClipboardData(CF_UNICODETEXT);
					if (handle == nint.Zero)
						return string.Empty; // clipboard has no unicode text — distinct from "failed"

					nint pointer = GlobalLock(handle);
					if (pointer == nint.Zero)
						return null;
					try
					{
						return Marshal.PtrToStringUni(pointer) ?? string.Empty;
					}
					finally
					{
						GlobalUnlock(handle);
					}
				}
				finally
				{
					CloseClipboard();
				}
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Writes <paramref name="text"/> to the Windows clipboard as CF_UNICODETEXT via the Win32 API.
		/// Returns <c>true</c> on success; <c>false</c> on any failure (so the caller can fall back to
		/// clip.exe). Never throws.
		/// </summary>
		private static bool TryWriteWindowsClipboard(string text)
		{
			if (!OperatingSystem.IsWindows())
				return false;

			try
			{
				if (!OpenClipboardWithRetry())
					return false;
				try
				{
					EmptyClipboard();

					// Allocate a moveable global block holding the null-terminated UTF-16 string.
					byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text + '\0');
					nint hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
					if (hGlobal == nint.Zero)
						return false;

					nint target = GlobalLock(hGlobal);
					if (target == nint.Zero)
					{
						GlobalFree(hGlobal);
						return false;
					}
					try
					{
						Marshal.Copy(bytes, 0, target, bytes.Length);
					}
					finally
					{
						GlobalUnlock(hGlobal);
					}

					// On success the clipboard OWNS hGlobal — do not free it. On failure we must free it.
					if (SetClipboardData(CF_UNICODETEXT, hGlobal) == nint.Zero)
					{
						GlobalFree(hGlobal);
						return false;
					}
					return true;
				}
				finally
				{
					CloseClipboard();
				}
			}
			catch
			{
				return false;
			}
		}

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool OpenClipboard(nint hWndNewOwner);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CloseClipboard();

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool EmptyClipboard();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern nint GetClipboardData(uint uFormat);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern nint SetClipboardData(uint uFormat, nint hMem);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern nint GlobalFree(nint hMem);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern nint GlobalLock(nint hMem);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GlobalUnlock(nint hMem);

		#endregion
	}
}
