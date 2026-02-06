# MultilineEditControl - Bug Fix & Enhancement Plan

> **Generated**: 2026-02-06
> **Analyzed by**: 4-agent review team (rendering, input, markup safety, feature gap)
> **Source file**: `SharpConsoleUI/Controls/MultilineEditControl.cs` (2174 lines)
> **Builder file**: `SharpConsoleUI/Builders/MultilineEditControlBuilder.cs` (565 lines)

---

## Summary

| Category | Count |
|----------|-------|
| Critical bugs | 6 |
| Major bugs | 4 |
| Moderate bugs | 10 |
| Significant bugs | 4 |
| Minor bugs | 13 |
| P0 missing features (essential) | 8 |
| P1 missing features (expected) | 17 |
| P2 missing features (advanced) | 19 |

---

## Dependency Graph

```
Phase 1 (WrapWords foundation)
  └──> Phase 2 (Critical input bugs)       -- depends on correct wrapping
        └──> Phase 3 (P0 features)         -- depends on mouse click positioning
              └──> Phase 4 (Undo/Redo)     -- depends on working edit operations
Phase 5 (Unicode) ---- can run parallel to Phase 3-4
Phase 6 (Events/API) - after Phase 2-4
Phase 7 (Performance) - after Phase 1 (wrapping cache)
Phase 8 (Advanced) --- after Phase 1-6
```

---

## Phase 1: Fix WrapWords Foundation

> **Status**: NOT STARTED
> **Priority**: CRITICAL - prerequisite for all other phases
> **Bugs fixed**: R1, R2, R3, R6, R16, R17, R18, I2, I7, M9

### The Core Problem

PaintDOM (lines 1850-1937) builds wrapped lines correctly using word-boundary splitting.
But ALL other code uses `_cursorX / effectiveWidth` character-based division:
- `GetTotalWrappedLineCount()` (line 1527)
- `EnsureCursorVisible()` (line 576-586)
- `GetLogicalCursorPosition()` (lines 1647, 1651, 1657)
- Up/Down/Home/End/PageUp/PageDown in `ProcessKey`

This means cursor, scrolling, selection, and measurement are ALL wrong in WrapWords mode.

### Implementation Guide

#### 1.1 Create the shared wrapping infrastructure

Add new fields and a data structure to the control. The key idea: build the wrapped
lines list ONCE, cache it, and use it everywhere (PaintDOM, cursor nav, scrolling, etc.).

**Add to `Configuration/ControlDefaults.cs`:**
```csharp
// MultilineEditControl defaults
public const int DefaultEditorWidth = 80;
public const int DefaultEditorViewportHeight = 10;
public const int DefaultScrollWheelLines = 3;
public const int DefaultTabSize = 4;
public const int DefaultUndoLimit = 100;
```

**New fields in MultilineEditControl (add near existing fields):**
```csharp
// Wrapping cache - invalidated on content change, resize, or wrap mode change
private List<WrappedLineInfo>? _wrappedLinesCache;
private int _wrappedLinesCacheWidth = -1; // effectiveWidth when cache was built

/// <summary>
/// Represents a single visual line after wrapping a source line.
/// </summary>
private readonly record struct WrappedLineInfo(
    int SourceLineIndex,   // index into _lines
    int SourceCharOffset,  // char offset within _lines[SourceLineIndex]
    int Length,            // number of chars from source line in this wrapped line
    string DisplayText     // the actual text to display (may differ from source slice in WrapWords due to space handling)
);
```

**The shared wrapping method (replaces lines 1850-1937 in PaintDOM and all other calcs):**
```csharp
private List<WrappedLineInfo> GetWrappedLines(int effectiveWidth)
{
    // Return cache if valid
    if (_wrappedLinesCache != null && _wrappedLinesCacheWidth == effectiveWidth)
        return _wrappedLinesCache;

    var result = new List<WrappedLineInfo>();
    int safeWidth = Math.Max(1, effectiveWidth);

    for (int i = 0; i < _lines.Count; i++)
    {
        string line = _lines[i];

        if (_wrapMode == WrapMode.NoWrap)
        {
            result.Add(new WrappedLineInfo(i, 0, line.Length, line));
        }
        else if (_wrapMode == WrapMode.Wrap)
        {
            if (line.Length == 0)
            {
                result.Add(new WrappedLineInfo(i, 0, 0, string.Empty));
            }
            else
            {
                for (int j = 0; j < line.Length; j += safeWidth)
                {
                    int len = Math.Min(safeWidth, line.Length - j);
                    result.Add(new WrappedLineInfo(i, j, len, line.Substring(j, len)));
                }
            }
        }
        else // WrapWords
        {
            BuildWordWrappedLines(result, line, i, safeWidth);
        }
    }

    _wrappedLinesCache = result;
    _wrappedLinesCacheWidth = effectiveWidth;
    return result;
}

/// <summary>
/// Word-wrap a single source line. Preserves original spacing (fixes R16: space collapsing).
/// Fixes R6: no phantom empty line after long words.
/// </summary>
private static void BuildWordWrappedLines(List<WrappedLineInfo> result, string line, int sourceIndex, int width)
{
    if (string.IsNullOrEmpty(line))
    {
        result.Add(new WrappedLineInfo(sourceIndex, 0, 0, string.Empty));
        return;
    }

    int pos = 0;
    while (pos < line.Length)
    {
        // How many chars fit on this visual line?
        int remaining = line.Length - pos;
        if (remaining <= width)
        {
            // Rest of line fits on one visual line
            result.Add(new WrappedLineInfo(sourceIndex, pos, remaining, line.Substring(pos, remaining)));
            break;
        }

        // Find the last space within [pos, pos+width) to break at
        int breakAt = -1;
        for (int j = pos + width - 1; j > pos; j--)
        {
            if (line[j] == ' ')
            {
                breakAt = j;
                break;
            }
        }

        if (breakAt > pos)
        {
            // Break at word boundary (include the space in this line)
            int len = breakAt - pos + 1;
            result.Add(new WrappedLineInfo(sourceIndex, pos, len, line.Substring(pos, len)));
            pos += len;
        }
        else
        {
            // No space found - force break at width (long word)
            result.Add(new WrappedLineInfo(sourceIndex, pos, width, line.Substring(pos, width)));
            pos += width;
        }
    }
}
```

**Cache invalidation - call this whenever content, wrap mode, or effective width changes:**
```csharp
private void InvalidateWrappedLinesCache()
{
    _wrappedLinesCache = null;
}
```

Add `InvalidateWrappedLinesCache()` calls to:
- `SetContent()`, `SetContentLines()`, `InsertText()`, `AppendContent()`, `AppendContentLines()`
- `WrapMode` setter
- `DeleteSelectedText()`
- Backspace handler, Delete handler, Enter handler, character insertion in `ProcessKey`
- Beginning of `PaintDOM` (if `_effectiveWidth` changed since last cache)

#### 1.2 Replace GetTotalWrappedLineCount

**Current (line 1515-1530):**
```csharp
// WRONG for WrapWords - uses (len-1)/safeWidth + 1
```

**Replace with:**
```csharp
private int GetTotalWrappedLineCount()
{
    if (_wrapMode == WrapMode.NoWrap)
        return _lines.Count;
    return GetWrappedLines(SafeEffectiveWidth).Count;
}
```

#### 1.3 Find which wrapped line contains the cursor

This is the key helper needed by EnsureCursorVisible, GetLogicalCursorPosition, Up/Down, Home/End, PageUp/PageDown:

