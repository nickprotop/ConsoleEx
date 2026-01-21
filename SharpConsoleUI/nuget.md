# SharpConsoleUI

![Version](https://img.shields.io/badge/version-2.3-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

A modern console window system for .NET 9 with fluent builders, async patterns, and built-in state services.

## Quick Start

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Controls;
using Spectre.Console;

// Initialize with driver configuration
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
{
    TopStatus = "My App",
    BottomStatus = "Ctrl+Q to Quit"
};

// Use fluent declarative building
var window = new WindowBuilder(windowSystem)
    .WithTitle("Hello World")
    .WithSize(60, 20)
    .Centered()
    .WithColors(Color.DarkBlue, Color.White)
    .AddControl(new MarkupControl(new List<string>
    {
        "[bold yellow]Welcome to SharpConsoleUI![/]",
        "",
        "A modern TUI framework for .NET"
    }))
    .Build();

windowSystem.AddWindow(window);
windowSystem.Run();
```

## Core Features

### Modern Architecture
- **Fluent Builders**: Declarative window and control building with `WindowBuilder` and `MarkupBuilder`
- **Driver Configuration**: Explicit driver initialization with `NetConsoleDriver`
- **State Services**: Built-in services for focus, modals, notifications, cursor, and more
- **Async Support**: Full async/await patterns throughout
- **Thread Safety**: Complete console lock coverage prevents ANSI sequence corruption

### Window Management
- **Multiple Windows**: Overlapping windows with proper Z-order and focus management
- **Window States**: Normal, maximized, minimized states
- **Modal Dialogs**: Stack-based modal window system
- **Notifications**: Built-in notification system with severity levels
- **Window Cycling**: Alt+1-9, Ctrl+T for quick navigation

### Rich Controls
- **MarkupControl**: Spectre.Console markup with colors and styles
- **ButtonControl**: Interactive buttons with click events
- **CheckboxControl**: Toggle controls
- **PromptControl**: Password and text input
- **TreeControl**: Hierarchical data with expand/collapse
- **ListControl**: Selectable lists with search
- **HorizontalGridControl**: Tabular data display
- **ProgressBarControl**: Determinate and indeterminate progress
- **FigleControl**: ASCII art text using Figlet fonts

### Rendering System
- **Spectre.Console Foundation**: Leverages Spectre's rendering engine
- **Double Buffering**: Flicker-free updates with `RenderMode.Buffer`
- **Dirty Regions**: Efficient partial updates
- **Theme Support**: Runtime theme switching with built-in themes

## Examples

### Fluent Window Building

```csharp
// Create a centered window with controls using fluent API
var window = new WindowBuilder(windowSystem)
    .WithTitle("User Profile")
    .WithSize(50, 15)
    .Centered()
    .Closable(true)
    .AddControl(new FigleControl { Text = "Profile" })
    .AddControl(new RuleControl())
    .AddControl(new MarkupControl(new List<string>
    {
        "[cyan]Name:[/] John Doe",
        "[cyan]Email:[/] john@example.com"
    }))
    .Build();

windowSystem.AddWindow(window);
```

### Using Built-in State Services

```csharp
// Show notifications
windowSystem.NotificationStateService.ShowNotification(
    "Success",
    "Operation completed!",
    NotificationSeverity.Success,
    duration: 3000
);

// Check modal state
if (windowSystem.ModalStateService.HasModals)
{
    // Modal window is active
}

// Access focused window
var focused = windowSystem.FocusStateService.FocusedWindow;

// Use logging service
windowSystem.LogService.LogInfo("Application started");
```

### Interactive Controls with Events

```csharp
var button = new ButtonControl
{
    Text = "[green]Click Me[/]",
    Margin = new Margin(1, 0, 0, 0)
};

button.OnClick += (sender) =>
{
    windowSystem.NotificationStateService.ShowNotification(
        "Info",
        "Button clicked!",
        NotificationSeverity.Info
    );
};

window.AddControl(button);
```

### Dynamic Content Updates

```csharp
// Create window with real-time updates
var statusControl = new MarkupControl(new List<string> { "Loading..." });
window.AddControl(statusControl);

// Update content in background task
_ = Task.Run(async () =>
{
    while (true)
    {
        var stats = new List<string>
        {
            $"[yellow]Time:[/] {DateTime.Now:HH:mm:ss}",
            $"[yellow]Memory:[/] {GC.GetTotalMemory(false) / 1024 / 1024} MB",
            $"[yellow]Threads:[/] {Process.GetCurrentProcess().Threads.Count}"
        };

        statusControl.SetContent(stats);
        await Task.Delay(1000);
    }
});
```

### Tree Control for Hierarchical Data

```csharp
var tree = new TreeControl();
var rootNode = tree.AddNode("Root");
var child1 = rootNode.AddChild("Child 1");
child1.AddChild("Grandchild 1");
child1.AddChild("Grandchild 2");
rootNode.AddChild("Child 2");

window.AddControl(tree);
```

### List Control with Selection

```csharp
var list = new ListControl();
list.AddRange(new[] { "Option 1", "Option 2", "Option 3" });

list.SelectedIndexChanged += (sender, index) =>
{
    windowSystem.NotificationStateService.ShowNotification(
        "Selection",
        $"Selected: {list.GetItem(index)}",
        NotificationSeverity.Info
    );
};

window.AddControl(list);
```

### Progress Indicators

```csharp
// Determinate progress
var progress = new ProgressBarControl
{
    Value = 0,
    MaxValue = 100,
    IsIndeterminate = false
};

window.AddControl(progress);

// Update progress
_ = Task.Run(async () =>
{
    for (int i = 0; i <= 100; i++)
    {
        progress.Value = i;
        await Task.Delay(50);
    }
});
```

### Layout Controls

```csharp
// Horizontal grid for columns
var grid = new HorizontalGridControl
{
    HorizontalAlignment = HorizontalAlignment.Stretch
};

grid.AddControl(new MarkupControl(new List<string> { "[cyan]Left[/]" }));
grid.AddControl(new MarkupControl(new List<string> { "[yellow]Center[/]" }));
grid.AddControl(new MarkupControl(new List<string> { "[green]Right[/]" }));

window.AddControl(grid);
```

## State Services

SharpConsoleUI provides centralized state management through built-in services accessible via `windowSystem`:

| Service | Purpose |
|---------|---------|
| `LogService` | Debug logging with file output |
| `NotificationStateService` | Show notifications with severity levels |
| `FocusStateService` | Track window and control focus |
| `ModalStateService` | Manage modal window stack |
| `WindowStateService` | Window registration and z-order |
| `CursorStateService` | Cursor visibility and positioning |
| `ThemeStateService` | Runtime theme management |

## Driver Configuration

Initialize with `NetConsoleDriver` for explicit control:

```csharp
// Buffer mode for smooth rendering (recommended)
var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);

