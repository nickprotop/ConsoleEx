# LogViewerControl

Scrollable control that displays log entries from the library's `LogService`, updating automatically as new entries arrive.

## Overview

LogViewerControl renders the entries held by an `ILogService` as a live, scrolling list. It subscribes to the service's `LogAdded` and `LogsCleared` events, so the view updates automatically whenever the application or the library writes a log entry. Each entry is rendered through `LogEntry.ToMarkup()`, which color-codes the level and dims the timestamp and category.

Internally the control composes a `GridControl` hosting a virtualized `TableControl` fed by a `LogTableDataSource`, so it inherits smooth scrolling, an optional scrollbar, and mouse-wheel support over large buffers without a per-entry control. Under `AutoScroll`, new entries appended at the bottom scroll into view; follow detaches when the user scrolls up and re-attaches when they scroll back to the bottom. That rule lives in the inner `TableControl` (`TableControl.AutoScroll`), so it holds for every scroll route — mouse wheel, scrollbar arrows, track, thumb drag and keyboard alike — and cannot be bypassed by event routing. It also holds regardless of buffer state: a scrolled-up reader is not pulled back down by buffer-eviction rebuilds (the steady state once `MaxBufferSize` is reached) or by view-filter changes, and a reader at the bottom keeps tailing through both. `LogViewerControl.AutoScroll` itself is sticky user intent — it stays as you set it and is not flipped by scrolling. The control honors the service's `MaxBufferSize`, trimming the oldest displayed entries when the buffer overflows.

LogViewerControl is thread-safe: log events may be raised from any thread. Incoming entries are placed on a concurrent queue and applied on the UI thread during the next paint, so background logging never races with rendering. The control supports an optional `Title` line and can filter what it shows by minimum log level and by category.

See also: [ListControl](ListControl.md), [MarkupControl](MarkupControl.md)

## Quick Start

```csharp
var logViewer = new LogViewerControl(windowSystem.LogService)
{
    Title = "Library Logs"
};

var window = new WindowBuilder(windowSystem)
    .WithTitle("Log Viewer")
    .WithSize(80, 25)
    .Centered()
    .AddControl(logViewer)
    .BuildAndShow();
```

> There is no fluent builder or `Controls.LogViewer()` factory for this control. Construct it directly with its constructor, passing an `ILogService` (typically `windowSystem.LogService`).

## Construction

LogViewerControl has a single constructor that binds it to a log service:

```csharp
public LogViewerControl(ILogService logService)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `logService` | `ILogService` | The log service whose entries are displayed. Must not be null (throws `ArgumentNullException`). Existing entries are loaded immediately; new entries appear as they are logged. |

```csharp
// Bind to the window system's built-in log service
var viewer = new LogViewerControl(windowSystem.LogService);

// Configure via object initializer
var viewer = new LogViewerControl(windowSystem.LogService)
{
    Title = "Diagnostics",
    AutoScroll = true,
    FilterLevel = LogLevel.Information,
    Name = "logViewer"
};
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AutoScroll` | `bool` | `true` | Whether to automatically scroll to show new entries. Delegates to the internal `ScrollablePanelControl`. |
| `FilterLevel` | `LogLevel` | `LogLevel.Trace` | Minimum log level to display; entries below this level are filtered out. Changing it refreshes the view. |
| `FilterCategory` | `string?` | `null` | Category filter. When null, all categories are shown; otherwise only entries whose `Category` matches exactly. Changing it refreshes the view. |
| `Title` | `string?` | `null` | Optional title rendered on a single line above the entries (markup-formatted). When focused, the title is shown in cyan; otherwise grey. |
| `IsEnabled` | `bool` | `true` | Enables or disables interaction (keyboard handling and focus). |
| `HasFocus` | `bool` | (computed) | Whether the control currently has keyboard focus. |
| `CanReceiveFocus` | `bool` | (computed) | True when the control is visible and enabled. |
| `WantsMouseEvents` | `bool` | `true` | Indicates the control consumes mouse events. |

Standard `BaseControl` members such as `Name`, `Width`, `Visible`, `Margin`, and `HorizontalAlignment` also apply.

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `MouseClick` | `EventHandler<MouseEventArgs>` | Declared for interface compatibility; not raised by this control. |
| `MouseDoubleClick` | `EventHandler<MouseEventArgs>` | Declared for interface compatibility; not raised by this control. |
| `MouseRightClick` | `EventHandler<MouseEventArgs>` | Fired when the control is right-clicked (mouse button 3). |
| `MouseEnter` | `EventHandler<MouseEventArgs>` | Declared for interface compatibility; not raised by this control. |
| `MouseLeave` | `EventHandler<MouseEventArgs>` | Declared for interface compatibility; not raised by this control. |
| `MouseMove` | `EventHandler<MouseEventArgs>` | Declared for interface compatibility; not raised by this control. |

> The log content itself comes from the bound `ILogService`. To react to new log entries programmatically, subscribe to `ILogService.LogAdded` rather than to this control's events.

## Keyboard Support

| Key | Action |
|-----|--------|
| **Up Arrow** | Scroll up one line |
| **Down Arrow** | Scroll down one line |
| **Page Up** | Scroll up by one viewport height |
| **Page Down** | Scroll down by one viewport height |
| **Home** | Scroll to the top |
| **End** | Scroll to the bottom |
| **Delete** | Clear all logs (calls `LogService.ClearLogs()`) |
| **Ctrl+C** | Clear all logs (calls `LogService.ClearLogs()`) |

Keyboard input is only processed when the control is enabled and focused.

## Mouse Support

| Action | Result |
|--------|--------|
| **Right Click** | Fires `MouseRightClick` |
| **Scroll Wheel** | Scrolls the log up/down (handled by the internal scroll panel) |
| **Other mouse events** | Delegated to the internal `ScrollablePanelControl` |

## Examples

### Basic Log Viewer Window

```csharp
var logViewer = new LogViewerControl(ws.LogService)
{
    Title = "Library Logs",
    Name = "logViewer"
};

