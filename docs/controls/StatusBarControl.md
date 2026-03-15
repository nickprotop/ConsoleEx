# StatusBarControl

Single-row status bar with three alignment zones, clickable items, and shortcut key hints.

## Overview

`StatusBarControl` renders a horizontal bar with left, center, and right zones for displaying status information, keyboard shortcuts, and clickable items. Items consist of an optional shortcut hint (rendered with an accent color) and a label. The control does not receive keyboard focus — it is display and click only.

By default the status bar sticks to the bottom of the window (`StickyPosition.Bottom`). An optional horizontal separator line can be rendered above the content.

See also: [ToolbarControl](ToolbarControl.md) — for interactive button toolbars

## Quick Start

```csharp
var statusBar = Controls.StatusBar()
    .AddLeft("↑↓", "Navigate")
    .AddLeft("Enter", "Select")
    .AddLeftSeparator()
    .AddLeft("Esc", "Exit")
    .AddCenterText("[dim]My Application[/]")
    .AddRight("Ctrl+S", "Save")
    .WithAboveLine()
    .WithBackgroundColor(Color.Grey15)
    .WithShortcutForegroundColor(Color.Cyan1)
    .StickyBottom()
    .Build();

window.AddControl(statusBar);
```

## Builder API

Create a `StatusBarBuilder` through the `Controls` factory:

```csharp
var builder = Controls.StatusBar();
```

### Adding Items

Items can be added to three zones. Each method returns the builder for chaining.

```csharp
// Left zone
.AddLeft(string shortcut, string label, Action? onClick = null)
.AddLeftText(string text, Action? onClick = null)
.AddLeft(StatusBarItem item)
.AddLeftSeparator()

// Center zone
.AddCenter(string shortcut, string label, Action? onClick = null)
.AddCenterText(string text, Action? onClick = null)
.AddCenter(StatusBarItem item)
.AddCenterSeparator()

// Right zone
.AddRight(string shortcut, string label, Action? onClick = null)
.AddRightText(string text, Action? onClick = null)
.AddRight(StatusBarItem item)
.AddRightSeparator()
```

### Style Methods

```csharp
.WithBackgroundColor(Color color)           // Status bar background
.WithForegroundColor(Color color)           // Label text color
.WithShortcutForegroundColor(Color color)   // Shortcut hint accent color
.WithAboveLine(bool show = true)            // Horizontal separator line above content
.WithAboveLineColor(Color color)            // Separator line color (also enables line)
.WithItemSpacing(int spacing)               // Space between items (default: 2)
.WithSeparatorChar(string separator)        // Section separator character (default: "|")
.WithShortcutLabelSeparator(string sep)     // Shortcut-label separator (default: ":")
```

### Layout Methods

```csharp
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.WithMargin(Margin margin)
.WithAlignment(HorizontalAlignment align)
.WithStickyPosition(StickyPosition pos)
.StickyTop()
.StickyBottom()
```

### Identity Methods

```csharp
.WithName(string name)
.WithTag(object tag)
```

### Event Methods

```csharp
.OnItemClicked(EventHandler<StatusBarItemClickedEventArgs> handler)
```

### Building

```csharp
StatusBarControl control = builder.Build();

// Implicit conversion is also supported:
StatusBarControl control = Controls.StatusBar()
    .AddLeft("Esc", "Close");
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BackgroundColor` | `Color` | theme/container | Status bar background color |
| `ForegroundColor` | `Color` | theme/container | Label text color |
| `ShortcutForegroundColor` | `Color` | theme/Cyan1 | Accent color for shortcut hints |
| `ItemSpacing` | `int` | `2` | Space between adjacent items |
| `SeparatorChar` | `string` | `"\|"` | Character rendered for separator items |
| `ShortcutLabelSeparator` | `string` | `":"` | Text between shortcut and label (e.g. "Ctrl+S:Save") |
| `ShowAboveLine` | `bool` | `false` | Render horizontal line above content |
| `AboveLineColor` | `Color?` | `null` | Line color (null uses foreground) |
| `LeftItems` | `IReadOnlyList<StatusBarItem>` | — | Items in the left zone |
| `CenterItems` | `IReadOnlyList<StatusBarItem>` | — | Items in the center zone |
| `RightItems` | `IReadOnlyList<StatusBarItem>` | — | Items in the right zone |

## StatusBarItem Properties