// Or direct mode for immediate updates
var directDriver = new NetConsoleDriver(RenderMode.Direct);
var directSystem = new ConsoleWindowSystem(directDriver);
```

**RenderMode Options:**
- `RenderMode.Buffer` - Double-buffered, flicker-free rendering (recommended)
- `RenderMode.Direct` - Immediate console updates

## Key Bindings

- **Alt+1-9**: Activate windows 1-9
- **Ctrl+T**: Cycle through windows
- **Ctrl+Q**: Quit application
- **ESC**: Close active window
- **Tab**: Navigate between controls
- **Arrow Keys**: Navigate lists and trees
- **Mouse**: Click, drag, resize windows

## Advanced Features

### Custom Themes

```csharp
windowSystem.Theme = new Theme
{
    Name = "Custom",
    WindowBackgroundColor = Color.Navy,
    WindowForegroundColor = Color.White,
    WindowBorderColor = Color.Cyan,
    DesktopBackgroundColor = Color.Black,
    DesktopForegroundColor = Color.Grey
};
```

### Window Lifecycle Events

```csharp
window.Closing += (sender, args) =>
{
    // args.Cancel = true; // Cancel close if needed
    windowSystem.LogService.LogInfo("Window closing");
};

window.Closed += (sender) =>
{
    windowSystem.LogService.LogInfo("Window closed");
};
```

### Debug Logging

Enable file-based debug logging via environment variables:

```bash
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/ui.log
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
```

Log levels: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`

## Migration from v1.x

### Old API (v1.x)
```csharp
var system = new ConsoleWindowSystem(RenderMode.Buffer);
var window = new Window(system, WindowThreadAsync)
{
    Title = "Window",
    Left = 10,
    Top = 5,
    Width = 50,
    Height = 15
};
```

### New API (v2.x+)
```csharp
var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
var window = new WindowBuilder(system)
    .WithTitle("Window")
    .WithSize(50, 15)
    .WithPosition(10, 5)
    .Build();
```

## Requirements

- **.NET 9.0** or later
- **Spectre.Console** 0.49.1 or later
- **Terminal with ANSI support** (Windows Terminal, iTerm2, etc.)

## Resources

- **GitHub**: [github.com/nickprotop/ConsoleEx](https://github.com/nickprotop/ConsoleEx)
- **Examples**: See `Examples/DemoApp` in repository
- **Documentation**: Full README at repository root

## License

MIT License - See LICENSE file in repository

---

**Note**: Avoid console-based output (`Console.WriteLine`, console logging providers) as they corrupt UI rendering. Use file-based logging or the built-in `LogService` instead.
