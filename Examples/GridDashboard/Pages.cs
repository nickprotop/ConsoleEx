// -----------------------------------------------------------------------
// GridDashboard — "Mission Control" example for SharpConsoleUI.
//
// Page builders. Each returns a Fill+Stretch GridControl that becomes the
// direct root of a NavigationView page (via nav.SetItemContent). The grids
// are handed to Simulation, which mutates the live controls each tick.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace GridDashboard;

/// <summary>
/// Holder for the live control references the <see cref="Simulation"/> updates each tick.
/// Populated by the page builders so the background walk can touch them directly.
/// </summary>
internal sealed class DashboardRefs
{
	// Overview tiles
	public MarkupControl[] TileNumbers { get; set; } = Array.Empty<MarkupControl>();
	public SparklineControl[] TileSparks { get; set; } = Array.Empty<SparklineControl>();
	public LineGraphControl OverviewGraph { get; set; } = null!;

	// Processes
	public TableControl ProcessTable { get; set; } = null!;

	// Network
	public LineGraphControl NetIn { get; set; } = null!;
	public LineGraphControl NetOut { get; set; } = null!;

	// Logs
	public ScrollablePanelControl LogPanel { get; set; } = null!;
}

internal static class Pages
{
	private static readonly Color BgSlate = new(28, 32, 48);
	private static readonly Color TileBg = new(34, 40, 60);
	private static readonly Color BorderClr = new(60, 80, 120);