```csharp
/// <summary>
/// Finds the index into the wrapped lines list for the current cursor position.
/// Returns -1 if not found.
/// </summary>
private int FindWrappedLineForCursor(List<WrappedLineInfo> wrappedLines)
{
    for (int i = 0; i < wrappedLines.Count; i++)
    {
        var wl = wrappedLines[i];
        if (wl.SourceLineIndex != _cursorY) continue;

        int endOffset = wl.SourceCharOffset + wl.Length;

        // Cursor is within this wrapped line
        if (_cursorX >= wl.SourceCharOffset && _cursorX < endOffset)
            return i;

        // Cursor is at the end of the last wrapped line for this source line
        if (_cursorX == endOffset)
        {
            // Check if next wrapped line belongs to same source line
            bool nextIsSameLine = (i + 1 < wrappedLines.Count &&
                                   wrappedLines[i + 1].SourceLineIndex == _cursorY);
            if (!nextIsSameLine)
                return i;
        }
    }
    return -1;
}

/// <summary>
/// Returns the horizontal offset within a wrapped line for the current cursor position.
/// </summary>
private int GetCursorOffsetInWrappedLine(WrappedLineInfo wrappedLine)
{
    return _cursorX - wrappedLine.SourceCharOffset;
}
```

#### 1.4 Rewrite EnsureCursorVisible (lines 564-627)

Replace the wrap-mode section (lines 572-603) with:
```csharp
if (_wrapMode != WrapMode.NoWrap)
{
    var wrappedLines = GetWrappedLines(effectiveWidth);
    int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
    if (wrappedIndex >= 0)
    {
        if (wrappedIndex < _verticalScrollOffset)
            _verticalScrollOffset = wrappedIndex;
        else if (wrappedIndex >= _verticalScrollOffset + _viewportHeight)
            _verticalScrollOffset = wrappedIndex - _viewportHeight + 1;
    }
    _horizontalScrollOffset = 0; // No horizontal scroll in wrap mode
}
// else: keep existing NoWrap logic unchanged
```

#### 1.5 Rewrite GetLogicalCursorPosition (lines 1619-1663)

Replace wrap-mode section (lines 1639-1662) with:
```csharp
else
{
    var wrappedLines = GetWrappedLines(effectiveWidth);
    int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
    if (wrappedIndex < 0) return null;

    int visualY = wrappedIndex - _verticalScrollOffset;
    int visualX = _cursorX - wrappedLines[wrappedIndex].SourceCharOffset;

    var pos = new Point(_margin.Left + visualX, _margin.Top + visualY);
    return pos;
}
```

#### 1.6 Rewrite Up/Down arrow navigation (lines 960-1046)

The pattern: find current wrapped line index, move up/down by 1, map back to source position.

```csharp
case ConsoleKey.UpArrow:
    if (_wrapMode != WrapMode.NoWrap)
    {
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
        if (wrappedIndex > 0)
        {
            var currentWl = wrappedLines[wrappedIndex];
            int offsetInLine = _cursorX - currentWl.SourceCharOffset;
            var prevWl = wrappedLines[wrappedIndex - 1];
            // Keep same horizontal offset, clamped to prev wrapped line length
            _cursorY = prevWl.SourceLineIndex;
            _cursorX = Math.Min(prevWl.SourceCharOffset + offsetInLine,
                               prevWl.SourceCharOffset + prevWl.Length);
            // Don't go past end of source line
            _cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
        }
    }
    else { /* existing NoWrap logic unchanged */ }
    break;

case ConsoleKey.DownArrow:
    if (_wrapMode != WrapMode.NoWrap)
    {
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
        if (wrappedIndex >= 0 && wrappedIndex < wrappedLines.Count - 1)
        {
            var currentWl = wrappedLines[wrappedIndex];
            int offsetInLine = _cursorX - currentWl.SourceCharOffset;
            var nextWl = wrappedLines[wrappedIndex + 1];
            _cursorY = nextWl.SourceLineIndex;
            _cursorX = Math.Min(nextWl.SourceCharOffset + offsetInLine,
                               nextWl.SourceCharOffset + nextWl.Length);
            _cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
        }
    }
    else { /* existing NoWrap logic unchanged */ }
    break;
```

#### 1.7 Rewrite Home/End for wrap mode (lines 1057-1094)

```csharp
case ConsoleKey.Home:
    if (isCtrlPressed)
    {
        _cursorX = 0; _cursorY = 0; // document start - unchanged
    }
    else if (_wrapMode != WrapMode.NoWrap)
    {
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
        if (wrappedIndex >= 0)
            _cursorX = wrappedLines[wrappedIndex].SourceCharOffset;
    }
    else { _cursorX = 0; }
    break;

case ConsoleKey.End:
    if (isCtrlPressed)
    {
        _cursorY = _lines.Count - 1; // document end - unchanged
        _cursorX = _lines[_cursorY].Length;
    }
    else if (_wrapMode != WrapMode.NoWrap)
    {
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
        if (wrappedIndex >= 0)
        {
            var wl = wrappedLines[wrappedIndex];
            int endPos = wl.SourceCharOffset + wl.Length;
            // If not last wrapped segment of this line, put cursor at end of segment
            // (but not past source line end)
            _cursorX = Math.Min(endPos, _lines[_cursorY].Length);
        }
    }
    else { _cursorX = _lines[_cursorY].Length; }
    break;
```

#### 1.8 Rewrite PageUp/PageDown for wrap mode (lines 1097-1185)

Same pattern: find wrapped index, move by `_viewportHeight` wrapped lines, map back.

```csharp
case ConsoleKey.PageUp:
    if (_wrapMode != WrapMode.NoWrap)
    {
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
        if (wrappedIndex >= 0)
        {
            int targetIndex = Math.Max(0, wrappedIndex - _viewportHeight);
            var targetWl = wrappedLines[targetIndex];
            _cursorY = targetWl.SourceLineIndex;
            _cursorX = Math.Min(targetWl.SourceCharOffset, _lines[_cursorY].Length);
        }
    }
    else { /* existing NoWrap logic */ }
    break;

case ConsoleKey.PageDown:
    if (_wrapMode != WrapMode.NoWrap)
    {
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
        if (wrappedIndex >= 0)
        {
            int targetIndex = Math.Min(wrappedLines.Count - 1, wrappedIndex + _viewportHeight);
            var targetWl = wrappedLines[targetIndex];
            _cursorY = targetWl.SourceLineIndex;
            _cursorX = Math.Min(targetWl.SourceCharOffset, _lines[_cursorY].Length);
        }
    }
    else { /* existing NoWrap logic */ }
    break;
```

#### 1.9 Update PaintDOM to use cached wrapping

Replace lines 1850-1937 (the wrapping loop) with:
```csharp
var allWrappedLineInfos = GetWrappedLines(effectiveWidth);

// Build display lists from WrappedLineInfo for painting
List<string> allWrappedLines = new(allWrappedLineInfos.Count);
List<int> sourceLineIndexList = new(allWrappedLineInfos.Count);
List<int> sourceLineOffsetList = new(allWrappedLineInfos.Count);
foreach (var wli in allWrappedLineInfos)
{
    allWrappedLines.Add(wli.DisplayText);
    sourceLineIndexList.Add(wli.SourceLineIndex);
    sourceLineOffsetList.Add(wli.SourceCharOffset);
}
```
Then use `sourceLineIndexList`/`sourceLineOffsetList` in place of the existing
`sourceLineIndex`/`sourceLineOffset` lists (same variable names, drop-in replacement).

#### 1.10 Fix horizontal scroll contamination (lines 1308-1318)

Replace lines 1308-1318 with:
```csharp
// In wrap mode, horizontal scroll is always 0 - wrapping handles overflow
// Only adjust horizontal scroll in NoWrap mode
if (_wrapMode == WrapMode.NoWrap)
{
    // existing horizontal scroll logic for NoWrap can stay
}
// Remove the incorrect _horizontalScrollOffset = _cursorX for wrap mode
```

#### 1.11 Also fix horizontal scroll application in PaintDOM (lines 2007-2010)

Add a guard:
```csharp
// Only apply horizontal scroll in NoWrap mode
if (_wrapMode == WrapMode.NoWrap && _horizontalScrollOffset > 0)
{
    if (_horizontalScrollOffset < line.Length)
        visibleLine = line.Substring(_horizontalScrollOffset);
    else
        visibleLine = string.Empty;
}
```

