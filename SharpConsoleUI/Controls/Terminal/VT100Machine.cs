using Spectre.Console;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// A VT100/xterm-256color terminal emulator that renders output into a CharacterBuffer.
/// Handles: cursor motion, SGR colors (16/256/24-bit), erase ops, scroll regions,
/// DEC Special Graphics charset (box-drawing characters used by ncurses/htop),
/// UTF-8 multi-byte sequences, scrollback history, and alternate screen buffer.
/// Thread safety: all public methods must be called under the caller's lock.
/// </summary>
internal sealed class VT100Machine
{
    private CharacterBuffer _screen;
    private int _cx, _cy;            // cursor position (col, row)
    private int _savedCx, _savedCy; // saved cursor
    private int _scrollTop;          // inclusive top row of scroll region
    private int _scrollBottom;       // inclusive bottom row of scroll region

    // Current SGR render state
    private Color _fg, _bg;
    private readonly Color _defaultFg;
    private readonly Color _defaultBg;
    private bool _bold;

    // DEC Special Graphics charset state
    // G0/G1 charset slots: true = DEC Special Graphics, false = ASCII
    private bool _g0Decs = false;
    private bool _g1Decs = false;
    private bool _useG1  = false;  // SO (0x0E) selects G1, SI (0x0F) selects G0

    // UTF-8 multi-byte decoder state
    private readonly byte[] _utf8Buf  = new byte[4];
    private int              _utf8Pos  = 0;
    private int              _utf8Need = 0;

    // Scrollback ring buffer: stores lines that scroll off the top of the screen
    private readonly Cell[][] _scrollbackLines;
    private int _scrollbackHead;   // next write position in ring buffer
    private int _scrollbackCount;  // number of lines stored (0..capacity)

    // Alternate screen buffer (used by vim, btop, less, etc.)
    private bool _alternateScreen;
    private CharacterBuffer? _savedMainScreen;
    private int _savedMainCx, _savedMainCy;

    // Parser state machine
    private enum ParseState
    {
        Normal,
        Escape,
        Csi,
        CsiPrivate,
        OscString,
        DesigCharset,   // consuming the charset designator byte after ESC ( ) * +
    }
    private ParseState _state = ParseState.Normal;
    private int        _charsetSlot;   // 0 = G0, 1 = G1
    private readonly System.Text.StringBuilder _param = new();

    // Standard 16 ANSI colors (xterm default palette)
    private static readonly Color[] Ansi16 =
    [
        new Color(  0,   0,   0),  //  0 black
        new Color(128,   0,   0),  //  1 red
        new Color(  0, 128,   0),  //  2 green
        new Color(128, 128,   0),  //  3 yellow
        new Color(  0,   0, 128),  //  4 blue
        new Color(128,   0, 128),  //  5 magenta
        new Color(  0, 128, 128),  //  6 cyan
        new Color(192, 192, 192),  //  7 white
        new Color(128, 128, 128),  //  8 bright black (grey)
        new Color(255,   0,   0),  //  9 bright red
        new Color(  0, 255,   0),  // 10 bright green
        new Color(255, 255,   0),  // 11 bright yellow
        new Color(  0,   0, 255),  // 12 bright blue
        new Color(255,   0, 255),  // 13 bright magenta
        new Color(  0, 255, 255),  // 14 bright cyan
        new Color(255, 255, 255),  // 15 bright white
    ];

    public int Width  { get; private set; }
    public int Height { get; private set; }
    public CharacterBuffer Screen => _screen;

    // Cursor state exposed to the renderer
    public int  CursorX       => _cx;
    public int  CursorY       => _cy;
    public bool CursorVisible { get; private set; } = true;

    // DECCKM: application cursor keys mode (ESC[?1h sets, ESC[?1l clears)
    public bool AppCursorKeys { get; private set; } = false;

    // Mouse reporting mode: 0=off, 1000=button, 1002=drag, 1003=any
    public int  MouseMode { get; private set; } = 0;
    // SGR extended mouse encoding (ESC[?1006h)
    public bool MouseSgr  { get; private set; } = false;

