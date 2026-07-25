# PanelControl

A bordered box with an optional header that holds either text content or child controls.

## Overview

`PanelControl` frames content in a border with an optional header — the terminal equivalent of a
card or group box. It handles two jobs:

- **Text panel** — set `Content` (or use `SetContent`) and the panel renders the text, optionally
  word-wrapped, inside the border.
- **Container** — add child controls with `AddControl` and the panel lays them out vertically inside
  the frame, honouring its padding.

`PanelControl` derives from [CollapsiblePanel](CollapsiblePanel.md) with collapsing permanently
disabled: `Collapsible` is sealed to `false`, `IsExpanded` is sealed to `true`, and `Toggle()` /
`Expand()` / `Collapse()` are sealed no-ops. Everything else — child hosting, borders, padding,
mouse events, color roles — is inherited behaviour, not a reimplementation. If you want a panel the
user can fold away, use `CollapsiblePanel` directly.

## Quick Start

```csharp
// A text panel
var panel = Controls.Panel()
    .WithHeader("Status")
    .WithContent("All systems nominal.")
    .Rounded()
    .Build();

window.AddControl(panel);

// Update the text later
panel.SetContent("[yellow]Degraded — 1 node offline.[/]");
```

Or the one-liner shorthand, which returns a `PanelControl` directly rather than a builder:

```csharp
var panel = Controls.Panel("Quick text panel");
```

## Builder API

Create a builder with `Controls.Panel()` (or `PanelControl.Create()`).

### Content

```csharp
.WithContent(string text)                  // Text content
.AddControl(IWindowControl control)        // Add a child control (repeatable)
.WordWrap(bool wrap = true)                // Wrap text content to the panel width
```

### Border

```csharp
.WithBorderStyle(BorderStyle style)
.SingleLine()                              // ┌─┐
.DoubleLine()                              // ╔═╗
.Rounded()                                 // ╭─╮
.NoBorder()
.WithBorderColor(Color color)
.UseSafeBorder(bool useSafe = true)        // ASCII-safe box characters
```

### Header

```csharp
.WithHeader(string header)
.WithHeaderAlignment(TextJustification alignment)
.HeaderLeft()
.HeaderCenter()
.HeaderRight()
```

### Padding and layout

```csharp
.WithPadding(int padding)                          // Uniform
.WithPadding(int horizontal, int vertical)
.WithPadding(int left, int top, int right, int bottom)
.WithWidth(int width)
.WithHeight(int height)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.FillVertical()
.StretchHorizontal()
.WithMargin(int margin)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(Margin margin)
.WithStickyPosition(StickyPosition position)
.StickyTop()
.StickyBottom()
.Visible(bool visible = true)
```

### Colors and identity

```csharp
.WithBackgroundColor(Color color)
.WithForegroundColor(Color color)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
.WithName(string name)
.WithTag(object tag)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Content` | `string?` | `null` | Text content of the panel |
| `WordWrap` | `bool` | — | Whether text content wraps to the panel width |
| `Header` | `string?` | `null` | Optional header text drawn in the border |
| `HeaderAlignment` | `TextJustification` | — | Header justification |
| `BorderStyle` | `BorderStyle` | — | Border line style |
| `BackgroundColor` | `Color?` | `null` | Background; inherits from the container when null |
| `ForegroundColor` | `Color?` | `null` | Text color; inherits from the container when null |
| `Padding` | `Padding` | — | Inner padding between the border and the content |
| `UseSafeBorder` | `bool` | — | Use ASCII-safe box-drawing characters |
| `Children` | `IReadOnlyList<IWindowControl>` | empty | Child controls hosted by the panel |

`BackgroundColor` and `ForegroundColor` are `Color?` shadows of the non-nullable base members, so
that "no explicit color — inherit" is expressible.

### Sealed members

These are inherited from `CollapsiblePanel` and deliberately fixed:

| Member | Value | Note |
|--------|-------|------|
| `Collapsible` | `false` | Sealed — a `PanelControl` never collapses |
| `IsExpanded` | `true` | Sealed |
| `Toggle()` / `Expand()` / `Collapse()` | no-op | Sealed |
| `ShowHeader` / `ShowHeaderSeparator` | sealed | Fixed by the panel's own header rendering |

