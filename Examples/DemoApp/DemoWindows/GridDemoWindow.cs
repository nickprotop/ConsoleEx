// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases the <see cref="GridControl"/> as a ServerHub-style tiled dashboard. A single grid fills
/// the window with a header that spans all four columns (col-span), a tile field below it with a
/// different control in every cell — line graph, bar graphs, a scrollable log, a services list, an
/// interactive settings panel (dropdown + sliders + checkboxes driving a live gauge), an editable
/// command prompt, and a tree — a tile that spans two rows (row-span), per-cell border/background
/// styling through the <see cref="GridCell"/> surface, and a theme-derived colour role so the cell
/// chrome re-tints when the toolbar theme changes. The interactive cells update OTHER cells live
/// (selecting a service updates the detail panel; the threshold slider drives the gauge + its colour),
/// proving the grid hosts fully-interactive controls that talk to each other. Tab walks the focusable
/// cells row-major.
/// </summary>
public static class GridDemoWindow
{
	private const int WindowWidth = 112;
	private const int WindowHeight = 38;

	public static Window Create(ConsoleWindowSystem ws)
	{
		// ── Header tile (row 0) — spans all four columns to prove col-span. ─────────────────────────
		var header = Controls.Markup(
				"[bold]System Dashboard[/]   [dim]ServerHub · 4-column grid · spans · gaps · per-cell styling · live interactive cells[/]")
			.WithMargin(1, 0, 1, 0)
			.Build();

		// ── CPU trend tile (row 1, col 0) — a LineGraph proves "any control in a cell". Live-updated:
		//    the async window thread pushes a new sample each tick and old points roll off. ──────────
		var cpuGraph = Controls.LineGraph()
			.WithTitle("CPU %")
			.WithMode(LineGraphMode.Braille)
			.WithColorRole(ColorRole.Info)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithData(new double[] { 18, 24, 31, 27, 44, 52, 61, 48, 39, 55, 67, 72, 64, 58, 49, 41 })
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();
		cpuGraph.MaxDataPoints = 48;     // rolling window — old samples scroll off as new ones arrive

		// ── Memory / disk tile (row 1, col 1) — stacked BarGraphs in a scroll-free panel. Live-updated. ─
		var memBar = Controls.BarGraph().WithLabel("Mem ").WithValue(73).WithMaxValue(100)
			.WithColorRole(ColorRole.Warning).ShowValue().WithMargin(1, 1, 1, 0).Build();
		var diskBar = Controls.BarGraph().WithLabel("Disk").WithValue(48).WithMaxValue(100)
			.WithColorRole(ColorRole.Success).ShowValue().WithMargin(1, 0, 1, 0).Build();
		var swapBar = Controls.BarGraph().WithLabel("Swap").WithValue(12).WithMaxValue(100)
			.WithColorRole(ColorRole.Info).ShowValue().WithMargin(1, 0, 1, 0).Build();
		var netBar = Controls.BarGraph().WithLabel("Net ").WithValue(61).WithMaxValue(100)
			.WithColorRole(ColorRole.Primary).ShowValue().WithMargin(1, 0, 1, 1).Build();
		var resourcePanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Resources[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(memBar)
			.AddControl(diskBar)
			.AddControl(swapBar)
			.AddControl(netBar)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Alerts tile (row 1, col 3) — spans rows 1 AND 2 to prove row-span. A scrollable log of
		//    short content proves compose-to-scroll inside a single cell. ────────────────────────────
		var alertsLog = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		alertsLog.AddControl(Controls.Markup("[bold]Activity Log[/]  [dim](scrolls)[/]").WithMargin(1, 1, 1, 0).Build());
		(string dot, string msg)[] logEntries =
		{
			("[green]●[/]", "web-01 ok"),
			("[green]●[/]", "web-02 ok"),
			("[yellow]●[/]", "api latency"),
			("[green]●[/]", "db synced"),
			("[red]●[/]", "cache fail"),
			("[green]●[/]", "queue ok"),
			("[yellow]●[/]", "disk 73%"),
			("[green]●[/]", "web-03 up"),
			("[yellow]●[/]", "api retry"),
			("[green]●[/]", "tls ok"),
			("[red]●[/]", "cache out"),
			("[green]●[/]", "backup ok"),
			("[green]●[/]", "scaled +1"),
			("[yellow]●[/]", "mem 73%"),
			("[green]●[/]", "sweep ok"),
		};
		int logSeq = 1;
		for (int pass = 0; pass < 3; pass++)
			foreach (var (dot, msg) in logEntries)
				alertsLog.AddControl(Controls.Markup($"[dim]{logSeq++,2}[/] {dot} {msg}").WithMargin(1, 0, 1, 0).Build());

		// ── Services tile (row 2, col 0) — a focusable List that drives the detail panel below. ─────
		var detail = Controls.Markup("[dim]Select a service…[/]").WithMargin(1, 1, 1, 1).Build();
		(string name, string status, string detailText)[] services =
		{
			("nginx",    "[green]running[/]",  "[bold]nginx[/]  [green]● running[/]\nport 80/443 · 4 workers\nuptime 12d 4h · 0 restarts"),
			("postgres", "[green]running[/]",  "[bold]postgres[/]  [green]● running[/]\nport 5432 · 38 connections\nuptime 12d 4h · WAL ok"),
			("redis",    "[yellow]degraded[/]","[bold]redis[/]  [yellow]● degraded[/]\nport 6379 · evicting keys\nmem 73% · maxmemory near"),
			("rabbitmq", "[green]running[/]",  "[bold]rabbitmq[/]  [green]● running[/]\nport 5672 · 7 queues\n412 msgs/s · 0 unacked"),
			("grafana",  "[green]running[/]",  "[bold]grafana[/]  [green]● running[/]\nport 3000 · 9 dashboards\nuptime 12d 4h"),
			("loki",     "[red]stopped[/]",    "[bold]loki[/]  [red]● stopped[/]\nport 3100 · exit code 1\nlast seen 3m ago — restarting"),
		};
		var services_list = Controls.List("Services")
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.OnSelectionChanged((sender, idx) =>
			{
				if (idx >= 0 && idx < services.Length)
					detail.SetContent(new List<string>(services[idx].detailText.Split('\n')));
			})
			.Build();
		foreach (var (name, status, _) in services)
			services_list.AddItem($"{status} {name}");
		services_list.SelectedIndex = 0;
		detail.SetContent(new List<string>(services[0].detailText.Split('\n')));

		// ── Detail tile (row 2, col 1) — a panel that the Services list updates live. ───────────────
		var detailPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Service Detail[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(detail)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Settings tile (row 2, col 2) — interactive controls that drive the gauge cell live. ────
		var thresholdGauge = Controls.ProgressBar()
			.WithHeader("Alert threshold")
			.ShowHeader()
			.WithValue(60).WithMaxValue(100)
			.ShowPercentage()
			.WithColorRole(ColorRole.Success)
			.WithMargin(1, 1, 1, 0)
			.Build();

		var thresholdSlider = Controls.Slider()
			.WithRange(0, 100).WithValue(60).WithStep(5)
			.ShowValueLabel()
			.Horizontal()
			.WithMargin(1, 0, 1, 0)
			.OnValueChanged((sender, v) =>
			{
				thresholdGauge.Value = v;
				// Re-tint the gauge by severity as the threshold moves.
				thresholdGauge.ColorRole = v >= 80 ? ColorRole.Danger
					: v >= 60 ? ColorRole.Warning
					: ColorRole.Success;
			})
			.Build();

		var modeDropdown = Controls.Dropdown()
			.WithPrompt("Mode: ")
			.AddItems("Balanced", "Performance", "Power-Saver", "Maintenance")
			.SelectedIndex(0)
			.WithMargin(1, 0, 1, 0)
			.Build();

		var autoScale = Controls.Checkbox("Auto-scale").Checked()
			.WithMargin(1, 0, 1, 0).Build();
		var notifyOps = Controls.Checkbox("Notify ops").Checked(false)
			.WithMargin(1, 0, 1, 1).Build();

		var settingsPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Controls[/]  [dim](Tab + ←/→/Space)[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(modeDropdown)
			.AddControl(thresholdSlider)
			.AddControl(thresholdGauge)
			.AddControl(autoScale)
			.AddControl(notifyOps)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Command tile (row 3, col 0) — an editable Prompt proves the cursor works in a cell. ────
		var commandPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Command[/]  [dim](Tab here, then type)[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.Prompt("hub> ").WithInputWidth(22).WithMargin(1, 1, 1, 1).Build())
			.AddControl(Controls.Markup("[dim]e.g. restart api-03[/]").WithMargin(1, 0, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Topology tile (row 3, col 1) — a focusable Tree proves another interactive control. ────
		var topology = Controls.Tree()
			.WithGuide(TreeGuide.Line)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1);
		var region = topology.AddRootNode("us-east-1");
		var az1 = region.AddChild("az-a");
		az1.AddChild("web-01");
		az1.AddChild("web-02");
		var az2 = region.AddChild("az-b");
		az2.AddChild("db-primary");
		az2.AddChild("db-replica");
		var topoTree = topology.Build();

		// ── Throughput tile (row 3, col 2) — a Sparkline for a compact live trend. Live-updated. ───
		var throughput = new List<double> { 3, 5, 4, 7, 6, 9, 8, 11, 7, 10, 12, 9, 13, 11, 8, 10, 14, 12 };
		var throughputSpark = Controls.Sparkline()
			.WithData(throughput.ToArray())
			.WithMargin(1, 1, 1, 0).Build();
		var throughputLabel = Controls.Markup("[green]▲ 12.4k[/]  [dim]peak 14.0k[/]").WithMargin(1, 1, 1, 1).Build();
		var throughputPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Throughput[/]  [dim]req/s[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(throughputSpark)
			.AddControl(throughputLabel)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Uptime tile (row 3, col 2) — a FIGlet-free compact info tile. ───────────────────────────
		var uptimePanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Uptime[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.Markup("[green]12d 4h 31m[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.Markup("[dim]SLA 99.98%[/]").WithMargin(1, 0, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Cluster health tile (row 3, col 3) — a small bar set. ───────────────────────────────────
		var healthPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Cluster[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.BarGraph().WithLabel("Up  ").WithValue(11).WithMaxValue(12)
				.WithColorRole(ColorRole.Success).ShowValue().WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.BarGraph().WithLabel("Warn").WithValue(2).WithMaxValue(12)
				.WithColorRole(ColorRole.Warning).ShowValue().WithMargin(1, 0, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Assemble the grid: 4 star columns, an Auto header row + 3 star body rows. ──────────────
		var grid = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithColorRole(ColorRole.Primary)
			.WithPadding(1, 0, 1, 0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Place(header, 0, 0, colSpan: 4)         // header spans all 4 columns
			.Place(cpuGraph, 1, 0)
			.Place(resourcePanel, 1, 1)
			.Place(settingsPanel, 1, 2)
			.Place(alertsLog, 1, 3, rowSpan: 2)      // alerts span two rows
			.Place(services_list, 2, 0)
			.Place(detailPanel, 2, 1)
			.Place(throughputPanel, 2, 2)
			.Place(commandPanel, 3, 0)
			.Place(topoTree, 3, 1)
			.Place(uptimePanel, 3, 2)
			.Place(healthPanel, 3, 3)
			.Build();

		// ── Per-cell styling through the GridCell surface — proves per-cell border + background. ───
		grid.Cell(1, 0).Border = BorderStyle.Rounded;        // frame the CPU tile
		grid.Cell(2, 0).Border = BorderStyle.Single;         // frame the services tile
		grid.Cell(1, 2).Border = BorderStyle.Rounded;        // frame the settings tile
		grid.Cell(1, 1).Background = new Color(40, 44, 60);   // subtle slate fill behind the resource tile
		grid.Cell(1, 3).Border = BorderStyle.Rounded;        // frame the spanning alerts tile
		grid.Cell(3, 1).Border = BorderStyle.Single;         // frame the topology tile

		// Live-update streams. A few rotating message pools so the Activity Log reads like a real feed.
		(string dot, string msg)[] feed =
		{
			("[green]●[/]", "web-{0:00} ok"), ("[yellow]●[/]", "api latency {0}ms"),
			("[green]●[/]", "db synced"), ("[red]●[/]", "cache miss {0}%"),
			("[green]●[/]", "queue drained"), ("[yellow]●[/]", "mem {0}%"),
			("[green]●[/]", "backup ok"), ("[green]●[/]", "scaled +1"),
			("[yellow]●[/]", "gc pause {0}ms"), ("[green]●[/]", "tls renew ok"),
		};

		var window = new WindowBuilder(ws)
			.WithTitle("Grid Layout")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(grid)
			// ── Real-time dashboard loop. This runs on the window's OWN thread, so it updates controls
			//    directly — no UI-thread marshalling, no manual invalidation: each control re-renders
			//    itself when its data/value changes (the reactive invalidation path). ─────────────────
			.WithAsyncWindowThread(async (win, ct) =>
			{
				// Deterministic pseudo-random walk (no Random — keeps the demo reproducible + AOT-trivial).
				long t = 0;
				double cpu = 41, mem = 73, disk = 48, swap = 12, net = 61;
				int seq = 46; // continue the log numbering after the initial entries
				double Wobble(double v, double lo, double hi, int phase)
				{
					// smooth-ish drift from a couple of sines + a small ramp, clamped to [lo,hi]
					double d = System.Math.Sin((t + phase) * 0.5) * 6 + System.Math.Sin((t + phase) * 0.17) * 4;
					return System.Math.Clamp(v + d * 0.5, lo, hi);
				}

				while (!ct.IsCancellationRequested)
				{
					await Task.Delay(600, ct);
					t++;

					// CPU line graph — push a new sample (old points roll off via MaxDataPoints).
					cpu = Wobble(cpu, 5, 98, 0);
					cpuGraph.AddDataPoint(cpu);

					// Resource bars drift independently.
					memBar.Value = mem = Wobble(mem, 20, 95, 11);
					diskBar.Value = disk = System.Math.Clamp(disk + System.Math.Sin(t * 0.07) * 1.5, 30, 90);
					swapBar.Value = swap = Wobble(swap, 2, 60, 23);
					netBar.Value = net = Wobble(net, 5, 99, 37);

					// Throughput sparkline — roll a new req/s value in, drop the oldest.
					double tp = System.Math.Clamp(10 + System.Math.Sin(t * 0.4) * 5 + System.Math.Sin(t * 0.11) * 3, 1, 20);
					throughput.Add(tp);
					if (throughput.Count > 24) throughput.RemoveAt(0);
					throughputSpark.SetDataPoints(throughput.ToArray());
					double peak = 0; foreach (var v in throughput) peak = System.Math.Max(peak, v);
					throughputLabel.SetContent(new List<string>
						{ $"[green]▲ {tp:0.0}k[/]  [dim]peak {peak:0.0}k[/]" });

					// Stream a new Activity Log line every other tick.
					if (t % 2 == 0)
					{
						int fi = (int)(t / 2) % feed.Length;
						var (dot, fmt) = feed[fi];
						int arg = (int)(20 + (t * 7) % 80);
						string line = string.Format(fmt, arg);
						alertsLog.AddControl(Controls.Markup($"[dim]{seq++,2}[/] {dot} {line}")
							.WithMargin(1, 0, 1, 0).Build());
					}
				}
			})
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();

		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}
}
