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
| Build a single-screen forms app | -- | **Yes** | **Yes** | **Yes** (one maximized borderless window = a full-screen app) |
| Build a multi-window app with overlapping windows | No | Yes (v2 GA) | No | **Best choice** |
| Drag, resize, minimize, maximize windows | No | Yes (v2 GA: move/resize/overlap) | No | **Built-in** |
| Embed a working terminal emulator | No | No | No | **Built-in (PTY)** |
| Run independent async tasks per window | No | No | No | **Built-in** |
| Apply visual effects (blur, fade, gradients) | No | No | Alpha blending only | **Built-in compositor** |
| Use Spectre.Console markup in controls | No (output only) | No | No (own markup) | **Yes, everywhere** |
| Render Markdown in controls | No | Yes (Markdown widget) | Yes (MarkdownControl) | **Yes (`[markdown]` tag, every markup control)** |
| Play video in the terminal | No | No | No | **Yes (VideoControl)** |
| Use it in production today on .NET 8+ | Yes (v0.56) | Yes (v2.4 GA, .NET 10) | Yes (v3.7, .NET 10) | Yes (v2.4.77, .NET 8/9/10) |

> **"Single-screen" is not a limitation for SharpConsoleUI.** A windowing system is a *superset* of a single-screen toolkit: a full-screen app is just one maximized, borderless window. You get the simple single-screen case for free, and the option to add more windows later if you ever need them.
>
> ```csharp
> // A full-screen, chromeless single-screen form — no title bar, no borders.
> new WindowBuilder(system)
>     .Maximized()
>     .Borderless()
>     .HideTitle()
>     .AddControl(form)
>     .BuildAndShow();
> ```
>
> And because dialogs are just windows, you get **proper dialogs for free** — modal *or* modeless, movable *or* pinned, resizable *or* fixed, with full title-bar chrome — using the same primitives, not a separate dialog concept. (`.AsModal()`, `.Movable(false)`, `.Resizable(false)`, plus `DialogTemplate` / `ToolWindowTemplate` and built-in File/Folder/Settings/About dialogs.)
>
> ```csharp
> // A modal, fixed-size, non-movable dialog — every behavior is a window setting.
> new WindowBuilder(system)
>     .WithTitle("Confirm")
>     .AsModal()
>     .Movable(false)
>     .Resizable(false)
>     .AddControl(dialogBody)
>     .BuildAndShow();
> ```

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
| **RGBA alpha blending** | No | No | **Yes (sRGB-linear LUT)** | **Yes (per-cell 0-255 Porter-Duff)** |

**What this means in practice:**

- **Spectre.Console** writes formatted text to stdout. No screen buffer, no compositing, no concurrent updates.
- **Terminal.Gui** paints all views into one shared buffer using a back-to-front painter's algorithm. If window A is fully behind window B, window A is still fully rendered.
- **XenoAtom.Terminal.UI** uses a single cell buffer with reactive invalidation and frame-to-frame diffing. Has the most sophisticated color system with true RGBA alpha blending in linear color space. But no per-window buffers or compositor pipeline.
- **SharpConsoleUI** gives each window its own character buffer, then composites them together with per-cell RGBA alpha blending (0-255 Porter-Duff). The **PreBufferPaint** hook fires before controls paint (for backgrounds, gradients, game rendering), controls render on top, then **PostBufferPaint** fires for effects (blur, fade, glow, transitions). Controls like TableControl support row-level animations (flash, highlight, fade-out removal) driven by the compositor's animation manager. This pipeline is unique -- no other .NET TUI framework exposes it.

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
| Data grid / table | Table (static output) | TableView | DataGridControl (virtualized, sort/filter/search/edit) | **TableControl (in-cell edit, AND/OR/per-column/fuzzy filter, multi-state sort, `ITableDataSource` virtualization seam)** |
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
| Image rendering | -- | -- | Yes (Kitty/Sixel/iTerm2) | **ImageControl (+ Kitty)** |
| Toolbar | -- | -- | CommandBar | **ToolbarControl** |
| Date picker | -- | DatePicker, DateEditor | -- | **DatePickerControl, TimePickerControl** |
| Slider | -- | LinearRange | Slider | **SliderControl, RangeSliderControl** |
| Hex viewer | -- | HexView | -- | -- |
| Graph view | -- | GraphView | LineChart, BreakdownChart | -- |
| Color picker | -- | ColorPicker (RGB/HSL) | ColorPicker | -- |
| Radio button | -- | OptionSelector | RadioButtonList | -- |
| Accordion / collapsible | -- | -- | Accordion, Collapsible | **CollapsiblePanel** |
| Hyperlinks (clickable) | `[link]` markup (OSC 8, terminal-handled) | `Link` view (clickable, keyboard) | Markdown renders links (click handling unconfirmed) | **`[link=url]` markup + Markdown links, in-app `LinkClicked` event, keyboard-navigable** |
| Markdown | -- | Yes (Markdig) | MarkdownControl (Markdig) | **`[markdown]` tag (Markdig, works in every markup control)** |
| Syntax highlighting | -- | Partial (TextMateSharp) | Yes (TextMateSharp) | **13 built-in (regex) + registry** |
| Video playback | -- | -- | -- | **VideoControl (half-block + Kitty)** |
| Wizard / stepper | -- | Wizard | -- | -- |
| Toast notifications | -- | -- | ToastService | **NotificationSystem** |

