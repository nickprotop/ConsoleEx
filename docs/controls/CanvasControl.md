# CanvasControl

Free-form drawing surface that exposes `CharacterBuffer` drawing primitives through a local-coordinate API. Supports both retained-mode (async) and immediate-mode (event-driven) painting, or a combination of both.

## Overview

`CanvasControl` gives you a pixel-level (cell-level) drawing canvas inside any window. It owns an internal `CharacterBuffer` that persists drawn content across render cycles. You can draw to this buffer at any time from any thread using `BeginPaint()`/`EndPaint()`, or subscribe to the `Paint` event to redraw each frame. Both approaches can be combined: the internal buffer is composited first, then the `Paint` event fires for overlay drawing on top.

The `CanvasGraphics` context returned by `BeginPaint()` and passed through the `Paint` event provides 30+ drawing methods — text, lines, boxes, circles, ellipses, arcs, polygons, gradients, patterns — all operating in canvas-local coordinates (0,0 = top-left of the canvas).

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CanvasWidth` | `int` | `40` | Logical canvas width in characters. Setting recreates the internal buffer. |
| `CanvasHeight` | `int` | `20` | Logical canvas height in characters. Setting recreates the internal buffer. |
| `AutoSize` | `bool` | `false` | When true, the internal buffer automatically resizes to match the layout bounds assigned by the parent container. Enable with Stretch/Fill alignment. |
| `AutoClear` | `bool` | `false` | When true, the internal buffer is cleared after compositing each frame, so the `Paint` event redraws from scratch (immediate mode). |
| `BackgroundColor` | `Color` | Container or Black | Background color for the canvas. |
| `ForegroundColor` | `Color` | Container or White | Default foreground color. |
| `IsEnabled` | `bool` | `true` | Whether the control accepts input. |
| `HasFocus` | `bool` | `false` | Whether the control has keyboard focus. |
| `Margin` | `Margin` | `0,0,0,0` | Layout margin around the canvas. |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | Horizontal alignment. Use `Stretch` with `AutoSize` for responsive sizing. |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | Vertical alignment. Use `Fill` with `AutoSize` for responsive sizing. |

## Events

| Event | Args | Description |
|-------|------|-------------|
| `Paint` | `CanvasPaintEventArgs` | Fires during each render cycle after compositing. `Graphics` draws to the window buffer. |
| `CanvasMouseClick` | `CanvasMouseEventArgs` | Left-click with canvas-local coordinates. |
| `CanvasMouseRightClick` | `CanvasMouseEventArgs` | Right-click with canvas-local coordinates. |
| `CanvasMouseMove` | `CanvasMouseEventArgs` | Mouse move with canvas-local coordinates. |
| `CanvasKeyPressed` | `ConsoleKeyInfo` | Key press while the canvas has focus. |
| `GotFocus` | `EventArgs` | The canvas received keyboard focus. |
| `LostFocus` | `EventArgs` | The canvas lost keyboard focus. |

### Event Args

**CanvasPaintEventArgs:**

| Property | Type | Description |
|----------|------|-------------|
| `Graphics` | `CanvasGraphics` | Drawing context wrapping the window buffer with offset translation. |
| `CanvasWidth` | `int` | Current canvas width. |
| `CanvasHeight` | `int` | Current canvas height. |

**CanvasMouseEventArgs:**

| Property | Type | Description |
|----------|------|-------------|
| `CanvasX` | `int` | X coordinate in canvas-local space (0-based). |
| `CanvasY` | `int` | Y coordinate in canvas-local space (0-based). |
| `OriginalArgs` | `MouseEventArgs` | Original mouse event for access to flags and absolute position. |

## Two Painting Modes

### Retained Mode (BeginPaint/EndPaint)

Content drawn via `BeginPaint()`/`EndPaint()` persists in the internal buffer across render cycles. Draw whenever you want, from any thread.

```csharp
var g = canvas.BeginPaint();
try
{
    g.Clear(Color.DarkBlue);
    g.DrawBox(0, 0, 60, 20, BoxChars.Single, Color.White, Color.DarkBlue);
    g.WriteStringCentered(10, "Hello Canvas!", Color.White, Color.DarkBlue);
}
finally
{
    canvas.EndPaint(); // releases lock, triggers repaint
}
```

### Immediate Mode (Paint Event)

Subscribe to the `Paint` event and set `AutoClear = true`. The canvas clears before each frame, so you redraw from scratch.

```csharp
var canvas = new CanvasControl { AutoClear = true };
canvas.Paint += (sender, e) =>
{
    var g = e.Graphics;
    g.DrawCircle(30, 10, 8, '*', Color.Yellow, Color.DarkBlue);
};
```

### Combined: Retained Background + Event Overlay

The internal buffer is composited first, then the `Paint` event fires for drawing on top.

```csharp
// Static background drawn once
var g = canvas.BeginPaint();
g.GradientFillRect(0, 0, 60, 20, Color.DarkBlue, Color.Black, horizontal: false);
canvas.EndPaint();

