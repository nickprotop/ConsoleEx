using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Windows ConPTY P/Invoke bindings (kernel32.dll).
/// Requires Windows 10 build 17763 (1809) or later.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WinPtyNative
{
    // ── Structures ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD { public short X; public short Y; }

    /// <summary>Matches the Win32 STARTUPINFOW layout exactly on both 32- and 64-bit.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public  int    cb;
        public  IntPtr lpReserved;
        public  IntPtr lpDesktop;
        public  IntPtr lpTitle;
        public  int    dwX;
        public  int    dwY;
        public  int    dwXSize;
        public  int    dwYSize;
        public  int    dwXCountChars;
        public  int    dwYCountChars;
        public  int    dwFillAttribute;
        public  int    dwFlags;
        public  short  wShowWindow;
        public  short  cbReserved2;
        public  IntPtr lpReserved2;
        public  IntPtr hStdInput;
        public  IntPtr hStdOutput;
        public  IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr      lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int    dwProcessId;
        public int    dwThreadId;
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>EXTENDED_STARTUPINFO_PRESENT — tells CreateProcess to use STARTUPINFOEX.</summary>
    public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

    /// <summary>ProcThreadAttributeValue(PseudoConsole=22, Thread=false, Input=true, Additive=false)</summary>
    public const nint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    public const uint INFINITE     = 0xFFFFFFFF;
    public const uint WAIT_TIMEOUT = 0x00000102;

    // ── Pipe APIs ─────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreatePipe(
        out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe,
        IntPtr             lpPipeAttributes,
        int                nSize);

    // ── ConPTY APIs ──────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int CreatePseudoConsole(
        COORD          size,
        SafeFileHandle hInput,
        SafeFileHandle hOutput,
        uint           dwFlags,
        out IntPtr     phPC);

    [DllImport("kernel32.dll")]
    public static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    // ── Process thread attributes ─────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool InitializeProcThreadAttributeList(
        IntPtr  lpAttributeList,
        int     dwAttributeCount,
        int     dwFlags,
        ref nint lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateProcThreadAttribute(
        IntPtr  lpAttributeList,
        uint    dwFlags,
        nint    Attribute,
        ref IntPtr lpValue,
        nint    cbSize,
        IntPtr  lpPreviousValue,
        IntPtr  lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    // ── CreateProcess ────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(
        string?              lpApplicationName,
        string               lpCommandLine,
        IntPtr               lpProcessAttributes,
        IntPtr               lpThreadAttributes,
        bool                 bInheritHandles,
        uint                 dwCreationFlags,
        IntPtr               lpEnvironment,
        string?              lpCurrentDirectory,
        ref STARTUPINFOEX    lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    // ── Handle utilities ─────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
}
