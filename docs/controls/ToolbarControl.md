# ToolbarControl

Horizontal toolbar with buttons, separators, and other controls. Supports wrapping, auto-height, separator lines, and content padding.

## Overview

`ToolbarControl` arranges child controls (typically buttons) in a horizontal strip. When wrapping is enabled, items that exceed the available width flow to the next row. Each row auto-sizes to the height of its tallest item, allowing bordered buttons and plain controls to coexist naturally.

Separator lines can be rendered above and/or below the toolbar content, and inner content padding offsets items from the toolbar edges. Tab and arrow key navigation moves focus between items.

See also: [StatusBarControl](StatusBarControl.md) — for non-focusable status display

## Quick Start

```csharp
var toolbar = Controls.Toolbar()
    .AddButton("New", (s, btn) => CreateNew())
    .AddButton("Open", (s, btn) => OpenFile())
    .AddButton("Save", (s, btn) => SaveFile())
    .AddSeparator()
    .AddButton("Settings", (s, btn) => ShowSettings())
    .WithSpacing(1)
    .WithBackgroundColor(Color.Grey11)
    .WithBelowLine()
    .StickyTop()
    .Build();

window.AddControl(toolbar);
```

## Builder API

Create a `ToolbarBuilder` through the `Controls` factory:

```csharp
var builder = Controls.Toolbar();
```

### Adding Items

```csharp
.AddButton(string text, EventHandler<ButtonControl> onClick)
.AddButton(string text, WindowEventHandler<ButtonControl> onClick)
.AddButton(ButtonBuilder builder)
.AddButton(ButtonControl button)
.AddSeparator()
.AddSeparator(int horizontalMargin)
.Add(IWindowControl control)                // Add any control
```

### Spacing and Layout

```csharp
.WithSpacing(int spacing)                   // Space between items (default: 0)
.WithWrap(bool wrap = true)                 // Enable item wrapping to next row
.WithHeight(int height)                     // Fixed row height (default: auto)
.WithWidth(int width)                       // Fixed width
```

### Separator Lines

```csharp
.WithAboveLine(bool show = true)            // Horizontal line above content
.WithBelowLine(bool show = true)            // Horizontal line below content
.WithAboveLineColor(Color color)            // Line color (also enables line)
.WithBelowLineColor(Color color)            // Line color (also enables line)
```

### Content Padding

```csharp
.WithContentPadding(int left, int top, int right, int bottom)
.WithContentPadding(int horizontal, int vertical)
.WithContentPadding(int all)
```

### Style

```csharp
.WithBackgroundColor(Color? color)          // Toolbar background (null = inherit)
.WithForegroundColor(Color? color)          // Toolbar foreground (null = inherit)
```

### Layout and Position

```csharp
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.WithMargin(Margin margin)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithStickyPosition(StickyPosition position)
.StickyTop()
.StickyBottom()
```

### Identity

```csharp
.WithName(string name)
.WithTag(object tag)
.Visible(bool visible)
```

### Events

```csharp
.OnGotFocus(EventHandler handler)
.OnGotFocus(WindowEventHandler<EventArgs> handler)
.OnLostFocus(EventHandler handler)
.OnLostFocus(WindowEventHandler<EventArgs> handler)
```

### Building

```csharp
ToolbarControl control = builder.Build();

// Implicit conversion is also supported:
ToolbarControl control = Controls.Toolbar()
    .AddButton("OK", (s, btn) => { });
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BackgroundColor` | `Color` | theme/container | Toolbar background color |
| `ForegroundColor` | `Color` | theme/container | Toolbar foreground color |
| `ItemSpacing` | `int` | `0` | Space between items in characters |
| `Wrap` | `bool` | `false` | Wrap items to next row when they exceed available width |
| `Height` | `int?` | `null` | Fixed row height (null = auto-size from tallest item) |
| `ShowAboveLine` | `bool` | `false` | Render horizontal separator line above content |
| `ShowBelowLine` | `bool` | `false` | Render horizontal separator line below content |
| `AboveLineColor` | `Color?` | `null` | Above line color (null uses foreground) |
| `BelowLineColor` | `Color?` | `null` | Below line color (null uses foreground) |
| `ContentPadding` | `Padding` | `(0,0,0,0)` | Inner padding between toolbar edge and items |
| `IsEnabled` | `bool` | `true` | Enable/disable all toolbar interaction |
| `Items` | `IReadOnlyList<IWindowControl>` | — | The toolbar's child controls |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `GotFocus` | `EventArgs` | Fired when the toolbar receives focus |
| `LostFocus` | `EventArgs` | Fired when the toolbar loses focus |
| `MouseClick` | `MouseEventArgs` | Fired when the toolbar area is clicked |

## Keyboard Support

| Key | Action |
|-----|--------|
| **Tab** | Move focus to next item |
| **Shift+Tab** | Move focus to previous item |
| **Left Arrow** | Move focus to previous item |
| **Right Arrow** | Move focus to next item |
| **Up Arrow** | Move focus to item above (wrapping mode) |
| **Down Arrow** | Move focus to item below (wrapping mode) |
| **Enter/Space** | Activate focused item (e.g. click button) |

## Mouse Support

