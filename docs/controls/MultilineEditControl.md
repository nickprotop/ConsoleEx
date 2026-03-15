# MultilineEditControl

A full-featured multiline text editor with syntax highlighting, pluggable gutter system, find/replace, undo/redo, word wrap, and extensive keyboard/mouse interaction.

## Overview

The `MultilineEditControl` is one of SharpConsoleUI's most powerful controls — a complete text editing component comparable to editors found in production IDEs and developer tools. It supports two interaction modes: **browse mode** (scroll/navigate with arrow keys) and **edit mode** (full text editing with Enter to activate, Escape to exit).

The control features a pluggable **gutter system** where multiple renderers (line numbers, breakpoint markers, diff indicators) can be stacked side-by-side. A pluggable **syntax highlighting** interface lets you provide language-specific colorization with full support for multi-line constructs like block comments and string literals. The token and wrapping caches are automatically invalidated on content changes for efficient re-rendering.

Thread-safe content access makes it safe to append log output or streaming data from background tasks. The built-in **find and replace** system supports plain text and regex matching with match highlighting, navigation, and batch replacement. A full **undo/redo** stack tracks all edits with cursor position restoration.

See also: [PromptControl](PromptControl.md) (single-line input)

## Quick Start

```csharp
var editor = Controls.MultilineEdit()
    .WithContent("Hello, World!")
    .WithLineNumbers()
    .WithHighlightCurrentLine()
    .WithAutoIndent()
    .OnContentChanged((s, content) => { /* handle changes */ })
    .Build();

window.AddControl(editor);
```

## Builder API

Create a `MultilineEditControlBuilder` through the `Controls` factory:

```csharp
var builder = Controls.MultilineEdit("optional initial content");
```

### Content Methods

```csharp
.WithContent(string content)              // Set initial content as single string
.WithContentLines(params string[] lines)  // Set initial content from lines
.WithContentLines(IEnumerable<string> lines)
```

### Layout Methods

```csharp
.WithViewportHeight(int height)           // Visible lines (default: 10)
.WithWidth(int width)                     // Control width in characters
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int uniform)                  // Uniform margin on all sides
.WithAlignment(HorizontalAlignment alignment)
.Centered()                               // Center horizontally
.WithVerticalAlignment(VerticalAlignment alignment)
.WithStickyPosition(StickyPosition position)
```

### Wrap Mode Methods

```csharp
.WithWrapMode(WrapMode mode)              // Set wrap mode directly
.NoWrap()                                 // No wrapping, horizontal scroll
.WrapWords()                              // Wrap at word boundaries
.WrapCharacters()                         // Wrap at character boundaries
```

### Scrollbar Methods

```csharp
.WithVerticalScrollbar(ScrollbarVisibility visibility)   // Auto, Always, Never
.WithHorizontalScrollbar(ScrollbarVisibility visibility)
```

### Color Methods

```csharp
.WithColors(Color foreground, Color background)
.WithFocusedColors(Color foreground, Color background)
.WithSelectionColors(Color foreground, Color background)
.WithScrollbarColors(Color trackColor, Color thumbColor)
.WithBorderColor(Color color)
.WithBackgroundColor(Color color)
.WithForegroundColor(Color color)
.WithLineNumberColor(Color color)
.WithCurrentLineHighlightColor(Color color)
```

### Editor Feature Methods

```csharp
.WithLineNumbers(bool show = true)        // Show line number gutter
.WithHighlightCurrentLine(bool highlight = true)
.WithShowWhitespace(bool show = true)     // Visible space markers (·)
.WithEditingHints(bool show = true)       // "Enter to edit" / "Esc to exit"
.WithAutoIndent(bool autoIndent = true)   // Inherit indentation on Enter
.WithOverwriteMode(bool overwrite = true) // Insert vs overwrite toggle
.WithTabSize(int tabSize)                 // Spaces per Tab (1-8, default: 4)
.WithMaxLength(int maxLength)             // Maximum character count
.WithUndoLimit(int limit)                 // Undo history depth (default: 100)
.WithPlaceholder(string text)             // Placeholder when empty
.WithEscapeExitsEditMode(bool exits = true) // false for IDE-style editors
.AsReadOnly(bool readOnly = true)         // Navigate/select but no editing
.IsEditing(bool isEditing = true)         // Start in edit mode
```