	private static GridBuilder FillGrid() =>
		Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch);

	#region Overview

	/// <summary>
	/// Four metric tiles (big number + sparkline) across the top row, a wide line graph below.
	/// </summary>
	public static GridControl BuildOverviewGrid(DashboardRefs refs)
	{
		string[] tileTitles = { "CPU", "Memory", "Network", "Disk" };
		Color[] tileColors =
		{
			new(100, 180, 255), new(120, 220, 160), new(220, 180, 60), new(220, 120, 80)
		};

		var numbers = new MarkupControl[4];
		var sparks = new SparklineControl[4];

		var grid = FillGrid()
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithPadding(1, 1, 1, 1)
			.Build();

		for (int i = 0; i < 4; i++)
		{
			var stack = Controls.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Build();

			var label = Controls.Markup($"[dim]{tileTitles[i]}[/]")
				.WithMargin(1, 1, 1, 0).Build();
			var number = Controls.Markup($"[bold rgb({tileColors[i].R},{tileColors[i].G},{tileColors[i].B})]  0%[/]")
				.WithMargin(1, 0, 1, 0).Build();
			var spark = Controls.Sparkline()
				.WithData(new double[] { 0 })
				.WithBarColor(tileColors[i])
				.WithBackgroundColor(TileBg)
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithMargin(1, 0, 1, 1)
				.Build();
			spark.MaxDataPoints = Simulation.MaxPoints;

			stack.AddControl(label);
			stack.AddControl(number);
			stack.AddControl(spark);

			numbers[i] = number;
			sparks[i] = spark;
			grid.Place(stack, 0, i);
			grid.Cell(0, i).Border = BorderStyle.Rounded;
			grid.Cell(0, i).Background = TileBg;
		}

		var graph = Controls.LineGraph()
			.WithTitle("System Load (60s window)")
			.WithMode(LineGraphMode.Braille)
			.WithColorRole(ColorRole.Info)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithData(new double[] { 0 })
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();
		graph.MaxDataPoints = Simulation.MaxPoints;

		grid.Place(graph, 1, 0, colSpan: 4);
		grid.Cell(1, 0).Border = BorderStyle.Rounded;
		grid.Cell(1, 0).Background = BgSlate;

		refs.TileNumbers = numbers;
		refs.TileSparks = sparks;
		refs.OverviewGraph = graph;
		return grid;
	}

	#endregion

	#region Processes

	public static GridControl BuildProcessesGrid(DashboardRefs refs)
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Star(1))
			.RowGap(1)
			.WithPadding(1, 1, 1, 1)
			.Build();

		var header = Controls.Markup(
				"[bold rgb(120,180,255)]Processes[/]   [dim]top consumers · CPU% churns live[/]")
			.WithMargin(1, 0, 1, 0)
			.Build();

		var table = Controls.Table()
			.WithColorRole(ColorRole.Primary)
			.AddColumn("PID", TextJustification.Right)
			.AddColumn("Name")
			.AddColumn("CPU%", TextJustification.Right)
			.AddColumn("Mem", TextJustification.Right)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 0)
			.Build();

		(int pid, string name, int cpu, int mem)[] seed =
		{
			(1042, "nginx", 12, 84),
			(1188, "postgres", 23, 412),
			(1356, "redis-server", 8, 196),
			(1490, "node app.js", 31, 268),
			(1622, "dotnet host", 19, 340),
			(1755, "rabbitmq", 6, 152),
			(1893, "grafana", 4, 118),
			(2014, "prometheus", 15, 224),
			(2210, "containerd", 9, 96),
			(2388, "sshd", 1, 12),
			(2501, "systemd", 2, 28),
			(2677, "kworker", 3, 8),
		};
		foreach (var (pid, name, cpu, mem) in seed)
			table.AddRow(pid.ToString(), name, cpu.ToString(), $"{mem}M");

		grid.Place(header, 0, 0);
		grid.Place(table, 1, 0);
		grid.Cell(1, 0).Border = BorderStyle.Rounded;
		grid.Cell(1, 0).Background = BgSlate;

		refs.ProcessTable = table;
		return grid;
	}

	#endregion

	#region Network

	public static GridControl BuildNetworkGrid(DashboardRefs refs)
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.ColumnGap(2)
			.ColumnSplitterAfter(0)
			.WithPadding(1, 1, 1, 1)
			.Build();

		var inGraph = Controls.LineGraph()
			.WithTitle("Inbound (Mbps)")
			.WithMode(LineGraphMode.Braille)
			.WithColorRole(ColorRole.Success)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithData(new double[] { 0 })
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();
		inGraph.MaxDataPoints = Simulation.MaxPoints;

		var outGraph = Controls.LineGraph()
			.WithTitle("Outbound (Mbps)")
			.WithMode(LineGraphMode.Braille)
			.WithColorRole(ColorRole.Warning)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithData(new double[] { 0 })
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();
		outGraph.MaxDataPoints = Simulation.MaxPoints;

		grid.Place(inGraph, 0, 0);
		grid.Place(outGraph, 0, 1);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 1).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = BgSlate;
		grid.Cell(0, 1).Background = BgSlate;

		refs.NetIn = inGraph;
		refs.NetOut = outGraph;
		return grid;
	}

	#endregion

	#region Logs

	public static GridControl BuildLogsGrid(DashboardRefs refs)
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.WithPadding(1, 1, 1, 1)
			.Build();

		var log = Controls.ScrollablePanel()
			.WithAutoScroll()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		log.AddControl(Controls.Markup("[bold rgb(120,180,255)]Event Stream[/]  [dim](auto-scroll)[/]")
			.WithMargin(1, 1, 1, 0).Build());

		grid.Place(log, 0, 0);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = BgSlate;

		refs.LogPanel = log;
		return grid;
	}

	#endregion

	#region Settings

	public static GridControl BuildSettingsGrid()
	{
		var grid = FillGrid()
			.Columns(GridLength.Cells(16), GridLength.Star(1))
			.Rows(GridLength.Cells(2), GridLength.Cells(2), GridLength.Cells(3), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithPadding(2, 2, 2, 2)
			.Build();

		grid.Place(Controls.Markup("[dim]Tick rate[/]").WithMargin(1, 0, 1, 0).Build(), 0, 0);
		grid.Place(Controls.Dropdown()
			.WithPrompt("rate: ")
			.AddItems("Fast (300ms)", "Normal (600ms)", "Slow (1200ms)")
			.SelectedIndex(1)
			.WithMargin(0, 0, 1, 0)
			.Build(), 0, 1);

		grid.Place(Controls.Markup("[dim]Animation[/]").WithMargin(1, 0, 1, 0).Build(), 1, 0);
		grid.Place(Controls.Checkbox("Animate charts").Checked()
			.WithMargin(0, 0, 1, 0).Build(), 1, 1);

		grid.Place(Controls.Markup("[dim]Note[/]").WithMargin(1, 0, 1, 0).Build(), 2, 0);
		grid.Place(Controls.Markup(
				"[dim]Settings here are illustrative — the dashboard runs a deterministic walk.[/]")
			.WithMargin(0, 0, 1, 0).Build(), 2, 1);

		return grid;
	}

	#endregion
}
