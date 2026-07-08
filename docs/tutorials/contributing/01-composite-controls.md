# Contributor Tutorial 1: Composite Controls

> **Difficulty:** Beginner (contributor) | **Prerequisites:** .NET 8+ and a local clone of the repo | **Estimated reading time:** ~15 minutes
>
> **←** [Contributing](README.md) | **Next →** [Adding a Control](02-adding-a-control.md)

---

**What you'll build:** A small composite control — a labeled value pair (a caption plus a value, e.g. `Downloads   1,204`) — by *arranging existing controls*. You will not write a single line of paint or layout math.

## Why a composite is the safest first contribution

Every rendering bug in a terminal UI lives in the same few places: painting cells, measuring text width (Unicode is hard), and clipping/scrolling. A **composite control does none of that.** It only *arranges* controls that already handle their own paint, scroll, and Unicode width. Because you never touch the render engine, you *cannot* introduce a rendering regression there — the worst you can do is arrange the children wrong, which is obvious on screen and cheap to fix.

The framework already ships two composites you can read as reference implementations. Both are "honest compositions" — they subclass a real container and host real child controls, delegating all the hard work downward:

- **`SharpConsoleUI/Controls/PanelControl.cs`** — a bordered panel implemented as a `CollapsiblePanel` whose body is a single child `MarkupControl`. It re-uses the panel's border, colours, and container behaviour; it adds none of its own paint code.
- **`SharpConsoleUI/Controls/ChatTranscriptControl.cs`** — a chat transcript that subclasses `ScrollablePanelControl` and hosts one `CollapsiblePanel` (with a `MarkupControl` body) per message. Its own doc-comment says it best: *"Scrolling, wrapping, markdown rendering, selection and the collapse animation are all provided by those child controls — none of it is re-implemented here."*

Keep those two files open in another tab. When this tutorial says "model on the reference control," that is what it means: copy the *shape*, not a snippet.

## Step 1: Decide where the file goes

Two homes, depending on your goal:

- **Contributing to the framework** → `SharpConsoleUI/Controls/LabeledValueControl.cs`, alongside `PanelControl.cs` and `ChatTranscriptControl.cs`. This is what you PR.
- **Just want it in your own app** → any file in your app project. Same code; you skip the PR steps at the end.

Every source file in `SharpConsoleUI/Controls/` opens with the standard licence banner. Copy it verbatim from any existing control (e.g. the top of `PanelControl.cs`):

```csharp
// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
```

The full style rules your PR is reviewed against are in [CODE_QUALITY.md](../../CODE_QUALITY.md) — worth a skim, but the banner and "no invented paint code" are the load-bearing ones for a composite.

## Step 2: The composite shape — subclass a container, host children

A composite is **a class that subclasses an existing container control and builds its children in the constructor.** That is the entire pattern, and it is exactly what the reference controls do:

- `PanelControl : CollapsiblePanel` — inherits a bordered container, replaces its body with a child.
- `ChatTranscriptControl : ScrollablePanelControl` — inherits a scrolling container, adds one child panel per message via `AddControl(panel)`.

For a caption + value laid out **side by side**, the natural container is `HorizontalGridControl` — a row of columns. So our composite subclasses it and adds two columns.

Both `HorizontalGridControl` and the container it lives in expose the container capability interface **`IControlHost`** (`SharpConsoleUI/Controls/IControlHost.cs`), whose members are:

```csharp
public interface IControlHost
{
    void AddControl(IWindowControl control);
    void RemoveControl(IWindowControl control);
    void ClearControls();
    IReadOnlyList<IWindowControl> Children { get; }
}
```

That is the whole "hosting" contract — you don't implement it yourself, you *inherit* it from the container you subclass. (There is a separate rendering abstraction, **`IContainer`** in `SharpConsoleUI/Controls/IContainer.cs`, that the container types already implement for the layout engine. A composite never touches it directly — that's the point.)

> **Do not invent host methods.** If you need the child controls, or need to clear them, use the inherited `AddControl` / `RemoveControl` / `ClearControls` / `Children` from `IControlHost` exactly as `ChatTranscriptControl` does — grep it for `AddControl(` and `RemoveControl(` to see the real calls.