### Tasks Checklist

- [ ] **1.1** Add `WrappedLineInfo` record struct and `GetWrappedLines()` with cache
- [ ] **1.2** Add `BuildWordWrappedLines()` static helper (fixes R6 phantom line + R16 space collapsing)
- [ ] **1.3** Add `InvalidateWrappedLinesCache()` and wire to all mutation points
- [ ] **1.4** Add `FindWrappedLineForCursor()` and `GetCursorOffsetInWrappedLine()` helpers
- [ ] **1.5** Rewrite `GetTotalWrappedLineCount()` to use cache
- [ ] **1.6** Rewrite `EnsureCursorVisible()` wrap-mode section
- [ ] **1.7** Rewrite `GetLogicalCursorPosition()` wrap-mode section
- [ ] **1.8** Rewrite Up/Down arrow wrap-mode navigation
- [ ] **1.9** Rewrite Home/End wrap-mode navigation (fixes I7 roundtrip bug)
- [ ] **1.10** Rewrite PageUp/PageDown wrap-mode navigation
- [ ] **1.11** Update PaintDOM to use `GetWrappedLines()` cache
- [ ] **1.12** Fix horizontal scroll contamination in typing (lines 1308-1318)
- [ ] **1.13** Guard horizontal scroll application in PaintDOM for wrap mode
- [ ] **1.14** Add constants to `ControlDefaults.cs` (DefaultEditorWidth=80, DefaultEditorViewportHeight=10)
- [ ] **1.15** Replace magic `80` fallback (lines 569, 1490, 1629) with `ControlDefaults.DefaultEditorWidth`

---

## Phase 2: Fix Critical Input Bugs

> **Status**: NOT STARTED
> **Priority**: CRITICAL
> **Depends on**: Phase 1
> **Bugs fixed**: I1, I3, I6, I8, I10, I11, I13, I14, I15, I17

### Implementation Guide

#### 2.1 Word boundary helper

Create `Helpers/WordBoundaryHelper.cs`. This is used by Ctrl+Left/Right, Ctrl+Backspace/Delete,
and later by double-click word selection:

```csharp
namespace SharpConsoleUI.Helpers;

/// <summary>
/// Provides word boundary detection for text navigation and editing.
/// Word characters: letters, digits, underscores. Everything else is a separator.
/// </summary>
public static class WordBoundaryHelper
{
    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Finds the position of the start of the previous word (for Ctrl+Left, Ctrl+Backspace).
    /// </summary>
    public static int FindPreviousWordBoundary(string line, int position)
    {
        if (position <= 0) return 0;
        int pos = position - 1;

        // Skip any whitespace/separators going left
        while (pos > 0 && !IsWordChar(line[pos]))
            pos--;

        // Skip word characters going left
        while (pos > 0 && IsWordChar(line[pos - 1]))
            pos--;

        return pos;
    }

    /// <summary>
    /// Finds the position after the end of the next word (for Ctrl+Right, Ctrl+Delete).
    /// </summary>
    public static int FindNextWordBoundary(string line, int position)
    {
        if (position >= line.Length) return line.Length;
        int pos = position;

        // Skip word characters going right
        while (pos < line.Length && IsWordChar(line[pos]))
            pos++;

        // Skip any whitespace/separators going right
        while (pos < line.Length && !IsWordChar(line[pos]))
            pos++;

        return pos;
    }

    /// <summary>
    /// Finds the word boundaries (start, end) around a given position (for double-click select).
    /// Returns (position, position) if not in a word.
    /// </summary>
    public static (int start, int end) FindWordAt(string line, int position)
    {
        if (position < 0 || position >= line.Length)
            return (position, position);

        if (!IsWordChar(line[position]))
        {
            // Click on separator - select the separator run
            int start = position, end = position;
            while (start > 0 && !IsWordChar(line[start - 1])) start--;
            while (end < line.Length && !IsWordChar(line[end])) end++;
            return (start, end);
        }

        int s = position, e = position;
        while (s > 0 && IsWordChar(line[s - 1])) s--;
        while (e < line.Length && IsWordChar(line[e])) e++;
        return (s, e);
    }
}
```

#### 2.2 Mouse click-to-position cursor (fixes I1)

Rewrite `ProcessMouseEvent` (lines 1761-1787). The key is mapping `args.X`/`args.Y` to
a `(_cursorY, _cursorX)` position accounting for margins, scroll offsets, and wrapped lines:

```csharp
public bool ProcessMouseEvent(MouseEventArgs args)
{
    if (!IsEnabled || !WantsMouseEvents)
        return false;

    if (args.HasFlag(MouseFlags.Button1Clicked))
    {
        if (_hasFocus && !_readOnly)
        {
            IsEditing = true; // Use property setter (fixes I17)
            PositionCursorFromMouse(args.X, args.Y);
        }
        MouseClick?.Invoke(this, args);
        return true;
    }

    // ... existing ReportMousePosition handler ...
    return false;
}

/// <summary>
/// Maps screen-relative mouse coordinates to cursor position in the document.
/// Coordinates are relative to the control's bounds (provided by the framework).
/// </summary>
private void PositionCursorFromMouse(int mouseX, int mouseY)
{
    // mouseX/mouseY are relative to control bounds
    // Adjust for margins
    int relX = mouseX - _margin.Left;
    int relY = mouseY - _margin.Top;

    if (relX < 0) relX = 0;
    if (relY < 0) relY = 0;

    if (_wrapMode == WrapMode.NoWrap)
    {
        // NoWrap: direct mapping with scroll offsets
        _cursorY = Math.Min(_lines.Count - 1, relY + _verticalScrollOffset);
        _cursorX = Math.Min(_lines[_cursorY].Length, relX + _horizontalScrollOffset);
    }
    else
    {
        // Wrap mode: map visual row to wrapped line, then to source position
        var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
        int wrappedIndex = relY + _verticalScrollOffset;

        if (wrappedIndex >= wrappedLines.Count)
            wrappedIndex = wrappedLines.Count - 1;
        if (wrappedIndex < 0) wrappedIndex = 0;

        var wl = wrappedLines[wrappedIndex];
        _cursorY = wl.SourceLineIndex;
        _cursorX = Math.Min(wl.SourceCharOffset + relX,
                           wl.SourceCharOffset + wl.Length);
        _cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
    }

    ClearSelection();
    EnsureCursorVisible();
    Container?.Invalidate(true);
}
```

#### 2.3 Implement Ctrl+C/X/V clipboard (fixes I3)

There's no clipboard in the codebase yet. For cross-platform console clipboard, use
process invocation. Add `Helpers/ClipboardHelper.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Cross-platform clipboard helper for console applications.
/// Uses xclip/xsel on Linux, pbcopy/pbpaste on macOS, clip.exe on Windows.
/// </summary>
public static class ClipboardHelper
{
    public static void SetText(string text)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                RunProcess("clip", text);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                RunProcess("pbcopy", text);
            else // Linux
                RunProcess("xclip", text, "-selection", "clipboard");
        }
        catch { /* Silently fail - clipboard is best-effort */ }
    }

    public static string GetText()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return RunProcessRead("powershell.exe", "-command", "Get-Clipboard");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RunProcessRead("pbpaste");
            else
                return RunProcessRead("xclip", "-selection", "clipboard", "-o");
        }
        catch { return string.Empty; }
    }

    private static void RunProcess(string cmd, string input, params string[] args)
    {
        var psi = new ProcessStartInfo(cmd, string.Join(" ", args))
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return;
        p.StandardInput.Write(input);
        p.StandardInput.Close();
        p.WaitForExit(1000);
    }

    private static string RunProcessRead(string cmd, params string[] args)
    {
        var psi = new ProcessStartInfo(cmd, string.Join(" ", args))
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return string.Empty;
        string result = p.StandardOutput.ReadToEnd();
        p.WaitForExit(1000);
        return result;
    }
}
```

