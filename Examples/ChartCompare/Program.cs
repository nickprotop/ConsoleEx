// -----------------------------------------------------------------------
// ChartCompare - the old graph controls and the new ChartControl, side by
// side on the same data, so the differences are visible rather than argued.
//
//   dotnet run --project Examples/ChartCompare -- --headless --socket /tmp/cmp.sock --page 1
//
// --page picks which pair is shown:
//   1  LineGraphControl   vs  ChartControl (line)
//   2  SparklineControl   vs  ChartControl (bar)
//   3  BarGraphControl    vs  ChartControl (single bar)
//   4  Sparkline split    vs  ChartControl (split view)
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using System.Linq;
using SharpConsoleUI.Layout;

namespace ChartCompare;

internal static class Program
{
	/// <summary>One dataset, so any difference is the control and not the data.</summary>
	private static readonly double[] Samples =
	{
		20, 55, 40, 85, 60, 90, 45, 70, 35, 65, 80, 50, 75, 42, 88, 58,
	};

	private static readonly double[] Secondary =
	{
		70, 30, 60, 15, 45, 10, 65, 25, 75, 30, 15, 55, 20, 68, 12, 48,
	};

	private static int Main(string[] args)
	{
		string? socket = Flag(args, "--socket") is { } path
			? (path.Length > 0 ? path : Path.Combine(Path.GetTempPath(), "cmp.sock"))
			: null;
		bool headless = args.Contains("--headless");
		int page = Size(args, "--page", 1);

		try
		{
			IConsoleDriver driver = headless
				? new HeadlessConsoleDriver(Size(args, "--width", 120), Size(args, "--height", 40))
				: new NetConsoleDriver(RenderMode.Buffer);

			var ws = new ConsoleWindowSystem(driver, options: new ConsoleWindowSystemOptions(
				ShowTopPanel: false, ShowBottomPanel: false, DiagnosticsSocket: socket));

			Console.CancelKeyPress += (_, e) => { e.Cancel = true; ws.Shutdown(0); };

			BuildPage(ws, page);
			return ws.Run();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex);
			return 1;
		}
	}

	private static void BuildPage(ConsoleWindowSystem ws, int page)
	{
		var (oldTitle, oldControl, newTitle, newControl) = page switch
		{
			2 => ("SparklineControl", Sparkline(), "ChartControl (bar)", ChartBars()),
			3 => ("BarGraphControl x3", SingleBar(), "ChartControl (Gauge)", ChartSingleBar()),
			4 => ("Sparkline Bidirectional", SparklineSplit(), "ChartControl (split)", ChartSplit()),
			5 => ("ChartControl Stretch", ChartStretch(), "ChartControl FixedStep (grows)", ChartFixedStep()),
			6 => ("Stacked + annotations", ChartEverything(), "Empty state", ChartEmpty()),
			7 => ("Thresholds + high/low", ChartThresholds(), "Two scales", ChartTwoScales()),
			8 => ("cxnet today (Sparkline)", CxnetOld(), "cxnet on ChartControl", CxnetNew()),
			_ => ("LineGraphControl", LineGraph(), "ChartControl (line)", ChartLine()),
		};

		var top = new Window(ws)
		{
			Title = $"OLD: {oldTitle}",
			Left = 1, Top = 0, Width = 116, Height = 19,
		};
		top.AddControl(oldControl);
		ws.AddWindow(top);

		var bottom = new Window(ws)
		{
			Title = $"NEW: {newTitle}",
			Left = 1, Top = 19, Width = 116, Height = 19,
		};
		bottom.AddControl(newControl);
		ws.AddWindow(bottom);
	}

	// --- page 1: line ---

	private static IWindowControl LineGraph()
	{
		var g = new LineGraphControl
		{
			Mode = LineGraphMode.Braille,
			GraphHeight = 14,
			ShowYAxisLabels = true,
			MinValue = 0,
			MaxValue = 100,
			ShowLegend = true,
			// The old control does not stretch on its own: without this it draws at
			// its natural width and the comparison would be of harnesses, not controls.
			AutoFitDataPoints = true,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		g.AddSeries("samples", Color.Green, ColorGradient.Predefined["spectrum"]);
		g.SetDataPoints("samples", Samples);
		return g;
	}

	private static IWindowControl ChartLine()
	{
		var c = new ChartControl
		{
			Name = "chart",
			PlotType = ChartPlotType.Line,
			// Braille, to match the old control mode for mode: page 1 is about
			// whether the new control finishes as well, not about the trade.
			RenderMode = ChartRenderMode.Braille,
			ShowLegend = true,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 100;
		var s = c.AddSeries("samples", Color.Green);
		s.Gradient = ColorGradient.Predefined["spectrum"];
		s.SetValues(Samples);
		return c;
	}

	// --- page 2: columns ---

	private static IWindowControl Sparkline()
	{
		var s = new SparklineControl
		{
			Mode = SparklineMode.Block,
			GraphHeight = 14,
			MinValue = 0,
			MaxValue = 100,
			Gradient = ColorGradient.Predefined["spectrum"],
			AutoFitDataPoints = true,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		s.SetDataPoints(Samples);
		return s;
	}

	private static IWindowControl ChartBars()
	{
		var c = new ChartControl
		{
			Name = "chart",
			PlotType = ChartPlotType.Bar,
			RenderMode = ChartRenderMode.Pixel,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 100;
		var s = c.AddSeries("samples", Color.Green);
		s.Gradient = ColorGradient.Predefined["spectrum"];
		s.GradientAxis = ChartGradientAxis.Y;
		s.SetValues(Samples);
		return c;
	}

	// --- page 3: a single bar ---

	private static IWindowControl SingleBar()
	{
		var stack = new FlowControl();
		foreach (var (name, value) in new[] { ("cpu", 72d), ("ram", 45d), ("disk", 88d) })
		{
			stack.AddControl(new BarGraphControl
			{
				Label = name,
				Value = value,
				MaxValue = 100,
				BarWidth = 60,
				ShowValue = true,
				SmoothGradient = ColorGradient.Predefined["warm"],
			});
		}
		return stack;
	}

	private static IWindowControl ChartSingleBar()
	{
		var c = new ChartControl
		{
			Name = "chart",
			PlotType = ChartPlotType.Gauge,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 100;
		c.YAxis.LabelFormatter = v => $"{v:0}%";

		foreach (var (name, value) in new[] { ("cpu", 72d), ("ram", 45d), ("disk", 88d) })
		{
			var s = c.AddSeries(name, Color.Orange1);
			s.Gradient = ColorGradient.Predefined["warm"];
			s.SetValues(new[] { value });
		}
		return c;
	}

	// --- page 4: split ---

	private static IWindowControl SparklineSplit()
	{
		var s = new SparklineControl
		{
			Mode = SparklineMode.Bidirectional,
			GraphHeight = 14,
			MinValue = 0,
			MaxValue = 100,
			Gradient = ColorGradient.FromColors(Color.Green, Color.Yellow),
			SecondaryGradient = ColorGradient.FromColors(Color.Cyan1, Color.Magenta1),
			AutoFitDataPoints = true,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		s.SetBidirectionalData(Samples, Secondary);
		return s;
	}

	private static IWindowControl ChartSplit()
	{
		var c = new ChartControl
		{
			Name = "chart",
			PlotType = ChartPlotType.Area,
			RenderMode = ChartRenderMode.Pixel,
			SplitView = true,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 100;

		var down = c.AddSeries("down", Color.Green);
		down.Gradient = ColorGradient.FromColors(Color.Green, Color.Yellow);
		down.GradientAxis = ChartGradientAxis.Y;
		down.SetValues(Samples);

		var up = c.AddSeries("up", Color.Cyan1);
		up.Gradient = ColorGradient.FromColors(Color.Cyan1, Color.Magenta1);
		up.GradientAxis = ChartGradientAxis.Y;
		up.SetValues(Secondary);
		return c;
	}

	// --- page 6: everything at once ---

	private static IWindowControl ChartEverything()
	{
		var c = new ChartControl
		{
			Name = "chart",
			Title = "Service load",
			Subtitle = "stacked by tier, last hour",
			TitleAlignment = ChartTitleAlignment.Center,
			BorderStyle = BorderStyle.Single,
			PlotType = ChartPlotType.Area,
			Stacked = true,
			ShowLegend = true,
			ShowHighLowLabels = true,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 200;
		c.YAxis.Title = "requests/s";
		c.YAxis.ShowGridlines = true;
		c.XAxis.Title = "minute";

		c.AddSeries("web", Color.Green).SetValues(new[] { 30d, 45, 38, 52, 40, 60, 35, 48 });
		c.AddSeries("api", Color.Cyan1).SetValues(new[] { 25d, 30, 42, 28, 50, 33, 45, 38 });
		c.AddSeries("cache", Color.Magenta1).SetValues(new[] { 15d, 20, 18, 25, 22, 28, 20, 24 });

		c.AddReferenceLine(150, Color.Red, "capacity");
		return c;
	}

	private static IWindowControl ChartEmpty()
	{
		return new ChartControl
		{
			Title = "Awaiting data",
			BorderStyle = BorderStyle.Single,
			EmptyText = "no samples received yet",
			VerticalAlignment = VerticalAlignment.Fill,
		};
	}

	// --- page 8: cxnet's network monitor, both ways ---
	//
	// A faithful substitution: same data, same theme colours, same shape. cxnet
	// uses a BidirectionalBraille sparkline with per-side rolling peaks, a
	// per-height gradient and a transparent background.

	private static readonly double[] Down = BuildHistory(seed: 7, peak: 40000);
	private static readonly double[] Up = BuildHistory(seed: 19, peak: 6000);

	/// <summary>A plausible throughput history, 512 samples as cxnet keeps.</summary>
	private static double[] BuildHistory(int seed, double peak)
	{
		var rng = new Random(seed);
		var v = new double[512];
		double level = peak * 0.3;
		for (int i = 0; i < v.Length; i++)
		{
			level += (rng.NextDouble() - 0.45) * peak * 0.08;
			level = Math.Clamp(level, 0, peak);
			if (rng.NextDouble() < 0.02) level = peak * (0.7 + rng.NextDouble() * 0.3);
			v[i] = level;
		}
		return v;
	}

	private static IWindowControl CxnetOld()
	{
		var s = new SparklineControl
		{
			Mode = SparklineMode.BidirectionalBraille,
			GraphHeight = 14,
			MaxValue = Down.Max(),
			SecondaryMaxValue = Up.Max(),
			Gradient = ColorGradient.FromColors(new Color(48, 82, 125), new Color(96, 165, 250)),
			SecondaryGradient = ColorGradient.FromColors(new Color(125, 73, 30), new Color(251, 146, 60)),
			AutoFitDataPoints = true,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		s.SetBidirectionalData(Down, Up);
		return s;
	}

	private static IWindowControl CxnetNew()
	{
		var c = new ChartControl
		{
			Name = "chart",
			PlotType = ChartPlotType.Area,
			RenderMode = ChartRenderMode.Braille,
			SplitView = true,
			ResampleToWidth = true,
			VerticalAlignment = VerticalAlignment.Fill,
		};

		// Per-side rolling peaks, which is what keeps a small upload readable
		// beside a large download.
		c.YAxis.Min = 0;
		c.YAxis.Max = Down.Max();
		c.LowerAxis.Min = 0;
		c.LowerAxis.Max = Up.Max();

		var down = c.AddSeries("down", new Color(96, 165, 250));
		down.Gradient = ColorGradient.FromColors(new Color(48, 82, 125), new Color(96, 165, 250));
		down.GradientAxis = ChartGradientAxis.Y;
		down.SetValues(Down);

		var up = c.AddSeries("up", new Color(251, 146, 60));
		up.Gradient = ColorGradient.FromColors(new Color(125, 73, 30), new Color(251, 146, 60));
		up.GradientAxis = ChartGradientAxis.Y;
		up.SetValues(Up);
		return c;
	}

	// --- page 7: thresholds and a second scale ---

	private static IWindowControl ChartThresholds()
	{
		var c = new ChartControl
		{
			Name = "chart",
			Title = "CPU",
			Subtitle = "colour changes past each limit",
			BorderStyle = BorderStyle.Single,
			ShowHighLowLabels = true,
			ShowLegend = true,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 100;
		c.YAxis.ShowGridlines = true;
		c.YAxis.Title = "percent";

		var cpu = c.AddSeries("cpu", Color.Green);
		cpu.SetThresholds(new ChartThreshold(70, Color.Yellow), new ChartThreshold(90, Color.Red));
		cpu.SetValues(new[] { 30d, 55, 72, 94, 85, 60, 40, 78, 95, 66, 35, 50 });

		c.AddReferenceLine(90, Color.Red, "critical");
		c.AddReferenceLine(70, Color.Yellow, "warn");
		return c;
	}

	private static IWindowControl ChartTwoScales()
	{
		var c = new ChartControl
		{
			Name = "chart",
			Title = "Rate against latency",
			BorderStyle = BorderStyle.Single,
			ShowLegend = true,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 1000;
		c.YAxis.Title = "req/s";
		c.SecondaryYAxis.Min = 0;
		c.SecondaryYAxis.Max = 400;

		c.AddSeries("requests", Color.Green).SetValues(
			new[] { 200d, 450, 380, 820, 600, 900, 450, 700 });

		var latency = c.AddSeries("latency ms", Color.Yellow);
		latency.UseSecondaryAxis = true;
		latency.SetValues(new[] { 40d, 55, 48, 180, 95, 320, 70, 140 });
		return c;
	}

	// --- page 5: the two sizings ---

	private static ChartControl SizingChart(ChartSizing sizing)
	{
		var c = new ChartControl
		{
			Name = "chart",
			PlotType = ChartPlotType.Bar,
			RenderMode = ChartRenderMode.Pixel,
			Sizing = sizing,
			StepWidth = 3,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		c.YAxis.Min = 0;
		c.YAxis.Max = 100;
		var s = c.AddSeries("samples", Color.Green);
		s.Gradient = ColorGradient.Predefined["spectrum"];
		s.GradientAxis = ChartGradientAxis.Y;
		// Half the dataset, so the growth headroom is visible.
		s.SetValues(Samples[..8]);
		return c;
	}

	private static IWindowControl ChartStretch() => SizingChart(ChartSizing.Stretch);

	private static IWindowControl ChartFixedStep() => SizingChart(ChartSizing.FixedStep);

	private static string? Flag(string[] args, string name)
	{
		int i = Array.IndexOf(args, name);
		if (i < 0) return null;
		return i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
			? args[i + 1] : string.Empty;
	}

	private static int Size(string[] args, string name, int fallback) =>
		Flag(args, name) is { Length: > 0 } v && int.TryParse(v, out int n) ? n : fallback;
}
