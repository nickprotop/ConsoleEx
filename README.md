# SharpConsoleUI

![Version](https://img.shields.io/badge/version-2.0-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

A modern console window system for .NET 9 with fluent builders, async patterns, and built-in state services.

## Quick Start

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;

// Create window system with built-in logging and state services
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Use fluent builder pattern
var window = new WindowBuilder(windowSystem)
    .WithTitle("Hello World")
    .WithSize(50, 15)
    .Centered()
    .WithColors(Color.DarkBlue, Color.White)
    .Build();

// Show a notification
windowSystem.NotificationStateService.ShowNotification(
    "Welcome", "Hello World!", NotificationSeverity.Info);

windowSystem.AddWindow(window);
windowSystem.Run();
```

## Table of Contents

- [Installation](#installation)
- [Core Features](#core-features)
- [API Usage](#api-usage)
- [Architecture Overview](#architecture-overview)
- [Examples](#examples)
- [Advanced Features](#advanced-features)
- [Contributing](#contributing)
- [Documentation](#documentation)
- [Projects Using SharpConsoleUI](#projects-using-sharpconsoleui)

## Installation

### Package Manager
```bash
Install-Package SharpConsoleUI
```

### .NET CLI
```bash
dotnet add package SharpConsoleUI
```

### PackageReference
```xml
<PackageReference Include="SharpConsoleUI" Version="2.0.0" />
```

## Core Features

### Window Management
- **Multiple Windows**: Create and manage overlapping windows with proper Z-order
- **Window States**: Normal, maximized, minimized states
- **Window Modes**: Normal and modal dialogs
- **Independent Window Threads**: Each window can run with its own async thread for real-time updates
- **Focus Management**: Keyboard and mouse focus handling
- **Window Cycling**: Alt+1-9, Ctrl+T for window navigation

### Input Handling
- **Keyboard Input**: Full keyboard support with modifier keys
- **Mouse Support**: Click, drag, and mouse event handling
- **Input Queue**: Efficient input processing system

### Rendering System
- **Spectre.Console Foundation**: Leverages Spectre's perfect rendering engine for rich TUI widgets
- **Double Buffering**: Smooth rendering without flicker
- **Dirty Regions**: Efficient partial updates
- **Render Modes**: Direct and buffered rendering
- **Themes**: Multiple built-in themes (Classic, ModernGray) with runtime switching
- **Plugins**: Extensible architecture with DeveloperTools plugin
- **Status Bars**: Top and bottom status bar support

### Controls Library
- **MarkupControl**: Rich text with Spectre.Console markup
- **ButtonControl**: Interactive buttons with events
- **CheckboxControl**: Toggle controls
- **MultilineEditControl**: Text editing with scrolling
- **TreeControl**: Hierarchical data display
- **ListControl**: List display and selection
- **HorizontalGridControl**: Tabular data display

## API Usage

SharpConsoleUI provides a fluent API with built-in logging, state services, and modern C# features.

### 1. Built-in Services

The library includes built-in logging and state services - no DI setup required:

```csharp
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Built-in logging (outputs to file, never console)
windowSystem.LogService.LogAdded += (s, entry) => { /* handle log */ };

// Built-in state services
windowSystem.NotificationStateService.ShowNotification("Title", "Message", NotificationSeverity.Info);
windowSystem.ModalStateService.HasModals;
windowSystem.FocusStateService.FocusedWindow;
```

Enable debug logging via environment variables:
```bash
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/consoleui.log
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
```

### 2. Fluent Window Builders
```csharp
using SharpConsoleUI.Builders;

// Create windows using fluent API
var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("Modern Application")
    .WithSize(80, 25)
    .Centered()
    .WithColors(Color.DarkBlue, Color.White)
    .Resizable()
    .Movable()
    .WithMinimumSize(60, 20)
    .Build();

// Dialog template - applies title, size, centered, modal automatically
var dialog = new WindowBuilder(windowSystem)
    .WithTemplate(new DialogTemplate("Confirmation", 40, 10))
    .Build();

// Tool window template - applies title, position, size automatically
var toolWindow = new WindowBuilder(windowSystem)
    .WithTemplate(new ToolWindowTemplate("Tools", new Point(5, 5), new Size(30, 15)))
    .Build();
```

### 3. Independent Window Threads

**KEY FEATURE**: Each window can run with its own async thread, enabling true multi-threaded UIs where windows update independently.

```csharp
// Create a window with an independent async thread
var clockWindow = new WindowBuilder(windowSystem)
    .WithTitle("Digital Clock [1s refresh]")
    .WithSize(40, 12)
    .WithAsyncWindowThread(UpdateClockAsync)  // Async method runs independently
    .Build();

// The async method receives Window and CancellationToken
private async Task UpdateClockAsync(Window window, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)  // Runs until window closes
    {
        try
        {
            var now = DateTime.Now;

            // Find and update control by name
            var timeControl = window.FindControl<MarkupControl>("timeDisplay");
            timeControl?.SetContent(new List<string>
            {
                $"[bold cyan]{now:HH:mm:ss}[/]",
                $"[yellow]{now:dddd}[/]",
                $"[white]{now:MMMM dd, yyyy}[/]"
            });

            await Task.Delay(1000, ct);  // Update every second
        }
        catch (OperationCanceledException) { break; }  // Clean shutdown
    }
}
```

**Benefits:**
- **True Parallelism**: Multiple windows update simultaneously without blocking
- **Real-time Data**: Perfect for dashboards, monitors, live feeds
- **Clean Cancellation**: CancellationToken handles automatic cleanup on window close
- **No Manual Threading**: Framework manages thread lifecycle

### 4. Resource Management
```csharp
using SharpConsoleUI.Core;

