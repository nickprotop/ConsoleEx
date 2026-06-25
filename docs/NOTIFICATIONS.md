# Notifications

SharpConsoleUI ships **two complementary notification systems**, both reached through the `ConsoleWindowSystem` instance:

- **`ToastService`** — lightweight, **non-blocking** single-line overlays that stack in a screen corner and auto-dismiss. Best for transient feedback ("Saved", "Sync started").
- **`NotificationStateService`** — title + message notifications that can be **modal/blocking**. Best for messages the user must read or acknowledge.

## Table of Contents

- [Choosing a system](#choosing-a-system)
- [ToastService](#toastservice)
  - [Quick start](#quick-start)
  - [Severities](#severities)
  - [Positions and stacking](#positions-and-stacking)
  - [ToastOptions](#toastoptions)
  - [Dismissing](#dismissing)
  - [Observing state](#observing-state)
  - [Defaults and tuning](#defaults-and-tuning)
  - [Theming](#theming)
- [NotificationStateService](#notificationstateservice)
  - [Showing notifications](#showing-notifications)
  - [Dismissing notifications](#dismissing-notifications)
  - [State and events](#state-and-events)
- [See also](#see-also)

## Choosing a system

| | `ToastService` | `NotificationStateService` |
|---|---|---|
| Access | `ws.ToastService` | `ws.NotificationStateService` |
| Blocks input | Never | Optional (`blockUi: true`) |
| Layout | Single line, anchored to a corner, auto-stacks | Title + message, centered or attached to a window |
| Dismissal | Auto-timeout, click-to-dismiss, or sticky | Auto-timeout or explicit dismiss |
| Typical use | Transient status ("Saved", "Sync started") | Errors/confirmations the user must read |

Both use the same [`NotificationSeverity`](#severities) levels.

## ToastService

`ToastService` (`SharpConsoleUI.Core`) manages transient toast overlays: showing, stacking, and auto-dismissing. It is rendered as a desktop portal and never steals focus or blocks input.

### Quick start

```csharp
// Simplest form — message + severity, default options
ws.ToastService.Show("Saved successfully", NotificationSeverity.Success);
ws.ToastService.Show("Sync started", NotificationSeverity.Info);
ws.ToastService.Show("Disk space is low", NotificationSeverity.Warning);
```

`Show` returns a `string` id you can keep to dismiss the toast later.

### Severities

Each severity supplies an icon and an accent color. The toast border and a matching inner accent bar follow the severity's [color role](THEMES.md).

```csharp
public enum NotificationSeverityEnum
{
    Danger,   // ✘  red    — error conditions (default timeout 6s)
    Info,     // ●  blue   — informational
    None,     //    none   — generic, no icon
    Success,  // ✔  green  — successful operations
    Warning   // ▲  yellow — warnings
}
```

Pass one of the static `NotificationSeverity` instances: `NotificationSeverity.Success`, `.Info`, `.Warning`, `.Danger`, `.None`.

### Positions and stacking

Toasts anchor to one of five positions and stack away from the anchored edge with no gap between them:

```csharp
public enum ToastPosition
{
    BottomRight,  // default
    TopRight,
    BottomLeft,
    TopLeft,
    BottomCenter
}
```

Set a process-wide default, or override per toast via [`ToastOptions`](#toastoptions):

```csharp
ws.ToastService.DefaultPosition = ToastPosition.TopRight;

ws.ToastService.Show("Anchored top-right", NotificationSeverity.Info,
    new ToastOptions(Position: ToastPosition.TopRight));
```

Multiple toasts stack automatically; the service reflows the stack as toasts are added and dismissed.

### ToastOptions

`ToastOptions` is an immutable record overriding per-toast behavior. All members are optional:

```csharp
public sealed record ToastOptions(
    int? Timeout = null,            // auto-dismiss in ms; null = severity default
    bool Sticky = false,           // true = never auto-dismiss
    ToastPosition? Position = null  // null = service DefaultPosition
);
```

```csharp
// Custom timeout
ws.ToastService.Show("Uploading…", NotificationSeverity.Info,
    new ToastOptions(Timeout: 10_000));

// Sticky — stays until clicked or DismissAll()
ws.ToastService.Show("Connection lost", NotificationSeverity.Danger,
    new ToastOptions(Sticky: true));
```

### Dismissing

```csharp
string id = ws.ToastService.Show("Working…", NotificationSeverity.Info,
    new ToastOptions(Sticky: true));

ws.ToastService.Dismiss(id);   // returns false if no matching toast
ws.ToastService.DismissAll();  // clears every active toast at once
```

Non-sticky toasts also dismiss automatically after their timeout, and **clicking a toast dismisses it**.

`ToastService` implements `IDisposable`; `Dispose()` calls `DismissAll()`.

### Observing state

The service is observable, which makes it easy to mirror toast state into a status bar, badge, or test:

```csharp
// INotifyPropertyChanged for HasToasts, ActiveCount, DefaultPosition
ws.ToastService.PropertyChanged += (_, e) => { /* e.PropertyName */ };

// Observable collection of the currently visible toasts
ws.ToastService.ActiveToasts;   // ObservableCollection<ToastInfo>

// Immutable snapshot: ActiveToasts + TotalShown + TotalDismissed counters
ToastState state = ws.ToastService.CurrentState;
bool any = ws.ToastService.HasToasts;
int count = ws.ToastService.ActiveCount;

// Events
ws.ToastService.ToastShown        += (_, e) => { /* e.Toast, e.PreviousState, e.CurrentState */ };
ws.ToastService.ToastDismissed    += (_, e) => { /* e.Toast, ... */ };
ws.ToastService.AllToastsDismissed += (_, _) => { };
ws.ToastService.StateChanged      += (_, state) => { };
```

`ToastInfo` exposes `Id`, `Message`, `Severity`, `Position`, and `Sticky`.

### Defaults and tuning

Toast defaults live in `Configuration.ControlDefaults`:

| Constant | Default | Meaning |
|---|---|---|
| `ToastDefaultTimeoutMs` | `3000` | Auto-dismiss for non-error toasts |
| `ToastErrorTimeoutMs` | `6000` | Auto-dismiss for `Danger` toasts |
| `ToastMaxWidth` | `48` | Max toast width, in columns |
| `ToastEdgeMargin` | `1` | Margin between the stack and the screen edge |
| `ToastGap` | `0` | Vertical gap between stacked toasts |

### Theming

Toast borders and the inner accent bar resolve from the severity's color role against the active theme, so toasts adapt to light/dark themes automatically. See [THEMES.md](THEMES.md) for the role palette.

### Demo

A runnable showcase of every option lives at `Examples/DemoApp/DemoWindows/ToastsWindow.cs` (severities, positions, sticky, stacking, dismiss-all).

## NotificationStateService

`NotificationStateService` manages title + message notifications, including modal ones that block the UI until dismissed. Reach it via `ws.NotificationStateService`.

### Showing notifications

```csharp
string ShowNotification(
    string title,
    string message,
    NotificationSeverity severity,
    bool blockUi = false,
    int? timeout = 5000,            // null = no auto-dismiss
    Window? parentWindow = null
);
```

```csharp
// Auto-dismisses after 5 seconds
ws.NotificationStateService.ShowNotification(
    "File Saved", "Your document has been saved.", NotificationSeverity.Success);

// Persistent + modal — user must dismiss, UI is blocked meanwhile
ws.NotificationStateService.ShowNotification(
    "Error", "Failed to connect to database.",
    NotificationSeverity.Danger, blockUi: true, timeout: null);

// Custom timeout
ws.NotificationStateService.ShowNotification(
    "Processing", "Operation in progress…",
    NotificationSeverity.Info, timeout: 10_000);

// Attached to a specific window
ws.NotificationStateService.ShowNotification(
    "Validation Error", "Please fill in all required fields.",
    NotificationSeverity.Warning, parentWindow: formWindow);
```

### Dismissing notifications

```csharp
string id = ws.NotificationStateService.ShowNotification(
    "Info", "Message", NotificationSeverity.Info);

ws.NotificationStateService.DismissNotification(id);
ws.NotificationStateService.DismissAllNotifications();
```

### State and events

```csharp
NotificationState State = ws.NotificationStateService.State;
bool any   = ws.NotificationStateService.HasNotifications;
int count  = ws.NotificationStateService.ActiveCount;

NotificationInfo? info = ws.NotificationStateService.GetNotification(id);

ws.NotificationStateService.NotificationShown     += (_, e) => { /* e.Notification.Title */ };
ws.NotificationStateService.NotificationDismissed += (_, e) => { };
```

## See also

- [State Services](STATE-SERVICES.md) — all built-in state services
- [Themes](THEMES.md) — color roles used by toast borders and severities
- [Common Patterns](patterns.md) — quick recipes