#### 2.4 Wire Ctrl key combos into ProcessKey

In the `default:` case (lines 1285-1323), BEFORE the existing Ctrl/Alt bubble-up check,
add explicit handling for Ctrl combos:

```csharp
// Replace the default: case starting at line 1285
default:
    if (isCtrlPressed)
    {
        switch (key.Key)
        {
            case ConsoleKey.A: // Select All
                if (_lines.Count > 0)
                {
                    _hasSelection = true;
                    _selectionStartX = 0;
                    _selectionStartY = 0;
                    _selectionEndX = _lines[_lines.Count - 1].Length;
                    _selectionEndY = _lines.Count - 1;
                    _cursorX = _selectionEndX;
                    _cursorY = _selectionEndY;
                    Container?.Invalidate(true);
                }
                return true;

            case ConsoleKey.C: // Copy
                if (_hasSelection)
                    ClipboardHelper.SetText(GetSelectedText());
                return true;

            case ConsoleKey.X: // Cut
                if (!_readOnly && _hasSelection)
                {
                    ClipboardHelper.SetText(GetSelectedText());
                    DeleteSelectedText();
                    contentChanged = true;
                }
                return true;

            case ConsoleKey.V: // Paste
                if (!_readOnly)
                {
                    string clipText = ClipboardHelper.GetText();
                    if (!string.IsNullOrEmpty(clipText))
                    {
                        if (_hasSelection) DeleteSelectedText();
                        InsertTextAtCursor(clipText); // Internal version that doesn't fire event (see below)
                        contentChanged = true;
                    }
                }
                return true;

            default:
                break; // Other Ctrl combos bubble up
        }
    }

    if (key.Modifiers.HasFlag(ConsoleModifiers.Control) ||
        key.Modifiers.HasFlag(ConsoleModifiers.Alt))
    {
        break; // Bubble up unhandled Ctrl/Alt combos
    }

    // ... rest of existing character insertion code ...
```

**Note:** `InsertTextAtCursor` is a private version of `InsertText` that doesn't fire
`ContentChanged` (the caller handles that). Or just set a flag and let the existing
`contentChanged` path at the end handle it.

#### 2.5 Ctrl+Left/Right word navigation (fixes I6)

In the `LeftArrow` case (line 934), check for Ctrl modifier:

```csharp
case ConsoleKey.LeftArrow:
    if (isCtrlPressed)
    {
        // Word jump left
        if (_cursorX > 0)
        {
            _cursorX = WordBoundaryHelper.FindPreviousWordBoundary(_lines[_cursorY], _cursorX);
        }
        else if (_cursorY > 0)
        {
            _cursorY--;
            _cursorX = _lines[_cursorY].Length;
        }
    }
    else
    {
        // Existing single-char left logic (lines 935-944)
    }
    break;

case ConsoleKey.RightArrow:
    if (isCtrlPressed)
    {
        // Word jump right
        if (_cursorX < _lines[_cursorY].Length)
        {
            _cursorX = WordBoundaryHelper.FindNextWordBoundary(_lines[_cursorY], _cursorX);
        }
        else if (_cursorY < _lines.Count - 1)
        {
            _cursorY++;
            _cursorX = 0;
        }
    }
    else
    {
        // Existing single-char right logic (lines 948-957)
    }
    break;
```

#### 2.6 Ctrl+Backspace/Delete word deletion

In the `Backspace` case (after selection handling, around line 1196):
```csharp
case ConsoleKey.Backspace:
    if (_readOnly) break;
    if (_hasSelection)
    {
        DeleteSelectedText();
        contentChanged = true;
    }
    else if (isCtrlPressed)
    {
        // Delete previous word
        if (_cursorX > 0)
        {
            int newPos = WordBoundaryHelper.FindPreviousWordBoundary(_lines[_cursorY], _cursorX);
            _lines[_cursorY] = _lines[_cursorY].Remove(newPos, _cursorX - newPos);
            _cursorX = newPos;
            contentChanged = true;
        }
        else if (_cursorY > 0)
        {
            // Join with previous line (same as existing)
            int previousLineLength = _lines[_cursorY - 1].Length;
            _lines[_cursorY - 1] += _lines[_cursorY];
            _lines.RemoveAt(_cursorY);
            _cursorY--;
            _cursorX = previousLineLength;
            contentChanged = true;
        }
    }
    else
    {
        // Existing single-char backspace (lines 1196-1212)
    }
    break;
```

Similar pattern for `Delete` with `FindNextWordBoundary`.

#### 2.7 Fix selection initiation for PageUp/PageDown (fixes I10)

Line 909-912, add PageUp/PageDown to the key list:
```csharp
if (isShiftPressed && !_hasSelection &&
    (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow ||
     key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow ||
     key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End ||
     key.Key == ConsoleKey.PageUp || key.Key == ConsoleKey.PageDown))  // Added
```

#### 2.8 Fix selection clearing for PageUp/PageDown (fixes I11)

Line 924-927, add PageUp/PageDown:
```csharp
else if (!isShiftPressed && _hasSelection &&
         (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow ||
          key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow ||
          key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End ||
          key.Key == ConsoleKey.PageUp || key.Key == ConsoleKey.PageDown))  // Added
```

#### 2.9 Fix Home key in non-editing mode (fixes I13)

Line 861, change condition:
```csharp
case ConsoleKey.Home:
    if (_verticalScrollOffset > 0 || _horizontalScrollOffset > 0)  // Was: only vertical
    {
        _skipUpdateScrollPositionsInRender = true;
        _verticalScrollOffset = 0;
        _horizontalScrollOffset = 0;
        Container?.Invalidate(true);
        return true;
    }
    return false;
```

#### 2.10 Fix Enter without focus (fixes I14)

Lines 889-898: add `_hasFocus` guard:
```csharp
if (!_isEditing && _hasFocus)  // Added _hasFocus
{
    if (key.Key == ConsoleKey.Enter) { ... }
    return false;
}
if (!_isEditing) return false; // Not focused and not editing = ignore
```

#### 2.11 Fix _isEditing direct writes (fixes I17)

Replace all `_isEditing = value` with `IsEditing = value` at lines: 780, 893, 1279, 1736, 1741, 1771.

### Tasks Checklist

- [ ] **2.1** Create `Helpers/WordBoundaryHelper.cs`
- [ ] **2.2** Implement mouse click-to-position cursor (rewrite ProcessMouseEvent)
- [ ] **2.3** Create `Helpers/ClipboardHelper.cs` (cross-platform)
- [ ] **2.4** Wire Ctrl+A (Select All) into ProcessKey
- [ ] **2.5** Wire Ctrl+C (Copy), Ctrl+X (Cut), Ctrl+V (Paste)
- [ ] **2.6** Implement Ctrl+Left/Right word navigation
- [ ] **2.7** Implement Ctrl+Backspace/Delete word deletion
- [ ] **2.8** Fix Shift+PageUp/PageDown selection initiation (add to key list)
- [ ] **2.9** Fix PageUp/PageDown selection clearing (add to key list)
- [ ] **2.10** Fix Home key ignoring horizontal scroll in non-editing mode
- [ ] **2.11** Fix Enter without focus guard
- [ ] **2.12** Replace all `_isEditing` direct field writes with property setter

---

## Phase 3: P0 Missing Features

> **Status**: NOT STARTED
> **Priority**: HIGH
> **Depends on**: Phase 2 (mouse click positioning needed for drag selection)
> **Bugs fixed**: I4, I5, I12

### Implementation Guide

#### 3.1 Mouse drag selection (fixes I4)

Add drag state fields:
```csharp
private bool _isDragging = false;
private DateTime _lastClickTime = DateTime.MinValue;
private int _clickCount = 0;
```