**Honest take:** Terminal.Gui (v2 GA) has the most mature and widest control library -- battle-tested over years, with specialized widgets like ColorPicker, HexView, and Wizard. XenoAtom.Terminal.UI is only ~5 months old but evolving fast (v3.7) and already ships a broad control set plus markdown, terminal graphics (Kitty/Sixel/iTerm2), and TextMate-based syntax highlighting. SharpConsoleUI now covers most common control types (DatePicker, TimePicker, Slider, RangeSlider, CollapsiblePanel) and its distinctive strengths are interactive/live controls (TerminalControl, SparklineControl, BarGraphControl, VideoControl, CanvasControl with 30+ drawing primitives), per-cell alpha blending, row-level animations, markup-everywhere (including `[markdown]` and clickable links), and the window management + compositor layer.

#### Data Grid: TableControl vs XenoAtom DataGridControl

The two most capable .NET TUI data grids are SharpConsoleUI's `TableControl` and XenoAtom's `DataGridControl`. They are close peers; an honest feature-by-feature:

| Capability | XenoAtom DataGridControl | SharpConsoleUI TableControl |
|---|---|---|
| In-cell inline editing | Yes | Yes (`BeginCellEdit`/`CommitEdit`/`CancelEdit`, sync **and** async `CellEditCompleted`/`CellEditCancelled` events, paste-into-cell) |
| Sorting | Yes | Multi-state cycle (None→Asc→Desc), per-column `IsSortable`, custom `IComparer`/`Comparison` |
| Filtering | Search / filter | **Compound grammar**: space = AND, `\|` = OR, per-column `column:value`, `>`/`<` operators, fuzzy match; plus programmatic `ApplyFilter(...)` |
| Column resize | Yes | Yes (`ColumnResizeEnabled`) |
| Direct cell activation | Yes | Yes (`CellActivated`) |
| Large / external data | Virtualized (built-in) | `ITableDataSource` pull seam (`RowCount` + `GetCellValue`, `INotifyCollectionChanged`) backs external/lazy data |
| Row virtualization (turnkey) | **Yes (built-in)** | Seam provided; not a shipped turnkey virtualizing renderer |

The honest split: **XenoAtom ships turnkey row virtualization**; SharpConsoleUI provides the pull-based data-source *seam* for it. In return, **SharpConsoleUI's filter is a richer expression grammar** (AND/OR/per-column/operators/fuzzy) rather than a single search box. Both are real editable, sortable, filterable data grids -- this category is a tie, not a win for either.

### Window Management

This is SharpConsoleUI's primary differentiator.

