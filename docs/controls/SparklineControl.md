# SparklineControl

Compact time-series graph — a scrolling history of values rendered as vertical bars or braille dots.

## Overview

`SparklineControl` keeps a rolling window of `double` values and draws them as a dense little chart.
Push a value with `AddDataPoint` and the oldest one falls off once the buffer is full, giving you a
live "last N samples" view in a handful of terminal rows.

Four render modes cover the common shapes: `Block` (the classic `▁▂▃▄▅▆▇█` ramp, 8 levels per cell),
`Braille` (denser and smoother), and the two **bidirectional** variants that draw a second series
downward from a centre line — the natural fit for upload/download or read/write pairs.

It is a **display control**: no focus, no keyboard or mouse handling. Colors can be flat, role-based,
or interpolated across a gradient, and an optional title, baseline, and X-axis can be layered on.

For a single value against a maximum use [BarGraphControl](BarGraphControl.md); for multi-series
plots with axes use [LineGraphControl](LineGraphControl.md).

## Quick Start

```csharp
var spark = Controls.Sparkline()
    .WithHeight(4)
    .WithAutoFitDataPoints()
    .WithBarColor(Color.Cyan1)
    .Build();

window.AddControl(spark);

// Push samples as they arrive — the control scrolls and repaints itself
spark.AddDataPoint(cpuPercent);
```

## Builder API

Create a builder with `Controls.Sparkline()` (or `new SparklineBuilder()`).

### Data

```csharp
.WithData(IEnumerable<double> dataPoints)          // Seed the primary series
.WithMaxDataPoints(int maxPoints)                  // Buffer size (default 50)
.WithAutoFitDataPoints(bool autoFit = true)        // Size the buffer to the control width
.WithMinValue(double minValue)                     // Fixed scale floor
.WithMaxValue(double maxValue)                     // Fixed scale ceiling
```

Leaving min/max unset auto-scales to the data currently in the buffer.

### Appearance

```csharp
.WithHeight(int height)                            // Graph height in rows (default 8)
.WithMode(SparklineMode mode)                      // Block, Braille, Bidirectional, BidirectionalBraille
.WithBarColor(Color color)
.WithBackgroundColor(Color color)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
.WithBorder(BorderStyle style)
.WithBorder(BorderStyle style, Color color)
.WithBorderColor(Color color)
```

### Gradients

```csharp
.WithGradient(ColorGradient gradient)
.WithGradient(string gradientSpec)                 // "cool", "warm", "blue→cyan→green"
.WithGradient(params Color[] colors)
```

### Title and baseline

```csharp
.WithTitle(string title)
.WithTitle(string title, Color color)
.WithTitleColor(Color color)
.WithTitlePosition(TitlePosition position)         // Top (default) or Bottom
.WithBaseline(bool show = true, char baselineChar = '┈',
              Color? color = null, TitlePosition position = TitlePosition.Bottom)
.WithInlineTitleBaseline(bool inline = true)       // Draw the title on the baseline row
```

### Secondary series (bidirectional modes)

```csharp
.WithSecondaryData(IEnumerable<double> dataPoints)
.WithSecondaryBarColor(Color color)
.WithSecondaryMaxValue(double maxValue)
.WithSecondaryGradient(ColorGradient gradient)
.WithSecondaryGradient(string gradientSpec)
.WithSecondaryGradient(params Color[] colors)
.WithBidirectionalData(IEnumerable<double> primaryData, IEnumerable<double> secondaryData)
```

### X-axis

```csharp
.WithXAxis(Func<SparklineAxisContext, IEnumerable<SparklineAxisTick>>? axisProvider,
           double unitsPerPoint = 1.0)
```

The control supplies the geometry and your provider returns the ticks:

- `SparklineAxisContext(int PointCount, int GraphWidth, double UnitsPerPoint)`
- `SparklineAxisTick(int PointIndex, string Label, Color? Color = null)`