Expand `ProcessMouseEvent`:
```csharp
if (args.HasFlag(MouseFlags.Button1Pressed))
{
    // Mouse down - start potential drag
    if (_hasFocus && !_readOnly)
    {
        IsEditing = true;
        PositionCursorFromMouse(args.X, args.Y);
        _hasSelection = true;
        _selectionStartX = _cursorX;
        _selectionStartY = _cursorY;
        _selectionEndX = _cursorX;
        _selectionEndY = _cursorY;
        _isDragging = true;
    }
    return true;
}

if (args.HasFlag(MouseFlags.ReportMousePosition) && _isDragging)
{
    // Mouse drag - extend selection
    PositionCursorFromMouseNoSelection(args.X, args.Y); // Move cursor, don't clear selection
    _selectionEndX = _cursorX;
    _selectionEndY = _cursorY;
    // Check if selection is non-empty
    _hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
    Container?.Invalidate(true);
    return true;
}

if (args.HasFlag(MouseFlags.Button1Released))
{
    _isDragging = false;
    return true;
}
```

`PositionCursorFromMouseNoSelection` is the same as `PositionCursorFromMouse` but doesn't
call `ClearSelection()`. Extract the positioning logic into a shared method.

#### 3.2 Mouse wheel scrolling

```csharp
if (args.HasFlag(MouseFlags.WheeledUp))
{
    int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, _verticalScrollOffset);
    if (scrollAmount > 0)
    {
        _skipUpdateScrollPositionsInRender = true;
        _verticalScrollOffset -= scrollAmount;
        Container?.Invalidate(true);
    }
    return true;
}

if (args.HasFlag(MouseFlags.WheeledDown))
{
    int totalLines = GetTotalWrappedLineCount();
    int maxScroll = Math.Max(0, totalLines - _viewportHeight);
    int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, maxScroll - _verticalScrollOffset);
    if (scrollAmount > 0)
    {
        _skipUpdateScrollPositionsInRender = true;
        _verticalScrollOffset += scrollAmount;
        Container?.Invalidate(true);
    }
    return true;
}
```

#### 3.3 Double-click word selection (fixes I5)

The `MouseFlags` enum already has `Button1DoubleClicked`:
```csharp
if (args.HasFlag(MouseFlags.Button1DoubleClicked))
{
    if (_hasFocus)
    {
        IsEditing = true;
        PositionCursorFromMouseNoSelection(args.X, args.Y);
        var (wordStart, wordEnd) = WordBoundaryHelper.FindWordAt(_lines[_cursorY], _cursorX);
        _hasSelection = wordStart != wordEnd;
        _selectionStartX = wordStart;
        _selectionStartY = _cursorY;
        _selectionEndX = wordEnd;
        _selectionEndY = _cursorY;
        _cursorX = wordEnd;
        Container?.Invalidate(true);
    }
    MouseDoubleClick?.Invoke(this, args); // Wire the previously-dead event!
    return true;
}
```

Remove the `#pragma warning disable CS0067` for `MouseDoubleClick` (line 125).

#### 3.4 Triple-click line selection

```csharp
if (args.HasFlag(MouseFlags.Button1TripleClicked))
{
    if (_hasFocus)
    {
        IsEditing = true;
        PositionCursorFromMouseNoSelection(args.X, args.Y);
        _hasSelection = true;
        _selectionStartX = 0;
        _selectionStartY = _cursorY;
        _selectionEndX = _lines[_cursorY].Length;
        _selectionEndY = _cursorY;
        _cursorX = _lines[_cursorY].Length;
        Container?.Invalidate(true);
    }
    return true;
}
```

#### 3.5 Tab key handling

Add `TabSize` property:
```csharp
private int _tabSize = ControlDefaults.DefaultTabSize;
public int TabSize
{
    get => _tabSize;
    set => _tabSize = Math.Max(1, Math.Min(8, value));
}
```

In ProcessKey, add a `ConsoleKey.Tab` case before the `default:`:
```csharp
case ConsoleKey.Tab:
    if (_readOnly) break;
    if (isShiftPressed)
    {
        // Shift+Tab: dedent
        if (_hasSelection)
        {
            var (sX, sY, eX, eY) = GetOrderedSelectionBounds();
            for (int ln = sY; ln <= eY; ln++)
            {
                int spaces = 0;
                while (spaces < _tabSize && spaces < _lines[ln].Length && _lines[ln][spaces] == ' ')
                    spaces++;
                if (spaces > 0)
                    _lines[ln] = _lines[ln].Substring(spaces);
            }
            contentChanged = true;
        }
        else
        {
            // Dedent current line
            int spaces = 0;
            while (spaces < _tabSize && spaces < _lines[_cursorY].Length && _lines[_cursorY][spaces] == ' ')
                spaces++;
            if (spaces > 0)
            {
                _lines[_cursorY] = _lines[_cursorY].Substring(spaces);
                _cursorX = Math.Max(0, _cursorX - spaces);
                contentChanged = true;
            }
        }
    }
    else
    {
        // Tab: indent
        if (_hasSelection)
        {
            var (sX, sY, eX, eY) = GetOrderedSelectionBounds();
            string indent = new string(' ', _tabSize);
            for (int ln = sY; ln <= eY; ln++)
                _lines[ln] = indent + _lines[ln];
            contentChanged = true;
        }
        else
        {
            // Insert spaces to next tab stop
            int spacesToInsert = _tabSize - (_cursorX % _tabSize);
            _lines[_cursorY] = _lines[_cursorY].Insert(_cursorX, new string(' ', spacesToInsert));
            _cursorX += spacesToInsert;
            contentChanged = true;
        }
    }
    break;
```

### Tasks Checklist

- [ ] **3.1** Implement mouse drag selection (Button1Pressed + ReportMousePosition + Button1Released)
- [ ] **3.2** Implement mouse wheel scrolling (WheeledUp/WheeledDown)
- [ ] **3.3** Implement double-click word selection (Button1DoubleClicked + WordBoundaryHelper)
- [ ] **3.4** Implement triple-click line selection (Button1TripleClicked)
- [ ] **3.5** Add `TabSize` property and Tab/Shift+Tab key handling (indent/dedent)
- [ ] **3.6** Wire `MouseDoubleClick` event, remove `#pragma CS0067` suppression

---

## Phase 4: Undo/Redo System

> **Status**: NOT STARTED
> **Priority**: HIGH
> **Depends on**: Phase 2 (working edit operations)
> **Bugs fixed**: I9

### Implementation Guide

#### 4.1 Undo action data structure

```csharp
/// <summary>
/// Represents a single undoable edit operation.
/// </summary>
private sealed class UndoAction
{
    public required string OldText { get; init; }     // Text state before the action
    public required string NewText { get; init; }     // Text state after the action
    public required int CursorXBefore { get; init; }
    public required int CursorYBefore { get; init; }
    public required int CursorXAfter { get; init; }
    public required int CursorYAfter { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

**Note:** Using full-text snapshots for simplicity. For very large documents, consider
diff-based approach later. For most console text editing, documents are small enough.

#### 4.2 Undo/redo stack fields

```csharp
private readonly Stack<UndoAction> _undoStack = new();
private readonly Stack<UndoAction> _redoStack = new();
private int _undoLimit = ControlDefaults.DefaultUndoLimit;
private bool _isModified = false;
private string? _savedContent = null; // content snapshot at last "save"

public int UndoLimit
{
    get => _undoLimit;
    set => _undoLimit = Math.Max(1, value);
}

public bool IsModified
{
    get => _isModified;
    private set { _isModified = value; /* fire event later in Phase 6 */ }
}
```

#### 4.3 Record edits helper

Call this BEFORE making any edit. It captures the "before" state:
```csharp
private string? _pendingUndoBefore;
private int _pendingCursorXBefore, _pendingCursorYBefore;

private void BeginUndoAction()
{
    _pendingUndoBefore = GetContent();
    _pendingCursorXBefore = _cursorX;
    _pendingCursorYBefore = _cursorY;
}

