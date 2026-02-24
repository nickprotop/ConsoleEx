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
			EnsureDetected();
			try
			{
				switch (_backend)
				{
					case ClipboardBackend.WindowsClip:
						RunProcessWithInput("clip", text);
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
						RunProcessWithInput("xsel", "--clipboard", "--input");
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
				var psi = new ProcessStartInfo(cmd, string.Join(" ", args))
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
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
			var psi = new ProcessStartInfo(cmd, string.Join(" ", args))
			{
				RedirectStandardInput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using var p = Process.Start(psi);
			if (p == null) return;
			p.StandardInput.Write(input);
			p.StandardInput.Close();
			p.WaitForExit(ProcessTimeoutMs);
		}

		private static string RunProcessRead(string cmd, params string[] args)
		{
			var psi = new ProcessStartInfo(cmd, string.Join(" ", args))
			{
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using var p = Process.Start(psi);
			if (p == null) return string.Empty;
			string result = p.StandardOutput.ReadToEnd();
			p.WaitForExit(ProcessTimeoutMs);
			return result;
		}
	}
}