### Extensibility Methods

```csharp
.WithSyntaxHighlighter(ISyntaxHighlighter highlighter) // Attach syntax coloring
.WithGutterRenderer(IGutterRenderer renderer)          // Add custom gutter renderer
```

### Event Methods

```csharp
.OnContentChanged(EventHandler<string> handler)
.OnContentChanged(WindowEventHandler<string> handler)  // With window access
.OnCursorPositionChanged(EventHandler<(int Line, int Column)> handler)
.OnSelectionChanged(EventHandler<string> handler)
.OnEditingModeChanged(EventHandler<bool> handler)
.OnOverwriteModeChanged(EventHandler<bool> handler)
.OnGotFocus(EventHandler handler)
.OnGotFocus(WindowEventHandler<EventArgs> handler)     // With window access
.OnLostFocus(EventHandler handler)
.OnLostFocus(WindowEventHandler<EventArgs> handler)    // With window access
```

### Standard Methods

```csharp
.WithName(string name)
.WithTag(object tag)
.Visible(bool visible = true)
.Enabled(bool enabled = true)
.Disabled()
.Build()
```

## Properties

### Content & State

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Content` | `string` | `""` | Get/set full text content with line breaks |
| `LineCount` | `int` | `1` | Total number of source lines (read-only) |
| `CurrentLine` | `int` | `1` | Cursor line number, 1-based (read-only) |
| `CurrentColumn` | `int` | `1` | Cursor column number, 1-based (read-only) |
| `IsEditing` | `bool` | `false` | Whether the control is in text editing mode |
| `ReadOnly` | `bool` | `false` | Allow navigation/selection but prevent modifications |
| `IsModified` | `bool` | `false` | Whether content changed since last `MarkAsSaved()` |
| `IsEnabled` | `bool` | `true` | Whether the control accepts any interaction |
| `OverwriteMode` | `bool` | `false` | Insert vs overwrite mode (toggled by Insert key) |
| `PlaceholderText` | `string?` | `null` | Text shown when empty and not editing |
| `MaxLength` | `int?` | `null` | Maximum total character count (null = unlimited) |

### Layout & Display

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ViewportHeight` | `int` | `10` | Number of visible lines |
| `WrapMode` | `WrapMode` | `Wrap` | Text wrapping: `NoWrap`, `Wrap`, `WrapWords` |
| `TabSize` | `int` | `4` | Spaces per tab (1-8) |
| `UndoLimit` | `int` | `100` | Maximum undo history depth |
| `AutoIndent` | `bool` | `false` | Copy leading whitespace on Enter |
| `ShowLineNumbers` | `bool` | `false` | Display line numbers in gutter |
| `ShowWhitespace` | `bool` | `false` | Show spaces as middle dots (·) |
| `HighlightCurrentLine` | `bool` | `false` | Highlight the cursor's line |
| `ShowEditingHints` | `bool` | `false` | Show "Enter to edit" / "Esc to exit" hints |
| `EscapeExitsEditMode` | `bool` | `true` | Whether Escape leaves edit mode |

### Scrolling

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `VerticalScrollbarVisibility` | `ScrollbarVisibility` | `Auto` | When to show vertical scrollbar |
| `HorizontalScrollbarVisibility` | `ScrollbarVisibility` | `Auto` | When to show horizontal scrollbar |
| `VerticalScrollOffset` | `int` | `0` | Lines scrolled from top (read-only) |
| `HorizontalScrollOffset` | `int` | `0` | Columns scrolled from left (read-only) |