private void CommitUndoAction()
{
    if (_pendingUndoBefore == null) return;
    string after = GetContent();
    if (after == _pendingUndoBefore) { _pendingUndoBefore = null; return; } // No change

    _undoStack.Push(new UndoAction
    {
        OldText = _pendingUndoBefore,
        NewText = after,
        CursorXBefore = _pendingCursorXBefore,
        CursorYBefore = _pendingCursorYBefore,
        CursorXAfter = _cursorX,
        CursorYAfter = _cursorY
    });

    // Trim stack to limit
    if (_undoStack.Count > _undoLimit)
    {
        var temp = _undoStack.ToArray();
        _undoStack.Clear();
        for (int i = 0; i < _undoLimit; i++)
            _undoStack.Push(temp[i]);
    }

    _redoStack.Clear(); // New edit clears redo
    _pendingUndoBefore = null;
    IsModified = _savedContent != after;
}
```

#### 4.4 Wire into ProcessKey

At the start of any editing case (Backspace, Delete, Enter, Tab, char insert, Ctrl+X, Ctrl+V):
```csharp
BeginUndoAction();
// ... perform the edit ...
// At the contentChanged path (lines 1342-1346):
if (contentChanged)
{
    CommitUndoAction();
    InvalidateWrappedLinesCache();
    Container?.Invalidate(true);
    ContentChanged?.Invoke(this, GetContent());
}
```

#### 4.5 Ctrl+Z Undo and Ctrl+Y Redo

Add to the Ctrl key handling section (from Phase 2):
```csharp
case ConsoleKey.Z: // Undo
    if (_undoStack.Count > 0)
    {
        var action = _undoStack.Pop();
        _redoStack.Push(action);
        SetContentInternal(action.OldText); // Internal: doesn't clear undo stack
        _cursorX = action.CursorXBefore;
        _cursorY = action.CursorYBefore;
        ClearSelection();
        EnsureCursorVisible();
        IsModified = _savedContent != action.OldText;
        Container?.Invalidate(true);
        ContentChanged?.Invoke(this, GetContent());
    }
    return true;

case ConsoleKey.Y: // Redo
    if (_redoStack.Count > 0)
    {
        var action = _redoStack.Pop();
        _undoStack.Push(action);
        SetContentInternal(action.NewText);
        _cursorX = action.CursorXAfter;
        _cursorY = action.CursorYAfter;
        ClearSelection();
        EnsureCursorVisible();
        IsModified = _savedContent != action.NewText;
        Container?.Invalidate(true);
        ContentChanged?.Invoke(this, GetContent());
    }
    return true;
```

`SetContentInternal` is like `SetContent` but doesn't clear undo/redo stacks or fire events.

#### 4.6 MarkAsSaved / IsModified

```csharp
public void MarkAsSaved()
{
    _savedContent = GetContent();
    IsModified = false;
}
```

### Tasks Checklist

- [ ] **4.1** Add `UndoAction` class and stack fields
- [ ] **4.2** Implement `BeginUndoAction()` / `CommitUndoAction()` helpers
- [ ] **4.3** Add `SetContentInternal()` that doesn't clear undo stack
- [ ] **4.4** Wire `BeginUndoAction` before all edit operations in ProcessKey
- [ ] **4.5** Wire `CommitUndoAction` in contentChanged path
- [ ] **4.6** Implement Ctrl+Z (Undo) and Ctrl+Y (Redo)
- [ ] **4.7** Add `IsModified` property and `MarkAsSaved()` method
- [ ] **4.8** Add `UndoLimit` property

---

## Phase 5: Unicode & Special Character Support

> **Status**: NOT STARTED
> **Priority**: HIGH
> **Can run parallel to**: Phases 3-4
> **Bugs fixed**: M2, M3, M7, M8

### Implementation Guide

#### 5.1 Unicode display width helper

Create `Helpers/UnicodeWidthHelper.cs`:

```csharp
using System.Globalization;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Calculates terminal display width of Unicode characters.
/// CJK = 2 columns, combining = 0 columns, most others = 1 column.
/// </summary>
public static class UnicodeWidthHelper
{
    public static int GetCharWidth(char c)
    {
        // Combining marks: zero width
        var category = char.GetUnicodeCategory(c);
        if (category == UnicodeCategory.NonSpacingMark ||
            category == UnicodeCategory.EnclosingMark ||
            category == UnicodeCategory.Format)
            return 0;

        // Surrogate: treat as 0 (the pair together is typically 2)
        if (char.IsSurrogate(c))
            return 0;

        int codePoint = (int)c;

        // CJK ranges (double-width)
        if (IsCjkCodePoint(codePoint))
            return 2;

        // Fullwidth forms
        if (codePoint >= 0xFF01 && codePoint <= 0xFF60)
            return 2;
        if (codePoint >= 0xFFE0 && codePoint <= 0xFFE6)
            return 2;

        return 1;
    }

    public static int GetStringWidth(string s)
    {
        int width = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                width += 2; // Most surrogate pair characters (emoji etc.) are double-width
                i++; // Skip low surrogate
            }
            else
            {
                width += GetCharWidth(s[i]);
            }
        }
        return width;
    }

    /// <summary>
    /// Returns the char index that corresponds to a given display column offset.
    /// </summary>
    public static int ColumnToCharIndex(string s, int targetColumn)
    {
        int column = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (column >= targetColumn) return i;
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                column += 2;
                i++;
            }
            else
            {
                column += GetCharWidth(s[i]);
            }
        }
        return s.Length;
    }

    private static bool IsCjkCodePoint(int cp) =>
        (cp >= 0x1100 && cp <= 0x115F) ||   // Hangul Jamo
        (cp >= 0x2E80 && cp <= 0x303E) ||   // CJK Radicals, Kangxi, Ideographic
        (cp >= 0x3040 && cp <= 0x33BF) ||   // Hiragana, Katakana, Bopomofo, Hangul, CJK
        (cp >= 0x3400 && cp <= 0x4DBF) ||   // CJK Unified Ext A
        (cp >= 0x4E00 && cp <= 0xA4CF) ||   // CJK Unified, Yi
        (cp >= 0xA960 && cp <= 0xA97C) ||   // Hangul Jamo Extended-A
        (cp >= 0xAC00 && cp <= 0xD7A3) ||   // Hangul Syllables
        (cp >= 0xF900 && cp <= 0xFAFF) ||   // CJK Compatibility Ideographs
        (cp >= 0xFE30 && cp <= 0xFE6F);     // CJK Compatibility Forms
}
```

#### 5.2 Surrogate pair-safe cursor movement

For Backspace (line 1199), delete BOTH chars of a surrogate pair:
```csharp
// Before: _lines[_cursorY] = _lines[_cursorY].Remove(_cursorX - 1, 1);
// After:
int deleteCount = 1;
if (_cursorX >= 2 && char.IsLowSurrogate(_lines[_cursorY][_cursorX - 1])
                  && char.IsHighSurrogate(_lines[_cursorY][_cursorX - 2]))
    deleteCount = 2;
