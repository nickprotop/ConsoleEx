using System.Diagnostics;
using System.Runtime.Versioning;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Linux PTY backend using <c>openpty</c> + an in-process shim that calls
/// <c>setsid / TIOCSCTTY / dup2 / execvp</c> before running the target executable.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxPtyBackend : IPtyBackend
{
    private readonly int     _masterFd;
    private readonly Process _shimProc;
    private readonly ILogService? _log;
    private int _disposed = 0;

    public LinuxPtyBackend(string exe, string[]? args, int rows, int cols, string? workingDirectory = null, ILogService? logService = null)
    {
        _log = logService;
        _log?.LogInfo($"LinuxPtyBackend: opening PTY ({rows}x{cols}) for exe='{exe}' cwd='{workingDirectory ?? "(inherit)"}'", "PTY");

        (_masterFd, int slave) = PtyNative.Open(rows, cols);
        _log?.LogDebug($"LinuxPtyBackend: openpty master={_masterFd} slave={slave}", "PTY");

        // Spawn this same executable as the shim: --pty-shim <slave> <exe> [args]
        var shimArgs = new List<string> { "--pty-shim", slave.ToString(), exe };
        if (args != null) shimArgs.AddRange(args);

        var psi = new ProcessStartInfo(Environment.ProcessPath ?? "/proc/self/exe")
            { UseShellExecute = false };
        if (workingDirectory != null)
            psi.WorkingDirectory = workingDirectory;
        psi.Environment["TERM"] = "xterm-256color";
        foreach (var a in shimArgs) psi.ArgumentList.Add(a);

        _shimProc = Process.Start(psi) ?? throw new InvalidOperationException("PTY shim failed to start");
        PtyNative.close(slave);  // parent closes its copy of the slave fd
        _log?.LogInfo($"LinuxPtyBackend: shim spawned, childPid={_shimProc.Id}", "PTY");
    }

    public int ChildProcessId => _shimProc.Id;

    public int Read(byte[] buf, int count) => PtyNative.read(_masterFd, buf, count);

    public void Write(byte[] buf, int count) => PtyNative.write(_masterFd, buf, count);

    public void Resize(int rows, int cols)
    {
        _log?.LogDebug($"LinuxPtyBackend.Resize({rows}x{cols})", "PTY");
        PtyNative.Resize(_masterFd, rows, cols);
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _log?.LogDebug($"LinuxPtyBackend.Dispose: closing master fd {_masterFd}, waiting on shim pid {_shimProc.Id}", "PTY");
            try { PtyNative.close(_masterFd); } catch { }
            try { _shimProc.WaitForExit(500);  } catch { }
        }
    }
}
