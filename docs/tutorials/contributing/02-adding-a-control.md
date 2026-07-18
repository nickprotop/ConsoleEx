# Contributor Tutorial 2: Adding a Control

> **Difficulty:** Intermediate (contributor) | **Prerequisites:** Read [Composite Controls](01-composite-controls.md) first | **Estimated reading time:** ~25 minutes
>
> **←** [Composite Controls](01-composite-controls.md) | **Next →** [Dialogs](03-dialogs.md)

---

**What you'll build:** `BadgeControl` — a small primitive that renders a colored count pill like `[ 3 ]`. It's tiny, but it exercises every piece the framework requires of a real control: layout, the reactive property contract, color roles, correct Unicode width, a builder, and a test.

The [previous tutorial](01-composite-controls.md) built a *composite* — a control that only *arranges* other controls and never touches the render engine. That is the safest first contribution precisely because it can't introduce a paint bug. This tutorial goes one level deeper: a **primitive** that paints its own cells and measures its own size. That means you now own the two hardest parts of a terminal UI — measuring text width correctly and writing cells to the buffer — so we'll be careful about both.

**Read `SpinnerControl.cs` alongside this tutorial.** `SharpConsoleUI/Controls/SpinnerControl.cs` is the simplest real primitive in the codebase, and it is the model for everything below: what it inherits, how it measures, how it paints, and how it resolves its color role. Every structural claim in this tutorial matches what `SpinnerControl` actually does — keep it open in a split pane and compare as you go.

## Step 1: Decide where the file goes

A primitive control lives beside its peers:

```
SharpConsoleUI/Controls/BadgeControl.cs
SharpConsoleUI/Builders/BadgeControlBuilder.cs
```

Start every source file with the project's license banner (copy it verbatim from the top of `SpinnerControl.cs`):

```csharp
// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
```

## Step 2: The interfaces — implement only what the badge needs

SharpConsoleUI follows Interface Segregation: a control implements one small interface per capability it actually has, rather than one broad interface with methods it must stub out. A badge is not focusable and does not react to the mouse, so it implements *neither* `IFocusableControl` *nor* `IMouseAwareControl`. It *does* have a themable surface (the pill color), so — exactly like `SpinnerControl` — it implements `IColorRoleableControl`. See [patterns.md](../../patterns.md) for the full interface catalog and the ISP rationale.

The base class `BaseControl` already supplies the layout plumbing (`ActualWidth`, `Margin`, `Invalidate`, `SetProperty`). It leaves you three **abstract** members you *must* implement — `ContentWidth`, `MeasureDOM`, `PaintDOM` — plus `GetLogicalContentSize`, which is **virtual**: it has a default that returns `ContentWidth` by one line plus vertical margin, so you only override it when your control is taller than one line or sizes itself differently. The badge overrides it anyway, to state its size explicitly. So the declaration mirrors `SpinnerControl : BaseControl, IColorRoleableControl`:

```csharp
using System.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A small count badge that renders a colored pill such as <c>[ 3 ]</c>. The pill color
/// is driven by the control's <see cref="ColorRole"/> (e.g. Primary for a normal count,
/// Danger for a warning count).
/// </summary>
public class BadgeControl : BaseControl, IColorRoleableControl
{
}
```

`IColorRoleableControl` requires three members — `ColorRole`, `ColorRoleMode`, and `Outline`. `SpinnerControl` implements them as a small region; copy that shape verbatim (all three go through `SetProperty`, which we cover in Step 4):

```csharp
    #region ColorRole

    private ColorRole _role = ColorRole.Default;
    private ThemeMode? _colorRoleMode;
    private bool _outline;

    /// <inheritdoc/>
    public ColorRole ColorRole
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    /// <inheritdoc/>
    public ThemeMode? ColorRoleMode
    {
        get => _colorRoleMode;
        set => SetProperty(ref _colorRoleMode, value);
    }

    /// <inheritdoc/>
    public bool Outline
    {
        get => _outline;
        set => SetProperty(ref _outline, value);
    }

    #endregion
```

## Step 3: The `Count` property via the reactive contract

Here is the part a previous tutorial got wrong — and it was caught in review. A control property setter must go through `SetProperty`, **never** a hand-rolled `set { _count = value; ... }`:

```csharp
    private int _count;

    /// <summary>Gets or sets the number shown inside the pill.</summary>
    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }
```

Why this exact form matters. `SetProperty` (defined on `BaseControl`, `SharpConsoleUI/Controls/BaseControl.cs`) does three things in one call:

