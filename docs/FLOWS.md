# Composable Flows

Building multi-step workflows in a terminal UI is tedious: each step needs its own modal window,
navigation state, a Back stack, cancellation plumbing, and exception handling — all wired
together by hand. **Composable Flows** collapses that boilerplate into two lightweight APIs:
`Flow.Run` for free-form imperative flows, and `Flow.Wizard` for declarative multi-step wizards
with standardized navigation.

## Table of Contents

- [Overview](#overview)
- [Tier A — Flow.Run](#tier-a--flowrun)
  - [FlowContext verbs](#flowcontext-verbs)
  - [FlowResult](#flowresult)
  - [Example — single-step flow](#example--single-step-flow)
- [Tier B — Flow.Wizard](#tier-b--flowwizard)
  - [Building a wizard](#building-a-wizard)
  - [Two step forms](#two-step-forms)
  - [Standardized button rows](#standardized-button-rows)
  - [Dynamic button enable — CanGoNext](#dynamic-button-enable--cangonext)
  - [Navigation semantics](#navigation-semantics)
  - [Example — multi-step install wizard](#example--multi-step-install-wizard)
- [Custom step content — IFlowStepContent](#custom-step-content--iflowstepcontent)
- [Hosts — IFlowHost and ModalWindowHost](#hosts--iflowhost-and-modalwindowhost)
- [Roadmap](#roadmap)
- [See also](#see-also)

---

## Overview

The flows API lives in `SharpConsoleUI.Flows`. There are two tiers:

| Tier | Entry point | Use when |
|---|---|---|
| A | `Flow.Run<T>` | Imperative body: call `ctx.Confirm`, `ctx.Prompt`, `ctx.RunWithProgress`, or `ctx.Show` in any order; return the result. |
| B | `Flow.Wizard<TState>` | Declarative ordered steps with Back/Next/Finish navigation, a shared mutable state object, and an optional step indicator. |

The [standalone `Dialogs.*` methods](DIALOGS.md) (`ConfirmAsync`, `PromptAsync`,
`RunWithProgressAsync`) work without any flow setup. `FlowContext` exposes the same three
primitives as `ctx.Confirm`, `ctx.Prompt`, and `ctx.RunWithProgress` so a flow body can call
them in the same way; the context routes them through the current host and wires them to the
flow's single cancellation token.

---

## Tier A — Flow.Run

`Flow.Run` runs an imperative async body and wraps its terminal state (completed, cancelled,
faulted) in a `FlowResult<T>`.

```csharp
// Typed — body returns a value
public static Task<FlowResult<T>> Flow.Run<T>(
    ConsoleWindowSystem ws,
    Window? parent,
    Func<FlowContext, Task<T>> body,
    IFlowHost? host = null)

// Untyped — body returns nothing; result carries bool (Value == true on completion)
public static Task<FlowResult<bool>> Flow.Run(
    ConsoleWindowSystem ws,
    Window? parent,
    Func<FlowContext, Task> body,
    IFlowHost? host = null)
```

| Parameter | Description |
|---|---|
| `ws` | The window system the flow presents into. |
| `parent` | Optional parent window for the default `ModalWindowHost`. |
| `body` | The flow body; receives a `FlowContext`. |
| `host` | Optional custom host. When `null` a `ModalWindowHost` is used. |

### FlowContext verbs

`FlowContext` (handed to the body) exposes:

| Member | Signature | Description |
|---|---|---|
| `Token` | `CancellationToken` | Single cancellation token for the whole flow. Esc / dismiss / host cancel trips it. |
| `Show<TResult>` | `Task<TResult?> Show<TResult>(IFlowStepContent<TResult>, string title = "", FlowButtons buttons = FlowButtons.OkCancel)` | Presents arbitrary typed content through the host. Returns the content value, or `default` on cancel. |
| `Confirm` | `Task<bool> Confirm(string title, string message, string ok = "OK", string cancel = "Cancel", NotificationSeverityEnum severity = Info)` | Built-in confirm dialog. Returns `true` on OK, `false` on cancel/dismiss. |
| `Prompt` | `Task<string?> Prompt(string title, string message, string? initial = null, NotificationSeverityEnum severity = Info)` | Built-in prompt dialog. Returns entered text or `null` on cancel/dismiss. |
| `RunWithProgress<TResult>` | `Task<TResult> RunWithProgress<TResult>(string title, string description, Func<CancellationToken, IProgress<string>, Task<TResult>> work)` | Built-in progress dialog. Returns the work result or `default` on cancel. |
| `Commit` | `void Commit()` | Marks a Back-barrier after side-effecting work. See [Navigation semantics](#navigation-semantics). |

### FlowResult

`FlowResult<T>` is a `readonly struct` carrying the terminal state of a completed flow run:

| Member | Type | Meaning |
|---|---|---|
| `Completed` | `bool` | The flow ran to completion; `Value` is valid. |
| `Value` | `T?` | The value returned by the body. Only meaningful when `Completed`. |
| `Cancelled` | `bool` | The user cancelled (or threw `OperationCanceledException`). |
| `Faulted` | `bool` | An unhandled exception terminated the flow; `Error` holds it. |
| `Error` | `Exception?` | The exception. Only meaningful when `Faulted`. |

Cancel and fault are not exceptions at the call site — the caller inspects the struct:

```csharp
var result = await Flow.Run<string>(ws, window, async ctx => { … });

if (result.Completed)
    UseValue(result.Value!);
else if (result.Cancelled)
    ; // user bailed — no action needed
else if (result.Faulted)
    LogError(result.Error!);
```

### Example — single-step flow

```csharp
var result = await Flow.Run<string>(ws, myWindow, async ctx =>
{
    // Ask a question first
    bool proceed = await ctx.Confirm(
        "Deploy",
        "Deploy to the staging server?",
        ok: "Deploy");

    if (!proceed)
        throw new OperationCanceledException(); // surfaces as FlowResult.Cancelled

    // Run the work with progress
    string log = await ctx.RunWithProgress<string>(
        "Deploying",
        "Connecting…",
        async (ct, progress) =>
        {
            progress.Report("Uploading artifacts…");
            await DeployAsync(ct);
            return "Deployment successful";
        });

    return log;
});

if (result.Completed)
    ShowBanner(result.Value!);
```

---

## Tier B — Flow.Wizard

`Flow.Wizard<TState>` builds a declarative multi-step wizard over a mutable `TState`. The wizard
owns the navigation loop (Next / Back / Cancel / Finish / Stay), the step indicator, and the
Back commit-barrier. The shared state flows through every step.

> To run a wizard **inline** in a region of your UI (rather than as a modal), use the
> [`WizardControl`](controls/WizardControl.md) — a discoverable, wizard-named `FlowControl`:
> `var wiz = new WizardControl(); panel.AddControl(wiz); await wiz.Run(Flow.Wizard<TState>()…);`

### Building a wizard

```csharp
public static FlowWizardBuilder<TState> Flow.Wizard<TState>() where TState : new()
```

Returns a `FlowWizardBuilder<TState>`. Chain fluent methods to configure it, add steps, then
call `.Run(ws, parent)` to execute.

| Method | Description |
|---|---|
| `.Seed(TState state)` | Sets the initial wizard state (otherwise default-constructed). |
| `.WithStepIndicator()` | Enables the `(N/Total)` indicator suffix in each step's title. |
| `.WithTitle(string)` | Default window title for content+buttons steps that don't override it. |
| `.Step(Func<FlowContext, TState, Task<FlowVerdict>>)` | Adds a code-driven step. |
| `.Step(Func<TState, IFlowStepContent<object?>>)` | Adds a content+buttons step; returns `FlowStepConfig<TState>` for per-step overrides. |
| `.Run(ConsoleWindowSystem ws, Window? parent, IFlowHost? host = null)` | Runs the wizard; returns `Task<FlowResult<TState>>`. |

### Two step forms

**Form 1 — Code-driven**

```csharp
.Step(async (ctx, state) =>
{
    bool go = await ctx.Confirm("Welcome", "Begin the install?", "Begin", "Cancel");
    return go ? FlowVerdict.Next : FlowVerdict.Cancel;
})
```

The step body receives the shared `FlowContext` and the mutable `TState`. It calls any
`FlowContext` verb and returns a `FlowVerdict` that drives the navigation loop.

**Form 2 — Content + standardized buttons**

```csharp
.Step(s => new MyPickerContent(s))         // factory builds content; reads/writes s.*
    .WithStepTitle("Choose location")
    .NextLabel("Install")
    .CanGoNext((ctx, s) => !string.IsNullOrWhiteSpace(s.Location))
    .OnNext((ctx, s) =>
        Task.FromResult(string.IsNullOrWhiteSpace(s.Location)
            ? FlowVerdict.Stay
            : FlowVerdict.Next))
```

`.Step(factory)` returns a `FlowStepConfig<TState>` sub-builder with:

| Method | Description |
|---|---|
| `.WithStepTitle(string)` | Title for this step's host frame. |
| `.NextLabel(string)` | Relabels the affirmative (Next/Finish) button. |
| `.BackLabel(string)` | Relabels the Back button. |
| `.CanGoNext(Func<FlowContext, TState, bool>)` | Predicate controlling whether the affirmative button is enabled. Re-evaluated live on `StateChanged`. |
| `.OnNext(Func<FlowContext, TState, Task<FlowVerdict>>)` | Callback when the affirmative button is clicked; its return drives the loop. |
| `.OnBack(Func<FlowContext, TState, Task<FlowVerdict>>)` | Callback when Back is clicked (default `FlowVerdict.Back`). |
| `.OnCancel(Func<FlowContext, TState, Task<FlowVerdict>>)` | Callback when Cancel is clicked (default `FlowVerdict.Cancel`). |

All `FlowStepConfig` members return the sub-builder for further chaining. The next `.Step(…)`,
`.Seed(…)`, `.Run(…)` etc. are forwarded so chaining continues naturally.

### Standardized button rows

The wizard renders a context-aware button row per step. The exact set depends on the step's
position in the sequence:

| Position | Affirmative | Back | Cancel |
|---|---|---|---|
| First of many | Next | — | Cancel |
| Middle | Next | Back | Cancel |
| Last | Finish | Back | Cancel |
| Only step (first **and** last) | Finish | — | Cancel |

On a single-step wizard the only step is simultaneously first and last, so the affirmative
button is **Finish** (unless `.NextLabel` overrides it).

The `FlowVerdict` emitted by each button drives the navigation loop:

| Verdict | Effect |
|---|---|
| `Next` | Advance to the next step. |
| `Finish` | Complete the wizard; return `FlowResult<TState>.Complete(state)`. |
| `Back` | Return to the previous step (subject to the commit barrier). |
| `Cancel` | Abort; return `FlowResult<TState>.Cancel()`. |
| `Stay` | Re-present the current step unchanged (failed validation). |

### Dynamic button enable — CanGoNext

`.CanGoNext` is re-evaluated on every `IFlowStepContent.StateChanged` event. The content must
follow the contract: **mutate state first, then raise `StateChanged`**. The wizard host
re-invokes the predicate and updates the button's enabled state in-place without rebuilding
the window.

```csharp
// Content raises StateChanged after writing the chosen value into state
btn.Click += (_, _) =>
{
    state.Location = choice;     // mutate first
    StateChanged?.Invoke();      // then raise
};
```

```csharp
// Wizard gates Next on the location being set
.CanGoNext((ctx, s) => !string.IsNullOrWhiteSpace(s.Location))
```

### Navigation semantics

| Situation | What happens |
|---|---|
| `FlowVerdict.Back` at step 0 | No-op: the wizard re-presents step 0 unchanged (Stay semantics). No cancel. |
| `FlowVerdict.Back` blocked by commit barrier | Same: re-presents the current step. No cancel. |
| `ctx.Commit()` called in a step | Raises the commit barrier: Back can no longer reach or re-run this step or any earlier one. |
| `FlowVerdict.Stay` | Re-presents the current step (typical validation failure response). |
| `FlowVerdict.Back` (allowed) | Re-runs the prior step with the current `TState` intact (state is never discarded on Back). |

**Back is never silently converted into Cancel.** A blocked Back re-presents the current step
and the flow continues. Code-driven steps that return `Back` unconditionally at step 0 will loop
on the first step indefinitely — the same contract as returning `Stay` unconditionally.

### Example — multi-step install wizard

```csharp
public sealed class InstallState
{
    public string? Location { get; set; }
    public bool Installed { get; set; }
}

FlowResult<InstallState> result = await Flow.Wizard<InstallState>()
    .WithStepIndicator()
    .WithTitle("Install Wizard")

    // Step 1 (code-driven): confirm to begin
    .Step(async (ctx, s) =>
    {
        bool go = await ctx.Confirm(
            "Welcome",
            "This wizard installs the demo package. Begin?",
            ok: "Begin", cancel: "Cancel");
        return go ? FlowVerdict.Next : FlowVerdict.Cancel;
    })

    // Step 2 (content + buttons): pick a location, Next gated on a selection being made
    .Step(s => new LocationPickerContent(
            "Choose an install location:",
            new[] { "/opt/demo", "/usr/local/demo", "~/demo" },
            choice => s.Location = choice))
        .WithStepTitle("Location")
        .NextLabel("Install")
        .CanGoNext((ctx, s) => !string.IsNullOrWhiteSpace(s.Location))
        .OnNext((ctx, s) =>
            Task.FromResult(string.IsNullOrWhiteSpace(s.Location)
                ? FlowVerdict.Stay
                : FlowVerdict.Next))

    // Step 3 (code-driven, final): run the work, commit, then finish
    .Step(async (ctx, s) =>
    {
        await ctx.RunWithProgress<bool>(
            "Installing",
            $"Installing to {s.Location}…",
            async (ct, progress) =>
            {
                for (int k = 1; k <= 4; k++)
                {
                    ct.ThrowIfCancellationRequested();
                    progress.Report($"Step {k}/4: writing {s.Location}…");
                    await Task.Delay(450, ct);
                }
                return true;
            });

        s.Installed = true;
        ctx.Commit();           // Back can never revisit this step
        return FlowVerdict.Finish;
    })

    .Run(ws, myWindow);

if (result.Completed)
{
    var s = result.Value!;
    ShowBanner($"Installed to {s.Location}");
}
else if (result.Cancelled)
{
    ShowStatus("Installation cancelled.");
}
```

---

## Custom step content — IFlowStepContent

`IFlowStepContent<TResult>` is the contract for app-provided step bodies. Implement it to build
custom UI inside a flow frame.

```csharp
public interface IFlowStepContent<TResult>
{
    // Called once per presentation. Returns the control to display.
    IWindowControl BuildContent(FlowChrome chrome);

    // Completes when the body self-resolves (e.g. user presses Enter on a list).
    // Static or button-driven content may leave this permanently incomplete.
    Task<TResult?> Completion { get; }

    // Raised AFTER the content writes its value into internal state.
    // Contract: mutate state, THEN raise.
    event Action? StateChanged;
}
```

`FlowChrome` carries the chrome hints passed by the host:

| Member | Type | Description |
|---|---|---|
| `Title` | `string` | Window/dialog title. |
| `StepIndicator` | `(int Index, int? Count)?` | Step position for the indicator suffix (e.g. `(2, 4)` → "(2/4)"). |
| `WidthHint` | `int?` | Preferred host window width in columns. |
| `HeightHint` | `int?` | Preferred host window height in rows. |
| `Buttons` | `IReadOnlyList<FlowButton>` | The button row to render (empty for primitives that build their own). |
| `RefreshButtons` | `Func<IReadOnlyList<FlowButton>>?` | Called by the host on `StateChanged` to update button enabled state. |
| `AutoSizeHeight` | `bool` | When `true` and `HeightHint` is null, the host sizes the window to fit the content (clamped; scrolls beyond a cap). Default `false`. |
| `Resizable` | `bool` | When `true`, the user can drag-resize the host window (minimize/maximize buttons stay off). Default `false`. |

#### Window height: hints, auto-size, and scrolling

The host picks the window height in this order:

1. **Explicit `HeightHint`** — used verbatim (always wins, even if `AutoSizeHeight` is set).
2. **`AutoSizeHeight = true`** (and `HeightHint` null) — the window fits the content:
   `clamp(bands + content height, FlowAutoSizeMinHeight, terminalHeight − FlowAutoSizeCapMargin)`.
   Short content gives a tight window; tall content grows to the cap, then the body scrolls.
3. **Neither** — a fixed default height.

Regardless of height, a tall step body **scrolls**: the host wraps the step body in a fill
scroll viewport, so content taller than the slot overflows with a scrollbar. (A body that is
already a `ScrollablePanelControl` is used as-is — see `FlowContentHelpers.WrapBody` and the
FlowControl guide.) The framework primitives (`Confirm`/`Prompt`/`Progress`) and the standalone
`Dialogs.*` set `AutoSizeHeight = true`, so built-in dialogs fit their content out of the box.

A minimal content class that self-resolves when a list item is double-clicked:

```csharp
public sealed class LocationPickerContent : IFlowStepContent<object?>
{
    private readonly TaskCompletionSource<object?> _tcs = new();
    private readonly string _prompt;
    private readonly IReadOnlyList<string> _choices;
    private readonly Action<string>? _onSelected;

    public event Action? StateChanged;
    public Task<object?> Completion => _tcs.Task;

    public LocationPickerContent(string prompt, IReadOnlyList<string> choices,
        Action<string>? onSelected = null)
    {
        _prompt = prompt;
        _choices = choices;
        _onSelected = onSelected;
    }

    public IWindowControl BuildContent(FlowChrome chrome)
    {
        var panel = Controls.ScrollablePanel().WithScrollbar(false).Build();

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]{_prompt}[/]")
            .WithMargin(1, 1, 1, 1)
            .Build());

        foreach (var choice in _choices)
        {
            var capture = choice;
            var btn = Controls.Button(capture).WithMargin(1, 0, 1, 0).Build();
            btn.Click += (_, _) =>
            {
                _onSelected?.Invoke(capture);  // write into wizard state
                StateChanged?.Invoke();         // notify dynamic predicates
                // Don't resolve _tcs here when used with content+buttons form;
                // the wizard's affirmative button drives navigation.
            };
            panel.AddControl(btn);
        }

        return panel;
    }
}
```

When used with `ctx.Show<TResult>` (Tier A), resolve `_tcs` on selection to let the step
self-complete. When used with the content+buttons wizard step form (Tier B), leave `_tcs`
unresolved and let the wizard's host-rendered button row drive navigation.

---

## Hosts — IFlowHost and ModalWindowHost

The presentation layer is pluggable via `IFlowHost`:

```csharp
public interface IFlowHost
{
    Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
        IFlowStepContent<TResult> content,
        FlowChrome chrome,
        CancellationToken ct);
}
```

`PresentAsync` receives the content and chrome, renders the step, and resolves to a
`FlowStepOutcome<TResult>` — the typed content value plus the navigation `FlowVerdict` (which
button was clicked, or `Cancel` on dismiss/token).

**`ModalWindowHost`** is the framework default. Each step is presented in a fresh modal window
built with `WindowBuilder.AsModal()`. The host:

- Renders the button row from `FlowChrome.Buttons` (right-aligned, first button is Primary-tinted).
- Wires content `StateChanged` → `FlowChrome.RefreshButtons` → in-place enabled update.
- Handles dismiss (Esc / title-bar close) → `Cancel`.
- Handles token cancellation → `Cancel`.
- Closes the modal in a `finally` — cancel or fault never leaks a window.

To use the default host, pass `host: null` (or omit the parameter) to `Flow.Run` or
`FlowWizardBuilder.Run`.

To supply a custom host, implement `IFlowHost` and pass it via the `host` parameter. The test
suite uses a headless scripted host (`HeadlessFlowHost`) that resolves steps programmatically
without opening any windows.

`SwapContentHost` is a built-in opt-in host that reuses a **single** modal window and swaps its
content between steps (a seamless wizard, with no per-step window open/close). Construct it and
pass it via the `host` parameter, or call `.WithSeamlessHost()` on `Flow.Wizard<TState>()`:

```csharp
var result = await Flow.Wizard<InstallState>()
    .WithSeamlessHost()        // all steps share one window
    .Step(/* ... */)
    .Run(ws, parent);

// or explicitly for Flow.Run:
var host = new SwapContentHost(ws, parent);
await Flow.Run(ws, parent, async ctx => { /* ... */ }, host);
```

The window opens on the first step and closes when the flow completes, cancels, or is dismissed.

For rendering a flow **inline** in a region rather than a modal, see [FlowControl](controls/FlowControl.md).

---

## Roadmap

[**`FlowControl`**](controls/FlowControl.md) is now available. It is an embeddable, focusable
control that renders any flow — `Flow.Run` bodies, `Flow.Wizard` wizards, or raw `IFlowHost`
calls — inside an existing window layout region, with no separate modal window.

---

## See also

- [Dialogs](DIALOGS.md) — standalone Confirm / Prompt / Progress primitives (no flow required)
- [Threading & Async](THREADING_AND_ASYNC.md) — UI thread model and `EnqueueOnUIThread`
- [Notifications](NOTIFICATIONS.md) — transient toasts and modal notification banners
