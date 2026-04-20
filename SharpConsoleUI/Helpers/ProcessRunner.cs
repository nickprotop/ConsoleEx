// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Safe process execution helper that prevents command injection by always
	/// using ArgumentList (never string-concatenated arguments). All process
	/// spawning in the framework should go through this class.
	/// </summary>
	public static class ProcessRunner
	{
		private const int DefaultTimeoutMs = 10000;

		/// <summary>
		/// Runs a process with the given arguments and returns its stdout content.
		/// Arguments are passed individually via ArgumentList to prevent injection.
		/// </summary>
		/// <param name="executable">The executable name or path.</param>
		/// <param name="arguments">Individual arguments (not shell-joined).</param>
		/// <param name="timeoutMs">Maximum time to wait for the process to exit.</param>
		/// <returns>The process stdout output, or empty string on failure.</returns>
		public static string RunAndReadOutput(string executable, IEnumerable<string> arguments, int timeoutMs = DefaultTimeoutMs)
		{
			var psi = CreateStartInfo(executable, arguments);
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;

			try
			{
				using var process = Process.Start(psi);
				if (process == null)
					return string.Empty;

				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(timeoutMs);
				return output;
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Runs a process with the given arguments, writes input to stdin, and waits for exit.
		/// </summary>
		/// <param name="executable">The executable name or path.</param>
		/// <param name="arguments">Individual arguments (not shell-joined).</param>
		/// <param name="stdinInput">Content to write to the process stdin.</param>
		/// <param name="timeoutMs">Maximum time to wait for the process to exit.</param>
		public static void RunWithInput(string executable, IEnumerable<string> arguments, string stdinInput, int timeoutMs = DefaultTimeoutMs)
		{
			var psi = CreateStartInfo(executable, arguments);
			psi.RedirectStandardInput = true;

			try
			{
				using var process = Process.Start(psi);
				if (process == null)
					return;

				process.StandardInput.Write(stdinInput);
				process.StandardInput.Close();
				process.WaitForExit(timeoutMs);
			}
			catch
			{
				// Silently ignore process failures
			}
		}

		/// <summary>
		/// Checks if a given executable can be found and run (exits without error).
		/// </summary>
		/// <param name="executable">The executable name or path.</param>
		/// <param name="arguments">Arguments for the probe invocation.</param>
		/// <param name="timeoutMs">Maximum time to wait.</param>
		/// <returns>True if the process started and exited successfully.</returns>
		public static bool CanRun(string executable, IEnumerable<string> arguments, int timeoutMs = DefaultTimeoutMs)
		{
			var psi = CreateStartInfo(executable, arguments);
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;

			try
			{
				using var process = Process.Start(psi);
				if (process == null)
					return false;

				process.WaitForExit(timeoutMs);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Starts a long-running process (e.g., ffmpeg) and returns it for stream-based I/O.
		/// The caller is responsible for disposing the process.
		/// </summary>
		/// <param name="executable">The executable name or path.</param>
		/// <param name="arguments">Individual arguments.</param>
		/// <param name="redirectStdin">Whether to redirect stdin.</param>
		/// <param name="redirectStdout">Whether to redirect stdout.</param>
		/// <param name="redirectStderr">Whether to redirect stderr.</param>
		/// <returns>The started process, or null if it failed to start.</returns>
		public static Process? StartProcess(string executable, IEnumerable<string> arguments,
			bool redirectStdin = false, bool redirectStdout = false, bool redirectStderr = false)
		{
			var psi = CreateStartInfo(executable, arguments);
			psi.RedirectStandardInput = redirectStdin;
			psi.RedirectStandardOutput = redirectStdout;
			psi.RedirectStandardError = redirectStderr;

			try
			{
				return Process.Start(psi);
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Creates a ProcessStartInfo with UseShellExecute=false and arguments added via ArgumentList.
		/// </summary>
		private static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> arguments)
		{
			var psi = new ProcessStartInfo(executable)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			foreach (var arg in arguments)
				psi.ArgumentList.Add(arg);

			return psi;
		}
	}
}
