# Status Bars and Start Menu System

SharpConsoleUI provides a comprehensive status system including top and bottom status bars, window task lists, and a Windows-like Start Menu.

## Table of Contents

- [Overview](#overview)
- [Status Bars](#status-bars)
  - [Top Status Bar](#top-status-bar)
  - [Bottom Status Bar](#bottom-status-bar)
  - [Window Task Bar](#window-task-bar)
  - [Performance Metrics](#performance-metrics)
- [Start Menu System](#start-menu-system)
  - [Configuration](#start-menu-configuration)
  - [Registering Actions](#registering-actions)
  - [Built-in Categories](#built-in-categories)
  - [Plugin Integration](#plugin-integration)
- [Complete Examples](#complete-examples)

## Overview

The status system provides:
- **Top Status Bar**: Application title, branding, performance metrics
- **Bottom Status Bar**: Status messages, window list, Start button
- **Window Task Bar**: Quick access to open windows (Alt+1-9)
- **Start Menu**: Centralized access to actions, windows, and system features

## Status Bars

### Top Status Bar

The top status bar appears at the very top of the screen, above all windows.

#### Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowTopStatus: true  // Enable/disable top status bar
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// Set top status text (supports Spectre.Console markup)
windowSystem.TopStatus = "[bold cyan]My Application[/] - [yellow]Connected[/]";
```

#### Content Priority

The top status bar displays (in order of priority):
1. **Performance Metrics** (if `EnablePerformanceMetrics: true`)
   - `FPS: 60 | Frame: 16.67ms | Windows: 3 | Dirty: 1`
2. **Start Button** (if `ShowStartButton: true` and `StartButtonLocation: Top`)
   - Appears on left or right based on `StartButtonPosition`
3. **Custom Status Text** (`TopStatus` property)
   - Fills remaining space

#### Usage Examples

**Application Title:**
```csharp
windowSystem.TopStatus = "[bold]My Application v1.0[/]";
```

**Real-time Status Updates:**
```csharp
// Update connection status
windowSystem.TopStatus = $"[green]Connected[/] to server at {DateTime.Now:HH:mm:ss}";

// Update progress
windowSystem.TopStatus = $"Processing... [yellow]{progress}%[/]";
```

**Rich Markup:**
```csharp
windowSystem.TopStatus = "[bold cyan]Dashboard[/] | CPU: [green]23%[/] | Memory: [yellow]45%[/]";
```

### Bottom Status Bar

The bottom status bar appears at the bottom of the screen, below all windows.

#### Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowBottomStatus: true  // Enable/disable bottom status bar
    )
);

windowSystem.BottomStatus = "Ready | [dim]Press F1 for help[/]";
```

#### Content Priority

The bottom status bar displays (left to right):
1. **Start Button** (if `ShowStartButton: true` and `StartButtonLocation: Bottom`)
   - Position controlled by `StartButtonPosition` (Left or Right)
2. **Window Task Bar** (if `ShowTaskBar: true`)
   - `Alt-1 Window 1 | Alt-2 Window 2 | ...`
3. **Custom Status Text** (`BottomStatus` property)
   - Fills remaining space

#### Layout Examples

**Left Start Button + Task Bar:**
```
☰ Start | Alt-1 Main | Alt-2 Editor | Alt-3 Terminal | Ready
```

**Right Start Button + Task Bar:**
```
Alt-1 Main | Alt-2 Editor | Ready                    ☰ Start
```

**No Start Button, Task Bar Only:**
```
Alt-1 Main | Alt-2 Editor | Alt-3 Terminal | Ready
```

**Custom Status Only:**
```
Connected to database | 42 records loaded | Ready
```

### Window Task Bar

The window task bar shows all open windows with keyboard shortcuts for quick switching.

#### Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowTaskBar: true  // Enable window task bar
    )
);
```

#### Features

**Keyboard Shortcuts:**
- `Alt+1` - Switch to first window
- `Alt+2` - Switch to second window
- ... up to `Alt+9`

**Visual Indicators:**
- **Normal**: `Alt-1 Window Title`
- **Minimized**: `Alt-1 [dim]Window Title[/]` (dimmed)
- **Truncation**: Long titles truncated with ellipsis

**Window Filtering:**
- Only shows top-level windows (not child windows)
- Updates automatically when windows are added/removed

#### Usage

```csharp
// Task bar appears automatically with open windows
var window1 = new Window(windowSystem) { Title = "Main Window" };
var window2 = new Window(windowSystem) { Title = "Editor" };
var window3 = new Window(windowSystem) { Title = "Terminal" };

windowSystem.AddWindow(window1);
windowSystem.AddWindow(window2);
windowSystem.AddWindow(window3);

// Bottom status now shows:
// Alt-1 Main Window | Alt-2 Editor | Alt-3 Terminal
```

#### Disable Task Bar

Hide window list from status bar (useful when using Start Menu instead):

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowTaskBar: false,           // Hide from status bar
        ShowWindowListInMenu: true    // Show in Start Menu instead
    )
);
```

### Performance Metrics

Real-time performance metrics in the top status bar.

#### Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: true  // Show metrics
);
```

#### Metrics Display

```
FPS: 60 | Frame: 16.67ms | Windows: 3 | Dirty: 1
```

- **FPS**: Current frames per second
- **Frame**: Milliseconds to render last frame
- **Windows**: Total number of windows
- **Dirty**: Windows needing re-render

#### Runtime Toggle

Users can toggle metrics via:
- Start Menu > System > Toggle Performance Metrics
- Or programmatically:

```csharp
// Access via options (runtime change not directly supported)
// Use Start Menu or rebuild system with new options
```

#### Environment Variable

Enable via environment variable:

```bash
export SHARPCONSOLEUI_PERF_METRICS=true
dotnet run
```

## Start Menu System

Windows-like Start Menu providing centralized access to actions, windows, and system features.

### Start Menu Configuration

#### Enabling the Start Button

The Start button is **disabled by default** (opt-in):

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: true,                           // Enable Start button
        StartButtonLocation: StatusBarLocation.Bottom,   // Top or Bottom
        StartButtonPosition: StartButtonPosition.Left,   // Left or Right
        StartButtonText: "☰ Start",                      // Button text
        StartMenuShortcutKey: ConsoleKey.M,              // Shortcut key (Ctrl+M)
        StartMenuShortcutModifiers: ConsoleModifiers.Control,
        ShowSystemMenuCategory: true,                    // Show System category
        ShowWindowListInMenu: true                       // Show Windows category
    )
);
```

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ShowStartButton` | `bool` | `false` | Show/hide the Start button |
| `StartButtonLocation` | `StatusBarLocation` | `Bottom` | Top or Bottom status bar |
| `StartButtonPosition` | `StartButtonPosition` | `Left` | Left or Right side |
| `StartButtonText` | `string` | `"☰ Start"` | Button display text |
| `StartMenuShortcutKey` | `ConsoleKey` | `M` | Keyboard shortcut key |
| `StartMenuShortcutModifiers` | `ConsoleModifiers` | `Control` | Modifier keys (Ctrl, Alt, Shift) |
| `ShowSystemMenuCategory` | `bool` | `true` | Show System category |
| `ShowWindowListInMenu` | `bool` | `true` | Show Windows category |

#### Placement Examples

**Bottom-Left (Windows-style):**
```csharp
StartButtonLocation: StatusBarLocation.Bottom,
StartButtonPosition: StartButtonPosition.Left
```
Result: `☰ Start | Alt-1 Window | Status text`

**Top-Right:**
```csharp
StartButtonLocation: StatusBarLocation.Top,
StartButtonPosition: StartButtonPosition.Right
```
Result: `Application Title                    ☰ Start`

**Custom Shortcut:**
```csharp
StartMenuShortcutKey: ConsoleKey.Escape,
StartMenuShortcutModifiers: ConsoleModifiers.None  // Just Escape key
```

### Registering Actions

Register custom actions organized by category.

#### Basic Action Registration

```csharp
windowSystem.RegisterStartMenuAction(
    actionName: "New Document",
    action: () =>
    {
        var window = new Window(windowSystem)
        {
            Title = "New Document",
            Width = 60,
            Height = 20
        };
        windowSystem.AddWindow(window);
    },
    category: "File",  // Category for organization
    order: 10          // Display order (lower = higher in menu)
);
```

#### Multiple Categories

```csharp
// File category
windowSystem.RegisterStartMenuAction("New Document", CreateNewDoc, "File", 10);
windowSystem.RegisterStartMenuAction("Open File", OpenFile, "File", 20);
windowSystem.RegisterStartMenuAction("Save", Save, "File", 30);

// Edit category
windowSystem.RegisterStartMenuAction("Undo", Undo, "Edit", 10);
windowSystem.RegisterStartMenuAction("Redo", Redo, "Edit", 20);
windowSystem.RegisterStartMenuAction("Preferences", ShowPrefs, "Edit", 30);

// Tools category
windowSystem.RegisterStartMenuAction("Calculator", OpenCalc, "Tools", 10);
windowSystem.RegisterStartMenuAction("Terminal", OpenTerminal, "Tools", 20);

// Help category
windowSystem.RegisterStartMenuAction("User Guide", ShowGuide, "Help", 10);
windowSystem.RegisterStartMenuAction("About", ShowAbout, "Help", 20);
```

### Built-in Categories

#### System Category

**Enabled by default** via `ShowSystemMenuCategory: true`. Provides:

- **Switch Theme** - Theme selector dialog
- **Settings** - System settings window
- **About** - Application information
- **Toggle Performance Metrics** - Show/hide FPS and metrics
- **Toggle Frame Rate Limiting** - Enable/disable FPS limiting
- **Toggle FPS Display** - Show/hide FPS counter

Disable System category:
```csharp
StatusBarOptions: new StatusBarOptions(
    ShowStartButton: true,
    ShowSystemMenuCategory: false  // Hide System actions
)
```

#### Windows Category

**Enabled by default** via `ShowWindowListInMenu: true`. Provides:

- List of all open windows
- Alt+1-9 shortcuts shown
- Click to activate/focus
- Minimized windows shown dimmed

Disable Windows category:
```csharp
StatusBarOptions: new StatusBarOptions(
    ShowStartButton: true,
    ShowWindowListInMenu: false  // Hide window list from Start Menu
)
```

**Note:** When disabled, windows still appear in bottom status bar if `ShowTaskBar: true`.

### Plugin Integration

Plugins can provide their own actions and windows in the Start Menu.

#### Plugin-Provided Actions

```csharp
// Load DeveloperTools plugin
windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();

// Plugin actions appear automatically:
// Debug > Clear Logs
// Debug > Export Diagnostics
// Debug > Toggle Performance Overlay

// Plugin windows appear:
// Developer Tools > Debug Console
// Developer Tools > Log Exporter
```

#### Creating Action Providers

```csharp
public class MyPlugin : IPlugin, IActionProvider
{
    public IEnumerable<StartMenuAction> GetActions()
    {
        yield return new StartMenuAction
        {
            Name = "My Custom Action",
            Category = "MyPlugin",
            Order = 10,
            Action = () => { /* action logic */ }
        };
    }
}
```

### User Interaction

#### Opening the Start Menu

**Keyboard:**
- Press `Ctrl+M` (default)
- Customizable via configuration

**Mouse:**
- Click `☰ Start` button in status bar

#### Navigation

**Keyboard:**
- `↑` / `↓` - Navigate categories and items
- `→` - Open category/submenu
- `←` - Close submenu
- `Enter` - Select action
- `Escape` - Close menu

**Mouse:**
- Hover to highlight
- Click to select
- Click outside to dismiss

#### Menu Behavior

- **Full-screen Overlay**: Dims underlying windows
- **Click Outside**: Dismisses menu
- **Auto-close**: Closes after action execution
- **Escape**: Always closes menu

## Complete Examples

### Example 1: Simple Application with Task Bar

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
{
    TopStatus = "[bold]My Application[/]",
    BottomStatus = "Ready"
};

// Task bar shows automatically with windows
// Bottom: Alt-1 Window1 | Alt-2 Window2 | Ready
```

### Example 2: Windows-like with Start Menu

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: true,
        StartButtonLocation: StatusBarLocation.Bottom,
        StartButtonPosition: StartButtonPosition.Left,
        ShowWindowListInMenu: true,  // Windows in Start Menu
        ShowTaskBar: false           // Not in status bar
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options)
{
    TopStatus = "[bold cyan]My App[/] - Press [yellow]Ctrl+M[/] for menu",
    BottomStatus = ""
};

// Register actions
windowSystem.RegisterStartMenuAction("New", CreateNew, "File", 10);
windowSystem.RegisterStartMenuAction("Open", OpenFile, "File", 20);

// Bottom: ☰ Start | [custom status]
// Start Menu shows File > New, File > Open, Windows > [list], System > [actions]
```

### Example 3: Dashboard with Performance Metrics

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: true,
    TargetFPS: 30,
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: false,
        ShowTaskBar: false,
        ShowTopStatus: true,
        ShowBottomStatus: true
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options)
{
    TopStatus = "",  // Metrics appear here automatically
    BottomStatus = "Press [yellow]F10[/] to exit"
};

// Top: FPS: 30 | Frame: 33.33ms | Windows: 1 | Dirty: 0
// Bottom: Press F10 to exit
```

### Example 4: Full-featured with Everything

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: true,
    EnableFrameRateLimiting: true,
    TargetFPS: 60,
    StatusBarOptions: new StatusBarOptions(
        // Start Menu enabled
        ShowStartButton: true,
        StartButtonLocation: StatusBarLocation.Bottom,
        StartButtonPosition: StartButtonPosition.Left,
        StartButtonText: "☰ Menu",
        StartMenuShortcutKey: ConsoleKey.M,
        StartMenuShortcutModifiers: ConsoleModifiers.Control,

        // Content options
        ShowSystemMenuCategory: true,
        ShowWindowListInMenu: true,
        ShowTaskBar: true,  // Show both in menu and status bar

        // Status bar visibility
        ShowTopStatus: true,
        ShowBottomStatus: true
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options)
{
    TopStatus = "[bold cyan]Application Suite[/]",
    BottomStatus = ""
};

// Load plugins
windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();

// Register custom actions
windowSystem.RegisterStartMenuAction("New Project", CreateProject, "File", 10);
windowSystem.RegisterStartMenuAction("Open Project", OpenProject, "File", 20);
windowSystem.RegisterStartMenuAction("Settings", ShowSettings, "Edit", 10);
windowSystem.RegisterStartMenuAction("Terminal", OpenTerminal, "Tools", 10);

// Top: FPS: 60 | Frame: 16.67ms | Windows: 3 | Dirty: 1 | Application Suite
// Bottom: ☰ Menu | Alt-1 Main | Alt-2 Editor | Alt-3 Terminal
```

### Example 5: Minimal (No Status System)

```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowTopStatus: false,
        ShowBottomStatus: false
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// No status bars at all - maximum screen space
```

## Best Practices

### Status Bars

1. **Keep Text Concise**: Status bars have limited space
2. **Use Markup Sparingly**: Too much markup reduces readability
3. **Update Appropriately**: Don't update status text every frame (causes flicker)
4. **Provide Context**: Use status for current state/activity
5. **Match Conventions**: Use familiar patterns (Ready, Connected, Processing...)

### Task Bar

1. **Meaningful Titles**: Window titles should be descriptive (shown in task bar)
2. **Limit Windows**: Alt+1-9 supports up to 9 windows
3. **Consider Placement**: Bottom task bar is most familiar to users
4. **Task Bar vs Start Menu**: Choose one or both based on application needs

### Start Menu

1. **Logical Categories**: Group related actions (File, Edit, Tools, Help)
2. **Meaningful Names**: Use action verbs (New, Open, Save, not "Document Creation")
3. **Consistent Ordering**: Order by frequency of use (most used = lower order number)
4. **Avoid Duplication**: Don't duplicate window list in both task bar and Start Menu
5. **Plugin Awareness**: Leave room for plugin categories

### Performance

1. **Enable Frame Limiting**: Use `TargetFPS: 30-60` for most applications
2. **Metrics for Development**: Enable metrics during development only
3. **Monitor Status Updates**: Excessive status text updates hurt performance
4. **Optimize Actions**: Keep Start Menu actions fast (< 100ms)

## See Also

- [Configuration Guide](CONFIGURATION.md) - Full configuration reference
- [Plugin Development](PLUGINS.md) - Creating plugins with actions
- [State Services](STATE-SERVICES.md) - Using notification and window services
- [Dialogs](DIALOGS.md) - Built-in system dialogs
