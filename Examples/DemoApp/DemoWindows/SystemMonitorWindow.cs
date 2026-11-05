using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;

namespace DemoApp.DemoWindows;

/// <summary>
/// A monitoring dashboard built on <see cref="ChartControl"/> and <see cref="GridControl"/>.
/// </summary>
/// <remarks>
/// <para>
/// The charts are braille. A monitor is read at a glance and what it has to show is the SHAPE
/// of a trace - a climb, a plateau, a spike - which half blocks round into a staircase. Braille
/// gives four vertical dots per cell, so the same eight rows carry thirty-two levels.
/// </para>
/// <para>
/// Layout is a real grid rather than nested columns: the tiles line up across rows because the
/// tracks say so, not because each column happened to be given matching heights. Each tile is a
/// styled cell - its own background, border and padding - so the chrome belongs to the layout and
/// the controls inside stay plain.
/// </para>
/// </remarks>
internal static class SystemMonitorWindow
{
	private const int WindowWidth = 116;
	private const int WindowHeight = 40;

	private const int MaxHistoryBuffer = 400;
	private const int BaseTickMs = 100;
	private const int EventIntervalTicks = 10;
	private const int StatusUpdateTicks = 5;
	private const int MaxEventItems = 14;

	private const double CpuMin = 5;
	private const double CpuMax = 95;
	private const double MemoryMin = 20;
	private const double MemoryMax = 85;
	private const double NetworkMin = 0;
	private const double NetworkMax = 100;
	private const double DiskMin = 5;
	private const double DiskReadMax = 80;
	private const double DiskWriteMax = 60;

	private const double CpuWarnAt = 60;
	private const double CpuCriticalAt = 80;
	private const double MemoryHighAt = 75;
	private const double NetworkSaturatedAt = 70;

	private const int ChartPlotHeight = 8;
	private const int GaugePlotHeight = 4;
	private const int TilePadding = 1;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var palette = Palette.From(ws);

