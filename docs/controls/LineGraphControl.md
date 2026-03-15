# LineGraphControl

A line graph control for visualizing time-series data using connected lines rendered with braille patterns or ASCII box-drawing characters. Supports multiple named data series.

## Overview

`LineGraphControl` renders data as connected line segments across a graph area. It offers two rendering modes: **Braille** mode uses Unicode braille characters (U+2800 block) with a 2x4 pixel-per-cell grid for smooth, high-resolution lines. **ASCII** mode uses box-drawing characters (`─`, `│`, `╱`, `╲`) for simpler display. Both modes support multiple named series, color gradients, Y-axis labels, title, baseline, and borders.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Mode` | `LineGraphMode` | `Braille` | Rendering mode: `Braille` (high-res) or `Ascii` (box-drawing). |
| `GraphHeight` | `int` | `10` | Height of the graph area in terminal lines. Minimum: 3. |
| `MaxDataPoints` | `int` | `100` | Maximum data points per series before oldest are trimmed. |
| `AutoFitDataPoints` | `bool` | `false` | When true, automatically adjusts MaxDataPoints to match rendered width. |
| `MinValue` | `double?` | `null` | Fixed minimum Y-axis value. Null = auto-scale from data. |
| `MaxValue` | `double?` | `null` | Fixed maximum Y-axis value. Null = auto-scale from data. |
| `Title` | `string?` | `null` | Optional title displayed above or below the graph. |
| `TitleColor` | `Color?` | `null` | Title text color. When null, uses foreground color. |
| `TitlePosition` | `TitlePosition` | `Top` | Title position: `Top` or `Bottom`. |
| `ShowYAxisLabels` | `bool` | `false` | Whether to display min/max values on the Y axis. |
| `AxisLabelFormat` | `string` | `"F1"` | Format string for Y-axis labels (e.g., `"F0"`, `"F2"`). |
| `AxisLabelColor` | `Color` | `Grey70` | Color of Y-axis labels. |
| `ShowBaseline` | `bool` | `false` | Whether to show a dotted baseline. |
| `BaselineChar` | `char` | `┈` | Character used for the baseline. |
| `BaselineColor` | `Color` | `Grey50` | Color of the baseline. |
| `BaselinePosition` | `TitlePosition` | `Bottom` | Baseline position: `Top` or `Bottom`. |
| `InlineTitleWithBaseline` | `bool` | `false` | Show title inline with baseline (when both have same position). |
| `BorderStyle` | `BorderStyle` | `None` | Border style around the graph. |
| `BorderColor` | `Color?` | `null` | Border color. When null, uses foreground color. |
| `BackgroundColor` | `Color?` | `null` | Background color. When null, inherits from container. |
| `ForegroundColor` | `Color?` | `null` | Foreground color. When null, inherits from container. |
| `Series` | `IReadOnlyList<LineGraphSeries>` | Empty | Read-only snapshot of current series. |

## Series Model

Each series has:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Unique identifier for the series. |
| `LineColor` | `Color` | Solid line color. |
| `Gradient` | `ColorGradient?` | Optional horizontal color gradient. |

## Methods

### Series Management

| Method | Description |
|--------|-------------|
| `AddSeries(name, color, gradient?)` | Creates a named series. Returns existing if name matches. |
| `RemoveSeries(name)` | Removes a series by name. Returns true if found. |

### Data Management

| Method | Description |
|--------|-------------|
| `AddDataPoint(value)` | Adds to default series (auto-created if none exist). |
| `AddDataPoint(seriesName, value)` | Adds to a named series. |
| `SetDataPoints(data)` | Replaces default series data. |
| `SetDataPoints(seriesName, data)` | Replaces named series data. |
| `ClearAllData()` | Clears data from all series (series remain). |

## Creating LineGraphControl

### Using Builder (Recommended)

```csharp
// Simple single-series graph
var graph = Controls.LineGraph()
    .WithTitle("CPU Load")
    .WithHeight(8)
    .WithData(new double[] { 10, 45, 28, 67, 34, 89, 56, 23, 78, 45 })
    .WithYAxisLabels()
    .WithBaseline()
    .Stretch()
    .Build();
```

### Multi-Series Graph

```csharp
var graph = Controls.LineGraph()
    .WithTitle("System Metrics", Color.Yellow)
    .WithMode(LineGraphMode.Braille)
    .WithHeight(10)
    .WithMaxValue(100)
    .AddSeries("cpu", Color.Cyan1, "cool")
    .AddSeries("memory", Color.Green)
    .WithYAxisLabels(true, "F0")
    .WithBorder(BorderStyle.Rounded)
    .Stretch()
    .Build();
```

### ASCII Mode

```csharp
var graph = Controls.LineGraph()
    .WithMode(LineGraphMode.Ascii)
    .WithHeight(6)
    .WithData(new double[] { 5, 20, 15, 35, 25, 40, 30 })
    .Build();
```

### Live Updates

```csharp
// In an async window thread:
var graph = window.FindControl<LineGraphControl>("myGraph");
graph?.AddDataPoint("cpu", newValue);
graph?.AddDataPoint("memory", memValue);
```

### Using Constructor

```csharp
var graph = new LineGraphControl
{
    GraphHeight = 8,
    Mode = LineGraphMode.Braille,
    Title = "Latency",
    ShowYAxisLabels = true,
    ShowBaseline = true
};
graph.AddSeries("p50", Color.Green);
graph.AddSeries("p99", Color.Red);
```

## Rendering Modes

### Braille Mode

Uses Unicode braille characters with a 2x4 pixel grid per terminal cell. Each cell can represent 8 independent dots, resulting in smooth, high-resolution lines. All series are rendered into the same pixel grid (lines overlap via OR). The last series rendered determines the color for each cell column.

### ASCII Mode

Uses box-drawing characters:
- `─` horizontal segments
- `│` vertical segments
- `╱` rising diagonals
- `╲` falling diagonals

Each series is rendered sequentially. Later series overwrite earlier ones at conflict cells.

## Thread Safety

All data operations (`AddDataPoint`, `SetDataPoints`, `ClearAllData`, `AddSeries`, `RemoveSeries`) are thread-safe and can be called from any thread, including async window threads.

## Related

- [SparklineControl](../CONTROLS.md) - Vertical bar chart for time-series data
- [BarGraphControl](../CONTROLS.md) - Horizontal bar graphs
- [CanvasControl](CanvasControl.md) - Free-form drawing for custom visualizations
