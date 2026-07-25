# BarGraphControl

Horizontal bar graph for showing a single value against a maximum — CPU load, memory use, disk
capacity, progress toward a target.

## Overview

`BarGraphControl` renders one labelled horizontal bar: an optional label, a filled/unfilled bar
track, and an optional formatted value. It is a **display control** — it takes no focus, handles no
input, and simply reflects whatever you assign to `Value`.

Colors can be static, driven by **thresholds** (green below 50, yellow below 80, red above), or
interpolated across a **smooth gradient**. Setting `Value` repaints automatically — the framework is
reactive at the property boundary, so you never call `Invalidate` yourself. Enable `AnimateValue` and
the bar eases to its new value instead of snapping, while the numeric readout still jumps to the
target immediately.

For a series of values over time use [SparklineControl](SparklineControl.md); for indeterminate or
step-based work use [ProgressBarControl](ProgressBarControl.md).

## Quick Start

```csharp
var bar = Controls.BarGraph()
    .WithLabel("CPU")
    .WithValue(42)
    .WithMaxValue(100)
    .WithStandardGradient()
    .Build();

window.AddControl(bar);

// Later — assigning Value repaints on its own
bar.Value = 78;
```

## Builder API

Create a builder with `Controls.BarGraph()` (or `new BarGraphBuilder()`).

### Value and scale

```csharp
.WithValue(double value)                    // Current value (default 0)
.WithMaxValue(double maxValue)              // Full-scale value (default 100)
.WithAnimatedValue(bool animate = true, TimeSpan? duration = null)
```

### Label and readout

```csharp
.WithLabel(string label)                    // Text shown before the bar
.WithLabelWidth(int width)                  // Fixed label column width (aligns several bars)
.WithLabelSeparator(string separator)       // Between label and bar (default ": ")
.ShowLabel(bool show = true)
.ShowValue(bool show = true)
.WithValueFormat(string format)             // Numeric format string (default "F1")
```

### Size and layout

```csharp
.WithBarWidth(int width)                    // Bar track width in cells (default 20)
.WithWidth(int width)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(Margin margin)
.Visible(bool visible)
.WithName(string name)
```

### Colors

```csharp
.WithFilledColor(Color color)               // Filled portion
.WithUnfilledColor(Color color)             // Track behind the bar (default Grey35)
.WithColors(Color filled, Color unfilled)
.WithBackgroundColor(Color color)
.WithForegroundColor(Color color)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
```

### Threshold and gradient coloring

```csharp
.WithGradient(params ColorThreshold[] thresholds)  // Explicit thresholds
.WithStandardGradient()                            // 0 green, 50 yellow, 80 red
.WithSmoothGradient(ColorGradient gradient)
.WithSmoothGradient(string gradientSpec)           // e.g. "cool", "blue→cyan→green"
.WithSmoothGradient(params Color[] colors)
```

`ColorThreshold` is a `record struct (double Threshold, Color Color)`. The bar takes the color of the
highest threshold its value has reached.

> **Precedence:** threshold gradients (`WithGradient` / `WithStandardGradient`) win over smooth
> gradients when both are set.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Value` | `double` | `0` | Current value. Assigning repaints the bar |
| `MaxValue` | `double` | `100.0` | Value corresponding to a completely filled bar |
| `Label` | `string` | `""` | Label text shown before the bar |
| `LabelWidth` | `int?` | `null` | Fixed label column width; `null` sizes to content |
| `LabelSeparator` | `string` | `": "` | Text between the label and the bar |
| `ShowLabel` | `bool` | `true` | Whether the label is rendered |
| `ShowValue` | `bool` | `true` | Whether the numeric value is rendered |
| `ValueFormat` | `string` | `"F1"` | .NET numeric format string for the value |
| `BarWidth` | `int` | `20` | Width of the bar track in cells |
| `FilledColor` | `Color` | theme | Color of the filled portion |
| `UnfilledColor` | `Color` | `Grey35` | Color of the empty track |
| `BackgroundColor` | `Color?` | `null` | Control background; inherits from container when null |
| `ForegroundColor` | `Color?` | `null` | Text color; inherits from container when null |
| `ColorRole` | `ColorRole` | `Default` | Semantic color role (see [Themes](../THEMES.md)) |
| `ColorRoleMode` | `ThemeMode?` | `null` | Optional theme mode override for the role |
| `Outline` | `bool` | `false` | Outline styling — role color on text and border |
| `ColorThresholds` | `IReadOnlyList<ColorThreshold>?` | `null` | Threshold-based colors |
| `SmoothGradient` | `ColorGradient?` | `null` | Gradient interpolated across the fill |
| `AnimateValue` | `bool` | `false` | Ease the bar to new values instead of snapping |
| `AnimationDuration` | `TimeSpan` | `250 ms` | Duration of a value transition when animating |

The bar is drawn with `█` for the filled portion and `░` for the remainder.

## Events

`BarGraphControl` raises no control-specific events. It implements `INotifyPropertyChanged` through
`BaseControl`, so its properties can be data-bound — see [Data Binding](../binding.md).

## Keyboard and mouse support

None. `BarGraphControl` is a display-only control: it is not focusable and does not process keyboard
or mouse input. To make a bar interactive, pair it with a focusable control such as
[SliderControl](SliderControl.md).

## Examples

### Several aligned bars

Give every bar the same `LabelWidth` so the tracks line up in a column.

```csharp
static BarGraphControl MakeBar(string name, string label, double value) =>
    Controls.BarGraph()
        .WithName(name)
        .WithLabel(label)
        .WithLabelWidth(8)
        .WithValue(value)
        .WithMaxValue(100)
        .WithValueFormat("F0")
        .WithStandardGradient()
        .WithAlignment(HorizontalAlignment.Stretch)
        .Build();

