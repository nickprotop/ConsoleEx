# Why SharpConsoleUI

A terminal application framework for .NET. Not a library for printing pretty
output — a framework for applications that happen to run in a terminal.

The difference shows up in the architecture. Every window owns a cell buffer.
A compositor merges them with occlusion culling and per-cell alpha. A
Measure→Arrange→Paint pipeline lays out a tree of controls. One UI thread drives
the loop, and any window can opt into a background loop of its own. If that
sounds like a desktop UI framework, that is the point.

## It is a multi-window system. Full screen is one window.

That sentence is the whole design. There is no full-screen mode and no
single-window mode to opt into — there is a window manager, and what you build
depends only on how many windows you open and whether you give them chrome.

**One borderless window** is a full-screen application:

```csharp
new WindowBuilder(system).Maximized().Borderless().HideTitle()
```

Nothing is switched off to get there. The compositor still runs, the layout
still docks to the edges, dialogs still open over the top. You simply did not
open a second window. Eight of the ten applications in the cx/lazy family are
this: a dashboard, a monitor, an editor, a client.

**Several windows with chrome** is a desktop. `cxshell` is exactly that — start
menu, taskbar, app manager, other TUI applications running live inside its
windows.

**Anything between the two** needs no decision in advance. A full-screen editor
that later wants a log viewer you can drag aside, or a settings panel you can
leave open while you work, just opens one. Not a modal dialog — a real window,
because there was never a mode preventing it.

This is why the choice stays yours rather than the framework's: you are never
porting an application from one shape to another, only opening or closing
windows. Transparency, overlap and z-order are properties of the system, not
of a mode you picked at the start, so a full-screen application that later
wants a floating pane does not have to be rewritten to get one.

It is also why you can afford it. Rendering goes to a cell buffer, and only the
cells that actually changed are written out — so a gradient under three
overlapping translucent windows costs whatever moved in that frame, not the
size of the screen. A cursor blinking in the corner writes one cell. The
framework measures this about itself: `cxdbg metrics` reports bytes written,
characters changed and cells rendered per frame, so "is it the rendering?" is a
question with an answer rather than a guess.

That economy is the whole reason the effects are usable at all. Alpha blending,
gradients, blur and fade are not expensive here because none of them cost
anything until a cell changes, and the diff is what decides that — not the
control, and not you.

## How the rendering works

Terminal frameworks differ most in how a tree of controls becomes bytes on a
wire. There are four broad approaches:

| Approach | What it does |
|---|---|
| **String output** | A view returns a string; the framework diffs strings |
| **Immediate mode** | Redraw the whole UI into one buffer each frame |
| **Retained, shared buffer** | A widget tree paints into one buffer with dirty tracking |
| **Retained, per-window buffers** | Each window has its own buffer; a compositor merges them |

Most take one of the first three. SharpConsoleUI takes the fourth — the pattern
desktop window managers use, adapted for character cells.

```
Per-window buffers     Compositor            Console driver
┌──────────┐
│ Window A │──┐
└──────────┘  │   ┌──────────────────┐   ┌──────────────────┐
┌──────────┐  ├──►│ Visible regions  │──►│ Diff-based flush │──► stdout
│ Window B │──┤   │ (skip occluded)  │   │ (changed cells)  │
└──────────┘  │   └──────────────────┘   └──────────────────┘
┌──────────┐  │
│ Overlay  │──┘
└──────────┘
```

The visible-regions step uses rectangle subtraction to work out which cells of
each window are actually on screen. Occluded regions are skipped rather than
painted and overwritten. Each window's render also exposes hooks:
`PreBufferPaint` runs before its controls (gradients, custom backgrounds),
then the controls paint, then `PostBufferPaint` runs (blur, fade, anything
else).

### Animations are frame-coupled

The animation system runs inside the render loop, not on timers:

1. Poll input
2. Advance animations by the frame's delta
3. Measure, arrange and paint whatever changed
4. Composite and flush the diff

A tween cannot tear, because it cannot advance between a measure and a paint.
A timer-driven animation in a shared-buffer framework can, and the usual fix is
a lock; here the ordering makes the lock unnecessary.

### The layout pipeline

Measure→Arrange→Paint, the same three-pass shape WPF and WinUI use. A control
is asked how big it wants to be, told how big it actually is, and then painted
into that rectangle.

What that buys concretely:

- **`GridControl`** — a real 2D grid. Star, Auto and Fixed tracks with
  per-track min and max, row and column spans, gaps, per-cell border,
  background and padding, and interactive splitters you can drag with the mouse
  or resize from the keyboard.
- **Nesting that keeps working** — a grid inside a scrollable panel inside a
  column container, with focus, cursor, keyboard and mouse routing intact at
  every level. This is the part that is hard, and the part a layout system is
  worth having for.
- **Sticky docking** — Top and Bottom at the window root, reserving space
  rather than overlaying it.

