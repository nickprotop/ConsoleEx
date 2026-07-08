# Contributor Tutorial 3: Dialogs

> **Difficulty:** Intermediate (contributor) | **Prerequisites:** Read [Adding a Control](02-adding-a-control.md) first | **Estimated reading time:** ~25 minutes
>
> **←** [Adding a Control](02-adding-a-control.md) | **Contributing** → [Hub](README.md)

---

**What you'll build:** `Dialogs.PickAsync<T>(system, title, items)` — a new modal that shows a list of items and returns the one the user chooses (or `default` if cancelled). You'll add it *alongside* the existing `ConfirmAsync` / `PromptAsync` / `RunWithProgressAsync` without changing any of them — the additive discipline in practice.

The [previous tutorial](02-adding-a-control.md) added a *control* — a leaf that paints its own cells. This one adds a *dialog primitive*: a modal window that runs an async round-trip with the user and hands back a typed result. You will not touch the render engine at all. A dialog is pure plumbing: a small content class that assembles existing controls into the three-band modal shape, plus a one-method entry point. Everything hard — layout, scrolling, focus, the border tint, closing on completion — is already solved by the shared host.

**Read the real thing alongside this tutorial.** Open two source files and keep them next to you:

- `SharpConsoleUI/Flows/PrimitiveStepContents.cs` — home of `ConfirmContent` and `PromptContent`.
- `SharpConsoleUI/Dialogs/MessageDialogs.cs` — home of `Dialogs.PromptAsync` and the shared `ShowContentModal`.

Your `PickContent<T>` is structurally a `PromptContent` whose body is a **selectable list** instead of a single-line text field. If you can read `PromptContent` and `PromptAsync`, you can write the picker — every framework API you need is already used by them.

---

## Step 1: The dialog architecture

Every built-in dialog in SharpConsoleUI is the same **three-band modal**:

```
┌─ StickyTop ────────────────────────┐   ← title banner + accent rule  (built by the HOST)
│  ⟳  My Dialog                      │
│  ────────────────────────────────  │
│                                     │
│  (scrollable body)                  │   ← your BuildContent(chrome)
│                                     │
│  ────────────────────────────────  │   ← ruler + right-aligned buttons (your BuildBottomBand)
│                          [ Cancel ] │   ← StickyBottom
└─────────────────────────────────────┘
```

Three pieces cooperate:

1. **A content class** implementing two interfaces:
   - `IFlowStepContent<T>` — supplies `IWindowControl BuildContent(FlowChrome chrome)` (the scrollable middle band) and `Task<T?> Completion` (the result the caller awaits).
   - `IFlowChromeBands` — supplies `IReadOnlyList<IWindowControl> BuildBottomBand(FlowChrome chrome)` (the ruler + button toolbar).

   The content owns a `TaskCompletionSource<T?>` and exposes it as `Completion`. When the user acts — clicks a button, activates a list item, or dismisses the window — the content calls `_tcs.TrySetResult(...)` and the awaiting caller unblocks.

