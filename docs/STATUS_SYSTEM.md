# Panels, Task Bar, and Start Menu

SharpConsoleUI provides a configurable panel system for screen-level UI — top and bottom bars with status text, window task lists, clocks, performance metrics, and a Windows-like Start Menu.

## Table of Contents

- [Overview](#overview)
- [Panels](#panels)
  - [Top Panel](#top-panel)
  - [Bottom Panel](#bottom-panel)
  - [Window Task Bar](#window-task-bar)
  - [Performance Metrics](#performance-metrics)
- [Start Menu System](#start-menu-system)
  - [Configuration](#start-menu-configuration)
  - [Registering Actions](#registering-actions)
  - [Built-in Categories](#built-in-categories)
  - [Plugin Integration](#plugin-integration)
- [Complete Examples](#complete-examples)

## Overview

The panel system provides:
- **Top Panel**: Application title, branding, performance metrics, clock
- **Bottom Panel**: Start button, window task bar, status messages, clock
- **Window Task Bar**: Quick access to open windows (Alt+1-9)
- **Start Menu**: Centralized access to actions, windows, and system features

Panels are composed from **elements** using a fluent builder. See the [Panel System guide](PANELS.md) for the full element reference.

> **Note:** For the in-window `StatusBarControl` (three-zone bar with clickable items and shortcut hints), see the [StatusBarControl documentation](controls/StatusBarControl.md).

## Panels

### Top Panel

The top panel appears at the very top of the screen, above all windows.

#### Configuration

```csharp
using SharpConsoleUI.Panel;

var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]My Application[/]"))
        .Left(Elements.Separator())
        .Left(Elements.StatusText("[dim]Ctrl+T: Theme[/]"))
        .Right(Elements.Performance())
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

#### Runtime Status Text

```csharp
// Update the first StatusTextElement in the top panel
windowSystem.PanelStateService.TopStatus = "[bold]My Application v1.0[/]";

// Or find and update a specific element
var title = windowSystem.PanelStateService.TopPanel?.FindElement<StatusTextElement>("title");
if (title != null) title.Text = "[green]Connected[/]";
```

#### Usage Examples

**Application Title:**
```csharp
TopPanelConfig: panel => panel
    .Left(Elements.StatusText("[bold]My Application v1.0[/]"))
```

**Title with Clock and Metrics:**
```csharp
TopPanelConfig: panel => panel
    .Left(Elements.StatusText("[bold cyan]Dashboard[/]"))
    .Right(Elements.Performance())
    .Right(Elements.Separator())
    .Right(Elements.Clock().WithFormat("HH:mm:ss"))
```

### Bottom Panel

The bottom panel appears at the bottom of the screen, below all windows.

#### Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu())
        .Center(Elements.TaskBar())
        .Right(Elements.Clock())
);

windowSystem.PanelStateService.BottomStatus = "Ready | [dim]Press F1 for help[/]";
```

#### Layout Examples

**Start Menu + Task Bar + Clock:**
```
☰ Start | Alt-1 Main | Alt-2 Editor | Alt-3 Terminal     14:30:00
```

**Task Bar Only:**
```
Alt-1 Main | Alt-2 Editor | Alt-3 Terminal
```

**Status Text Only:**
```
Connected to database | 42 records loaded | Ready
```

### Window Task Bar

The `TaskBar` element shows all open windows with keyboard shortcuts for quick switching.

#### Configuration

```csharp
BottomPanelConfig: panel => panel
    .Center(Elements.TaskBar()
        .WithActiveColor(Color.Cyan1)
        .WithMinimizedDim(true))
```

#### Features

**Keyboard Shortcuts:**
- `Alt+1` - Switch to first window
- `Alt+2` - Switch to second window
- ... up to `Alt+9`

**Visual Indicators:**
- **Normal**: `Alt-1 Window Title`
- **Minimized**: Dimmed text
- **Truncation**: Long titles truncated with ellipsis

**Window Filtering:**
- Only shows top-level windows (not child windows or the Start Menu)
- Updates automatically when windows are added/removed
- Click to activate/focus a window

### Performance Metrics

Real-time performance metrics via the `Performance` element.

#### Configuration

```csharp
TopPanelConfig: panel => panel
    .Right(Elements.Performance()
        .ShowFPS(true)
        .ShowDirtyChars(true))
```

#### Display

The performance element shows FPS and dirty character counts, auto-updating every 250ms.

#### Environment Variable

Enable performance metrics tracking via environment variable:

```bash
export SHARPCONSOLEUI_PERF_METRICS=true
dotnet run
```

## Start Menu System

Windows-like Start Menu providing centralized access to actions, windows, and system features. The Start Menu is added as a `StartMenu` element on a panel.

### Start Menu Configuration

#### Adding a Start Menu

```csharp
using SharpConsoleUI.Panel;

var options = new ConsoleWindowSystemOptions(
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithText("☰ Start")
            .WithShortcutKey(ConsoleKey.Spacebar, ConsoleModifiers.Control)
            .WithOptions(new StartMenuOptions
            {
                Layout = StartMenuLayout.TwoColumn,
                AppName = "My App",
                AppVersion = "1.0.0"
            }))
        .Center(Elements.TaskBar())
);
```

#### StartMenuOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Layout` | `StartMenuLayout` | `TwoColumn` | Layout mode (see below) |
| `AppName` | `string?` | `null` | App name in header (defaults to "SharpConsoleUI") |
| `AppVersion` | `string?` | `null` | App version in header (defaults to library version) |
| `ShowIcons` | `bool` | `true` | Show Unicode icons in header and exit row |
| `HeaderIcon` | `string` | `"☰"` | Icon next to app name in header |
| `ShowSystemCategory` | `bool` | `true` | Show System category |
| `ShowWindowList` | `bool` | `true` | Show Windows list |
| `SidebarStyle` | `StartMenuSidebarStyle` | `IconLabel` | Sidebar display style |
| `BackgroundGradient` | `GradientBackground?` | `null` | Optional gradient background |

#### Layout Modes

- **`TwoColumn`** (default): Left column has quick actions and category submenus; right column has a live window list with Alt+N shortcuts and an info strip showing theme, window count, and plugin count.
- **`SingleColumn`**: Compact vertical menu. Windows appear as a flyout submenu instead of an inline list.

#### Placement

Place the Start Menu in any panel zone:

**Bottom-Left (Windows-style):**
```csharp
BottomPanelConfig: panel => panel
    .Left(Elements.StartMenu())
    .Center(Elements.TaskBar())
```

**Top-Right:**
```csharp
TopPanelConfig: panel => panel
    .Left(Elements.StatusText("[bold]My App[/]"))
    .Right(Elements.StartMenu())
```

### Registering Actions

Actions are registered directly on the `StartMenuElement` instance.

#### Basic Action Registration

```csharp
// Get the StartMenuElement from the panel
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;

// Register actions by category
startMenu.RegisterAction("New Document", () =>
{
    var window = new Window(windowSystem) { Title = "New Document", Width = 60, Height = 20 };
    windowSystem.AddWindow(window);
}, category: "File", order: 10);
```

#### Multiple Categories

```csharp
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;

// File category
startMenu.RegisterAction("New Document", () => CreateNewDoc(), "File", 10);
startMenu.RegisterAction("Open File", () => OpenFile(), "File", 20);
startMenu.RegisterAction("Save", () => Save(), "File", 30);

// Edit category
startMenu.RegisterAction("Undo", () => Undo(), "Edit", 10);
startMenu.RegisterAction("Preferences", () => ShowPrefs(), "Edit", 20);

// Tools category
startMenu.RegisterAction("Calculator", () => OpenCalc(), "Tools", 10);
startMenu.RegisterAction("Terminal", () => OpenTerminal(), "Tools", 20);

// Help category
startMenu.RegisterAction("User Guide", () => ShowGuide(), "Help", 10);
startMenu.RegisterAction("About", () => ShowAbout(), "Help", 20);
```

### Built-in Categories

#### System Category

**Enabled by default** via `StartMenuOptions.ShowSystemCategory`. Provides:

- **Change Theme...** - Theme selector dialog
- **Settings...** - System settings window
- **About...** - Application information
- **Performance** > Toggle Metrics, Toggle Frame Limiting, Set Target FPS...

Disable System category:
```csharp
new StartMenuOptions { ShowSystemCategory = false }
```

#### Windows Category

**Enabled by default** via `ShowWindowList: true`. Provides:

- List of all open windows
- Alt+1-9 shortcuts shown
- Click to activate/focus
- Minimized windows shown dimmed

Disable Windows list:
```csharp
new StartMenuOptions { ShowWindowList = false }
```

### Plugin Integration

Plugins can provide their own actions and windows in the Start Menu.

#### Plugin-Provided Actions

```csharp
// Load DeveloperTools plugin
windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();

// Plugin actions appear automatically in the Start Menu:
// Debug > Clear Logs
// Debug > Export Diagnostics
// Debug > Toggle Performance Overlay
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
- Press `Ctrl+Space` (default)
- Customizable via `.WithShortcutKey()` on the builder

**Mouse:**
- Click the `☰ Start` button in the panel

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

- **Window-based**: The Start menu is a borderless, always-on-top window
- **Click Outside**: Closes automatically when deactivated
- **Auto-close**: Closes after action execution
- **Escape**: Always closes menu
- **Toggle**: Clicking the Start button again closes the menu
- **Hidden from TaskBar**: Does not appear in the task bar or Alt+N window list

## Complete Examples

### Example 1: Simple Application with Task Bar

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Panel;

var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold]My Application[/]")),
    BottomPanelConfig: panel => panel
        .Center(Elements.TaskBar())
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// Task bar shows automatically with windows
// Bottom: Alt-1 Window1 | Alt-2 Window2
```

### Example 2: Windows-like with Start Menu

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]My App[/] - Press [yellow]Ctrl+Space[/] for menu")),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithOptions(new StartMenuOptions { AppName = "My App", AppVersion = "1.0.0" }))
        .Center(Elements.TaskBar())
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// Register actions
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;
startMenu.RegisterAction("New", () => CreateNew(), "File", 10);
startMenu.RegisterAction("Open", () => OpenFile(), "File", 20);
```

### Example 3: Dashboard with Performance Metrics

```csharp
var options = new ConsoleWindowSystemOptions(
    TargetFPS: 30,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText(""))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StatusText("Press [yellow]F10[/] to exit"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

windowSystem.PanelStateService.TopStatus = "[bold cyan]System Dashboard[/]";
```

### Example 4: Full-featured with Everything

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]Application Suite[/]"))
        .Right(Elements.Performance())
        .Right(Elements.Separator())
        .Right(Elements.Clock().WithFormat("HH:mm:ss")),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithText("☰ Menu")
            .WithOptions(new StartMenuOptions
            {
                AppName = "My IDE",
                AppVersion = "3.0.0",
                SidebarStyle = StartMenuSidebarStyle.IconLabel,
                BackgroundGradient = new GradientBackground(
                    ColorGradient.FromColors(new Color(25, 25, 60), new Color(15, 15, 35)),
                    GradientDirection.Vertical)
            }))
        .Center(Elements.TaskBar())
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// Load plugins
windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();

// Register custom actions
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;
startMenu.RegisterAction("New Project", () => CreateProject(), "File", 10);
startMenu.RegisterAction("Open Project", () => OpenProject(), "File", 20);
startMenu.RegisterAction("Settings", () => ShowSettings(), "Edit", 10);
startMenu.RegisterAction("Terminal", () => OpenTerminal(), "Tools", 10);
```

### Example 5: No Panels

```csharp
var options = new ConsoleWindowSystemOptions(
    ShowTopPanel: false,
    ShowBottomPanel: false
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// No panels at all - maximum screen space
```

## Best Practices

### Panels

1. **Keep Text Concise**: Panel elements have limited space
2. **Use Markup Sparingly**: Too much markup reduces readability
3. **Match Conventions**: Use familiar patterns (title in top-left, clock in bottom-right)
4. **Consider Zones**: Left for branding/menus, center for task bar, right for metrics/clock

### Task Bar

1. **Meaningful Titles**: Window titles should be descriptive (shown in task bar)
2. **Limit Windows**: Alt+1-9 supports up to 9 windows
3. **Consider Placement**: Center zone is conventional for task bars

### Start Menu

1. **Logical Categories**: Group related actions (File, Edit, Tools, Help)
2. **Meaningful Names**: Use action verbs (New, Open, Save)
3. **Consistent Ordering**: Order by frequency of use (most used = lower order number)
4. **Plugin Awareness**: Leave room for plugin categories

### Performance

1. **Enable Frame Limiting**: Use `TargetFPS: 30-60` for most applications
2. **Metrics for Development**: Add Performance element during development
3. **Optimize Actions**: Keep Start Menu actions fast (< 100ms)

## See Also

- [Panel System](PANELS.md) - Full panel and element API reference
- [Configuration Guide](CONFIGURATION.md) - Full configuration reference
- [Plugin Development](PLUGINS.md) - Creating plugins with actions
- [State Services](STATE-SERVICES.md) - PanelStateService and other services
- [Dialogs](DIALOGS.md) - Built-in system dialogs