| Feature | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|:---:|:---:|:---:|:---:|
| Multiple overlapping windows | -- | Yes (v2 GA) | Popups only (WindowLayer) | **Yes** |
| Window Z-order management | -- | Yes (v2 ViewArrangement) | Z-stack for overlays | **Multi-pass (Normal/Active/AlwaysOnTop)** |
| Drag to move | -- | Yes (v2 Movable) | Popup drag only | **Yes** |
| Drag to resize (8 directions) | -- | Yes (v2 Resizable) | No | **Yes, configurable per-direction** |
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

### What's Distinctive About SharpConsoleUI

The through-line is **desktop-GUI capabilities brought to the terminal**. A handful of features set it apart in the .NET TUI space -- claims below describe SharpConsoleUI's own implementation:

- **Markup everywhere, including `[markdown]`.** One parser, one pipeline: Markdown lowers to the same Spectre-compatible markup, which parses straight into cells (no ANSI roundtrip), fully Unicode-aware (wide/CJK, combining marks, emoji). The parser-level `[markdown]` tag, `[link=url]` clickable links, `[spinner]`, and `[gradient]` text are all available in every one of the 26 markup-rendering controls -- not one dedicated widget. Fenced code blocks flow into the syntax highlighters.
- **VideoControl with Kitty true-graphics.** Terminal video playback using half-block rendering everywhere, and the Kitty graphics protocol for true raster output where the terminal supports it. None of the other three libraries do video.
- **PTY-backed embedded terminal.** `TerminalControl` runs a real PTY-backed terminal emulator inside a window, alongside your other controls -- rare in the .NET TUI ecosystem.
- **GUI-level threading model.** Each window can own an independent async thread, with UI-thread marshaling (`EnqueueOnUIThread`) much like a desktop dispatcher, so panels update on their own schedule without blocking the UI.
- **Per-window compositor.** Each window renders into its own `CharacterBuffer`; a compositor merges them with per-cell RGBA alpha blending, occlusion culling, and `PreBufferPaint`/`PostBufferPaint` hooks for backgrounds and effects -- the window-manager model adapted to character cells.
- **13 built-in syntax highlighters.** C#, Bash, JSON, JS, CSS, HTML, XML, YAML, Razor, Dockerfile, SLN, Diff, and Markdown, behind a `SyntaxHighlighters` registry shared by Markdown code blocks and `MultilineEditControl`. These are regex/lexical highlighters, not grammar-based -- TextMate-driven highlighters (Terminal.Gui, XenoAtom) parse full grammars and will be more precise on edge cases.

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
| Table cells, Menu items | Yes |
| Bar/Line graph & Sparkline labels | Yes |
| Date/Time pickers, Spinners, Progress bars | Yes |
| MultilineEdit, LogViewer, NavigationView | Yes |
| MarkupControl | Yes (entire content, incl. clickable links) |
| Status bars | Yes |

*(26 controls call the markup parser in total -- the table above is a representative sample.)*

The markup pipeline also renders **Markdown**, via a parser-level `[markdown]` tag (Markdig-based) rather than a dedicated control. Because it lives in the markup parser, every one of the **26 markup-rendering controls** -- buttons, list items, tree nodes, table cells, tab headers, the status bar -- can render Markdown, mixed inline with native markup in the same string. Fenced code blocks pick up the built-in syntax highlighters, and copied text falls back to plain text. Other libraries expose Markdown as a single dedicated control; here it is available anywhere markup is.

**One parser, one pipeline: Markdown → native markup → cells.** This is the architectural core. Markdig-parsed Markdown lowers to the same Spectre-compatible markup the framework uses everywhere, which a single `MarkupParser` turns directly into display cells -- no ANSI roundtrip, no second renderer. That one parser handles colors (named + hex), the full decoration set (bold/dim/italic/underline/strike/blink/reverse/invert), `[link=url]` clickable links, `[spinner]` animated glyphs, `[gradient]` text, `[fillwidth]`, and `[markdown]` regions -- and it is **fully Unicode-aware** (width-correct wide/CJK characters, combining marks, surrogate-pair emoji). Because that single pipeline is wired into 26 controls, *any text in almost any control* speaks the same rich, Unicode-correct markup. No other .NET TUI library makes one markup parser this pervasive.

