# SharpConsoleUI

[![NuGet](https://img.shields.io/nuget/v/SharpConsoleUI.svg)](https://www.nuget.org/packages/SharpConsoleUI/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpConsoleUI.svg)](https://www.nuget.org/packages/SharpConsoleUI/)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

**SharpConsoleUI** is a terminal GUI framework for .NET — not just a TUI library, but a full **retained-mode GUI framework** that targets the terminal as its display surface. Cross-platform (Windows, Linux, macOS).

- **GUI-grade rendering engine** — DOM-based layout (Measure → Arrange → Paint), three-level dirty tracking, occlusion culling
- **Multi-window with per-window threads** — each window updates independently without blocking others
- **30+ built-in controls** — buttons, lists, trees, tables, text editors, dropdowns, menus, tabs, canvas, image viewer, and more
- **Rich markup everywhere** — `[bold red]text[/]` with colors, styles, gradients, and decorations
- **Embedded terminal emulator** — PTY-backed `TerminalControl` runs real shells inside your TUI
- **Canvas drawing** — retained and immediate mode drawing with `CanvasControl`
- **Compositor effects** — PreBufferPaint/PostBufferPaint hooks for custom rendering, transitions, or games
- **MVVM-compatible** — `INotifyPropertyChanged` on all controls; `Bind()` / `BindTwoWay()` support
- **Fluent builders** for windows, controls, and layouts

**[Documentation](https://nickprotop.github.io/ConsoleEx/)** | **[GitHub](https://github.com/nickprotop/ConsoleEx)** | **[Examples](https://github.com/nickprotop/ConsoleEx/blob/master/docs/EXAMPLES.md)** | **[Video Demo](https://www.youtube.com/watch?v=sl5C9jrJknM)**

## Quick Start

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

var window = new WindowBuilder(windowSystem)
    .WithTitle("Hello World")
    .WithSize(60, 20)
    .Centered()
    .WithColors(Color.White, Color.DarkBlue)
    .AddControl(new MarkupControl(new List<string>
    {
        "[bold yellow]Welcome to SharpConsoleUI![/]",
        "",
        "A terminal GUI framework for .NET"
    }))
    .Build();

windowSystem.AddWindow(window);
windowSystem.Run();
```

## Project Templates

```bash
dotnet new install SharpConsoleUI.Templates

dotnet new tui-app -n MyApp            # Starter app with list, button, notification
dotnet new tui-dashboard -n MyDash     # Fullscreen dashboard with tabs and live metrics
dotnet new tui-multiwindow -n MyApp    # Two windows with master-detail pattern

cd MyApp && dotnet run
```

## Controls Library (30+)

| Category | Controls |
|----------|----------|
| **Text & Display** | MarkupControl, FigleControl, RuleControl, SeparatorControl, SparklineControl, BarGraphControl, LogViewerControl |
| **Input** | ButtonControl, CheckboxControl, PromptControl, DropdownControl, MultilineEditControl |
| **Data** | ListControl, TreeControl, TableControl (virtual DataGrid with sorting/editing), HorizontalGridControl |
| **Navigation** | MenuControl, ToolbarControl, TabControl |
| **Layout** | ColumnContainer, SplitterControl, ScrollablePanelControl, PanelControl |
| **Drawing** | CanvasControl, ImageControl (PNG/JPEG/BMP/GIF/WebP/TIFF) |
| **Advanced** | TerminalControl (PTY-backed shell), ProgressBarControl, SpectreRenderableControl |

## Key Features

### Independent Window Threads

Each window can run with its own async thread — perfect for dashboards and real-time monitoring:

```csharp
var window = new WindowBuilder(windowSystem)
    .WithTitle("Live Monitor")
    .WithSize(60, 20)
    .WithAsyncWindowThread(async (window, ct) =>
    {
        while (!ct.IsCancellationRequested)
        {
            var markup = window.FindControl<MarkupControl>("status");
            markup?.SetContent(new List<string>
            {
                $"[bold cyan]{DateTime.Now:HH:mm:ss}[/]",
                $"[yellow]Memory:[/] {GC.GetTotalMemory(false) / 1024 / 1024} MB"
            });
            await Task.Delay(1000, ct);
        }
    })
    .Build();
```

### Canvas Drawing

```csharp
var canvas = new CanvasControl { AutoSize = true };

// Retained mode — draw from any thread, content persists
var g = canvas.BeginPaint();
g.DrawCircle(30, 10, 8, '*', Color.Cyan, Color.Black);
g.GradientFillRect(0, 0, 60, 20, Color.DarkBlue, Color.Black, horizontal: false);
canvas.EndPaint();

// Immediate mode — redraw each frame
canvas.Paint += (sender, e) =>
{
    e.Graphics.WriteStringCentered(10, "Hello!", Color.White, Color.Black);
};
```

### Built-in State Services

```csharp
// Notifications
windowSystem.NotificationStateService.ShowNotification(
    "Success", "Done!", NotificationSeverity.Success);

// Focus, modals, themes — all built-in
windowSystem.FocusStateService.FocusedWindow;
windowSystem.ModalStateService.HasModals;
windowSystem.ThemeStateService.CurrentTheme;
```

### Compositor Effects

```csharp
// Post-processing effects (blur, fade, transitions)
window.Renderer.PostBufferPaint += (buffer, dirty, clip) =>
{
    // Manipulate the buffer after controls render
};

// Custom backgrounds (fractals, particles)
window.Renderer.PreBufferPaint += (buffer, dirty, clip) =>
{
    // Render before controls
};
```

## Rendering Architecture

- **Two-Level Double Buffering**: Window-level CharacterBuffer + screen-level ConsoleBuffer with front/back diff
- **Three-Level Dirty Tracking**: Window → cell → screen-level comparison
- **Occlusion Culling**: Rectangle subtraction — occluded content is never rendered
- **Adaptive Rendering**: Smart mode chooses Cell or Line rendering per line based on coverage heuristics

## Requirements

- **.NET 9.0** or later
- **Terminal with ANSI support** (Windows Terminal, iTerm2, GNOME Terminal, etc.)

## License

MIT License — See [LICENSE](https://github.com/nickprotop/ConsoleEx/blob/master/LICENSE.txt)

**Note**: Avoid `Console.WriteLine()` or console logging providers — they corrupt UI rendering. Use the built-in `LogService` instead.
