# WizardControl

A discoverable, wizard-shaped control that runs a multi-step `Flow.Wizard<TState>()` **inline** — in a pane or region of an existing window — instead of opening a modal window per step.

## Overview

`WizardControl` is the obvious door for "I want a wizard." When you reach for it, IntelliSense finds it, and a few lines later a multi-step wizard is running inside your layout:

```csharp
var wiz = new WizardControl();          // or Controls.Wizard()
panel.AddControl(wiz);

var result = await wiz.Run(Flow.Wizard<InstallState>()
    .WithStepIndicator()
    .Step((ctx, s) => /* ... */)
    .Step((ctx, s) => /* ... */));

if (result.Completed)   Use(result.Value!);
else if (result.Cancelled) { /* user backed out */ }
```

**It IS a `FlowControl`.** `WizardControl` subclasses [`FlowControl`](FlowControl.md) the same way `PanelControl` subclasses `CollapsiblePanel` — a presetted, honestly-named specialization, **not** a wrapper or facade. It inherits the entire inline-flow surface unchanged (rendering, focus and child hosting, `AsHost()`, the idle/running/done lifecycle, and the `Run<TState>(FlowWizardBuilder<TState>)` entry point). It adds only a wizard-flavoured identity:

- a discoverable type name (`WizardControl` / `Controls.Wizard()`), and
- a wizard-friendly default `Placeholder` (`[dim]No wizard running.[/]`), which you can override.

It does **not** re-implement the wizard loop. `wiz.Run(builder)` is the inherited `FlowControl.Run<TState>(...)`; the wizard engine (`Flow.Wizard`, the navigation loop, Back/Commit/Stay, the standardized button row) is reused as-is.

## Generics live on the builder, not the control

The control is **non-generic** — like every other control it drops cleanly into the control tree, containers, and collections as a plain `IWindowControl`. The wizard's state type stays on `Flow.Wizard<TState>()` (the builder), where it is used to author strongly typed steps and surfaces as the returned `FlowResult<TState>`. This keeps the control honest (no vestigial type parameter on a tree node) while preserving full type safety where it matters — authoring the steps.

## Lifecycle, focus, sizing

Inherited from [`FlowControl`](FlowControl.md):

- **States:** idle → running → done, re-runnable. Idle/done show the `Placeholder`.
- **Focus:** a normal focus scope — Tab moves into the wizard's body/buttons and back out to siblings; the surrounding UI stays live (no modal block).
- **Cancel:** via the Cancel button or token (or removing the control mid-flow) → `FlowResult.Cancelled`. Cancel is never an exception.
- **Sizing:** fills its slot; the banner and button toolbar are fixed bands and the step body scrolls when it overflows.
- **Re-entrancy:** calling `Run` while a wizard is already running throws `InvalidOperationException`.

## Scope: inline vs. modal

`WizardControl` runs a wizard **inline** (it is a `FlowControl`). To run the *same* wizard as a **modal** instead, skip the control and run the builder directly:

```csharp
var result = await Flow.Wizard<InstallState>()
    .WithStepIndicator()
    .Step(/* ... */)
    .Run(ws, parent);          // modal (ModalWindowHost), or .WithSeamlessHost() for one reused window
```

So: reach for `WizardControl` when the wizard should live **in a region** of your UI; use `Flow.Wizard<T>().Run(ws, parent)` when it should be a **popup**.

See also: [FlowControl](FlowControl.md), [Composable Flows](../FLOWS.md), [GridControl](GridControl.md)