// Dynamic overlay redrawn each frame
canvas.Paint += (sender, e) =>
{
    e.Graphics.WriteStringCentered(10, $"Time: {DateTime.Now:HH:mm:ss}",
        Color.White, Color.DarkBlue);
};
```

## Creating a Canvas

### Fixed Size

```csharp
var canvas = new CanvasControl(80, 24);
window.AddControl(canvas);
```

### Auto-Sizing (Stretch/Fill)

```csharp
var canvas = new CanvasControl
{
    HorizontalAlignment = HorizontalAlignment.Stretch,
    VerticalAlignment = VerticalAlignment.Fill,
    AutoSize = true
};
window.AddControl(canvas);
```

The canvas will resize its internal buffer to match the space assigned by the parent container.

### Using WindowBuilder

```csharp
var canvas = new CanvasControl
{
    HorizontalAlignment = HorizontalAlignment.Stretch,
    VerticalAlignment = VerticalAlignment.Fill,
    AutoSize = true,
    AutoClear = true
};

canvas.Paint += (sender, e) =>
{
    // Draw each frame
};

var window = new WindowBuilder(ws)
    .WithTitle("My Canvas")
    .WithSize(60, 25)
    .Centered()
    .Resizable(true)
    .AddControl(canvas)
    .Build();

