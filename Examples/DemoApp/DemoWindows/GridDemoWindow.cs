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
/// the window with a header that spans all three columns (col-span), a 3x2 tile field below it with a
/// different control type in every cell (line graph, bar graphs, list, scrollable log, editable
/// prompt, info markup), a tile that spans two rows (row-span), per-cell border and background
/// styling applied through the <see cref="GridCell"/> surface, and a theme-derived colour role so the
/// cell chrome re-tints when the toolbar theme changes. Tab walks the focusable cells row-major.
/// </summary>
public static class GridDemoWindow
{
	private const int WindowWidth = 90;
	private const int WindowHeight = 30;

	public static Window Create(ConsoleWindowSystem ws)
	{
		// ── Header tile (row 0) — spans all three columns to prove col-span. ───────────────────────
		var header = Controls.Markup(
				"[bold]System Dashboard[/]   [dim]ServerHub · 3-column grid · spans · gaps · per-cell styling[/]")
			.WithMargin(1, 0, 1, 0)
			.Build();

		// ── CPU trend tile (row 1, col 0) — a LineGraph proves "any control in a cell". ────────────
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

		// ── Memory / disk tile (row 1, col 1) — stacked BarGraphs in a scroll-free panel. ──────────
		var resourcePanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Resources[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.BarGraph().WithLabel("Mem ").WithValue(73).WithMaxValue(100)
				.WithColorRole(ColorRole.Warning).ShowValue().WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.BarGraph().WithLabel("Disk").WithValue(48).WithMaxValue(100)
				.WithColorRole(ColorRole.Success).ShowValue().WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.BarGraph().WithLabel("Swap").WithValue(12).WithMaxValue(100)
				.WithColorRole(ColorRole.Info).ShowValue().WithMargin(1, 0, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Alerts tile (row 1, col 2) — spans rows 1 AND 2 to prove row-span. A scrollable log of
		//    long content proves compose-to-scroll inside a single cell. ────────────────────────────
		var alertsLog = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		alertsLog.AddControl(Controls.Markup("[bold]Activity Log[/]  [dim](scrolls)[/]").WithMargin(1, 1, 1, 0).Build());
		string[] logLines =
		{
			"[green]OK[/]   web-01 healthy",
			"[green]OK[/]   web-02 healthy",
			"[yellow]WARN[/] api-03 latency 412ms",
			"[green]OK[/]   db-01 replica synced",
			"[red]ERR[/]  cache-02 timeout",
			"[green]OK[/]   web-01 healthy",
			"[yellow]WARN[/] disk 73% on db-01",
			"[green]OK[/]   queue drained",
			"[green]OK[/]   web-03 deployed v2.4",
			"[yellow]WARN[/] api-03 retry x2",
			"[green]OK[/]   tls cert renewed",
			"[red]ERR[/]  cache-02 evicted",
			"[green]OK[/]   backup completed",
			"[green]OK[/]   web-02 scaled +1",
			"[yellow]WARN[/] mem 73% on web-01",
			"[green]OK[/]   health sweep done",
		};
		foreach (var line in logLines)
			alertsLog.AddControl(Controls.Markup(line).WithMargin(1, 0, 1, 0).Build());

		// ── Services tile (row 2, col 0) — a List proves a focusable control in a cell. ────────────
		var services = Controls.List("Services")
			.AddItems("nginx", "postgres", "redis", "rabbitmq", "grafana", "loki")
			.WithMargin(1, 1, 1, 1)
			.Build();
		services.SelectedIndex = 0;

		// ── Command tile (row 2, col 1) — an editable Prompt proves the cursor works in a cell. ────
		var commandPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold]Command[/]  [dim](Tab here, then type)[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(Controls.Prompt("hub> ").WithInputWidth(20).WithMargin(1, 1, 1, 1).Build())
			.AddControl(Controls.Markup("[dim]e.g. restart api-03[/]").WithMargin(1, 0, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// ── Assemble the grid: 3 star columns, an Auto header row + 2 star body rows. ──────────────
		var grid = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Star(1), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithColorRole(ColorRole.Primary)
			.WithPadding(1, 0, 1, 0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Place(header, 0, 0, colSpan: 3)        // header spans all 3 columns
			.Place(cpuGraph, 1, 0)
			.Place(resourcePanel, 1, 1)
			.Place(alertsLog, 1, 2, rowSpan: 2)     // alerts span two rows
			.Place(services, 2, 0)
			.Place(commandPanel, 2, 1)
			.Build();

		// ── Per-cell styling through the GridCell surface — proves per-cell border + background. ───
		grid.Cell(1, 0).Border = BorderStyle.Rounded;   // frame the CPU tile
		grid.Cell(2, 0).Border = BorderStyle.Single;    // frame the services tile
		grid.Cell(1, 1).Background = new Color(40, 44, 60);  // subtle slate fill behind the resource tile
		grid.Cell(1, 2).Border = BorderStyle.Rounded;   // frame the spanning alerts tile

		var window = new WindowBuilder(ws)
			.WithTitle("Grid Layout")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(grid)
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