_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX - deleteCount, deleteCount);
_cursorX -= deleteCount;
```

Same pattern for Delete (line 1227), Left/Right arrow movement, etc.

#### 5.3 Tab-to-spaces conversion on input

In `SetContent()`, `InsertText()`, `AppendContent()`, and `SetContentLines()`, sanitize:
```csharp
private string SanitizeInputText(string text)
{
    // Convert tabs to spaces
    text = text.Replace("\t", new string(' ', _tabSize));

    // Strip dangerous control characters (keep \n, \r for line splitting)
    var sb = new StringBuilder(text.Length);
    foreach (char c in text)
    {
        if (c == '\n' || c == '\r' || !char.IsControl(c))
            sb.Append(c);
    }
    return sb.ToString();
}
```

### Tasks Checklist

- [ ] **5.1** Create `Helpers/UnicodeWidthHelper.cs` with `GetCharWidth`, `GetStringWidth`, `ColumnToCharIndex`
- [ ] **5.2** Replace `string.Length` width calculations with `UnicodeWidthHelper.GetStringWidth` in key locations
- [ ] **5.3** Handle surrogate pairs in Backspace (don't split pairs)
- [ ] **5.4** Handle surrogate pairs in Delete (don't split pairs)
- [ ] **5.5** Handle surrogate pairs in Left/Right arrow (skip as unit)
- [ ] **5.6** Implement `SanitizeInputText()` for tab conversion and control char stripping
- [ ] **5.7** Wire `SanitizeInputText` into `SetContent`, `InsertText`, `AppendContent`, `SetContentLines`

---

## Phase 6: Events & Public API

> **Status**: NOT STARTED
> **Priority**: MEDIUM
> **Depends on**: Phases 2-4

### Implementation Guide

#### 6.1 New events

```csharp
public event EventHandler<(int Line, int Column)>? CursorPositionChanged;
public event EventHandler? SelectionChanged;
public event EventHandler<bool>? EditingModeChanged;
```

Fire `CursorPositionChanged` at the end of ProcessKey when cursor moved (lines 1335-1339):
```csharp
if (_cursorX != oldCursorX || _cursorY != oldCursorY)
{
    EnsureCursorVisible();
    Container?.Invalidate(true);
    CursorPositionChanged?.Invoke(this, (_cursorY + 1, _cursorX + 1)); // 1-based
}
```

Fire `SelectionChanged` when selection state changes:
```csharp
if (_hasSelection != oldHasSelection || /* selection bounds changed */)
    SelectionChanged?.Invoke(this, EventArgs.Empty);
```

Fire `EditingModeChanged` in the `IsEditing` property setter.

#### 6.2 Public cursor/status properties

```csharp
/// <summary>Current line number (1-based).</summary>
public int CurrentLine => _cursorY + 1;

/// <summary>Current column number (1-based).</summary>
public int CurrentColumn => _cursorX + 1;

/// <summary>Total number of lines in the document.</summary>
public int LineCount => _lines.Count;
```

#### 6.3 GoToLine method

```csharp
public void GoToLine(int lineNumber)
{
    int line = Math.Max(0, Math.Min(_lines.Count - 1, lineNumber - 1)); // 1-based input
    _cursorY = line;
    _cursorX = 0;
    ClearSelection();
    EnsureCursorVisible();
    Container?.Invalidate(true);
}
```

#### 6.4 PlaceholderText

```csharp
private string? _placeholderText;
public string? PlaceholderText { get => _placeholderText; set { _placeholderText = value; Container?.Invalidate(true); } }
```

In PaintDOM, when content is empty and not editing, render placeholder:
```csharp
// After the existing painting loop, check if we should show placeholder
if (_lines.Count == 1 && _lines[0].Length == 0 && !_isEditing && _placeholderText != null)
{
    string placeholder = _placeholderText.Length > effectiveWidth
        ? _placeholderText.Substring(0, effectiveWidth)
        : _placeholderText;
    Color placeholderColor = /* dimmed version of fgColor, or ScrollbarColor as approximation */;
    for (int c = 0; c < placeholder.Length && c < effectiveWidth; c++)
    {
        int cellX = startX + c;
        if (cellX >= clipRect.X && cellX < clipRect.Right)
            buffer.SetCell(cellX, startY, placeholder[c], placeholderColor, bgColor);
    }
}
```

#### 6.5 MaxLength

```csharp
private int? _maxLength;
public int? MaxLength { get => _maxLength; set => _maxLength = value.HasValue ? Math.Max(1, value.Value) : null; }
```

Check in all insertion points (character insert, paste, InsertText):
```csharp
if (_maxLength.HasValue && GetContent().Length + insertLength > _maxLength.Value)
    return; // or truncate to fit
```

#### 6.6 Remove dead code

- Delete `RenderHorizontalScrollbar()` (lines 1532-1573) - never called
- Delete `RenderVerticalScrollbar()` (lines 1575-1616) - never called
- Remove `#pragma CS0067` for `MouseEnter`/`MouseLeave` (keep declarations for interface, wire later)

#### 6.7 Update builder

Add to `MultilineEditControlBuilder.cs`:
```csharp
public MultilineEditControlBuilder WithPlaceholder(string text) { _placeholder = text; return this; }
public MultilineEditControlBuilder WithMaxLength(int max) { _maxLength = max; return this; }
public MultilineEditControlBuilder WithTabSize(int size) { _tabSize = size; return this; }
public MultilineEditControlBuilder WithScrollMargin(int margin) { _scrollMargin = margin; return this; }
public MultilineEditControlBuilder WithUndoLimit(int limit) { _undoLimit = limit; return this; }
public MultilineEditControlBuilder OnSelectionChanged(EventHandler handler) { _onSelectionChanged = handler; return this; }
public MultilineEditControlBuilder OnCursorPositionChanged(EventHandler<(int,int)> handler) { ... }
public MultilineEditControlBuilder OnEditingModeChanged(EventHandler<bool> handler) { ... }
```

### Tasks Checklist

