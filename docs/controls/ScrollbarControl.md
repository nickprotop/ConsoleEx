# ScrollbarControl

A standalone scrollbar, decoupled from any single scrollable view.

## Overview

ScrollbarControl is a bar control that is not attached to a particular scrollable view — it just tracks a range (`Maximum`, `ViewportLength`, `Value`) and reports user interaction through events. Every built-in scrolling control (`ScrollablePanelControl`, `ListControl`, `TreeControl`, `TableControl`, `HtmlControl`) already draws its own embedded scrollbar using the same shared engine; `ScrollbarControl` exposes that engine as its own control so it can drive views that don't have — or shouldn't each have — their own bar.

The motivating case (issue #85) is a side-by-side diff viewer: two panes that must scroll in lockstep, with a single shared scrollbar between them rather than one per pane. `ScrollbarControl` is built for exactly that kind of composite — one bar, several driven views.

The bar supports vertical and horizontal orientation, arrow clicks, track (page) clicks, thumb dragging, and mouse wheel, and paints with the same focus-aware default colors as the embedded scrollbars, or custom colors via `ScrollbarColor`/`ScrollbarThumbColor`. It is never itself a Tab stop — it is driven by mouse/wheel and by the composite that owns it, not by keyboard focus.

See also: [ScrollablePanelControl](ScrollablePanelControl.md)

## Quick Start

```csharp
var bar = Controls.Scrollbar()
    .WithMaximum(200)
    .WithViewportLength(20)
    .OnUserValueChanged((s, value) => panel.ScrollVerticalBy(value - panel.VerticalScrollOffset))
    .Build();

window.AddControl(bar);
```

## Builder API

Create a builder with `Controls.Scrollbar()` or `new ScrollbarBuilder()`. A builder implicitly converts to a `ScrollbarControl`, so it can be passed directly where a control is expected.

### Range

```csharp
.WithOrientation(ScrollbarOrientation orientation)   // Vertical or Horizontal
.Vertical()                                           // Runs top to bottom (default)
.Horizontal()                                         // Runs left to right
.WithMaximum(int maximum)                             // Total length of the content being scrolled
.WithViewportLength(int viewportLength)               // Visible length of the view the bar represents
.WithValue(int value)                                 // Initial scroll offset
.WithSmallChange(int smallChange)                     // Amount per arrow click / wheel notch
.WithLargeChange(int largeChange)                     // Amount per track (page) click
```

### Appearance & State

```csharp
.WithIsActive(bool isActive = true)                   // Paint as focused while a sibling view holds real focus
.WithEnabled(bool enabled = true)                     // Enable/disable the bar
.WithScrollbarColors(Color trackColor, Color thumbColor)
```

### Events

```csharp
.OnValueChanged(EventHandler<int> handler)            // Every change, including a code-set Value
.OnUserValueChanged(EventHandler<int> handler)        // Only a change made through the bar itself
```

### Layout & Identity

```csharp
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.WithMargin(Margin margin)
.Visible(bool visible = true)
.WithWidth(int width)
.WithHeight(int height)
.WithName(string name)
.WithTag(object tag)
.WithStickyPosition(StickyPosition position)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Orientation` | `ScrollbarOrientation` | `Vertical` | Runs top-to-bottom (`Vertical`) or left-to-right (`Horizontal`) |
| `Maximum` | `int` | `0` | Total length of the content being scrolled. Shrinking re-clamps `Value` |
| `ViewportLength` | `int` | `1` | Visible length of the view the bar represents. Changing it re-clamps `Value` and, when `LargeChange` was never set explicitly, updates the default paging amount |
| `Value` | `int` | `0` | Current scroll offset, clamped to `[0, Maximum - ViewportLength]` |
| `SmallChange` | `int` | `ControlDefaults.DefaultScrollWheelLines` | Amount `Value` moves per arrow click or wheel notch |
| `LargeChange` | `int` | `ViewportLength` | Amount `Value` moves per track (page) click; defaults to `ViewportLength` until set explicitly |
| `IsActive` | `bool` | `false` | Whether the bar paints itself as focused, even though it never holds keyboard focus |
| `IsEnabled` | `bool` | `true` | Whether the bar responds to mouse input; a disabled bar dims to its unfocused colors |
| `ScrollbarColor` | `Color?` | `null` | Track color; `null` uses the shared engine's focus-aware theme color |
| `ScrollbarThumbColor` | `Color?` | `null` | Thumb color; `null` uses the shared engine's focus-aware theme color |
| `CanReceiveFocus` | `bool` | `false` | Always `false` — the bar is not a Tab stop |

## Events

The two events are the reason the control exists: they let a composite tell the difference between "the user moved this bar" and "code set this bar's value," so it can wire the bar to several views without an echo loop or a guard flag.

| Event | Arguments | Fires when |
|-------|-----------|------------|
| `ValueChanged` | `int` (new value) | Every time `Value` changes, for any reason — including a value set by code. Conventional, INPC-consistent behavior, matching `SliderControl.ValueChanged`. |
| `UserValueChanged` | `int` (new value) | Only when the user changes the value through the bar itself: an arrow click, a track (page) click, a thumb drag, or a mouse wheel notch. Never raised by a code-set `Value`. |

A composite subscribes to `UserValueChanged` — not `ValueChanged` — to push the bar's new offset out to the views it drives, and writes each view's own scroll position back into `Value` from the views' scroll events. Because that write-back is a code-set `Value`, it raises only `ValueChanged`, never `UserValueChanged` — so the loop terminates on its own with no guard flag needed.

`MouseClick`, `MouseDoubleClick`, `MouseRightClick`, `MouseEnter`, `MouseLeave`, and `MouseMove` are also present to satisfy `IMouseAwareControl`.

## Mouse Support

ScrollbarControl implements `IMouseAwareControl` and wants mouse events while `IsEnabled` is `true`.

- **Arrow click**: Moves `Value` by `SmallChange`.
- **Track (page) click**: Moves `Value` by `LargeChange` toward the click.
- **Thumb drag**: Drags the thumb to scrub `Value` continuously.
- **Mouse wheel**: Moves `Value` by `SmallChange` per notch.

All of the above call the internal user-input path, so each one raises both `ValueChanged` and `UserValueChanged`.

## Examples

### One Bar Driving Two Panes (the case the control exists for)

Two `ScrollablePanelControl`s with their own scrollbars hidden (`.WithScrollbar(false)`), sharing a single `ScrollbarControl` between them:

```csharp
var paneA = Controls.ScrollablePanel().WithScrollbar(false).WithHeight(20).Build();
var paneB = Controls.ScrollablePanel().WithScrollbar(false).WithHeight(20).Build();
// ...populate paneA and paneB...

var bar = Controls.Scrollbar()
    .Vertical()
    .WithMaximum(lineCount)
    .WithViewportLength(20)
    .Build();

// The bar drives the panes: only a user-initiated change (drag/click/wheel) pushes.
bar.UserValueChanged += (_, value) =>
{
    paneA.ScrollVerticalBy(value - paneA.VerticalScrollOffset);
    paneB.ScrollVerticalBy(value - paneB.VerticalScrollOffset);
};

// The panes drive the bar back: a code-set Value only raises ValueChanged, so this
// never re-enters UserValueChanged and never loops.
paneA.Scrolled += (_, e) =>
{
    if (bar.Value != e.VerticalOffset)
        bar.Value = e.VerticalOffset;
};

var grid = new GridControl { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Fill };
grid.ColumnDefinitions.Add(GridLength.Star());
grid.ColumnDefinitions.Add(GridLength.Star());
grid.ColumnDefinitions.Add(GridLength.Cells(1));
grid.RowDefinitions.Add(GridLength.Star());
grid.Place(paneA, 0, 0);
grid.Place(paneB, 0, 1);
grid.Place(bar, 0, 2);

window.AddControl(grid);
```

Only one pane's `Scrolled` handler needs to write back to `bar.Value` — once the bar's value changes, its own `UserValueChanged` does *not* re-fire (that write was code, not user input), so the second pane is safe to drive purely from `bar.UserValueChanged` without also wiring its own `Scrolled` event. Wiring both panes' `Scrolled` back to the bar is also safe: the `if (bar.Value != e.VerticalOffset)` guard means the second write is a no-op once the bar already holds that value.

### Painting as Focused While a Sibling Holds Focus

Since the bar itself can never take keyboard focus (`CanReceiveFocus` is always `false`), a composite that wants the bar to look "active" while one of the views it drives has real focus sets `IsActive`:

```csharp
paneA.GotFocus += (_, _) => bar.IsActive = true;
paneA.LostFocus += (_, _) => bar.IsActive = paneB.HasFocus;
paneB.GotFocus += (_, _) => bar.IsActive = true;
paneB.LostFocus += (_, _) => bar.IsActive = paneA.HasFocus;
```

### Custom Colors

```csharp
var bar = Controls.Scrollbar()
    .WithMaximum(500)
    .WithViewportLength(25)
    .WithScrollbarColors(Color.Grey35, Color.Cyan1)
    .Build();
```

## Best Practices

1. **Hide the driven views' own scrollbars** with `.WithScrollbar(false)` — a standalone bar next to a view that also draws its own scrollbar shows two bars for one range.
2. **Subscribe to `UserValueChanged`, not `ValueChanged`**, when pushing the bar's value out to the views it drives — that is what keeps the composite from re-entering itself.
3. **Write scroll position back with a plain `Value =` assignment** from the driven views' own scroll events; a code-set `Value` only raises `ValueChanged`, so no guard flag is needed to avoid an echo.
4. **Keep `Maximum` and `ViewportLength` in the same units as the views' own scroll offsets** (typically lines) so the values line up directly with no conversion.
5. **Set `IsActive`, not focus**, to show the bar as "part of the active group" — it is never itself a Tab stop.

## See Also

- [ScrollablePanelControl](ScrollablePanelControl.md) - The most common driven view; hide its own scrollbar with `.WithScrollbar(false)` when pairing it with a standalone bar

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
