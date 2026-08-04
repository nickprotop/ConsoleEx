# Configuration Guide

SharpConsoleUI provides comprehensive configuration options through `ConsoleWindowSystemOptions` and the [Panel system](PANELS.md).

## Table of Contents

- [Overview](#overview)
- [ConsoleWindowSystemOptions](#consolewindowsystemoptions)
- [Panel Configuration](#panel-configuration)
- [Environment Variables](#environment-variables)
- [Complete Configuration Examples](#complete-configuration-examples)
- [RegistryConfiguration](#registryconfiguration)

## Overview

Configuration is done at system initialization via the `options` parameter:

```csharp
using SharpConsoleUI.Panel;

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: new ConsoleWindowSystemOptions(
        EnablePerformanceMetrics: false,
        EnableFrameRateLimiting: true,
        TargetFPS: 60,
        TopPanelConfig: panel => panel
            .Left(Elements.StatusText("[bold]My App[/]")),
        BottomPanelConfig: panel => panel
            .Left(Elements.StartMenu())
            .Center(Elements.TaskBar())
            .Right(Elements.Clock())
    ));
```

## ConsoleWindowSystemOptions

Main configuration for the console window system.

`ConsoleWindowSystemOptions` is a **positional record** with init-only properties, so all
settings are passed as named constructor arguments and modified with `with` expressions
rather than assignment.

#### Rendering and performance

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnablePerformanceMetrics` | `bool` | `false` | Enable performance metric tracking |
| `EnableFrameRateLimiting` | `bool` | `true` | Limit rendering to target FPS (prevents excessive CPU usage) |
| `TargetFPS` | `int` | `60` | Target frames per second (0 = unlimited) |
| `ClampToWindowWidth` | `bool` | `false` | Clamp rendered output to the window width |
| `DirtyTrackingMode` | `DirtyTrackingMode` | `Smart` | Dirty-region granularity: `Smart` (adaptive), `Cell` (minimal output), `Line` (fewer cursor moves) |
| `SmartModeCoverageThreshold` | `float` | `0.6f` | `Smart` mode only — above this fraction of dirty cells, switch to line mode |
| `SmartModeFragmentationThreshold` | `int` | `5` | `Smart` mode only — above this many separate regions, switch to line mode |
| `ClearDestinationOnWindowMove` | `bool` | `true` | Clear the vacated area when a window moves |
| `EnableAnimations` | `bool` | `true` | Enable the animation system |

#### Panels and desktop

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TopPanelConfig` | `Func<PanelBuilder, PanelBuilder>?` | `null` | Top panel element configuration |
| `BottomPanelConfig` | `Func<PanelBuilder, PanelBuilder>?` | `null` | Bottom panel element configuration |
| `ShowTopPanel` | `bool` | `true` | Show/hide top panel |
| `ShowBottomPanel` | `bool` | `true` | Show/hide bottom panel |
| `DesktopBackground` | `DesktopBackgroundConfig?` | `null` | Desktop background (gradient, pattern, animated). See [Desktop Background](DESKTOP_BACKGROUND.md) |
| `TerminalTransparencyMode` | `TerminalTransparencyMode` | `PreserveWindowColor` | How semi-transparent windows blend over a transparent terminal |

#### Input and lifecycle

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `WindowCycleKey` | `ConsoleKey?` | `ConsoleKey.T` | Ctrl+key that cycles windows. `null` disables it |
| `ExitKey` | `ConsoleKey?` | `ConsoleKey.Q` | Ctrl+key that exits. `null` disables it |
| `AltDigitSelectsWindow` | `bool` | `true` | Alt+1..9 activates a top-level window by taskbar index. `false` leaves the chord to your app |
| `Watchdog` | `WatchdogOptions?` | `null` | Main-loop watchdog settings. `null` uses `new WatchdogOptions()` |
| `InstallSynchronizationContext` | `bool` | `false` | Install a UI `SynchronizationContext` for the duration of `Run()`. See the caveat below |

#### Diagnostics and testing

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableDiagnostics` | `bool` | `false` | Enable rendering diagnostics (zero overhead when disabled) |
| `DiagnosticsRetainFrames` | `int` | `1` | Number of frames of diagnostic data to retain |
| `DiagnosticsLayers` | `DiagnosticsLayers` | `All` | Which layers to capture diagnostics for |
| `EnableQualityAnalysis` | `bool` | `false` | Enable render quality analysis |
| `EnablePerformanceProfiling` | `bool` | `false` | Enable detailed performance profiling |

### InstallSynchronizationContext

> ⚠️ **Leave this `false` unless you are certain.** It defaults to `false` so that awaited
> continuations resume on the thread pool. A handler that blocks on async work
> (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) then freezes-and-recovers instead of
> deadlocking outright.
>
> Setting it to `true` opts into the WinForms/WPF model where `await` resumes on the UI
> thread — but then handlers **must** use `await` and must never block on async work on the
> UI thread, or the captured continuation deadlocks against the main loop.

See [Opting in (`InstallSynchronizationContext`)](THREADING_AND_ASYNC.md#opting-in-installsynchronizationcontext)
in the Threading & Async guide for the full discussion, and
[Why blocking on async work deadlocks](THREADING_AND_ASYNC.md#why-blocking-on-async-work-deadlocks)
for the underlying mechanism.

### Computed Properties

| Property | Type | Description |
|----------|------|-------------|
| `MinFrameTime` | `int` | Minimum milliseconds between frames (1000/TargetFPS) |

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

## Panel Configuration

Panels are the top and bottom bars of the screen. They are composed from **elements** using a fluent builder pattern. See the [Panel System guide](PANELS.md) for the full reference.

### Quick Reference

```csharp
using SharpConsoleUI.Panel;

var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]App Title[/]"))
        .Left(Elements.Separator())
        .Left(Elements.StatusText("[dim]Ctrl+T: Theme[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithText("☰ Start")
            .WithOptions(new StartMenuOptions
            {
                AppName = "My App",
                AppVersion = "1.0.0"
            }))
        .Center(Elements.TaskBar())
        .Right(Elements.Clock().WithFormat("HH:mm:ss"))
);
```

### Available Elements

| Factory Method | Description |
|----------------|-------------|
| `Elements.StatusText(text)` | Static or markup text |
| `Elements.Separator()` | Vertical separator |
| `Elements.TaskBar()` | Window list with Alt+N shortcuts |
| `Elements.Clock()` | Live clock display |
| `Elements.Performance()` | FPS and metrics display |
| `Elements.StartMenu()` | Start button + menu |
| `Elements.Custom(name)` | Custom render callback |

### Performance Metrics

Add a `Performance` element to display real-time metrics:

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Right(Elements.Performance())
);
```

The performance element displays:
- **FPS**: Current frames per second
- **Dirty**: Number of dirty characters per frame

### Runtime Status Text

Set status text at runtime via `PanelStateService`:

```csharp
windowSystem.PanelStateService.TopStatus = "[green]Connected to server[/]";
windowSystem.PanelStateService.BottomStatus = "[yellow]Ready[/]";
```

This sets the `Text` property of the first `StatusTextElement` in each panel.

### Panel Visibility

Toggle panel visibility at runtime:

```csharp
windowSystem.PanelStateService.ShowTopPanel = false;
windowSystem.PanelStateService.ShowBottomPanel = true;
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
// Uses all defaults — default panels with status text elements
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Set status text on default panels
windowSystem.PanelStateService.TopStatus = "[bold]My App[/]";
windowSystem.PanelStateService.BottomStatus = "Ready";
```

**Result:**
- Default top and bottom panels with status text
- No Start Menu, no task bar, no clock
- 60 FPS frame rate limiting

### Windows-like Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]My App v1.0[/]"))
        .Right(Elements.Clock()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithOptions(new StartMenuOptions
            {
                Layout = StartMenuLayout.TwoColumn,
                AppName = "My App",
                AppVersion = "1.0.0"
            }))
        .Center(Elements.TaskBar())
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- Start button in bottom-left (like Windows)
- Ctrl+Space opens Start Menu
- Window task bar in center
- Clock in top-right
- 60 FPS rendering

### Development Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnablePerformanceMetrics: true,
    EnableFrameRateLimiting: false,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold]Dev Mode[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu())
        .Center(Elements.TaskBar())
        .Right(Elements.Clock().WithFormat("HH:mm:ss"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- Performance metrics in top-right
- Unlimited FPS for performance testing
- Full bottom bar with Start Menu, task bar, and clock

### Dashboard/Monitor Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: true,
    TargetFPS: 30,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[cyan]System Dashboard[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StatusText("Press [yellow]F10[/] to exit"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- 30 FPS (sufficient for dashboards)
- No Start Menu or task bar
- Clean layout with title and exit hint

### Full-Screen Application

```csharp
var options = new ConsoleWindowSystemOptions(
    ShowBottomPanel: false,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold]My Application[/]"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- Only top panel visible
- No bottom panel — maximum screen space
- Single status line for branding

### Game Configuration

```csharp
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: false,
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[cyan]Game Title[/]"))
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StatusText("[yellow]WASD:[/] Move | [yellow]Space:[/] Jump | [yellow]ESC:[/] Quit"))
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

**Result:**
- Unlimited FPS with visible FPS counter
- Top bar for title and metrics
- Bottom bar for control hints

### No Panels (Maximum Screen Space)

```csharp
var options = new ConsoleWindowSystemOptions(
    ShowTopPanel: false,
    ShowBottomPanel: false
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

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

## RegistryConfiguration

Persistent key-value storage is configured separately via `RegistryConfiguration`, passed at construction:

```csharp
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    registryConfiguration: new RegistryConfiguration(
        FilePath: "myapp.json",       // where to persist data
        EagerFlush: false,            // write on every Set*?
        FlushInterval: TimeSpan.FromSeconds(30) // background timer
    )
);
```

See the [Registry guide](REGISTRY.md) for full documentation.

## See Also

- [Panel System](PANELS.md) - Full panel and element reference
- [Desktop Background](DESKTOP_BACKGROUND.md) - Gradients, patterns, animated backgrounds
- [Threading & Async](THREADING_AND_ASYNC.md) - The UI thread model and `InstallSynchronizationContext`
- [State Services](STATE-SERVICES.md) - Runtime state management
- [Registry](REGISTRY.md) - Persistent key-value storage
- [Themes](THEMES.md) - Theme system and customization