// Automatic resource disposal
using var disposableManager = new DisposableManager();

// Register resources for automatic cleanup
var window1 = disposableManager.Register(CreateWindow("Window 1"));
var window2 = disposableManager.Register(CreateWindow("Window 2"));

// Register custom cleanup actions
disposableManager.RegisterDisposalAction(() =>
{
    // Perform custom cleanup
});

// Create scoped disposals
using var scope = disposableManager.CreateScope();
scope.Register(temporaryWindow);
scope.RegisterDisposalAction(() => SaveTempData());
// Scope automatically disposes when using block exits
```

### 5. Event Handlers with Window Access

All event handlers in fluent builders include a `window` parameter, enabling access to other controls via `FindControl<T>()`:

```csharp
// Create controls with names
window.AddControl(
    Controls.Markup("[bold]Status:[/] Ready")
        .WithName("status")
        .Build()
);

window.AddControl(
    Controls.Prompt("Enter name:")
        .WithName("nameInput")
        .OnInputChanged((sender, text, window) =>
        {
            // Access other controls through window parameter
            var status = window.FindControl<MarkupControl>("status");
            status?.SetContent($"[bold]Status:[/] Typing... ({text.Length} chars)");
        })
        .Build()
);

window.AddControl(
    Controls.Button("Submit")
        .OnClick((sender, e, window) =>
        {
            var nameInput = window.FindControl<PromptControl>("nameInput");
            var status = window.FindControl<MarkupControl>("status");

            if (string.IsNullOrWhiteSpace(nameInput?.Text))
            {
                status?.SetContent("[red]Error:[/] Name is required");
            }
            else
            {
                status?.SetContent($"[green]Submitted:[/] {nameInput.Text}");
                nameInput.Text = "";
            }
        })
        .Build()
);
```

#### Available Event Handler Signatures

All fluent builders provide event handlers with window access:

| Builder | Event Method | Event Handler Signature |
|---------|--------------|-------------------------|
| ButtonBuilder | `OnClick` | `(sender, ButtonControl, Window)` |
| ListBuilder | `OnItemActivated` | `(sender, ListItem, Window)` |
| ListBuilder | `OnSelectionChanged` | `(sender, int, Window)` |
| ListBuilder | `OnSelectedItemChanged` | `(sender, ListItem?, Window)` |
| CheckboxBuilder | `OnCheckedChanged` | `(sender, bool, Window)` |
| DropdownBuilder | `OnSelectionChanged` | `(sender, int, Window)` |
| DropdownBuilder | `OnSelectedItemChanged` | `(sender, DropdownItem?, Window)` |
| PromptBuilder | `OnEntered` | `(sender, string, Window)` |
| PromptBuilder | `OnInputChanged` | `(sender, string, Window)` |

This enables **pure declarative UIs** where all control interactions happen through named lookups, eliminating the need to maintain field references.

## Architecture Overview

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    SharpConsoleUI Architecture              │
├─────────────────────────────────────────────────────────────┤
│  Application Layer (Your Code)                              │
│  └── Window Builders & Event Handlers                       │
├─────────────────────────────────────────────────────────────┤
│  Framework Layer                                            │
│  ├── Window Builders (Fluent API)                           │
│  ├── State Services (Focus, Modal, Notification, etc.)      │
│  ├── Logging Service (ILogService)                          │
│  └── Resource Management (DisposableManager)                │
├─────────────────────────────────────────────────────────────┤
│  Core UI Layer                                              │
│  ├── ConsoleWindowSystem (Window Management)                │
│  ├── Window (Container & Rendering)                         │
│  ├── Controls (UI Components)                               │
│  └── Themes (Appearance)                                    │
├─────────────────────────────────────────────────────────────┤
│  Driver Layer                                               │
│  ├── IConsoleDriver (Abstraction)                           │
│  ├── NetConsoleDriver (Implementation)                      │
│  └── Input/Output Handling                                  │
└─────────────────────────────────────────────────────────────┘
```

