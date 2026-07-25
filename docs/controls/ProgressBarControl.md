# ProgressBarControl

Progress indicator for tasks — determinate with a percentage, or indeterminate with a pulsing
animation.

## Overview

`ProgressBarControl` shows how far along a task is. In **determinate** mode you set `Value` against
`MaxValue` and the bar fills proportionally, optionally printing the percentage. In
**indeterminate** mode the bar runs a pulsing block back and forth — the right choice when you know
work is happening but not how much is left.

Values are clamped for you: `Value` is held within `[0, MaxValue]`, and `MaxValue` has a floor of
`0.01`, so a stray assignment can't produce a divide-by-zero or a bar past full. The control
stretches horizontally by default.

It is a **display control** — no focus, no keyboard or mouse handling. For a measured quantity that
isn't task progress (CPU load, disk usage) prefer [BarGraphControl](BarGraphControl.md); for a value
the user can change, use [SliderControl](SliderControl.md).

## Quick Start

```csharp
var progress = Controls.ProgressBar()
    .WithHeader("Downloading")
    .WithPercentage(0)
    .ShowPercentage()
    .Build();

window.AddControl(progress);

// Advance it — assigning Value repaints on its own
progress.Value = 45;
```

## Builder API

Create a builder with `Controls.ProgressBar()`.

### Value

```csharp
.WithValue(double value)                   // Current value
.WithMaxValue(double maxValue)             // Full-scale value (default 100)
.WithPercentage(double percentage)         // Convenience: value on a 0–100 scale
```

### Indeterminate mode

```csharp
.Indeterminate(bool indeterminate = true)  // Pulsing animation instead of a fill
.WithAnimationInterval(int milliseconds)   // Pulse tick rate (default 100)
.WithPulseWidth(int width)                 // Width of the moving block (default 5)
```

### Header and readout

```csharp
.WithHeader(string header)
.ShowHeader(bool show = true)
.ShowPercentage(bool show = true)
```

### Size and layout

```csharp
.WithBarWidth(int width)                   // Fixed bar width
.Stretch()                                 // Fill the available width
.WithWidth(int width)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(Margin margin)
.StickyTop()
.StickyBottom()
.Visible(bool visible)
.WithName(string name)
.WithTag(object tag)
```

### Colors

```csharp
.WithFilledColor(Color color)
.WithUnfilledColor(Color color)
.WithColors(Color filled, Color unfilled)
.WithPercentageColor(Color color)
.WithBackgroundColor(Color color)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Value` | `double` | `0` | Current progress, clamped to `[0, MaxValue]` |
| `MaxValue` | `double` | `100.0` | Full-scale value; floored at `0.01` |
| `IsIndeterminate` | `bool` | `false` | Pulsing mode; setting it starts/stops the animation timer |
| `AnimationInterval` | `int` | `100` | Pulse tick interval in milliseconds |
| `PulseWidth` | `int` | `5` | Width of the pulsing block |
| `BarWidth` | `int?` | `null` | Fixed bar width; stretches when null |
| `ShowPercentage` | `bool` | — | Whether the percentage readout is drawn |
| `ShowHeader` | `bool` | — | Whether the header is drawn |
| `Header` | `string?` | `null` | Header text above the bar |
| `FilledColor` | `Color?` | `null` | Filled portion; theme color when null |
| `UnfilledColor` | `Color?` | `null` | Empty track; theme color when null |
| `PercentageColor` | `Color?` | `null` | Percentage text color |
| `BackgroundColor` | `Color?` | `null` | Control background; inherits when null |
| `ColorRole` | `ColorRole` | `Default` | Semantic color role |
| `ColorRoleMode` | `ThemeMode?` | `null` | Optional theme mode override |
| `Outline` | `bool` | `false` | Outline styling |

The control sets `HorizontalAlignment.Stretch` in its constructor, so it fills its container unless
you give it a `BarWidth` or a different alignment.

## Events

None. `ProgressBarControl` implements `INotifyPropertyChanged` via `BaseControl`, so its properties
can be data-bound — see [Data Binding](../binding.md).

## Keyboard and mouse support

None — this is a display-only control.

## Examples

### Determinate progress from a background task

```csharp
var progress = Controls.ProgressBar()
    .WithName("copyProgress")
    .WithHeader("Copying files")
    .WithMaxValue(totalFiles)
    .ShowPercentage()
    .Stretch()
    .Build();

window.AddControl(progress);

// From a worker — assigning Value only invalidates, so this is safe off the UI thread
for (int i = 0; i < totalFiles; i++)
{
    await CopyFileAsync(files[i], ct);
    progress.Value = i + 1;
}
```

> Adding or removing controls from a background thread is **not** safe — route that through
> `windowSystem.EnqueueOnUIThread(...)`. See [Threading & Async](../THREADING_AND_ASYNC.md).

### Indeterminate work

Use this when there is no meaningful percentage — a network handshake, a query with unknown length.

```csharp
var spinner = Controls.ProgressBar()
    .WithHeader("Connecting…")
    .Indeterminate()
    .WithPulseWidth(8)
    .WithAnimationInterval(80)
    .Build();

// Switch to determinate once the total is known
spinner.IsIndeterminate = false;
spinner.MaxValue = totalBytes;
spinner.Value = 0;
```

### Semantic coloring

```csharp
var bar = Controls.ProgressBar()
    .WithHeader("Disk usage")
    .WithPercentage(91)
    .WithColorRole(ColorRole.Danger)
    .ShowPercentage()
    .Build();
```

### Fixed width, no header

```csharp
var inline = Controls.ProgressBar()
    .WithBarWidth(24)
    .WithPercentage(60)
    .ShowHeader(false)
    .ShowPercentage()
    .WithFilledColor(Color.Cyan1)
    .WithUnfilledColor(Color.Grey23)
    .Build();
```

### Non-percentage scale

```csharp
var download = Controls.ProgressBar()
    .WithHeader("Download")
    .WithMaxValue(totalBytes)      // e.g. 5_242_880
    .WithValue(receivedBytes)
    .ShowPercentage()              // still displayed as a percentage of MaxValue
    .Build();
```

## Best Practices

1. **Use indeterminate mode when you genuinely don't know the total.** A determinate bar that jumps
   from 0 to 100 is worse than an honest pulse.
2. **Turn the animation off when the work ends.** Set `IsIndeterminate = false` — the pulse runs a
   timer, and leaving it on burns frames for no reason.
3. **Set `MaxValue` to the real total** (bytes, files, steps) instead of pre-computing a percentage;
   the readout is still a percentage, and the code stays clearer.
4. **Prefer `WithColorRole` to hardcoded colors** so the bar tracks the active theme.
5. **Don't use it for measured quantities.** A CPU meter is not progress —
   [BarGraphControl](BarGraphControl.md) is the right control there.
6. **Assign `Value` and stop.** The setter clamps and invalidates; calling `Invalidate` yourself is
   redundant.
7. **For a modal "please wait" flow**, `Dialogs.RunWithProgressAsync` already wraps a progress
   dialog — see [Dialogs](../DIALOGS.md).

## See Also

- [BarGraphControl](BarGraphControl.md) — a measured value against a maximum
- [SparklineControl](SparklineControl.md) — a value series over time
- [SliderControl](SliderControl.md) — an interactive value
- [SpinnerControl](SpinnerControl.md) — a compact activity indicator
- [Dialogs](../DIALOGS.md) — `RunWithProgressAsync` for modal progress
- [Themes](../THEMES.md) — color roles

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