ws.AddWindow(window);
```

## CanvasGraphics API

`CanvasGraphics` wraps a `CharacterBuffer` and translates all coordinates from canvas-local (0,0) to absolute buffer position. You get a `CanvasGraphics` from `BeginPaint()` or from `CanvasPaintEventArgs.Graphics`.

### Core

| Method | Description |
|--------|-------------|
| `SetCell(x, y, ch, fg, bg)` | Set a single cell. |
| `GetCell(x, y)` | Read a cell from the buffer. |
| `Clear()` | Clear with the canvas background color. |
| `Clear(bg)` | Clear with a specific color. |
| `FillRect(x, y, w, h, ch, fg, bg)` | Fill a rectangle with a character and colors. |
| `FillRect(x, y, w, h, bg)` | Fill a rectangle with a background color. |

### Text

| Method | Description |
|--------|-------------|
| `WriteString(x, y, text, fg, bg)` | Write text at a position. |
| `WriteStringCentered(y, text, fg, bg)` | Write text centered horizontally. |
| `WriteStringRight(y, text, fg, bg)` | Write text right-aligned. |
| `WriteStringInBox(x, y, w, h, text, fg, bg)` | Write text centered in a box region. |
| `WriteWrappedText(x, y, w, text, fg, bg)` | Write text with word wrapping. |

### Lines and Boxes

| Method | Description |
|--------|-------------|
| `DrawLine(x0, y0, x1, y1, ch, fg, bg)` | Draw a line between two points (Bresenham). |
| `DrawHorizontalLine(x, y, length, ch, fg, bg)` | Draw a horizontal line. |
| `DrawVerticalLine(x, y, length, ch, fg, bg)` | Draw a vertical line. |
| `DrawBox(x, y, w, h, boxChars, fg, bg)` | Draw a box with border characters. |

### Circles and Ellipses

| Method | Description |
|--------|-------------|
| `DrawCircle(cx, cy, r, ch, fg, bg)` | Draw a circle outline. |
| `FillCircle(cx, cy, r, ch, fg, bg)` | Draw a filled circle. |
| `DrawEllipse(cx, cy, rx, ry, ch, fg, bg)` | Draw an ellipse outline. |
| `FillEllipse(cx, cy, rx, ry, ch, fg, bg)` | Draw a filled ellipse. |
| `DrawArc(cx, cy, r, startAngle, endAngle, ch, fg, bg)` | Draw a circular arc. |

### Polygons

| Method | Description |
|--------|-------------|
| `DrawTriangle(points, ch, fg, bg)` | Draw a triangle outline from 3 points. |
| `FillTriangle(points, ch, fg, bg)` | Draw a filled triangle from 3 points. |
| `DrawPolygon(points, ch, fg, bg)` | Draw a polygon outline from N points. |
| `FillPolygon(points, ch, fg, bg)` | Draw a filled polygon from N points. |

### Gradients and Patterns

| Method | Description |
|--------|-------------|
| `GradientFillHorizontal(x, y, w, h, left, right)` | Horizontal gradient fill. |
| `GradientFillVertical(x, y, w, h, top, bottom)` | Vertical gradient fill. |
| `GradientFillRect(x, y, w, h, start, end, horizontal)` | Gradient fill in either direction. |
| `PatternFill(x, y, w, h, pattern, fg, bg)` | Fill a rectangle with a repeating text pattern. |
| `CheckerFill(x, y, w, h, ch1, ch2, fg1, bg1, fg2, bg2)` | Fill with alternating checker cells. |
| `StippleFill(x, y, w, h, density, ch, fg, bg)` | Fill with random stipple pattern at a given density. |

## Keyboard Support

All key events are forwarded to the `CanvasKeyPressed` event when the canvas has focus.

| Key | Behavior |
|-----|----------|
| Any key | Fires `CanvasKeyPressed` with the `ConsoleKeyInfo` |
| Tab | Moves focus to next control (default window behavior) |

## Mouse Support

| Event | Behavior |
|-------|----------|
| Left click | Fires `CanvasMouseClick` with canvas-local coordinates, focuses the canvas |
| Right click | Fires `CanvasMouseRightClick` with canvas-local coordinates |
| Mouse move | Fires `CanvasMouseMove` with canvas-local coordinates |

Coordinates are automatically translated from absolute screen position to canvas-local (0,0 = top-left of the drawing area, excluding margins).

## Thread Safety

`BeginPaint()` acquires a monitor lock on the internal buffer. `EndPaint()` releases it and invalidates the window. This makes it safe to draw from background threads, timers, or async loops.

```csharp
// Safe from any thread
Task.Run(async () =>
{
    while (!ct.IsCancellationRequested)
    {
        var g = canvas.BeginPaint();
        // draw...
        canvas.EndPaint();
        await Task.Delay(16, ct); // ~60fps
    }
});
```

The `Paint` event fires within the render lock, so no additional synchronization is needed there.

## Examples

### Interactive Drawing

```csharp
canvas.CanvasMouseClick += (sender, e) =>
{
    var g = canvas.BeginPaint();
    g.FillCircle(e.CanvasX, e.CanvasY, 2, '*', Color.Red, Color.Black);
    canvas.EndPaint();
};
```

### Animated Background with Async Thread

```csharp
var canvas = new CanvasControl
{
    HorizontalAlignment = HorizontalAlignment.Stretch,
    VerticalAlignment = VerticalAlignment.Fill,
    AutoSize = true
};