### Methods

```csharp
void SetContent(string text);                 // Equivalent to setting Content
void AddControl(IWindowControl control);
void RemoveControl(IWindowControl control);
IReadOnlyList<IWindowControl> GetChildren();
static PanelBuilder Create();                 // Same as Controls.Panel()
```

## Events

`PanelControl` raises no panel-specific events of its own. It inherits the mouse events and
container behaviour of `CollapsiblePanel`, and implements `INotifyPropertyChanged` through
`BaseControl` — see [Data Binding](../binding.md).

## Keyboard and mouse support

The panel itself is not an interactive target: with collapsing sealed off there is nothing to
activate. It hosts children, so **focus and input flow to the controls inside it** in the usual way,
and mouse events over the body are routed to the child under the cursor.

## Examples

### Grouping related controls

```csharp
var settings = Controls.Panel()
    .WithHeader("Connection")
    .HeaderLeft()
    .SingleLine()
    .WithPadding(1)
    .AddControl(Controls.Prompt("Host:").WithName("host").Build())
    .AddControl(Controls.Prompt("Port:").WithName("port").Build())
    .AddControl(Controls.Checkbox("Use TLS", true).WithName("tls").Build())
    .Build();

window.AddControl(settings);
```

### Live status card

```csharp
var status = Controls.Panel()
    .WithHeader("Build")
    .Rounded()
    .WithColorRole(ColorRole.Info)
    .WithContent("Waiting…")
    .WithPadding(1)
    .Build();

// Markup is supported in the content
status.SetContent("[green]✓[/] Build succeeded in 4.2s");
```

### Wrapped prose

```csharp
var help = Controls.Panel()
    .WithHeader("About")
    .HeaderCenter()
    .WordWrap()
    .WithWidth(48)
    .WithContent(
        "SharpConsoleUI renders real overlapping windows in the terminal, " +
        "with a compositor, themes, and a full control library.")
    .Build();
```

### Filling available height

```csharp
var sidebar = Controls.Panel()
    .WithHeader("Files")
    .NoBorder()
    .FillVertical()
    .AddControl(fileTree)
    .Build();
```

### Semantic coloring

Roles keep the panel theme-agnostic — the same code adapts to whichever theme is active.

```csharp
var warning = Controls.Panel()
    .WithHeader("Attention")
    .WithColorRole(ColorRole.Warning)
    .Outline()
    .WithContent("Two migrations are pending.")
    .Build();
```

### ASCII-safe borders

For terminals or fonts that render box-drawing characters poorly.

```csharp
var panel = Controls.Panel()
    .WithHeader("Legacy terminal")
    .UseSafeBorder()
    .WithContent("Rendered with ASCII box characters.")
    .Build();
```

## Best Practices

1. **Use `PanelControl` for a permanent frame and `CollapsiblePanel` for a foldable one.** Don't try
   to un-seal collapsing here — reach for the base class instead.
2. **Set `WithPadding(1)` on panels holding controls.** Children rendered flush against the border
   are noticeably harder to read.
3. **Prefer `AddControl` over hand-formatted text** when the content is really several fields — you
   get focus, input, and layout for free.
4. **Use `WithColorRole` rather than hardcoded colors** so panels follow the active theme. See
   [Themes](../THEMES.md).
5. **Reach for `SetContent` for live updates.** Assigning `Content` invalidates the panel by itself;
   there is no need to call `Invalidate`.
6. **Turn on `WordWrap` for prose and leave it off for pre-formatted text**, where wrapping would
   break intentional alignment.
7. **Consider `UseSafeBorder()` if your users run older Windows consoles** or fonts with patchy
   box-drawing coverage.

## See Also

- [CollapsiblePanel](CollapsiblePanel.md) — the same panel, with user-toggleable collapsing
- [ScrollablePanelControl](ScrollablePanelControl.md) — a scrolling viewport for overflowing content
- [GridControl](GridControl.md) — 2D layout with rows and columns
- [Themes](../THEMES.md) — color roles
- [Panels](../PANELS.md) — the screen-level top/bottom bars (a different concept)

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