Here is the skeleton. It holds references to its two child controls (so it can update them later, the way `ChatTranscriptControl` keeps a `MarkupControl? Body` per message), builds them in the constructor, and exposes `Caption`/`Value` as reactive properties:

```csharp
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Controls
{
    /// <summary>
    /// A labeled value pair: a caption on the left and a value on the right,
    /// laid out side by side (e.g. "Downloads   1,204"). A composition over
    /// <see cref="HorizontalGridControl"/> — it arranges two <see cref="MarkupControl"/>
    /// children and adds no paint or layout code of its own.
    /// </summary>
    public class LabeledValueControl : HorizontalGridControl
    {
        private readonly MarkupControl _captionChild;
        private readonly MarkupControl _valueChild;

        public LabeledValueControl(string caption, string value)
        {
            // Column 1: the caption. Column 2: the value.
            // (built out in Step 3)
        }
    }
}
```

The pattern to internalise: **subclass a container, keep field references to the child controls, build them in the constructor.** That's every composite in the repo.

## Step 3: Build and arrange the children

The children are just existing controls, built with the `Controls.*` factories (all defined in `SharpConsoleUI/Builders/Controls.cs`). For a caption + value, two labels are enough — `Controls.Label(text)` returns a ready-made `MarkupControl`, so the caption can carry markup like `[bold]`:

```csharp
_captionChild = Controls.Label($"[dim]{caption}[/]");
_valueChild   = Controls.Label(value);
```

Now arrange them into the two columns of the grid you're subclassing. A column in a `HorizontalGridControl` is a `ColumnContainer`; you build one, add content to it, and register it — mirroring how the `HorizontalGridBuilder` does it internally (`new ColumnContainer(grid)` → `column.AddContent(control)` → `grid.AddColumn(column)`):

```csharp
public LabeledValueControl(string caption, string value)
{
    _captionChild = Controls.Label($"[dim]{caption}[/]");
    _valueChild   = Controls.Label(value);

    var captionColumn = new ColumnContainer(this);
    captionColumn.Width = 12;                 // fixed-width caption gutter
    captionColumn.AddContent(_captionChild);
    AddColumn(captionColumn);

    var valueColumn = new ColumnContainer(this);
    valueColumn.AddContent(_valueChild);      // takes the remaining width
    AddColumn(valueColumn);
}
```

`ColumnContainer(this)`, `AddContent(...)`, and `AddColumn(...)` are all real members — `ColumnContainer` lives in `SharpConsoleUI/Controls/ColumnContainer.cs` and `AddColumn` in `SharpConsoleUI/Controls/HorizontalGridControl/HorizontalGridControl.cs`. That is the complete layout of the composite: two labels, two columns, zero paint code. The grid measures, arranges, and paints the columns; the labels measure, arrange, and paint their own (Unicode-correct) text.

To let callers update the value live (a downloads counter, say), expose a property that writes through to the child. Follow the reactive property contract used everywhere in the codebase — model it on how `ChatTranscriptControl.MessagesSelectable` writes through to its child bodies:

```csharp
public string Value
{
    get => _value;
    set
    {
        if (!SetProperty(ref _value, value)) return;
        _valueChild.SetContent(new System.Collections.Generic.List<string> { value });
    }
}
```

This is the **reactive property contract** every control setter in the codebase follows: `SetProperty` change-guards (returns `false` and bails when the value is unchanged), raises `INotifyPropertyChanged`, and invalidates — so you never call `Invalidate` by hand. Only after it confirms a real change do you write the new text through to the child. It matches how `ChatTranscriptControl.MessagesSelectable` does `if (!SetProperty(ref _field, value)) return;` before writing through to its children. `SetProperty` is available because the composite subclasses a `BaseControl`-derived container. See [patterns.md](../../patterns.md) and CLAUDE.md rule #5b for the full contract.

## Step 4: Add a `Controls.LabeledValue(...)` builder

Every control in the framework is reachable through a `Controls.<Name>()` factory in `SharpConsoleUI/Builders/Controls.cs` — that's how `Controls.Markup()`, `Controls.Label()`, `Controls.HorizontalGrid()`, `Controls.Grid()`, `Controls.ScrollablePanel()`, and `Controls.Panel()` are all defined. Add one for your composite so users write `Controls.LabeledValue(...)` in the same style.

