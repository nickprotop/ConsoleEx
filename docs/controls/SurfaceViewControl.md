# SurfaceViewControl

Shows a grid of cells that something else painted.

## Overview

Every other control renders content of its own. A surface view renders content
that arrived from outside: another application's screen over a diagnostics
socket, a remote host, a recorded session, a rendered preview. It is a window
onto a cell grid, and the two things it has to get right are that a cell arrives
unchanged and that a surface larger than the view can be scrolled over.

Because the surface is someone else's screen, it changes size when they do. That
is why the explicit size matters here more than elsewhere: give the view a
`Width` and `Height` and it holds still and scrolls; leave them off and it
measures itself from whatever it is currently showing.

```csharp
var view = Controls.SurfaceView(80, 24)
    .WithSurface(rows)
    .OnCellClicked((s, p) => Send($"click {p.X} {p.Y}"))
    .Build();
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SurfaceWidth` | `int` | `0` | Columns in the surface — the widest row. Read-only. |
| `SurfaceHeight` | `int` | `0` | Rows in the surface. Read-only. |
| `ScrollX` | `int` | `0` | First surface column shown. Clamped to the surface. |
| `ScrollY` | `int` | `0` | First surface row shown. Clamped to the surface. |
| `BackgroundColor` | `Color` | container's | Shown where the surface does not reach. |
| `ForegroundColor` | `Color` | container's | The padding around the surface. |
| `WantsMouseEvents` | `bool` | `false` | True once `CellClicked` has a subscriber. Read-only. |

Scroll positions are **clamped, not rejected**: scrolling past the end shows the
end. A pane that scrolled into blankness would read as the application having
died.

## Events

| Event | Argument | When |
|-------|----------|------|
| `CellClicked` | `Point` | A cell was clicked, in **surface** coordinates |

The point is the surface's own coordinate, with the scroll offset already
applied — so it can be sent straight back to whatever produced the surface
without the caller redoing the arithmetic.

```csharp
view.CellClicked += (s, p) => socket.Send($"{{\"cmd\":\"mouse\",\"x\":{p.X},\"y\":{p.Y},\"action\":\"click\"}}");
```

What a click *means* is the caller's business. This control only says where.

## Setting the surface

`SetSurface` replaces what is shown:

```csharp
var rows = new List<IReadOnlyList<Cell>>();
foreach (string line in lines)
    rows.Add(MarkupParser.Parse(line, Color.White, Color.Black));

view.SetSurface(rows);
```

Rows need not be the same length; the surface is as wide as its widest. Scroll
positions are re-clamped on every call, so a surface that shrinks cannot leave
the view looking at nothing.

## Sizing

An explicit size wins over the surface's own, as everywhere else in the library:

```csharp
// Holds at 80x24 and scrolls, whatever the surface does.
var pinned = Controls.SurfaceView(80, 24).WithSurface(rows).Build();

// Grows and shrinks with the surface.
var natural = Controls.SurfaceView().WithSurface(rows).Build();
```

With `HorizontalAlignment.Stretch` or `VerticalAlignment.Fill` the view takes
what it is offered instead, which is what you want inside a scrolling parent or
a grid cell.

## Scrolling

```csharp
view.ScrollX += 4;
view.ScrollY += 1;
```

The control reports both axes to the layout system — surface extent against
viewport extent — because a view onto someone else's screen is usually smaller
than what it shows, and "is there more to the right" is the first question
anyone asks of it.

## Builder

| Method | Description |
|--------|-------------|
| `WithSize(w, h)` | Both dimensions at once |
| `WithWidth(w)` / `WithHeight(h)` | One dimension |
| `WithSurface(rows)` | The cells to show |
| `WithScroll(x, y)` | Where in the surface to start |
| `WithBackgroundColor(c)` / `WithForegroundColor(c)` / `WithColors(fg, bg)` | Colors |
| `OnCellClicked(handler)` | Subscribe to `CellClicked` |
| `WithAlignment` / `WithVerticalAlignment` / `WithMargin` / `WithVisible` / `WithName` / `WithTag` / `WithStickyPosition` | The usual control chrome |

`Controls.SurfaceView(width, height)` takes the size directly, so the common
case is one call.

The builder sets the surface **before** the scroll offset, because the offset is
clamped against the surface's size — applied to an empty view it would clamp to
zero and be lost.

## Notes

- The control is not interactive and takes no keyboard focus; it has no
  `IsEnabled`. It shows a screen, it does not act like one.
- It implements `IMouseAwareControl`, and `WantsMouseEvents` is derived from
  whether anything subscribed to `CellClicked` — a view nobody is listening to
  does not take mouse events away from what is underneath it.
- Cells are copied with their flags intact, so wide characters, combining marks
  and decorations survive the round trip.

## See also

- [CanvasControl](CanvasControl.md) — draw your own cells rather than show
  someone else's
- [Debugging](../DEBUGGING.md) — the diagnostics socket, one source of a
  surface to show