### Spectre.Console Integration

SharpConsoleUI uses **Spectre.Console as its rendering foundation**, combining Spectre's beautiful rendering with a complete windowing system:

1. **Capture Spectre Output**: `AnsiConsoleHelper` captures Spectre's ANSI rendering into our CharacterBuffer
2. **Wrap Any Spectre Widget**: `SpectreRenderableControl` makes any `IRenderable` (Tables, Trees, Panels, Charts) work as a window control
3. **Combine with Windows**: Multiple Spectre-rendered controls + interactive controls + independent window threads
4. **Result**: Best of both worlds - Spectre's rich rendering + full windowing system + multi-threading

This architecture allows using Spectre's perfect rendering engine while adding features Spectre.Console doesn't provide: overlapping windows, Z-order management, independent window threads, and a complete event-driven UI system.

### Modern C# Features Used
- **Records**: Immutable data structures (WindowBounds, InputEvent, etc.)
- **Nullable Reference Types**: Explicit null handling
- **Pattern Matching**: Enhanced switch expressions
- **Async/Await**: Throughout the framework
- **Top-level Programs**: Simplified entry points
- **Init-only Properties**: Immutable initialization
- **Primary Constructors**: Concise record definitions

## Examples

### Complete Modern Application
```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;

namespace MyApp;

internal class Program
{
    static int Main(string[] args)
    {
        // Create window system (has built-in logging and state services)
        var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
        {
            TopStatus = "My Modern App",
            BottomStatus = "ESC: Close | F1: Help"
        };

        // Main window with fluent builder
        var mainWindow = new WindowBuilder(windowSystem)
            .WithTitle("Task Manager")
            .WithSize(80, 25)
            .Centered()
            .WithColors(Color.DarkBlue, Color.White)
            .Build();

        // Add controls
        mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold yellow]Welcome to Task Manager![/]",
            "",
            "[green]Features:[/]",
            "• Real-time task monitoring",
            "• Interactive controls",
            "• Built-in notifications",
            "",
            "[dim]Press F2 to add a new task[/]"
        }));

        // Setup key handlers
        mainWindow.KeyPressed += (sender, e) =>
        {
            switch (e.KeyInfo.Key)
            {
                case ConsoleKey.F2:
                    CreateAddTaskWindow(windowSystem);
                    e.Handled = true;
                    break;
                case ConsoleKey.Escape:
                    windowSystem.CloseWindow(mainWindow);
                    e.Handled = true;
                    break;
            }
        };

        windowSystem.AddWindow(mainWindow);
        return windowSystem.Run();
    }

    static void CreateAddTaskWindow(ConsoleWindowSystem windowSystem)
    {
        var taskWindow = new WindowBuilder(windowSystem)
            .WithTitle("➕ Add Task")
            .WithSize(50, 12)
            .Centered()
            .AsModal()
            .Build();

        taskWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold]Add New Task[/]",
            "",
            "Enter task description and press Enter:",
            ""
        }));

        // Add interactive input
        var input = new PromptControl
        {
            Prompt = "Task: ",
            OnEnter = (text) =>
            {
                // Handle task creation
                SaveTask(text);
                windowSystem.CloseWindow(taskWindow);
                return true;
            }
        };

        taskWindow.AddControl(input);
        windowSystem.AddWindow(taskWindow, activate: true);
    }

    static void SaveTask(string taskDescription)
    {
        // Save task to storage
        // File.AppendAllText("tasks.txt", taskDescription + Environment.NewLine);
    }
}
```

