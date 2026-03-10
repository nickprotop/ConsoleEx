# Choosing a .NET Console UI Library

The .NET ecosystem has several libraries for building console applications. Each solves a different problem. This guide helps you pick the right one.

## Four Libraries, Four Jobs

| Library | What it is | Think of it as... |
|---------|-----------|-------------------|
| **[Spectre.Console](https://github.com/spectreconsole/spectre.console)** | Rich console output & formatting | A **printer** -- beautiful output, scrolls away |
| **[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui)** | Forms-based TUI toolkit | A **dialog box** -- single-screen forms and views |
| **[XenoAtom.Terminal.UI](https://github.com/XenoAtom/XenoAtom.Terminal.UI)** | Reactive UI framework | A **WPF for the terminal** -- reactive bindings, alpha blending |
| **[SharpConsoleUI](https://github.com/nickprotop/ConsoleEx)** | Windowed console UI system | A **desktop** -- overlapping windows with a compositor engine |

They're complementary, not always competing. SharpConsoleUI can host Spectre.Console renderables inside its windows via `SpectreRenderableControl`.

## Quick Decision Guide

| I need to... | Spectre.Console | Terminal.Gui | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|:---:|:---:|:---:|:---:|
| Pretty-print tables, trees, charts | **Best choice** | -- | -- | Via Spectre wrapper |
| Build a CLI tool with prompts | **Best choice** | -- | Yes (inline mode) | -- |
| Build a single-screen forms app | -- | **Best choice** | **Best choice** | Works |
| Build a multi-window app with overlapping windows | No | v2 beta only | No | **Best choice** |
| Drag, resize, minimize, maximize windows | No | v2 beta only | No | **Built-in** |
| Embed a working terminal emulator | No | No | No | **Built-in** |
| Run independent async tasks per window | No | No | No | **Built-in** |
| Apply visual effects (blur, fade, gradients) | No | No | Alpha blending only | **Built-in compositor** |
| Use Spectre.Console markup in controls | No (output only) | No | No (own markup) | **Yes, everywhere** |
| Use it in production today on .NET 9 | Yes (v0.54) | v1 stable, v2 in beta | No (.NET 10 only) | Yes (v2.4) |

## Detailed Comparison

### Rendering Architecture

This is the most fundamental difference between the four libraries. It determines what kinds of visual effects and compositing are possible.

| | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **Buffer model** | No buffer (stream to stdout) | Single shared buffer | Single CellBuffer | **Per-window CharacterBuffer** |
| **Compositing** | N/A | Painter's algorithm (back-to-front into shared buffer) | Single buffer + diff | **True compositor (merge per-window buffers)** |
| **Compositor hooks** | No | No | No | **Yes (PreBufferPaint / PostBufferPaint)** |
| **Occlusion culling** | N/A | No (hidden content still painted) | Clip-skip only | **Yes (hidden regions skipped entirely)** |
| **Double buffering** | No | Single back buffer | Single buffer + diff | **Per-window double buffering** |
| **Dirty tracking** | No | Cell-level | Visual-level invalidation | **Region-level (3-level dirty tracking)** |
| **Flicker prevention** | Cursor repositioning (can flicker) | ANSI driver diff | Synchronized output (DEC 2026) | **Occlusion culling + diff-based flush** |
| **Frame management** | N/A | Event-driven redraw | Event-driven | ~60 FPS with dirty-check skip |
| **RGBA alpha blending** | No | No | **Yes (sRGB-linear LUT)** | No |

**What this means in practice:**

- **Spectre.Console** writes formatted text to stdout. No screen buffer, no compositing, no concurrent updates.
- **Terminal.Gui** paints all views into one shared buffer using a back-to-front painter's algorithm. If window A is fully behind window B, window A is still fully rendered.
- **XenoAtom.Terminal.UI** uses a single cell buffer with reactive invalidation and frame-to-frame diffing. Has the most sophisticated color system with true RGBA alpha blending in linear color space. But no per-window buffers or compositor pipeline.
- **SharpConsoleUI** gives each window its own character buffer, then composites them together. The **PreBufferPaint** hook fires before controls paint (for backgrounds, gradients, game rendering), controls render on top, then **PostBufferPaint** fires for effects (blur, fade, glow, transitions). This pipeline is unique -- no other .NET TUI framework exposes it.

### Gradient Backgrounds

SharpConsoleUI supports multi-stop color gradients on windows that flow through the entire control hierarchy:

```csharp
new WindowBuilder(system)
    .WithBackgroundGradient(
        ColorGradient.FromColors(Color.Navy, Color.DarkCyan, Color.Teal),
        GradientDirection.Vertical)
    .BuildAndShow();
```

Controls with transparent backgrounds blend naturally with the gradient behind them. Controls with explicit backgrounds punch through. Gradients support four directions (horizontal, vertical, diagonal down, diagonal up) and can be defined with named presets (`"warm"`, `"cool"`, `"spectrum"`) or arrow notation (`"red→yellow→green"`).

Gradient text is also supported in markup: `[gradient=spectrum]Rainbow Text[/]`

Neither Terminal.Gui nor XenoAtom.Terminal.UI offer window-level gradients that propagate through nested containers and controls.

### Input & Interactivity

| | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **Keyboard** | Blocking `ReadKey` only | Full event-driven | Full event-driven + routed | Full event-driven |
| **Mouse** | None | Click, drag, wheel | Click, drag, wheel, hover | Click, drag, wheel, double-click, hover |
| **Focus system** | None (one prompt at a time) | Tab-based focus chain | Tab-based + routed focus | Window + control focus with Alt+1-9 cycling |
| **Modal dialogs** | Sequential prompts | Dialog-based | Dialog + Backdrop | True modal stack with input blocking |
| **Concurrent interactive controls** | No | Yes | Yes | Yes, across multiple windows |

### Controls

| Control type | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| Button | -- | Yes | Yes | Yes |
| Text input | Prompt (blocking) | TextField, TextView | TextBox, TextArea | PromptControl, MultilineEditControl |
| List / selection | SelectionPrompt (blocking) | ListView | ListBox, SelectionList | ListControl (live, searchable) |
| Tree view | Tree (static output) | TreeView&lt;T&gt; | TreeView | TreeControl |
| Data grid / table | Table (static output) | TableView | DataGridControl | **TableControl (virtual, sort, filter, edit)** |
| Tabs | -- | TabView | TabControl | TabControl |
| Menu bar | -- | MenuBar | MenuBar + CommandPalette | MenuControl with portals |
| Dropdown | -- | ComboBox | Select&lt;T&gt; | DropdownControl with smart positioning |
| Checkbox | -- | CheckBox | CheckBox, Switch | CheckboxControl |
| Progress bar | ProgressBar (excellent) | ProgressBar | ProgressBar + task groups | **ProgressBarControl** |
| Canvas / drawing | Canvas (basic) | LineCanvas | Canvas | **CanvasControl (30+ primitives)** |
| Terminal emulator | -- | -- | -- | **TerminalControl (PTY)** |
| Sparklines | -- | -- | Sparkline | **SparklineControl (4 render modes)** |
| Bar graphs | BarChart (static) | -- | BarChart, LineChart, Breakdown | **BarGraphControl (live, gradient)** |
| FIGlet text | FigletText | -- | TextFiglet (25 fonts) | **FigleControl (with wrapping)** |
| Log viewer | -- | -- | LogControl | **LogViewerControl** |
| Splitter | -- | TileView | HSplitter, VSplitter | SplitterControl |
| Image rendering | -- | -- | -- | **ImageControl** |
| Toolbar | -- | -- | CommandBar | **ToolbarControl** |
| Date picker | -- | DatePicker, DateEditor | -- | -- |
| Slider | -- | LinearRange | Slider | -- |
| Hex viewer | -- | HexView | -- | -- |
| Graph view | -- | GraphView | LineChart, BreakdownChart | -- |
| Color picker | -- | ColorPicker (RGB/HSL) | ColorPicker | -- |
| Radio button | -- | OptionSelector | RadioButtonList | -- |
| Accordion | -- | -- | Accordion, Collapsible | -- |
| Markdown | -- | -- | MarkdownControl (Markdig) | -- |
| Wizard / stepper | -- | Wizard | -- | -- |
| Toast notifications | -- | -- | ToastService | **NotificationSystem** |

**Honest take:** Terminal.Gui has the most mature and widest control library -- battle-tested over years, with specialized widgets like DatePicker, ColorPicker, Slider, HexView, and Wizard. XenoAtom.Terminal.UI ships the most controls overall (100+) but is only 2 months old. SharpConsoleUI has fewer controls, but its unique strengths are interactive/live controls (TerminalControl, SparklineControl, BarGraphControl, CanvasControl with 30+ drawing primitives) and the window management + compositor layer.

### Window Management

This is SharpConsoleUI's primary differentiator.

| Feature | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|:---:|:---:|:---:|:---:|
| Multiple overlapping windows | -- | Beta | Popups only (WindowLayer) | **Yes** |
| Window Z-order management | -- | Basic | Z-stack for overlays | **Multi-pass (Normal/Active/AlwaysOnTop)** |
| Drag to move | -- | Beta | Popup drag only | **Yes** |
| Drag to resize (8 directions) | -- | Beta | No | **Yes, configurable per-direction** |
| Minimize / Maximize | -- | -- | No | **Yes** |
| Close button | -- | -- | No | **Yes** |
| Window state events | -- | -- | No | **Yes (StateChanged, Closing with cancel)** |
| Independent async window threads | -- | -- | No | **Yes** |
| Modal window stack | -- | Basic | Dialog + Backdrop | **Yes, with input blocking** |
| Always-on-top windows | -- | -- | No | **Yes** |
| Per-window compositor hooks | -- | -- | No | **Yes (PreBufferPaint, PostBufferPaint)** |
| Window templates | -- | -- | No | **Yes (DialogTemplate, ToolWindowTemplate)** |
| Taskbar | -- | -- | No | **Yes (Alt+1-9 switching)** |

XenoAtom.Terminal.UI has a `WindowLayer` that supports z-ordered overlays with bring-to-front and click-to-focus, plus `Popup` and `Dialog` controls. But there is no full window manager with title bars, minimize/maximize/close chrome, or arbitrary resizing.

### Rich Text Markup Everywhere

SharpConsoleUI uses a Spectre-compatible markup syntax (`[red bold]text[/]`) that works **on virtually every control** -- not just in output rendering:

```csharp
// Checkbox with colored label
var checkbox = Controls.Checkbox()
    .WithLabel("[red]C[/]heck[yellow]B[/]ox")
    .Build();

// List with rich items
var list = Controls.List()
    .WithItems("[green]Online[/] Server 1", "[red]Offline[/] Server 2")
    .Build();

// Button with styled text
var button = Controls.Button()
    .WithText("[bold]Save[/] changes")
    .Build();
```

| Control | Markup in text/labels |
|---|:---:|
| Buttons, Checkboxes | Yes |
| List items, Tree nodes | Yes |
| Tab headers, Dropdowns | Yes |
| Prompts, Panels, Rules | Yes |
| Bar graph labels | Yes |
| MarkupControl | Yes (entire content) |
| Status bars | Yes |

**How this compares:**

- **Spectre.Console** has excellent markup -- but only for static output that scrolls away. You can't use it in live, interactive controls.
- **Terminal.Gui** has no markup system. Styling requires working with `ColorScheme` objects and attribute-based formatting. v2 adds `VisualRole` semantic styling, but no inline markup syntax.
- **XenoAtom.Terminal.UI** has its own markup and `Brush` system (including gradient brushes for text), but it's a separate syntax incompatible with Spectre.Console. No `IRenderable` bridge.
- **SharpConsoleUI** parses Spectre-compatible markup directly into cells (no ANSI roundtrip) and supports any Spectre `IRenderable` (Tables, Charts, BarCharts) as a control via `SpectreRenderableControl`. This extends Spectre.Console rather than replacing it.

### Layout System

| | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **Approach** | Measure/Render | Pos/Dim with arithmetic | Flexbox + Grid + Dock | DOM pipeline (Measure/Arrange/Paint) |
| **Containers** | Layout (grid cells) | View nesting | VStack, HStack, WrapStack, Grid, DockLayout, ZStack, Splitter, ScrollViewer | HorizontalGrid, ColumnContainer, ScrollablePanel |
| **Reactive layout** | No | Computed Pos/Dim | **[Bindable] source-generated with dependency tracking** | Event-driven invalidation |
| **Flexibility** | Low | High (Pos/Dim arithmetic) | **Very high (WPF/Avalonia-level)** | Moderate |

XenoAtom.Terminal.UI has the most sophisticated layout system with a proper `FlexAllocator` (grow/shrink/min/max like CSS Flexbox), full Grid with row/column definitions, and DockLayout. Terminal.Gui's Pos/Dim system with arithmetic composition is also very powerful. SharpConsoleUI's layout is simpler -- it prioritizes the compositor pipeline over layout complexity.

### Developer Experience

| | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **API style** | Fluent builders | Property assignment | Property assignment + reactive bindings | **Fluent builders** |
| **Data binding** | None | Manual / events | **Reactive [Bindable] with auto-invalidation** | Manual / events |
| **Async support** | Limited (Progress) | Event-driven | Dispatcher-based | **Full async/await, per-window threads** |
| **Plugin architecture** | No | No | No | **Yes (themes, controls, windows, services)** |
| **Theming** | No built-in theming | JSON-based Scheme + VisualRole | Theme + 73 per-control style files | **Theme registry with runtime switching** |
| **Markup syntax** | `[red bold]text[/]` | No markup | Own markup + Brush/Gradient | `[red bold]text[/]` (Spectre-compatible) |
| **Testing support** | Spectre.Console.Testing | Input injection (v2) | Screenshot/snapshot testing | HeadlessConsoleDriver |
| **Logging** | N/A | Microsoft.Extensions.Logging | N/A | **Built-in LogService (file-based, never console)** |

### Project Health (March 2026)

| | Spectre.Console | Terminal.Gui | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **Stars** | ~11,260 | ~10,830 | ~140 | 114 |
| **NuGet downloads** | ~9.9M | ~1.6M | New | 6,504 |
| **Contributors** | ~115 | 199 | 1 | 1 |
| **Latest stable** | 0.54.0 (pre-1.0) | 1.19.0 (v1 only) | 1.4.0 | 2.4.40 |
| **v2 / latest** | 0.54.0 | v2 beta.1 (March 2026) | 1.4.0 | 2.4.40 |
| **.NET version** | .NET Standard 2.0+ | .NET 10 (v2 beta) | .NET 10 only | .NET 9.0 |
| **License** | MIT | MIT | BSD-2-Clause | MIT |
| **Repo age** | ~5 years | ~7 years | ~2 months | ~1 year |

**Honest take:** Terminal.Gui and Spectre.Console have large communities and years of battle-testing. XenoAtom.Terminal.UI is brand new (January 2026) but built by Alexandre Mutel, a highly respected .NET developer (Markdig, SharpDX, Scriban, former .NET Foundation TSG member). SharpConsoleUI is solo-maintained. If you need maximum community support, StackOverflow answers, and long-term maintenance guarantees, the larger libraries are safer bets. SharpConsoleUI and XenoAtom compensate with rapid iteration and features the others don't offer.

## When NOT to Use SharpConsoleUI

Be honest about the right tool:

- **Just need pretty CLI output?** Use **Spectre.Console**. It's purpose-built for that and does it better than anything else in .NET.
- **Building a simple single-screen form?** **Terminal.Gui** has the widest mature control library. **XenoAtom.Terminal.UI** has the most modern architecture with reactive bindings, but requires .NET 10.
- **Need maximum community and ecosystem?** The bigger libraries have more users, more contributors, more blog posts, and more StackOverflow answers.
- **Targeting .NET 9 or older?** SharpConsoleUI requires .NET 9. Spectre.Console and Terminal.Gui v1 support .NET Standard 2.0. XenoAtom requires .NET 10.
- **Need DatePicker, Slider, ColorPicker, or HexView?** Terminal.Gui has these built-in. XenoAtom has Slider and ColorPicker. SharpConsoleUI doesn't (yet).
- **Need reactive data binding?** XenoAtom's `[Bindable]` source-generated property system with automatic dependency tracking is significantly more sophisticated than manual events.

## When SharpConsoleUI Shines

SharpConsoleUI is the right choice when you need:

- **Multi-window desktop-style apps** -- overlapping windows with drag, resize, minimize, maximize, taskbar. No other .NET library does this in a stable release.
- **Visual effects and compositing** -- gradient backgrounds, blur, fade transitions, custom buffer effects via PreBufferPaint/PostBufferPaint. The compositor pipeline is unique in the .NET TUI space.
- **Dashboard / monitoring tools** -- independent async window threads mean each panel updates on its own schedule without blocking the UI.
- **IDE-like tools** -- [LazyDotIDE](https://github.com/nickprotop/LazyDotIDE) is a working .NET IDE built entirely on SharpConsoleUI, proving the framework handles complex, multi-window applications.
- **Embedded terminal + UI** -- TerminalControl gives you a real PTY-backed terminal emulator inside a window, alongside your UI controls. Unique in the .NET ecosystem.
- **Spectre.Console integration** -- use `[red bold]markup[/]` in every control, and embed any `IRenderable` (Tables, Charts, BarCharts) as a windowed control. Extends Spectre.Console rather than replacing it.
- **Live data visualization** -- SparklineControl and BarGraphControl update in real-time with configurable styles.
- **Plugin-based architectures** -- extend the framework with custom themes, controls, windows, and services.

## Code Comparison

### Creating a selectable list

**Spectre.Console** (blocking -- halts your program until user selects):
```csharp
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Pick a language:")
        .AddChoices("C#", "F#", "VB.NET"));
// Nothing else can happen on screen while this runs
```

**Terminal.Gui** (single-screen):
```csharp
var list = new ListView(new[] { "C#", "F#", "VB.NET" })
{
    X = 0, Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill()
};
win.Add(list);
```

**SharpConsoleUI** (inside a managed window, alongside other windows):
```csharp
var list = Controls.List()
    .WithItems("C#", "F#", "VB.NET")
    .OnSelected((window, args) => { /* handle selection */ })
    .Build();
window.AddControl(list);
// Other windows continue updating independently
```

### Creating a live-updating dashboard

**Spectre.Console** -- possible with `Live`, but only one live region, no windowing:
```csharp
await AnsiConsole.Live(table).StartAsync(async ctx =>
{
    while (true)
    {
        UpdateTable(table);
        ctx.Refresh();
        await Task.Delay(1000);
    }
});
// Nothing else can be on screen
```

**SharpConsoleUI** -- multiple independent windows, each with its own update loop:
```csharp
var cpuWindow = new WindowBuilder(system)
    .WithTitle("CPU Monitor")
    .WithSize(40, 12)
    .WithAsyncWindowThread(async (window, token) =>
    {
        while (!token.IsCancellationRequested)
        {
            sparkline.AddValue(GetCpuUsage());
            await Task.Delay(500, token);
        }
    })
    .Build();

var logWindow = new WindowBuilder(system)
    .WithTitle("Live Logs")
    .WithSize(60, 20)
    .WithAsyncWindowThread(async (window, token) =>
    {
        await foreach (var entry in logStream.ReadAllAsync(token))
            logViewer.AddEntry(entry);
    })
    .Build();

// Both windows run simultaneously, user can drag/resize/switch between them
```

## Compatibility with Spectre.Console

SharpConsoleUI doesn't replace Spectre.Console -- it can host it. Use `SpectreRenderableControl` to embed any Spectre.Console renderable inside a SharpConsoleUI window:

```csharp
var table = new Table()
    .AddColumn("Name")
    .AddColumn("Status")
    .AddRow("Server 1", "[green]Online[/]")
    .AddRow("Server 2", "[red]Offline[/]");

window.AddControl(new SpectreRenderableControl(table));
// Spectre's beautiful table, inside a draggable, resizable window
```

This gives you Spectre's polished formatting with SharpConsoleUI's window management -- the best of both worlds.

---

*Last updated: March 2026*