| Action | Result |
|--------|--------|
| **Click on item** | Focus and activate the item |
| **Right-click** | Fires `MouseRightClick` event |

## Auto-Height

When `Height` is null (default), each row sizes to its tallest item. This means bordered buttons (height 3) and plain buttons (height 1) coexist — plain items are vertically aligned within the taller row.

```csharp
var toolbar = Controls.Toolbar()
    .AddButton(Controls.Button()
        .WithText("  Compile  ")
        .WithBorder(ButtonBorderStyle.Rounded)
        .OnClick((s, btn) => Compile()))
    .AddButton("Cancel", (s, btn) => Cancel())  // plain, height 1
    .WithSpacing(1)
    .Build();
// Row auto-sizes to height 3 from the bordered button
// "Cancel" is vertically aligned within the row
```

Control vertical alignment within a row:

```csharp
var label = new MarkupControl(new List<string> { "[dim]Ready[/]" })
{
    VerticalAlignment = VerticalAlignment.Center  // Center in row
};
toolbar.AddItem(label);
```

## Examples

### Sticky Top Toolbar with Below Line

```csharp
var toolbar = Controls.Toolbar()
    .AddButton("New", (s, btn) => NewFile())
    .AddButton("Open", (s, btn) => OpenFile())
    .AddButton("Save", (s, btn) => SaveFile())
    .WithSpacing(1)
    .WithBackgroundColor(Color.Grey11)
    .WithBelowLine()
    .StickyTop()
    .Build();
```

### Bordered Buttons with Full Polish

```csharp
var toolbar = Controls.Toolbar()
    .AddButton(Controls.Button()
        .WithText("  Compile  ")
        .WithBorder(ButtonBorderStyle.Rounded)
        .OnClick((s, btn) => Compile()))
    .AddButton(Controls.Button()
        .WithText("  Run  ")
        .WithBorder(ButtonBorderStyle.Rounded)
        .OnClick((s, btn) => Run()))
    .AddButton(Controls.Button()
        .WithText("  Debug  ")
        .WithBorder(ButtonBorderStyle.Rounded)
        .OnClick((s, btn) => Debug()))
    .WithSpacing(1)
    .WithBackgroundColor(Color.Grey15)
    .WithAboveLine()
    .WithBelowLine()
    .WithContentPadding(1, 0, 1, 0)
    .Build();
```

### Wrapping Toolbar

```csharp
var toolbar = Controls.Toolbar()
    .WithSpacing(1)
    .WithWrap()
    .WithBackgroundColor(Color.Grey11)
    .WithAboveLine()
    .WithBelowLine()
    .WithContentPadding(1, 0, 1, 0)
    .WithMargin(0, 1, 0, 0)
    .Build();

toolbar.AddItem(addBtn);
toolbar.AddItem(removeBtn);
toolbar.AddItem(updateBtn);
toolbar.AddItem(toggleBtn);
// Items wrap to next row if they don't fit
```

### Adding Items Dynamically

```csharp
var toolbar = Controls.Toolbar()
    .WithSpacing(1)
    .Build();

// Add items after creation
toolbar.AddItem(Controls.Button("Action 1")
    .OnClick((s, btn) => DoAction1())
    .Build());

toolbar.AddItem(new SeparatorControl());

toolbar.AddItem(Controls.Button("Action 2")
    .OnClick((s, btn) => DoAction2())
    .Build());

// Remove and clear
toolbar.RemoveItem(someItem);
toolbar.Clear();
```

### Mixed Controls

```csharp
var toolbar = Controls.Toolbar()
    .WithSpacing(1)
    .WithBackgroundColor(Color.Grey19)
    .WithAboveLine()
    .WithBelowLine()
    .Build();

var borderedBtn = Controls.Button()
    .WithText("  Apply  ")
    .WithBorder(ButtonBorderStyle.Rounded)
    .OnClick((s, btn) => Apply())
    .Build();

var label = new MarkupControl(new List<string> { "[dim]Ready[/]" })
{
    VerticalAlignment = VerticalAlignment.Center
};

var plainBtn = Controls.Button()
    .WithText("Cancel")
    .OnClick((s, btn) => Cancel())
    .Build();
plainBtn.VerticalAlignment = VerticalAlignment.Center;

toolbar.AddItem(borderedBtn);
toolbar.AddItem(label);
toolbar.AddItem(plainBtn);
```

## Best Practices

1. **Use `WithSpacing(1)`** to add visual breathing room between items
2. **Enable `WithWrap()`** when the toolbar may contain many items or the window may be narrow
3. **Use separator lines** (`WithAboveLine()`, `WithBelowLine()`) to visually separate the toolbar from surrounding content
4. **Use `WithContentPadding(1, 0, 1, 0)`** to offset items from the toolbar edges
5. **Stick to top** (`StickyTop()`) for primary action toolbars
6. **Set `VerticalAlignment.Center`** on plain controls when mixing with bordered buttons

## See Also

- [ButtonControl](ButtonControl.md) - Buttons used in toolbars
- [StatusBarControl](StatusBarControl.md) - Non-focusable status display bar
- [HorizontalGridControl](../CONTROLS.md) - Alternative multi-column layout

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
