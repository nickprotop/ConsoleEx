// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;

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
		public static void SetText(string text)
		{
			TryEmitOsc52(text);
			EnsureDetected();
			try
			{
				switch (_backend)
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
					case ClipboardBackend.InternalFallback:
					default:
						lock (_lock) { _internalBuffer = text; }
						break;
				}
			}
			catch
			{
				// External tool failed at runtime — store in internal buffer
				lock (_lock) { _internalBuffer = text; }
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
					ClipboardBackend.WindowsClip =>
						RunProcessRead("powershell.exe", "-command", "Get-Clipboard"),
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
	}
}