var window = new WindowBuilder(ws)
    .WithTitle("Animation")
    .WithSize(60, 25)
    .Centered()
    .WithAsyncWindowThread(async (win, ct) =>
    {
        int frame = 0;
        while (!ct.IsCancellationRequested)
        {
            var g = canvas.BeginPaint();
            g.Clear(Color.Black);
            int cx = canvas.CanvasWidth / 2;
            int cy = canvas.CanvasHeight / 2;
            int r = (int)(Math.Sin(frame * 0.05) * 5 + 7);
            g.FillCircle(cx, cy, r, '*', Color.Cyan, Color.Black);
            canvas.EndPaint();
            frame++;
            await Task.Delay(33, ct);
        }
    })
    .AddControl(canvas)
    .Build();
```

### Geometry Showcase

```csharp
canvas.Paint += (sender, e) =>
{
    var g = e.Graphics;
    int w = e.CanvasWidth, h = e.CanvasHeight;

    // Gradient background
    g.GradientFillRect(0, 0, w, h, Color.DarkBlue, Color.Black, horizontal: false);

    // Centered circle
    g.DrawCircle(w / 2, h / 2, Math.Min(w, h) / 4, 'o', Color.Cyan, Color.DarkBlue);

    // Box with text
    g.DrawBox(2, 2, 20, 5, BoxChars.Double, Color.Yellow, Color.DarkBlue);
    g.WriteStringInBox(2, 2, 20, 5, "Hello!", Color.White, Color.DarkBlue);

    // Polygon
    var points = new (int X, int Y)[]
    {
        (w - 15, 3), (w - 5, 3), (w - 10, 8)
    };
    g.FillTriangle(points, '#', Color.Green, Color.DarkBlue);
};
```

## Best Practices

- **Use `AutoSize = true` with `Stretch`/`Fill`** when the canvas should adapt to the window size. Read `CanvasWidth`/`CanvasHeight` each frame for proportional drawing.
- **Always pair `BeginPaint()` with `EndPaint()`** in a try/finally block to avoid deadlocks.
- **Prefer retained mode** for content that changes infrequently (backgrounds, static shapes). Use the `Paint` event for content that changes every frame (animations, overlays).
- **Read dimensions each frame** in the `Paint` event — `e.CanvasWidth` and `e.CanvasHeight` may change if the window is resized.
- **Don't call `BeginPaint()` inside a `Paint` handler** — the render lock is already held, and you would deadlock.

## CanvasControl vs. Compositor Effects

| Feature | CanvasControl | [Compositor Effects](../COMPOSITOR_EFFECTS.md) |
|---------|---------------|------------------------------------------------|
| Scope | Single control within a window | Entire window buffer |
| Coordinates | Canvas-local (0,0 = top-left of control) | Absolute buffer coordinates |
| Drawing API | `CanvasGraphics` (30+ methods) | Raw `CharacterBuffer` cell access |
| Persistence | Internal buffer retains content | No persistence (runs each frame) |
| Use case | Custom drawing surfaces, games, visualizations | Post-processing effects, transitions, overlays |
| Thread model | `BeginPaint()`/`EndPaint()` from any thread | Runs in render lock via event |

Use `CanvasControl` when you need a self-contained drawing area within your window layout. Use [Compositor Effects](../COMPOSITOR_EFFECTS.md) when you need to manipulate the entire rendered window buffer (blur, fade, color grading).

## See Also

- [Compositor Effects](../COMPOSITOR_EFFECTS.md) — Window-level buffer manipulation
- [DOM Layout System](../DOM_LAYOUT_SYSTEM.md) — How controls are measured and arranged
- [Rendering Pipeline](../RENDERING_PIPELINE.md) — Understanding the rendering flow
- [Controls Reference](../CONTROLS.md) — Complete list of available controls

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
