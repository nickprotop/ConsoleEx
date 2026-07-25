# State Services

SharpConsoleUI includes built-in state management services for managing different aspects of the UI. These services are automatically created and available through the `ConsoleWindowSystem` instance.

## Table of Contents

- [Overview](#overview)
- [PanelStateService](#panelstateservice)
- [DesktopBackgroundService](#desktopbackgroundservice)
- [WindowStateService](#windowstateservice)
- [WindowPlacementService](#windowplacementservice)
- [FocusManager](#focusmanager)
- [ModalStateService](#modalstateservice)
- [NotificationStateService](#notificationstateservice)
- [ThemeStateService](#themestateservice)
- [CursorStateService](#cursorstateservice)
- [InputStateService](#inputstateservice)
- [PluginStateService](#pluginstateservice)
- [RegistryStateService](#registrystateservice)

## Overview

All state services are accessible through the `ConsoleWindowSystem` instance:

```csharp
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Access state services
windowSystem.PanelStateService          // Panel visibility, status text
windowSystem.DesktopBackgroundService   // Desktop background rendering
windowSystem.WindowStateService
window.FocusManager              // Focus is per-window
windowSystem.ModalStateService
windowSystem.NotificationStateService
windowSystem.ThemeStateService
windowSystem.CursorStateService
windowSystem.InputStateService
windowSystem.PluginStateService
```

## PanelStateService

Manages the top and bottom screen panels — visibility, status text shortcuts, and panel references. See the [Panel System guide](PANELS.md) for the full element and builder reference.

### Key Properties

```csharp
// Panel references
Panel? TopPanel { get; }
Panel? BottomPanel { get; }

// Visibility toggles
bool ShowTopPanel { get; set; }
bool ShowBottomPanel { get; set; }

// Convenience status text (sets the first StatusTextElement in each panel)
string TopStatus { set; }
string BottomStatus { set; }

// Dirty state
bool IsDirty { get; }
```

### Key Methods

```csharp
// Mark both panels for redraw
void MarkDirty();
```

### Usage Example

```csharp
// Set status text at runtime
windowSystem.PanelStateService.TopStatus = "[bold cyan]Connected[/]";
windowSystem.PanelStateService.BottomStatus = "Ready";

// Toggle panel visibility
windowSystem.PanelStateService.ShowTopPanel = false;
windowSystem.PanelStateService.ShowBottomPanel = true;

// Access panels directly for element manipulation
var bottomPanel = windowSystem.PanelStateService.BottomPanel;
var startMenu = bottomPanel?.FindElement<StartMenuElement>("startmenu");
startMenu?.RegisterAction("New", () => { /* ... */ }, category: "File", order: 10);

// Check if panels need redraw
if (windowSystem.PanelStateService.IsDirty)
{
    // Panels will be redrawn on next frame
}
```

### Shorthand Access

Panels are also accessible directly on `ConsoleWindowSystem`:

```csharp
// These are equivalent:
windowSystem.PanelStateService.TopPanel
windowSystem.PanelStateService.BottomPanel

// Shorthand:
windowSystem.BottomPanel
```

## DesktopBackgroundService

Manages the desktop background — a cached `CharacterBuffer` that is rendered once and blitted to exposed regions each frame. See the [Desktop Background](DESKTOP_BACKGROUND.md) guide for the full reference.

### Usage

```csharp
// Set background via the convenience property (preferred)
windowSystem.DesktopBackground = DesktopBackgroundConfig.FromGradient(
    ColorGradient.FromColors(Color.DarkBlue, Color.Black),
    GradientDirection.Vertical);

// Or access the service directly
windowSystem.DesktopBackgroundService.Config = new DesktopBackgroundConfig { ... };

// Reset to theme default
windowSystem.DesktopBackground = null;
```

Changes are applied automatically on the next frame.

## WindowPlacementService

Resolves a `SharpConsoleUI.Layout.Placement` to absolute window bounds against the live **usable**
desktop (status-bar-aware). It backs `Window.Placement` and `WindowBuilder.WithPlacement`, and
re-resolves placed windows on desktop resize.

```csharp
// Resolve a placement to a Rectangle (left, top, width, height) against the current desktop
System.Drawing.Rectangle bounds = windowSystem.WindowPlacementService
    .Resolve(Placement.Snap(SnapZone.RightHalf));
```

Most code sets `Window.Placement` (or `WithPlacement`) rather than calling `Resolve` directly — see
[WINDOWS.md → Placement](WINDOWS.md#placement-snap-zones).

## WindowStateService

Manages window registration, z-order, and window lifecycle.

### Key Properties

```csharp
// Get all registered windows
IReadOnlyDictionary<string, Window> Windows { get; }

// Get the currently active (focused) window
Window? ActiveWindow { get; }

// Check if in drag or resize operation
bool IsDragging { get; }
bool IsResizing { get; }
```

### Key Methods

```csharp
// Register a window (activates it by default)
void RegisterWindow(Window window, bool activate = true);

// Unregister a window
void UnregisterWindow(Window window);

// Look up windows
Window? GetWindow(string guid);
Window? FindWindowByName(string name);
bool WindowExists(string name);

// Bring window to front (update z-order)
void BringToFront(Window window);

// Get windows by z-order, or only the visible ones
IReadOnlyList<Window> GetWindowsByZOrder();
IReadOnlyList<Window> GetVisibleWindows();
int GetMaxZIndex();

// Start drag operation
void StartDrag(Window window, Point mousePos);

// Start resize operation
void StartResize(Window window, ResizeDirection direction, Point mousePos);

// End drag/resize operations
void EndDrag();
void EndResize();
```

### Usage Example

```csharp
// Get all windows sorted by z-order
var windows = windowSystem.WindowStateService.GetWindowsByZOrder();
foreach (var window in windows)
{
    windowSystem.LogService.LogInfo($"Window: {window.Title} (Z: {window.ZIndex})");
}

// Check active window
var active = windowSystem.WindowStateService.ActiveWindow;
if (active != null)
{
    windowSystem.LogService.LogInfo($"Active window: {active.Title}");
}

// Check if user is dragging a window
if (windowSystem.WindowStateService.IsDragging)
{
    windowSystem.LogService.LogInfo("Window drag in progress");
}
```

## FocusManager

Each `Window` has its own `FocusManager` that tracks which control has focus. This replaced the former system-wide `FocusStateService`.

### Key Properties

```csharp
// Currently focused control within the window
IFocusableControl? FocusedControl { get; }

// Ancestor chain from window root to the focused control
IReadOnlyList<IWindowControl> FocusPath { get; }
```

### Key Methods

```csharp
// Set focus to a control (delegates into IFocusScope if applicable)
void SetFocus(IFocusableControl? control, FocusReason reason);

// Move focus forward/backward via Tab
void MoveFocus(bool backward);

// Route a mouse click to the nearest focusable ancestor
void HandleClick(IWindowControl? hit);
```

### Events

```csharp
// Fired when FocusedControl changes
event EventHandler<FocusChangedEventArgs>? FocusChanged;
```

### Usage Example

```csharp
// Subscribe to focus changes
window.FocusManager.FocusChanged += (sender, e) =>
{
    windowSystem.LogService.LogInfo($"Focus changed to: {e.NewControl?.GetType().Name ?? "none"}");
};

// Set focus programmatically
window.FocusManager.SetFocus(myControl, FocusReason.Programmatic);

// Get current focus
var focusedControl = window.FocusManager.FocusedControl;
```

## ModalStateService

Manages modal window stack and blocking behavior.

### Key Properties

```csharp
// Check if there are any modal windows
bool HasModals { get; }

// Get the topmost modal window
Window? TopModal { get; }

// Get count of modal windows
int ModalCount { get; }
```

### Key Methods

```csharp
// Push a window onto the modal stack, optionally scoped to a parent window
void PushModal(Window modal, Window? parent);

// Remove a window from the modal stack
void PopModal(Window modal);

// Check if a window is modal
bool IsModal(Window window);

// Get the modal blocking a window, or null when it is not blocked
Window? GetBlockingModal(Window targetWindow);

// Check whether activating a window is blocked by a modal
bool IsActivationBlocked(Window targetWindow);

// Modal relationships
Window? GetModalParent(Window modal);
IReadOnlyList<Window> GetModalChildren(Window parent);
Window? GetDeepestModalChild(Window parent);
```

### Usage Example

```csharp
// Create a modal dialog
var dialog = new WindowBuilder(windowSystem)
    .WithTitle("Confirmation")
    .WithSize(40, 10)
    .Centered()
    .AsModal()  // This calls PushModal internally
    .Build();

windowSystem.AddWindow(dialog);

// Check modal state
if (windowSystem.ModalStateService.HasModals)
{
    var topModal = windowSystem.ModalStateService.TopmostModal;
    windowSystem.LogService.LogInfo($"Modal active: {topModal?.Title}");
}

// Check if a window is blocked
bool isBlocked = windowSystem.ModalStateService.IsActivationBlocked(mainWindow);
```

## NotificationStateService

Manages title + message notifications (optionally modal/blocking), including display, timeout, and dismissal.

```csharp
// Auto-dismisses after 5 seconds
windowSystem.NotificationStateService.ShowNotification(
    title: "File Saved",
    message: "Your document has been saved successfully",
    severity: NotificationSeverity.Success);

// Blocking + persistent — user must dismiss
windowSystem.NotificationStateService.ShowNotification(
    "Error", "Failed to connect to database",
    NotificationSeverity.Danger, blockUi: true, timeout: null);
```

> **For the full notifications guide — including the non-blocking corner `ToastService` (`ws.ToastService`) and when to use which system — see [NOTIFICATIONS.md](NOTIFICATIONS.md).**

## ThemeStateService

Manages current theme and theme transitions.

### Key Properties

```csharp
// Get current active theme
ITheme CurrentTheme { get; }
```

### Key Methods

```csharp
// Set the active theme
void SetTheme(ITheme theme);

// Get the current theme
ITheme GetCurrentTheme();
```

### Events

```csharp
// Fired when theme changes
event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
```

### Usage Example

```csharp
// Subscribe to theme changes
windowSystem.ThemeStateService.ThemeChanged += (sender, e) =>
{
    windowSystem.LogService.LogInfo($"Theme changed from {e.OldTheme} to {e.NewTheme}");

    // Refresh custom UI elements
    UpdateCustomColors();
};

// Get current theme
var currentTheme = windowSystem.ThemeStateService.CurrentTheme;
windowSystem.LogService.LogInfo($"Current theme window background: {currentTheme.WindowBackgroundColor}");

// Set theme programmatically
windowSystem.ThemeStateService.SetTheme(new ModernGrayTheme());
```

## CursorStateService

Manages console cursor visibility and position.

### Key Properties

```csharp
// Immutable snapshot of the current cursor state
CursorState CurrentState { get; }
```

`CursorState` exposes `IsVisible`, `AbsolutePosition`, and the owning control/window.

### Key Methods

```csharp
// Visibility
void SetVisible(bool visible);
void HideCursor();

// Cursor shape (block, underline, bar — see CursorShape)
void SetShape(CursorShape shape);

// Diagnostics
IReadOnlyList<CursorState> GetHistory();
void ClearHistory();
string GetDebugInfo();
```

> **Note:** cursor **position** is not set directly by application code. The framework derives it
> each frame from the focused control via `UpdateFromWindowSystem(...)` and applies it to the
> terminal with `ApplyCursorToConsole(...)`. To move the cursor, move focus or set the cursor
> position on the focused control (for example `MultilineEditControl.SetLogicalCursorPosition`).

### Usage Example

```csharp
// Hide the cursor
windowSystem.CursorStateService.HideCursor();

// Show it again
windowSystem.CursorStateService.SetVisible(true);

// Inspect the current state
var cursor = windowSystem.CursorStateService.CurrentState;
windowSystem.LogService.LogInfo($"Cursor visible: {cursor.IsVisible} at {cursor.AbsolutePosition}");
```

## InputStateService

Manages input state and key/mouse event processing.

This service owns the **input queue** the main loop drains each frame. It does not track
held-key or mouse-button state — terminals report discrete key events, not press/release
pairs, so there is no "is this key currently down" concept to query.

### Key Properties

```csharp
// Pending input
bool HasPendingInput { get; }
int PendingInputCount { get; }

// Timing / idle detection
DateTime LastKeyTime { get; }
ConsoleModifiers LastModifiers { get; }
bool IsIdle { get; }
TimeSpan TimeSinceLastKey { get; }

// Invoked when input arrives while the loop is idle
Action? WakeCallback { get; set; }
```

### Key Methods

```csharp
// Enqueue input (used by drivers, and by tests to drive the real input path)
void EnqueueKey(ConsoleKeyInfo key);
void EnqueuePaste(string text);

// Drain input
ConsoleKeyInfo? DequeueKey();
ConsoleKeyInfo? PeekKey();
bool TryDequeuePaste(out string text);
void ClearQueue();
```

### Usage Example

```csharp
// Inspect the modifiers that accompanied the last key
if (windowSystem.InputStateService.LastModifiers.HasFlag(ConsoleModifiers.Control))
{
    windowSystem.LogService.LogInfo("Last key was pressed with Ctrl");
}

// Idle detection — e.g. dim the UI after inactivity
if (windowSystem.InputStateService.IsIdle)
{
    windowSystem.LogService.LogInfo(
        $"Idle for {windowSystem.InputStateService.TimeSinceLastKey.TotalSeconds:F0}s");
}

// Synthesize input (this is how tests drive the real input path)
windowSystem.InputStateService.EnqueueKey(
    new ConsoleKeyInfo('\0', ConsoleKey.Tab, false, false, false));
```

## PluginStateService

Manages the plugin system, including plugin loading, service registration, control/window factories, and plugin state tracking.

### Key Properties

```csharp
// Get current plugin system state
PluginState CurrentState { get; }

// Get loaded plugins
IReadOnlyList<IPlugin> LoadedPlugins { get; }

// Get registered plugin contributions
IReadOnlyCollection<string> RegisteredControlNames { get; }
IReadOnlyCollection<string> RegisteredWindowNames { get; }
IReadOnlyCollection<string> RegisteredServiceNames { get; }
IReadOnlyCollection<IPluginService> RegisteredServices { get; }
```

### PluginState Record

```csharp
public record PluginState(
    int LoadedPluginCount,
    int RegisteredServiceCount,
    int RegisteredControlCount,
    int RegisteredWindowCount,
    IReadOnlyList<string> PluginNames
);
```

### Key Methods

```csharp
// Load plugins (in-process only — no loading from disk, see PLUGINS.md)
void LoadPlugin<T>() where T : IPlugin, new();
void LoadPlugin(IPlugin plugin);
void UnloadPlugin(IPlugin plugin);

// Query plugins
IPlugin? GetPlugin(string name);
bool IsPluginLoaded(string name);

// Create plugin content
IWindowControl? CreateControl(string name);
Window? CreateWindow(string name);

// Access plugin services
IPluginService? GetService(string serviceName);
bool HasService(string serviceName);
T? GetService<T>() where T : class; // Legacy, deprecated
```

### Events

```csharp
// Fired when plugin state changes
event EventHandler<PluginStateChangedEventArgs>? StateChanged;

// Fired when a plugin is loaded
event EventHandler<PluginEventArgs>? PluginLoaded;

// Fired when a plugin is unloaded
event EventHandler<PluginEventArgs>? PluginUnloaded;

// Fired when a service is registered
event EventHandler<ServiceRegisteredEventArgs>? ServiceRegistered;
```

### Usage Example

```csharp
// Load a plugin
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Get current state
var state = windowSystem.PluginStateService.CurrentState;
windowSystem.LogService.LogInfo($"Loaded plugins: {state.LoadedPluginCount}");
windowSystem.LogService.LogInfo($"Registered services: {state.RegisteredServiceCount}");
windowSystem.LogService.LogInfo($"Registered controls: {state.RegisteredControlCount}");

// Subscribe to plugin events
windowSystem.PluginStateService.PluginLoaded += (sender, e) =>
{
    windowSystem.LogService.LogInfo($"Plugin loaded: {e.Info.Name} v{e.Info.Version}");
    windowSystem.NotificationStateService.ShowNotification(
        "Plugin Loaded",
        $"{e.Info.Name} is now available",
        NotificationSeverity.Success
    );
};

windowSystem.PluginStateService.StateChanged += (sender, e) =>
{
    windowSystem.LogService.LogInfo($"Plugin count: {e.PreviousState.LoadedPluginCount} → {e.NewState.LoadedPluginCount}");
};

// Check if a plugin is loaded
if (windowSystem.PluginStateService.IsPluginLoaded("MyPlugin"))
{
    windowSystem.LogService.LogInfo("MyPlugin is available");
}

// Get a plugin by name
var myPlugin = windowSystem.PluginStateService.GetPlugin("MyPlugin");
if (myPlugin != null)
{
    windowSystem.LogService.LogInfo($"Found plugin: {myPlugin.Info.Description}");
}

// Create plugin control
var logExporter = windowSystem.PluginStateService.CreateControl("LogExporter");
if (logExporter != null)
{
    mainWindow.AddControl(logExporter);
}

// Create plugin window
var debugWindow = windowSystem.PluginStateService.CreateWindow("DebugConsole");
if (debugWindow != null)
{
    windowSystem.AddWindow(debugWindow);
}

// Access plugin service
var diagnostics = windowSystem.PluginStateService.GetService("Diagnostics");
if (diagnostics != null)
{
    var report = (string)diagnostics.Execute("GetDiagnosticsReport")!;
    windowSystem.LogService.LogInfo(report);
}

// Get all registered service names
var services = windowSystem.PluginStateService.RegisteredServiceNames;
windowSystem.LogService.LogInfo($"Available services: {string.Join(", ", services)}");

// Plugins are registered in-process — there is no directory auto-loading
// (removed for NativeAOT compatibility). Register each plugin explicitly:
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();
```

## Complete Example

Here's an example using multiple state services together:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Create main window
var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("State Services Demo")
    .WithSize(80, 25)
    .Centered()
    .Build();

// Subscribe to focus changes (focus is per-window via FocusManager)
window.FocusManager.FocusChanged += (sender, e) =>
{
    windowSystem.NotificationStateService.ShowNotification(
        "Focus Changed",
        $"Control: {e.NewControl?.GetType().Name ?? "None"}",
        NotificationSeverity.Info,
        timeout: 2000
    );
};

// Subscribe to theme changes
windowSystem.ThemeStateService.ThemeChanged += (sender, e) =>
{
    windowSystem.NotificationStateService.ShowNotification(
        "Theme Changed",
        "UI appearance has been updated",
        NotificationSeverity.Success,
        timeout: 2000
    );
};

// Add buttons to test services
mainWindow.AddControl(
    Controls.Button("Show Info")
        .OnClick((sender, e, window) =>
        {
            var windows = windowSystem.WindowStateService.Windows;
            var hasModals = windowSystem.ModalStateService.HasModals;
            var focusedControl = window.FocusManager.FocusedControl;

            windowSystem.NotificationStateService.ShowNotification(
                "System Info",
                $"Windows: {windows.Count}, Modals: {hasModals}, Focused: {focusedControl?.GetType().Name}",
                NotificationSeverity.Info
            );
        })
        .Build()
);

mainWindow.AddControl(
    Controls.Button("Change Theme")
        .OnClick((sender, e, window) =>
        {
            windowSystem.ThemeStateService.ShowThemeSelector();
        })
        .Build()
);

mainWindow.AddControl(
    Controls.Button("Show Modal")
        .OnClick((sender, e, window) =>
        {
            var dialog = new WindowBuilder(windowSystem)
                .WithTitle("Modal Dialog")
                .WithSize(40, 10)
                .Centered()
                .AsModal()
                .Build();

            dialog.AddControl(new MarkupControl(new List<string>
            {
                "[yellow]This is a modal dialog[/]",
                "",
                "Press ESC to close"
            }));

            dialog.KeyPressed += (s, ev) =>
            {
                if (ev.KeyInfo.Key == ConsoleKey.Escape)
                {
                    windowSystem.CloseWindow(dialog);
                    ev.Handled = true;
                }
            };

            windowSystem.AddWindow(dialog);
        })
        .Build()
);

windowSystem.AddWindow(mainWindow);
windowSystem.Run();
```

## RegistryStateService

Manages persistent key-value storage that survives application restarts. Wraps `AppRegistry`, loading data on startup and saving on shutdown. See the [Registry guide](REGISTRY.md) for full documentation.

Accessible only when a `RegistryConfiguration` was passed to `ConsoleWindowSystem`:

```csharp
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    registryConfiguration: RegistryConfiguration.ForFile("myapp.json")
);

// Null if no RegistryConfiguration was provided
RegistryStateService? registry = windowSystem.RegistryStateService;
```

### Key Methods

```csharp
// Open a section (created automatically if absent)
RegistrySection OpenSection(string path);

// Explicitly flush to disk
void Save();

// Reload from disk (discards unsaved changes)
void Load();
```

### Usage Example

```csharp
var registry = windowSystem.RegistryStateService!;

var ui = registry.OpenSection("app/ui");
string theme = ui.GetString("theme", "ModernGray");
int width = ui.GetInt("windowWidth", 80);

ui.SetString("theme", "Solarized");
ui.SetInt("windowWidth", 120);

// Data persists across restarts; saved automatically on shutdown
```

For the full API including primitive types, generic types, custom storage backends, and thread-safety notes, see the [Registry guide](REGISTRY.md).

---

## Best Practices

1. **Don't modify state directly**: Always use service methods to change state
2. **Subscribe to events**: Use state change events to react to system changes
3. **Check state before operations**: Verify state before performing actions
4. **Clean up subscriptions**: Unsubscribe from events when done
5. **Use appropriate service**: Each service has a specific purpose - use the right one

---

[Back to Main Documentation](../README.md)