- [ ] **6.1** Add `CursorPositionChanged` event and fire it
- [ ] **6.2** Add `SelectionChanged` event and fire it
- [ ] **6.3** Add `EditingModeChanged` event and fire from `IsEditing` setter
- [ ] **6.4** Add `CurrentLine`, `CurrentColumn`, `LineCount` properties
- [ ] **6.5** Implement `GoToLine(int)` method
- [ ] **6.6** Add `PlaceholderText` property with rendering in PaintDOM
- [ ] **6.7** Add `MaxLength` property with enforcement
- [ ] **6.8** Remove dead code (unused scrollbar methods, #pragma suppressions)
- [ ] **6.9** Update builder with all new properties and event handlers

---

## Phase 7: Performance & Polish

> **Status**: NOT STARTED
> **Priority**: MEDIUM
> **Bugs fixed**: R4, R5, R7, R8, R9, R11, R12, R13, R14, R15, B1, B2

### Implementation Guide

#### 7.1 Cache per-frame calculations in PaintDOM

At the top of PaintDOM (after calculating `effectiveWidth`), compute once:
```csharp
int totalWrappedLines = GetTotalWrappedLineCount(); // Cached via Phase 1
int maxLineLength = GetMaxLineLength(); // Compute once

// Scrollbar thumb (vertical) - compute once, not per row
int vThumbHeight = 0, vThumbPos = 0;
if (needsVerticalScrollbar)
{
    vThumbHeight = Math.Max(1, (_viewportHeight * _viewportHeight) / Math.Max(1, totalWrappedLines));
    int maxThumbPos = _viewportHeight - vThumbHeight;
    vThumbPos = totalWrappedLines > _viewportHeight
        ? (int)Math.Round((double)_verticalScrollOffset / (totalWrappedLines - _viewportHeight) * maxThumbPos)
        : 0;
}
```

Then in the per-row loops (lines 2058-2069 and 2098-2109), replace the inline
calculations with the pre-computed values:
```csharp
bool isThumb = i >= vThumbPos && i < vThumbPos + vThumbHeight;
```

#### 7.2 Fix _skipUpdateScrollPositionsInRender reset (R8)

Add reset before early returns:
```csharp
if (targetWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }
// ...
if (effectiveWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }
```

#### 7.3 Fix DivisionByZero (R9)

Line 1310: replace `_effectiveWidth` with `SafeEffectiveWidth`:
```csharp
if (_cursorX > 0 && _cursorX % SafeEffectiveWidth == 0)
```

#### 7.4 Fix double invalidation (R13)

Line 270 (IsEditing setter):
```csharp
public bool IsEditing
{
    get => _isEditing;
    set
    {
        if (_isEditing == value) return; // Guard: no change = no work
        _isEditing = value;
        Container?.Invalidate(true); // Single invalidation
        EditingModeChanged?.Invoke(this, value); // From Phase 6
    }
}
```

#### 7.5 Fix scrollbar background (R15)

Use a fixed color for scrollbar background instead of focus-dependent `bgColor`:
```csharp
Color scrollbarBg = BackgroundColor; // Always unfocused bg, not bgColor
```

#### 7.6 Fix Escape key behavior (I15)

```csharp
case ConsoleKey.Escape:
    if (_hasSelection)
    {
        ClearSelection();
        Container?.Invalidate(true);
        return true; // Consumed: cleared selection
    }
    if (_isEditing)
    {
        IsEditing = false;
        return true; // Consumed: exited edit mode
    }
    return false; // Not consumed: let it bubble up
```

### Tasks Checklist

- [ ] **7.1** Pre-compute `totalWrappedLines`, `maxLineLength`, scrollbar thumb in PaintDOM
- [ ] **7.2** Fix `_skipUpdateScrollPositionsInRender` reset on early returns
- [ ] **7.3** Fix DivisionByZero: use `SafeEffectiveWidth` at line 1310
- [ ] **7.4** Fix double invalidation in `IsEditing` setter (add guard clause)
- [ ] **7.5** Fix scrollbar background flickering (use fixed background color)
- [ ] **7.6** Fix Escape key to bubble up when nothing to do
- [ ] **7.7** Replace magic numbers with `ControlDefaults` constants
- [ ] **7.8** Fix MeasureDOM scrollbar interaction (R7)
- [ ] **7.9** Add width validation in builder (B1)

---

## Phase 8: Advanced Features (P1/P2)

> **Status**: NOT STARTED
> **Priority**: LOW
> **Depends on**: Phases 1-6

These don't need detailed code sketches yet - implement after the foundation is solid.

### Tasks Checklist

- [ ] **8.1** Scroll margin (`ScrollMargin` property, modify `EnsureCursorVisible` to pad by N lines)
- [ ] **8.2** Line numbers gutter (`ShowLineNumbers` property, reserve gutter width in PaintDOM)
- [ ] **8.3** Current line highlighting (`HighlightCurrentLine` property)
- [ ] **8.4** Find / Find & Replace (`Find()`, `FindNext()`, `Replace()`, `ReplaceAll()` with match highlighting)
- [ ] **8.5** Move line up/down (Alt+Up/Down in ProcessKey)
- [ ] **8.6** Duplicate line (Ctrl+D or Ctrl+Shift+D)
- [ ] **8.7** Auto-indent on Enter (copy leading whitespace from current line)
- [ ] **8.8** Overwrite/Insert mode toggle (Insert key)
- [ ] **8.9** Syntax highlighting hooks (`ISyntaxHighlighter` interface, apply token colors in PaintDOM)
- [ ] **8.10** Visible whitespace toggle (`ShowWhitespace` property)

---

## Complete Bug Reference

### Critical Bugs (6)

| ID | Phase | Bug | Location |
|----|-------|-----|----------|
| R1 | 1 | WrapWords wrapping math inconsistent vs PaintDOM | Throughout |
| R2 | 1 | Selection rendering broken in WrapWords | 2021-2034 |
| R3 | 1 | Horizontal scroll set during wrap-mode typing | 1308-1318 |
| I1 | 2 | Mouse click doesn't position cursor | 1767-1776 |
| I2 | 1 | WrapWords cursor navigation wrong math | 960-1185 |
| I3 | 2 | No clipboard operations (Ctrl+C/X/V) | 1285-1291 |

### Major Bugs (4)

| ID | Phase | Bug | Location |
|----|-------|-----|----------|
| I4 | 3 | No mouse drag selection | 1761-1787 |
| I5 | 3 | No double-click word selection | 125-134 |
| I6 | 2 | No Ctrl+Arrow word navigation | 934-958 |
| I7 | 1 | End+Home doesn't roundtrip in wrap mode | 1057-1094 |

### Moderate Bugs (10)

| ID | Phase | Bug | Location |
|----|-------|-----|----------|
| R4 | 7 | GetTotalWrappedLineCount O(n) per row | 2058, 2098 |
| R5 | 7 | GetMaxLineLength called 3x per frame | 1801, 1841, 2130 |
| R6 | 1 | Phantom empty line in WrapWords after long words | 1896-1932 |
| R7 | 7 | MeasureDOM scrollbar interaction not handled | 1798-1801 |
| R8 | 7 | _skipUpdateScrollPositionsInRender not reset | 789-1977 |
| R9 | 7 | DivisionByZero when _effectiveWidth is 0 | 1310 |
| M2 | 5 | Tab characters render as single cell | 1294, 2040 |
| M3 | 5 | No CJK/emoji/combining char width handling | Throughout |
| M8 | 5 | Surrogate pair splitting = data corruption | 1199, 1227, 663, 718 |
| M9 | 1 | WrapWords cursor mismatch (same as R1) | 960-1001 |

### Significant Bugs (4)

| ID | Phase | Bug | Location |
|----|-------|-----|----------|
| I8 | 2 | No Select All (Ctrl+A) | 1285-1291 |
| I9 | 4 | No Undo/Redo | (absent) |
| I10 | 2 | Shift+PageUp/Down can't start selection | 909-912 |
| I11 | 2 | PageUp/Down w/o Shift doesn't clear selection | 924-927 |

### Minor Bugs (13)

| ID | Phase | Bug | Location |
|----|-------|-----|----------|
| R10 | 6 | Dead code: unused scrollbar render methods | 1532-1616 |
| R11 | 7 | Scrollbar thumb calc duplicated per-row | 2058-2103 |
| R12 | 7 | Magic numbers (80, 10) | 569, 1490, 1629 |
| R13 | 7 | Double invalidation in IsEditing | 270 |
| R14 | 7 | _effectiveWidth stale between frames | 1845 |
| R15 | 7 | Scrollbar bg flickers with focus | 2069, 2109 |
| R16 | 1 | WrapWords collapses consecutive spaces | 1891 |
| R17 | 1 | No wrapping cache between frames | 1850-1937 |
| R18 | 1 | Horizontal scroll applied in wrap mode | 2007-2010 |
| I12 | 3 | No Tab key handling | (absent) |
| I14 | 2 | Enter can start editing without focus | 889-898 |
| I15 | 7 | Escape always consumed | 1269-1283 |
| I17 | 2 | _isEditing set directly bypassing property | 780, 893, etc. |

### Builder Issues (2)

| ID | Phase | Bug | Location |
|----|-------|-----|----------|
| B1 | 7 | No width validation (allows 0) | Builder:109 |
| B2 | 7 | Wasteful invalidations during construction | Builder:477-493 |

### Positive Findings (Markup Safety)

| Finding | Details |
|---------|---------|
| Spectre markup injection: SAFE | Character-by-character buffer rendering bypasses parser |
| ANSI escape injection: SAFE | Sequences broken into individual cells |
| Plain text design: CORRECT | Consistent with control's purpose as text editor |

---

## New Files Created by This Plan

| File | Phase | Purpose |
|------|-------|---------|
| `Helpers/WordBoundaryHelper.cs` | 2 | Word boundary detection for navigation and selection |
| `Helpers/ClipboardHelper.cs` | 2 | Cross-platform clipboard access |
| `Helpers/UnicodeWidthHelper.cs` | 5 | Terminal display width for CJK, emoji, combining chars |

---

## Progress Tracking

| Phase | Description | Tasks | Done | Status |
|-------|-------------|-------|------|--------|
| 1 | WrapWords Foundation | 15 | 0 | NOT STARTED |
| 2 | Critical Input Bugs | 12 | 0 | NOT STARTED |
| 3 | P0 Missing Features | 6 | 0 | NOT STARTED |
| 4 | Undo/Redo System | 8 | 0 | NOT STARTED |
| 5 | Unicode & Special Chars | 7 | 0 | NOT STARTED |
| 6 | Events & Public API | 9 | 0 | NOT STARTED |
| 7 | Performance & Polish | 9 | 0 | NOT STARTED |
| 8 | Advanced Features | 10 | 0 | NOT STARTED |
| **Total** | | **76** | **0** | |
