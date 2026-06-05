# Threading & Async

SharpConsoleUI runs on a cooperative single-threaded model — one UI thread owns
all rendering and event dispatch — with well-defined APIs for moving work off
that thread and for marshalling results back onto it.

## Table of Contents

- [The UI thread model](#the-ui-thread-model)
- [Golden rule: never block the UI thread](#golden-rule-never-block-the-ui-thread)
- [Async event handlers](#async-event-handlers)
- [Marshalling from background threads](#marshalling-from-background-threads)
- [Contract vs notification events](#contract-vs-notification-events)
- [The unresponsive watchdog](#the-unresponsive-watchdog)

---

## The UI thread model

`ConsoleWindowSystem.Run()` drives a tight main loop on the calling thread.
Everything that reads or writes UI state runs on this thread:

| What runs **on** the UI thread | What runs **off** it |
|---|---|
| Input dispatch — control `ProcessKey` / `ProcessMouseEvent` | `Window.WindowThreadDelegateAsync` — runs on a background `Task` |
| Button `Click` handlers | `Task.Run(…)` — anything you explicitly push off-thread |
| Paint events — `PreBufferPaint` / `PostBufferPaint` | Timer callbacks, file-system watchers, socket receive loops |
| Window lifecycle — `Activated`, `OnResize`, and similar handlers | |
| Actions marshalled via `EnqueueOnUIThread` / `InvokeAsync` | |

A `SynchronizationContext` tied to the UI thread is installed for the lifetime
of `Run()`. This means that if you write an `async` handler and you are already
on the UI thread, `await` will resume you on the UI thread automatically —
exactly the same model as WinForms and WPF.

```csharp
// This handler is called on the UI thread. After the await, execution
// continues on the UI thread — no manual marshalling needed.
button.ClickAsync += async (s, e) =>
{
    var result = await FetchDataAsync();   // suspends; resumes on UI thread
    label.SetContent(new List<string> { result });
};
```

### Why blocking on async work deadlocks

The flip side of "continuations resume on the UI thread" is that **blocking the UI
thread while waiting for an `await` to resume deadlocks**. Consider:

```csharp
button.Click += (s, e) =>          // a *synchronous* handler on the UI thread
{
    var data = FetchDataAsync().Result;   // ❌ blocks the UI thread
    // ...
};
```

Step by step, with the UI `SynchronizationContext` installed:

1. `.Result` blocks the **UI thread**, waiting for `FetchDataAsync()` to finish.
2. Inside `FetchDataAsync`, some `await` completes on a thread-pool thread and
   needs to run its continuation (the code after the `await`).
3. Because the context was captured, that continuation is **posted back to the
   UI thread** — via `EnqueueOnUIThread` — to be run by the loop.
4. But the UI thread is frozen at step 1 (`.Result`), so the loop never drains
   the queue, so the continuation never runs, so `.Result` never completes.

Each side waits for the other forever. The watchdog detects the stall and
force-exits. The cure is to never block: make the handler `async` and `await`,
or keep the synchronous handler synchronous and push the work to a background
task that reports back via `InvokeAsync` / `EnqueueOnUIThread`.

> This is the *same* deadlock WinForms and WPF have, for the same reason — it is
> intrinsic to a single-threaded UI with a capturing `SynchronizationContext`.
> A plain console app (no context) does **not** have it, because the continuation
> in step 3 would resume on the thread pool instead of the UI thread. That is why
> installing the context is the one upgrade behavior that can affect existing apps.

### Opting out (`InstallSynchronizationContext`)

If you are upgrading an existing app that deliberately blocks on async work on the
UI thread and you cannot fix every call site yet, you can restore the legacy
behavior (continuations resume on the thread pool) by disabling the context:

```csharp
var options = new ConsoleWindowSystemOptions
{
    InstallSynchronizationContext = false   // default is true
};
var system = new ConsoleWindowSystem(RenderMode.Buffer, options: options);
```

With it disabled, `await` in your handlers resumes on a thread-pool thread, so you
**must** marshal any UI mutation back yourself via `InvokeAsync` /
`EnqueueOnUIThread`. `IsOnUIThread`, `VerifyAccess`, and `InvokeAsync` keep working
regardless of this setting. Leaving it at the default (`true`) is recommended for
new code — it is what makes the async event handlers above safe.

---

## Golden rule: never block the UI thread

Blocking the UI thread freezes rendering and input for every open window. Even a
50 ms stall is perceptible; a second-long block will trigger the watchdog.

| DON'T (freezes the UI) | DO instead |
|---|---|
| `task.Wait()` / `task.Result` in a handler | `await task` (use an async handler) |
| synchronous I/O (e.g. `HttpClient.GetString()`) | `await` the async I/O API |
| `Thread.Sleep(…)` in a handler | `await Task.Delay(…)` |
| heavy CPU work in a handler | `await Task.Run(() => Work())`, then update UI |
| a lock contended with a window thread | marshal via `InvokeAsync` / `EnqueueOnUIThread` |

---

## Async event handlers

Most user-facing notification events have an async twin named `<Name>Async` with
the delegate type `SharpConsoleUI.Core.AsyncEventHandler<TArgs>`:

```csharp
Task Handler(object? sender, TArgs args)
```

Async and sync variants both exist and both fire. You can subscribe to either or
both on the same event.

```csharp
// Sync subscription — fine for lightweight handlers
button.Click += (s, e) =>
{
    statusLabel.SetContent(new List<string> { "Clicked!" });
};

// Async subscription — use when the handler needs to await I/O or delay
button.ClickAsync += async (s, e) =>
{
    statusLabel.SetContent(new List<string> { "Loading…" });
    var data = await _service.LoadAsync();          // does not block the loop
    statusLabel.SetContent(new List<string> { data });
};
```

Because the UI `SynchronizationContext` is active, the continuation after
`await` runs back on the UI thread, so updating controls directly after the
`await` is safe — no marshalling required.

Exceptions thrown by async handlers are routed to the SharpConsoleUI log
(see `SHARPCONSOLEUI_DEBUG_LOG`) rather than propagated to the caller.

---

## Marshalling from background threads

Use these APIs on `ConsoleWindowSystem` when you need to touch UI state from a
background thread:

### `EnqueueOnUIThread(Action action)`

Fire-and-forget marshal. The action is queued and runs on the next loop
iteration. Returns immediately; does not wait for the action to complete.

```csharp
// File-watcher callback — runs on a thread-pool thread
_watcher = fileSystem.WatchDirectory(path, _ =>
{
    ws.EnqueueOnUIThread(() =>
    {
        RefreshFileList();
        statusBar.SetText("Refreshed");
    });
});
```

### `Task InvokeAsync(Action work)` and `Task<T> InvokeAsync<T>(Func<T> work)`

Marshal and await. If the caller is already on the UI thread the delegate runs
inline; otherwise it is queued and the returned `Task` completes when the action
has finished.

```csharp
private async Task UpdateFromBackgroundAsync(string newText)
{
    var data = await _service.ComputeAsync(newText);   // off-thread

    // Marshal the UI update and wait for it to complete
    await ws.InvokeAsync(() =>
    {
        label.SetContent(new List<string> { data });
        window.Invalidate(true);
    });
}
```

`InvokeAsync<T>` lets you read UI state from a background context:

```csharp
int selectedIndex = await ws.InvokeAsync(() => list.SelectedIndex);
```

### `bool IsOnUIThread`

Returns `true` when the calling thread is the UI thread.

```csharp
void SafeUpdate(string text)
{
    if (ws.IsOnUIThread)
        label.SetContent(new List<string> { text });
    else
        ws.EnqueueOnUIThread(() => label.SetContent(new List<string> { text }));
}
```

### `void VerifyAccess()`

Throws `InvalidOperationException` if called from any thread other than the UI
thread. Use this defensively in methods that must only be called from the UI
thread:

```csharp
void UpdateCriticalState(int value)
{
    ws.VerifyAccess();   // throws if not on UI thread
    _criticalControl.Value = value;
}
```

---

## Contract vs notification events

Not every event has an async twin. Events that return a value or carry back-
behaviour to the framework stay **synchronous** and have no `<Name>Async`
counterpart, because the framework must read the result before it can proceed.

| Synchronous-only event | What the framework reads back |
|---|---|
| `OnClosing` | `e.Allow` — whether to permit the close |
| `KeyPressed` / `PreviewKeyPressed` | `e.Handled` — whether to suppress further routing |
| `*Changing` events | `e.Cancel` — whether to veto the change |
| `Unresponsive` | `e.ShowBanner` — whether to display the built-in freeze banner |
| `Recovered` | `e.FullRefresh` — whether to do a full-screen repaint |

High-frequency events are also synchronous by design to avoid per-event `Task`
allocations: `Mouse*`, `*Hovered`, slider `ValueChanged`, and `Scrolled`.

---

## The unresponsive watchdog

SharpConsoleUI monitors the main loop in the background. When the loop stalls
past a configurable threshold, the watchdog raises two events on
`ConsoleWindowSystem`:

- **`Unresponsive`** (`EventHandler<Core.UnresponsiveEventArgs>`) — raised on
  the **watchdog timer thread** when the stall is detected.
- **`Recovered`** (`EventHandler<Core.RecoveredEventArgs>`) — raised on the
  **UI thread** after the loop becomes responsive again.

> **Thread-safety warning**
> `Unresponsive` handlers fire on a separate timer thread, not the UI thread.
> They **must** be thread-safe and **must not** touch any UI state. Safe
> operations: log a message, set an `int` or `bool` flag with `Volatile.Write`,
> write raw ANSI to stderr, signal a `ManualResetEventSlim`.

### `UnresponsiveEventArgs` members

| Member | Type | Description |
|---|---|---|
| `StalledFor` | `TimeSpan` | How long the loop has been blocked |
| `Phase` | `Core.MainLoopPhase` | Loop phase where the stall was detected: `Unknown`, `Input`, `Drain`, `Render`, or `Idle` |
| `BlockedIn` | `string?` | Name of the method or component that was executing when the stall was detected, if available |
| `TimestampUtc` | `DateTime` | UTC time when the stall was detected |
| `ShowBanner` | `bool` (settable) | Set to `false` to suppress the built-in freeze overlay and display your own |

### `RecoveredEventArgs` members

| Member | Type | Description |
|---|---|---|
| `WasStalledFor` | `TimeSpan` | Total duration of the stall |
| `TimestampUtc` | `DateTime` | UTC time of recovery |
| `FullRefresh` | `bool` (settable) | Set to `false` to skip the automatic full-screen repaint after recovery |

### Code sample

```csharp
ws.Unresponsive += (sender, e) =>
{
    // Called on the WATCHDOG TIMER THREAD — no UI access allowed
    Console.Error.WriteLine(
        $"[WATCHDOG] UI stalled for {e.StalledFor.TotalSeconds:F1}s " +
        $"in phase {e.Phase}, blocked in: {e.BlockedIn ?? "unknown"}");

    // Suppress the built-in banner and show nothing (or write raw ANSI here)
    e.ShowBanner = false;
};

ws.Recovered += (sender, e) =>
{
    // Called on the UI thread — safe to update controls
    logger.LogWarning(
        "UI recovered after {Duration}ms stall",
        e.WasStalledFor.TotalMilliseconds);

    // Keep the automatic full-screen repaint (default: true)
    // e.FullRefresh = false;  // uncomment to skip it
};
```

### Watchdog configuration

Pass a `WatchdogOptions` record (from `SharpConsoleUI.Configuration`) via
`ConsoleWindowSystemOptions.Watchdog`:

```csharp
var ws = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: new ConsoleWindowSystemOptions(
        Watchdog: new WatchdogOptions(
            Enabled: true,
            StaleThresholdMs: 2000,
            UnresponsiveThresholdMs: 5000,
            PollIntervalMs: 500,
            ShowUnresponsiveBanner: true,
            FullRefreshOnRecovery: true
        )
    ));
```

| Field | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Enable or disable the watchdog entirely |
| `StaleThresholdMs` | `int` | `2000` | Milliseconds before the loop is considered stale |
| `UnresponsiveThresholdMs` | `int` | `5000` | Milliseconds before `Unresponsive` is raised |
| `PollIntervalMs` | `int` | `500` | How often the watchdog timer checks the loop |
| `ShowUnresponsiveBanner` | `bool` | `true` | Show the built-in freeze overlay by default |
| `FullRefreshOnRecovery` | `bool` | `true` | Trigger a full-screen repaint after recovery by default |

---

## See Also

- [Configuration Guide](CONFIGURATION.md) — `ConsoleWindowSystemOptions` reference
- [Patterns Cookbook](patterns.md) — Pattern 3: async data updates, Pattern C: `EnqueueOnUIThread`
- [State Services](STATE-SERVICES.md) — runtime state management