Each item in the status bar is a `StatusBarItem` with these properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Shortcut` | `string?` | `null` | Key hint text rendered with accent color |
| `Label` | `string` | `""` | Label text (supports markup) |
| `IsVisible` | `bool` | `true` | Whether the item is rendered |
| `IsSeparator` | `bool` | `false` | Render as separator character |
| `OnClick` | `Action?` | `null` | Click handler for this item |
| `ShortcutForeground` | `Color?` | `null` | Override shortcut color for this item |
| `ShortcutBackground` | `Color?` | `null` | Override shortcut background for this item |
| `LabelForeground` | `Color?` | `null` | Override label color for this item |
| `LabelBackground` | `Color?` | `null` | Override label background for this item |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `ItemClicked` | `StatusBarItemClickedEventArgs` | Fired when any non-separator item is clicked |
| `MouseClick` | `MouseEventArgs` | Fired on any mouse click on the status bar |

## Mouse Interaction

- **Click on an item** fires the item's `OnClick` handler and the `ItemClicked` event
- **Click on a separator** has no effect
- The status bar does not receive keyboard focus

## Dynamic Updates

Items can be modified at runtime. Changes trigger automatic re-rendering.

```csharp
// Update an item's text
var counterItem = statusBar.RightItems[0];
counterItem.Label = "[yellow]5 outdated[/]";

// Add items at runtime
statusBar.AddLeft("F5", "Refresh");

// Remove items
var items = statusBar.LeftItems;
if (items.Count > 0)
    statusBar.RemoveLeft(items[items.Count - 1]);

// Batch updates for performance
statusBar.BatchUpdate(() =>
{
    statusBar.AddLeft("A", "Action1");
    statusBar.AddLeft("B", "Action2");
    statusBar.AddLeft("C", "Action3");
});
```

## Examples

### Application Status Bar

```csharp
var statusBar = Controls.StatusBar()
    .AddLeft("↑↓", "Navigate")
    .AddLeft("Enter", "View")
    .AddLeftSeparator()
    .AddLeft("Esc", "Exit")
    .AddCenterText("[dim]StatusBar Demo[/]")
    .AddRightText("[yellow]3 outdated[/]")
    .AddRight("Ctrl+S", "Search")
    .WithAboveLine()
    .WithBackgroundColor(Color.Grey15)
    .WithShortcutForegroundColor(Color.Cyan1)
    .StickyBottom()
    .Build();
```

### Status Bar with Click Handlers

```csharp
var statusBar = Controls.StatusBar()
    .AddLeft("F5", "Refresh", () => RefreshData())
    .AddLeft("F2", "Edit", () => StartEditing())
    .AddRight("Ctrl+Q", "Quit", () => windowSystem.Stop())
    .OnItemClicked((sender, args) =>
    {
        var display = args.Item.Shortcut != null
            ? $"{args.Item.Shortcut}:{args.Item.Label}"
            : args.Item.Label;
        // Log which item was clicked
    })
    .StickyBottom()
    .Build();
```

### Status Bar at Top

```csharp
var topBar = Controls.StatusBar()
    .AddLeftText("[bold cyan]My App[/]")
    .AddRightText($"[dim]v{version}[/]")
    .StickyTop()
    .WithBackgroundColor(Color.Grey11)
    .Build();
```

### Per-Item Color Overrides

```csharp
var errorItem = new StatusBarItem
{
    Label = "3 errors",
    LabelForeground = Color.Red,
    OnClick = () => ShowErrors()
};

var statusBar = Controls.StatusBar()
    .AddRight(errorItem)
    .StickyBottom()
    .Build();
```

### Toggling Item Visibility

```csharp
bool centerVisible = true;
toggleBtn.Click += (_, _) =>
{
    centerVisible = !centerVisible;
    var centerItems = statusBar.CenterItems;
    if (centerItems.Count > 0)
        centerItems[0].IsVisible = centerVisible;
};
```

## Best Practices

1. **Use shortcut+label pairs** for keyboard-driven items — the accent color makes shortcuts discoverable
2. **Use `AddLeftText`/`AddCenterText`/`AddRightText`** for display-only items without keyboard hints
3. **Batch updates** when adding multiple items at once to avoid redundant re-renders
4. **Use markup in labels** for dynamic coloring (e.g. `[yellow]3 warnings[/]`)
5. **Stick to bottom** (default) for traditional status bars; use `StickyTop()` for header bars
6. **Enable `WithAboveLine()`** to visually separate the status bar from content above

## See Also

- [ToolbarControl](ToolbarControl.md) - Interactive button toolbar with wrapping
- [Status System](../STATUS_SYSTEM.md) - System-level status bars and taskbar

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
