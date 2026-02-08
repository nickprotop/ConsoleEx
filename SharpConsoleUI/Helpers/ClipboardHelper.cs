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
	/// Cross-platform clipboard helper for console applications.
	/// Uses xclip/xsel on Linux, pbcopy/pbpaste on macOS, clip.exe on Windows.
	/// Operations are best-effort and will not throw on failure.
	/// </summary>
	public static class ClipboardHelper
	{
		private const int ProcessTimeoutMs = 1000;

		/// <summary>
		/// Copies the specified text to the system clipboard.
		/// </summary>
		public static void SetText(string text)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					RunProcessWithInput("clip", text);
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					RunProcessWithInput("pbcopy", text);
				else
					RunProcessWithInput("xclip", text, "-selection", "clipboard");
			}
			catch
			{
				// Clipboard is best-effort in console environments
			}
		}

		/// <summary>
		/// Returns the current text content of the system clipboard, or empty string on failure.
		/// </summary>
		public static string GetText()
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					return RunProcessRead("powershell.exe", "-command", "Get-Clipboard");
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					return RunProcessRead("pbpaste");
				else
					return RunProcessRead("xclip", "-selection", "clipboard", "-o");
			}
			catch
			{
				return string.Empty;
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