```csharp
var label = Controls.Markup()
    .WithText("[markdown]## Status\n- **CPU**: `42%`\n- **Mem**: `1.2 GB`[/]")
    .Build();
```

**How this compares:**

- **Spectre.Console** has excellent markup -- but only for static output that scrolls away. You can't use it in live, interactive controls.
- **Terminal.Gui** has no markup system. Styling requires working with `ColorScheme` objects and attribute-based formatting. v2 adds `VisualRole` semantic styling, but no inline markup syntax.
- **XenoAtom.Terminal.UI** has its own markup and `Brush` system (including gradient brushes for text), but it's a separate syntax incompatible with Spectre.Console. No `IRenderable` bridge.
- **SharpConsoleUI** parses Spectre-compatible markup directly into cells (no ANSI roundtrip) and supports any Spectre `IRenderable` (Tables, Charts, BarCharts) as a control via `SpectreRenderableControl`. This extends Spectre.Console rather than replacing it.

### Hyperlinks

All four libraries can show links, but they handle the *click* very differently:

- **Spectre.Console** emits **OSC 8** terminal hyperlinks via `[link]` / `[link=url]` markup. The terminal (Windows Terminal, iTerm2, etc.) makes them clickable; on terminals without OSC 8 support they render as plain text. Because Spectre is output-only with no event loop, the **application never learns a link was clicked** — the terminal owns the action.
- **Terminal.Gui v2** has a dedicated `Link` view: clickable and keyboard-activatable (HotKey / Enter / Space), the framework opens the URL itself (`Process.Start` / `open` / `xdg-open`, with a scheme allow-list) and also emits OSC 8. A solid, self-contained link widget.
- **XenoAtom.Terminal.UI** renders Markdown links (Markdig) and has OSC 8 *parsing* infrastructure; whether Markdown links are clickable in-app is unconfirmed in its docs.
- **SharpConsoleUI** takes the **in-app event** approach: a `[link=url]` markup tag (and Markdown `[text](url)` inside `[markdown]` regions) renders styled link text and raises a **`LinkClicked` event** carrying the URL and visible text. The application decides what to do — open a browser, navigate within the app, copy the URL, anything. Links are also **keyboard-navigable**: a markup control with links becomes a Tab stop, Left/Right move between links and Enter activates. Because the link lives in the markup parser, *any* markup-rendering control (and `HtmlControl`) gets clickable links, not just one dedicated widget.

The practical difference: Spectre hands the click to the terminal; Terminal.Gui opens the URL for you; SharpConsoleUI hands the click to *your code* as an event, so an app can do something other than "open in browser" (in-app routing, previews, custom schemes) and stays in control.

### Layout System

| | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **Approach** | Measure/Render | Pos/Dim with arithmetic | Flexbox + Grid + Dock | DOM pipeline (Measure/Arrange/Paint) |
| **Containers** | Layout (grid cells) | View nesting | VStack, HStack, WrapStack, Grid, DockLayout, ZStack, Splitter, ScrollViewer | HorizontalGrid, ColumnContainer, ScrollablePanel |
| **Reactive layout** | No | Computed Pos/Dim | **[Bindable] source-generated with dependency tracking** | MVVM binding with auto-invalidation |
| **Flexibility** | Low | High (Pos/Dim arithmetic) | **Very high (WPF/Avalonia-level)** | Moderate |

XenoAtom.Terminal.UI has the most sophisticated layout system with a proper `FlexAllocator` (grow/shrink/min/max like CSS Flexbox), full Grid with row/column definitions, and DockLayout. Terminal.Gui's Pos/Dim system with arithmetic composition is also very powerful. SharpConsoleUI's layout is simpler -- it prioritizes the compositor pipeline over layout complexity.

### Developer Experience