For a control this simple, the factory can just construct it directly (exactly like the existing `Controls.Label` and `Controls.Panel(string)` one-liners in that file):

```csharp
// In SharpConsoleUI/Builders/Controls.cs, next to the other factories:

/// <summary>Creates a labeled value pair (a caption plus a value laid out side by side).</summary>
public static LabeledValueControl LabeledValue(string caption, string value) =>
    new LabeledValueControl(caption, value);
```

Callers now get:

```csharp
window.AddControl(Controls.LabeledValue("Downloads", "1,204"));
```

If your composite grows more options, promote this to a fluent builder class instead — see [BUILDERS.md](../../BUILDERS.md) and the existing `HorizontalGridBuilder` for the `Controls.<Name>()` → `Builder` → `.Build()` pattern. A one-argument factory is fine to start.

## Step 5: A "real thing" test

The rule for this codebase (see [CODE_QUALITY.md](../../CODE_QUALITY.md), the *"Real thing" test required* section) is that a test must exercise the **actual usage path end to end**, not just poke the control in isolation. For a composite that means: build it with real children, add it to a real window, drive the framework, and assert the composed result — both parts present.

Tests run headless against **`MockConsoleDriver`** (`SharpConsoleUI.Tests/Infrastructure/MockConsoleDriver.cs`) so there is no real terminal. A minimal "real thing" test for `LabeledValueControl`:

```csharp
[Fact]
public void LabeledValue_hosts_both_caption_and_value_children()
{
    var driver = new MockConsoleDriver();
    var system = new ConsoleWindowSystem(driver);

    var window = new WindowBuilder(system).WithTitle("Test").WithSize(40, 6).Build();

    var labeled = Controls.LabeledValue("Downloads", "1,204");
    window.AddControl(labeled);
    system.AddWindow(window);

    // The composite hosts exactly the two columns we arranged — proof the
    // children are composed, not just that the class constructed.
    Assert.Equal(2, labeled.Children.Count);
}
```

`Children` here is the inherited `IControlHost.Children` from Step 2 — the same property the reference composites expose. That asserts the composition survived being added to a real window.

To go the full distance (assert both parts actually *render* to cells at boundary sizes, re-rendering between action and assert), copy the render-and-read-back recipe from the test section of the [next tutorial](02-adding-a-control.md) and from the existing tests under `SharpConsoleUI.Tests/` that use `MockConsoleDriver`. The point of *this* tutorial is that your control is composed of already-tested parts, so a composition-level assertion carries most of the weight.

## Step 6: Open the PR

You've added three things — a control, a builder factory, and a test — and changed nothing that already existed. That's the ideal composite PR: strictly additive.

- Fill in the repo's PR checklist: [`.github/pull_request_template.md`](../../../.github/pull_request_template.md) (tick **New feature**, describe what you arranged, and note the test).
- Re-read the **No breaking changes** rule in [CONTRIBUTING.md](../../../CONTRIBUTING.md): SharpConsoleUI has real NuGet users, so we never remove or rename existing public API — we add new ones. A composite is inherently safe here because it *only adds*: a new control class, a new `Controls.LabeledValue(...)` factory. As long as you didn't touch an existing signature, you're within the rule.
- Run `dotnet format SharpConsoleUI/SharpConsoleUI.csproj` before committing — CI has a blocking format gate.

That's a complete, mergeable contribution built without writing one line of paint or layout math — the safest way to make your first change to the framework.

## What you learned

- Why a composite can't break the render engine — it only *arranges* controls that own their own paint/scroll/Unicode width.
- The composite pattern: **subclass a container, keep field references to child controls, build them in the constructor** — modelled on `PanelControl` and `ChatTranscriptControl`.
- Hosting children via the inherited `IControlHost` surface (`AddControl` / `Children`), never hand-rolled.
- Arranging children with `Controls.Label()` into a `HorizontalGridControl`'s `ColumnContainer` columns.
- Adding a `Controls.LabeledValue(...)` factory in `Builders/Controls.cs`.
- A "real thing" test against `MockConsoleDriver` that asserts the composition survives.
- Opening a strictly-additive PR under the no-breaking-changes rule.

---

**←** [Contributing](README.md) | **Next →** [Adding a Control](02-adding-a-control.md)
