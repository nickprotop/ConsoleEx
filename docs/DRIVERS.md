# Drivers and render modes

Everything the library draws ends up at a **console driver**: the layer that turns cells into
bytes a terminal understands, and terminal bytes back into keys and mouse events. There is one
driver for real terminals, one stand-in for when there is no terminal at all, and one interface
if you want to write your own.

```
          your app
             │
   ConsoleWindowSystem
             │
      IConsoleDriver
       ╱           ╲
NetConsoleDriver   HeadlessConsoleDriver
  (a terminal)      (no terminal)
       │
  RenderMode:
  Buffer │ Direct
```

---

## NetConsoleDriver

The real one. Talks to the terminal you are sitting in front of.

```csharp
// The usual way — the constructor builds a NetConsoleDriver for you.
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);
```

It handles raw mode, the alternate screen, bracketed paste, mouse reporting, and cursor shape.
On Unix it bypasses .NET's `ConsolePal` entirely and writes ANSI itself, because `ConsolePal`
on the hot path is slow and gets in the way of raw input; if raw mode cannot be entered it
falls back to `ConsolePal` and logs that it did.

It also implements `IGraphicsProtocol`, which is how inline images reach terminals that
support the Kitty graphics protocol.

### The two render modes

`RenderMode` is the one real choice this driver offers, and it changes how writes reach the
terminal — not what gets drawn.

```csharp
new ConsoleWindowSystem(RenderMode.Buffer)   // the default, and what you want
new ConsoleWindowSystem(RenderMode.Direct)   // unbuffered
```

**`Buffer`** allocates a `ConsoleBuffer` at startup. Writes accumulate into it, and `Flush()`
paints by diffing against the previous frame, so only cells that actually changed are sent to
the terminal. This is what makes a full-screen redraw cheap and flicker-free.

**`Direct`** allocates nothing. Every write formats its own ANSI escape and is sent
immediately, cursor-addressed to its position.

What that costs you, concretely:

| | `Buffer` | `Direct` |
|---|---|---|
| Back buffer | Allocated at `Start()` | None |
| Writes reach the terminal | On `Flush()` | Immediately, per call |
| Only changed cells sent | Yes, via dirty-region diff | No — every write is emitted |
| `Flush()` | Paints the frame | **No-op** |
| `InvalidateFrontBuffer()` | Forces a full repaint | **No-op** |
| `Clear()` | Clears the buffer | `Console.Clear()` |
| `GetDirtyCharacterCount()` | Real count | **Always `0`** |
| Flicker on complex updates | No | Yes |

Two of those are worth saying plainly, because they are silent rather than loud:

- **`GetDirtyCharacterCount()` returns `0` in `Direct`** — not "unavailable", just zero. Any
  metric built on it reads as "nothing changed" when the truth is "nobody is counting".
- **`WriteBufferRegion` in `Direct` re-serialises every cell's decorations** into a string, per
  strip, every time. The flicker is the visible cost; this is the one you only see in a profile.

`Direct` exists for the cases where the diff is in your way — a one-shot program that paints
once and exits, or debugging the render path itself. **For an application, use `Buffer`.** It
is the default for a reason, and the compositor is built around it.

---

## HeadlessConsoleDriver

A driver with no terminal behind it. It accepts every call a real driver does and records the
output instead of emitting it.

```csharp
var driver = new HeadlessConsoleDriver(width: 120, height: 40);
var windowSystem = new ConsoleWindowSystem(driver, options);
```

This is what makes the test suite possible, and what makes the diagnostics socket interesting:
an application can run completely headless, be driven through the socket, and be asserted on —
with no terminal, no pty, and no screen-scraping.

What it adds over the interface:

| Member | What it gives you |
|---|---|
| `OutputHistory`, `FullOutput` | Everything the app tried to write |
| `WriteCallCount`, `TotalBytesWritten` | Write volume, for regression checks |
| `CursorPosition`, `CursorVisible`, `LastCursorBlink` | Cursor state without a terminal |
| `SimulateKeyPress(...)` | Inject a keystroke |
| `SimulateMouseEvent(...)` | Inject a click, move or wheel |
| `SimulateScreenResize(w, h)` | **Resize** — impossible on a real driver |
| `ClearHistory()` | Reset between phases |