		var grid = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Cells(34, min: 26))
			.Rows(GridLength.Auto(), GridLength.Auto(), GridLength.Auto(), GridLength.Star(1, min: 8))
			.RowGap(1)
			.ColumnGap(2)
			.WithPadding(1, 0, 1, 0)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(Headline(palette), 0, 0, colSpan: 2)
			.Place(ReadoutPanel(palette), 0, 2)
			.Place(CpuChart(palette), 1, 0)
			.Place(MemoryChart(palette), 1, 1)
			.Place(GaugeStack(palette), 1, 2, rowSpan: 2)
			.Place(NetworkChart(palette), 2, 0)
			.Place(DiskChart(palette), 2, 1)
			.Place(ActivityList(), 3, 0, colSpan: 3)
			.Build();

		StyleTiles(grid, palette);

		string faint = palette.Faint.ToMarkup();
		var footer = Controls.Markup(
				$"[{faint}]Simulated data   |   Braille charts, grid layout   |   ESC to close[/]")
			.Centered()
			.StickyBottom()
			.WithMargin(0, 1, 0, 0)
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("System Monitor")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(grid, footer)
			.WithAsyncWindowThread(UpdateLoopAsync)
			.OnKeyPressed((s, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)s!);
					e.Handled = true;
				}
			})
			.BuildAndShow();

		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}

	// ---- palette -----------------------------------------------------------

	/// <summary>
	/// The colours this page draws with, derived from the active theme.
	/// </summary>
	/// <remarks>
	/// Derived rather than hardcoded so the dashboard follows a theme switch. The previous version
	/// pinned Grey27 and Grey35 for its rules and borders, which read as intended on a dark theme
	/// and as muddy smears on a light one.
	/// </remarks>
	private readonly record struct Palette(
		Color Surface, Color Border, Color Heading, Color Faint,
		Color Cpu, Color Memory, Color NetUp, Color NetDown, Color DiskRead, Color DiskWrite,
		Color Warn, Color Critical)
	{
		public static Palette From(ConsoleWindowSystem ws)
		{
			Color bg = ws.Theme.WindowBackgroundColor;
			bool dark = bg.IsDark();

			// A tile sits just off the window background: enough to read as a panel,
			// not enough to become a box the eye has to climb over.
			Color surface = dark ? bg.Tint(0.10) : bg.Shade(0.05);
			Color border = dark ? bg.Tint(0.26) : bg.Shade(0.20);
			Color heading = dark ? bg.Tint(0.80) : bg.Shade(0.75);
			Color faint = PaletteColors.EnsureContrastRatio(
				dark ? bg.Tint(0.45) : bg.Shade(0.45), bg, PaletteColors.GraphicContrast);

			return new Palette(
				Surface: surface,
				Border: border,
				Heading: heading,
				Faint: faint,
				Cpu: Readable(Color.Cyan1, surface),
				Memory: Readable(Color.Green, surface),
				NetUp: Readable(Color.SpringGreen2, surface),
				NetDown: Readable(Color.DodgerBlue1, surface),
				DiskRead: Readable(Color.Magenta1, surface),
				DiskWrite: Readable(Color.Orange1, surface),
				Warn: Readable(Color.Yellow, surface),
				Critical: Readable(Color.Red, surface));
		}

		/// <summary>A series colour guaranteed to separate from the tile it is painted on.</summary>
		private static Color Readable(Color c, Color surface) =>
			PaletteColors.EnsureContrastRatio(c, surface, PaletteColors.GraphicContrast);
	}

	// ---- tiles -------------------------------------------------------------

	private static IWindowControl Headline(Palette p) =>
		Controls.Markup()
			.AddLine($"[bold {p.Heading.ToMarkup()}]System Monitor[/]")
			.AddLine($"[{p.Faint.ToMarkup()}]Four live feeds, sampled every {BaseTickMs} ms[/]")
			.Build();

	private static ChartControl CpuChart(Palette p) =>
		Trace("cpuChart", "CPU", p.Cpu, p)
			.Also(c =>
			{
				// The limits a reader checks against, drawn once so the trace does not
				// have to be measured by eye against the axis.
				c.AddReferenceLine(CpuCriticalAt, p.Critical, "crit");
				c.AddReferenceLine(CpuWarnAt, p.Warn, "warn");
				c.Series[0].SetThresholds(
					new ChartThreshold(CpuWarnAt, p.Warn),
					new ChartThreshold(CpuCriticalAt, p.Critical));
			});

	private static ChartControl MemoryChart(Palette p) =>
		Trace("memChart", "Memory", p.Memory, p)
			.Also(c =>
			{
				c.AddReferenceLine(MemoryHighAt, p.Warn, "high");
				c.Series[0].SetThresholds(new ChartThreshold(MemoryHighAt, p.Warn));
			});

	/// <summary>Up against down, about a shared zero.</summary>
	private static ChartControl NetworkChart(Palette p) =>
		Controls.Chart()
			.WithName("netChart")
			.WithTitle("Network")
			.WithSubtitle("up / down, Mbps")
			.WithPlotType(ChartPlotType.Area)
			.WithRenderMode(ChartRenderMode.Braille)
			.WithColors(background: p.Surface)
			.WithPlotBackground(Color.Transparent)
			.WithPlotHeight(ChartPlotHeight)
			.WithSplitView()
			.WithCentreAxis(" 0 ", showValue: false)
			.WithYRange(NetworkMin, NetworkMax)
			.WithLegend()
			.ConfigureLowerAxis(a => { a.Min = NetworkMin; a.Max = NetworkMax; })
			.ConfigureXAxis(a => a.ShowLabels = false)
			.WithTitleColor(p.Heading)
			.WithSubtitleColor(p.Faint)
			.AddSeries("up", p.NetUp, Array.Empty<double>())
			.AddSeries("down", p.NetDown, Array.Empty<double>())
			.Build();

	/// <summary>Read against write, two series on one scale.</summary>
	private static ChartControl DiskChart(Palette p) =>
		Controls.Chart()
			.WithName("diskChart")
			.WithTitle("Disk I/O")
			.WithSubtitle("read / write, MB/s")
			.WithPlotType(ChartPlotType.Line)
			.WithRenderMode(ChartRenderMode.Braille)
			.WithColors(background: p.Surface)
			.WithPlotBackground(Color.Transparent)
			.WithPlotHeight(ChartPlotHeight)
			.WithYRange(0, DiskReadMax)
			.WithGridlines()
			.WithLegend()
			.ConfigureXAxis(a => a.ShowLabels = false)
			.WithTitleColor(p.Heading)
			.WithSubtitleColor(p.Faint)
			.AddSeries("read", p.DiskRead, Array.Empty<double>())
			.AddSeries("write", p.DiskWrite, Array.Empty<double>())
			.Build();

	/// <summary>A trace tile: one series, braille, no X labels - the axis is "recently".</summary>
	private static ChartControl Trace(string name, string title, Color colour, Palette p,
		ChartPlotType type = ChartPlotType.Line) =>
		Controls.Chart()
			.WithName(name)
			.WithTitle(title)
			.WithSubtitle("percent")
			.WithPlotType(type)
			.WithRenderMode(ChartRenderMode.Braille)
			.WithColors(background: p.Surface)
			.WithPlotBackground(Color.Transparent)
			.WithPlotHeight(ChartPlotHeight)
			.WithYRange(0, 100)
			.WithGridlines()
			.ConfigureXAxis(a => a.ShowLabels = false)
			.WithTitleColor(p.Heading)
			.WithSubtitleColor(p.Faint)
			.AddSeries("v", colour, Array.Empty<double>())
			.Build();

	/// <summary>The four current readings as gauges: one row each, length is the value.</summary>
	private static IWindowControl GaugeStack(Palette p)
	{
		var panel = Controls.ScrollablePanel()
			.WithAutoScroll(false)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		panel.AddControl(Controls.Markup($"[bold {p.Heading.ToMarkup()}]Now[/]")
			.WithMargin(0, 0, 0, 1)
			.Build());

		// A gauge reads left to right, so the gradient runs along X: green at the
		// quiet end, red at the limit, which puts the warning in the bar itself.
		var scale = ColorGradient.FromColors(p.Memory, p.Warn, p.Critical);

		panel.AddControl(Controls.Chart()
			.WithName("gauges")
			.AsGauge()
			.WithColors(background: p.Surface)
			.WithPlotBackground(Color.Transparent)
			.WithPlotHeight(GaugePlotHeight)
			.WithYRange(0, 100)
			.ConfigureYAxis(a => a.LabelFormatter = v => $"{v:0}%")
			.AddSeries("cpu", p.Cpu, new[] { 0d }, scale, ChartGradientAxis.X)
			.AddSeries("memory", p.Memory, new[] { 0d }, scale, ChartGradientAxis.X)
			.AddSeries("disk", p.DiskRead, new[] { 0d }, scale, ChartGradientAxis.X)
			.AddSeries("network", p.NetDown, new[] { 0d }, scale, ChartGradientAxis.X)
			.Build());

		// The gauges say where things stand; this says where they have been. A
		// stacked total of all four, so one glance answers "is the box busier
		// than it was a minute ago" without reading four separate traces.
		panel.AddControl(Controls.Markup($"[bold {p.Heading.ToMarkup()}]Total load[/]")
			.WithMargin(0, 1, 0, 1)
			.Build());

		panel.AddControl(Controls.Chart()
			.WithName("loadChart")
			.WithPlotType(ChartPlotType.Area)
			.WithRenderMode(ChartRenderMode.Braille)
			.WithColors(background: p.Surface)
			.WithPlotBackground(Color.Transparent)
			.WithPlotHeight(ChartPlotHeight)
			.Stacked()
			.WithYRange(0, 400)
			.WithGridlines()
			.ConfigureXAxis(a => a.ShowLabels = false)
			.ConfigureYAxis(a => a.ShowLabels = false)
			.AddSeries("cpu", p.Cpu, Array.Empty<double>())
			.AddSeries("mem", p.Memory, Array.Empty<double>())
			.AddSeries("disk", p.DiskRead, Array.Empty<double>())
			.AddSeries("net", p.NetDown, Array.Empty<double>())
			.Build());

		return panel;
	}

	/// <summary>
	/// The current numbers, in the header band beside the title.
	/// </summary>
	/// <remarks>
	/// Two lines of text do not need a tile of their own. Given one on the bottom
	/// row it stretched to fill every row the charts did not want, leaving a large
	/// empty box under the traces; up here it costs nothing and the bottom row
	/// goes to the activity log, which can use the height.
	/// </remarks>
	private static IWindowControl ReadoutPanel(Palette p) =>
		Controls.Markup($"[{p.Faint.ToMarkup()}]Waiting for the first sample[/]")
			.WithName("readout")
			.Build();

	private static IWindowControl ActivityList() =>
		Controls.List()
			.WithName("activityList")
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.MaxVisibleItems(MaxEventItems)
			.Build();

	/// <summary>
	/// Gives every tile its surface, border and breathing room.
	/// </summary>
	/// <remarks>
	/// Done on the CELLS rather than inside each control: the tile is a property of the layout, so
	/// a chart dropped into a different cell does not carry a panel's chrome around with it.
	/// </remarks>
	private static void StyleTiles(GridControl grid, Palette p)
	{
		(int Row, int Col)[] tiles =
		{
			(1, 0), (1, 1), (1, 2),
			(2, 0), (2, 1),
			(3, 0),
		};

		foreach (var (row, col) in tiles)
		{
			var cell = grid[row, col];
			cell.Background = p.Surface;
			cell.Border = BorderStyle.Rounded;
			cell.Padding = new Padding(TilePadding, 0, TilePadding, 0);
		}
	}

	// ---- the feed ----------------------------------------------------------

	private static async Task UpdateLoopAsync(Window window, CancellationToken ct)
	{
		var random = new Random();
		double cpu = 25, mem = 45, diskR = 20, diskW = 15, netUp = 5, netDown = 15;

		var cpuHistory = new List<double>();
		var memHistory = new List<double>();
		var netUpHistory = new List<double>();
		var netDownHistory = new List<double>();
		var diskRHistory = new List<double>();
		var diskWHistory = new List<double>();
		int tick = 0;

		var activityList = window.FindControl<ListControl>("activityList");
		activityList?.AddItem(new ListItem("[green]Monitoring started[/]"));

		while (!ct.IsCancellationRequested)
		{
			try
			{
				tick++;

				cpu = Math.Clamp(cpu + (random.NextDouble() - 0.5) * 8, CpuMin, CpuMax);
				mem = Math.Clamp(mem + (random.NextDouble() - 0.5) * 3, MemoryMin, MemoryMax);
				netUp = Math.Clamp(netUp + (random.NextDouble() - 0.5) * 10, NetworkMin, NetworkMax);
				netDown = Math.Clamp(netDown + (random.NextDouble() - 0.5) * 15, NetworkMin, NetworkMax);
				diskR = Math.Clamp(diskR + (random.NextDouble() - 0.5) * 12, DiskMin, DiskReadMax);
				diskW = Math.Clamp(diskW + (random.NextDouble() - 0.5) * 8, DiskMin, DiskWriteMax);

				AddHistory(cpuHistory, cpu);
				AddHistory(memHistory, mem);
				AddHistory(netUpHistory, netUp);
				AddHistory(netDownHistory, netDown);
				AddHistory(diskRHistory, diskR);
				AddHistory(diskWHistory, diskW);

				Feed(window, "cpuChart", cpuHistory);
				Feed(window, "memChart", memHistory);
				Feed(window, "netChart", netUpHistory, netDownHistory);
				Feed(window, "diskChart", diskRHistory, diskWHistory);

				var load = window.FindControl<ChartControl>("loadChart");
				if (load != null && load.Series.Count == 4)
				{
					load.Series[0].SetValues(cpuHistory.ToArray());
					load.Series[1].SetValues(memHistory.ToArray());
					load.Series[2].SetValues(diskRHistory.ToArray());
					load.Series[3].SetValues(netDownHistory.ToArray());
					load.Invalidate(Invalidation.Repaint);
				}

				var gauges = window.FindControl<ChartControl>("gauges");
				if (gauges != null && gauges.Series.Count == 4)
				{
					gauges.Series[0].SetValues(new[] { cpu });
					gauges.Series[1].SetValues(new[] { mem });
					gauges.Series[2].SetValues(new[] { (diskR + diskW) / 2 });
					gauges.Series[3].SetValues(new[] { (netUp + netDown) / 2 });
					gauges.Invalidate(Invalidation.Repaint);
				}

				if (tick % StatusUpdateTicks == 0)
				{
					var readout = window.FindControl<MarkupControl>("readout");
					readout?.SetContent(Readout(cpu, mem, diskR, diskW, netUp, netDown));
				}

				if (tick % EventIntervalTicks == 0)
				{
					LogActivity(window, random, cpu, mem, netDown);
				}
			}
			catch (Exception)
			{
				// Controls are disposed as the window closes; the next tick will not run.
			}

			try { await Task.Delay(BaseTickMs, ct); }
			catch (TaskCanceledException) { break; }
		}
	}

	/// <summary>Replaces a chart's series data and repaints it.</summary>
	/// <remarks>
	/// SetValues rather than AddPoint: the history list is already the window, so handing it over
	/// whole keeps the chart's X running 0..n-1 with no counter to drift out of step.
	/// </remarks>
	private static void Feed(Window window, string name,
		List<double> primary, List<double>? secondary = null)
	{
		var chart = window.FindControl<ChartControl>(name);
		if (chart == null || chart.Series.Count == 0) return;

		chart.Series[0].SetValues(primary.ToArray());
		if (secondary != null && chart.Series.Count > 1)
			chart.Series[1].SetValues(secondary.ToArray());

		chart.Invalidate(Invalidation.Repaint);
	}

	private static List<string> Readout(double cpu, double mem,
		double diskR, double diskW, double netUp, double netDown)
	{
		// A dot carries the state instead of a word: the header column is narrow,
		// and "CRITICAL" spelled out wrapped the line onto a third row.
		string cpuDot = cpu > CpuCriticalAt ? "[red]*[/]"
			: cpu > CpuWarnAt ? "[yellow]*[/]"
			: "[green]*[/]";
		string memDot = mem > MemoryHighAt ? "[yellow]*[/]" : "[green]*[/]";

		return new List<string>
		{
			$"{cpuDot} [bold]CPU[/] {cpu,5:F1}%   {memDot} [bold]Mem[/] {mem,5:F1}%",
			$"[magenta1]r{diskR,3:F0}[/] [orange1]w{diskW,3:F0}[/] MB/s  "
				+ $"[springgreen2]{netUp,3:F0}[/]/[dodgerblue1]{netDown,3:F0}[/] Mbps",
		};
	}

	private static void LogActivity(Window window, Random random,
		double cpu, double mem, double netDown)
	{
		var list = window.FindControl<ListControl>("activityList");
		if (list == null) return;

		string time = DateTime.Now.ToString("HH:mm:ss");
		string entry;

		if (cpu > CpuCriticalAt)
			entry = $"[red]{time}  CPU critical {cpu:F0}%[/]";
		else if (mem > MemoryHighAt)
			entry = $"[yellow]{time}  Memory high {mem:F0}%[/]";
		else if (netDown > NetworkSaturatedAt)
			entry = $"[orange1]{time}  Net busy {netDown:F0} Mbps[/]";
		else
		{
			string[] types = { "CPU sample", "Mem check", "I/O poll", "Net ping", "Heartbeat" };
			entry = $"[grey]{time}  {types[random.Next(types.Length)]} ok[/]";
		}

		list.AddItem(new ListItem(entry));
		while (list.Items.Count > MaxEventItems) list.Items.RemoveAt(0);
		if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1;
	}

	private static void AddHistory(List<double> history, double value)
	{
		history.Add(value);
		while (history.Count > MaxHistoryBuffer) history.RemoveAt(0);
	}

	/// <summary>Applies a configuration step to a freshly built control, inline.</summary>
	private static T Also<T>(this T control, Action<T> configure)
	{
		configure(control);
		return control;
	}
}
