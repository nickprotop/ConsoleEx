using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class SystemMonitorWindow
{
	private const int WindowWidth = 110;
	private const int WindowHeight = 38;
	private const int MaxHistoryBuffer = 500;
	private const int BaseTickMs = 100;
	private const int EventIntervalTicks = 10;
	private const int StatusUpdateTicks = 5;
	private const int MaxEventItems = 15;
	private const double CpuMin = 5;
	private const double CpuMax = 95;
	private const double MemoryMin = 20;
	private const double MemoryMax = 85;
	private const double NetworkMin = 0;
	private const double NetworkMax = 100;
	private const double DiskMin = 5;
	private const double DiskReadMax = 80;
	private const double DiskWriteMax = 60;
	private const int SparklineHeight = 4;
	private const int LabelWidth = 7;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var mainGrid = Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 0)
			.WithSplitterAfter(0)
			// Left column: Resources + Activity
			.Column(col => col
				.Width(38)
				// Resources section
				.Add(Controls.Markup("[bold cyan]Resources[/]")
					.WithMargin(0, 0, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).Build())
				.Add(MakeBar("cpuBar", "CPU", 25, Color.Green, Color.Yellow, Color.Red))
				.Add(MakeBar("memBar", "Mem", 45, Color.Cyan1, Color.Yellow, Color.Orange1))
				.Add(MakeBar("diskBar", "Disk", 30, Color.Blue, Color.Cyan1, Color.Green))
				.Add(MakeBar("netBar", "Net", 15, Color.DodgerBlue1, Color.Magenta1, Color.Red))
				// Status section
				.Add(Controls.Markup("[bold white]Status[/]")
					.WithMargin(0, 1, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).Build())
				.Add(PanelControl.Create()
					.WithName("statusPanel")
					.Rounded()
					.WithBorderColor(Color.Grey35)
					.WithPadding(1, 0)
					.WithContent("[grey]Initializing...[/]")
					.Build())
				// Activity section
				.Add(Controls.Markup("[bold grey70]Activity[/]")
					.WithMargin(0, 1, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).Build())
				.Add(Controls.List()
					.WithName("activityList")
					.WithAlignment(HorizontalAlignment.Stretch)
					.WithVerticalAlignment(VerticalAlignment.Fill)
					.MaxVisibleItems(MaxEventItems)
					.Build())
			)
			// Right column: Sparklines
			.Column(col => col
				// CPU sparkline
				.Add(Controls.Markup("[bold cyan]CPU[/]")
					.WithMargin(1, 0, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).WithMargin(1, 0, 0, 0).Build())
				.Add(new SparklineBuilder()
					.WithName("cpuSparkline")
					.WithHeight(SparklineHeight)
					.WithAutoFitDataPoints()
					.WithMode(SparklineMode.Block)
					.WithBarColor(Color.Cyan1)
					.WithGradient("cool")
					.WithBaseline(true, '─', Color.Grey35, TitlePosition.Bottom)
					.WithAlignment(HorizontalAlignment.Stretch)
					.WithMargin(1, 0, 0, 0)
					.Build())
				// Memory sparkline
				.Add(Controls.Markup("[bold green]Memory[/]")
					.WithMargin(1, 1, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).WithMargin(1, 0, 0, 0).Build())
				.Add(new SparklineBuilder()
					.WithName("memSparkline")
					.WithHeight(SparklineHeight)
					.WithAutoFitDataPoints()
					.WithMode(SparklineMode.Braille)
					.WithBarColor(Color.Green)
					.WithGradient("warm")
					.WithBaseline(true, '╌', Color.Grey35, TitlePosition.Bottom)
					.WithAlignment(HorizontalAlignment.Stretch)
					.WithMargin(1, 0, 0, 0)
					.Build())
				// Network sparkline
				.Add(Controls.Markup("[bold yellow]Network[/]  [dim green]▲ Up[/]  [dim red]▼ Down[/]")
					.WithMargin(1, 1, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).WithMargin(1, 0, 0, 0).Build())
				.Add(new SparklineBuilder()
					.WithName("netSparkline")
					.WithHeight(SparklineHeight)
					.WithAutoFitDataPoints()
					.WithMode(SparklineMode.Bidirectional)
					.WithBarColor(Color.Green)
					.WithSecondaryBarColor(Color.Red)
					.WithBaseline(true, '┄', Color.Grey35, TitlePosition.Bottom)
					.WithInlineTitleBaseline(true)
					.WithAlignment(HorizontalAlignment.Stretch)
					.WithMargin(1, 0, 0, 0)
					.Build())
				// Disk sparkline
				.Add(Controls.Markup("[bold magenta1]Disk I/O[/]  [dim blue]▲ Read[/]  [dim magenta1]▼ Write[/]")
					.WithMargin(1, 1, 0, 0).Build())
				.Add(Controls.RuleBuilder().WithColor(Color.Grey27).WithMargin(1, 0, 0, 0).Build())
				.Add(new SparklineBuilder()
					.WithName("diskSparkline")
					.WithHeight(SparklineHeight)
					.WithAutoFitDataPoints()
					.WithMode(SparklineMode.BidirectionalBraille)
					.WithBarColor(Color.Blue)
					.WithSecondaryBarColor(Color.Magenta1)
					.WithBaseline(true, '┈', Color.Grey35, TitlePosition.Bottom)
					.WithInlineTitleBaseline(true)
					.WithAlignment(HorizontalAlignment.Stretch)
					.WithMargin(1, 0, 0, 0)
					.Build())
			)
			.Build();

		var bottomRule = Controls.RuleBuilder()
			.WithColor(Color.Grey27)
			.StickyBottom()
			.Build();

		var footer = Controls.Markup("[dim]Live simulated data[/]  [dim grey50]|[/]  [dim]ESC to close[/]")
			.Centered()
			.StickyBottom()
			.Build();

		var gradient = ColorGradient.FromColors(
			new Color(30, 45, 80),
			new Color(5, 5, 12));

		return new WindowBuilder(ws)
			.WithTitle("System Monitor")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.WithBackgroundGradient(gradient, GradientDirection.Vertical)
			.AddControls(mainGrid, bottomRule, footer)
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
	}

	private static BarGraphControl MakeBar(string name, string label, double value,
		Color color1, Color color2, Color color3)
	{
		return new BarGraphBuilder()
			.WithName(name)
			.WithLabel(label)
			.WithLabelWidth(LabelWidth)
			.WithValue(value)
			.WithMaxValue(100)
			.WithUnfilledColor(Color.Grey27)
			.ShowLabel()
			.ShowValue()
			.WithValueFormat("F0")
			.WithSmoothGradient(color1, color2, color3)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
	}

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

		// Seed the activity list
		var activityList = window.FindControl<ListControl>("activityList");
		activityList?.AddItem(new ListItem("[green]System monitoring started[/]"));

		while (!ct.IsCancellationRequested)
		{
			try
			{
				tick++;

				// Smooth random walk
				cpu = Math.Clamp(cpu + (random.NextDouble() - 0.5) * 8, CpuMin, CpuMax);
				mem = Math.Clamp(mem + (random.NextDouble() - 0.5) * 3, MemoryMin, MemoryMax);
				netUp = Math.Clamp(netUp + (random.NextDouble() - 0.5) * 10, NetworkMin, NetworkMax);
				netDown = Math.Clamp(netDown + (random.NextDouble() - 0.5) * 15, NetworkMin, NetworkMax);
				diskR = Math.Clamp(diskR + (random.NextDouble() - 0.5) * 12, DiskMin, DiskReadMax);
				diskW = Math.Clamp(diskW + (random.NextDouble() - 0.5) * 8, DiskMin, DiskWriteMax);

				// History
				AddHistory(cpuHistory, cpu);
				AddHistory(netUpHistory, netUp);
				AddHistory(netDownHistory, netDown);
				if (tick % 2 == 0) AddHistory(memHistory, mem);
				if (tick % 3 == 0) { AddHistory(diskRHistory, diskR); AddHistory(diskWHistory, diskW); }

				// Sparklines - each at different rates
				window.FindControl<SparklineControl>("cpuSparkline")?.SetDataPoints(cpuHistory);

				if (tick % 2 == 0)
					window.FindControl<SparklineControl>("memSparkline")?.SetDataPoints(memHistory);

				var netSpark = window.FindControl<SparklineControl>("netSparkline");
				if (netSpark != null)
				{
					netSpark.SetDataPoints(netUpHistory);
					netSpark.SetSecondaryDataPoints(netDownHistory);
				}

				if (tick % 3 == 0)
				{
					var diskSpark = window.FindControl<SparklineControl>("diskSparkline");
					if (diskSpark != null)
					{
						diskSpark.SetDataPoints(diskRHistory);
						diskSpark.SetSecondaryDataPoints(diskWHistory);
					}
				}

				// Bar graphs at staggered rates
				if (tick % 3 == 0)
				{
					var bar = window.FindControl<BarGraphControl>("cpuBar");
					if (bar != null) bar.Value = cpu;
				}
				if (tick % 5 == 0)
				{
					var bar = window.FindControl<BarGraphControl>("memBar");
					if (bar != null) bar.Value = mem;
				}
				if (tick % 7 == 0)
				{
					var bar = window.FindControl<BarGraphControl>("diskBar");
					if (bar != null) bar.Value = (diskR + diskW) / 2;
				}
				if (tick % 4 == 0)
				{
					var bar = window.FindControl<BarGraphControl>("netBar");
					if (bar != null) bar.Value = (netUp + netDown) / 2;
				}

				// Status panel
				if (tick % StatusUpdateTicks == 0)
				{
					var statusPanel = window.FindControl<PanelControl>("statusPanel");
					if (statusPanel != null)
					{
						var cpuStatus = cpu > 80 ? "[red bold]CRITICAL[/]" :
							cpu > 60 ? "[yellow]Elevated[/]" : "[green]Normal[/]";
						var memStatus = mem > 75 ? "[yellow]High[/]" : "[green]Normal[/]";

						statusPanel.SetContent(
							$"[white]CPU[/]     {cpu,5:F1}%  {cpuStatus}\n" +
							$"[white]Memory[/]  {mem,5:F1}%  {memStatus}\n" +
							$"[white]Disk[/]    [cyan]R {diskR,4:F0}[/] [magenta1]W {diskW,4:F0}[/] MB/s\n" +
							$"[white]Network[/] [green]▲{netUp,4:F0}[/] [red]▼{netDown,4:F0}[/] Mbps");
					}
				}

				// Activity log
				if (tick % EventIntervalTicks == 0)
				{
					activityList = window.FindControl<ListControl>("activityList");
					if (activityList != null)
					{
						var time = DateTime.Now.ToString("HH:mm:ss");
						string entry;

						if (cpu > 80)
							entry = $"[red]{time} CPU critical: {cpu:F0}%[/]";
						else if (mem > 75)
							entry = $"[yellow]{time} Memory high: {mem:F0}%[/]";
						else if (netDown > 70)
							entry = $"[orange1]{time} Net saturated: {netDown:F0} Mbps[/]";
						else
						{
							var types = new[] { "CPU sample", "Mem check", "I/O poll", "Net ping", "Heartbeat" };
							entry = $"[grey]{time} {types[random.Next(types.Length)]} OK[/]";
						}

						activityList.AddItem(new ListItem(entry));
						while (activityList.Items.Count > MaxEventItems)
							activityList.Items.RemoveAt(0);
						if (activityList.Items.Count > 0)
							activityList.SelectedIndex = activityList.Items.Count - 1;
					}
				}
			}
			catch (Exception)
			{
				// Silently continue on control disposal during window close
			}

			try { await Task.Delay(BaseTickMs, ct); }
			catch (TaskCanceledException) { break; }
		}
	}

	private static void AddHistory(List<double> history, double value)
	{
		history.Add(value);
		while (history.Count > MaxHistoryBuffer)
			history.RemoveAt(0);
	}
}
