// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Controls.Terminal;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Supplies a stand-in for <see cref="SharpConsoleUI.PtyShim"/> so tests can start a real PTY
/// with a real, long-lived child.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LinuxPtyBackend"/> starts the session by re-running the CURRENT executable in shim
/// mode, relying on the host's <c>Main</c> calling <c>PtyShim.RunIfShim</c> to claim the PTY and
/// exec the target. Under xUnit the current executable is the test runner, which does no such
/// thing: it exits immediately, the PTY closes, and the child never exists.
/// </para>
/// <para>
/// That makes the interesting case untestable, because the lifecycle bugs only appear when a
/// child is still ALIVE and holding the PTY open at teardown. So these tests point
/// <see cref="LinuxPtyBackend.ShimHostPath"/> at a small script that does exactly what the real
/// shim does — <c>setsid</c>, <c>TIOCSCTTY</c>, <c>dup2</c>, <c>execvp</c>.
/// </para>
/// <para>
/// Python, not shell: POSIX <c>sh</c> cannot redirect from a descriptor held in a variable, and
/// it has no way to issue <c>TIOCSCTTY</c>. Without a controlling terminal the child is not in
/// the PTY's session, and signalling its process group — the whole point of the fix — would be
/// testing something other than what ships.
/// </para>
/// </remarks>
internal static class PtyShimHost
{
	private static readonly object Gate = new();
	private static string? _scriptPath;

	/// <summary>
	/// True when this machine can run the tests: Linux, with a usable python3.
	/// </summary>
	internal static bool IsAvailable => OperatingSystem.IsLinux() && PythonWorks.Value;

	private static readonly Lazy<bool> PythonWorks = new(() =>
	{
		try
		{
			using var probe = Process.Start(new ProcessStartInfo("python3", "-c \"import fcntl,termios\"")
			{ UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true });
			if (probe == null) return false;
			return probe.WaitForExit(5000) && probe.ExitCode == 0;
		}
		catch { return false; }
	});

	/// <summary>Points the Linux backend at the stand-in shim for the duration of a test.</summary>
	internal static void Install()
	{
		if (!IsAvailable) return;
		LinuxPtyBackend.ShimHostPath = EnsureScript();
	}

	/// <summary>Restores the default shim host — the current executable.</summary>
	internal static void Uninstall() => LinuxPtyBackend.ShimHostPath = null;

	private static string EnsureScript()
	{
		lock (Gate)
		{
			if (_scriptPath != null && File.Exists(_scriptPath)) return _scriptPath;

			string path = Path.Combine(Path.GetTempPath(), $"sharpconsoleui-pty-shim-{Environment.ProcessId}.py");
			File.WriteAllText(path, Script);
			// Executable by its owner; the backend starts it directly, not through an interpreter.
			File.SetUnixFileMode(path,
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

			_scriptPath = path;
			return path;
		}
	}

	/// <summary>
	/// Mirrors <c>PtyShim.RunIfShim</c> step for step, so what the tests exercise is the shipped
	/// session setup rather than an approximation of it.
	/// </summary>
	private const string Script = """
		#!/usr/bin/env python3
		import fcntl, os, sys, termios

		# The backend invokes: --pty-shim <slaveFd> <exe> [args...]
		if len(sys.argv) < 4 or sys.argv[1] != "--pty-shim":
		    sys.exit(2)

		slave_fd = int(sys.argv[2])
		argv = sys.argv[3:]

		os.setsid()                                  # become a session leader
		fcntl.ioctl(slave_fd, termios.TIOCSCTTY, 0)  # claim the slave as controlling terminal
		for std in (0, 1, 2):
		    os.dup2(slave_fd, std)
		if slave_fd > 2:
		    os.close(slave_fd)

		os.environ["TERM"] = "xterm-256color"
		os.execvp(argv[0], argv)
		""";
}


/// <summary>
/// A fact that runs only where a real PTY session can be built: Linux, with a python3 able to
/// host the stand-in shim. Elsewhere it reports as skipped rather than failing — these tests
/// assert on process lifetimes and signals, which have no Windows equivalent to fall back on.
/// </summary>
internal sealed class LinuxPtyFactAttribute : Xunit.FactAttribute
{
	public LinuxPtyFactAttribute()
	{
		if (!PtyShimHost.IsAvailable)
			Skip = "requires Linux and python3 (needed to host a real PTY shim)";
	}
}