Passing a `null` provider leaves the axis off, which is handy for enabling it conditionally.

### Layout

```csharp
.WithWidth(int width)
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(Margin margin)
.Visible(bool visible)
.WithName(string name)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DataPoints` | `IReadOnlyList<double>` | empty | The primary series |
| `MaxDataPoints` | `int` | `50` | Rolling buffer size |
| `AutoFitDataPoints` | `bool` | `false` | Size the buffer to the control's width |
| `MinValue` | `double?` | `null` | Scale floor; auto-scales when null |
| `MaxValue` | `double?` | `null` | Scale ceiling; auto-scales when null |
| `GraphHeight` | `int` | `8` | Graph height in rows |
| `Mode` | `SparklineMode` | `Block` | Render mode |
| `BarColor` | `Color` | theme | Primary series color |
| `Gradient` | `ColorGradient?` | `null` | Gradient applied across the primary series |
| `BackgroundColor` | `Color?` | `null` | Control background; inherits when null |
| `ForegroundColor` | `Color?` | `null` | Text color; inherits when null |
| `BorderStyle` | `BorderStyle` | `None` | Border around the graph |
| `BorderColor` | `Color?` | `null` | Border color |
| `ColorRole` | `ColorRole` | `Default` | Semantic color role |
| `ColorRoleMode` | `ThemeMode?` | `null` | Optional theme mode override |
| `Outline` | `bool` | `false` | Outline styling |
| `Title` | `string?` | `null` | Optional title |
| `TitleColor` | `Color?` | `null` | Title color |
| `TitlePosition` | `TitlePosition` | `Top` | Title above or below the graph |
| `ShowBaseline` | `bool` | `false` | Draw a baseline rule |
| `BaselineChar` | `char` | `'┈'` | Baseline character |
| `BaselineColor` | `Color` | `Grey50` | Baseline color |
| `BaselinePosition` | `TitlePosition` | `Bottom` | Baseline above or below |
| `InlineTitleWithBaseline` | `bool` | `false` | Render the title on the baseline row |
| `ShowXAxis` | `bool` | `false` | Whether the X-axis is drawn |
| `UnitsPerPoint` | `double` | `1.0` | Passed to the axis provider (e.g. seconds per sample) |
| `SecondaryDataPoints` | `IReadOnlyList<double>` | empty | Secondary series |
| `SecondaryBarColor` | `Color` | `Green` | Secondary series color |
| `SecondaryMaxValue` | `double?` | `null` | Independent scale for the secondary series |
| `SecondaryGradient` | `ColorGradient?` | `null` | Gradient for the secondary series |

### Methods

```csharp
void AddDataPoint(double value);                        // Append; drops the oldest when full
void SetDataPoints(IEnumerable<double> dataPoints);     // Replace the series
void ClearDataPoints();

void AddSecondaryDataPoint(double value);
void SetSecondaryDataPoints(IEnumerable<double> dataPoints);
void ClearSecondaryDataPoints();
void SetBidirectionalData(IEnumerable<double> primaryData, IEnumerable<double> secondaryData);
```

## Events

None. `SparklineControl` implements `INotifyPropertyChanged` via `BaseControl`, so its properties can
be data-bound — see [Data Binding](../binding.md).

## Keyboard and mouse support

None — this is a display-only control. It takes no focus and processes no input.

## Examples

### Live CPU history

`WithAutoFitDataPoints` sizes the buffer to the control's width, so the graph always fills the
available space no matter how the window is resized.

```csharp
var spark = Controls.Sparkline()
    .WithName("cpuSparkline")
    .WithHeight(4)
    .WithAutoFitDataPoints()
    .WithMode(SparklineMode.Block)
    .WithBarColor(Color.Cyan1)
    .WithGradient("cool")
    .WithBaseline(true, '─', Color.Grey35, TitlePosition.Bottom)
    .WithAlignment(HorizontalAlignment.Stretch)
    .Build();

// From a window thread
var spark = window.FindControl<SparklineControl>("cpuSparkline");
spark?.AddDataPoint(ReadCpuPercent());
```