| | Spectre.Console | Terminal.Gui v2 | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **API style** | Fluent builders | Property assignment | Property assignment + reactive bindings | **Fluent builders** |
| **Data binding** | None | Manual / events | **Reactive [Bindable] with auto-invalidation** | **MVVM with Bind/BindTwoWay** |
| **Async support** | Limited (Progress) | Event-driven | Dispatcher-based | **Full async/await, per-window threads** |
| **Plugin architecture** | No | No | No | **Yes (themes, controls, windows, services)** |
| **Theming** | No built-in theming | JSON-based Scheme + VisualRole | Theme + 73 per-control style files | **Theme registry with runtime switching** |
| **Markup syntax** | `[red bold]text[/]` | No markup | Own markup + Brush/Gradient | `[red bold]text[/]` (Spectre-compatible) |
| **Testing support** | Spectre.Console.Testing | Input injection (v2) | Screenshot/snapshot testing | HeadlessConsoleDriver |
| **Logging** | N/A | Microsoft.Extensions.Logging | N/A | **Built-in LogService (file-based, never console)** |
| **NativeAOT** | Yes (since 2024) | In progress (v2 AOT validation) | Yes (AOT-oriented design) | **Yes (`IsAotCompatible`, publish-and-run CI gate over the full control set)** |

**On NativeAOT:** AOT support is common here — Spectre.Console and XenoAtom.Terminal.UI are both AOT-ready, and Terminal.Gui v2 is restoring AOT validation. SharpConsoleUI is AOT-compatible too: a CI job publishes a native binary on every push and runs it to confirm. The one documented caveat is a trim *warning* (not a failure) from `AngleSharp.Css` when you use `HtmlControl` — see [docs/AOT.md](AOT.md).

### Project Health (June 2026)

| | Spectre.Console | Terminal.Gui | XenoAtom.Terminal.UI | SharpConsoleUI |
|---|---|---|---|---|
| **Stars** | ~11,490 | ~11,060 | ~271 | 232 |
| **NuGet downloads** | ~44.4M | ~1.8M | ~17.8K | ~14,000 |
| **Contributors** | ~146 | ~130 | ~2 | 1 |
| **Latest stable** | 0.56.0 (pre-1.0) | 2.4.5 (v2 GA) | 3.7.4 | 2.4.77 |
| **v2 / latest** | 0.56.0 | 2.4.5 (v2 GA, GA since 2024) | 3.7.4 | 2.4.77 |
| **.NET version** | net8/9/10 + .NET Standard 2.0 | .NET 10 (v2.4.x) | .NET 10 only | net8/9/10 |
| **License** | MIT | MIT | BSD-2-Clause | MIT |
| **Repo age** | ~6 years | ~8.5 years | ~5 months | ~16 months |

**Honest take:** Terminal.Gui and Spectre.Console have large communities and years of battle-testing, and Terminal.Gui v2 is now GA (not beta). XenoAtom.Terminal.UI is only ~5 months old but evolving rapidly (already at v3.7) and built by Alexandre Mutel, a highly respected .NET developer (Markdig, SharpDX, Scriban, former .NET Foundation TSG member). SharpConsoleUI is solo-maintained at ~232 stars. If you need maximum community support, StackOverflow answers, and long-term maintenance guarantees, the larger libraries are safer bets. SharpConsoleUI and XenoAtom compensate with rapid iteration and features the others don't offer.

## When NOT to Use SharpConsoleUI

Be honest about the right tool:

