# SpectreRenderableControl

Wrap any Spectre.Console `IRenderable` for display within the SharpConsoleUI window system.

## Overview

SpectreRenderableControl is a bridge control that hosts any Spectre.Console renderable — tables, panels, bar charts, trees, rules, calendars, breakdown charts, or any custom `IRenderable` — inside a SharpConsoleUI window. The renderable is rendered to ANSI, parsed back into the framework's cell buffer, and painted with full color, alignment, and margin support.

This is the only place in the framework that renders Spectre.Console `IRenderable` objects. The control captures the renderable's ANSI output, parses the escape sequences (SGR colors including 16-color, 256-color, and 24-bit RGB) into native cells, and handles Unicode width correctly (wide characters, combining marks, and VS16 emoji widening). All other controls use the built-in markup parser instead.

> **Spectre.Console is an optional dependency.** SharpConsoleUI references Spectre.Console (0.49.1) only for this control. If your application does not host Spectre renderables, you do not need to use it — but the package is already pulled in transitively by the library, so `SpectreRenderableControl` is always available.

See also: [MarkupControl](MarkupControl.md)

## Quick Start

```csharp
using SharpConsoleUI.Controls;
using Spectre.Console;

var table = new Table()
    .AddColumn("Name")
    .AddColumn("Role");
table.AddRow("Alice", "Engineer");
table.AddRow("Bob", "Designer");

var control = SpectreRenderableControl.Create()
    .WithRenderable(table)
    .WithMargin(1)
    .Build();

window.AddControl(control);
```

## Builder API

Create a builder via `SpectreRenderableControl.Create()`.

### Content

```csharp
.WithRenderable(IRenderable renderable)   // The Spectre.Console renderable to display
```

### Alignment

```csharp
.WithAlignment(HorizontalAlignment alignment)       // Left (default), Center, Right
.WithVerticalAlignment(VerticalAlignment alignment) // Top (default), Middle, Bottom
.Centered()                                         // Shortcut for HorizontalAlignment.Center
```

### Layout

```csharp
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)            // Uniform margin on all sides
.WithMargin(Margin margin)
.WithWidth(int width)              // Fixed render width
.Visible(bool visible = true)
.Hidden()
.StickyTop()
.StickyBottom()
.WithStickyPosition(StickyPosition position)
```

### Colors

```csharp
.WithBackgroundColor(Color color)
.WithForegroundColor(Color color)
.WithColors(Color foreground, Color background)
```

### Identity

```csharp
.WithName(string name)   // For FindControl queries
.WithTag(object tag)     // Custom data storage
```

### Mouse

```csharp
.WithMouseEvents(bool wants = true)     // Enable/disable mouse events (default enabled)
.CanFocusWithMouse(bool canFocus = true) // Allow mouse to focus the control (default false)
.OnClick(EventHandler<MouseEventArgs> handler)
.OnDoubleClick(EventHandler<MouseEventArgs> handler)
.OnMouseEnter(EventHandler<MouseEventArgs> handler)
.OnMouseLeave(EventHandler<MouseEventArgs> handler)
.OnMouseMove(EventHandler<MouseEventArgs> handler)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Renderable` | `IRenderable?` | `null` | The Spectre.Console renderable to display |
| `BackgroundColor` | `Color` | Container/theme fallback | Background color for rendering; falls back to container or theme colors if not explicitly set |
| `ForegroundColor` | `Color` | Theme `WindowForegroundColor`, else `White` | Foreground color for rendering; falls back to theme colors if not explicitly set |
| `WantsMouseEvents` | `bool` | `true` | Whether the control receives mouse events |
| `CanFocusWithMouse` | `bool` | `false` | Whether the control can receive focus via mouse clicks |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | Horizontal alignment of the rendered content within its bounds |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | Vertical alignment of the control |
| `Margin` | `Margin` | `0,0,0,0` | Outer margin around the content |
| `Width` | `int?` | `null` | Fixed render width; uses available width when null |
| `Visible` | `bool` | `true` | Whether the control is rendered |
| `StickyPosition` | `StickyPosition` | `None` | Pins the control to the top or bottom of the window |
| `Name` | `string?` | `null` | Identifier for `FindControl` queries |
| `Tag` | `object?` | `null` | Arbitrary user data |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `MouseClick` | `MouseEventArgs` | Raised on a single left-button click |
| `MouseDoubleClick` | `MouseEventArgs` | Raised on a left-button double-click (driver-provided or detected via threshold) |
| `MouseRightClick` | `MouseEventArgs` | Raised on a right-button (Button3) click |
| `MouseEnter` | `MouseEventArgs` | Raised when the mouse pointer enters the control |
| `MouseLeave` | `MouseEventArgs` | Raised when the mouse pointer leaves the control |
| `MouseMove` | `MouseEventArgs` | Raised when the mouse moves within the control |