There is no flex allocator. Star tracks plus nesting reach most of the same
outcomes, but if you want per-control grow/shrink with min and max, that is a
real gap and
[XenoAtom.Terminal.UI](https://github.com/xoofx/XenoAtom.Terminal.UI) has it.

## What else it is good at

**Applications that show things.** Charts with two axes, tables backed by
virtual data sources, trees, an HTML browser that renders real web pages with
images, a terminal emulator hosting another terminal application, video. The
compositor is what makes these possible: a control that draws pixels is just a
control that fills cells.

**Applications that stay responsive.** One UI thread runs the loop, and any window
can opt into a background async loop of its own,
with marshalling back to the UI thread handled for you. A
panel waiting on a slow query does not freeze the panel next to it.

## What the code looks like

A window with a list in it, updating while other windows carry on:

```csharp
var window = new WindowBuilder(system)
    .WithTitle("Languages")
    .WithSize(40, 12)
    .Centered()
    .AddControl(Controls.List()
        .AddItems("C#", "F#", "VB.NET")
        .OnSelectedItemChanged((w, item) => Log(item?.Text))
        .Build())
    .BuildAndShow();
```

Nothing blocks. `OnSelectedItemChanged` fires on the UI thread; the rest of the
application keeps rendering. A second window opened now would overlap this one
and composite over it.

Live data, on the window's own thread:

```csharp
var chart = Controls.Chart().WithTitle("CPU").Build();
var load = chart.AddSeries("load");

new WindowBuilder(system)
    .WithTitle("CPU")
    .AddControl(chart)
    .WithAsyncWindowThread(async (w, ct) =>
    {
        while (!ct.IsCancellationRequested)
        {
            load.SetValues(await ReadCpuHistoryAsync());
            await Task.Delay(1000, ct);
        }
    })
    .BuildAndShow();
```

That loop belongs to the window. A slow read here does not stall the window
next to it, and the marshalling back to the UI thread is handled.

## Forty-seven controls

The ones worth knowing about:

| | |
|---|---|
| **TableControl** | A real data grid: in-cell editing, multi-state sort, an AND/OR filter grammar, and `ITableDataSource` so a million rows cost what is on screen |
| **TreeTableControl** | Every table feature, tree-aware |
| **ChartControl** | Two axes, several series, gradients by value, heatmaps |
| **MultilineEditControl** | TextMate grammars for ~64 languages, pluggable gutters, find/replace, undo/redo |
| **HtmlControl** | AngleSharp parses real pages; images render as half-block pixels |
| **TerminalControl** | A PTY, with a real shell in it, inside a window |
| **VideoControl** | FFmpeg decode, Kitty graphics where supported, cell fallbacks where not |
| **NavigationView** | A WinUI-style nav pane that collapses responsively |
| **GridControl** | The 2D grid described above |
| **CanvasControl** | Free-form drawing in cells, with a half-block pixel surface on top at double vertical resolution - and text composes into the same surface |

Plus the ordinary ones — buttons, checkboxes, radios, sliders, dropdowns, date
and time pickers, prompts, menus, toolbars, status bars, forms, panels, tabs,
splitters, progress bars, spinners, sparklines. Each has
[a page of its own](CONTROLS.md).

## What it is not

**It is not the thing for pretty CLI output.** If you want a progress bar, a
table and some color in a command-line tool, use
[Spectre.Console](https://spectreconsole.net). It is purpose-built for that and
does it better. SharpConsoleUI understands Spectre's markup syntax and can host
any `IRenderable`, so the two compose rather than compete.

**It is not the oldest or the largest.** [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui)
has been at this for years, has the widest mature control set, and has a
community behind it. If you need StackOverflow answers at two in the morning,
that matters.

**It does not have everything.** No color picker, no hex viewer. Reactive
binding is lambda-based `Bind()`/`BindTwoWay()` over `INotifyPropertyChanged`,
not source-generated — [XenoAtom.Terminal.UI](https://github.com/xoofx/XenoAtom.Terminal.UI)
does that more thoroughly, on .NET 10.

**It needs .NET 8 or newer.** Nothing older.

**It is small.** One maintainer, a handful of contributors, and a NuGet package
that a growing number of projects depend on. That is a real risk and worth
weighing. What you get in exchange is a framework that moves quickly and
answers its own bug reports.

## What it does that the others do not

Three things, honestly:

**A compositor, and a cheap one.** Per-cell Porter-Duff alpha, occlusion
culling, gradient backgrounds that propagate through transparent controls, and
hooks for blur and fade — all of it behind a diff that writes only the cells
that changed. Nothing else in the .NET terminal space composites, and the
reason it is practical here is that the expensive-sounding part is free until
something moves.

**Markup everywhere.** Twenty-nine controls parse inline styling — color,
weight, gradients, clickable links, embedded Markdown. Not a dedicated
rich-text widget; a property of every control that shows text.

**A debugger.** A running application answers questions over a Unix socket:
what the screen shows, what the layout engine believes, *why a control is the
size it is*. Then it takes real keys and clicks through its own dispatch.
`dotnet tool install -g cxdbg`. The same application runs headless with no
terminal at all, which is what makes it scriptable in CI and renderable to a
browser.

## Who uses it

The applications built on it are listed in the
[README](https://github.com/nickprotop/ConsoleEx#built-with-sharpconsoleui) —
ten in the cx/lazy family, which are ours, and third-party projects including a
database client, an event-store workbench, a 6502 assembler, a MU\* client and
an AI skill catalog.

They have one thing in common: every one is a serious stateful application, not
a tool that prints a menu and exits. That is the niche, and it chose itself.

## Where to start

- [Tutorials](tutorials/README.md) — an empty project to a running application
- [Controls](CONTROLS.md) — forty-seven of them, with a page each
- [Debugging](DEBUGGING.md) — the socket protocol, and cxdbg