- **Just need pretty CLI output?** Use **Spectre.Console**. It's purpose-built for that and does it better than anything else in .NET.
- **Building a simple single-screen form?** All three work — a SharpConsoleUI app can be a single maximized, borderless window (`.Maximized().Borderless().HideTitle()`), which is exactly a full-screen form. Reach for **Terminal.Gui** if you specifically want its widest mature control library, or **XenoAtom.Terminal.UI** for reactive bindings (requires .NET 10). Pick by control set and binding style, not by "can it do single-screen" — they all can.
- **Need maximum community and ecosystem?** The bigger libraries have more users, more contributors, more blog posts, and more StackOverflow answers.
- **Targeting .NET 6 or older?** SharpConsoleUI requires .NET 8+. Spectre.Console supports .NET Standard 2.0 (and net8/9/10). Terminal.Gui v2 targets .NET 10. XenoAtom requires .NET 10.
- **Need ColorPicker or HexView?** Terminal.Gui has these built-in. XenoAtom has ColorPicker. SharpConsoleUI doesn't (yet).
- **Need source-generated reactive bindings?** XenoAtom's `[Bindable]` source-generated property system with automatic dependency tracking is more sophisticated than SharpConsoleUI's lambda-based MVVM bindings.

## When SharpConsoleUI Shines

SharpConsoleUI is the right choice when you need:

