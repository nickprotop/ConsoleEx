# Choosing a .NET Console UI Library

The .NET ecosystem has three main libraries for building console applications. Each solves a different problem. This guide helps you pick the right one.

## Three Libraries, Three Jobs

| Library | What it is | Think of it as... |
|---------|-----------|-------------------|
| **[Spectre.Console](https://github.com/spectreconsole/spectre.console)** | Rich console output &amp; formatting | A **printer** -- beautiful output, scrolls away |
| **[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui)** | Forms-based TUI toolkit | A **dialog box** -- single-screen forms and views |
| **[SharpConsoleUI](https://github.com/nickprotop/ConsoleEx)** | Windowed console UI system | A **desktop** -- overlapping windows, like Windows/macOS in your terminal |

They're complementary, not always competing. In fact, SharpConsoleUI can host Spectre.Console renderables inside its windows via `SpectreRenderableControl`.

## Quick Decision Guide

| I need to... | Spectre.Console | Terminal.Gui | SharpConsoleUI |
|---|:---:|:---:|:---:|
| Pretty-print tables, trees, charts | **Best choice** | -- | Via Spectre wrapper |
| Build a CLI tool with prompts | **Best choice** | -- | -- |
| Build a single-screen forms app | -- | **Best choice** | Works |
| Build a multi-window app with overlapping windows | No | v2 beta only | **Best choice** |
| Drag, resize, minimize, maximize windows | No | v2 beta only | **Built-in** |
| Embed a working terminal emulator | No | No | **Built-in** |
| Run independent async tasks per window | No | No | **Built-in** |
| Use it in production today on .NET 9 | Yes (v0.54) | v1 stable, v2 in beta | Yes (v2.4) |

## Detailed Comparison

### Architecture

| | Spectre.Console | Terminal.Gui v2 | SharpConsoleUI |
|---|---|---|---|
| **Rendering model** | Sequential output, no screen buffer | Single-pass ANSI rendering | Double-buffered with 3-level dirty tracking |
| **Screen mode** | Scrolling terminal (no alternate screen) | Full-screen alternate screen | Full-screen alternate screen |
| **Window support** | None | Overlapped views (beta) | Full window manager with Z-order |
| **Flicker prevention** | Cursor repositioning (can still flicker) | ANSI driver rewrite (improving) | Occlusion culling + diff-based flush |
| **Frame management** | N/A | Event-driven redraw | ~60 FPS with dirty-check skip |

### Input & Interactivity

| | Spectre.Console | Terminal.Gui v2 | SharpConsoleUI |
|---|---|---|---|
| **Keyboard** | Blocking `ReadKey` only | Full event-driven | Full event-driven |
| **Mouse** | None | Click, drag, wheel | Click, drag, wheel, double-click, hover |
| **Focus system** | None (one prompt at a time) | Tab-based focus chain | Window + control focus with Alt+1-9 cycling |
| **Modal dialogs** | Sequential prompts | Dialog-based | True modal stack with input blocking |
| **Concurrent interactive controls** | No | Yes | Yes, across multiple windows |

### Controls

| Control type | Spectre.Console | Terminal.Gui v2 | SharpConsoleUI |
|---|---|---|---|
| Button | -- | Yes | Yes |
| Text input | Prompt (blocking) | TextField, TextView | PromptControl, MultilineEditControl |
| List / selection | SelectionPrompt (blocking) | ListView | ListControl (live, searchable) |
| Tree view | Tree (static output) | TreeView&lt;T&gt; | TreeControl |
| Table | Table (static output) | TableView | Via SpectreRenderableControl |
| Tabs | -- | TabView | TabControl |
| Menu bar | -- | MenuBar | MenuControl with portals |
| Dropdown | -- | ComboBox | DropdownControl with smart positioning |
| Checkbox | -- | CheckBox | CheckboxControl |
| Progress bar | ProgressBar (excellent) | ProgressBar | Via Spectre wrapper |
| Canvas / drawing | Canvas (basic) | LineCanvas | CanvasControl (30+ primitives) |
| Terminal emulator | -- | -- | **TerminalControl (PTY)** |
| Sparklines | -- | -- | **SparklineControl** |
| Bar graphs | BarChart (static) | -- | **BarGraphControl (live)** |
| FIGlet text | FigletText | -- | **FigleControl (with wrapping)** |
| Log viewer | -- | -- | **LogViewerControl** |
| Splitter | -- | TileView | SplitterControl |
| Date picker | -- | DatePicker | -- |
| Slider | -- | Slider | -- |
| Hex viewer | -- | HexView | -- |
| Graph view | -- | GraphView | -- |
| Color picker | -- | ColorPicker | -- |

**Honest take:** Terminal.Gui has the widest control library -- especially specialized widgets like DatePicker, Slider, and HexView. Spectre.Console has the most polished static output widgets. SharpConsoleUI's unique strengths are interactive/live controls (TerminalControl, SparklineControl, BarGraphControl) and the window management layer.

### Window Management

This is SharpConsoleUI's primary differentiator.

| Feature | Spectre.Console | Terminal.Gui v2 | SharpConsoleUI |
|---|:---:|:---:|:---:|
| Multiple overlapping windows | -- | Beta | **Yes** |
| Window Z-order management | -- | Basic | **Multi-pass (Normal/Active/AlwaysOnTop)** |
| Drag to move | -- | Beta | **Yes** |
| Drag to resize (8 directions) | -- | Beta | **Yes, configurable per-direction** |
| Minimize / Maximize | -- | -- | **Yes** |
| Close button | -- | -- | **Yes** |
| Window state events | -- | -- | **Yes (StateChanged, Closing with cancel)** |
| Independent async window threads | -- | -- | **Yes** |
| Modal window stack | -- | Basic | **Yes, with input blocking** |
| Always-on-top windows | -- | -- | **Yes** |
| Per-window compositor hooks | -- | -- | **Yes (PreBufferPaint, PostBufferPaint)** |
| Window templates | -- | -- | **Yes (DialogTemplate, ToolWindowTemplate)** |

### Rich Text Markup Everywhere

SharpConsoleUI uses a Spectre-compatible markup syntax (`[red bold]text[/]`) -- but unlike Spectre.Console where markup only works in output rendering, SharpConsoleUI supports it **on virtually every control**:

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

Terminal.Gui has no built-in markup system -- styling requires working with `ColorScheme` objects and attribute-based formatting. Spectre.Console has great markup, but only for static output that scrolls away. SharpConsoleUI gives you rich text inside live, interactive controls.

### Developer Experience

| | Spectre.Console | Terminal.Gui v2 | SharpConsoleUI |
|---|---|---|---|
| **API style** | Fluent builders | Property assignment | **Fluent builders** |
| **Async support** | Limited (Progress) | Event-driven | **Full async/await, per-window threads** |
| **Plugin architecture** | No | No | **Yes (themes, controls, windows, services)** |
| **Theming** | No built-in theming | JSON-based config | **Theme registry with runtime switching** |
| **Markup syntax** | `[red bold]text[/]` | No markup | `[red bold]text[/]` (Spectre-compatible) |
| **Layout system** | Measure/Render | Pos/Dim computed layout | **DOM pipeline (Measure/Arrange/Paint)** |
| **Testing support** | Spectre.Console.Testing | Input injection (v2) | HeadlessConsoleDriver |
| **Logging** | N/A | N/A | **Built-in LogService (file-based, never console)** |

### Project Health (March 2026)

| | Spectre.Console | Terminal.Gui | SharpConsoleUI |
|---|---|---|---|
| **Stars** | ~11,260 | ~10,500 | 114 |
| **NuGet downloads** | ~9.9M | ~1.6M | 6,504 |
| **Contributors** | ~115 | 162 | 1 |
| **Latest stable** | 0.54.0 (pre-1.0) | 1.19.0 (v1 only) | 2.4.40 |
| **Latest version** | 0.54.0 | v2 beta (March 2026) | 2.4.40 |
| **.NET version** | .NET Standard 2.0+ | .NET Standard 2.0+ | .NET 9.0 |
| **License** | MIT | MIT | MIT |

**Honest take:** SharpConsoleUI is a young, solo-maintained project. The other two have large communities and years of battle-testing. If you need maximum community support, StackOverflow answers, and long-term maintenance guarantees, the larger libraries are safer bets. SharpConsoleUI compensates with rapid iteration and features the others don't offer.

## When NOT to Use SharpConsoleUI

Be honest about the right tool:

- **Just need pretty CLI output?** Use **Spectre.Console**. It's purpose-built for that and does it better than anything else in .NET.
- **Building a simple single-screen form?** **Terminal.Gui** has the widest control library and the most mature layout system for forms-style apps.
- **Need maximum community and ecosystem?** The bigger libraries have more users, more contributors, more blog posts, and more StackOverflow answers.
- **Targeting older .NET versions?** SharpConsoleUI requires .NET 9. Both alternatives support .NET Standard 2.0.
- **Need DatePicker, Slider, HexView, or GraphView?** Terminal.Gui has these built-in. SharpConsoleUI doesn't (yet).

## When SharpConsoleUI Shines

SharpConsoleUI is the right choice when you need:

- **Multi-window desktop-style apps** -- overlapping windows with drag, resize, minimize, maximize. No other .NET library does this in a stable release.
- **Dashboard / monitoring tools** -- independent async window threads mean each panel updates on its own schedule without blocking the UI.
- **IDE-like tools** -- [LazyDotIDE](https://github.com/nickprotop/LazyDotIDE) is a working .NET IDE built entirely on SharpConsoleUI, proving the framework handles complex, multi-window applications.
- **Embedded terminal + UI** -- TerminalControl gives you a real PTY-backed terminal emulator inside a window, alongside your UI controls. Unique in the .NET ecosystem.
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