### Colors

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BackgroundColor` | `Color` | Theme | Background when not focused |
| `ForegroundColor` | `Color` | Theme | Foreground when not focused |
| `FocusedBackgroundColor` | `Color` | Theme | Background when focused |
| `FocusedForegroundColor` | `Color` | Theme | Foreground when focused |
| `BorderColor` | `Color` | `White` | Border outline color |
| `SelectionBackgroundColor` | `Color` | Theme | Selected text background |
| `SelectionForegroundColor` | `Color` | Theme | Selected text foreground |
| `ScrollbarColor` | `Color` | Theme | Scrollbar track color |
| `ScrollbarThumbColor` | `Color` | Theme | Scrollbar handle color |
| `LineNumberColor` | `Color` | `Grey` | Line number gutter foreground |
| `CurrentLineHighlightColor` | `Color` | `Grey11` | Current line background |

### Extensibility

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SyntaxHighlighter` | `ISyntaxHighlighter?` | `null` | Syntax coloring provider |
| `GutterRenderers` | `IReadOnlyList<IGutterRenderer>` | `[]` | Registered gutter renderers |
| `LineHighlights` | `Dictionary<int, Color>` | `{}` | Per-line background colors (0-based index) |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `ContentChanged` | `string` | Fires when text content changes |
| `CursorPositionChanged` | `(int Line, int Column)` | Fires on cursor movement (1-based) |
| `SelectionChanged` | `string` | Fires on selection change (selected text or empty) |
| `EditingModeChanged` | `bool` | Fires when entering/leaving edit mode |
| `OverwriteModeChanged` | `bool` | Fires when Insert key toggles overwrite mode |
| `MatchCountChanged` | `int` | Fires when find/replace match count changes |
| `GutterClick` | `GutterClickEventArgs` | Fires when user clicks the gutter area |
| `GotFocus` | `EventArgs` | Fires when control receives keyboard focus |
| `LostFocus` | `EventArgs` | Fires when control loses keyboard focus |
| `MouseClick` | `MouseEventArgs` | Fires on left-click |
| `MouseDoubleClick` | `MouseEventArgs` | Fires on double-click (selects word) |
| `MouseRightClick` | `MouseEventArgs` | Fires on right-click (context menu hook) |

## Content Manipulation API

```csharp
// Get/set content
string text = editor.GetContent();
editor.SetContent("new content");
editor.SetContentLines(new List<string> { "line 1", "line 2" });

// Append content (thread-safe, auto-scrolls to end)
editor.AppendContent("new text\n");
editor.AppendContentLines(new List<string> { "log entry 1", "log entry 2" });

// Insert at cursor position
editor.InsertText("inserted text");
editor.DeleteCharsBefore(5);

// Cursor navigation
editor.GoToLine(42);              // Jump to line (1-based)
editor.GoToEnd();                 // Jump to document end
editor.EnsureCursorVisible();     // Scroll cursor into view

// Selection
string selected = editor.GetSelectedText();
editor.SelectRange(0, 0, 5, 10); // Select range (0-based)
editor.ClearSelection();

// Undo/redo
editor.MarkAsSaved();            // Reset IsModified tracking
bool modified = editor.IsModified;
```

## Find and Replace API

```csharp
// Search
int matches = editor.Find("pattern");                    // Plain text search
int matches = editor.Find("pattern", caseSensitive: true);
int matches = editor.Find(@"\d+", useRegex: true);      // Regex search

// Navigate matches
editor.FindNext();        // Jump to next match (wraps around)
editor.FindPrevious();    // Jump to previous match (wraps around)

// Replace
editor.Replace("replacement");    // Replace current match, advance to next
int count = editor.ReplaceAll("replacement");  // Replace all matches

// Query state
string? term = editor.SearchTerm;
int matchCount = editor.MatchCount;
int currentIdx = editor.CurrentMatchIndex;  // 0-based, -1 if none
bool active = editor.HasActiveSearch;

// Clear
editor.ClearFind();       // Remove all match highlighting
```

## Keyboard Support

### Browse Mode (focused, not editing)

| Key | Action |
|-----|--------|
| Enter | Enter edit mode |
| Arrow Keys | Scroll content |
| Page Up/Down | Page scroll |
| Home | Scroll to document top |
| End | Scroll to document bottom |

### Edit Mode

| Key | Action |
|-----|--------|
| Arrow Keys | Move cursor |
| Ctrl+Left/Right | Word boundary navigation |
| Home/End | Line start/end |
| Ctrl+Home/End | Document start/end |
| Page Up/Down | Move cursor by viewport height |
| Shift+Arrows | Extend selection |
| Shift+Page Up/Down | Extend selection by page |
| Shift+Home/End | Select to line start/end |
| Ctrl+Shift+Home/End | Select to document start/end |
| Backspace | Delete character before cursor |
| Delete | Delete character after cursor |
| Ctrl+Backspace | Delete word before cursor |
| Ctrl+Delete | Delete word after cursor |
| Tab | Insert tab spaces |
| Shift+Tab | Remove indentation |
| Enter | Insert new line (with auto-indent if enabled) |
| Ctrl+A | Select all |
| Ctrl+C | Copy selection to clipboard |
| Ctrl+X | Cut selection to clipboard |
| Ctrl+V | Paste from clipboard |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+D | Duplicate current line(s) |
| Alt+Up | Move line(s) up |
| Alt+Down | Move line(s) down |
| Insert | Toggle overwrite mode |
| Escape | Exit edit mode (if `EscapeExitsEditMode` is true) |