- **Multi-window desktop-style apps** -- overlapping windows with drag, resize, minimize, maximize, taskbar, and full title-bar chrome. Terminal.Gui v2 (GA) now offers movable/resizable/overlapping views too; SharpConsoleUI's differentiator is the full window-manager experience plus the compositor underneath it.
- **Visual effects and compositing** -- per-cell RGBA alpha blending, gradient backgrounds, blur, fade transitions, row animations (flash, highlight, fade-out removal), custom buffer effects via PreBufferPaint/PostBufferPaint. The compositor pipeline is unique in the .NET TUI space.
- **Dashboard / monitoring tools** -- independent async window threads mean each panel updates on its own schedule without blocking the UI.
- **IDE-like tools** -- [LazyDotIDE](https://github.com/nickprotop/LazyDotIDE) is a working .NET IDE built entirely on SharpConsoleUI, proving the framework handles complex, multi-window applications.
- **Embedded terminal + UI** -- TerminalControl gives you a real PTY-backed terminal emulator inside a window, alongside your UI controls. Rare in the .NET ecosystem.
- **Markdown and rich markup in every control** -- one parser lowers Markdown to native markup and parses it straight into Unicode-correct cells, so the `[markdown]` tag (and clickable `[link=url]`, `[gradient]`, `[spinner]`) work inside any of the 26 markup controls, not just a single dedicated widget.
- **Terminal video** -- VideoControl plays video with half-block rendering, and Kitty true-graphics where supported. None of the other .NET TUI libraries do video.
- **MVVM data binding** -- `Bind()` and `BindTwoWay()` with lambda expressions, type converters, and standard `INotifyPropertyChanged` ViewModels. All controls support property change notification out of the box.  See the [Data Binding guide](binding.md).
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

## Rendering Architecture Deep Dive

TUI frameworks differ most fundamentally in how they turn a tree of controls into pixels on screen. This section explains where SharpConsoleUI sits and why the differences matter.

### How TUI Frameworks Render

| Approach | Description | Used by |
|---|---|---|
| **String output** | `View()` returns a string; framework diffs strings | BubbleTea (Go) |
| **Immediate-mode** | Redraw entire UI into a single buffer each frame | Ratatui (Rust) |
| **Retained-mode, shared buffer** | Widget tree paints into one buffer with dirty tracking | Textual (Python), Terminal.Gui (.NET) |
| **Retained-mode, per-window buffers** | Each window has its own buffer; a compositor merges them | SharpConsoleUI (.NET) |

Most TUI frameworks use a single shared buffer. SharpConsoleUI gives each window its own `CharacterBuffer`, then composites them together -- a pattern borrowed from desktop window managers (DWM, Quartz, Wayland compositors), adapted for character cells.

### The Compositor Pipeline

```
Per-Window Buffers    Compositor           Console Driver
┌──────────┐
│ Window A │──┐
└──────────┘  │   ┌──────────────────┐   ┌─────────────────┐
┌──────────┐  ├──▶│ Visible Regions  │──▶│ Diff-based flush│──▶ stdout
│ Window B │──┤   │ (skip occluded)  │   │ (changed cells) │
└──────────┘  │   └──────────────────┘   └─────────────────┘
┌──────────┐  │
│ Overlay  │──┘
└──────────┘
```

The visible regions calculator uses rectangle subtraction to determine which pixels of each window are actually visible on screen. Occluded regions are skipped entirely rather than painted and overwritten.

Each window's render also supports compositor hooks: `PreBufferPaint` fires before controls (for gradients, custom backgrounds), then controls paint, then `PostBufferPaint` fires (for effects like blur or fade).

### Frame-Coupled Animations

The animation system runs inside the main render loop, not on separate timers:

1. Poll input
2. Advance animations (`AnimationManager.Update(deltaTime)`)
3. Layout pass (Measure → Arrange → Paint)
4. Composite and flush

Delta time is capped at 33ms to prevent animations from completing instantly after idle periods. This is what enables smooth transitions in controls like NavigationView -- the animation and layout run in the same frame tick.

### DOM Layout

SharpConsoleUI uses a three-pass DOM layout (Measure → Arrange → Paint) similar to WPF and Avalonia. Each control reports its desired size, receives its final bounds from its parent, then paints into its window buffer. This is what allows the responsive NavigationView to detect its actual width and switch display modes.

---

## Cross-Ecosystem Comparison

Different languages have their own established TUI frameworks. Here's how the major ones compare architecturally.

### Feature Comparison

| | SharpConsoleUI | Textual | Ratatui | BubbleTea | Terminal.Gui |
|---|---|---|---|---|---|
| **Language** | C# | Python | Rust | Go | C# |
| **Architecture** | Compositor | Retained + segment compositor | Immediate-mode | Elm (TEA) | Retained, shared buffer |
| **Overlapping windows** | Yes | Screens (modal stack) | No | No | Yes (v2 GA) |
| **Window management** | Drag, resize, minimize, maximize | No | No | No | Move/resize/overlap (v2 GA) |
| **Built-in animations** | Frame-coupled tweens + row animations | CSS-like transitions | Via tachyonfx crate | No | No |
| **Overlay/portal system** | Yes (auto-positioning, auto-dismiss) | Screen stack | Manual | Manual | No |
| **Responsive controls** | Yes (NavigationView) | CSS-like media queries | No | No | No |
| **Per-window buffers** | Yes | No | No | No | No |
| **Compositor hooks** | PreBufferPaint / PostBufferPaint | No | No | No | No |
| **DOM layout pipeline** | Measure / Arrange / Paint | CSS Box Model | Constraint-based | String concat | Pos/Dim arithmetic |
| **Async per-window** | Yes | Async workers | Manual | Goroutines | No |
| **Desktop packaging** | schost | textual-web (browser) | No | No | No |
| **Embedded terminal** | PTY-backed | No | No | No | No |
| **Mouse** | Full | Full | Via backend | Via backend | Full |
| **24-bit color** | Yes | Yes | Yes | Via Lip Gloss | Yes |

### How They Differ

**Ratatui** is a lightweight Rust library where you own the event loop and redraw the entire UI each frame. Clean and fast, but no retained state or compositing -- you manage everything yourself.

**BubbleTea** uses the Elm architecture in Go -- your `View()` returns a string, the framework diffs it. Elegant for single-screen apps. The string-based model doesn't support pixel-level compositing.

**Textual** has its own segment-based compositor that merges overlapping widget output. It uses a spatial grid for fast hit-testing and supports CSS-like animations and styling. The most feature-rich Python TUI framework.

**Terminal.Gui** is the most mature .NET TUI library with the widest control set. v2 (GA) adds overlapping, movable, and resizable window support. Uses a shared buffer with painter's algorithm rendering.

**SharpConsoleUI** takes the compositor approach further with per-window buffers, occlusion culling via rectangle subtraction, and compositor hooks for visual effects. The animation system and DOM layout pipeline enable features like the responsive NavigationView with animated mode transitions.

Each framework makes different tradeoffs. The compositor approach adds complexity but enables desktop-class features (window management, visual effects, animated responsive controls) that are difficult to achieve with a shared buffer or string-based model.

---

*Last updated: June 2026*