1. **Change-guards** — returns early if the new value equals the old, so setting `Count = 3` when it's already `3` does no work and fires no notification.
2. **Raises `INotifyPropertyChanged`** (`OnPropertyChanged`) — so data-bound consumers see the change.
3. **Self-invalidates** — calls `Invalidate(Invalidation.Relayout)` on the control itself, which schedules a re-measure and repaint.

That third step is *why the framework is reactive at the property boundary*: assigning `badge.Count = 5` makes the UI update on its own. You never write an `Invalidate` call for a property change. See [patterns.md](../../patterns.md) for the full property/invalidation contract.

> **Do NOT hand-roll `Container?.Invalidate(...)` in a setter.** A setter that pokes the container directly (`set { _count = value; Container?.Invalidate(true); }`) bypasses the change-guard and the notification, and invalidates the wrong node. It is rejected in review. Invalidate *`this`* via `SetProperty`; the framework forwards identity for you.

`SetProperty` issues a `Relayout` (the count can change the pill's width, so layout must re-run). If you were adding an *appearance-only* property that can't change size — a color, say — you'd use the granular `Invalidate(Invalidation.Repaint)` form (with a change-guard and `OnPropertyChanged`), exactly as `SpinnerControl`'s `Color` setter does. `Count` affects width, so `SetProperty`/`Relayout` is correct.

## Step 4: Measure → Arrange → Paint

Every primitive answers two questions for the layout engine: *how big am I?* (measure) and *draw yourself here* (paint). Arrange happens in between and is done for you by the container — it hands your `PaintDOM` the final `bounds`. Read [DOM_LAYOUT_SYSTEM.md](../../DOM_LAYOUT_SYSTEM.md) for the full measure/arrange/paint walk.

First, a small helper for the rendered pill text and its display width — we'll use it in both measure and paint so the two never disagree:

```csharp
    /// <summary>The literal pill text, e.g. "[ 3 ]".</summary>
    private string PillText() => $"[ {_count} ]";

    /// <summary>The pill's display width in terminal columns.</summary>
    private int PillWidth() => UnicodeWidth.GetStringWidth(PillText());
```

Note we measure with `UnicodeWidth.GetStringWidth` (`SharpConsoleUI/Helpers/UnicodeWidth.cs`), **not** `PillText().Length`. `string.Length` counts UTF-16 code units, not terminal columns — it's wrong for wide and combining characters (see the Unicode-aware rendering rules in [CODE_QUALITY.md](../../CODE_QUALITY.md)). For an all-ASCII pill the two happen to agree, but using the correct API from the start means the control stays correct if the format ever gains a wide glyph.

Now the three `BaseControl` overrides. `ContentWidth` reports the intrinsic width; `GetLogicalContentSize` reports intrinsic size including margins; `MeasureDOM` clamps that into the constraints. These mirror `SpinnerControl` exactly:

```csharp
    /// <inheritdoc/>
    public override int? ContentWidth => PillWidth();

    /// <inheritdoc/>
    public override Size GetLogicalContentSize()
    {
        int width = PillWidth();
        int height = 1 + Margin.Top + Margin.Bottom;
        return new Size(width + Margin.Left + Margin.Right, height);
    }

    /// <inheritdoc/>
    public override LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        int width = PillWidth() + Margin.Left + Margin.Right;
        int height = 1 + Margin.Top + Margin.Bottom;
        return new LayoutSize(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
        );
    }
```

`MeasureDOM(LayoutConstraints constraints)` returning a `LayoutSize` is the exact signature from `IDOMPaintable` / `IDOMMeasurable` (`SharpConsoleUI/Layout/IDOMPaintable.cs`). The badge is a one-line control, so height is always `1` (plus vertical margin). `Math.Clamp` is safe here because the constraints always carry `MinWidth ≤ MaxWidth`.

## Step 5: Paint — resolve the role color, then write cells

The paint signature is fixed by `IDOMPaintable` — copy it character-for-character:

```csharp
    /// <inheritdoc/>
    public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground)
    {
        SetActualBounds(bounds);

        int x = bounds.X + Margin.Left;
        int y = bounds.Y + Margin.Top;
        if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom) return;

        // Resolve the pill color from the semantic role via the active theme —
        // never a hardcoded Color literal. Same call SpinnerControl uses.
        Color fg = ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, mode: ColorRoleMode)
                   ?? defaultForeground;
        Color bg = Container?.BackgroundColor ?? defaultBackground;

        // The pill is literal narrow ASCII ("[ 3 ]"), so write each cell with
        // SetNarrowCell — it assumes width-1 and is the correct API for literal
        // narrow characters.
        string text = PillText();
        foreach (var rune in text.EnumerateRunes())
        {
            if (x >= bounds.Right || x >= clipRect.Right) break;
            if (x >= clipRect.X)
                buffer.SetNarrowCell(x, y, rune, fg, bg);
            x++;
        }
    }
```

Two rules from the buffer-write contract (see the Unicode-aware rendering section of [CODE_QUALITY.md](../../CODE_QUALITY.md)) are load-bearing here:

- **Color comes from the role, not a literal.** `ColorResolver.ColorRoleForeground(...)` (`SharpConsoleUI/Helpers/ColorResolver.cs`) derives the color from the active theme's palette for the chosen `ColorRole` — so a `Danger` badge is red *in whatever the current theme calls red*, and it re-themes for free. This is the identical call `SpinnerControl.PaintDOM` makes. Never write `Color.Red` in a control. See [THEMES.md](../../THEMES.md).
- **Literal narrow chars use `SetNarrowCell`.** `SetNarrowCell` assumes a width-1 character and clears cell flags — correct for the brackets, spaces, and ASCII digits of the pill. The other API, `buffer.SetCell(x, y, cell)`, is for cells that came out of `MarkupParser.Parse` (which already carries wide-continuation flags). If your text could contain markup or wide glyphs, you'd parse it instead — that's what `SpinnerControl` does for its (possibly marked-up) frames:

  ```csharp
  var cells = MarkupParser.Parse(text, fg, bg);
  foreach (var cell in cells) { buffer.SetCell(x, y, cell); x++; }
  ```

  A count pill is plain ASCII, so `SetNarrowCell` per rune is the right, simplest choice.

`SetActualBounds(bounds)` (inherited from `BaseControl`) records where the control was painted so hit-testing and the reactive layer know its real position — every `PaintDOM` calls it first, just like `SpinnerControl`.

## Step 6: A `Controls.Badge(int count)` builder

Users create controls through the fluent `Controls.<Name>()` factory, not `new`. Add a builder modeled on `SpinnerBuilder` (`SharpConsoleUI/Builders/SpinnerBuilder.cs`); see [BUILDERS.md](../../BUILDERS.md) for the convention.

`SharpConsoleUI/Builders/BadgeControlBuilder.cs`:

```csharp
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Builders
{
    /// <summary>Fluent builder for <see cref="BadgeControl"/>.</summary>
    public class BadgeControlBuilder : IControlBuilder<BadgeControl>
    {
        private readonly BadgeControl _control = new();

        /// <summary>Sets the count shown in the pill.</summary>
        public BadgeControlBuilder WithCount(int count) { _control.Count = count; return this; }

        /// <summary>Sets the semantic colour role (e.g. Primary, Danger).</summary>
        public BadgeControlBuilder WithColorRole(ColorRole role, ThemeMode? mode = null)
        { _control.ColorRole = role; _control.ColorRoleMode = mode; return this; }

        /// <summary>Renders the badge in outline style.</summary>
        public BadgeControlBuilder Outline(bool outline = true) { _control.Outline = outline; return this; }

        /// <summary>Sets the control name.</summary>
        public BadgeControlBuilder WithName(string name) { _control.Name = name; return this; }

        /// <summary>Builds the configured <see cref="BadgeControl"/>.</summary>
        public BadgeControl Build() => _control;
    }
}
```

Then add the factory to `Controls` (`SharpConsoleUI/Builders/Controls.cs`), right beside the existing `Spinner()` factory:

```csharp
    /// <summary>Creates a new badge builder seeded with a count.</summary>
    /// <param name="count">The initial count shown in the pill.</param>
    /// <returns>A new badge builder.</returns>
    public static BadgeControlBuilder Badge(int count) =>
        new BadgeControlBuilder().WithCount(count);
```

Now a caller writes `Controls.Badge(3).WithColorRole(ColorRole.Danger).Build()`.

## Step 7: The "real thing" test

The rule for this codebase (see [CODE_QUALITY.md](../../CODE_QUALITY.md), the *"Real thing" test required* section) is that a test must exercise the **actual usage path end to end** and, crucially, assert that a change **survives a re-render**. Isolated asserts have passed green while the live control was visibly broken — so we build the real control, paint it, read the cells back, change `Count`, paint again, and confirm the new value is what's on screen.

Tests paint into a `CharacterBuffer` and read cells back with `buffer.GetCell(x, y)` — the recipe used throughout `SharpConsoleUI.Tests/Controls/`. A tiny paint helper mirrors `SpinnerControlTests`:

```csharp
using System.Drawing;
using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

public class BadgeControlTests
{
    private static CharacterBuffer Paint(BadgeControl b, int w = 20, int h = 1)
    {
        var buffer = new CharacterBuffer(w, h);
        var rect = new LayoutRect(0, 0, w, h);
        b.PaintDOM(buffer, rect, rect, Color.White, Color.Black);
        return buffer;
    }

    private static string RowText(CharacterBuffer buffer, int y, int width)
    {
        var sb = new StringBuilder();
        for (int x = 0; x < width; x++)
            sb.Append(buffer.GetCell(x, y).Character.ToString());
        return sb.ToString();
    }

    [Fact]
    public void Badge_renders_pill_and_survives_count_change()
    {
        var badge = new BadgeControl { ColorRole = ColorRole.Primary, Count = 3 };

        // Measure reports the pill's true display width, not string.Length.
        var size = badge.MeasureDOM(new LayoutConstraints(0, 100, 0, 100));
        Assert.Equal(UnicodeWidth.GetStringWidth("[ 3 ]"), size.Width);

        // First render: the pill text is on screen.
        var b1 = Paint(badge);
        Assert.StartsWith("[ 3 ]", RowText(b1, 0, 20));

        // Change the reactive property, re-render, assert the update SURVIVED.
        badge.Count = 42;
        var b2 = Paint(badge);
        Assert.StartsWith("[ 42 ]", RowText(b2, 0, 20));
    }
}
```

The second half is the "real thing" part: it drives the reactive property (`badge.Count = 42`), re-renders, and asserts the *new* value is the one painted — proving the `SetProperty`-based setter actually reaches the screen, not just that the field changed.

> For layout-sensitive controls, CODE_QUALITY.md also asks for a test nested in the **real container** at **boundary sizes**, driven via the **real input path**. A count badge has no input and a fixed one-row height, so the render-and-survive assertion above carries the weight; add a nested-container test if you later give the badge interactive behavior.

## Step 8: Open the PR

You added a new control, a new builder, and a new factory — all strictly additive, so you're safely inside the no-breaking-changes rule (SharpConsoleUI has real NuGet users; we never remove or rename existing public API — we only add). Before opening the PR:

- Run `dotnet build` and `dotnet test` — the new test must be green.
- Run `dotnet format SharpConsoleUI/SharpConsoleUI.csproj` — CI has a blocking format gate (tabs).
- Give every new public member an XML `<summary>` (the compiler warns otherwise).
- Fill in the PR checklist in [`.github/pull_request_template.md`](../../../.github/pull_request_template.md) (tick **New feature**) and re-read the **No breaking changes** section of [CONTRIBUTING.md](../../../CONTRIBUTING.md).

That's a complete primitive control — measure, paint, the reactive property contract, role-driven color, correct Unicode width, a builder, and a survives-a-re-render test — merged as a purely additive change.

## What you learned

- Which interfaces a primitive implements (`BaseControl` + only the capability interfaces it needs — `IColorRoleableControl` for a themable surface, no focus/mouse) — the ISP philosophy.
- The three layout overrides — `ContentWidth`, `GetLogicalContentSize`, and `MeasureDOM(LayoutConstraints) → LayoutSize` — modeled on `SpinnerControl`.
- `PaintDOM(CharacterBuffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground)` — the exact `IDOMPaintable` signature — plus `SetActualBounds` and clip-rect guarding.
- The reactive property contract: `Count` via `SetProperty`, which change-guards, notifies, and self-invalidates — so you never call `Invalidate` by hand, and never `Container?.Invalidate` in a setter.
- Role-driven color via `ColorResolver.ColorRoleForeground` — no hardcoded `Color.` literals; the badge re-themes for free.
- Unicode-correct width with `UnicodeWidth.GetStringWidth`, and writing literal narrow cells with `SetNarrowCell` (vs. `SetCell` for parsed/wide cells).
- A `Controls.Badge(int count)` factory + `BadgeControlBuilder`, modeled on `SpinnerBuilder`.
- A "real thing" test that renders, reads cells back, changes `Count`, and asserts the update survives the re-render.

---

**←** [Composite Controls](01-composite-controls.md) | **Next →** [Dialogs](03-dialogs.md)