    public int  ScrollbackCapacity { get; }
    public int  ScrollbackCount    => _scrollbackCount;
    public bool AlternateScreen    => _alternateScreen;

    public VT100Machine(int width, int height,
        Color? defaultFg = null, Color? defaultBg = null,
        int scrollbackCapacity = ControlDefaults.DefaultTerminalScrollbackLines)
    {
        Width  = width;
        Height = height;
        _defaultFg    = defaultFg ?? new Color(192, 192, 192);
        _defaultBg    = defaultBg ?? Color.Black;
        _fg           = _defaultFg;
        _bg           = _defaultBg;
        _scrollTop    = 0;
        _scrollBottom = height - 1;
        _screen = new CharacterBuffer(width, height, _defaultBg);

        ScrollbackCapacity = scrollbackCapacity;
        _scrollbackLines   = new Cell[scrollbackCapacity][];
    }

    /// <summary>
    /// Gets a scrollback line by index. Index 0 is the most recently scrolled-off line,
    /// index ScrollbackCount-1 is the oldest retained line.
    /// Returns null if index is out of range or the line's width doesn't match current width.
    /// </summary>
    public Cell[]? GetScrollbackLine(int index)
    {
        if (index < 0 || index >= _scrollbackCount) return null;
        // _scrollbackHead points to the next write slot.
        // Most recent line is at (_scrollbackHead - 1), oldest at (_scrollbackHead - _scrollbackCount).
        int pos = ((_scrollbackHead - 1 - index) % ScrollbackCapacity + ScrollbackCapacity) % ScrollbackCapacity;
        return _scrollbackLines[pos];
    }

    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth == Width && newHeight == Height) return;
        Width  = newWidth;
        Height = newHeight;
        _scrollTop    = 0;
        _scrollBottom = newHeight - 1;
        _screen.Resize(newWidth, newHeight);
        _cx = Math.Clamp(_cx, 0, newWidth  - 1);
        _cy = Math.Clamp(_cy, 0, newHeight - 1);
    }

    public void Process(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            ProcessByte(b);
    }

    // ── Parser ───────────────────────────────────────────────────────────────

    private void ProcessByte(byte b)
    {
        switch (_state)
        {
            case ParseState.Normal:      ProcessNormal(b);    break;
            case ParseState.Escape:      ProcessEscape(b);    break;
            case ParseState.Csi:
            case ParseState.CsiPrivate:  ProcessCsi(b);       break;
            case ParseState.OscString:
                if (b == 0x07)       _state = ParseState.Normal;  // BEL ends OSC
                else if (b == 0x1B) _state = ParseState.Escape;   // ESC \ (ST) — consume the \ via ProcessEscape
                break;
            case ParseState.DesigCharset:
                // Byte following ESC ( ) * + is the charset name:
                //   '0' = DEC Special Graphics,  'B' or 'A' = ASCII/Latin-1
                if (_charsetSlot == 0) _g0Decs = (b == '0');
                else                   _g1Decs = (b == '0');
                _state = ParseState.Normal;
                break;
        }
    }

    private void ProcessNormal(byte b)
    {
        switch (b)
        {
            case 0x1B: _state = ParseState.Escape; _utf8Pos = 0; _utf8Need = 0; break;
            case 0x0E: _useG1 = true;  break;  // SO – shift to G1 charset
            case 0x0F: _useG1 = false; break;  // SI – shift to G0 charset
            case (byte)'\r': _cx = 0; break;
            case (byte)'\n': LineFeed(); break;
            case (byte)'\b': if (_cx > 0) _cx--; break;
            case (byte)'\a': break; // bell – ignore
            case (byte)'\t':
                _cx = (_cx + 8) & ~7;
                if (_cx >= Width) _cx = Width - 1;
                break;
            default:
                if (b >= 0xC0)
                {
                    // Start of a multi-byte UTF-8 sequence
                    _utf8Buf[0] = b;
                    _utf8Pos    = 1;
                    _utf8Need   = b < 0xE0 ? 2 : b < 0xF0 ? 3 : 4;
                }
                else if (b >= 0x80 && _utf8Need > 0)
                {
                    // Continuation byte
                    _utf8Buf[_utf8Pos++] = b;
                    if (_utf8Pos == _utf8Need)
                    {
                        try
                        {
                            string s = System.Text.Encoding.UTF8.GetString(_utf8Buf, 0, _utf8Need);
                            foreach (char ch in s) WriteChar(ch);
                        }
                        catch { /* invalid sequence – silently drop */ }
                        _utf8Pos = _utf8Need = 0;
                    }
                }
                else if (b >= 0x20)
                {
                    // Plain ASCII printable
                    _utf8Pos = _utf8Need = 0;
                    WriteChar((char)b);
                }
                break;
        }
    }

    private void ProcessEscape(byte b)
    {
        _state = ParseState.Normal;
        switch (b)
        {
            case (byte)'[':
                _state = ParseState.Csi;
                _param.Clear();
                break;
            case (byte)']':
                _state = ParseState.OscString;
                break;
            // Charset designation: ESC ( ) * + followed by charset name byte
            case (byte)'(':
            case (byte)')':
            case (byte)'*':
            case (byte)'+':
                _charsetSlot = (b == '(') ? 0 : 1;
                _state = ParseState.DesigCharset;
                break;
            case (byte)'M': // reverse index
                if (_cy <= _scrollTop) ScrollDown(); else _cy--;
                break;
            case (byte)'7': _savedCx = _cx; _savedCy = _cy; break; // save cursor
            case (byte)'8': _cx = _savedCx; _cy = _savedCy; break;  // restore cursor
            case (byte)'=': break; // application keypad – ignore
            case (byte)'>': break; // normal keypad – ignore
            case (byte)'c': Reset(); break;  // RIS – full reset
        }
    }

    private void ProcessCsi(byte b)
    {
        if (b == (byte)'?' && _state == ParseState.Csi)
        {
            _state = ParseState.CsiPrivate;
            return;
        }

        // Parameter bytes: digits, ; and other 0x30–0x3F
        if (b >= 0x30 && b <= 0x3F) { _param.Append((char)b); return; }

        // Intermediate bytes 0x20–0x2F: ignore
        if (b >= 0x20 && b <= 0x2F) return;

        // Final byte 0x40–0x7E: dispatch
        if (b >= 0x40 && b <= 0x7E)
        {
            bool priv = _state == ParseState.CsiPrivate;
            DispatchCsi((char)b, _param.ToString(), priv);
            _param.Clear();
            _state = ParseState.Normal;
        }
    }

    private void DispatchCsi(char cmd, string param, bool priv)
    {
        int[] n = ParseInts(param);
        int p1 = n.Length > 0 ? n[0] : 0;
        int p2 = n.Length > 1 ? n[1] : 0;

        switch (cmd)
        {
            case 'A': MoveCursor(0, -Math.Max(1, p1)); break;
            case 'B': MoveCursor(0,  Math.Max(1, p1)); break;
            case 'C': MoveCursor( Math.Max(1, p1), 0); break;
            case 'D': MoveCursor(-Math.Max(1, p1), 0); break;
            case 'E': _cx = 0; MoveCursor(0,  Math.Max(1, p1)); break;
            case 'F': _cx = 0; MoveCursor(0, -Math.Max(1, p1)); break;
            case 'G': _cx = Math.Clamp((p1 < 1 ? 1 : p1) - 1, 0, Width - 1); break;

            case 'H': case 'f':
                _cy = Math.Clamp((p1 < 1 ? 1 : p1) - 1, 0, Height - 1);
                _cx = Math.Clamp((p2 < 1 ? 1 : p2) - 1, 0, Width  - 1);
                break;

            case 'J': EraseDisplay(p1); break;
            case 'K': EraseLine(p1);    break;

            case 'L': for (int i = 0; i < Math.Max(1, p1); i++) ScrollDown(); break;
            case 'M': for (int i = 0; i < Math.Max(1, p1); i++) ScrollUp();   break;
            case 'P': DeleteChars(Math.Max(1, p1)); break;
            case '@': InsertChars(Math.Max(1, p1)); break;

            case 'S': for (int i = 0; i < Math.Max(1, p1); i++) ScrollUp();   break;
            case 'T': for (int i = 0; i < Math.Max(1, p1); i++) ScrollDown(); break;

            case 'd': _cy = Math.Clamp((p1 < 1 ? 1 : p1) - 1, 0, Height - 1); break;

            case 'm': ApplySgr(n); break;

            case 'r':
                _scrollTop    = Math.Clamp((p1 < 1 ? 1 : p1) - 1, 0, Height - 1);
                _scrollBottom = Math.Clamp((p2 < 1 ? Height : p2) - 1, 0, Height - 1);
                if (_scrollTop >= _scrollBottom) { _scrollTop = 0; _scrollBottom = Height - 1; }
                _cx = 0; _cy = 0;
                break;

            case 's': _savedCx = _cx; _savedCy = _cy; break;
            case 'u': _cx = _savedCx; _cy = _savedCy; break;

            case 'h':
                if (priv) foreach (int code in n) SetPrivateMode(code, true);
                break;
            case 'l':
                if (priv) foreach (int code in n) SetPrivateMode(code, false);
                break;
        }
    }

    // ── Screen operations ────────────────────────────────────────────────────

    private void WriteChar(char ch)
    {
        if (_cx >= Width) { _cx = 0; LineFeed(); }

        // Apply DEC Special Graphics mapping when the active charset is DEC
        bool isDec = _useG1 ? _g1Decs : _g0Decs;
        if (isDec) ch = MapDecSpecial(ch);

        _screen.SetCell(_cx, _cy, ch, _fg, _bg);
        _cx++;
    }

    /// <summary>
    /// Maps DEC Special Graphics characters to their Unicode equivalents.
    /// Used when G0/G1 charset is set to '0' (DEC Special Graphics) by ESC(0.
    /// ncurses uses this for box-drawing in htop, vim, mc, etc.
    /// </summary>
    private static char MapDecSpecial(char ch) => ch switch
    {
        'j' => '┘', 'k' => '┐', 'l' => '┌', 'm' => '└',
        'n' => '┼', 'q' => '─', 't' => '├', 'u' => '┤',
        'v' => '┴', 'w' => '┬', 'x' => '│',
        '`' => '◆', 'a' => '▒', 'f' => '°', 'g' => '±',
        'h' => '░', 'o' => '⎺', 'p' => '⎻', 'r' => '⎼',
        's' => '⎽', '~' => '·',
        _ => ch,
    };

    private void LineFeed()
    {
        if (_cy >= _scrollBottom) ScrollUp(); else _cy++;
    }

    private void ScrollUp()
    {
        // Capture the top row into scrollback before it's overwritten,
        // but only for full-screen scrolls on the main screen buffer.
        if (_scrollTop == 0 && !_alternateScreen && ScrollbackCapacity > 0)
            CaptureTopRowToScrollback();

        for (int y = _scrollTop; y < _scrollBottom; y++)
            for (int x = 0; x < Width; x++)
            {
                var c = _screen.GetCell(x, y + 1);
                _screen.SetCell(x, y, c.Character, c.Foreground, c.Background);
            }
        FillRow(_scrollBottom);
    }

    private void CaptureTopRowToScrollback()
    {
        var row = new Cell[Width];
        for (int x = 0; x < Width; x++)
            row[x] = _screen.GetCell(x, _scrollTop);

        _scrollbackLines[_scrollbackHead] = row;
        _scrollbackHead = (_scrollbackHead + 1) % ScrollbackCapacity;
        if (_scrollbackCount < ScrollbackCapacity)
            _scrollbackCount++;
    }

    private void ScrollDown()
    {
        for (int y = _scrollBottom; y > _scrollTop; y--)
            for (int x = 0; x < Width; x++)
            {
                var c = _screen.GetCell(x, y - 1);
                _screen.SetCell(x, y, c.Character, c.Foreground, c.Background);
            }
        FillRow(_scrollTop);
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                EraseLine(0);
                for (int y = _cy + 1; y < Height; y++) FillRow(y);
                break;
            case 1:
                for (int y = 0; y < _cy; y++) FillRow(y);
                EraseLine(1);
                break;
            case 2: case 3:
                for (int y = 0; y < Height; y++) FillRow(y);
                break;
        }
    }

    private void EraseLine(int mode)
    {
        int start, end;
        switch (mode)
        {
            case 0: start = _cx; end = Width;   break;
            case 1: start = 0;   end = _cx + 1; break;
            default: start = 0;  end = Width;   break;
        }
        for (int x = start; x < end; x++)
            _screen.SetCell(x, _cy, ' ', _defaultFg, _defaultBg);
    }

    private void FillRow(int y)
    {
        for (int x = 0; x < Width; x++)
            _screen.SetCell(x, y, ' ', _defaultFg, _defaultBg);
    }

    private void DeleteChars(int count)
    {
        int remaining = Width - _cx - count;
        for (int i = 0; i < remaining; i++)
        {
            var c = _screen.GetCell(_cx + count + i, _cy);
            _screen.SetCell(_cx + i, _cy, c.Character, c.Foreground, c.Background);
        }
        for (int i = Math.Max(0, Width - count); i < Width; i++)
            _screen.SetCell(i, _cy, ' ', _defaultFg, _defaultBg);
    }

    private void InsertChars(int count)
    {
        for (int i = Width - 1; i >= _cx + count; i--)
        {
            var c = _screen.GetCell(i - count, _cy);
            _screen.SetCell(i, _cy, c.Character, c.Foreground, c.Background);
        }
        for (int i = _cx; i < Math.Min(_cx + count, Width); i++)
            _screen.SetCell(i, _cy, ' ', _defaultFg, _defaultBg);
    }

    private void MoveCursor(int dx, int dy)
    {
        _cx = Math.Clamp(_cx + dx, 0, Width  - 1);
        _cy = Math.Clamp(_cy + dy, 0, Height - 1);
    }

    private void SetPrivateMode(int code, bool set)
    {
        switch (code)
        {
            case 1:    AppCursorKeys = set;            break;  // DECCKM  – application cursor keys
            case 25:   CursorVisible = set;            break;  // DECTCEM – cursor visibility
            case 1000: MouseMode = set ? 1000 : 0;    break;  // X10/normal mouse reporting
            case 1002: MouseMode = set ? 1002 : 0;    break;  // button-event mouse tracking
            case 1003: MouseMode = set ? 1003 : 0;    break;  // any-event mouse tracking
            case 1006: MouseSgr  = set;                break;  // SGR extended coordinates

            case 47:
            case 1047:
                if (set) EnterAlternateScreen(saveCursor: false);
                else     LeaveAlternateScreen(restoreCursor: false);
                break;

            case 1049:
                // 1049 = save cursor + enter alt screen / leave alt screen + restore cursor
                if (set) EnterAlternateScreen(saveCursor: true);
                else     LeaveAlternateScreen(restoreCursor: true);
                break;
        }
    }

    private void EnterAlternateScreen(bool saveCursor)
    {
        if (_alternateScreen) return;
        _alternateScreen = true;

        if (saveCursor)
        {
            _savedMainCx = _cx;
            _savedMainCy = _cy;
        }

        // Save main screen contents
        _savedMainScreen = new CharacterBuffer(Width, Height, _defaultBg);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var c = _screen.GetCell(x, y);
                _savedMainScreen.SetCell(x, y, c.Character, c.Foreground, c.Background);
            }

        // Clear the screen for the alternate buffer
        for (int y = 0; y < Height; y++) FillRow(y);
    }

    private void LeaveAlternateScreen(bool restoreCursor)
    {
        if (!_alternateScreen) return;
        _alternateScreen = false;

        // Restore main screen contents
        if (_savedMainScreen != null)
        {
            int h = Math.Min(Height, _savedMainScreen.Height);
            int w = Math.Min(Width, _savedMainScreen.Width);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = _savedMainScreen.GetCell(x, y);
                    _screen.SetCell(x, y, c.Character, c.Foreground, c.Background);
                }
            _savedMainScreen = null;
        }

        if (restoreCursor)
        {
            _cx = Math.Clamp(_savedMainCx, 0, Width - 1);
            _cy = Math.Clamp(_savedMainCy, 0, Height - 1);
        }
    }

    private void Reset()
    {
        _cx = _cy = _savedCx = _savedCy = 0;
        _scrollTop    = 0;
        _scrollBottom = Height - 1;
        _g0Decs = _g1Decs = _useG1 = false;
        ResetSgr();
        for (int y = 0; y < Height; y++) FillRow(y);
    }

    // ── SGR color handling ───────────────────────────────────────────────────

    private void ApplySgr(int[] codes)
    {
        if (codes.Length == 0) { ResetSgr(); return; }

        for (int i = 0; i < codes.Length; i++)
        {
            int c = codes[i];
            switch (c)
            {
                case 0:  ResetSgr(); break;
                case 1:  _bold = true;  break;
                case 2:  break; // dim – ignore
                case 3:  break; // italic – ignore
                case 4:  break; // underline – ignore
                case 7:  // reverse video – swap fg/bg
                    (_fg, _bg) = (_bg, _fg); break;
                case 22: _bold = false; break;
                case 23: break; // italic off
                case 24: break; // underline off
                case 27: // reverse off – swap back (best-effort)
                    (_fg, _bg) = (_bg, _fg); break;
                case 39: _fg = _defaultFg; break;
                case 49: _bg = _defaultBg; break;

                case int x when x >= 30 && x <= 37:
                    _fg = GetAnsi16(x - 30, _bold); break;
                case int x when x >= 40 && x <= 47:
                    _bg = GetAnsi16(x - 40, false); break;
                case int x when x >= 90 && x <= 97:
                    _fg = Ansi16[x - 90 + 8]; break;
                case int x when x >= 100 && x <= 107:
                    _bg = Ansi16[x - 100 + 8]; break;

                case 38 when i + 2 < codes.Length && codes[i + 1] == 5:
                    _fg = Get256Color(codes[i + 2]); i += 2; break;
                case 48 when i + 2 < codes.Length && codes[i + 1] == 5:
                    _bg = Get256Color(codes[i + 2]); i += 2; break;
                case 38 when i + 4 < codes.Length && codes[i + 1] == 2:
                    _fg = new Color((byte)codes[i+2], (byte)codes[i+3], (byte)codes[i+4]);
                    i += 4; break;
                case 48 when i + 4 < codes.Length && codes[i + 1] == 2:
                    _bg = new Color((byte)codes[i+2], (byte)codes[i+3], (byte)codes[i+4]);
                    i += 4; break;
            }
        }
    }

    private void ResetSgr()
    {
        _fg = _defaultFg;
        _bg = _defaultBg;
        _bold = false;
    }

    private static Color GetAnsi16(int index, bool bold)
        => (bold && index < 8) ? Ansi16[index + 8] : Ansi16[index];

    private static Color Get256Color(int index)
    {
        if (index < 16) return Ansi16[index];
        if (index < 232)
        {
            int i = index - 16;
            int r = i / 36, g = (i % 36) / 6, b = i % 6;
            return new Color(
                (byte)(r > 0 ? 55 + r * 40 : 0),
                (byte)(g > 0 ? 55 + g * 40 : 0),
                (byte)(b > 0 ? 55 + b * 40 : 0));
        }
        byte v = (byte)(8 + (index - 232) * 10);
        return new Color(v, v, v);
    }

    private static int[] ParseInts(string param)
    {
        if (string.IsNullOrEmpty(param)) return [];
        var parts = param.Split(';');
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            int.TryParse(parts[i], out result[i]);
        return result;
    }
}
