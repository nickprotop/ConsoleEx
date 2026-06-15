# MarkupControl

Display rich formatted text using SharpConsoleUI markup syntax.

## Overview

MarkupControl displays multi-line text with rich formatting using SharpConsoleUI's markup syntax. Supports colors, styles (bold, italic, underline), and more.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | Empty | Content as a single newline-separated string (get/set). Set content via `SetContent(List<string>)` or `AppendLine`/`AppendLines`. |
| `Wrap` | `bool` | `true` | Word-wrap text to the available width |
| `BackgroundColor` | `Color?` | `null` | Background color (falls back to container) |
| `ForegroundColor` | `Color?` | `null` | Text color (falls back to container) |
| `EnableSelection` | `bool` | `false` | Opt-in mouse text selection + copy shortcut (see below) |
| `SelectionForegroundColor` | `Color?` | `null` | Foreground color for selected text |
| `SelectionBackgroundColor` | `Color?` | `null` | Background color for selected text |
| `HasSelection` | `bool` | `false` | (read-only) Whether text is currently selected |
| `CopyEnabled` | `bool` | `true` | Whether the keyboard copy shortcut is active |
| `CopyKey` | `ConsoleKey` | `C` | Key that triggers a copy |
| `CopyModifiers` | `ConsoleModifiers` | `Control` | Modifiers required for the copy shortcut |
| `MarkdownStyle` | `MarkdownStyle?` | `null` | Per-control style for `[markdown]` content; `null` uses `MarkdownStyle.Default` (see [Markdown](#markdown)) |
| `IsEnabled` | `bool` | `true` | Whether the control accepts keyboard input (link navigation). A disabled control is never a focus stop |
| `FocusedLinkForegroundColor` | `Color?` | `null` | Foreground for the keyboard-focused link highlight; `null` uses a high-contrast default (see [Links](#links)) |
| `FocusedLinkBackgroundColor` | `Color?` | `null` | Background for the keyboard-focused link highlight; `null` uses a high-contrast default |

## Methods

| Method | Description |
|--------|-------------|
| `SetContent(List<string>)` | Replaces all content |
| `SetMarkdown(string)` | Replaces the content with rendered Markdown (see [Markdown](#markdown)) |
| `AppendLine(string)` | Appends a single markup line |
| `AppendLines(IEnumerable<string>)` | Appends multiple markup lines |
| `AppendText(string)` | Appends text, splitting on `\n` |
| `GetSelectedText()` | Returns the current selection as plain text (markup stripped) |
| `ClearSelection()` | Clears the current selection |
| `CopySelectionToClipboard()` | Copies the selection to the clipboard; returns `true` if anything was copied |
| `CopyToClipboard()` | Copies the entire content (plain text) to the clipboard |

## Events

| Event | Type | Description |
|-------|------|-------------|
| `LinkClicked` | `EventHandler<LinkClickedEventArgs>` | Fires when a rendered link is activated by mouse click **or** keyboard (Enter); payload exposes `Url` and `Text` (see [Links](#links)) |
| `SelectionChanged` | `EventHandler<string>` | Fires when the selection changes; payload is the selected plain text (empty when cleared) |
| `TextSelectionChanged` | `EventHandler<TextSelectionChangedEventArgs>` | Richer companion carrying `HasSelection` and `SelectedText`; fires together with `SelectionChanged` |
| `MouseRightClick` | `EventHandler<MouseEventArgs>` | Fires on right-click (surface for a context menu — see below) |

## Creating Markup

### Using Builder (Recommended)

```csharp
var markup = Controls.Markup()
    .AddLine("[bold yellow]Welcome to SharpConsoleUI![/]")
    .AddLine("")
    .AddLine("Features:")
    .AddLine("  [green]• Modern UI controls[/]")
    .AddLine("  [green]• Async support[/]")
    .AddLine("  [green]• Rich formatting[/]")
    .AddLine("")
    .AddLine("[dim]Press any key to continue...[/]")
    .WithAlignment(HorizontalAlignment.Center)
    .Build();

window.AddControl(markup);
```

### Using Constructor

```csharp
var markup = new MarkupControl(new List<string>
{
    "[bold yellow]Title[/]",
    "",
    "Regular text",
    "[green]Success message[/]",
    "[red]Error message[/]"
});

window.AddControl(markup);
```

### Using Static Helpers

```csharp
// Simple text
window.AddControl(Controls.Label("Plain text"));

// Formatted shortcuts
window.AddControl(Controls.Header("Section Title"));  // Bold yellow
window.AddControl(Controls.Info("Information"));      // Blue
window.AddControl(Controls.Warning("Warning"));       // Orange
window.AddControl(Controls.Error("Error"));           // Red
window.AddControl(Controls.Success("Success"));       // Green
```

## Markup Syntax

SharpConsoleUI uses its own markup syntax (Spectre-compatible):

### Colors

```csharp
"[red]Red text[/]"
"[green]Green text[/]"
"[blue]Blue text[/]"
"[yellow]Yellow text[/]"
"[cyan]Cyan text[/]"
"[magenta]Magenta text[/]"
"[white]White text[/]"
"[black]Black text[/]"

// Extended colors
"[orange3]Orange text[/]"
"[purple]Purple text[/]"
"[grey]Grey text[/]"

// RGB colors
"[rgb(255,128,0)]Custom color[/]"
"[#FF8000]Hex color[/]"
```

### Styles

```csharp
"[bold]Bold text[/]"
"[italic]Italic text[/]"
"[underline]Underlined text[/]"
"[strikethrough]Strikethrough text[/]"
"[dim]Dimmed text[/]"
"[reverse]Reversed text[/]"
```

### Combined Markup

```csharp
"[bold red]Bold red text[/]"
"[italic blue]Italic blue text[/]"
"[bold yellow underline]Multiple styles[/]"
```

### Escaping

```csharp
"Use [[double brackets]] to display literal brackets"
"This shows [red]colored[/] and [[red]] literal markup"
```

## Text Selection & Copy

By default `MarkupControl` is display-only — its text cannot be selected or copied. Set
`EnableSelection = true` (opt-in, off by default, WinUI-style) to make the control
mouse-selectable. The user can then:

- **Drag** to select a range of text
- **Double-click** to select a word
- **Triple-click** to select a line
- Press **Ctrl+C** to copy the selection to the clipboard

The copied text is always **plain text** — all markup tags are stripped automatically. When a
selected line is soft-wrapped across multiple display rows, it is copied back as a single line.

Selection is coordinated **per window**: only one control can hold the active selection at a time,
so starting a selection in one control clears any selection in another. **Ctrl+C** is handled at the
window level and copies whatever is currently selected. **Left-clicking** empty space clears the
selection; **right-click** is surfaced to the application (e.g. to show a context menu) and does not
affect the selection.

Because selection is off by default, existing applications are unaffected.

### Enabling Selection

```csharp
// Via property
var markup = new MarkupControl(new List<string> { "[bold]Selectable[/] output" })
{
    EnableSelection = true,
    SelectionForegroundColor = Color.Black,            // optional
    SelectionBackgroundColor = new Color(95, 175, 255) // optional
};

// Via fluent builder
var markup = Controls.Markup("[green]Build succeeded[/] in 3.4s")
    .WithSelectionEnabled()
    .WithSelectionColors(Color.Black, new Color(95, 175, 255)) // optional
    .Build();
```

### Reading the Selection

```csharp
if (markup.HasSelection)
{
    string plain = markup.GetSelectedText(); // markup-free
}

markup.SelectionChanged += (sender, selectedText) =>
{
    // selectedText is the current plain-text selection ("" when cleared)
};

// Richer event carrying both the state and the text:
markup.TextSelectionChanged += (sender, e) =>
{
    // e.HasSelection, e.SelectedText
};

markup.ClearSelection();
```

The control implements `ISelectableControl`, so it participates in the window's
`SelectionManager` (`window.SelectionManager.ActiveSelection` / `GetSelectedText()`).
`MultilineEditControl` also implements `ISelectableControl`, so an editor and selectable markup
controls in the same window share the single-selection behavior.

> Copy works locally **and over SSH** — see [Clipboard, Copy & Paste](../CLIPBOARD.md) for how OSC 52
> carries the copy to the local clipboard over a remote session, and how to configure it.

### Programmatic Copy

```csharp
// Copy the current selection (plain text). Returns false if nothing is selected.
markup.CopySelectionToClipboard();

// Copy the control's entire content (plain text), ignoring the selection.
markup.CopyToClipboard();
```

### Customizing the Copy Shortcut

The keyboard copy shortcut defaults to **Ctrl+C** and is handled at the window level. It can be
remapped or disabled per control (programmatic copy is unaffected):

```csharp
markup.CopyKey = ConsoleKey.Y;                  // copy on Ctrl+Y
markup.CopyModifiers = ConsoleModifiers.Control;
markup.CopyEnabled = false;                      // disable the shortcut entirely

// Or via the builder:
Controls.Markup("...")
    .WithSelectionEnabled()
    .WithCopyKey(ConsoleKey.Y)        // Ctrl+Y
    .WithCopyEnabled(true)
    .Build();
```

### Appending Content

```csharp
markup.AppendLine("[green]New line[/]");
markup.AppendLines(new[] { "line 2", "line 3" });
markup.AppendText("multi\nline\ntext");   // splits on \n
```

### Right-Click Context Menu

Right-click is surfaced via the `MouseRightClick` event — the control does not show a menu itself,
leaving the app free to present its own (e.g. Copy / Copy All / Clear).

A context menu is typically shown as a **portal** anchored at the click point, hosting a vertical
`MenuControl`. The pattern below mirrors the **Selectable Text** screen in the DemoApp:

```csharp
// A small reusable portal hosting a vertical MenuControl.
internal sealed class ContextMenuPortal : PortalContentContainer
{
    private readonly MenuControl _menu;
    public event EventHandler<MenuItem>? ItemSelected;

    public ContextMenuPortal(IEnumerable<MenuItem> items, int anchorX, int anchorY,
        int windowWidth, int windowHeight)
    {
        _menu = new MenuControl { Orientation = MenuOrientation.Vertical };
        foreach (var item in items) _menu.AddItem(item);
        _menu.ItemSelected += (_, mi) => ItemSelected?.Invoke(this, mi);

        DismissOnOutsideClick = true;          // library auto-dismisses on outside click
        BorderStyle = BoxChars.Rounded;
        PortalFocusedControl = _menu;
        AddChild(_menu);
        SetFocusOnFirstChild();

        int w = 24, h = _menu /* item count */ is var _ ? 6 : 6;
        // Anchor + bounds are in window CONTENT space (0,0 = first content row); Below opens the
        // menu one line under the click. Convert window-space click coords with `- 1` (see below).
        var pos = PortalPositioner.CalculateFromPoint(
            new Point(anchorX, anchorY), new Size(w, h),
            new Rectangle(0, 0, windowWidth - 2, windowHeight - 2),
            PortalPlacement.Below);
        PortalBounds = pos.Bounds;
    }
}

// Wiring it to a selectable MarkupControl:
var markup = Controls.Markup("[green]Build succeeded[/]").WithSelectionEnabled().Build();

markup.MouseRightClick += (sender, args) =>
{
    var items = new[]
    {
        new MenuItem { Text = "Copy",  Shortcut = "Ctrl+C", IsEnabled = markup.HasSelection },
        new MenuItem { Text = "Copy All" },
        new MenuItem { IsSeparator = true },
        new MenuItem { Text = "Clear Selection", IsEnabled = markup.HasSelection },
    };

    // args.WindowPosition is window-space (title/border at 0). Subtract the 1-cell border so the
    // portal — positioned in content-space — opens exactly one line below the click.
    var portal = new ContextMenuPortal(items,
        args.WindowPosition.X - 1, args.WindowPosition.Y - 1,
        window.Width, window.Height) { Container = window };

    var node = window.CreatePortal(markup, portal);

    portal.ItemSelected += (_, mi) =>
    {
        window.RemovePortal(markup, node);
        switch (mi.Text)
        {
            case "Copy":            markup.CopySelectionToClipboard(); break;
            case "Copy All":        markup.CopyToClipboard();          break;
            case "Clear Selection": markup.ClearSelection();           break;
        }
    };
    portal.DismissRequested += (_, _) => { /* portal already removed by the library */ };
};
```

> **Coordinate note:** the menu is rendered as a portal arranged in window *content* space, while
> `MouseEventArgs.WindowPosition` is in window space (the title/border occupies row 0). Subtract the
> 1-cell border (`- 1`) from the click coordinates so `PortalPlacement.Below` places the menu top
> exactly one row beneath the cursor.

## Examples

### Welcome Screen

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("")
        .AddLine("[bold yellow]╔═══════════════════════════════╗[/]")
        .AddLine("[bold yellow]║   Welcome to My Application   ║[/]")
        .AddLine("[bold yellow]╚═══════════════════════════════╝[/]")
        .AddLine("")
        .AddLine("[dim]Version 1.0.0[/]")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Status Messages

```csharp
// Success
window.AddControl(Controls.Success("File saved successfully!"));

// Error
window.AddControl(Controls.Error("Failed to load file"));

// Warning
window.AddControl(Controls.Warning("Disk space running low"));

// Info
window.AddControl(Controls.Info("Checking for updates..."));
```

### Formatted List

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold yellow]Available Commands:[/]")
        .AddLine("")
        .AddLine("  [green]/help[/]     - Show this help message")
        .AddLine("  [green]/quit[/]     - Exit the application")
        .AddLine("  [green]/clear[/]    - Clear the screen")
        .AddLine("  [green]/settings[/] - Open settings")
        .Build()
);
```

### Dynamic Content Updates

```csharp
var status = Controls.Markup()
    .AddLine("[dim]Status: Ready[/]")
    .WithName("statusDisplay")
    .Build();

window.AddControl(status);

// Later, update the content
var statusControl = window.FindControl<MarkupControl>("statusDisplay");
if (statusControl != null)
{
    statusControl.SetContent(new List<string>
    {
        "[green]Status: Connected[/]",
        "[dim]Last updated: " + DateTime.Now.ToString("HH:mm:ss") + "[/]"
    });
}
```

### Progress Indicator

```csharp
void UpdateProgress(int percent, MarkupControl control)
{
    int barWidth = 30;
    int filled = (int)((percent / 100.0) * barWidth);
    string bar = new string('█', filled) + new string('░', barWidth - filled);

    control.SetContent(new List<string>
    {
        $"[bold]Progress:[/] {percent}%",
        $"[cyan]{bar}[/]"
    });
}

var progress = Controls.Markup()
    .WithName("progress")
    .Build();

window.AddControl(progress);

// Update progress
for (int i = 0; i <= 100; i += 10)
{
    var progressControl = window.FindControl<MarkupControl>("progress");
    if (progressControl != null)
    {
        UpdateProgress(i, progressControl);
    }
    await Task.Delay(100);
}
```

### Color-coded Logs

```csharp
var logDisplay = Controls.Markup()
    .WithName("logs")
    .Build();

window.AddControl(logDisplay);

// Add log entries
void AddLog(string level, string message)
{
    var logs = window.FindControl<MarkupControl>("logs");
    if (logs == null) return;

    var color = level switch
    {
        "ERROR" => "red",
        "WARN" => "yellow",
        "INFO" => "blue",
        "DEBUG" => "grey",
        _ => "white"
    };

    var timestamp = DateTime.Now.ToString("HH:mm:ss");
    var lines = logs.Text.Split('\n').ToList();
    lines.Add($"[dim]{timestamp}[/] [{color}]{level}[/] {message}");

    // Keep only last 20 lines
    if (lines.Count > 20)
        lines.RemoveAt(0);

    logs.SetContent(lines);
}

AddLog("INFO", "Application started");
AddLog("WARN", "Configuration file not found");
AddLog("ERROR", "Failed to connect to database");
```

### Table-like Display

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold]Name          Age    City[/]")
        .AddLine("[dim]─────────────────────────────────[/]")
        .AddLine("Alice         30     New York")
        .AddLine("Bob           25     London")
        .AddLine("Charlie       35     Tokyo")
        .Build()
);
```

### Banner/Header

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold cyan on blue]                                        [/]")
        .AddLine("[bold cyan on blue]     MY APPLICATION - v2.0.0             [/]")
        .AddLine("[bold cyan on blue]                                        [/]")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Multi-column Text

```csharp
window.AddControl(
    Controls.HorizontalGrid()
        .Column(col => col.Add(
            Controls.Markup()
                .AddLine("[bold yellow]Left Column[/]")
                .AddLine("Content on")
                .AddLine("the left side")
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Markup()
                .AddLine("[bold yellow]Middle Column[/]")
                .AddLine("Content in")
                .AddLine("the middle")
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Markup()
                .AddLine("[bold yellow]Right Column[/]")
                .AddLine("Content on")
                .AddLine("the right side")
                .Build()
        ))
        .Build()
);
```

### Links/References

See [Links](#links) below for clickable, keyboard-navigable links (both hand-written markup
and links inside `[markdown]` content). A quick example:

```csharp
var docs = Controls.Markup()
    .AddLine("[bold]Documentation:[/]")
    .AddLine("")
    .AddLine("[link=https://github.com/example/docs]Repository[/]")
    .AddLine("[link=https://example.com/guide]User Guide[/]")
    .OnLinkClicked((sender, e) => OpenInBrowser(e.Url))
    .Build();

window.AddControl(docs);
```

## Helper Methods

### Set Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    markup.SetContent(new List<string>
    {
        "[green]New content[/]",
        "Line 2"
    });
}
```

### Set Single Line Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    markup.SetContent("[yellow]Single line update[/]");
}
```

### Append Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    var lines = markup.Text.Split('\n').ToList();
    lines.Add("[green]New line appended[/]");
    markup.SetContent(lines);
}
```

### Clear Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    markup.SetContent(new List<string>());
}
```

## Best Practices

1. **Use semantic helpers**: Prefer `Controls.Header()`, `Controls.Info()`, etc. for common patterns
2. **Don't over-format**: Too many colors/styles can be distracting
3. **Consistent colors**: Use same colors for same meaning (red = error, green = success)
4. **Test readability**: Ensure text is readable with different themes
5. **Escape brackets**: Use `[[` and `]]` to display literal brackets
6. **Keep lines reasonable**: Very long lines may cause wrapping issues
7. **Update efficiently**: Use `SetContent()` instead of recreating controls

## Common Patterns

### Title and Description

```csharp
window.AddControl(
    Controls.Header("Application Settings")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);

window.AddControl(
    Controls.Label("Configure your preferences below")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Status Bar

```csharp
window.AddControl(
    Controls.Label("[dim]Ready | ESC: Exit | F1: Help[/]")
        .StickyBottom()
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Error Display

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold red]Error:[/]")
        .AddLine("")
        .AddLine("Failed to connect to server.")
        .AddLine("[dim]Please check your network connection.[/]")
        .Build()
);
```

## Color Reference

Common colors available in SharpConsoleUI:

- **Basic**: red, green, blue, yellow, cyan, magenta, white, black
- **Grays**: grey, grey0-grey100 (0=black, 100=white)
- **Extended**: orange, orange3, purple, lime, aqua, fuchsia, maroon, navy, olive, teal, silver
- **Custom**: rgb(r,g,b) or #RRGGBB

## Inline Spinners

MarkupControl content may contain the `[spinner]` tag, which renders an animated spinner glyph inline:

```csharp
var status = Controls.Markup("Loading [yellow][spinner][/] please wait").Build();
```

The spinner animates automatically while the window system is running. See [Markup Syntax → Spinner](../MARKUP_SYNTAX.md#spinner-animated) for all styles.

## Links

MarkupControl supports **clickable, keyboard-navigable links**. A link is written with the
`[link=<url>]…[/]` tag and renders as styled text (by default the Markdown link color, underlined),
exactly as it looks today — but the URL is now preserved and the link is interactive.

```csharp
var c = Controls.Markup()
    .AddLine("See the [link=https://example.com/docs]documentation[/] for details.")
    .OnLinkClicked((sender, e) => OpenInBrowser(e.Url))   // e.Url, e.Text
    .Build();
```

Links also come for free from Markdown — a Markdown `[text](url)` inside a `[markdown]` region (or
via `SetMarkdown` / `Controls.Markdown(...)`) is translated into a `[link=…]` span by the Markdown
parser, so rendered Markdown links are clickable too. Bare autolinks (`<https://example.com>`) work
as well.

### Activating links

- **Mouse:** clicking a link raises `LinkClicked`. (Clicking does **not** move keyboard focus — mouse
  behavior is unchanged from a display-only control.)
- **Keyboard:** when a control contains at least one link it becomes a single **Tab stop**. Once
  focused, **Left/Right arrows** move the highlight between links (in document order), and **Enter**
  activates the focused link (raising `LinkClicked`). **Tab/Shift+Tab** move focus to the next/previous
  control; **Up/Down/PageUp/PageDown** are passed through to an enclosing scroll container.

```csharp
markup.LinkClicked += (sender, e) =>
{
    // e.Url  — the link target (already unescaped)
    // e.Text — the visible link text
    // e.Mouse — the originating MouseEventArgs, or null for keyboard activation
    OpenInBrowser(e.Url);
};
```

`LinkClickedEventArgs` is the same type used by [HtmlControl](HtmlControl.md), so link handling is
consistent across both controls.

### Focusability is automatic and content-gated

- A MarkupControl with **no links** is **not focusable** and is skipped by Tab traversal — its
  behavior is identical to a plain display label. This preserves backward compatibility for existing
  display-only markup.
- A MarkupControl that **has links** automatically becomes keyboard-reachable (one Tab stop). No flag
  to set. If you need to suppress it for a specific control, set `IsEnabled = false` (it then accepts
  no keyboard input and is not a focus stop) — or hide it (`Visible = false`).
- When the focused link is scrolled out of view inside a `ScrollablePanelControl` (even several
  container levels up), arrow-navigating to it scrolls it into view automatically.

### Focus highlight colors

The keyboard-focused link is drawn with a definite high-contrast highlight (a swap of the link's own
colors would be unreadable over a transparent background). Override per control or via the builder:

```csharp
var markup = new MarkupControl(new List<string> { "[link=u]link[/]" })
{
    FocusedLinkForegroundColor = Color.Black,
    FocusedLinkBackgroundColor = new Color(235, 175, 60)   // amber (the default)
};

// Or via the builder:
Controls.Markup("[link=https://x]click[/]")
    .WithFocusedLinkColors(Color.Black, new Color(235, 175, 60))
    .OnLinkClicked((_, e) => OpenInBrowser(e.Url))
    .Build();
```

### URL escaping

The URL inside `[link=…]` is percent-escaped so characters like `]`, `[`, space, and `%` cannot break
the markup parser; this is handled automatically by the Markdown translator and by `LinkUrl.Escape`.
Hand-written `[link=…]` tags should likewise percent-escape a `]` in the URL as `%5D` (rare in
practice).

## Markdown

MarkupControl content may contain the `[markdown]…[/]` tag, which parses its inner text as Markdown (headings, lists, emphasis, code blocks, blockquotes, links, tables) and renders it as native markup. Markdown links (`[text](url)`) and autolinks become clickable `[link=…]` spans — see [Links](#links). Copied text stays plain.

```csharp
var c = Controls.Markdown("# Report\n\n**Status:** OK\n\n- one\n- two").Build();
c.SetMarkdown("# Updated\n\nNew content");   // live update
```

Styling is controlled by `MarkdownStyle` (per-control via `MarkupControl.MarkdownStyle` or builder `.WithMarkdownStyle(...)`, globally via `MarkdownStyle.Default`). See [Markup Syntax → Markdown](../MARKUP_SYNTAX.md#markdown) for the full reference.

## See Also

- [Markup Syntax Reference](../MARKUP_SYNTAX.md) - Complete markup syntax guide with all colors and decorations
- [FigleControl](FigleControl.md) - For large ASCII art text
- [SpectreRenderableControl](SpectreRenderableControl.md) - For Spectre.Console widgets
- [LogViewerControl](LogViewerControl.md) - For log display with filtering

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