## Mouse Support

| Interaction | Action |
|-------------|--------|
| Left click | Position cursor, enter edit mode |
| Double-click | Select word at click position |
| Right-click | Fire `MouseRightClick` event (for context menus) |
| Click + drag | Select text range |
| Gutter click | Fire `GutterClick` event with source line index |
| Scrollbar thumb drag | Scroll content smoothly |
| Scrollbar track click | Page up/down |
| Scrollbar arrows | Scroll by single line |

## Extensibility

### Syntax Highlighting

Implement `ISyntaxHighlighter` to provide language-specific colorization:

```csharp
public interface ISyntaxHighlighter
{
    (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
        Tokenize(string line, int lineIndex, SyntaxLineState startState);
}

// SyntaxToken specifies a colored span within a line
public readonly record struct SyntaxToken(
    int StartIndex,   // 0-based character position
    int Length,        // Number of characters
    Color ForegroundColor
);

// SyntaxLineState carries parser state between lines
// Subclass for language-specific state (e.g. "inside block comment")
public record SyntaxLineState
{
    public static readonly SyntaxLineState Initial = new();
}
```

**Usage:**
```csharp
var editor = Controls.MultilineEdit()
    .WithSyntaxHighlighter(new CSharpSyntaxHighlighter())
    .WithLineNumbers()
    .Build();
```

The token cache is automatically managed — when content changes, only affected lines and their successors are re-tokenized.

### Custom Gutter Renderers

Implement `IGutterRenderer` to add custom content in the left gutter area (breakpoint markers, diff indicators, fold toggles, etc.):

```csharp
public interface IGutterRenderer
{
    int GetWidth(int totalLineCount);  // How many columns this renderer needs
    void Render(in GutterRenderContext context, int width);  // Paint one row
}
```

**GutterRenderContext** provides:
- `Buffer` — the character buffer to paint into
- `X`, `Y` — coordinates for this renderer's area
- `SourceLineIndex` — 0-based line index (-1 if beyond content)
- `IsFirstWrappedSegment` — false for continuation rows from wrapping
- `IsCursorLine` — whether this row contains the cursor
- `HasFocus` — whether the editor has keyboard focus
- `ForegroundColor`, `BackgroundColor` — current editor colors
- `TotalLineCount` — total source lines in document

**Example — breakpoint gutter:**
```csharp
public class BreakpointGutterRenderer : IGutterRenderer
{
    private readonly HashSet<int> _breakpoints = new();

    public int GetWidth(int totalLineCount) => 2;

    public void Render(in GutterRenderContext context, int width)
    {
        char marker = _breakpoints.Contains(context.SourceLineIndex) ? '●' : ' ';
        Color fg = _breakpoints.Contains(context.SourceLineIndex) ? Color.Red : context.ForegroundColor;
        context.Buffer.SetNarrowCell(context.X, context.Y, marker, fg, context.BackgroundColor);
        context.Buffer.SetNarrowCell(context.X + 1, context.Y, ' ', fg, context.BackgroundColor);
    }

    public void ToggleBreakpoint(int line) {
        if (!_breakpoints.Add(line)) _breakpoints.Remove(line);
    }
}
```

**Usage:**
```csharp
var bpRenderer = new BreakpointGutterRenderer();

var editor = Controls.MultilineEdit()
    .WithLineNumbers()                  // Line numbers at index 0
    .WithGutterRenderer(bpRenderer)     // Breakpoints after line numbers
    .Build();

editor.GutterClick += (s, e) => {
    if (e.SourceLineIndex >= 0)
        bpRenderer.ToggleBreakpoint(e.SourceLineIndex);
};
```