window.AddControl(MakeBar("cpuBar", "CPU", 25));
window.AddControl(MakeBar("memBar", "Memory", 45));
window.AddControl(MakeBar("diskBar", "Disk", 60));
```

### Live updates from a window thread

Look the control up by name and assign `Value`. The assignment invalidates the control for you.

```csharp
var window = new WindowBuilder(windowSystem)
    .WithTitle("System Monitor")
    .WithAsyncWindowThread(async (win, ct) =>
    {
        while (!ct.IsCancellationRequested)
        {
            var bar = win.FindControl<BarGraphControl>("cpuBar");
            if (bar != null)
                bar.Value = ReadCpuPercent();

            await Task.Delay(500, ct);
        }
    })
    .BuildAndShow();
```

> **Thread safety:** assigning `Value` from a background thread is safe because it only invalidates.
> Mutating *window* state (adding/removing controls, changing the title) from off the UI thread must
> go through `windowSystem.EnqueueOnUIThread(...)` — see
> [Threading & Async](../THREADING_AND_ASYNC.md).

### Custom thresholds

Bars that are "good when high" — like remaining battery — want the opposite of the standard ramp.

```csharp
var battery = Controls.BarGraph()
    .WithLabel("Battery")
    .WithValue(18)
    .WithGradient(
        new ColorThreshold(0, Color.Red),
        new ColorThreshold(20, Color.Yellow),
        new ColorThreshold(50, Color.Green))
    .Build();
```

### Smooth gradient

```csharp
// From a spec string — predefined names, or arrow notation
var load = Controls.BarGraph()
    .WithLabel("Load")
    .WithValue(72)
    .WithSmoothGradient("blue→cyan→green")
    .Build();

// Or from explicit colors
var thermal = Controls.BarGraph()
    .WithLabel("Temp")
    .WithValue(64)
    .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
    .Build();
```

### Animated transitions

```csharp
var bar = Controls.BarGraph()
    .WithLabel("Throughput")
    .WithMaxValue(1000)
    .WithAnimatedValue(true, TimeSpan.FromMilliseconds(400))
    .WithSmoothGradient("cool")
    .Build();

bar.Value = 750;   // the bar eases over 400 ms; the readout shows 750.0 at once
```

### Non-percentage scales

`MaxValue` and `ValueFormat` are independent, so any unit works.

```csharp
var disk = Controls.BarGraph()
    .WithLabel("Disk")
    .WithValue(412.5)
    .WithMaxValue(1024)
    .WithValueFormat("F1")
    .WithLabelSeparator(" ")
    .Build();
```

## Best Practices

1. **Give related bars the same `LabelWidth`** so their tracks align into a readable column.
2. **Set `MaxValue` to the real full-scale value** rather than pre-scaling inputs to 0–100 — the
   control does the arithmetic, and the raw value stays readable in the readout.
3. **Use `WithValueFormat("F0")` for percentages.** The default `"F1"` adds a decimal place that
   rarely helps for whole-percent readings.
4. **Pick thresholds or a smooth gradient, not both** — thresholds silently win. Thresholds suit
   alert-style bars with meaningful boundaries; smooth gradients suit continuous readings.
5. **Reach for `AnimateValue` on slow-polling bars.** When updates arrive every few seconds, easing
   reads as far less jumpy; leave it off for fast-refreshing bars where it just adds lag.
6. **Assign `Value` and stop there.** Calling `Container?.Invalidate()` yourself is redundant — the
   setter already invalidates.
7. **Don't reach for it as a progress indicator.** [ProgressBarControl](ProgressBarControl.md)
   handles indeterminate and step-based work; `BarGraphControl` is for measured quantities.

## See Also

- [SparklineControl](SparklineControl.md) — a value series over time
- [LineGraphControl](LineGraphControl.md) — multi-series line plots
- [ProgressBarControl](ProgressBarControl.md) — task progress, including indeterminate
- [SliderControl](SliderControl.md) — an interactive value the user can change
- [Themes](../THEMES.md) — color roles and theme-aware coloring
- [Gradients](../GRADIENTS.md) — gradient specification syntax

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
