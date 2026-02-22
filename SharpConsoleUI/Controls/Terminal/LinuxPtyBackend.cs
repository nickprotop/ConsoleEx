using System.Diagnostics;
using System.Runtime.Versioning;

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
    private int _disposed = 0;

    public LinuxPtyBackend(string exe, string[]? args, int rows, int cols, string? workingDirectory = null)
    {
        (_masterFd, int slave) = PtyNative.Open(rows, cols);

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
    }

    public int Read(byte[] buf, int count) => PtyNative.read(_masterFd, buf, count);

    public void Write(byte[] buf, int count) => PtyNative.write(_masterFd, buf, count);

    public void Resize(int rows, int cols) => PtyNative.Resize(_masterFd, rows, cols);

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try { PtyNative.close(_masterFd); } catch { }
            try { _shimProc.WaitForExit(500);  } catch { }
        }
    }
}
