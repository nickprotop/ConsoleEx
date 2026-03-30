# Panel System

SharpConsoleUI provides a configurable **Panel** system for the top and bottom bars of the screen. Panels sit outside the window area and display elements like status text, clocks, task bars, start menus, and performance metrics.

## Table of Contents

- [Overview](#overview)
- [Configuration](#configuration)
- [Elements Factory](#elements-factory)
- [Built-in Elements](#built-in-elements)
  - [StatusText](#statustext)
  - [Separator](#separator)
  - [TaskBar](#taskbar)
  - [Clock](#clock)
  - [Performance](#performance)
  - [StartMenu](#startmenu)
  - [Custom](#custom)
- [PanelBuilder](#panelbuilder)
- [Runtime Access](#runtime-access)
- [Panel Visibility](#panel-visibility)
- [Complete Examples](#complete-examples)

## Overview

The panel system replaces the former `StatusBarOptions` configuration. Instead of toggling predefined status bar features, you now compose panels from discrete **elements** using a fluent builder.

Each panel has three zones — **Left**, **Center**, and **Right** — and elements are laid out within those zones. Elements can be fixed-width or flexible (flex-grow), and the layout engine distributes space automatically.

```
┌─────────────────────────────────────────────────────────────┐
│  [Left elements...]        [Center...]      [Right elements] │  ← Top Panel
├─────────────────────────────────────────────────────────────┤
│                                                             │
│                      Window Area                            │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│  [Left elements...]        [Center...]      [Right elements] │  ← Bottom Panel
└─────────────────────────────────────────────────────────────┘
```

> **Note:** For the in-window `StatusBarControl` (three-zone bar with clickable items and shortcut hints), see the [StatusBarControl documentation](controls/StatusBarControl.md). That is a different control from the system-level panels documented here.

## Configuration

Panels are configured via `TopPanelConfig` and `BottomPanelConfig` on `ConsoleWindowSystemOptions`. Each accepts a function that receives a `PanelBuilder` and returns it with elements added:

```csharp
using SharpConsoleUI.Panel;

var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]My App[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu())
        .Center(Elements.TaskBar())
        .Right(Elements.Clock())
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

### Panel Visibility

Show or hide panels at configuration time:

```csharp
var options = new ConsoleWindowSystemOptions(
    ShowTopPanel: true,      // default: true
    ShowBottomPanel: false   // hide bottom panel entirely
);
```

Or toggle at runtime:

```csharp
windowSystem.PanelStateService.ShowTopPanel = false;
windowSystem.PanelStateService.ShowBottomPanel = true;
```

### Default Panels

When no `TopPanelConfig` or `BottomPanelConfig` is provided, the system creates default panels with a status text element in each. This lets `PanelStateService.TopStatus` and `BottomStatus` work out of the box.

## Elements Factory

The `Elements` static class provides factory methods for all built-in panel elements:

```csharp
using SharpConsoleUI.Panel;

Elements.StatusText("text")    // Static or markup text
Elements.Separator()           // Vertical separator character
Elements.TaskBar()             // Window list with Alt+N shortcuts
Elements.Clock()               // Live clock display
Elements.Performance()         // FPS and dirty-char metrics
Elements.StartMenu()           // Start button + menu system
Elements.Custom("name")        // Custom render callback
```

Each method returns a builder that supports fluent configuration before being passed to a `PanelBuilder` zone.

## Built-in Elements

### StatusText

Displays static or markup-formatted text. Supports click handlers.

```csharp
Elements.StatusText("[bold cyan]My Application[/]")
    .WithName("title")
    .WithColor(Color.Cyan1)
    .OnClick(() => ShowAbout())
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithName(string)` | Set element name for lookup |
| `WithColor(Color)` | Override text color |
| `OnClick(Action)` | Click handler |

**Properties (on `StatusTextElement`):**
| Property | Type | Description |
|----------|------|-------------|
| `Text` | `string` | Markup text to display (get/set) |
| `TextColor` | `Color?` | Optional color override |
| `ClickHandler` | `Action?` | Optional click callback |

### Separator

Renders a single vertical separator character between elements.

```csharp
Elements.Separator()
    .WithChar('|')
    .WithColor(Color.Grey50)
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithName(string)` | Set element name |
| `WithChar(char)` | Separator character (default: `│`) |
| `WithColor(Color)` | Separator color |

### TaskBar

Displays a list of open windows with Alt+N keyboard shortcuts. Flexes to fill available space.

```csharp
Elements.TaskBar()
    .WithActiveColor(Color.Cyan1)
    .WithInactiveColor(Color.Grey50)
    .WithMinimizedDim(true)
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithName(string)` | Set element name |
| `WithActiveColor(Color)` | Active window highlight color |
| `WithInactiveColor(Color)` | Inactive window color |
| `WithMinimizedDim(bool)` | Dim minimized windows (default: true) |

**Features:**
- `Alt+1` through `Alt+9` to switch windows
- Click to activate a window
- Minimized windows shown dimmed
- Automatically updates when windows are added/removed

### Clock

Displays the current time with configurable format. Auto-updates.

```csharp
Elements.Clock()
    .WithFormat("HH:mm:ss")
    .WithColor(Color.Yellow)
    .WithUpdateInterval(1000)
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithName(string)` | Set element name |
| `WithFormat(string)` | Time format string (default: `"HH:mm"`) |
| `WithColor(Color)` | Text color |
| `WithUpdateInterval(int)` | Update interval in ms (default: 1000) |

### Performance

Displays real-time FPS and dirty character count. Auto-updates.

```csharp
Elements.Performance()
    .ShowFPS(true)
    .ShowDirtyChars(true)
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithName(string)` | Set element name |
| `ShowFPS(bool)` | Show FPS counter (default: true) |
| `ShowDirtyChars(bool)` | Show dirty char count (default: true) |

### StartMenu

Adds a Start button that opens a Windows-like menu with categorized actions, window list, and system features.

```csharp
Elements.StartMenu()
    .WithText("☰ Start")
    .WithColors(Color.White, Color.DarkBlue)
    .WithShortcutKey(ConsoleKey.Spacebar, ConsoleModifiers.Control)
    .WithOptions(new StartMenuOptions
    {
        Layout = StartMenuLayout.TwoColumn,
        AppName = "My App",
        AppVersion = "1.0.0",
        ShowSystemCategory = true,
        ShowWindowList = true
    })
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithName(string)` | Set element name |
| `WithText(string)` | Button text (default: `"☰ Start"`) |
| `WithColors(Color, Color)` | Button foreground and background |
| `WithShortcutKey(ConsoleKey, ConsoleModifiers)` | Keyboard shortcut (default: Ctrl+Space) |
| `WithOptions(StartMenuOptions)` | Menu configuration |

**StartMenuOptions:**
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Layout` | `StartMenuLayout` | `TwoColumn` | `SingleColumn` or `TwoColumn` |
| `AppName` | `string?` | `null` | App name in header |
| `AppVersion` | `string?` | `null` | App version in header |
| `ShowIcons` | `bool` | `true` | Show icons in header/exit |
| `HeaderIcon` | `string` | `"☰"` | Header icon |
| `ShowSystemCategory` | `bool` | `true` | Show System category |
| `ShowWindowList` | `bool` | `true` | Show Windows list |
| `SidebarStyle` | `StartMenuSidebarStyle` | `IconLabel` | Sidebar display style |
| `BackgroundGradient` | `GradientBackground?` | `null` | Menu gradient background |

**Registering Actions:**

Actions are registered directly on the `StartMenuElement`:

```csharp
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;

startMenu.RegisterAction("New Document", () => CreateDoc(), category: "File", order: 10);
startMenu.RegisterAction("Open File", () => OpenFile(), category: "File", order: 20);
startMenu.RegisterAction("Calculator", () => OpenCalc(), category: "Tools", order: 10);
```

**StartMenuElement Methods:**
| Method | Description |
|--------|-------------|
| `RegisterAction(name, callback, category?, order?)` | Register a menu action |
| `UnregisterAction(name)` | Remove an action |
| `GetActions()` | Get all registered actions |
| `Show()` | Programmatically open the menu |

### Custom

Create elements with custom render callbacks for full control.

```csharp
Elements.Custom("myElement")
    .WithFixedWidth(20)
    .WithRenderCallback((buffer, x, y, width, fg, bg) =>
    {
        buffer.WriteString(x, y, "Custom content", fg, bg);
    })
    .WithClickHandler(() => HandleClick())
```

**Builder Methods:**
| Method | Description |
|--------|-------------|
| `WithFixedWidth(int)` | Set fixed width |
| `WithFlexGrow(int)` | Set flex grow factor |
| `WithRenderCallback(Action<...>)` | Set render function |
| `WithClickHandler(Action)` | Set click handler |

## PanelBuilder

The `PanelBuilder` provides a fluent API for composing panel layouts:

```csharp
var panel = Panel.Builder()
    .Left(Elements.StatusText("Left"))
    .Left(Elements.Separator())
    .Center(Elements.TaskBar())
    .Right(Elements.Clock())
    .WithBackgroundColor(Color.DarkBlue)
    .WithForegroundColor(Color.White)
    .Visible(true)
    .Build();
```

**Methods:**
| Method | Returns | Description |
|--------|---------|-------------|
| `Left(IPanelElement)` | `PanelBuilder` | Add element to left zone |
| `Left(IPanelElementBuilder)` | `PanelBuilder` | Add builder's element to left zone |
| `Left(params object[])` | `PanelBuilder` | Add mixed elements/builders to left |
| `Center(...)` | `PanelBuilder` | Same variants for center zone |
| `Right(...)` | `PanelBuilder` | Same variants for right zone |
| `WithBackgroundColor(Color)` | `PanelBuilder` | Set background color |
| `WithForegroundColor(Color)` | `PanelBuilder` | Set foreground color |
| `Visible(bool)` | `PanelBuilder` | Set initial visibility |
| `Build()` | `Panel` | Build the panel |

## Runtime Access

### Via PanelStateService

Access panels and convenience properties through `windowSystem.PanelStateService`:

```csharp
// Convenience status text shortcuts
windowSystem.PanelStateService.TopStatus = "[bold cyan]Connected[/]";
windowSystem.PanelStateService.BottomStatus = "Ready";

// Toggle visibility
windowSystem.PanelStateService.ShowTopPanel = false;
windowSystem.PanelStateService.ShowBottomPanel = true;

// Check dirty state
if (windowSystem.PanelStateService.IsDirty) { /* ... */ }
```

### Via Panel References

Access panels directly for element manipulation:

```csharp
// Direct panel access
var topPanel = windowSystem.PanelStateService.TopPanel;
var bottomPanel = windowSystem.PanelStateService.BottomPanel;

// Or via shorthand properties
var bottomPanel = windowSystem.BottomPanel;

// Find elements by name
var clock = bottomPanel!.FindElement<ClockElement>("clock");

// Find elements by type
var startMenu = bottomPanel!.FindElement<StartMenuElement>("startmenu");

// Check for element existence
if (bottomPanel!.HasElement<TaskBarElement>()) { /* ... */ }

// Get all elements of a type
var allText = topPanel!.FindAllElements<StatusTextElement>();

// Add/remove elements at runtime
topPanel!.AddRight(new StatusTextElement("New item"));
topPanel!.Remove("itemName");

// Clear zones
topPanel!.ClearRight();
bottomPanel!.ClearAll();
```

### Panel Properties

| Property | Type | Description |
|----------|------|-------------|
| `Visible` | `bool` | Panel visibility (triggers screen redraw) |
| `BackgroundColor` | `Color?` | Background color (falls back to theme) |
| `ForegroundColor` | `Color?` | Foreground color (falls back to theme) |
| `IsDirty` | `bool` | Whether panel needs redraw |
| `Height` | `int` | 1 if visible, 0 if hidden |

### Panel Methods

| Method | Description |
|--------|-------------|
| `AddLeft(params IPanelElement[])` | Add elements to left zone |
| `AddCenter(params IPanelElement[])` | Add elements to center zone |
| `AddRight(params IPanelElement[])` | Add elements to right zone |
| `Remove(string name)` | Remove element by name |
| `Remove(IPanelElement)` | Remove specific element |
| `FindElement<T>(string name)` | Find and cast element by name |
| `HasElement<T>()` | Check if element type exists |
| `FindAllElements<T>()` | Get all elements of type |
| `ClearAll()` | Clear all zones |
| `ClearLeft()` / `ClearCenter()` / `ClearRight()` | Clear specific zone |
| `MarkDirty()` | Mark for redraw |

## Complete Examples

### Minimal (Default Panels)

```csharp
// Default panels with status text elements
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
windowSystem.PanelStateService.TopStatus = "[bold]My App[/]";
windowSystem.PanelStateService.BottomStatus = "Ready";
```

### Windows-like Desktop

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]My App[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithText("☰ Start")
            .WithOptions(new StartMenuOptions
            {
                Layout = StartMenuLayout.TwoColumn,
                AppName = "My App",
                AppVersion = "1.0.0"
            }))
        .Center(Elements.TaskBar())
        .Right(Elements.Clock().WithFormat("HH:mm:ss"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);

// Register Start Menu actions
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;
startMenu.RegisterAction("New Document", () => { /* ... */ }, category: "File", order: 10);
startMenu.RegisterAction("Settings", () => { /* ... */ }, category: "Edit", order: 10);
```

### Dashboard (No Start Menu)

```csharp
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: true,
    TargetFPS: 30,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]System Dashboard[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StatusText("Press [yellow]F10[/] to exit"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

### Full-Screen App (Top Panel Only)

```csharp
var options = new ConsoleWindowSystemOptions(
    ShowBottomPanel: false,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold]My Application[/]"))
        .Right(Elements.Clock())
);
```

### No Panels

```csharp
var options = new ConsoleWindowSystemOptions(
    ShowTopPanel: false,
    ShowBottomPanel: false
);
```

### Custom Element

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold]App[/]"))
        .Right(Elements.Custom("indicator")
            .WithFixedWidth(12)
            .WithRenderCallback((buffer, x, y, width, fg, bg) =>
            {
                var status = IsConnected ? "[green]●[/]" : "[red]●[/]";
                buffer.WriteMarkup(x, y, $" {status} Online", fg, bg);
            }))
);
```

## Creating Custom Elements

Extend `PanelElement` to create reusable custom elements:

```csharp
public class BatteryElement : PanelElement
{
    public BatteryElement() : base("battery") { }

    public override int? FixedWidth => 10;

    public override void Render(CharacterBuffer buffer, int x, int y,
        int width, Color fg, Color bg)
    {
        var level = GetBatteryLevel();
        var color = level > 20 ? Color.Green : Color.Red;
        buffer.WriteString(x, y, $"⚡ {level}%", color, bg);
    }
}

// Use it:
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Right(new BatteryElement())
);
```

## Migration from StatusBarOptions

If you were using `StatusBarOptions`, here's how to migrate:

| Old (StatusBarOptions) | New (Panel System) |
|------------------------|--------------------|
| `ShowTopStatus: true` | `ShowTopPanel: true` |
| `ShowBottomStatus: true` | `ShowBottomPanel: true` |
| `ShowStartButton: true` | Add `Elements.StartMenu()` to a panel |
| `ShowTaskBar: true` | Add `Elements.TaskBar()` to a panel |
| `StartButtonLocation: Bottom` | Add StartMenu to `BottomPanelConfig` |
| `StartButtonPosition: Left` | Use `.Left(Elements.StartMenu())` |
| `windowSystem.TopStatus = "..."` | `windowSystem.PanelStateService.TopStatus = "..."` |
| `windowSystem.BottomStatus = "..."` | `windowSystem.PanelStateService.BottomStatus = "..."` |
| `windowSystem.RegisterStartMenuAction(...)` | `startMenuElement.RegisterAction(...)` |

**Before:**
```csharp
var options = new ConsoleWindowSystemOptions(
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: true,
        StartButtonLocation: StatusBarLocation.Bottom,
        ShowTaskBar: true,
        StartMenu: new StartMenuOptions { AppName = "My App" }
    )
);
windowSystem.TopStatus = "[bold]Title[/]";
windowSystem.RegisterStartMenuAction("New", CreateNew, "File", 10);
```

**After:**
```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold]Title[/]")),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithOptions(new StartMenuOptions { AppName = "My App" }))
        .Center(Elements.TaskBar())
);
var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;
startMenu.RegisterAction("New", () => CreateNew(), category: "File", order: 10);
```

## See Also

- [Configuration Guide](CONFIGURATION.md) - System configuration reference
- [State Services](STATE-SERVICES.md) - PanelStateService and other services
- [Fluent Builders](BUILDERS.md) - Panel and element builder APIs
- [Plugins](PLUGINS.md) - Plugin integration with Start Menu
