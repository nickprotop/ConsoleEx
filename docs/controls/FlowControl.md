# FlowControl

An embeddable, focusable control that renders any flow inline — in a pane or as a stack section — instead of opening a modal popup.

## Overview

FlowControl is a three-band container (Auto top / Star body / Auto bottom) that presents flow steps *inside* an existing window layout rather than in a separate modal window. It is the in-control counterpart to `ModalWindowHost` (a new modal per step) and `SwapContentHost` (a single reused modal). Under the hood it implements `IFlowHost` via a private `InlineFlowHost`, so the complete flow engine — `Flow.Run`, `Flow.Wizard`, all `FlowContext` verbs — works without any change; you just call `Run` on the control instead of `Flow.Run(ws, parent)`.

**Composition.** `FlowControl` subclasses `GridControl`. It is a three-row grid (`Auto` top band / `Star` body / `Auto` bottom band) and inherits the full container surface for free: child controls participate in the layout tree, take focus and Tab order, route mouse clicks, and report a cursor. This is why the banner, scrollable body, and button toolbar all work as first-class interactive controls placed inside the control.

**Lifecycle.** The control moves through three states:

| State | What is displayed |
|-------|-------------------|
| Idle | `Placeholder` (if set), or empty |
| Running | The three-band flow step: title/rule banner, scrollable body, button toolbar |
| Done | `Placeholder` (or empty) — the control is re-runnable |

**Focus.** Normal focus scope. Tab moves focus in and out of the control (and through its bands) the same way as any other container — no input capture, no blocking loop. The surrounding window UI stays fully interactive while the flow runs.

**Cancel.** The user cancels via the Cancel button or the flow's cancellation token. Cancel resolves the flow as `FlowResult.Cancelled`; no modal is dismissed and no window is closed because there is no separate window.

**Sizing.** Fills its slot. The top and bottom bands are `Auto`-height (sized to their content); the body row is `Star` (fills the remaining height). When the body content is taller than the available space it scrolls within the control's bounds.

**Re-entrant `Run`.** Calling `Run` while a flow is already running throws `InvalidOperationException` synchronously. The existing flow must be awaited or cancelled first.

**Removal mid-flow.** If the control is removed from its parent while a flow is running, the flow's cancellation token is cancelled and the in-flight step is resolved immediately as `FlowVerdict.Cancel`, so the `await` unblocks without waiting for the next step. The `Run` task resolves as `FlowResult.Cancelled`.

### Relationship to hosts

FlowControl is the third built-in `IFlowHost` implementation, designed for inline embedding:

| Host | Where steps appear |
|------|--------------------|
| `ModalWindowHost` (default) | A new modal window per step |
| `SwapContentHost` | One shared modal window, content swapped per step |
| **`FlowControl`** | Inside an existing layout region, no window opened |

Pass `AsHost()` as the `host` parameter to `Flow.Run` or `FlowWizardBuilder.Run` for maximum flexibility, or use the `Run` overloads directly for the common case.

See [FLOWS](../FLOWS.md) for the complete flow engine reference.

## API

### Constructor

```csharp
new FlowControl()
```

Creates an idle, empty control. Shows nothing until `Placeholder` is set or `Run` is called.

### Placeholder

```csharp
IWindowControl? Placeholder { get; set; }
```

The control displayed when the `FlowControl` is idle (before any `Run` call) and after a flow ends. `null` renders the control empty in those states. Setting the property while idle immediately updates the display; setting it during a running flow stores the value for restoration when the flow ends.

### Run overloads

```csharp
Task<FlowResult<T>>    Run<T>(Func<FlowContext, Task<T>> body)
Task<FlowResult<bool>> Run(Func<FlowContext, Task> body)
Task<FlowResult<TState>> Run<TState>(FlowWizardBuilder<TState> wizard) where TState : new()
```

Each overload runs the given body or wizard inline inside this control and returns a `FlowResult` with the terminal state (completed, cancelled, or faulted). The control must be added to a window before `Run` is called; calling `Run` on an unattached control throws `InvalidOperationException`.

The untyped `Run(body)` overload returns `FlowResult<bool>` with `Value == true` on completion.

### AsHost

```csharp
IFlowHost AsHost()
```

Returns the `IFlowHost` that presents steps inside this control. The same instance is returned on every call. Use this to pass the control as a host to `Flow.Run` or `FlowWizardBuilder.Run` directly:

```csharp
await Flow.Run(ws, parent: null, async ctx => { … }, host: fc.AsHost());
await Flow.Wizard<State>().Step(/* … */).Run(ws, parent: null, host: fc.AsHost());
```

### Inherited GridControl surface

`FlowControl` inherits `GridControl`. The three-row definition (`Auto` / `Star` / `Auto`) and step content are managed internally; **do not manually call the inherited grid mutators** (`Place`, `AddControl`, `ClearControls`, `RowDefinitions` edits) — they are used by the flow engine to swap step bands and would corrupt the flow display.

## Quick Start

### Inline wizard in a panel

```csharp
// Create the FlowControl with a placeholder shown while idle.
var fc = new FlowControl
{
    Placeholder = Ctl.Markup("[dim]Click a button to run a flow here[/]")
        .WithMargin(1, 1, 1, 1)
        .Build(),
};

// Host it in a bordered panel to give the inline region a visual frame.
var region = Ctl.Panel()
    .WithHeader("Inline Flow Region")
    .HeaderLeft()
    .Rounded()
    .WithHeight(9)                  // enough room for the three-band layout
    .WithMargin(1, 1, 1, 0)
    .AddControl(fc)
    .Build();

// Wire a button to run a wizard inside the FlowControl.
var runBtn = Controls.Button("Run inline wizard")
    .OnClick(async (_, _, _) =>
    {
        var result = await fc.Run(
            Flow.Wizard<InstallState>()
                .WithStepIndicator()
                .WithTitle("Inline Wizard")
                .Step(async (ctx, s) =>
                {
                    bool go = await ctx.Confirm("Welcome", "Begin the install?", "Begin", "Cancel");
                    return go ? FlowVerdict.Next : FlowVerdict.Cancel;
                })
                .Step(s => new LocationPickerContent("Choose a location:", _locations, c => s.Location = c))
                    .WithStepTitle("Location")
                    .CanGoNext((ctx, s) => !string.IsNullOrWhiteSpace(s.Location))
                .Step(async (ctx, s) =>
                {
                    await ctx.RunWithProgress<bool>(
                        "Installing", $"Installing to {s.Location}…",
                        async (ct, progress) =>
                        {
                            progress.Report("Writing files…");
                            await Task.Delay(600, ct);
                            return true;
                        });
                    s.Installed = true;
                    ctx.Commit();
                    return FlowVerdict.Finish;
                }));

        if (result.Completed)
            windowSystem.NotificationStateService.ShowNotification(
                "Done", $"Installed to {result.Value!.Location}", NotificationSeverity.Success);
    })
    .Build();

window.AddControl(runBtn);
window.AddControl(region);
```

### One-liner confirm

```csharp
var result = await fc.Run(async ctx => await ctx.Confirm("Proceed?", "Continue with this action?"));
if (result.Completed && result.Value)
    DoWork();
```

`result.Value` is `true` when the user confirmed, `false` when they cancelled via the Cancel button; `result.Cancelled` is true when the flow token was cancelled externally.

## See Also

- [FLOWS](../FLOWS.md) — complete flow engine reference (`Flow.Run`, `Flow.Wizard`, `IFlowHost`, `FlowContext`)
- [GridControl](GridControl.md) — the base class; `FlowControl` inherits its layout and focus surface
