using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Linux P/Invoke bindings for PTY management and process exec.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class PtyNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Winsize
    {
        public ushort ws_row, ws_col, ws_xpixel, ws_ypixel;
    }

    // openpty: allocates master+slave fd pair
    [DllImport("libc", SetLastError = true)]
    private static extern int openpty(out int master, out int slave,
        IntPtr name, IntPtr termios, ref Winsize ws);

    // ioctl overloads
    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, ref Winsize ws);
    [DllImport("libc", SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, int arg);

    // fcntl for FD_CLOEXEC control
    [DllImport("libc", SetLastError = true)]
    private static extern int fcntl(int fd, int cmd, int arg);

    // PTY session setup (used in shim)
    [DllImport("libc", SetLastError = true)]
    public static extern int setsid();
    [DllImport("libc", SetLastError = true)]
    public static extern int dup2(int oldfd, int newfd);
    [DllImport("libc", SetLastError = true)]
    public static extern int close(int fd);

    // exec (replaces current process image)
    [DllImport("libc", SetLastError = true)]
    public static extern int execvp(string file, string?[] argv);

    // Raw fd I/O (used for PTY master reads/writes)
    [DllImport("libc", SetLastError = true)]
    public static extern int read(int fd, byte[] buf, int count);
    [DllImport("libc", SetLastError = true)]
    public static extern int write(int fd, byte[] buf, int count);

    private const int F_SETFD = 2;
    private const int FD_CLOEXEC = 1;

    public const ulong TIOCSWINSZ = 0x5414;  // Linux x86_64
    public const ulong TIOCSCTTY  = 0x540E;  // Linux x86_64

    /// <summary>
    /// Opens a PTY pair. Master is close-on-exec; slave is inheritable by the shim child.
    /// </summary>
    public static (int master, int slave) Open(int rows, int cols)
    {
        var ws = new Winsize { ws_row = (ushort)rows, ws_col = (ushort)cols };
        if (openpty(out int master, out int slave, IntPtr.Zero, IntPtr.Zero, ref ws) != 0)
            throw new InvalidOperationException(
                $"openpty failed: errno={Marshal.GetLastPInvokeError()}");

        fcntl(master, F_SETFD, FD_CLOEXEC); // master: shim child must NOT inherit
        fcntl(slave,  F_SETFD, 0);          // slave:  shim child MUST inherit

        return (master, slave);
    }

    /// <summary>
    /// Sends TIOCSWINSZ to the master fd, which resizes the PTY and delivers SIGWINCH
    /// to the foreground process group automatically.
    /// </summary>
    public static void Resize(int master, int rows, int cols)
    {
        var ws = new Winsize { ws_row = (ushort)rows, ws_col = (ushort)cols };
        ioctl(master, TIOCSWINSZ, ref ws);
    }
}
