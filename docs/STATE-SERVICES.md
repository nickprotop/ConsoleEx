# State Services

SharpConsoleUI includes built-in state management services for managing different aspects of the UI. These services are automatically created and available through the `ConsoleWindowSystem` instance.

## Table of Contents

- [Overview](#overview)
- [WindowStateService](#windowstateservice)
- [FocusStateService](#focusstateservice)
- [ModalStateService](#modalstateservice)
- [NotificationStateService](#notificationstateservice)
- [ThemeStateService](#themestateservice)
- [CursorStateService](#cursorstateservice)
- [InputStateService](#inputstateservice)
- [PluginStateService](#pluginstateservice)

## Overview

All state services are accessible through the `ConsoleWindowSystem` instance:

```csharp
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Access state services
windowSystem.WindowStateService
windowSystem.FocusStateService
windowSystem.ModalStateService
windowSystem.NotificationStateService
windowSystem.ThemeStateService
windowSystem.CursorStateService
windowSystem.InputStateService
windowSystem.PluginStateService
```

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
// Register a window
void RegisterWindow(Window window);

// Unregister a window
void UnregisterWindow(string windowId);

// Bring window to front (update z-order)
void BringToFront(Window window);

// Get windows in z-order (top to bottom)
IEnumerable<Window> GetWindowsInZOrder();

// Start drag operation
void StartDrag(Window window, Point startMousePos);

// Start resize operation
void StartResize(Window window, Point startMousePos, ResizeDirection direction);

// End drag/resize operations
void EndDrag();
void EndResize();
```

### Usage Example

```csharp
// Get all windows sorted by z-order
var windows = windowSystem.WindowStateService.GetWindowsInZOrder();
foreach (var window in windows)
{
    Console.WriteLine($"Window: {window.Title} (Z: {window.ZOrder})");
}

// Check active window
var active = windowSystem.WindowStateService.ActiveWindow;
if (active != null)
{
    Console.WriteLine($"Active window: {active.Title}");
}

// Check if user is dragging a window
if (windowSystem.WindowStateService.IsDragging)
{
    Console.WriteLine("Window drag in progress");
}
```

## FocusStateService

Manages keyboard focus tracking for windows and controls.

### Key Properties

```csharp
// Currently focused window
Window? FocusedWindow { get; }

// Currently focused control within the focused window
IWindowControl? FocusedControl { get; }
```

### Key Methods

```csharp
// Set focus to a window
void SetFocusedWindow(Window? window);

// Set focus to a control
void SetFocusedControl(IWindowControl? control);

// Get window that contains a control
Window? GetWindowForControl(IWindowControl control);
```

### Events

```csharp
// Fired when focused window changes
event EventHandler<FocusChangedEventArgs>? FocusedWindowChanged;

// Fired when focused control changes
event EventHandler<FocusChangedEventArgs>? FocusedControlChanged;
```

### Usage Example

```csharp
// Subscribe to focus changes
windowSystem.FocusStateService.FocusedWindowChanged += (sender, e) =>
{
    Console.WriteLine($"Focus changed to: {e.CurrentWindow?.Title ?? "none"}");
};

// Set focus programmatically
windowSystem.FocusStateService.SetFocusedWindow(myWindow);

// Get current focus
var focusedWindow = windowSystem.FocusStateService.FocusedWindow;
var focusedControl = windowSystem.FocusStateService.FocusedControl;
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
// Push a window onto the modal stack
void PushModal(Window window);

// Remove a window from the modal stack
void RemoveModal(Window window);

// Check if a window is modal
bool IsModal(Window window);

// Check if a window should be blocked (beneath a modal)
bool IsBlockedByModal(Window window);
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
    var topModal = windowSystem.ModalStateService.TopModal;
    Console.WriteLine($"Modal active: {topModal?.Title}");
}

// Check if a window is blocked
bool isBlocked = windowSystem.ModalStateService.IsBlockedByModal(mainWindow);
```

## NotificationStateService

Manages notification display, timeout, and dismissal.

### Key Properties

```csharp
// Get current notification state
NotificationState State { get; }

// Check if there are active notifications
bool HasNotifications => State.HasNotifications;

// Get count of active notifications
int ActiveCount => State.ActiveCount;
```

### Notification Severity

```csharp
public enum NotificationSeverity
{
    None,    // Gray
    Info,    // Blue
    Success, // Green
    Warning, // Yellow
    Danger   // Red
}
```

### Key Methods

```csharp
// Show a notification
string ShowNotification(
    string title,
    string message,
    NotificationSeverity severity,
    bool blockUi = false,
    int? timeout = 5000,
    Window? parentWindow = null
);

// Dismiss a notification by ID
bool DismissNotification(string notificationId);

// Dismiss all notifications
void DismissAllNotifications();

// Get notification by ID
NotificationInfo? GetNotification(string notificationId);
```

### Events

```csharp
// Fired when a notification is shown
event EventHandler<NotificationEventArgs>? NotificationShown;

// Fired when a notification is dismissed
event EventHandler<NotificationEventArgs>? NotificationDismissed;
```

### Usage Examples

```csharp
// Show a simple notification (auto-dismisses after 5 seconds)
windowSystem.NotificationStateService.ShowNotification(
    title: "File Saved",
    message: "Your document has been saved successfully",
    severity: NotificationSeverity.Success
);

// Show a blocking notification (user must dismiss)
windowSystem.NotificationStateService.ShowNotification(
    title: "Error",
    message: "Failed to connect to database",
    severity: NotificationSeverity.Danger,
    blockUi: true,
    timeout: null  // No auto-dismiss
);

// Show notification with custom timeout
windowSystem.NotificationStateService.ShowNotification(
    title: "Processing",
    message: "Operation in progress...",
    severity: NotificationSeverity.Info,
    timeout: 10000  // 10 seconds
);

// Show notification attached to a specific window
windowSystem.NotificationStateService.ShowNotification(
    title: "Validation Error",
    message: "Please fill in all required fields",
    severity: NotificationSeverity.Warning,
    parentWindow: formWindow
);

// Subscribe to notification events
windowSystem.NotificationStateService.NotificationShown += (sender, e) =>
{
    Console.WriteLine($"Notification shown: {e.Notification.Title}");
};

windowSystem.NotificationStateService.NotificationDismissed += (sender, e) =>
{
    Console.WriteLine($"Notification dismissed: {e.Notification.Title}");
};

// Dismiss specific notification
string notificationId = windowSystem.NotificationStateService.ShowNotification(
    "Info", "Message", NotificationSeverity.Info);

// Later...
windowSystem.NotificationStateService.DismissNotification(notificationId);

// Dismiss all notifications
windowSystem.NotificationStateService.DismissAllNotifications();
```

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
    Console.WriteLine($"Theme changed from {e.OldTheme} to {e.NewTheme}");

    // Refresh custom UI elements
    UpdateCustomColors();
};

// Get current theme
var currentTheme = windowSystem.ThemeStateService.CurrentTheme;
Console.WriteLine($"Current theme window background: {currentTheme.WindowBackgroundColor}");

// Set theme programmatically
windowSystem.ThemeStateService.SetTheme(new ModernGrayTheme());
```

## CursorStateService

Manages console cursor visibility and position.

### Key Properties

```csharp
// Check if cursor is currently visible
bool IsCursorVisible { get; }

// Get current cursor position
Point CursorPosition { get; }
```

### Key Methods

```csharp
// Show/hide cursor
void ShowCursor();
void HideCursor();

// Set cursor position
void SetCursorPosition(int x, int y);
void SetCursorPosition(Point position);

// Save and restore cursor position
void SaveCursorPosition();
void RestoreCursorPosition();
```

### Usage Example

```csharp
// Hide cursor during rendering
windowSystem.CursorStateService.HideCursor();

// Show cursor for input
windowSystem.CursorStateService.ShowCursor();
windowSystem.CursorStateService.SetCursorPosition(10, 5);

// Save and restore cursor position
windowSystem.CursorStateService.SaveCursorPosition();
// ... do something ...
windowSystem.CursorStateService.RestoreCursorPosition();
```

## InputStateService

Manages input state and key/mouse event processing.

### Key Properties

```csharp
// Check if a key is currently pressed
bool IsKeyPressed(ConsoleKey key);

// Get mouse button states
bool IsLeftButtonPressed { get; }
bool IsRightButtonPressed { get; }
bool IsMiddleButtonPressed { get; }

// Get current mouse position
Point MousePosition { get; }
```

### Key Methods

```csharp
// Track key press/release
void SetKeyPressed(ConsoleKey key, bool isPressed);

// Track mouse button press/release
void SetMouseButtonPressed(MouseButton button, bool isPressed);

// Update mouse position
void UpdateMousePosition(Point position);

// Reset all input state
void Reset();
```

### Usage Example

```csharp
// Check if a key is being held down
if (windowSystem.InputStateService.IsKeyPressed(ConsoleKey.LeftControl))
{
    Console.WriteLine("Ctrl key is pressed");
}

// Check mouse button state
if (windowSystem.InputStateService.IsLeftButtonPressed)
{
    var mousePos = windowSystem.InputStateService.MousePosition;
    Console.WriteLine($"Left mouse button is down at ({mousePos.X}, {mousePos.Y})");
}

// Reset input state (useful when losing focus)
windowSystem.InputStateService.Reset();
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

// Get configuration
PluginConfiguration Configuration { get; }
```

### PluginState Record

```csharp
public record PluginState(
    int LoadedPluginCount,
    int RegisteredServiceCount,
    int RegisteredControlCount,
    int RegisteredWindowCount,
    IReadOnlyList<string> PluginNames,
    bool AutoLoadEnabled,
    string? PluginsDirectory
);
```

### Key Methods

```csharp
// Load plugins
void LoadPlugin<T>() where T : IPlugin, new();
void LoadPlugin(IPlugin plugin);
void LoadPlugin(string dllPath);
void LoadPluginsFromDirectory(string? pluginsPath = null);

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

// Configuration
void UpdateConfiguration(PluginConfiguration configuration);
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
windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();

// Get current state
var state = windowSystem.PluginStateService.CurrentState;
Console.WriteLine($"Loaded plugins: {state.LoadedPluginCount}");
Console.WriteLine($"Registered services: {state.RegisteredServiceCount}");
Console.WriteLine($"Registered controls: {state.RegisteredControlCount}");

// Subscribe to plugin events
windowSystem.PluginStateService.PluginLoaded += (sender, e) =>
{
    Console.WriteLine($"Plugin loaded: {e.Info.Name} v{e.Info.Version}");
    windowSystem.NotificationStateService.ShowNotification(
        "Plugin Loaded",
        $"{e.Info.Name} is now available",
        NotificationSeverity.Success
    );
};

windowSystem.PluginStateService.StateChanged += (sender, e) =>
{
    Console.WriteLine($"Plugin count: {e.PreviousState.LoadedPluginCount} → {e.NewState.LoadedPluginCount}");
};

// Check if a plugin is loaded
if (windowSystem.PluginStateService.IsPluginLoaded("DeveloperTools"))
{
    Console.WriteLine("DeveloperTools plugin is available");
}

// Get a plugin by name
var devTools = windowSystem.PluginStateService.GetPlugin("DeveloperTools");
if (devTools != null)
{
    Console.WriteLine($"Found plugin: {devTools.Info.Description}");
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
    Console.WriteLine(report);
}

// Get all registered service names
var services = windowSystem.PluginStateService.RegisteredServiceNames;
Console.WriteLine($"Available services: {string.Join(", ", services)}");

// Auto-load plugins from directory with configuration
var pluginConfig = new PluginConfiguration(
    AutoLoad: true,
    PluginsDirectory: "./plugins"
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    pluginConfiguration: pluginConfig
);
// Plugins are loaded automatically on startup
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

// Subscribe to focus changes
windowSystem.FocusStateService.FocusedWindowChanged += (sender, e) =>
{
    windowSystem.NotificationStateService.ShowNotification(
        "Focus Changed",
        $"Window: {e.CurrentWindow?.Title ?? "None"}",
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
            var focusedWindow = windowSystem.FocusStateService.FocusedWindow;

            windowSystem.NotificationStateService.ShowNotification(
                "System Info",
                $"Windows: {windows.Count}, Modals: {hasModals}, Focused: {focusedWindow?.Title}",
                NotificationSeverity.Info
            );
        })
        .Build()
);

mainWindow.AddControl(
    Controls.Button("Change Theme")
        .OnClick((sender, e, window) =>
        {
            windowSystem.ShowThemeSelectorDialog();
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

## Best Practices

1. **Don't modify state directly**: Always use service methods to change state
2. **Subscribe to events**: Use state change events to react to system changes
3. **Check state before operations**: Verify state before performing actions
4. **Clean up subscriptions**: Unsubscribe from events when done
5. **Use appropriate service**: Each service has a specific purpose - use the right one

---

[Back to Main Documentation](../README.md)
