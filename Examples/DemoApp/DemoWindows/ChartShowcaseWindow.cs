using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace DemoApp.DemoWindows;

/// <summary>
/// Everything <see cref="ChartControl"/> can do, a page at a time.
/// </summary>
/// <remarks>
/// Written entirely through <see cref="ChartBuilder"/>: a showcase that had to
/// reach past the fluent API would be reporting a gap in it. Pages are static
/// so the feature on show holds still long enough to read; the last one streams.
/// </remarks>
internal static class ChartShowcaseWindow
{
	private const int WindowWidth = 116;
	private const int WindowHeight = 40;
	private const int NavWidth = 26;
	private const int PlotHeight = 12;
	private const int LiveIntervalMs = 400;
	private const int LiveWindowSize = 160;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var live = new LiveFeed();

		var nav = Controls.NavigationView()
			.WithNavWidth(NavWidth)
			.WithPaneHeader("[bold]  ChartControl[/]")
			.WithContentBorder(BorderStyle.Rounded)
			.AddHeader("Plots", Color.Cyan1, h => h
				.AddItem("Line & Area", subtitle: "The two curve types", content: LinePage())
				.AddItem("Bar & Scatter", subtitle: "Grouped bars, unconnected marks", content: BarPage())
				.AddItem("Gauges", subtitle: "Inline readouts, one row each", content: GaugePage())
				.AddItem("Combo", subtitle: "Per-series plot types on one frame", content: ComboPage()))
			.AddHeader("Rendering", Color.Magenta1, h => h
				.AddItem("Render Modes", subtitle: "Pixel, braille and ascii", content: RenderModesPage())
				.AddItem("Gradients", subtitle: "By position, by value, blended", content: GradientPage())
				.AddItem("Thresholds", subtitle: "Colour that changes at a limit", content: ThresholdPage()))
			.AddHeader("Axes", Color.Yellow, h => h
				.AddItem("Ticks & Labels", subtitle: "Nice numbers, time, categories", content: AxisPage())
				.AddItem("Custom Ticks", subtitle: "A caller-supplied tick provider", content: TickProviderPage())
				.AddItem("Two Scales", subtitle: "A second axis on the right", content: SecondaryAxisPage()))
			.AddHeader("Composition", Color.Green, h => h
				.AddItem("Stacked", subtitle: "Parts of a total", content: StackedPage())
				.AddItem("Split View", subtitle: "Two opposed directions", content: SplitPage())
				.AddItem("Annotations", subtitle: "Limits, markers, peaks", content: AnnotationPage())
				.AddItem("Chrome", subtitle: "Titles, borders, empty state", content: ChromePage()))
			.AddHeader("Behaviour", Color.Orange1, h => h
				.AddItem("Interaction", subtitle: "Hover, select, zoom", content: InteractionPage())
				.AddItem("Live Data", subtitle: "Streaming with a rolling window", content: live.Page()))
			.WithSelectedIndex(0)
			.Fill()
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Chart Showcase")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(nav)
			.BuildAndShow();