`SimulateScreenResize` is the one with no real-terminal equivalent. `NetConsoleDriver.ScreenSize`
is owned by the terminal and read-only, so resize behaviour — where a great many layout bugs
live — is otherwise only reachable by hand. Headless, it is a method call, which is why the
diagnostics socket's `size` verb can resize a headless app and returns an explicit error against
a real one rather than silently doing nothing.

The driver rebuilds its console buffer **before** raising `ScreenResized`. That ordering is a
contract, not an implementation detail: `ConsoleWindowSystem.HandleScreenResize` relies on it,
and a driver that raises the event first reports the new size while frames still go to the old
buffer.

### Driving it

Headless has no loop of its own, so the host owns the loop:

```csharp
using var session = windowSystem.BeginHosted();
while (session.Tick())
{
    // Your own work here — a test step, a socket poll, a frame budget.
}
```

`BeginHosted()` is `Run()` without the loop — the same startup, the same per-frame work. Use it
rather than reaching for lower-level entry points: it wires the key, paste and resize handlers,
and captures the calling thread as the UI thread. Disposing the session tears down whatever it
established, even if the loop throws.

Set `BlockWhenIdle = false` if your host must not block inside `Tick()` — a browser frame
callback, a game loop — and pace the calls yourself.

> `MockConsoleDriver` in the test project subclasses `HeadlessConsoleDriver`. It is test
> infrastructure, not part of the shipped package.

---

## IConsoleDriver

The contract, if you are writing your own driver — a different terminal backend, a GUI surface
hosting a console, a WebAssembly canvas.

What you must provide:

| Group | Members |
|---|---|
| Lifecycle | `Initialize(windowSystem)`, `Start()`, `Stop()` |
| Screen | `ScreenSize`, `Clear()`, `Flush()` |
| Writing | `SetNarrowCell(...)`, `FillCells(...)`, `WriteBufferRegion(...)` |
| Cursor | `SetCursorPosition`, `SetCursorVisible`, `SetCursorShape`, `ResetCursorShape` |
| Events | `KeyPressed`, `Paste`, `MouseEvent`, `ScreenResized` |

Things that are easy to get wrong:

- **`ScreenResized` fires on a background thread**, before the framework repositions windows.
  A subscriber has to marshal to the UI thread itself. Application code should listen to
  `ConsoleWindowSystem.WindowResized` instead, which fires on the UI thread after reposition
  and re-invalidates automatically.
- **Rebuild your buffer before raising `ScreenResized`**, as described above.
- **`Paste` must be delivered atomically** — a pasted block arrives as one event, not as a
  storm of synthetic keystrokes, so an editor can tell typing from pasting.
- **`WriteBufferRegion` must preserve cell flags.** `IsWideContinuation`, combining marks and
  decorations all travel with the cell; dropping them corrupts wide characters and CJK text.
- **`InvalidateFrontBuffer()` has a default no-op implementation**, so a driver with no front
  buffer need not implement it.

Optionally implement `IGraphicsProtocol` for inline images.

---

## Choosing

| Situation | Use |
|---|---|
| An application | `NetConsoleDriver` with `RenderMode.Buffer` |
| Paint-once, exit; or debugging the render path | `RenderMode.Direct` |
| Tests, CI, golden images, remote-driven sessions | `HeadlessConsoleDriver` |
| A new backend (GUI surface, WASM canvas, other terminal) | Implement `IConsoleDriver` |

---

## See also

- [`RENDERING_PIPELINE.md`](RENDERING_PIPELINE.md) — what happens before the driver sees a cell
- [`DEBUGGING.md`](DEBUGGING.md) — the diagnostics socket, including driving a headless app
- [`EXPORT.md`](EXPORT.md) — exporting a window or the desktop as an image or text
- [`THREADING_AND_ASYNC.md`](THREADING_AND_ASYNC.md) — which thread may touch what
- [`IMAGE_RENDERING.md`](IMAGE_RENDERING.md) — `IGraphicsProtocol` and Kitty graphics
