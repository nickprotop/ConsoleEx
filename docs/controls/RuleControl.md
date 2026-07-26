# RuleControl

A horizontal divider line with an optional title — and, when you want it, a progress bar in
disguise.

## Overview

`RuleControl` draws a horizontal line across its container, optionally with a title embedded in it.
It is the standard way to separate sections of a window, and it backs both `Controls.Rule(title)`
and `Controls.Separator()`.

It also has a second mode most dividers don't: `SetProgress` fills the rule left-to-right with a
gradient, and `SetIndeterminate` sweeps a shimmering segment across it. That turns a section divider
into an unobtrusive progress indicator without adding a control or changing the layout.

The control is display-only — it takes no focus and handles no input.

> **Naming gotcha:** `Controls.Separator()` returns a **`RuleControl`** (a horizontal rule with an
> empty title), *not* a `SeparatorControl`. The `SeparatorControl` type is the **vertical**
> separator, created with `Controls.VerticalSeparator()`. See
> [Vertical separators](#vertical-separators-separatorcontrol) below.

## Quick Start

```csharp
// A titled section divider
window.AddControl(Controls.Rule("Settings"));

// A plain horizontal line
window.AddControl(Controls.Separator());
```

With the builder, when you need colors or alignment:

```csharp
var rule = Controls.RuleBuilder()
    .WithTitle("Advanced")
    .TitleCenter()
    .WithColor(Color.Grey35)
    .Build();
```

## Builder API

Create a builder with `Controls.RuleBuilder()`. The `Controls.Rule(title)` and
`Controls.Separator()` factories return a finished `RuleControl` directly.

### Title

```csharp
.WithTitle(string title)
.WithTitleAlignment(TextJustification alignment)
.TitleLeft()
.TitleCenter()
.TitleRight()
```

### Appearance

```csharp
.WithColor(Color color)
.WithBorderStyle(BorderStyle style)          // Line style (default: Single)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
```

### Layout and identity

```csharp
.WithWidth(int width)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.WithMargin(Margin margin)
.Visible(bool visible = true)
.WithName(string name)
.WithTag(object tag)
.WithStickyPosition(StickyPosition position)
.StickyTop()
.StickyBottom()
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string?` | `null` | Text embedded in the line; `null` or empty draws a plain rule |
| `TitleAlignment` | `TextJustification` | `Left` | Where the title sits along the line |
| `Color` | `Color?` | `null` | Line color; theme color when null |
| `BorderStyle` | `BorderStyle` | `Single` | Line style |
| `ColorRole` | `ColorRole` | `Default` | Semantic color role |
| `ColorRoleMode` | `ThemeMode?` | `null` | Optional theme mode override |
| `Outline` | `bool` | `false` | Outline styling |
| `ProgressRatio` | `float` | `0` | Current progress, `0.0`–`1.0` (read-only) |
| `IsProgressActive` | `bool` | `false` | True while determinate or indeterminate progress is showing (read-only) |
| `IsIndeterminate` | `bool` | `false` | True while the shimmer is running (read-only) |

### Progress methods

```csharp
void SetProgress(float ratio, ColorGradient gradient);
void SetIndeterminate(ColorGradient gradient, TimeSpan? cycleDuration = null);
void ClearProgress(TimeSpan? fadeDuration = null);
```

| Method | Behaviour |
|--------|-----------|
| `SetProgress` | Fills the rule left-to-right with the gradient up to `ratio` (clamped to `[0,1]`). Cancels any shimmer or clear animation |
| `SetIndeterminate` | Sweeps a gradient segment across the rule in a loop. `cycleDuration` defaults to **1500 ms** |
| `ClearProgress` | Cancels the shimmer and fades progress to zero. `fadeDuration` defaults to **300 ms**; pass `TimeSpan.Zero` to clear immediately |

## Events

None. `RuleControl` implements `INotifyPropertyChanged` via `BaseControl` — see
[Data Binding](../binding.md).

## Keyboard and mouse support

None — this is a display-only control.

## Examples

### Section dividers

```csharp
window.AddControl(Controls.Rule("Connection"));
window.AddControl(hostInput);
window.AddControl(portInput);

window.AddControl(Controls.Rule("Advanced"));
window.AddControl(timeoutInput);
```

### A plain line between blocks

```csharp
window.AddControl(Controls.Separator());   // a RuleControl with an empty title
```

### Centred title with a muted color

```csharp
var rule = Controls.RuleBuilder()
    .WithTitle("[dim]optional[/]")
    .TitleCenter()
    .WithColor(Color.Grey35)
    .WithMargin(0, 1, 0, 1)
    .Build();
```

### The divider as a progress bar

A section rule that reports the work happening beneath it, without adding a control:

```csharp
var rule = Controls.RuleBuilder().WithTitle("Sync").WithName("syncRule").Build();
window.AddControl(rule);

// Determinate
rule.SetProgress(0.42f, ColorGradient.Parse("blue→cyan"));

// Indeterminate, while the total is unknown
rule.SetIndeterminate(ColorGradient.Parse("cool"), TimeSpan.FromMilliseconds(1200));

// Done — fades out over 300 ms
rule.ClearProgress();
```

### Vertical separators (`SeparatorControl`)

For a **vertical** divider — between toolbar buttons, or between columns — use `SeparatorControl`
via `Controls.VerticalSeparator()`. It is a different type from `RuleControl`, non-interactive and
non-focusable, and draws a single vertical line character.

```csharp
// Default: '│'
window.AddControl(Controls.VerticalSeparator());

// With horizontal breathing room on both sides
window.AddControl(Controls.VerticalSeparator(horizontalMargin: 1));
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Character` | `char` | `'│'` | The character used to draw the line |
| `ForegroundColor` | `Color?` | `null` | Line color; inherits when null |
| `BackgroundColor` | `Color?` | `null` | Background; inherits when null |
| `ColorRole` | `ColorRole` | `Default` | Semantic color role |
| `Outline` | `bool` | `false` | Outline styling |

`ToolbarControl` and `NavigationView` place these for you, so you rarely construct one directly.

## Best Practices

1. **Use `Controls.Rule(title)` for section headers** — a titled rule reads better than a label
   followed by a bare line.
2. **Know which "separator" you want.** `Controls.Separator()` is horizontal (a `RuleControl`);
   `Controls.VerticalSeparator()` is vertical (a `SeparatorControl`). The names are close and the
   types are not interchangeable.
3. **Reach for the progress mode instead of adding a control** when a section is loading — it keeps
   the layout stable, which a bar appearing and disappearing does not.
4. **Prefer `ClearProgress()` with its default fade** over `TimeSpan.Zero`; the fade reads as
   completion rather than as the UI glitching.
5. **Use `WithColorRole` rather than fixed colors** so rules follow the active theme.
6. **Don't stack rules to create spacing.** Use margins — `.WithMargin(0, 1, 0, 1)` — so the divider
   stays one line.

## See Also

- [HorizontalSplitterControl](HorizontalSplitterControl.md) — a *draggable* horizontal divider
- [PanelControl](PanelControl.md) — a bordered box, when you want to group rather than divide
- [ProgressBarControl](ProgressBarControl.md) — a dedicated progress indicator
- [ToolbarControl](ToolbarControl.md) — places vertical separators automatically
- [Gradients](../GRADIENTS.md) — gradient syntax for the progress modes
- [Themes](../THEMES.md) — color roles

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