### Real-time Data Window
```csharp
public static async Task CreateRealtimeWindow(ConsoleWindowSystem windowSystem)
{
    var dataWindow = new WindowBuilder(windowSystem)
        .WithTitle("Real-time Data")
        .WithSize(60, 20)
        .AtPosition(10, 5)
        .Build();

    // Background task for real-time updates
    var updateTask = Task.Run(async () =>
    {
        var random = new Random();

        while (windowSystem.Windows.Values.Contains(dataWindow))
        {
            dataWindow.ClearControls();

            dataWindow.AddControl(new MarkupControl(new List<string>
            {
                "[bold blue]System Metrics[/]",
                $"[green]CPU Usage:[/] {random.Next(0, 100)}%",
                $"[yellow]Memory:[/] {random.Next(1000, 8000)}MB",
                $"[red]Network:[/] {random.Next(0, 1000)}KB/s",
                $"[cyan]Updated:[/] {DateTime.Now:HH:mm:ss}",
                "",
                "[dim]Updates every 2 seconds • ESC to close[/]"
            }));

            await Task.Delay(2000);
        }
    });

    dataWindow.KeyPressed += (sender, e) =>
    {
        if (e.KeyInfo.Key == ConsoleKey.Escape)
        {
            windowSystem.CloseWindow(dataWindow);
            e.Handled = true;
        }
    };

    windowSystem.AddWindow(dataWindow);
}
```

## Advanced Features

### Built-in Themes & Theme Registry

SharpConsoleUI includes multiple built-in themes that can be switched at runtime:

```csharp
// Switch to Modern Gray theme (dark theme with gray tones)
windowSystem.ThemeRegistry.SetTheme("ModernGray");

// Switch to Classic theme (navy blue windows, traditional look)
windowSystem.ThemeRegistry.SetTheme("Classic");

// Available built-in themes:
// - "Classic": Navy blue windows with traditional styling
// - "ModernGray": Modern dark theme with gray color scheme
```

Theme changes apply immediately to all windows and controls, enabling dynamic appearance customization.

### Plugin System

The DeveloperTools plugin provides built-in development tools and diagnostics:

```csharp
// Load the built-in DeveloperTools plugin
windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();

// Switch to DevDark theme (provided by plugin)
windowSystem.SwitchTheme("DevDark");

// Create Debug Console window from plugin
var debugWindow = windowSystem.PluginStateService.CreateWindow("DebugConsole");
windowSystem.AddWindow(debugWindow);

// Get diagnostics service from plugin (agnostic - no type knowledge required!)
var diagnostics = windowSystem.PluginStateService.GetService("Diagnostics");
if (diagnostics != null)
{
    var report = (string)diagnostics.Execute("GetDiagnosticsReport")!;
}
```

**DeveloperTools Plugin Provides:**
- **DevDark Theme**: Dark developer theme with green terminal-inspired accents
- **LogExporter Control**: Export and filter application logs
- **DebugConsole Window**: Interactive debug console for runtime inspection
- **Diagnostics Service**: System diagnostics and performance metrics (agnostic IPluginService)

Create custom plugins by implementing `IPlugin` for application-specific tools and extensions.

### Custom Themes
```csharp
using SharpConsoleUI.Themes;

public class MyDarkTheme : ITheme
{
    public Color WindowBackgroundColor => Color.Black;
    public Color WindowForegroundColor => Color.White;
    public Color ActiveBorderForegroundColor => Color.Cyan;
    public Color InactiveBorderForegroundColor => Color.DarkGray;
    public Color ActiveTitleForegroundColor => Color.Yellow;
    public Color InactiveTitleForegroundColor => Color.Gray;
    public Color DesktopBackgroundColor => Color.DarkBlue;
    public Color DesktopForegroundColor => Color.White;
    public char DesktopBackroundChar => '░';
}

// Apply theme
windowSystem.Theme = new MyDarkTheme();
```

## Contributing

We welcome contributions! Here's how to get started:

### Development Setup
```bash
git clone https://github.com/nickprotop/ConsoleEx.git
cd ConsoleEx
dotnet restore
dotnet build
```

### Running Examples
```bash
# Comprehensive demo application
cd Examples/DemoApp
dotnet run

# Console monitoring tool (ConsoleTop-style dashboard)
cd Examples/ConsoleTopExample
dotnet run

# Full screen example
cd Examples/FullScreenExample
dotnet run

# Plugin showcase
cd Examples/PluginShowcaseExample
dotnet run
```

### Project Structure
```
ConsoleEx/
├── SharpConsoleUI/           # Main library
│   ├── Core/                 # State services & infrastructure
│   ├── Logging/              # Built-in logging system
│   ├── Controls/             # UI controls
│   ├── Builders/             # Fluent builders & templates
│   ├── Helpers/              # Utility classes
│   ├── Drivers/              # Console abstraction layer
│   └── Themes/               # Theming system
├── Examples/                 # Example applications
│   ├── DemoApp/              # Comprehensive demo
│   ├── ConsoleTopExample/    # System monitoring dashboard
│   ├── FullScreenExample/    # Full screen demo
│   ├── PluginShowcaseExample/# Plugin system demo
│   ├── AgentStudio/          # Advanced agent demo
│   ├── MultiDashboard/       # Multi-window dashboard
│   ├── MenuDemo/             # Menu system demo
│   └── BorderStyleDemo/      # Border style demo
└── Tests/                    # Unit tests
```

