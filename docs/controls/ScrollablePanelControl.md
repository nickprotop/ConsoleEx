# ScrollablePanelControl

A scrollable container that hosts child controls with automatic vertical/horizontal scrolling, mouse wheel support, and visual scrollbars.

## Overview

ScrollablePanelControl is a container control that stacks child controls vertically and provides scrolling when the combined content exceeds the visible viewport. It is the standard building block for grouping multiple controls into a single, scrollable region — for example the body of a tab, a form, a log view, or a side panel in an IDE-style layout.

The panel manages its children's focus lifecycle as an opaque focus scope: Tab and Shift+Tab move focus between focusable children, Escape drops into "scroll mode" where the arrow keys, Page Up/Down, and Home/End scroll the viewport. When a child gains focus it is automatically scrolled into view. The panel also supports mouse-wheel scrolling, draggable scrollbars, an optional border with a header, and an `AutoScroll` mode that keeps the view pinned to the bottom as new content arrives (useful for live logs and chat-style output).

The panel exposes rich state about its scroll position (`VerticalScrollOffset`, `CanScrollUp`/`CanScrollDown`, `TotalContentHeight`, `ViewportHeight`, etc.) and a `Scrolled` event so callers can react to scrolling. Children are added via `AddControl` (or the builder's `.AddControl()`), and the panel can be created with the `Controls.ScrollablePanel()` / `ScrollablePanelBuilder` fluent API.

See also: [TabControl](TabControl.md), [HorizontalGridControl](HorizontalGridControl.md)

## Quick Start

```csharp
var panel = Controls.ScrollablePanel()
    .AddControl(Controls.Markup("[bold cyan]Welcome![/]").Build())
    .AddControl(Controls.Rule("Details"))
    .AddControl(Controls.Markup("Scrollable content goes here.").Build())
    .WithScrollbar(true)
    .Build();

window.AddControl(panel);
```

## Builder API

Create a builder with `Controls.ScrollablePanel()` or `new ScrollablePanelBuilder()`. A builder implicitly converts to a `ScrollablePanelControl`, so it can be passed directly where a control is expected.

### Children

```csharp
.AddControl(IWindowControl control)   // Add a child control (call multiple times to stack)
```

### Scrolling

```csharp
.WithVerticalScroll(ScrollMode mode = ScrollMode.Scroll)     // Vertical scroll mode (default: Scroll)
.WithHorizontalScroll(ScrollMode mode = ScrollMode.Scroll)   // Horizontal scroll mode (default: None)
.WithMouseWheel(bool enable = true)                          // Enable/disable mouse-wheel scrolling
.WithAutoScroll(bool enabled = true)                         // Auto-scroll to bottom as content is added
```

### Scrollbar

```csharp
.WithScrollbar(bool show = true)                             // Show/hide the scrollbar
.WithScrollbarPosition(ScrollbarPosition position)          // Left or Right
.ScrollbarLeft()                                             // Position scrollbar on the left
.ScrollbarRight()                                            // Position scrollbar on the right (default)
```

### Border, Header & Padding

```csharp
.WithBorderStyle(BorderStyle style)
.Rounded()                                                   // Rounded border
.SingleBorder()                                              // Single-line border
.WithBorderColor(Color color)
.WithPadding(int left, int top, int right, int bottom)
.WithPadding(Padding padding)
.WithHeader(string header)                                   // Header text shown in the top border
.WithHeaderAlignment(TextJustification alignment)
```

### Layout & Appearance

```csharp
.WithWidth(int width)
.WithHeight(int height)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.WithMargin(Margin margin)
.Visible(bool visible = true)
.WithBackgroundColor(Color color)
.WithForegroundColor(Color color)
.WithColors(Color foreground, Color background)
.WithStickyPosition(StickyPosition position)
.StickyTop()
.StickyBottom()
```

### Identity & Events

```csharp
.WithName(string name)                                       // For FindControl queries
.WithTag(object tag)                                         // Custom data storage
.OnScrolled(EventHandler<ScrollEventArgs> handler)
.OnGotFocus(EventHandler handler)
.OnLostFocus(EventHandler handler)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowScrollbar` | `bool` | `true` | Whether to show the scrollbar |
| `ScrollbarPosition` | `ScrollbarPosition` | `Right` | Side the scrollbar appears on (`Left`/`Right`) |
| `VerticalScrollMode` | `ScrollMode` | `Scroll` | Vertical scroll behavior (`None`/`Scroll`/`Wrap`) |
| `HorizontalScrollMode` | `ScrollMode` | `None` | Horizontal scroll behavior (`None`/`Scroll`/`Wrap`) |
| `EnableMouseWheel` | `bool` | `true` | Whether mouse-wheel scrolling is enabled |
| `AutoScroll` | `bool` | `false` | Auto-scroll to bottom when content is added if currently at/near bottom |
| `VerticalScrollOffset` | `int` | `0` | Current vertical scroll offset in lines (read-only) |
| `HorizontalScrollOffset` | `int` | `0` | Current horizontal scroll offset in characters (read-only) |
| `TotalContentHeight` | `int` | `0` | Total height of the inner content in lines (read-only) |
| `TotalContentWidth` | `int` | `0` | Total width of the inner content in characters (read-only) |
| `ViewportHeight` | `int` | `0` | Height of the visible viewport in lines (read-only) |
| `ViewportWidth` | `int` | `0` | Width of the visible viewport in characters (read-only) |
| `CanScrollUp` | `bool` | `false` | Whether content can be scrolled up (read-only) |
| `CanScrollDown` | `bool` | `false` | Whether content can be scrolled down (read-only) |
| `CanScrollLeft` | `bool` | `false` | Whether content can be scrolled left (read-only) |
| `CanScrollRight` | `bool` | `false` | Whether content can be scrolled right (read-only) |
| `BorderStyle` | `BorderStyle` | `None` | Border style around the panel |
| `BorderColor` | `Color?` | `null` | Border color (uses foreground color when null) |
| `Padding` | `Padding` | `(0,0,0,0)` | Padding inside the border |
| `Header` | `string?` | `null` | Header text displayed in the top border |
| `HeaderAlignment` | `TextJustification` | `Left` | Alignment of the header text |
| `Children` | `IReadOnlyList<IWindowControl>` | empty | Snapshot of the child controls (read-only) |
| `Height` | `int?` | `null` | Fixed height (clamped to ≥ 0; auto-sized if null) |
| `Width` | `int?` | `null` | Fixed width (auto-sized if null) |
| `BackgroundColor` | `Color` | `Color.Transparent` | Background color (transparent inherits parent/gradient) |
| `ForegroundColor` | `Color` | `Color.White` | Foreground color |
| `IsEnabled` | `bool` | `true` | Enable/disable the panel |
| `HasFocus` | `bool` | `false` | Whether this panel or a descendant is in the focus path (read-only) |
| `CanReceiveFocus` | `bool` | - | True when the panel has scrollable content or focusable children (read-only) |
| `ForceReceiveFocus` | `bool` | `false` | Forces focusability even with non-interactive children |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `Scrolled` | `ScrollEventArgs` | Raised when the panel is scrolled (carries `Direction`, `VerticalOffset`, `HorizontalOffset`) |
| `GotFocus` | `EventArgs` | Raised when the panel gains focus |
| `LostFocus` | `EventArgs` | Raised when the panel loses focus |
| `MouseClick` | `MouseEventArgs` | Mouse click (interface requirement) |
| `MouseDoubleClick` | `MouseEventArgs` | Mouse double-click (interface requirement) |
| `MouseRightClick` | `MouseEventArgs` | Mouse right-click (interface requirement) |
| `MouseEnter` | `MouseEventArgs` | Mouse enters the control area (interface requirement) |
| `MouseLeave` | `MouseEventArgs` | Mouse leaves the control area (interface requirement) |
| `MouseMove` | `MouseEventArgs` | Mouse moves over the control (interface requirement) |

## Methods

### Child Management

| Method | Description |
|--------|-------------|
| `AddControl(IWindowControl control)` | Add a child control to the panel |
| `InsertControl(int index, IWindowControl control)` | Insert a child at the given index (clamped) |
| `RemoveControl(IWindowControl control)` | Remove a child control |
| `ClearContents()` | Remove and dispose all child controls |
| `GetChildren()` | Get children for Tab navigation traversal |

### Scrolling

| Method | Description |
|--------|-------------|
| `ScrollVerticalBy(int lines)` | Scroll vertically by lines (positive = down, negative = up; clamped) |
| `ScrollHorizontalBy(int chars)` | Scroll horizontally by characters (positive = right; clamped) |
| `ScrollToTop()` | Scroll to the top of the content |
| `ScrollToBottom()` | One-shot scroll to the bottom (deferred until laid out; does not enable `AutoScroll`) |
| `ScrollToPosition(int vertical, int horizontal = 0)` | Scroll to a specific position |
| `ScrollChildIntoView(IWindowControl child)` | Scroll so the given child is visible (called automatically on child focus) |

## Keyboard Support

The panel acts as an opaque focus scope. Keys are first delegated to the focused child; remaining keys are handled by the panel.

| Key | Action |
|-----|--------|
| **Tab** | Move focus to the next focusable child (exits panel when past the last child) |
| **Shift+Tab** | Move focus to the previous focusable child (exits panel before the first child) |
| **Escape** | Save the focused child and drop into scroll mode (focus the panel itself) |
| **Up / Down Arrow** | Scroll up/down one line (scroll mode, vertical scroll enabled) |
| **Page Up / Page Down** | Scroll up/down one viewport height (scroll mode) |
| **Home** | Scroll to top (scroll mode) |
| **End** | Scroll to bottom and re-enable `AutoScroll` (scroll mode) |
| **Left / Right Arrow** | Scroll left/right one character (scroll mode, horizontal scroll enabled) |

> Scrolling keys only act when the panel actually needs scrolling (content exceeds the viewport).

## Mouse Support

ScrollablePanelControl implements `IMouseAwareControl` and always wants mouse events.

- **Mouse wheel up/down**: Forwarded to the child under the cursor first; if unhandled, scrolls the panel viewport (when `EnableMouseWheel` is true). Scrolls by `ControlDefaults.DefaultScrollWheelLines` per notch.
- **Scrollbar drag**: Click and drag the scrollbar thumb to scroll; clicking the track jumps the view.
- **Click on a child**: Focuses the clicked child and routes the click to it.
- **Click on empty space**: Enters scroll mode (focuses the panel itself).
- **Child mouse capture**: While a child is dragging, events stay routed to it to prevent drag-stealing between siblings.

## Examples

### Stacking Controls in a Scrollable Region

```csharp
var content = Controls.ScrollablePanel()
    .AddControl(Controls.Markup("[bold underline cyan]Markup Syntax Showcase[/]")
        .Centered().Build())
    .AddControl(Controls.Rule("Basic Named Colors"))
    .AddControl(Controls.Markup(
        "  [red]red[/]  [green]green[/]  [blue]blue[/]  [cyan]cyan[/]").Build())
    .AddControl(Controls.Rule("Extended Colors"))
    .AddControl(Controls.Markup(
        "  [orange1]orange1[/]  [hotpink]hotpink[/]  [coral]coral[/]").Build())
    .Build();

window.AddControl(content);
```

### Filling a Side Panel (IDE Layout)

```csharp
var explorerPanel = Controls.ScrollablePanel()
    .AddControl(projectTree)
    .WithScrollbar(true)
    .WithAlignment(HorizontalAlignment.Stretch)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .Build();
```

### Bordered Panel with a Header

```csharp
var settings = Controls.ScrollablePanel()
    .SingleBorder()
    .WithHeader("Settings")
    .WithHeaderAlignment(TextJustification.Center)
    .WithPadding(1, 0, 1, 0)
    .AddControl(Controls.Checkbox("Enable notifications"))
    .AddControl(Controls.Checkbox("Auto-save"))
    .AddControl(Controls.Checkbox("Dark mode"))
    .WithHeight(12)
    .Build();

window.AddControl(settings);
```

### Live Log View with Auto-Scroll

```csharp
var logPanel = Controls.ScrollablePanel()
    .WithAutoScroll(true)        // stays pinned to the bottom as lines arrive
    .WithScrollbar(true)
    .WithName("log")
    .WithHeight(15)
    .Build();

window.AddControl(logPanel);

// Later, from the UI thread, append output:
logPanel.AddControl(Controls.Markup("[green]Build started...[/]").Build());
logPanel.AddControl(Controls.Markup("[green]Build succeeded.[/]").Build());
// AutoScroll keeps the newest line visible while the user is at the bottom;
// scrolling up detaches auto-scroll, scrolling back to the bottom re-attaches it.
```

### Reacting to Scrolling and Programmatic Control

```csharp
var panel = Controls.ScrollablePanel()
    .AddControl(longContent)
    .WithHeight(20)
    .OnScrolled((sender, e) =>
    {
        var p = (ScrollablePanelControl)sender!;
        // e.VerticalOffset, e.Direction, p.CanScrollDown, p.TotalContentHeight ...
    })
    .Build();

window.AddControl(panel);

// Programmatic scrolling
panel.ScrollToBottom();
panel.ScrollVerticalBy(-5);   // up 5 lines
panel.ScrollToTop();
```

### Replacing Content at Runtime

```csharp
var panel = Controls.ScrollablePanel().WithName("body").WithHeight(20).Build();
window.AddControl(panel);

// Swap the page contents
panel.ClearContents();
panel.AddControl(Controls.Header("New Page"));
panel.AddControl(Controls.Markup("Fresh content here.").Build());
```

## Best Practices

1. **Set an explicit height** with `.WithHeight()` (or use `Fill`/`Stretch` alignment) so the panel has a defined viewport to scroll within.
2. **Use `AutoScroll` for live output** (logs, chat, build output) — it keeps the newest content visible while still letting users scroll back to read history.
3. **Mutate children from the UI thread** — `AddControl`, `RemoveControl`, and `ClearContents` are not thread-safe. From background work, marshal with `EnqueueOnUIThread`.
4. **Wrap tab content** in a ScrollablePanel — `TabControl` does not scroll on its own, so it is the recommended container for tab pages.
5. **Hide the scrollbar** with `.WithScrollbar(false)` for compact panels where a visible scrollbar would be distracting; mouse-wheel and keyboard scrolling still work.
6. **Use `Escape` to enter scroll mode** when the panel holds focusable children — it lets users scroll the viewport without leaving the panel via Tab.

## See Also

- [TabControl](TabControl.md) - Multi-page container; wrap each page in a ScrollablePanel for scrolling
- [HorizontalGridControl](HorizontalGridControl.md) - For multi-column layouts that can be nested inside a panel

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
