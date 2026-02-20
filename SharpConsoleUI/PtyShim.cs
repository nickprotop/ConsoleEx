using SharpConsoleUI.Controls.Terminal;

namespace SharpConsoleUI;

public static class PtyShim
{
    /// <summary>
    /// If args start with --pty-shim, sets up slave PTY and exec's the target.
    /// On success never returns. Returns false if not in shim mode or not on Linux.
    /// </summary>
    public static bool RunIfShim(string[] args)
    {
        if (!OperatingSystem.IsLinux()) return false;
        if (args.Length < 3 || args[0] != "--pty-shim") return false;
        int slaveFd = int.Parse(args[1]);
        string exe  = args[2];
        string?[] argv = [.. args[2..].Cast<string?>(), null];
        PtyNative.setsid();
        PtyNative.ioctl(slaveFd, PtyNative.TIOCSCTTY, 0);
        PtyNative.dup2(slaveFd, 0);
        PtyNative.dup2(slaveFd, 1);
        PtyNative.dup2(slaveFd, 2);
        if (slaveFd > 2) PtyNative.close(slaveFd);
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");
        PtyNative.execvp(exe, argv);
        return true;
    }
}
