using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Windows ConPTY backend.
/// Uses <c>CreatePseudoConsole</c> (kernel32) and two anonymous pipes for I/O.
/// Requires Windows 10 build 17763 (October 2018 Update) or later.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPtyBackend : IPtyBackend
{
    private IntPtr  _hPcon    = IntPtr.Zero;
    private IntPtr  _hProcess = IntPtr.Zero;
    private Stream? _inputStream;   // parent writes keyboard input here
    private Stream? _outputStream;  // parent reads terminal output from here
    private int _disposed = 0;

    public WindowsPtyBackend(string exe, string[]? args, int rows, int cols, string? workingDirectory = null)
    {
        // ── 1. Create anonymous pipes ─────────────────────────────────────────
        //   Input pipe:  parent writes → ConPTY reads (keyboard input)
        //   Output pipe: ConPTY writes → parent reads (terminal output)
        if (!WinPtyNative.CreatePipe(out var inRead,  out var inWrite,  IntPtr.Zero, 0))
            throw new InvalidOperationException(
                $"CreatePipe (input) failed: {Marshal.GetLastWin32Error()}");
        if (!WinPtyNative.CreatePipe(out var outRead, out var outWrite, IntPtr.Zero, 0))
            throw new InvalidOperationException(
                $"CreatePipe (output) failed: {Marshal.GetLastWin32Error()}");

        // ── 2. Create the ConPTY ──────────────────────────────────────────────
        //   ConPTY takes ownership of inRead and outWrite (the PTY-side handles).
        var size = new WinPtyNative.COORD { X = (short)cols, Y = (short)rows };
        int hr = WinPtyNative.CreatePseudoConsole(size, inRead, outWrite, 0, out _hPcon);
        if (hr != 0)
            throw new InvalidOperationException(
                $"CreatePseudoConsole failed: HRESULT 0x{hr:X8}");

        // Parent no longer needs the PTY-side handles; ConPTY owns them now.
        inRead.Dispose();
        outWrite.Dispose();

        // ── 3. Wrap parent-side handles as streams ───────────────────────────
        _inputStream  = new FileStream(inWrite,  FileAccess.Write, bufferSize: 4096);
        _outputStream = new FileStream(outRead,  FileAccess.Read,  bufferSize: 4096);

        // ── 4. Build STARTUPINFOEX with PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE ──
        var siEx = new WinPtyNative.STARTUPINFOEX();
        siEx.StartupInfo.cb = Marshal.SizeOf<WinPtyNative.STARTUPINFOEX>();

        // Get the required size for the attribute list, then allocate and initialise.
        nint attrListSize = 0;
        WinPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
        var attrList = Marshal.AllocHGlobal(attrListSize);
        try
        {
            if (!WinPtyNative.InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
                throw new InvalidOperationException(
                    $"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

            // hPconValue must stay alive (on the stack) until after CreateProcess.
            // IntPtr is a value type — not moved by the GC — so ref is safe here.
            IntPtr hPconValue = _hPcon;
            if (!WinPtyNative.UpdateProcThreadAttribute(
                    attrList, 0,
                    WinPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    ref hPconValue,
                    (nint)IntPtr.Size,
                    IntPtr.Zero, IntPtr.Zero))
                throw new InvalidOperationException(
                    $"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

            siEx.lpAttributeList = attrList;

            // ── 5. Start the child process ────────────────────────────────────
            string cmdLine = BuildCommandLine(exe, args);
            if (!WinPtyNative.CreateProcess(
                    null, cmdLine,
                    IntPtr.Zero, IntPtr.Zero,
                    false,
                    WinPtyNative.EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero, workingDirectory,
                    ref siEx, out var pi))
            {
                throw new InvalidOperationException(
                    $"CreateProcess({exe}) failed: {Marshal.GetLastWin32Error()}");
            }

            _hProcess = pi.hProcess;
            WinPtyNative.CloseHandle(pi.hThread); // we don't need the thread handle
        }
        finally
        {
            WinPtyNative.DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrList);
        }
    }

    // ── IPtyBackend ──────────────────────────────────────────────────────────

    public int Read(byte[] buf, int count)
    {
        if (_outputStream == null) return 0;
        try   { return _outputStream.Read(buf, 0, count); }
        catch { return 0; }
    }

    public void Write(byte[] buf, int count)
    {
        if (_inputStream == null) return;
        try { _inputStream.Write(buf, 0, count); _inputStream.Flush(); }
        catch { /* process exited / pipe broken */ }
    }

    public void Resize(int rows, int cols)
    {
        if (_hPcon == IntPtr.Zero) return;
        WinPtyNative.ResizePseudoConsole(
            _hPcon,
            new WinPtyNative.COORD { X = (short)cols, Y = (short)rows });
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Close input first — signals EOF to the child's stdin.
        try { _inputStream?.Dispose(); } catch { }
        _inputStream = null;

        // Close the ConPTY — this closes its internal outWrite handle, which causes
        // the output pipe to deliver EOF to our Read loop.
        if (_hPcon != IntPtr.Zero)
        {
            WinPtyNative.ClosePseudoConsole(_hPcon);
            _hPcon = IntPtr.Zero;
        }

        // Close the output stream — may interrupt any blocked Read().
        try { _outputStream?.Dispose(); } catch { }
        _outputStream = null;

        // Wait briefly for the process, then close its handle.
        if (_hProcess != IntPtr.Zero)
        {
            WinPtyNative.WaitForSingleObject(_hProcess, 1000);
            WinPtyNative.CloseHandle(_hProcess);
            _hProcess = IntPtr.Zero;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a properly-quoted Windows command line string.
    /// Follows the CommandLineToArgvW quoting rules.
    /// </summary>
    private static string BuildCommandLine(string exe, string[]? args)
    {
        var sb = new StringBuilder();
        AppendQuoted(sb, exe);
        if (args != null)
            foreach (var a in args) { sb.Append(' '); AppendQuoted(sb, a); }
        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string arg)
    {
        // No special characters — no quoting needed.
        if (arg.Length > 0 && !arg.AsSpan().ContainsAny(" \t\""))
        {
            sb.Append(arg);
            return;
        }

        sb.Append('"');
        for (int i = 0; i < arg.Length; )
        {
            // Count consecutive backslashes.
            int bs = 0;
            while (i < arg.Length && arg[i] == '\\') { bs++; i++; }

            if (i == arg.Length)
            {
                // Trailing backslashes before closing quote must be doubled.
                sb.Append('\\', bs * 2);
            }
            else if (arg[i] == '"')
            {
                // Backslashes before a literal quote must be doubled, plus escape the quote.
                sb.Append('\\', bs * 2 + 1);
                sb.Append('"');
                i++;
            }
            else
            {
                sb.Append('\\', bs);
                sb.Append(arg[i++]);
            }
        }
        sb.Append('"');
    }
}