Multiple renderers are painted left-to-right in registration order.

### Per-Line Highlights

Set background colors on individual source lines programmatically:

```csharp
editor.SetLineHighlight(5, Color.DarkRed);    // Highlight line 5 (0-based)
editor.SetLineHighlight(10, Color.DarkGreen);
editor.SetLineHighlight(5, null);             // Clear highlight on line 5
editor.ClearLineHighlights();                 // Clear all highlights
```

## Examples

### Simple Note Editor

```csharp
var editor = Controls.MultilineEdit()
    .WithViewportHeight(15)
    .WrapWords()
    .WithEditingHints()
    .WithPlaceholder("Start typing...")
    .Build();
```

### Code Editor with Syntax Highlighting

```csharp
var editor = Controls.MultilineEdit()
    .WithContent(sourceCode)
    .NoWrap()
    .WithLineNumbers()
    .WithHighlightCurrentLine()
    .WithAutoIndent()
    .WithTabSize(4)
    .WithSyntaxHighlighter(new CSharpSyntaxHighlighter())
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithEscapeExitsEditMode(false)
    .OnCursorPositionChanged((s, pos) =>
    {
        statusBar.SetLines($"Ln {pos.Line}, Col {pos.Column}");
    })
    .Build();
```

### Read-Only Log Viewer

```csharp
var logViewer = Controls.MultilineEdit()
    .AsReadOnly()
    .WrapWords()
    .WithVerticalScrollbar(ScrollbarVisibility.Always)
    .WithColors(Color.Green, Color.Black)
    .Build();

// Append from background thread (thread-safe)
Task.Run(async () => {
    while (true)
    {
        logViewer.AppendContent($"[{DateTime.Now:HH:mm:ss}] Event\n");
        await Task.Delay(1000);
    }
});
```

### Editor with Status Bar and Find

```csharp
var editor = Controls.MultilineEdit()
    .WithLineNumbers()
    .WithHighlightCurrentLine()
    .WithShowWhitespace()
    .OnCursorPositionChanged((s, pos) =>
    {
        status.SetLines($"Ln {pos.Line}, Col {pos.Column} | {editor.LineCount} lines");
    })
    .Build();

// Find usage
int matches = editor.Find("TODO", caseSensitive: false);
// Navigate: editor.FindNext(), editor.FindPrevious()
// Replace: editor.Replace("DONE"), editor.ReplaceAll("DONE")
```

### IDE-Style Editor with Custom Gutter

```csharp
var breakpoints = new BreakpointGutterRenderer();

var editor = Controls.MultilineEdit()
    .WithContent(sourceCode)
    .NoWrap()
    .WithLineNumbers()
    .WithGutterRenderer(breakpoints)
    .WithHighlightCurrentLine()
    .WithAutoIndent()
    .WithSyntaxHighlighter(new CSharpSyntaxHighlighter())
    .WithEscapeExitsEditMode(false)
    .Build();

editor.GutterClick += (s, e) =>
{
    if (e.SourceLineIndex >= 0)
    {
        breakpoints.ToggleBreakpoint(e.SourceLineIndex);
        editor.Container?.Invalidate(true);
    }
};
```

## Best Practices

1. **Use `VerticalAlignment.Fill`** for editors that should expand to fill their container — avoids hardcoding viewport height.

2. **Use `NoWrap()` for code editors** — code is typically not word-wrapped. Use `WrapWords()` for prose/notes/logs.

3. **Set `EscapeExitsEditMode(false)` for IDE-style editors** where Escape is needed for dismissing dialogs or canceling operations.

4. **Use `AppendContent()` for streaming data** — it's thread-safe and auto-scrolls to the bottom, ideal for log viewers.

5. **Implement `ISyntaxHighlighter` with state tracking** — use `SyntaxLineState` subclasses to handle multi-line constructs (block comments, multi-line strings). The cache ensures only modified lines are re-tokenized.

6. **Combine gutter renderers** for rich editor chrome — line numbers + breakpoints + git diff markers can all coexist in the gutter.

7. **Use `MarkAsSaved()` and `IsModified`** to track unsaved changes and prompt before closing.

## See Also

- [PromptControl](PromptControl.md) - Single-line text input
- [ListControl](ListControl.md) - Scrollable item list

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