## Mouse Support

SpectreRenderableControl implements `IMouseAwareControl`.

| Interaction | Behavior |
|-------------|----------|
| Left click | Raises `MouseClick`; consumes the event |
| Left double-click | Raises `MouseDoubleClick`; consumes the event |
| Right click | Raises `MouseRightClick`; consumes the event |
| Mouse wheel (any direction) | Not consumed — bubbles up to the parent so scrollable containers can handle it |
| Pointer enter/leave | Raises `MouseEnter` / `MouseLeave` and invalidates the container |
| Pointer move | Raises `MouseMove` |

Mouse handling is skipped entirely when `WantsMouseEvents` is `false`.

## Methods

| Method | Description |
|--------|-------------|
| `SetRenderable(IRenderable renderable)` | Replaces the displayed renderable and invalidates the container for a repaint |
| `static Create()` | Returns a new `SpectreRenderableBuilder` |

## Examples

### Table

```csharp
using Spectre.Console;

var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[bold]Service[/]")
    .AddColumn("[bold]Status[/]");
table.AddRow("Web", "[green]Online[/]");
table.AddRow("Database", "[green]Online[/]");
table.AddRow("Cache", "[red]Offline[/]");

window.AddControl(
    SpectreRenderableControl.Create()
        .WithRenderable(table)
        .WithMargin(1)
        .Build()
);
```

### Panel with a Title

```csharp
using Spectre.Console;

var panel = new Panel("Deployment completed successfully.")
{
    Header = new PanelHeader("[bold green]Result[/]"),
    Border = BoxBorder.Double
};

window.AddControl(
    SpectreRenderableControl.Create()
        .WithRenderable(panel)
        .Centered()
        .Build()
);
```

### Bar Chart

```csharp
using Spectre.Console;

var chart = new BarChart()
    .Width(60)
    .Label("[bold]CPU Usage by Core[/]")
    .AddItem("Core 0", 72, Color.Green)
    .AddItem("Core 1", 45, Color.Yellow)
    .AddItem("Core 2", 91, Color.Red)
    .AddItem("Core 3", 30, Color.Green);

window.AddControl(
    SpectreRenderableControl.Create()
        .WithRenderable(chart)
        .WithMargin(1)
        .Build()
);
```

### Updating the Renderable Live

```csharp
var control = SpectreRenderableControl.Create()
    .WithName("liveChart")
    .Build();

window.AddControl(control);

// Later, from the UI thread, swap in a new renderable
var spectre = window.FindControl<SpectreRenderableControl>("liveChart");
if (spectre != null)
{
    var updated = new BreakdownChart()
        .AddItem("Used", 60, Color.Red)
        .AddItem("Free", 40, Color.Green);

    spectre.SetRenderable(updated);
}
```

> When updating from a background thread (timer, `Task.Run`), marshal the call:
> ```csharp
> windowSystem.EnqueueOnUIThread(() => spectre.SetRenderable(updated));
> ```

### Constructor Usage

```csharp
using Spectre.Console;

var rule = new Rule("[yellow]Section[/]")
{
    Justification = Justify.Left
};

// Direct construction is also supported
var control = new SpectreRenderableControl(rule);
window.AddControl(control);
```

### Mouse Interaction

```csharp
var control = SpectreRenderableControl.Create()
    .WithRenderable(new Panel("Click me"))
    .WithMouseEvents()
    .OnClick((s, e) => logService.LogInfo("Renderable clicked"))
    .OnMouseEnter((s, e) => logService.LogInfo("Mouse entered"))
    .Build();

window.AddControl(control);
```

## Best Practices

1. **Build the renderable with Spectre's own API**: Configure tables, charts, and panels using Spectre.Console builders, then hand the finished `IRenderable` to `WithRenderable()`.
2. **Set a width for predictable layout**: Use `.WithWidth()` (or size the Spectre widget itself) when the renderable should not flex to fill available space.
3. **Swap content with `SetRenderable()`**: Reuse a single control and call `SetRenderable()` for live updates instead of recreating the control.
4. **Marshal background updates**: Always call `SetRenderable()` on the UI thread via `EnqueueOnUIThread` when updating from timers or background tasks.
5. **Let the wheel bubble**: Mouse-wheel events intentionally pass through to parent containers — place the control inside a scrollable panel for large renderables.
6. **Prefer MarkupControl for plain text**: If you only need formatted text, [MarkupControl](MarkupControl.md) is lighter weight; reserve this control for genuine Spectre widgets.

## See Also

- [MarkupControl](MarkupControl.md) - For rich formatted text using SharpConsoleUI markup syntax

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
