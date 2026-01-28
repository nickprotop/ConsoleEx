# Configuration Guide

SharpConsoleUI provides comprehensive configuration options through `ConsoleWindowSystemOptions` and `StatusBarOptions`.

## Table of Contents

- [Overview](#overview)
- [ConsoleWindowSystemOptions](#consolewindowsystemoptions)
- [StatusBarOptions](#statusbaroptions)
- [Environment Variables](#environment-variables)
- [Complete Configuration Examples](#complete-configuration-examples)

## Overview

Configuration is done at system initialization via the `options` parameter:

```csharp
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: new ConsoleWindowSystemOptions(
        EnablePerformanceMetrics: false,
        EnableFrameRateLimiting: true,
        TargetFPS: 60,
        StatusBarOptions: new StatusBarOptions(/* ... */)
    ));
```

## ConsoleWindowSystemOptions

Main configuration for the console window system.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnablePerformanceMetrics` | `bool` | `false` | Show performance metrics (FPS, frame time, window count) in status bar |
| `EnableFrameRateLimiting` | `bool` | `true` | Limit rendering to target FPS (prevents excessive CPU usage) |
| `TargetFPS` | `int` | `60` | Target frames per second (0 = unlimited) |
| `StatusBarOptions` | `StatusBarOptions?` | `null` | Status bar and Start Menu configuration (uses defaults if null) |

### Computed Properties

| Property | Type | Description |
|----------|------|-------------|
| `MinFrameTime` | `int` | Minimum milliseconds between frames (1000/TargetFPS) |
| `StatusBar` | `StatusBarOptions` | Returns StatusBarOptions or defaults if not specified |

### Static Factory Methods

#### `Default`
Returns default configuration:
```csharp
var options = ConsoleWindowSystemOptions.Default;
// EnablePerformanceMetrics: false
// EnableFrameRateLimiting: true
// TargetFPS: 60
```

#### `Create(bool?, bool?, int?)`
Creates configuration with optional environment variable override for metrics:
```csharp
var options = ConsoleWindowSystemOptions.Create(
    enableMetrics: true,
    enableFrameRateLimiting: false,
    targetFPS: 30
);
```

#### `WithMetrics`
Returns configuration with performance metrics enabled:
```csharp
var options = ConsoleWindowSystemOptions.WithMetrics;
// EnablePerformanceMetrics: true
// Other settings: defaults
```

#### `WithoutFrameRateLimiting`
Returns configuration with unlimited FPS:
```csharp
var options = ConsoleWindowSystemOptions.WithoutFrameRateLimiting;
// EnableFrameRateLimiting: false
// Other settings: defaults
```

#### `WithTargetFPS(int)`
Returns configuration with custom target FPS:
```csharp
var options = ConsoleWindowSystemOptions.WithTargetFPS(30);
// TargetFPS: 30
// Other settings: defaults
```

### Performance Metrics

When `EnablePerformanceMetrics: true`, the top status bar displays real-time metrics:

```
FPS: 60 | Frame: 16.67ms | Windows: 3 | Dirty: 1
```

Metrics include:
- **FPS**: Current frames per second
- **Frame**: Time to render last frame (milliseconds)
- **Windows**: Total number of windows
- **Dirty**: Number of windows needing re-render

### Frame Rate Limiting

**Purpose**: Prevents excessive CPU usage by limiting rendering speed.

```csharp
// Limit to 30 FPS (good for dashboards with infrequent updates)
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: true,
    TargetFPS: 30
);

// Unlimited FPS (render as fast as possible - use for games/animations)
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: false
);

// Default 60 FPS (balanced - recommended for most applications)
var options = ConsoleWindowSystemOptions.Default;
```

**Recommendations:**
- **30 FPS**: Dashboards, monitoring tools, mostly static UIs
- **60 FPS**: General applications with animations and interactions (default)
- **Unlimited**: Real-time games, simulations, high-frequency visualizations

## StatusBarOptions

Configuration for status bars and Start Menu system.

### Properties

#### Start Button Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowStartButton` | `bool` | `false` | Show/hide Start button (opt-in) |
| `StartButtonLocation` | `StatusBarLocation` | `Bottom` | Top or Bottom status bar |
| `StartButtonPosition` | `StartButtonPosition` | `Left` | Left or Right position in status bar |
| `StartButtonText` | `string` | `"☰ Start"` | Text displayed on Start button |
| `StartMenuShortcutKey` | `ConsoleKey` | `M` | Keyboard shortcut key (Ctrl+M by default) |
| `StartMenuShortcutModifiers` | `ConsoleModifiers` | `Control` | Keyboard modifiers (Ctrl, Alt, Shift, or combinations) |

#### Start Menu Content

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowSystemMenuCategory` | `bool` | `true` | Show built-in System category (themes, settings, about) |
| `ShowWindowListInMenu` | `bool` | `true` | Show Windows category with open window list |

#### Status Bar Display

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowTopStatus` | `bool` | `true` | Show top status bar |
| `ShowBottomStatus` | `bool` | `true` | Show bottom status bar |
| `ShowTaskBar` | `bool` | `true` | Show window list in bottom status bar (Alt-1, Alt-2, etc.) |

### Enums

#### StatusBarLocation
```csharp
public enum StatusBarLocation
{
    Top,     // Top status bar
    Bottom   // Bottom status bar
}
```

#### StartButtonPosition
```csharp
public enum StartButtonPosition
{
    Left,    // Left side of status bar
    Right    // Right side of status bar
}
```

### Static Presets

#### `Default`
Returns default status bar configuration:
```csharp
var statusBar = StatusBarOptions.Default;
// ShowStartButton: false
// ShowTopStatus: true
// ShowBottomStatus: true
// ShowTaskBar: true
```

#### `WithStartButtonDisabled`
Returns configuration with Start button explicitly disabled:
```csharp
var statusBar = StatusBarOptions.WithStartButtonDisabled;
// ShowStartButton: false
```

#### `TopStartButton`
Returns configuration with Start button in top status bar:
```csharp
var statusBar = StatusBarOptions.TopStartButton;
// StartButtonLocation: Top
```

## Environment Variables

### Debug Logging

Control built-in debug logging via environment variables:

```bash
# Enable debug logging to file
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/consoleui.log

# Set minimum log level (Trace, Debug, Information, Warning, Error, Critical)
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
```

**Log Levels:**
- `Trace` - Most verbose (mouse events, render cycles, focus changes)
- `Debug` - Significant operations (window add/close, drag/resize)
- `Information` - General information messages
- `Warning` - Default level - warnings only
- `Error` - Errors only
- `Critical` - Fatal errors only

### Performance Metrics Override

Override `EnablePerformanceMetrics` via environment variable:

```bash
# Enable performance metrics via environment variable
export SHARPCONSOLEUI_PERF_METRICS=true

# Or use 1 for true
export SHARPCONSOLEUI_PERF_METRICS=1
```

This allows enabling metrics without code changes:
```csharp
// Will check SHARPCONSOLEUI_PERF_METRICS environment variable
var options = ConsoleWindowSystemOptions.Create();
```

## Complete Configuration Examples

### Minimal Configuration (Defaults)

```csharp
// Uses all defaults
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Equivalent to:
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: ConsoleWindowSystemOptions.Default
);
```

**Result:**
- No Start button (opt-in)
- No performance metrics
- 60 FPS frame rate limiting
- Top and bottom status bars visible
- Window list in bottom status bar

### Windows-like Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: false,
    EnableFrameRateLimiting: true,
    TargetFPS: 60,
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: true,
        StartButtonLocation: StatusBarLocation.Bottom,
        StartButtonPosition: StartButtonPosition.Left,
        StartButtonText: "☰ Start",
        StartMenuShortcutKey: ConsoleKey.M,
        StartMenuShortcutModifiers: ConsoleModifiers.Control,
        ShowSystemMenuCategory: true,
        ShowWindowListInMenu: true,
        ShowTaskBar: false  // Hide taskbar, window list only in Start Menu
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- Start button in bottom-left (like Windows)
- Ctrl+M opens Start Menu
- Window list only in Start Menu (not in status bar)
- System actions in Start Menu
- 60 FPS rendering

### Development Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: true,  // Show FPS and metrics
    EnableFrameRateLimiting: false,  // Unlimited FPS for testing
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: true,
        StartButtonLocation: StatusBarLocation.Top,  // Top bar for Start button
        StartButtonPosition: StartButtonPosition.Right,  // Keep top-left clear for metrics
        ShowSystemMenuCategory: true,
        ShowWindowListInMenu: true,
        ShowTaskBar: true  // Show window list in both places
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- Performance metrics in top-left
- Start button in top-right
- Unlimited FPS for performance testing
- Window list in both bottom status bar and Start Menu
- All debugging features enabled

### Dashboard/Monitor Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: false,
    EnableFrameRateLimiting: true,
    TargetFPS: 30,  // Lower FPS for dashboards
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: false,  // No Start Menu needed
        ShowTopStatus: true,
        ShowBottomStatus: true,
        ShowTaskBar: false  // No window switching for full-screen dashboard
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options)
{
    TopStatus = "[cyan]System Dashboard[/]",
    BottomStatus = "Press [yellow]F10[/] to exit"
};
```

**Result:**
- 30 FPS (sufficient for dashboards)
- No Start Menu or window list
- Clean full-screen dashboard
- Custom status text only

### Full-Screen Application

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: false,
    EnableFrameRateLimiting: true,
    TargetFPS: 60,
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: false,
        ShowTopStatus: true,   // Show top status for title
        ShowBottomStatus: false,  // Hide bottom status
        ShowTaskBar: false
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options)
{
    TopStatus = "[bold]My Application[/]"
    // BottomStatus not set - bottom bar hidden
};
```

**Result:**
- Only top status bar visible
- No Start Menu, no taskbar
- Maximum screen space for content
- Single status line for branding

### Game Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: true,  // Monitor FPS
    EnableFrameRateLimiting: false,  // Render as fast as possible
    StatusBarOptions: new StatusBarOptions(
        ShowStartButton: false,
        ShowTopStatus: true,   // Show FPS
        ShowBottomStatus: true,  // Show controls help
        ShowTaskBar: false
    )
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options)
{
    TopStatus = "[cyan]Game Title[/]",
    BottomStatus = "[yellow]WASD:[/] Move | [yellow]Space:[/] Jump | [yellow]ESC:[/] Quit"
};
```

**Result:**
- Unlimited FPS with visible FPS counter
- Top bar for title and metrics
- Bottom bar for control hints
- No window management features (single full-screen game)

### Using Environment Variable Override

```csharp
// Enable metrics via environment variable if set
var options = ConsoleWindowSystemOptions.Create(
    enableMetrics: null,  // null = check environment variable
    enableFrameRateLimiting: true,
    targetFPS: 60
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Shell:**
```bash
# Enable metrics for this run only
SHARPCONSOLEUI_PERF_METRICS=true dotnet run

# Enable debug logging
SHARPCONSOLEUI_DEBUG_LOG=/tmp/app.log SHARPCONSOLEUI_DEBUG_LEVEL=Debug dotnet run
```

## Runtime Changes

Some settings can be changed at runtime:

### Status Text

```csharp
// Change status bar text anytime
windowSystem.TopStatus = "[green]Connected to server[/]";
windowSystem.BottomStatus = "[yellow]Ready[/]";
```

### Theme Switching

```csharp
// Switch theme at runtime (if System menu enabled)
// User can access via Start Menu > System > Switch Theme

// Or programmatically
windowSystem.ThemeStateService.SetTheme("ModernGray");
```

### Performance Toggles

When System menu is enabled, users can toggle:
- Performance metrics display
- Frame rate limiting
- FPS counter

These are accessible via Start Menu > System category.

## See Also

- [Start Menu System](START_MENU.md) - Detailed Start Menu documentation
- [State Services](STATE-SERVICES.md) - Runtime state management
- [Themes](THEMES.md) - Theme system and customization
