using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Spectre.Console;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// A self-contained PTY-backed terminal control. The constructor opens the PTY,
/// spawns the shim process, and starts a background read thread. Add to any window
/// with .AddControl(). No WithAsyncWindowThread needed.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class TerminalControl
    : IWindowControl, IDOMPaintable, IInteractiveControl, IMouseAwareControl, IDisposable
{
    private readonly VT100Machine _vt;
    private readonly int _masterFd;
    private readonly Process _shimProc;
    private readonly Thread _readThread;
    private readonly object _lock = new();
    private int _disposed = 0;
    private int _actualX, _actualY, _actualWidth, _actualHeight;

    /// <summary>Window title derived from the launched executable name.</summary>
    public string Title { get; }

    internal TerminalControl(string exe, string[]? args)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("TerminalControl requires Linux.");

        int rows = 24;
        int cols = 80;
        _vt = new VT100Machine(cols, rows);
        (_masterFd, int slave) = PtyNative.Open(rows, cols);

        // Spawn shim: this-exe --pty-shim <slave> <exe> [args]
        var shimArgs = new List<string> { "--pty-shim", slave.ToString(), exe };
        if (args != null) shimArgs.AddRange(args);
        var psi = new ProcessStartInfo(Environment.ProcessPath ?? "/proc/self/exe")
            { UseShellExecute = false };
        psi.Environment["TERM"] = "xterm-256color";
        foreach (var a in shimArgs) psi.ArgumentList.Add(a);
        _shimProc = Process.Start(psi) ?? throw new InvalidOperationException("PTY shim failed");
        PtyNative.close(slave);  // parent closes its copy

        Title = $"  Terminal — {Path.GetFileName(exe)}";

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "PTY-read" };
        _readThread.Start();
    }

    private void ReadLoop()
    {
        var buf = new byte[4096];
        while (true)
        {
            int n = PtyNative.read(_masterFd, buf, buf.Length);
            if (n <= 0) break;
            lock (_lock) _vt.Process(buf.AsSpan(0, n));
            Container?.Invalidate(true);
        }
        // EOF or fd closed: clean up and close window
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            try { PtyNative.close(_masterFd); } catch { }
        try { _shimProc.WaitForExit(500); } catch { }
        (Container as Window)?.TryClose(force: true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            try { PtyNative.close(_masterFd); } catch { }
    }

    // ── IInteractiveControl ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool HasFocus   { get; set; } = true;
    /// <inheritdoc/>
    public bool IsEnabled  { get; set; } = true;

    /// <inheritdoc/>
    public bool ProcessKey(ConsoleKeyInfo key)
    {
        bool appCursor;
        lock (_lock) appCursor = _vt.AppCursorKeys;

        var bytes = EncodeKey(key, appCursor);
        if (bytes.Length == 0) return false;

        try { PtyNative.write(_masterFd, bytes, bytes.Length); }
        catch { /* master closed */ }
        return true;
    }

    // ── IMouseAwareControl ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool WantsMouseEvents  => true;
    /// <inheritdoc/>
    public bool CanFocusWithMouse => false;

#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseClick;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseDoubleClick;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseEnter;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseLeave;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public bool ProcessMouseEvent(MouseEventArgs args)
    {
        int mouseMode; bool mouseSgr;
        lock (_lock) { mouseMode = _vt.MouseMode; mouseSgr = _vt.MouseSgr; }

        if (mouseMode == 0) return false;

        int col = args.Position.X + 1;
        int row = args.Position.Y + 1;

        int mods = 0;
        if (args.HasFlag(MouseFlags.ButtonShift)) mods |= 4;
        if (args.HasFlag(MouseFlags.ButtonAlt))   mods |= 8;
        if (args.HasFlag(MouseFlags.ButtonCtrl))  mods |= 16;

        if (args.HasFlag(MouseFlags.WheeledUp))
            { SendMouse(64 | mods, col, row, false, mouseSgr); return true; }
        if (args.HasFlag(MouseFlags.WheeledDown))
            { SendMouse(65 | mods, col, row, false, mouseSgr); return true; }

        if (args.HasFlag(MouseFlags.Button1Pressed))
            { SendMouse(0 | mods, col, row, false, mouseSgr); return true; }
        if (args.HasFlag(MouseFlags.Button2Pressed))
            { SendMouse(1 | mods, col, row, false, mouseSgr); return true; }
        if (args.HasFlag(MouseFlags.Button3Pressed))
            { SendMouse(2 | mods, col, row, false, mouseSgr); return true; }

        if (args.HasFlag(MouseFlags.Button1Released))
            { SendMouse(0 | mods, col, row, true, mouseSgr); return true; }
        if (args.HasFlag(MouseFlags.Button2Released))
            { SendMouse(1 | mods, col, row, true, mouseSgr); return true; }
        if (args.HasFlag(MouseFlags.Button3Released))
            { SendMouse(2 | mods, col, row, true, mouseSgr); return true; }

        if (mouseMode >= 1002)
        {
            if (args.HasFlag(MouseFlags.Button1Dragged))
                { SendMouse(32 | mods, col, row, false, mouseSgr); return true; }
            if (args.HasFlag(MouseFlags.Button2Dragged))
                { SendMouse(33 | mods, col, row, false, mouseSgr); return true; }
            if (args.HasFlag(MouseFlags.Button3Dragged))
                { SendMouse(34 | mods, col, row, false, mouseSgr); return true; }
        }

        if (mouseMode >= 1003 && args.HasFlag(MouseFlags.ReportMousePosition))
            { SendMouse(35 | mods, col, row, false, mouseSgr); return true; }

        return false;
    }

    private void SendMouse(int btn, int col, int row, bool release, bool sgr)
    {
        byte[] seq;
        if (sgr)
        {
            char suffix = release ? 'm' : 'M';
            seq = Encoding.ASCII.GetBytes($"\x1b[<{btn};{col};{row}{suffix}");
        }
        else
        {
            if (col > 222 || row > 222) return;
            int b = release ? 3 : btn;
            seq = [0x1B, (byte)'[', (byte)'M',
                   (byte)(32 + b), (byte)(32 + col), (byte)(32 + row)];
        }
        try { PtyNative.write(_masterFd, seq, seq.Length); } catch { }
    }

    // ── IWindowControl ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public int?                ContentWidth        { get; } = null;
    /// <inheritdoc/>
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Stretch;
    /// <inheritdoc/>
    public VerticalAlignment   VerticalAlignment   { get; set; } = VerticalAlignment.Fill;
    /// <inheritdoc/>
    public IContainer?         Container           { get; set; }
    /// <inheritdoc/>
    public Margin              Margin              { get; set; } = new Margin(0, 0, 0, 0);
    /// <inheritdoc/>
    public StickyPosition      StickyPosition      { get; set; } = StickyPosition.None;
    /// <inheritdoc/>
    public string?             Name                { get; set; }
    /// <inheritdoc/>
    public object?             Tag                 { get; set; }
    /// <inheritdoc/>
    public bool                Visible             { get; set; } = true;
    /// <inheritdoc/>
    public int?                Width               { get; set; }

    /// <inheritdoc/>
    public int ActualX      => _actualX;
    /// <inheritdoc/>
    public int ActualY      => _actualY;
    /// <inheritdoc/>
    public int ActualWidth  => _actualWidth;
    /// <inheritdoc/>
    public int ActualHeight => _actualHeight;

    /// <inheritdoc/>
    public System.Drawing.Size GetLogicalContentSize()
        => new(_vt.Width, _vt.Height);

    /// <inheritdoc/>
    public void Invalidate() => Container?.Invalidate(true);

    // ── IDOMPaintable ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public LayoutSize MeasureDOM(LayoutConstraints constraints)
        => new(
            Math.Clamp(_vt.Width,  constraints.MinWidth,  constraints.MaxWidth),
            Math.Clamp(_vt.Height, constraints.MinHeight, constraints.MaxHeight));

    /// <inheritdoc/>
    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
                         Color defaultFg, Color defaultBg)
    {
        _actualX      = bounds.X;
        _actualY      = bounds.Y;
        _actualWidth  = bounds.Width;
        _actualHeight = bounds.Height;

        lock (_lock)
        {
            if (bounds.Width != _vt.Width || bounds.Height != _vt.Height)
            {
                _vt.Resize(bounds.Width, bounds.Height);
                PtyNative.Resize(_masterFd, bounds.Height, bounds.Width);
            }

            buffer.CopyFrom(
                _vt.Screen,
                new LayoutRect(0, 0,
                    Math.Min(bounds.Width,  _vt.Width),
                    Math.Min(bounds.Height, _vt.Height)),
                bounds.X, bounds.Y);

            if (_vt.CursorVisible)
            {
                int cx = _vt.CursorX, cy = _vt.CursorY;
                if (cx < bounds.Width && cy < bounds.Height)
                {
                    var cell = _vt.Screen.GetCell(cx, cy);
                    buffer.SetCell(bounds.X + cx, bounds.Y + cy,
                                   cell.Character, cell.Background, cell.Foreground);
                }
            }
        }
    }

    // ── Key encoding: ConsoleKeyInfo → xterm-256color escape sequences ───────

    private static byte[] EncodeKey(ConsoleKeyInfo key, bool appCursorKeys)
    {
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            return key.Key switch
            {
                ConsoleKey.C => [0x03],
                ConsoleKey.D => [0x04],
                ConsoleKey.Z => [0x1A],
                ConsoleKey.L => [0x0C],
                ConsoleKey.A => [0x01],
                ConsoleKey.E => [0x05],
                ConsoleKey.U => [0x15],
                ConsoleKey.W => [0x17],
                ConsoleKey.K => [0x0B],
                ConsoleKey.R => [0x12],
                _ => EncodeChar(key.KeyChar),
            };
        }

        return key.Key switch
        {
            ConsoleKey.Enter     => [0x0D],
            ConsoleKey.Backspace => [0x7F],
            ConsoleKey.Tab       => [0x09],
            ConsoleKey.Escape    => [0x1B],

            ConsoleKey.UpArrow    => appCursorKeys ? [0x1B, (byte)'O', (byte)'A'] : [0x1B, (byte)'[', (byte)'A'],
            ConsoleKey.DownArrow  => appCursorKeys ? [0x1B, (byte)'O', (byte)'B'] : [0x1B, (byte)'[', (byte)'B'],
            ConsoleKey.RightArrow => appCursorKeys ? [0x1B, (byte)'O', (byte)'C'] : [0x1B, (byte)'[', (byte)'C'],
            ConsoleKey.LeftArrow  => appCursorKeys ? [0x1B, (byte)'O', (byte)'D'] : [0x1B, (byte)'[', (byte)'D'],

            ConsoleKey.Home   => [0x1B, (byte)'O', (byte)'H'],
            ConsoleKey.End    => [0x1B, (byte)'O', (byte)'F'],

            ConsoleKey.Delete   => [0x1B, (byte)'[', (byte)'3', (byte)'~'],
            ConsoleKey.PageUp   => [0x1B, (byte)'[', (byte)'5', (byte)'~'],
            ConsoleKey.PageDown => [0x1B, (byte)'[', (byte)'6', (byte)'~'],
            ConsoleKey.Insert   => [0x1B, (byte)'[', (byte)'2', (byte)'~'],

            ConsoleKey.F1  => [0x1B, (byte)'O', (byte)'P'],
            ConsoleKey.F2  => [0x1B, (byte)'O', (byte)'Q'],
            ConsoleKey.F3  => [0x1B, (byte)'O', (byte)'R'],
            ConsoleKey.F4  => [0x1B, (byte)'O', (byte)'S'],
            ConsoleKey.F5  => [0x1B, (byte)'[', (byte)'1', (byte)'5', (byte)'~'],
            ConsoleKey.F6  => [0x1B, (byte)'[', (byte)'1', (byte)'7', (byte)'~'],
            ConsoleKey.F7  => [0x1B, (byte)'[', (byte)'1', (byte)'8', (byte)'~'],
            ConsoleKey.F8  => [0x1B, (byte)'[', (byte)'1', (byte)'9', (byte)'~'],
            ConsoleKey.F9  => [0x1B, (byte)'[', (byte)'2', (byte)'0', (byte)'~'],
            ConsoleKey.F10 => [0x1B, (byte)'[', (byte)'2', (byte)'1', (byte)'~'],
            ConsoleKey.F11 => [0x1B, (byte)'[', (byte)'2', (byte)'3', (byte)'~'],
            ConsoleKey.F12 => [0x1B, (byte)'[', (byte)'2', (byte)'4', (byte)'~'],

            _ => EncodeChar(key.KeyChar),
        };
    }

    private static byte[] EncodeChar(char ch)
        => ch != 0 ? Encoding.UTF8.GetBytes([ch]) : [];
}