		live.Start(ws, window);
		window.OnClosed += (_, _) => live.Stop();
		return window;
	}

	// ---- plots -------------------------------------------------------------

	private static IWindowControl LinePage() => Page(
		"A line joins its points; an area fills to the baseline underneath.",
		Chart("Line", ChartPlotType.Line, Wave(28, 40, 35)),
		Chart("Area", ChartPlotType.Area, Wave(28, 40, 35)));

	private static IWindowControl BarPage() => Page(
		"Bars anchor to zero, so their length is the value. Scatter leaves the "
			+ "space between points empty - that gap is the information.",
		Controls.Chart()
			.WithTitle("Grouped bars")
			.WithPlotType(ChartPlotType.Bar)
			.WithPlotHeight(PlotHeight)
			.WithYRange(0, 100)
			.WithCategories("cpu", "gpu", "ram", "disk", "net")
			.WithLegend()
			.AddSeries("Q1", Color.Green, new[] { 45d, 70, 30, 55, 40 })
			.AddSeries("Q2", Color.Cyan1, new[] { 60d, 50, 45, 35, 65 })
			.Build(),
		Controls.Chart()
			.WithTitle("Scatter")
			.WithPlotType(ChartPlotType.Scatter)
			.WithPlotHeight(PlotHeight)
			.WithYRange(0, 100)
			.AddSeries("samples", Color.Yellow, Scatter(26))
			.Build());

	private static IWindowControl GaugePage() => Page(
		"A gauge has no second dimension, so length carries the reading and the "
			+ "gradient says where it sits on the scale.",
		Controls.Chart()
			.WithTitle("Capacity")
			.AsGauge()
			.WithYRange(0, 100)
			.ConfigureYAxis(a => a.LabelFormatter = v => $"{v:0}%")
			.AddSeries("cpu", Color.Orange1, new[] { 72d }, ColorGradient.Predefined["warm"])
			.AddSeries("memory", Color.Orange1, new[] { 45d }, ColorGradient.Predefined["warm"])
			.AddSeries("disk", Color.Orange1, new[] { 88d }, ColorGradient.Predefined["warm"])
			.AddSeries("network", Color.Orange1, new[] { 23d }, ColorGradient.Predefined["warm"])
			.Build());

	private static IWindowControl ComboPage() => Page(
		"Plot type is per series, so bars with a line over them is a configuration "
			+ "rather than a special case.",
		Controls.Chart()
			.WithTitle("Volume and trend")
			.WithPlotType(ChartPlotType.Bar)
			.WithPlotHeight(PlotHeight + 2)
			.WithYRange(0, 100)
			.WithLegend()
			.WithGridlines()
			.AddSeries("volume", Color.Blue, new[] { 30d, 45, 38, 62, 50, 71, 48, 66 })
			.AddSeries("trend", Color.Yellow, new[] { 35d, 42, 46, 54, 56, 63, 60, 64 },
				plotType: ChartPlotType.Line)
			.Build());

	// ---- rendering ---------------------------------------------------------

	private static IWindowControl RenderModesPage() => Page(
		"One cell carries one foreground colour. Braille packs 2x4 dots into it, "
			+ "so it draws the finest line - but two series crossing in one cell "
			+ "lose a colour. Half blocks give two independently coloured pixels: "
			+ "coarser, and honest about overlap. Ascii is for terminals with neither.",
		ModeChart("Pixel - two colours per cell", ChartRenderMode.Pixel),
		ModeChart("Braille - 2x4 dots, one colour", ChartRenderMode.Braille),
		ModeChart("Ascii - one glyph per cell", ChartRenderMode.Ascii));

	private static ChartControl ModeChart(string title, ChartRenderMode mode) =>
		Controls.Chart()
			.WithTitle(title)
			.WithRenderMode(mode)
			.WithPlotType(ChartPlotType.Line)
			.WithPlotHeight(8)
			.WithYRange(0, 100)
			// Two series tracking each other closely: where they share a cell is
			// exactly where the modes differ.
			.AddSeries("a", Color.Red, Track(30, 0))
			.AddSeries("b", Color.Blue, Track(30, 6))
			.Build();

	private static IWindowControl GradientPage() => Page(
		"A gradient runs along the plot, by value, or a blend of the two. By value "
			+ "is normalised against the AXIS, so a colour means the same thing on "
			+ "every chart that shares the scale.",
		GradientChart("By position (X)", ChartGradientAxis.X),
		GradientChart("By value (Y)", ChartGradientAxis.Y),
		GradientChart("Blended (Both)", ChartGradientAxis.Both));

	private static ChartControl GradientChart(string title, ChartGradientAxis axis) =>
		Controls.Chart()
			.WithTitle(title)
			.WithPlotType(ChartPlotType.Area)
			.WithPlotHeight(7)
			.WithYRange(0, 100)
			.AddSeries("v", Color.White, Wave(34, 45, 40),
				ColorGradient.Predefined["spectrum"], axis)
			.Build();

	private static IWindowControl ThresholdPage() => Page(
		"A threshold recolours the series at the crossing, so the data carries the "
			+ "warning instead of the reader checking it against a line.",
		Controls.Chart()
			.WithTitle("CPU")
			.WithPlotHeight(PlotHeight)
			.WithYRange(0, 100)
			.WithGridlines()
			.WithReferenceLine(90, Color.Red, "critical")
			.WithReferenceLine(70, Color.Yellow, "warn")
			.AddSeries("cpu", Color.Green,
				new[] { 30d, 55, 72, 94, 85, 60, 40, 78, 95, 66, 35, 50 },
				thresholds: new[]
				{
					new ChartThreshold(70, Color.Yellow),
					new ChartThreshold(90, Color.Red),
				})
			.Build());

	// ---- axes --------------------------------------------------------------

	private static IWindowControl AxisPage() => Page(
		"Ticks land on 1, 2 or 5 times a power of ten, because those are the "
			+ "numbers a reader can divide at a glance. The axis kind decides how "
			+ "they are labelled. A plot too short for even one interior tick "
			+ "labels its two ENDS instead, because the range is what an axis is "
			+ "for - see the Time chart below.",
		Controls.Chart()
			.WithTitle("Numeric")
			.WithPlotHeight(14)
			.WithYRange(0, 137)
			.WithAxisTitles("samples", "value")
			.WithGridlines()
			.AddSeries("v", Color.Cyan1, Wave(30, 60, 55))
			.Build(),
		Controls.Chart()
			.WithTitle("Time")
			.WithPlotHeight(8)
			.ConfigureXAxis(a => { a.Kind = ChartAxisKind.Time; a.LabelFormat = "HH:mm"; })
			.AddSeries("v", Color.Green, Array.Empty<double>())
			.Build()
			.FedWithTimeSeries());

	private static IWindowControl TickProviderPage() => Page(
		"Some ticks follow something other than the value scale. A monitor labels "
			+ "its history by the sample clock, so the caller says where the ticks "
			+ "go and the nice-number engine stands aside.",
		Controls.Chart()
			.WithTitle("Rolling history")
			.WithPlotType(ChartPlotType.Area)
			.WithPlotHeight(PlotHeight)
			.WithYRange(0, 100)
			.ConfigureYAxis(a => a.ShowLabels = false)
			.ConfigureXAxis(a => a.TickProvider = ctx =>
			{
				var ticks = new List<ChartAxisTick>();
				double span = ctx.Max - ctx.Min;
				foreach (var (fraction, label) in new[]
				{
					(0.0, "-2m"), (0.25, "-90s"), (0.5, "-1m"), (0.75, "-30s"), (1.0, "now"),
				})
				{
					ticks.Add(new ChartAxisTick(ctx.Min + fraction * span, label, Color.Grey62));
				}
				return ticks;
			})
			.AddSeries("load", Color.Cyan1, Wave(40, 50, 40),
				ColorGradient.FromColors(new Color(13, 148, 136), new Color(78, 205, 196)),
				ChartGradientAxis.Y)
			.Build());

	private static IWindowControl SecondaryAxisPage() => Page(
		"Two series in different units flatten each other on one scale. A second "
			+ "axis, labelled on the right, gives one its own - and is only drawn "
			+ "when something uses it.",
		Controls.Chart()
			.WithTitle("Rate against latency")
			.WithPlotHeight(PlotHeight)
			.WithLegend()
			.WithYRange(0, 1000)
			.WithAxisTitles(null, "req/s")
			.ConfigureSecondaryYAxis(a => { a.Min = 0; a.Max = 400; })
			.AddSeries("requests", Color.Green, new[] { 200d, 450, 380, 820, 600, 900, 450, 700 })
			.AddSeries("latency ms", Color.Yellow, new[] { 40d, 55, 48, 180, 95, 320, 70, 140 },
				useSecondaryAxis: true)
			.Build());

	// ---- composition -------------------------------------------------------

	private static IWindowControl StackedPage() => Page(
		"Each series sits on the sum of those before it - how a total divides "
			+ "between its parts. The range follows the stacked total, and hiding "
			+ "one restacks the rest.",
		Controls.Chart()
			.WithTitle("Service load")
			.WithSubtitle("stacked by tier")
			.WithPlotType(ChartPlotType.Area)
			.WithPlotHeight(PlotHeight)
			.Stacked()
			.WithLegend()
			.WithGridlines()
			.WithYRange(0, 200)
			.WithReferenceLine(150, Color.Red, "capacity")
			.AddSeries("web", Color.Green, new[] { 30d, 45, 38, 52, 40, 60, 35, 48 })
			.AddSeries("api", Color.Cyan1, new[] { 25d, 30, 42, 28, 50, 33, 45, 38 })
			.AddSeries("cache", Color.Magenta1, new[] { 15d, 20, 18, 25, 22, 28, 20, 24 })
			.Build());

	private static IWindowControl SplitPage() => Page(
		"Two opposed quantities about a shared centre. Both halves measure the "
			+ "same positive range growing away from it - five megabits down is "
			+ "not minus five - so the axis reads its maximum at both ends and "
			+ "zero in the middle.",
		Controls.Chart()
			.WithTitle("Network throughput")
			.WithPlotType(ChartPlotType.Area)
			.WithPlotHeight(PlotHeight + 2)
			.WithSplitView()
			.WithCentreAxis(" B/s ", showValue: true)
			.WithLegend()
			.WithYRange(0, 100)
			.ConfigureLowerAxis(a => { a.Min = 0; a.Max = 60; })
			.AddSeries("down", Color.Green, Wave(30, 55, 40),
				ColorGradient.FromColors(Color.Green, Color.Yellow), ChartGradientAxis.Y)
			.AddSeries("up", Color.Cyan1, Wave(30, 30, 22, phase: 2.1),
				ColorGradient.FromColors(Color.Cyan1, Color.Magenta1), ChartGradientAxis.Y)
			.Build());

	private static IWindowControl AnnotationPage() => Page(
		"Reference lines go under the data, because a limit is context rather "
			+ "than content. Markers and high/low labels go over it, because they "
			+ "name specific points.",
		Controls.Chart()
			.WithTitle("Annotated")
			.WithPlotHeight(PlotHeight)
			.WithYRange(0, 100)
			.WithGridlines()
			.WithHighLowLabels()
			.WithReferenceLine(75, Color.Red, "SLA")
			.AddSeries("latency", Color.Cyan1, new[] { 30d, 95, 40, 12, 60, 48, 82, 35 })
			.Build());

	private static IWindowControl ChromePage() => Page(
		"Title, subtitle, border and axis titles. A chart with nothing to plot "
			+ "says so - a blank frame is indistinguishable from a broken one.",
		Controls.Chart()
			.WithTitle("Fully dressed")
			.WithSubtitle("title, subtitle, border, axis titles")
			.WithTitleAlignment(ChartTitleAlignment.Center)
			.WithBorder()
			.WithPlotHeight(9)
			.WithAxisTitles("minutes", "req/s")
			.WithGridlines()
			.WithAxisLines()
			.WithYRange(0, 100)
			.AddSeries("v", Color.Green, Wave(26, 50, 40))
			.Build(),
		Controls.Chart()
			.WithTitle("Awaiting data")
			.WithBorder()
			.WithPlotHeight(6)
			.WithEmptyText("no samples received yet")
			.Build());

	// ---- behaviour ---------------------------------------------------------

	private static IWindowControl InteractionPage() => Page(
		"Every interaction is opt-in, so a chart used as decoration takes neither "
			+ "the mouse nor the keyboard. Hover a point for a crosshair and a "
			+ "readout, click to select, arrow keys to walk, drag or wheel to zoom, "
			+ "Escape to reset.",
		Controls.Chart()
			.WithTitle("Try it")
			.WithPlotHeight(PlotHeight + 2)
			.WithYRange(0, 100)
			.WithGridlines()
			.WithLegend()
			.Interactive()
			.AddSeries("requests", Color.Green, Wave(34, 55, 40))
			.AddSeries("errors", Color.Red, Wave(34, 18, 14, phase: 1.3))
			.Build());

	// ---- helpers -----------------------------------------------------------

	private static ChartControl Chart(string title, ChartPlotType type, double[] data) =>
		Controls.Chart()
			.WithTitle(title)
			.WithPlotType(type)
			.WithPlotHeight(PlotHeight - 2)
			.WithYRange(0, 100)
			.AddSeries("v", Color.Cyan1, data)
			.Build();

	/// <summary>A caption above one or more charts, in a scrolling page.</summary>
	private static IWindowControl Page(string caption, params IWindowControl[] charts)
	{
		// A directly-hosted page bypasses the nav's own content panel, and with it
		// that panel's padding - so the page carries its own, or the rightmost
		// axis label is stretched past the visible edge and clipped.
		var panel = Controls.ScrollablePanel()
			.WithAutoScroll(false)
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithPadding(1, 0, 2, 0)
			.Build();

		panel.AddControl(Controls.Markup($"[grey70]{caption}[/]")
			.WithMargin(0, 0, 0, 1)
			.Build());

		foreach (var chart in charts)
		{
			panel.AddControl(chart);
			panel.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());
		}

		return panel;
	}

	private static double[] Wave(int n, double centre, double amplitude, double phase = 0) =>
		Enumerable.Range(0, n)
			.Select(i => Math.Clamp(centre + amplitude * Math.Sin(i / 3.3 + phase)
				+ amplitude * 0.25 * Math.Sin(i / 1.1 + phase), 0, 100))
			.ToArray();

	private static double[] Track(int n, double offset) =>
		Enumerable.Range(0, n)
			.Select(i => Math.Clamp(50 + 30 * Math.Sin(i / 4.0) + offset, 0, 100))
			.ToArray();

	private static double[] Scatter(int n)
	{
		var rng = new Random(11);
		return Enumerable.Range(0, n).Select(_ => 20 + rng.NextDouble() * 65).ToArray();
	}

	/// <summary>Streams samples into a fixed-step chart with a rolling window.</summary>
	private sealed class LiveFeed
	{
		private readonly Random _rng = new(7);
		private ChartControl? _chart;
		private System.Threading.Timer? _timer;
		private double _level = 45;

		public IWindowControl Page()
		{
			_chart = Controls.Chart()
				.WithTitle("Streaming")
				.WithSubtitle("a fixed step per sample, so the trace marches rightward")
				.WithPlotType(ChartPlotType.Area)
				.WithPlotHeight(PlotHeight + 2)
				.WithFixedStep(2)
				.WithYRange(0, 100)
				.WithGridlines()
				.ConfigureXAxis(a => a.ShowLabels = false)
				.AddSeries("samples", Color.Cyan1, Array.Empty<double>(),
					ColorGradient.Predefined["spectrum"], ChartGradientAxis.Y)
				.Build();

			return ChartShowcaseWindow.Page(
				"A live feed wants a fixed step: a chart that rescales on every "
					+ "sample shifts the whole trace under the reader, where a fixed "
					+ "step lets it march rightward with earlier samples staying put. "
					+ "TrimTo caps the history so it cannot grow without bound.",
				_chart);
		}

		public void Start(ConsoleWindowSystem ws, Window window)
		{
			_timer = new System.Threading.Timer(_ =>
			{
				// The sample is computed here; the control is touched on the UI
				// thread, because the render loop reads it concurrently.
				_level = Math.Clamp(_level + (_rng.NextDouble() - 0.48) * 18, 2, 98);
				double value = _level;

				ws.EnqueueOnUIThread(() =>
				{
					if (_chart == null || _chart.Series.Count == 0) return;

					var series = _chart.Series[0];
					series.AddPoint(new ChartPoint(series.Points.Count, value));
					series.TrimTo(LiveWindowSize);
					_chart.Invalidate(Invalidation.Repaint);
				});
			}, null, LiveIntervalMs, LiveIntervalMs);
		}

		public void Stop()
		{
			_timer?.Dispose();
			_timer = null;
		}
	}

	/// <summary>Fills a time-axis chart with timestamped samples.</summary>
	private static ChartControl FedWithTimeSeries(this ChartControl chart)
	{
		var start = DateTime.Now.AddHours(-6);
		var rng = new Random(23);
		double level = 50;

		chart.Series[0].SetTimeSeries(Enumerable.Range(0, 30).Select(i =>
		{
			level = Math.Clamp(level + (rng.NextDouble() - 0.5) * 22, 5, 95);
			return (start.AddMinutes(i * 12), level);
		}));

		return chart;
	}
}
