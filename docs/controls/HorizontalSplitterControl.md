# HorizontalSplitterControl

A draggable horizontal bar that resizes the control above it against the control below.

## Overview

`HorizontalSplitterControl` sits between two controls in a vertical stack and lets the user
redistribute height between them — by dragging with the mouse, or by focusing the bar and pressing
Up/Down. It is the vertical-axis counterpart to the column splitters built into
[GridControl](GridControl.md).

You do not wire the neighbours up manually in the common case: dropped between two controls in a
window or column, the splitter resolves the control immediately above and below itself. Use
`SetControls` (or `.WithControls(...)`) only when you need to target specific controls explicitly.

The splitter is **focusable and interactive** — it participates in Tab order and renders distinct
focused and dragging colors so the user can see which state it is in.

> **Looking for column splitters inside a grid?** Those are built into `GridControl` — see
> [GridControl → Splitters](GridControl.md#splitters). `SplitterControl` (the vertical divider) is
> an implementation detail of `HorizontalGridControl` and is created for you by
> `AddSplitter` / `AddSplitterBefore`.

## Quick Start

```csharp
window.AddControl(topPanel);
window.AddControl(Controls.HorizontalSplitter().Build());
window.AddControl(bottomPanel);
```

That is the whole setup — the splitter finds its neighbours. Give the panels
`VerticalAlignment.Fill` so they have height to trade.

## Builder API

Create a builder with `Controls.HorizontalSplitter()`.

### Neighbours and constraints

```csharp
.WithControls(IWindowControl above, IWindowControl below)  // Explicit neighbours
.WithMinHeightAbove(int minHeight)                         // Floor for the control above
.WithMinHeightBelow(int minHeight)                         // Floor for the control below
.WithMinHeights(int above, int below)                      // Both at once
```

Minimum heights are floored at `3` rows (`ControlDefaults.HorizontalSplitterMinControlHeight`) — a
smaller value is silently raised to it.

### Colors

```csharp
.WithFocusedColors(Color foreground, Color background)
.WithDraggingColors(Color foreground, Color background)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
```

### Events, layout, identity

```csharp
.OnSplitterMoved(EventHandler<HorizontalSplitterMovedEventArgs> handler)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.Visible(bool visible = true)
.WithName(string name)
.WithTag(object tag)
.WithStickyPosition(StickyPosition position)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MinHeightAbove` | `int` | `3` | Minimum rows for the control above; values below `3` are raised to it |
| `MinHeightBelow` | `int` | `3` | Minimum rows for the control below; same floor |
| `AboveControl` | `IWindowControl?` | resolved | The control the splitter resizes above itself (read-only) |
| `BelowControl` | `IWindowControl?` | resolved | The control below (read-only) |
| `IsEnabled` | `bool` | `true` | Whether the splitter responds to input |
| `HasFocus` | `bool` | `false` | Whether the splitter currently has keyboard focus |
| `ForegroundColor` | `Color` | theme | Bar color when idle |
| `BackgroundColor` | `Color?` | `null` | Background when idle; inherits when null |
| `FocusedForegroundColor` | `Color` | theme | Bar color while focused |
| `FocusedBackgroundColor` | `Color` | theme | Background while focused |
| `DraggingForegroundColor` | `Color` | theme | Bar color while being dragged |
| `DraggingBackgroundColor` | `Color` | theme | Background while being dragged |
| `ColorRole` | `ColorRole` | `Default` | Semantic color role |
| `ColorRoleMode` | `ThemeMode?` | `null` | Optional theme mode override |
| `Outline` | `bool` | `false` | Outline styling |

### Methods

```csharp
void SetControls(IWindowControl aboveControl, IWindowControl belowControl);
```

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `SplitterMoved` | `HorizontalSplitterMovedEventArgs` | Fires after a resize; carries `AboveControlHeight` and `BelowControlHeight` |
| `SplitterMovedAsync` | `AsyncEventHandler<HorizontalSplitterMovedEventArgs>` | Async counterpart |

## Keyboard Support

| Key | Action |
|-----|--------|
| Tab | Move focus to or from the splitter |
| Up | Move the boundary up one row |
| Down | Move the boundary down one row |
| Shift+Up / Shift+Down | Move the boundary 5 rows (`ControlDefaults.HorizontalSplitterKeyboardJumpSize`) |

## Mouse Support

Press and drag the bar to resize. The splitter renders its dragging colors for the duration of the
drag, and the boundary stops at whichever minimum height it reaches first.

## Examples

### Two stacked panels

```csharp
var top = Controls.ScrollablePanel()
    .WithHeader("Top Panel")
    .WithBorderStyle(BorderStyle.Rounded)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .AddControl(Controls.Markup("Drag the bar below to resize.").Build())
    .Build();

var bottom = Controls.ScrollablePanel()
    .WithHeader("Bottom Panel")
    .WithBorderStyle(BorderStyle.Rounded)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .AddControl(Controls.Markup("This panel adjusts too.").Build())
    .Build();

window.AddControl(top);
window.AddControl(Controls.HorizontalSplitter().Build());
window.AddControl(bottom);
```

### Resizing a grid against a panel below it

The neighbour can be any control, including a container.

```csharp
window.AddControl(grid);                                  // a HorizontalGrid or Grid
window.AddControl(Controls.HorizontalSplitter().Build());
window.AddControl(bottomControl);
```

### Enforcing minimum heights

```csharp
var splitter = Controls.HorizontalSplitter()
    .WithMinHeights(above: 5, below: 8)   // the log pane keeps at least 8 rows
    .Build();
```

### Reacting to a resize

```csharp
var splitter = Controls.HorizontalSplitter()
    .OnSplitterMoved((sender, e) =>
    {
        windowSystem.LogService.LogInfo(
            $"Split is now {e.AboveControlHeight}/{e.BelowControlHeight}");
    })
    .Build();
```

### Explicit neighbours

When the splitter is not directly between the two controls it should resize:

```csharp
var splitter = Controls.HorizontalSplitter()
    .WithControls(editorPanel, outputPanel)
    .Build();

// or later
splitter.SetControls(editorPanel, outputPanel);
```

## Best Practices

1. **Give both neighbours `VerticalAlignment.Fill`.** A fixed-height control has no slack to trade,
   so the splitter appears to do nothing.
2. **Rely on automatic neighbour resolution.** Only call `SetControls` / `.WithControls(...)` when
   the splitter is not physically between the two controls.
3. **Set minimum heights for panes that become useless when squashed** — a log pane at two rows is
   worse than no log pane.
4. **Remember it is focusable.** It takes a Tab stop, which is what makes keyboard resizing work;
   factor that into your tab order.
5. **Use `WithColorRole` rather than hardcoded colors** so the bar follows the active theme.
6. **For column (vertical) splitting, use the grid.** `GridControl` has splitters built in — don't
   hand-place `SplitterControl`, which exists for `HorizontalGridControl`'s internal use.

## See Also

- [GridControl](GridControl.md#splitters) — built-in row and column splitters
- [HorizontalGridControl](HorizontalGridControl.md) — column layout with its own splitters
- [ScrollablePanelControl](ScrollablePanelControl.md) — the usual thing on either side of a splitter
- [RuleControl](RuleControl.md) — a non-interactive horizontal divider
- [Themes](../THEMES.md) — color roles

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