var window = new WindowBuilder(ws)
    .WithTitle("Log Viewer")
    .WithSize(80, 25)
    .Centered()
    .AddControl(logViewer)
    .OnKeyPressed((s, e) =>
    {
        if (e.KeyInfo.Key == ConsoleKey.Escape)
        {
            ws.CloseWindow((Window)s!);
            e.Handled = true;
        }
    })
    .BuildAndShow();
```

### Filtering by Level

```csharp
// Only show warnings and above
var viewer = new LogViewerControl(windowSystem.LogService)
{
    Title = "Warnings & Errors",
    FilterLevel = LogLevel.Warning
};

window.AddControl(viewer);
```

### Filtering by Category

```csharp
// Only show entries logged under the "Window" category
var viewer = new LogViewerControl(windowSystem.LogService)
{
    Title = "Window Events",
    FilterCategory = "Window"
};

window.AddControl(viewer);
```

### Writing to the Log

The control reflects whatever is written to its bound service. Use the `ILogService` convenience methods:

```csharp
var log = windowSystem.LogService;

log.LogInfo("Application started", category: "System");
log.LogDebug("Loaded 42 items");
log.LogWarning("Cache nearly full", category: "Cache");

try
{
    DoWork();
}
catch (Exception ex)
{
    log.LogError("Work failed", ex, category: "Worker");
}
```

> Never use `Console.WriteLine` in a SharpConsoleUI application — it corrupts the UI. Route diagnostics through `LogService` and surface them with a LogViewerControl instead.

### Logging From a Background Thread

The control is thread-safe, so it is fine to log from background work:

```csharp
var viewer = new LogViewerControl(windowSystem.LogService) { Title = "Activity" };
window.AddControl(viewer);

_ = Task.Run(async () =>
{
    for (int i = 0; i < 100; i++)
    {
        windowSystem.LogService.LogInfo($"Processed batch {i}", category: "Worker");
        await Task.Delay(200);
    }
});
```

Entries logged from the background thread are queued and rendered on the next paint; with `AutoScroll` enabled the view follows the newest entry.

### Adjusting the Retained Buffer

The number of entries retained (and therefore trimmed in the viewer) is governed by the service's `MaxBufferSize`:

```csharp
windowSystem.LogService.MaxBufferSize = 1000; // keep the last 1000 entries

var viewer = new LogViewerControl(windowSystem.LogService) { Title = "Logs" };
window.AddControl(viewer);
```

## Best Practices

1. **Bind to the system log service**: Pass `windowSystem.LogService` so the viewer shows the library's own diagnostics alongside your messages.
2. **Filter for focus**: Set `FilterLevel` (and optionally `FilterCategory`) to keep noisy logs readable.
3. **Use categories consistently**: Logging with a `category` makes `FilterCategory` and visual grouping useful.
4. **Keep AutoScroll on for live tails**: Leave `AutoScroll = true` so new entries stay visible; users scrolling up temporarily pauses the follow behavior.
5. **Tune MaxBufferSize**: Adjust the service's `MaxBufferSize` to balance history depth against memory.
6. **Never write to the console**: Route all diagnostics through `LogService`; console output corrupts the UI.

## See Also

- [ListControl](ListControl.md) - For scrollable selectable lists
- [MarkupControl](MarkupControl.md) - For static markup-formatted text (used internally per entry)

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