### Coding Standards
- Follow C# coding conventions
- Use modern C# features (records, nullable refs, etc.)
- Include XML documentation
- Add unit tests for new features
- Maintain backward compatibility

### Critical: Console Output & Logging
**NEVER use console-based output in SharpConsoleUI applications - it corrupts the display!**

**❌ Avoid These (corrupt UI rendering):**
- `Console.WriteLine()`, `Console.Write()`, `Console.Clear()`
- `builder.AddConsole()` in logging configuration
- Any output that writes directly to console

**✅ Use These Alternatives:**
- **UI Updates**: `window.AddControl(new MarkupControl("message"))`
- **File Logging**: File-based or database logging providers
- **Debug Logging**: Only in development, not console output
- **Event Logging**: Windows Event Log or similar

**Use the built-in logging instead:**
```bash
# Enable debug logging via environment variables
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/consoleui.log
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
```

### Built-in Debug Logging

The library includes a built-in debug logging system for troubleshooting, controlled via environment variables:

```bash
# Enable debug logging to file
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/consoleui.log

# Set minimum log level (Trace, Debug, Information, Warning, Error, Critical)
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
```

Access logs programmatically:
```csharp
// Subscribe to log events
windowSystem.LogService.LogAdded += (s, entry) => { /* handle entry */ };

// Get recent logs
var logs = windowSystem.LogService.GetRecentLogs(50);
```

### Notifications

Display notifications using the built-in notification service:
```csharp
windowSystem.NotificationStateService.ShowNotification(
    title: "Success",
    message: "Operation completed",
    severity: NotificationSeverity.Success,
    blockUi: false,
    timeout: 5000);
```

Severity levels: `Info`, `Success`, `Warning`, `Danger`, `None`

## License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## Acknowledgments

- Built on [Spectre.Console](https://github.com/spectreconsole/spectre.console) for rich console output
- Inspired by traditional GUI frameworks adapted for console applications

## Development Notes

SharpConsoleUI was initially developed manually with core windowing functionality and double-buffered rendering. The project evolved to include modern features (DOM-based layout system, fluent builders, plugin architecture, theme system) with AI assistance. Architectural decisions and feature design came from the project author, while AI generated code implementations based on those decisions. The development process involved code generation, review sessions, debugging, and manual refinements. This collaborative approach enabled rapid iteration on complex features while maintaining architectural coherence.

---

## Documentation

Detailed documentation is available in separate guides:

- **[Configuration Guide](docs/CONFIGURATION.md)** - Complete system configuration reference
- **[Status System](docs/STATUS_SYSTEM.md)** - Status bars, window task bar, and Start Menu
- **[Controls Reference](docs/CONTROLS.md)** - Complete guide to all 25+ UI controls
- **[Built-in Dialogs](docs/DIALOGS.md)** - File pickers, folder browsers, and system dialogs
- **[Theme System](docs/THEMES.md)** - Built-in themes, custom themes, and runtime switching
- **[Plugin Development](docs/PLUGINS.md)** - Creating custom plugins and using the plugin architecture
- **[State Services](docs/STATE-SERVICES.md)** - Window state, focus, modal, and notification services
- **[Fluent Builders](docs/BUILDERS.md)** - WindowBuilder and control builder APIs

## Links

- **NuGet Package**: [SharpConsoleUI](https://www.nuget.org/packages/SharpConsoleUI)
- **GitHub Repository**: [ConsoleEx](https://github.com/nickprotop/ConsoleEx)
- **Documentation**: [docs/](docs/) folder and inline XML comments
- **Issues**: [GitHub Issues](https://github.com/nickprotop/ConsoleEx/issues)

## Projects Using SharpConsoleUI

Real-world applications built with SharpConsoleUI:

| Project | Description |
|---------|-------------|
| **[ServerHub](https://github.com/nickprotop/ServerHub)** | A terminal-based control panel for Linux servers and homelabs. Features 14 bundled widgets for monitoring CPU, memory, disk, network, Docker containers, systemd services, and more. Supports custom widgets in any language and context-aware actions. |

*Using SharpConsoleUI in your project? Open a PR to add it to this list!*

---

**Made for the .NET console development community**