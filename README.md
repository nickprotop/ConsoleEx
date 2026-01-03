# SharpConsoleUI

![Version](https://img.shields.io/badge/version-2.0-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

A modern console window system for .NET 9 with dependency injection, async patterns, and fluent builders.

## 🚀 Quick Start

### Simple Approach (Original)
```csharp
using SharpConsoleUI;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);
var window = new Window(windowSystem)
{
    Title = "Hello World",
    Width = 50,
    Height = 15
};
windowSystem.AddWindow(window);
windowSystem.Run();
```

### Modern Approach (v2.0+)
```csharp
using Microsoft.Extensions.DependencyInjection;
using SharpConsoleUI;
using SharpConsoleUI.Builders;

// Setup with dependency injection
var services = new ServiceCollection()
    .AddLogging()
    .BuildServiceProvider();

// Create window system
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);

// Use fluent builder pattern
var window = new WindowBuilder(windowSystem, services)
    .WithTitle("Modern Hello World")
    .WithSize(50, 15)
    .Centered()
    .WithColors(Color.DarkBlue, Color.White)
    .Build();

windowSystem.AddWindow(window);
await Task.Run(() => windowSystem.Run());
```

## 📋 Table of Contents

- [Installation](#-installation)
- [Core Features](#-core-features)
- [Simple API (Original)](#-simple-api-original)
- [Modern API (v2.0+)](#-modern-api-v20)
- [Architecture Overview](#-architecture-overview)
- [Examples](#-examples)
- [Advanced Features](#-advanced-features)
- [Migration Guide](#-migration-guide)
- [Contributing](#-contributing)

## 📦 Installation

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

## ✨ Core Features

### 🪟 Window Management
- **Multiple Windows**: Create and manage overlapping windows with proper Z-order
- **Window States**: Normal, maximized, minimized states
- **Window Modes**: Normal and modal dialogs
- **Focus Management**: Keyboard and mouse focus handling
- **Window Cycling**: Alt+1-9, Ctrl+T for window navigation

### 🎮 Input Handling
- **Keyboard Input**: Full keyboard support with modifier keys
- **Mouse Support**: Click, drag, and mouse event handling
- **Input Queue**: Efficient input processing system
- **Event System**: Enhanced event aggregation and handling

### 🎨 Rendering System
- **Double Buffering**: Smooth rendering without flicker
- **Dirty Regions**: Efficient partial updates
- **Render Modes**: Direct and buffered rendering
- **Themes**: Customizable appearance and colors
- **Status Bars**: Top and bottom status bar support

### 🧩 Controls Library
- **MarkupControl**: Rich text with Spectre.Console markup
- **ButtonControl**: Interactive buttons with events
- **CheckboxControl**: Toggle controls
- **MultilineEditControl**: Text editing with scrolling
- **TreeControl**: Hierarchical data display
- **ListControl**: List display and selection
- **HorizontalGridControl**: Tabular data display

## 🔧 Simple API (Original)

Perfect for getting started quickly or simple applications.

### Basic Window Creation
```csharp
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

// Create window system
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
{
    TopStatus = "My App v1.0",
    BottomStatus = "Press Ctrl+Q to quit"
};

// Create a simple window
var window = new Window(windowSystem)
{
    Title = "Simple Window",
    Left = 10,
    Top = 5,
    Width = 60,
    Height = 20,
    BackgroundColor = Color.DarkBlue,
    ForegroundColor = Color.White
};

// Add content
window.AddControl(new MarkupControl(new List<string>
{
    "[yellow]Welcome to SharpConsoleUI![/]"
}));

// Add to system and run
windowSystem.AddWindow(window);
windowSystem.Run();
```

### Adding Interactive Controls
```csharp
// Button
var button = new ButtonControl
{
    Text = "Click Me!",
    Width = 20
};
button.OnClick += (sender, e) =>
{
    // Handle button click
};
window.AddControl(button);

// Checkbox
var checkbox = new CheckboxControl("Enable Feature", false);
checkbox.CheckedChanged += (sender, e) =>
{
    // Handle checkbox change
};
window.AddControl(checkbox);

// Text input
var textInput = new MultilineEditControl
{
    ViewportHeight = 5,
    WrapMode = WrapMode.Wrap
};
window.AddControl(textInput);
```

### Event Handling (Simple)
```csharp
window.KeyPressed += (sender, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.Escape)
    {
        window.Close();
        e.Handled = true;
    }
};

window.OnClosed += (sender, e) =>
{
    // Cleanup when window closes
};
```

## 🚀 Modern API (v2.0+)

Enhanced with dependency injection, fluent builders, async patterns, and modern C# features.

### 1. Dependency Injection Setup
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpConsoleUI.DependencyInjection;
using SharpConsoleUI.Events.Enhanced;
using SharpConsoleUI.ExceptionHandling;

// Create service collection
var services = new ServiceCollection();

// Add logging - AVOID AddConsole() in UI apps!
services.AddLogging(builder =>
{
    // Use file logging, debug logging, or minimal logging for UI apps
    builder.SetMinimumLevel(LogLevel.Warning);
    // In production: builder.AddFile("logs/app-{Date}.txt");
});

// Create service container for SharpConsoleUI
var serviceContainer = new ConsoleUIServiceContainer();

// Register services
serviceContainer.RegisterService<IEventAggregator>(sp =>
    new EventAggregator(sp.GetService<ILogger<EventAggregator>>()));
serviceContainer.RegisterService<IExceptionManager>(sp =>
    new ExceptionManager(sp.GetService<ILogger<ExceptionManager>>()));

var serviceProvider = services.BuildServiceProvider();
```

### 2. Fluent Window Builders
```csharp
using SharpConsoleUI.Builders;

// Create windows using fluent API
var mainWindow = new WindowBuilder(windowSystem, serviceProvider)
    .WithTitle("🚀 Modern Application")
    .WithSize(80, 25)
    .Centered()
    .WithColors(Color.DarkBlue, Color.White)
    .Resizable()
    .Movable()
    .WithMinimumSize(60, 20)
    .Build();

// Dialog template
var dialog = new WindowBuilder(windowSystem, serviceProvider)
    .WithTitle("⚠️ Confirmation")
    .WithSize(40, 10)
    .Centered()
    .AsModal()
    .WithTemplate(new DialogTemplate("Are you sure?"))
    .Build();

// Tool window
var toolWindow = new WindowBuilder(windowSystem, serviceProvider)
    .WithTitle("🔧 Tools")
    .AtPosition(5, 5)
    .WithSize(30, 15)
    .WithTemplate(new ToolWindowTemplate("Tools", new Point(5, 5), new Size(30, 15)))
    .Build();
```

### 3. Enhanced Event System
```csharp
using SharpConsoleUI.Events.Enhanced;

// Subscribe to events with priority and async support
await eventAggregator.SubscribeAsync<WindowCreatedEvent>(
    async (eventData, cancellationToken) =>
    {
        logger.LogInformation("Window created: {WindowId}", eventData.WindowId);
        await SomeAsyncOperation(eventData.WindowId);
    },
    EventPriority.High);

// Publish events
await eventAggregator.PublishAsync(new WindowCreatedEvent(
    window.Guid,
    "MainWindow",
    DateTime.UtcNow
));

// Event data using records (immutable)
public record WindowCreatedEvent(string WindowId, string WindowType, DateTime Timestamp);
public record WindowClosedEvent(string WindowId, DateTime Timestamp);
```

### 4. Exception Handling
```csharp
using SharpConsoleUI.ExceptionHandling;

// Configure exception handling strategies
var exceptionManager = serviceProvider.GetService<IExceptionManager>();

// Configure retry strategy
exceptionManager?.ConfigureStrategy<FileNotFoundException>(
    ExceptionStrategy.Retry(maxAttempts: 3, delayMs: 1000));

// Configure fallback strategy
exceptionManager?.ConfigureStrategy<NetworkException>(
    ExceptionStrategy.Fallback(() => GetCachedData()));

// Use in application code
try
{
    await SomeRiskyOperation();
}
catch (Exception ex)
{
    var result = await exceptionManager.HandleExceptionAsync(ex);
    if (!result.Handled)
    {
        // Handle unrecoverable error
        throw;
    }
}
```

### 5. Async Patterns
```csharp
// Async window thread
var window = new WindowBuilder(windowSystem, serviceProvider)
    .WithTitle("Async Demo")
    .WithAsyncWindowThread(async window =>
    {
        while (!window.IsClosed)
        {
            // Update UI with real-time data
            var data = await GetRealTimeDataAsync();
            UpdateWindowContent(window, data);

            await Task.Delay(1000); // Update every second
        }
    })
    .Build();

// Background tasks with proper cleanup
var cts = new CancellationTokenSource();
var backgroundTask = Task.Run(async () =>
{
    await ProcessBackgroundDataAsync(cts.Token);
}, cts.Token);

window.OnClosed += (sender, e) =>
{
    cts.Cancel(); // Clean cancellation
};
```

### 6. Resource Management
```csharp
using SharpConsoleUI.Core;

// Automatic resource disposal
using var disposableManager = new DisposableManager(logger);

// Register resources for automatic cleanup
var window1 = disposableManager.Register(CreateWindow("Window 1"));
var window2 = disposableManager.Register(CreateWindow("Window 2"));

// Register custom cleanup actions
disposableManager.RegisterDisposalAction(() =>
{
    logger.LogInformation("Performing custom cleanup");
});

// Create scoped disposals
using var scope = disposableManager.CreateScope();
scope.Register(temporaryWindow);
scope.RegisterDisposalAction(() => SaveTempData());
// Scope automatically disposes when using block exits
```

### 7. Configuration System
```csharp
using Microsoft.Extensions.Configuration;
using SharpConsoleUI.Configuration;

// Load configuration from JSON
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Configure themes from config
var themeConfig = configuration.GetSection("UI:Theme");
var customTheme = new Theme
{
    WindowBackgroundColor = Enum.Parse<Color>(themeConfig["BackgroundColor"]),
    WindowForegroundColor = Enum.Parse<Color>(themeConfig["ForegroundColor"]),
    ActiveBorderColor = Enum.Parse<Color>(themeConfig["BorderColor"])
};

windowSystem.Theme = customTheme;
```

## 🏗️ Architecture Overview

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    SharpConsoleUI Architecture              │
├─────────────────────────────────────────────────────────────┤
│  Application Layer (Your Code)                             │
│  ├── Dependency Injection (IServiceProvider)               │
│  ├── Configuration (IConfiguration)                        │
│  └── Logging (ILogger)                                     │
├─────────────────────────────────────────────────────────────┤
│  Framework Layer                                           │
│  ├── Window Builders (Fluent API)                         │
│  ├── Event System (IEventAggregator)                      │
│  ├── Exception Handling (IExceptionManager)               │
│  ├── Plugin System (IPlugin)                              │
│  └── Resource Management (DisposableManager)              │
├─────────────────────────────────────────────────────────────┤
│  Core UI Layer                                            │
│  ├── ConsoleWindowSystem (Window Management)              │
│  ├── Window (Container & Rendering)                       │
│  ├── Controls (UI Components)                             │
│  └── Themes (Appearance)                                  │
├─────────────────────────────────────────────────────────────┤
│  Driver Layer                                             │
│  ├── IConsoleDriver (Abstraction)                        │
│  ├── NetConsoleDriver (Implementation)                   │
│  └── Input/Output Handling                               │
└─────────────────────────────────────────────────────────────┘
```

### Modern C# Features Used
- **Records**: Immutable data structures (WindowBounds, InputEvent, etc.)
- **Nullable Reference Types**: Explicit null handling
- **Pattern Matching**: Enhanced switch expressions
- **Async/Await**: Throughout the framework
- **Top-level Programs**: Simplified entry points
- **Init-only Properties**: Immutable initialization
- **Primary Constructors**: Concise record definitions

## 📖 Examples

### Complete Modern Application
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace MyApp;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup DI
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning)) // No console output!
            .BuildServiceProvider();

        var logger = services.GetService<ILogger<Program>>();

        try
        {
            // Create window system
            var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "🚀 My Modern App",
                BottomStatus = "ESC: Close | F1: Help"
            };

            // Main window with fluent builder
            var mainWindow = new WindowBuilder(windowSystem, services)
                .WithTitle("📋 Task Manager")
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
                "• Modern async patterns",
                "",
                "[dim]Press F2 to add a new task[/]"
            }));

            // Setup key handlers
            mainWindow.KeyPressed += async (sender, e) =>
            {
                switch (e.KeyInfo.Key)
                {
                    case ConsoleKey.F2:
                        await CreateAddTaskWindow(windowSystem, services);
                        e.Handled = true;
                        break;
                    case ConsoleKey.Escape:
                        windowSystem.CloseWindow(mainWindow);
                        e.Handled = true;
                        break;
                }
            };

            windowSystem.AddWindow(mainWindow);

            logger?.LogInformation("Starting application");
            await Task.Run(() => windowSystem.Run());

            return 0;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Application error");
            return 1;
        }
    }

    static async Task CreateAddTaskWindow(ConsoleWindowSystem windowSystem, IServiceProvider services)
    {
        var taskWindow = new WindowBuilder(windowSystem, services)
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
        // Implement task saving logic - DON'T use Console.WriteLine in UI apps!
        // Instead use:
        // 1. Logging service
        // 2. Database/file storage
        // 3. Update UI elements

        // Example proper approaches:
        // logger?.LogInformation("Task saved: {TaskDescription}", taskDescription);
        // await taskRepository.SaveAsync(new Task { Description = taskDescription });
        // or update a status label in the UI instead of console output
    }
}
```

### Real-time Data Window
```csharp
public static async Task CreateRealtimeWindow(ConsoleWindowSystem windowSystem, IServiceProvider services)
{
    var dataWindow = new WindowBuilder(windowSystem, services)
        .WithTitle("📊 Real-time Data")
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
                "[bold blue]📊 System Metrics[/]",
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

## 🎯 Advanced Features

### Plugin Development
```csharp
using SharpConsoleUI.Plugins;

// Create custom plugin
public class MyCustomPlugin : IControlPlugin
{
    public string Name => "My Custom Plugin";
    public Version Version => new(1, 0, 0);
    public string Description => "Adds custom functionality";

    public Task InitializeAsync(IServiceProvider services)
    {
        // Initialize plugin
        return Task.CompletedTask;
    }

    public IWIndowControl CreateControl(string type, object? parameters = null)
    {
        return type switch
        {
            "CustomControl" => new MyCustomControl(),
            _ => throw new ArgumentException($"Unknown control type: {type}")
        };
    }

    public Task ShutdownAsync()
    {
        // Cleanup
        return Task.CompletedTask;
    }
}

// Register plugin
var pluginManager = new PluginManager(services.GetService<ILogger<PluginManager>>());
await pluginManager.LoadPluginAsync<MyCustomPlugin>();
```

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

## 🔄 Migration Guide

### From v1.x to v2.0

#### Simple Migration (No Breaking Changes)
Your existing v1.x code continues to work as-is:

```csharp
// v1.x code (still works)
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);
var window = new Window(windowSystem);
window.Title = "My Window";
windowSystem.AddWindow(window);
windowSystem.Run();
```

#### Enhanced Migration (Recommended)
Gradually adopt new features:

```csharp
// Step 1: Add DI (optional)
var services = new ServiceCollection()
    .AddLogging()
    .BuildServiceProvider();

// Step 2: Use fluent builders (optional)
var window = new WindowBuilder(windowSystem, services)
    .WithTitle("My Window")
    .Build();

// Step 3: Add async patterns (optional)
await Task.Run(() => windowSystem.Run());
```

### Feature Mapping

| v1.x Feature | v2.0 Equivalent | Enhancement |
|--------------|-----------------|-------------|
| `new Window()` | `new WindowBuilder().Build()` | Fluent API |
| Event handling | Event handlers + EventAggregator | Async events |
| Manual cleanup | DisposableManager | Auto cleanup |
| Try/catch | ExceptionManager | Strategy patterns |
| Static config | IConfiguration | External config |

## 🤝 Contributing

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
# Simple example
cd Example
dotnet run

# Modern example (shows all v2.0 features)
cd Examples/ModernExample
dotnet run
```

### Project Structure
```
ConsoleEx/
├── SharpConsoleUI/           # Main library
│   ├── Core/                 # State services & infrastructure
│   ├── Logging/              # Debug logging system
│   ├── Controls/             # UI controls
│   ├── Builders/             # Fluent builders
│   ├── DependencyInjection/  # DI system
│   ├── Events/               # Event system
│   ├── ExceptionHandling/    # Exception management
│   ├── Plugins/              # Plugin system
│   └── Themes/               # Theming system
├── Example/                  # Simple examples
├── Examples/
│   └── ModernExample/        # Modern features demo
└── Tests/                    # Unit tests
```

### Coding Standards
- Follow C# coding conventions
- Use modern C# features (records, nullable refs, etc.)
- Include XML documentation
- Add unit tests for new features
- Maintain backward compatibility

### ⚠️ Critical: Console Output & Logging
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

**Example Safe Logging Configuration:**
```csharp
services.AddLogging(builder =>
{
    // ❌ DON'T: builder.AddConsole(); // Corrupts UI!

    // ✅ DO: Use file/debug/event logging
    builder.SetMinimumLevel(LogLevel.Warning);
    // In production: add file logging package
    // builder.AddFile("logs/app-{Date}.txt");
});
```

### 🔧 Built-in Debug Logging

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

### 🔔 Notifications

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

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🙏 Acknowledgments

- Built on [Spectre.Console](https://github.com/spectreconsole/spectre.console) for rich console output
- Inspired by traditional GUI frameworks adapted for console applications
- Uses Microsoft.Extensions.* for modern .NET patterns

---

## 🔗 Links

- **NuGet Package**: [SharpConsoleUI](https://www.nuget.org/packages/SharpConsoleUI)
- **GitHub Repository**: [ConsoleEx](https://github.com/nickprotop/ConsoleEx)
- **Documentation**: This README and inline XML comments
- **Issues**: [GitHub Issues](https://github.com/nickprotop/ConsoleEx/issues)

---

**Made with ❤️ for the .NET console development community**