### Braille mode for denser detail

```csharp
var mem = Controls.Sparkline()
    .WithHeight(4)
    .WithAutoFitDataPoints()
    .WithMode(SparklineMode.Braille)
    .WithBarColor(Color.Green)
    .WithGradient("warm")
    .Build();
```

### Bidirectional network graph

Upload rises from the centre line, download falls below it.

```csharp
var net = Controls.Sparkline()
    .WithHeight(6)
    .WithAutoFitDataPoints()
    .WithMode(SparklineMode.BidirectionalBraille)
    .WithBarColor(Color.Green)              // upload, drawn upward
    .WithSecondaryBarColor(Color.Red)       // download, drawn downward
    .Build();

net.AddDataPoint(uploadKbps);
net.AddSecondaryDataPoint(downloadKbps);
```

Give the two series **independent scales** with `WithSecondaryMaxValue` when their magnitudes differ
— otherwise a large download flattens the upload trace into invisibility.

### Fixed scale

Auto-scaling makes a flat series look dramatic, because the control stretches whatever range it has
to fill the height. Pin the scale when absolute level matters.

```csharp
var load = Controls.Sparkline()
    .WithHeight(5)
    .WithMinValue(0)
    .WithMaxValue(100)      // 0–100% always, however calm the data
    .WithTitle("Load", Color.Grey70)
    .Build();
```

### Time axis

```csharp
var spark = Controls.Sparkline()
    .WithHeight(6)
    .WithAutoFitDataPoints()
    .WithXAxis(ctx =>
    {
        // One tick per 30 samples, labelled as seconds in the past
        var ticks = new List<SparklineAxisTick>();
        for (int i = 0; i < ctx.PointCount; i += 30)
        {
            double secondsAgo = (ctx.PointCount - 1 - i) * ctx.UnitsPerPoint;
            ticks.Add(new SparklineAxisTick(i, $"-{secondsAgo:F0}s", Color.Grey50));
        }
        return ticks;
    }, unitsPerPoint: 0.5)   // a sample every 500 ms
    .Build();
```

### Seeding from existing history

```csharp
var spark = Controls.Sparkline()
    .WithData(recentSamples)     // e.g. values loaded from a log
    .WithMaxDataPoints(120)
    .Build();

// Or replace the whole series later
spark.SetDataPoints(refreshedSamples);
```

## Best Practices

1. **Prefer `WithAutoFitDataPoints()` for live graphs.** It keeps one sample per column, so the graph
   fills the width and stays honest when the window resizes.
2. **Pin `MinValue`/`MaxValue` when the absolute level matters.** Auto-scaling exaggerates a flat
   series — steady 2 % CPU renders as a dramatic mountain range.
3. **Match the mode to the height.** `Braille` packs more detail into few rows; `Block` reads more
   clearly when you have height to spare.
4. **Give bidirectional series independent scales** via `WithSecondaryMaxValue` unless the two
   genuinely share a range.
5. **Push with `AddDataPoint`, don't rebuild the list.** It maintains the rolling window and
   invalidates for you; `SetDataPoints` is for wholesale replacement.
6. **Never call `Invalidate` after adding a point.** The control already does it — see
   [State Services](../STATE-SERVICES.md) and the reactive property contract.
7. **Keep `MaxDataPoints` sane.** Buffering thousands of points that render into 40 columns costs
   memory and buys nothing visible.

## See Also

- [BarGraphControl](BarGraphControl.md) — a single value against a maximum
- [LineGraphControl](LineGraphControl.md) — multi-series plots with axes
- [ProgressBarControl](ProgressBarControl.md) — task progress
- [Gradients](../GRADIENTS.md) — gradient specification syntax
- [Themes](../THEMES.md) — color roles

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
