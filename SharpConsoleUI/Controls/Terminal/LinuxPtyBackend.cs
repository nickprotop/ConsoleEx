// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Linux PTY backend using <c>openpty</c> + an in-process shim that calls
/// <c>setsid / TIOCSCTTY / dup2 / execvp</c> before running the target executable.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxPtyBackend : IPtyBackend
{
	/// <summary>
	/// The PTY master descriptor, or -1 once closed. Not readonly and not a plain int: it is set
	/// to -1 BEFORE the fd is closed and read by every I/O entry point, so a late write cannot
	/// land on a descriptor the OS has already recycled for an unrelated file. Interlocked
	/// publication makes that ordering visible to the UI thread and the read thread alike.
	/// </summary>
	private int _masterFd;

	private readonly Process _shimProc;

	/// <summary>
	/// Captured at spawn because <see cref="Process.Id"/> throws once the Process object is
	/// disposed, and the pid is still wanted afterwards — by logging, and by a host reading
	/// <c>ProcessId</c> from a ProcessExited handler, which runs after teardown.
	/// </summary>
	private readonly int _childPid;

	private readonly ILogService? _log;
	private int _disposed = 0;
	private int _shutdownBegun = 0;

	/// <summary>
	/// Overrides the executable re-run in shim mode, which is normally this process itself.
	/// <para>
	/// Exists for tests: the shim host has to be a program whose <c>Main</c> calls
	/// <see cref="PtyShim.RunIfShim"/>, and a test host does not. Without this the shim would
	/// re-run the test runner, which exits at once and so can never reproduce the case that
	/// matters — a child still alive and holding the PTY open when teardown begins.
	/// </para>
	/// Null (the default) uses the current process.
	/// </summary>
	internal static string? ShimHostPath;

	public LinuxPtyBackend(string exe, string[]? args, int rows, int cols, string? workingDirectory = null, ILogService? logService = null)
	{
		_log = logService;
		_log?.LogInfo($"LinuxPtyBackend: opening PTY ({rows}x{cols}) for exe='{exe}' cwd='{workingDirectory ?? "(inherit)"}'", "PTY");

		(_masterFd, int slave) = PtyNative.Open(rows, cols);
		_log?.LogDebug($"LinuxPtyBackend: openpty master={_masterFd} slave={slave}", "PTY");

		// Spawn this same executable as the shim: --pty-shim <slave> <exe> [args]
		var shimArgs = new List<string> { "--pty-shim", slave.ToString(), exe };
		if (args != null) shimArgs.AddRange(args);

		var psi = new ProcessStartInfo(ShimHostPath ?? Environment.ProcessPath ?? "/proc/self/exe")
		{ UseShellExecute = false };
		if (workingDirectory != null)
			psi.WorkingDirectory = workingDirectory;
		psi.Environment["TERM"] = "xterm-256color";
		foreach (var a in shimArgs) psi.ArgumentList.Add(a);

		_shimProc = Process.Start(psi) ?? throw new InvalidOperationException("PTY shim failed to start");
		_childPid = _shimProc.Id;
		PtyNative.close(slave);  // parent closes its copy of the slave fd
		_log?.LogInfo($"LinuxPtyBackend: shim spawned, childPid={_childPid}", "PTY");
	}

	public int ChildProcessId => _childPid;

	/// <summary>
	/// After execvp the shim process IS the target command — same pid, same exit status — so
	/// waiting on <c>_shimProc</c> yields the command's own status, not a wrapper's.
	/// </summary>
	public int? ExitCode => _exitCodeSet != 0 ? _exitCode : null;

	// Written by whichever thread reaps the child, read from the UI thread through
	// TerminalControl.ExitCode. The flag is published with Interlocked AFTER the value, so a
	// reader that sees the flag set is guaranteed to see the value too.
	private int _exitCode;
	private int _exitCodeSet;

	/// <summary>How long SIGHUP is given to end the child before SIGKILL follows.</summary>
	private const int GracefulExitMs = 200;

	/// <summary>How long the reap waits after SIGKILL before giving up on a status.</summary>
	private const int ForcedExitMs = 300;

	public int Read(byte[] buf, int count)
	{
		int fd = Volatile.Read(ref _masterFd);
		if (fd < 0) return 0;
		return PtyNative.read(fd, buf, count);
	}

	public void Write(byte[] buf, int count)
	{
		int fd = Volatile.Read(ref _masterFd);
		if (fd < 0) return;
		PtyNative.write(fd, buf, count);
	}

	public void Resize(int rows, int cols)
	{
		int fd = Volatile.Read(ref _masterFd);
		if (fd < 0) return;
		_log?.LogDebug($"LinuxPtyBackend.Resize({rows}x{cols})", "PTY");
		PtyNative.Resize(fd, rows, cols);
	}

	/// <inheritdoc/>
	public void BeginShutdown()
	{
		if (Interlocked.Exchange(ref _shutdownBegun, 1) != 0) return;

		_log?.LogDebug($"LinuxPtyBackend.BeginShutdown: SIGHUP to process group {_childPid}", "PTY");

		// SIGHUP to the GROUP is what actually wakes a blocked read: the read then returns
		// -1/EIO, which the read loop already treats as end-of-stream. Closing the fd would not
		// wake it (measured). The fd stays open until Dispose so the loop can drain whatever the
		// child wrote on its way out.
		try { PtyNative.KillGroup(_childPid, PtyNative.SIGHUP); } catch { }
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

		_log?.LogDebug($"LinuxPtyBackend.Dispose: tearing down pid {_childPid}", "PTY");

		// Signal first in case Dispose was reached without BeginShutdown.
		BeginShutdown();

		try
		{
			// Process.ExitCode throws while the process is alive, so it is only read once a wait
			// confirms exit; a wait that times out leaves ExitCode null rather than crashing.
			if (!_shimProc.WaitForExit(GracefulExitMs))
			{
				// The child ignored SIGHUP — or is wedged in uninterruptible state. SIGKILL the
				// group so nothing survives the control that owns it.
				_log?.LogDebug($"LinuxPtyBackend.Dispose: SIGHUP ignored, SIGKILL to group {_childPid}", "PTY");
				try { PtyNative.KillGroup(_childPid, PtyNative.SIGKILL); } catch { }
				_shimProc.WaitForExit(ForcedExitMs);
			}

			if (_shimProc.HasExited)
			{
				_exitCode = _shimProc.ExitCode;
				Interlocked.Exchange(ref _exitCodeSet, 1);
			}
		}
		catch { }

		// Invalidate before closing, so a concurrent Write/Resize either sees a live fd that is
		// still open or sees -1 and does nothing — never a stale number the OS has handed out
		// again. This is the ordering Windows already had by nulling its stream fields.
		int fd = Interlocked.Exchange(ref _masterFd, -1);
		if (fd >= 0)
		{
			try { PtyNative.close(fd); } catch { }
		}

		// The Process object holds a native handle; without this it is released only by
		// finalization, which leaks one handle per terminal session.
		try { _shimProc.Dispose(); } catch { }
	}
}
