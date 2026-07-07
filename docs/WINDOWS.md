# Windows

A **Window** is the primary container in SharpConsoleUI. Windows hold controls, draw a border and
title bar, can be moved/resized/maximized, and stack in z-order on the desktop. This guide covers
creating and configuring windows; for the controls you put inside them see [CONTROLS.md](CONTROLS.md),
and for the full fluent builder surface see [BUILDERS.md](BUILDERS.md).

## Table of Contents

- [Creating a window](#creating-a-window)
- [Size and position](#size-and-position)
- [Border styles](#border-styles)
  - [Frameless windows](#frameless-windows)
- [Padding](#padding)
- [Window state](#window-state)
- [Interactivity (move / resize / buttons)](#interactivity-move--resize--buttons)
- [Modal windows](#modal-windows)
- [Events](#events)
- [Moving and resizing in code](#moving-and-resizing-in-code)

## Creating a window

Use the fluent `WindowBuilder`. `Build()` returns the window; `BuildAndShow()` builds it and adds it
to the system (optionally activating it).

```csharp
var window = new WindowBuilder(windowSystem)
    .WithTitle("Hello")
    .WithSize(60, 20)
    .Centered()
    .AddControl(Controls.Markup("[bold]Welcome[/]").Build())
    .BuildAndShow();
```

The classic, non-fluent form still works:

```csharp
var window = new Window(windowSystem) { Title = "Hello", Width = 60, Height = 20 };
windowSystem.AddWindow(window);
```

## Size and position

```csharp
.WithSize(width, height)                  // window size in cells
.AtPosition(x, y) / .WithPosition(x, y)   // top-left, in desktop coordinates
.WithBounds(x, y, width, height)          // size + position in one call
.Centered()                               // center on the desktop
.WithMinimumSize(w, h) / .WithMaximumSize(w, h)
```

Positions are in **desktop coordinates** (the area inside any top/bottom status bars). The desktop
automatically excludes the status bars; a hidden bar reclaims its row (see [STATUS_SYSTEM.md](STATUS_SYSTEM.md)).

## Placement (snap zones)

`Window.Placement` (nullable `SharpConsoleUI.Layout.Placement`) is a Windows-11-style declarative
placement that resolves to bounds against the live usable desktop. It is **sticky live state**:

- Setting it snaps the window to the resolved zone/anchor.
- On a **desktop resize** the placement **re-resolves** (a `LeftHalf` window stays the left half at
  the new size) — the same auto-refit behaviour `Maximized` windows already have.
- A **manual drag or resize detaches it** — the window becomes free-floating `Normal` again (exactly
  as dragging a maximized window restores it).

```csharp
using SharpConsoleUI.Layout;

window.Placement = Placement.Snap(SnapZone.LeftHalf);   // fills the left half
window.Placement = Placement.Maximized;                 // == Snap(SnapZone.Full)
window.Placement = Placement.Center(SizePreset.Medium); // 60% of the desktop, centered
window.Placement = Placement.Anchor(Anchor.TopRight, 40, 12, margin: 1);
window.Placement = null;                                // detach → free-floating
```

Build-time (declarative): `new WindowBuilder(ws).WithPlacement(Placement.Snap(SnapZone.RightHalf))`.

**Tier-1 `SnapZone`s (9):** `Full`, `LeftHalf`, `RightHalf`, `TopHalf`, `BottomHalf`, `TopLeft`,
`TopRight`, `BottomLeft`, `BottomRight`. On odd desktop sizes the remainder cell goes to the
left/top zone. Placement is orthogonal to `WindowState` — a placed window stays `Normal`. Bounds are
resolved by the `WindowPlacementService` (see [STATE-SERVICES.md](STATE-SERVICES.md)).

## Border styles

`BorderStyle` controls the window frame. Set it via `.WithBorderStyle(style)`, the `.Borderless()` /
`.Frameless()` shortcuts, or the `Window.BorderStyle` property (which can be changed live).

| Style | Frame | Interactive chrome | Notes |
|-------|-------|--------------------|-------|
| `DoubleLine` (default) | `╔═╗║╚╝` active, `┌─┐│└┘` inactive | Yes | Traditional double-line border. |
| `Single` | `┌─┐│└┘` | Yes | Consistent single-line border. |
| `Rounded` | `╭─╮│╰╯` | Yes | Rounded corners. |
| `None` | Invisible (blank) | Yes | **Invisible** border: the 1-cell frame is still reserved (content starts one row/column in) and the title bar, drag, resize, and buttons still work — they just aren't drawn. |
| `Frameless` | None at all | **No** | Content fills the entire rect; fully chrome-less. See below. |

```csharp
.WithBorderStyle(BorderStyle.Rounded)
.Borderless()    // = BorderStyle.None  (invisible border, frame reserved)
.Frameless()     // = BorderStyle.Frameless  (no frame, no chrome)
```

### Frameless windows

`.Frameless()` reclaims the border/title frame so content fills the **entire** window rect — there is
no reserved row or column. A frameless window is **fully chrome-less and non-interactive at the frame
level**: no title bar, no drag-to-move handle, no resize edges or grip, and no title buttons. Mouse
clicks anywhere go straight to the content.

This is the right choice for single-window / full-screen apps and for stacking content without wasted
border padding. The common pattern fills the terminal:

```csharp
new WindowBuilder(windowSystem)
    .Frameless()
    .Maximized()            // size to the whole desktop
    .WithPadding(1)         // optional breathing room (see below)
    .AddControl(myContent)
    .BuildAndShow();
```

Notes:
- **Move/resize still work in code.** Frameless removes the *grab surface*, not the *capability*.
  `SetPosition(...)` / `SetSize(...)` (and any keyboard handler you wire) move and resize a frameless
  window normally. `IsMovable` / `IsResizable` keep their values but the (non-existent) frame can't be
  grabbed with the mouse.
- **Scrollbar.** A scrollable frameless window reserves its last content column for the scrollbar when
  content overflows, so the bar never overlaps content; otherwise content uses the full width.
- **`Frameless` vs `None`.** `None` keeps the invisible 1-cell frame (content still starts one cell in)
  and remains interactive; `Frameless` reclaims the frame entirely and is chrome-less.

## Padding

`Window.Padding` adds space *inside* the window, between the frame and the content. It applies to every
window (default `Padding.None`, so existing windows are unchanged):

```csharp
.WithPadding(2)                          // uniform, all four sides
.WithPadding(horizontal, vertical)       // left/right, top/bottom
.WithPadding(new Padding(left, top, right, bottom))   // per-side
```

- On a **frameless** window, padding is the only inset — `WithPadding(1)` gives a 1-cell margin around
  content that otherwise fills the rect.
- On a **bordered** window, padding adds inner space between the border and the content.

(Top-level windows have no concept of *margin* — margin is space between an element and its siblings,
and a window has no layout parent. Use padding.)

## Window state

```csharp
.Maximized()           // start maximized (fills the desktop)
.Minimized()           // start minimized
.WithState(WindowState.Normal | Maximized | Minimized)
```

At runtime: `window.Maximize()`, `window.Minimize()`, `window.Restore()`. Maximizing sizes the window
to the full desktop; restoring returns it to its previous bounds.

## Interactivity (move / resize / buttons)

```csharp
.Movable(bool)             // drag the title bar to move (default true)
.Resizable(bool)           // drag edges/grip to resize (default true)
.WithResizeDirections(ResizeBorderDirections)   // which edges allow resize
.Closable(bool)            // show/allow the close button (default true)
.Minimizable(bool) / .Maximizable(bool)
.HideCloseButton() / .HideTitleButtons()
.HideTitle()               // hide the title text (frame/buttons remain)
```

These govern the **bordered** chrome. A `Frameless` window has no chrome, so these mouse affordances
have nowhere to live (the flags keep their values but are inert for mouse interaction — capability via
`SetPosition`/`SetSize` is unaffected).

## Modal windows

```csharp
.AsModal()                 // or .WithModal(true)
```

A modal window blocks interaction with windows beneath it until it closes. Combine with a
`TaskCompletionSource` for await-style dialogs — see [DIALOGS.md](DIALOGS.md).

## Events

```csharp
.OnShown(handler)
.OnActivated(handler) / .OnDeactivated(handler)
.OnKeyPressed(handler)         // KeyPressedEventArgs; set e.Handled to consume
.OnClosing(handler)            // ClosingEventArgs; can veto via the args
.OnClosed(handler)
.OnResize(handler)
.OnStateChanged(handler)
```

A common idiom is closing on ESC:

```csharp
.OnKeyPressed((sender, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.Escape)
    {
        windowSystem.CloseWindow(window);
        e.Handled = true;
    }
})
```

This is the standard way to make a `Frameless` window closable, since it has no close button.

## Moving and resizing in code

These work for **any** window, including frameless ones:

```csharp
window.SetPosition(new Point(x, y));   // move
window.SetSize(width, height);         // resize
```

> ⚠️ **Threading:** mutate window properties (including `Title`, `BorderStyle`, `Padding`, position,
> size) on the UI thread. From a background thread, marshal via
> `windowSystem.EnqueueOnUIThread(() => { ... })`. See [THREADING_AND_ASYNC.md](THREADING_AND_ASYNC.md).