2. **`ShowContentModal`** (in `MessageDialogs.cs`) — the shared host. It builds a modal `WindowBuilder`, adds the top band + wrapped body + bottom band as **window children** (only the window's content layout honours `StickyPosition`), tints the border by severity, wires `OnClosed` → your dismiss handler, and closes the window automatically when `Completion` finishes.

3. **The entry point** — a `public static Task<T?> PickAsync<T>(...)` on the `Dialogs` class that constructs your content, builds a `FlowChrome`, calls `ShowContentModal`, and returns `content.Completion`.

> The HOST always builds the **top band** from `FlowChrome`. Content classes never build their own title banner — that is what keeps every dialog visually uniform. You only supply the middle body and the bottom button band.

Background reading (skim, don't memorize): [`docs/FLOWS.md`](../../FLOWS.md) for the flow/step model and [`docs/DIALOGS.md`](../../DIALOGS.md) for the dialog primitives.

## Step 2: Author `PickContent<T>`

Add a new class to `SharpConsoleUI/Flows/PrimitiveStepContents.cs`, next to `ConfirmContent` and `PromptContent`. Model it on `PromptContent` line for line — the only real difference is that the body is a selectable list and the list's activation event (not a text field's `Entered`) resolves the result.

```csharp
/// <summary>
/// A picker-dialog body: displays a selectable list of items and completes with the
/// item the user activates (Enter / double-click), or <c>default(T)</c> on Cancel or dismiss.
/// </summary>
/// <typeparam name="T">The type of the items offered for selection.</typeparam>
internal sealed class PickContent<T> : IFlowStepContent<T>, IFlowChromeBands
{
    private readonly TaskCompletionSource<T?> _tcs = new();
    private readonly string _message;
    private readonly IReadOnlyList<T> _items;
    private readonly Func<T, string> _labelSelector;
    private readonly NotificationSeverityEnum _severity;

    /// <summary>
    /// Initializes a new <see cref="PickContent{T}"/>.
    /// </summary>
    /// <param name="message">The prompt label displayed above the list.</param>
    /// <param name="items">The items offered for selection.</param>
    /// <param name="labelSelector">Maps each item to its display text.</param>
    /// <param name="severity">Severity that controls the glyph and accent role.</param>
    public PickContent(
        string message,
        IReadOnlyList<T> items,
        Func<T, string> labelSelector,
        NotificationSeverityEnum severity = NotificationSeverityEnum.Info)
    {
        _message = message;
        _items = items;
        _labelSelector = labelSelector;
        _severity = severity;
    }

    /// <inheritdoc/>
    public Task<T?> Completion => _tcs.Task;

    /// <inheritdoc/>
    public event System.Action? StateChanged;

    /// <inheritdoc/>
    public IWindowControl BuildContent(FlowChrome chrome)
    {
        // A selectable list. Each ListItem carries its source item in Tag, so activation
        // can hand the real T back — not just the display string.
        var listBuilder = Ctl.List()
            .WithName("flow-pick-list")
            .Selectable(true)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithMargin(1, 0, 1, 1);

        foreach (var item in _items)
            listBuilder.AddItem(_labelSelector(item), tag: item);

        // Enter / double-click on a row resolves the picker with that row's source item.
        listBuilder.OnItemActivated((_, listItem) =>
        {
            if (listItem.Tag is T chosen)
                _tcs.TrySetResult(chosen);
        });

        // Scrollable body: message label + the list, filling the window content height so the
        // StickyBottom band anchors to the true window bottom (matching PromptContent).
        return Ctl.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .AddControl(Ctl.Markup()
                .AddLine(MarkupParser.Escape(_message))
                .WithMargin(1, 1, 1, 0)
                .Build())
            .AddControl(listBuilder.Build())
            .Build();
    }

    /// <summary>StickyBottom band: ruler + right-aligned toolbar holding the Cancel button.</summary>
    public IReadOnlyList<IWindowControl> BuildBottomBand(FlowChrome chrome)
    {
        var role = FlowContentHelpers.SeverityToRole(_severity);

        var cancelBtn = Ctl.Button("Cancel")
            .WithName("flow-pick-cancel")
            .Build();
        cancelBtn.Click += (_, _) => CancelFromDismiss();

        return FlowContentHelpers.BuildBottomBand(role, cancelBtn);
    }

    /// <summary>Resolves the content as cancelled (<c>default</c>) when the host window is dismissed.</summary>
    internal void CancelFromDismiss() => _tcs.TrySetResult(default);
}
```

Every member here mirrors a real one in `PromptContent`:

| `PromptContent` | `PickContent<T>` |
|---|---|
| `TaskCompletionSource<string?> _tcs` | `TaskCompletionSource<T?> _tcs` |
| `Task<string?> Completion => _tcs.Task` | `Task<T?> Completion => _tcs.Task` |
| `BuildContent` returns message + `Ctl.Prompt(...)` input | `BuildContent` returns message + `Ctl.List(...)` |
| `promptCtrl.Entered += (_, text) => _tcs.TrySetResult(text)` | `.OnItemActivated((_, listItem) => _tcs.TrySetResult(chosen))` |
| `BuildBottomBand` with OK + Cancel buttons | `BuildBottomBand` with a Cancel button |
| `CancelFromDismiss() => _tcs.TrySetResult(null)` | `CancelFromDismiss() => _tcs.TrySetResult(default)` |

Notes on the real APIs used:

- `Ctl` is the file's `using Ctl = SharpConsoleUI.Builders.Controls;` alias — already present at the top of `PrimitiveStepContents.cs`.
- `Ctl.List()` returns a `ListBuilder`. `AddItem(string text, object? tag = null)` attaches your source item to the row via `ListItem.Tag`, and `OnItemActivated(EventHandler<ListItem>)` fires when a row is activated. These are the real builder methods — verify them in `SharpConsoleUI/Builders/ListBuilder.cs`.
- The picker has **no OK button** — activating a list row *is* the affirmative action (exactly like pressing Enter in `PromptContent`'s field). The only button is Cancel, so the bottom band is a one-button toolbar like `ProgressContent`'s.

> **Reactive-contract note:** this class is dialog plumbing — it configures controls through their builders and never defines a control property setter. If you ever *do* add a property to a control while wiring a dialog, it must go through `SetProperty(ref _field, value)`, never a hand-rolled `set { _field = value; ... }`. See CLAUDE.md rule #5b. It doesn't arise here, but keep it in mind.

## Step 3: The `Dialogs.PickAsync<T>` entry point

Add the entry point to `SharpConsoleUI/Dialogs/MessageDialogs.cs`, inside the `Dialogs` class, right next to `PromptAsync`. It must live inside that class because `ShowContentModal` is `internal`. Model it on `PromptAsync` (`MessageDialogs.cs:239`):

```csharp
/// <summary>
/// Shows a modal picker that lists <paramref name="items"/> and completes with the item the
/// user activates (Enter / double-click), or <c>default(T)</c> on Cancel or dismiss.
/// </summary>
/// <typeparam name="T">The type of the items offered for selection.</typeparam>
/// <param name="windowSystem">The window system to host the dialog in.</param>
/// <param name="title">Title displayed in the dialog window chrome.</param>
/// <param name="items">The items offered for selection.</param>
/// <param name="message">The prompt label displayed above the list. Defaults to <c>"Select an item:"</c>.</param>
/// <param name="labelSelector">
/// Maps each item to its display text. Defaults to <c>item?.ToString()</c>.
/// </param>
/// <param name="severity">
/// Severity that controls the glyph, accent rule color, and window border tint.
/// Defaults to <see cref="NotificationSeverityEnum.Info"/>.
/// </param>
/// <param name="parent">
/// Optional parent window. When provided the dialog is modal to that window only.
/// </param>
/// <returns>
/// A <see cref="Task{TResult}"/> that completes with the chosen item, or <c>default(T)</c>
/// on Cancel/dismiss.
/// </returns>
public static Task<T?> PickAsync<T>(
    ConsoleWindowSystem windowSystem,
    string title,
    IReadOnlyList<T> items,
    string message = "Select an item:",
    Func<T, string>? labelSelector = null,
    NotificationSeverityEnum severity = NotificationSeverityEnum.Info,
    Window? parent = null)
{
    var selector = labelSelector ?? (item => item?.ToString() ?? string.Empty);
    var content = new PickContent<T>(message, items, selector, severity);
    var chrome = new FlowChrome(title, widthHint: 50, severity: severity, autoSizeHeight: true);

    ShowContentModal(
        windowSystem,
        FlowContentHelpers.BuildTopBand(chrome),
        content.BuildContent(chrome),
        content.BuildBottomBand(chrome),
        chrome,
        parent,
        onDismiss: content.CancelFromDismiss,
        completion: content.Completion);

    return content.Completion;
}
```

This is `PromptAsync` with three swaps: the content type (`PickContent<T>` for `PromptContent`), the return type (`Task<T?>` for `Task<string?>`), and the extra `items` / `labelSelector` parameters. The `FlowChrome` construction is identical (`widthHint: 50, autoSizeHeight: true`), and the `ShowContentModal(...)` argument list is copied verbatim — same eight arguments in the same order, including `onDismiss: content.CancelFromDismiss` and `completion: content.Completion`.

Because `PickContent<T>` lives in `SharpConsoleUI.Flows`, add `using SharpConsoleUI.Flows;` at the top of `MessageDialogs.cs` if it isn't already there (the existing primitives reference `FlowContentHelpers` and `FlowChrome`, so it will be).

## Step 4: Cancellation

You don't write a cancellation path — you inherit one. `ShowContentModal` wires:

```csharp
modal.OnClosed += (_, _) => onDismiss();
```

So when the user presses **Esc** or clicks the title-bar close button, the window closes, `OnClosed` fires, and `onDismiss` — which you passed as `content.CancelFromDismiss` — runs. For the picker that is:

```csharp
internal void CancelFromDismiss() => _tcs.TrySetResult(default);
```

`TrySetResult` (not `SetResult`) means it's safe even if the result was already set by an activation — the first setter wins, the second is a no-op. The awaiting caller gets `default(T)` on cancel, exactly as `PromptAsync` returns `null` and `RunWithProgressAsync` returns `default`. The Cancel button uses the same path (its `Click` handler calls `CancelFromDismiss()` directly), so a click and an Esc are indistinguishable to the caller.

## Step 5: The "real thing" test

Drive the **real** `Dialogs.PickAsync` on a headless `MockConsoleDriver`-backed system, resolve it, and assert the awaited result. Model the setup on `SharpConsoleUI.Tests/Dialogs/RunWithProgressAsyncTests.cs`, which uses the exact headless-await pattern (`TestWindowSystemBuilder.CreateTestSystem(...)`, then `await DialogsApi.XAsync(...)`).

The awaited `PickAsync` completes when the content's `TaskCompletionSource` is resolved. In a headless test the cleanest way to drive that is through the public surface the user sees — but you can also resolve directly through the content, exactly as the progress tests resolve by reporting progress and returning. Add `SharpConsoleUI.Tests/Dialogs/PickAsyncTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using DialogsApi = SharpConsoleUI.Dialogs.Dialogs;

namespace SharpConsoleUI.Tests.Dialogs;

public class PickAsyncTests
{
    [Fact]
    public async Task Cancel_ResolvesToDefault()
    {
        var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
        var items = new List<string> { "Alpha", "Beta", "Gamma" };

        // Kick off the dialog but do not await yet — capture the pending task.
        var pending = DialogsApi.PickAsync<string>(sys, "Pick one", items);

        // Dismiss the modal (Esc / close) → OnClosed → CancelFromDismiss → default.
        var modal = sys.WindowStateService.ActiveWindow!;
        modal.Close();

        var result = await pending;
        Assert.Null(result); // default(string) == null
    }
}
```

Then add the **positive** "real thing" test — launch the real modal and drive a real row activation, then assert the awaited value equals the chosen item. The point of the "real thing" rule (see CLAUDE.md) is that the test exercises the actual dialog path — `PickAsync` → `ShowContentModal` → a live modal window — not an isolated `PickContent` in a vacuum. Locate the list by its `"flow-pick-list"` name (`Window.FindControl` exists in `Window.Controls.cs`), select a row, and drive activation through the **real input path** so the list's `ItemActivated` fires exactly as it would for a user:

```csharp
[Fact]
public async Task ActivateItem_ResolvesToThatItem()
{
    var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
    var items = new List<string> { "Alpha", "Beta", "Gamma" };

    var pending = DialogsApi.PickAsync<string>(sys, "Pick one", items);

    // Find the live list in the real modal (ActiveWindow is public on the system).
    var modal = sys.ActiveWindow!;
    var list = modal.FindControl<ListControl>("flow-pick-list")!;

    list.SelectedIndex = 1;                     // highlight "Beta"
    // Drive Enter through the real input path so ItemActivated fires as it would for a user.
    // Mirror the exact key-injection helper the existing List/dialog tests use
    // (e.g. InputStateService.EnqueueKey + sys.Input.ProcessInput, or the list's ProcessKey).

    var result = await pending;
    Assert.Equal("Beta", result);
}
```

> **Point-at-reference:** `ListControl` has no public "activate the selected row" method — activation is raised internally from the keyboard/mouse handlers (`ListControl.Keyboard.cs` / `ListControl.Mouse.cs` invoke `ItemActivated`). So the affirmative test must reach activation through the **real input path** (enqueue an Enter key and pump input), exactly as the CLAUDE.md "real thing" rule prescribes and as the existing List tests already do. Before writing the test, open a current `ListControl` test and copy its precise key-injection helper — do not invent an `Activate...()` method. The load-bearing shape is fixed: launch the real `PickAsync`, drive the real modal via real input, `await` the returned task, assert the value survives.

## Step 6: The additive discipline

Look back at what you changed:

- **Added** one class: `internal sealed class PickContent<T>` in `PrimitiveStepContents.cs`.
- **Added** one method: `public static Task<T?> PickAsync<T>(...)` in `MessageDialogs.cs`.
- **Added** one test file.

You **changed nothing else**. `ConfirmContent`, `PromptContent`, `ProgressContent<T>`, `ConfirmAsync`, `PromptAsync`, and `RunWithProgressAsync` are byte-for-byte untouched. `ShowContentModal`, `FlowChrome`, `BuildTopBand`, and `BuildBottomBand` were *reused*, not modified. No existing method signature moved; no default behavior changed.

That is the whole discipline: **new capability is a new symbol, never a mutation of an existing one.** SharpConsoleUI has real third-party NuGet users, and the rule is absolute — see the no-breaking-changes section of [CONTRIBUTING.md](../../../CONTRIBUTING.md). Because `PickAsync` and `PickContent<T>` are brand-new names, there is zero chance of breaking a caller: nobody was using a symbol that didn't exist. This is why "add alongside" is almost always the safe move — an additive change cannot regress code that never referenced it.

## Step 7: Open the PR

With the class, the method, and both tests in place and the suite green, open a pull request:

- Title: `feat(dialogs): add Dialogs.PickAsync<T> modal picker`.
- In the description, state plainly that it is **additive**: one new content class, one new entry point, existing dialogs untouched.
- Confirm you ran `dotnet format SharpConsoleUI/SharpConsoleUI.csproj` (the CI format gate blocks on tabs) and that the full test suite passes.

You've now added a dialog primitive the same way the framework's own `Prompt` and `Confirm` were built — by composing the three-band host, not reinventing it.

---

**←** [Adding a Control](02-adding-a-control.md) | **Contributing** → [Hub](README.md)
