// -----------------------------------------------------------------------
// GridDashboard — a "Mission Control" example for SharpConsoleUI.
//
// A full-screen NavigationView shell whose every page is a GridControl set
// as the page's DIRECT root (via nav.SetItemContent) so each grid FILLS the
// content area. A single deterministic background walk (Simulation) feeds
// live data into the controls on the window's own thread.
//
// Run:  dotnet run --project Examples/GridDashboard
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace GridDashboard;

internal class Program
{
	static int Main(string[] args)
	{
		if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

		try
		{
			var ws = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

			ws.PanelStateService.ShowTopPanel = false;
			ws.PanelStateService.ShowBottomPanel = false;

			CreateMainWindow(ws);

			ws.Run();
			return 0;
		}
		catch (Exception ex)
		{
			Console.Clear();
			ExceptionFormatter.WriteException(ex);
			return 1;
		}
	}

	private static void CreateMainWindow(ConsoleWindowSystem ws)
	{
		var gradient = ColorGradient.FromColors(
			new Color(8, 12, 30),
			new Color(20, 30, 60),
			new Color(12, 18, 42));

		// Shared live-control references the simulation mutates each tick.
		var refs = new DashboardRefs();

		// Build the page grids up front so we can hand them to the nav AND the simulation.
		var overview = Pages.BuildOverviewGrid(refs);
		var processes = Pages.BuildProcessesGrid(refs);
		var network = Pages.BuildNetworkGrid(refs);
		var logs = Pages.BuildLogsGrid(refs);
		var settings = Pages.BuildSettingsGrid();

		var nav = Controls.NavigationView()
			.WithNavWidth(26)
			.WithPaneHeader("[bold rgb(120,180,255)]  ◆  Mission Control[/]")
			.WithSelectedColors(Color.White, new Color(40, 80, 160))
			.WithContentBorder(BorderStyle.Rounded)
			.WithContentBorderColor(new Color(60, 80, 120))
			.WithContentBackground(new Color(16, 22, 42))
			.WithContentPadding(0, 0, 0, 0)
			.WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
			.WithExpandedThreshold(80)
			.WithCompactThreshold(50)
			.AddHeader("Monitoring", new Color(100, 180, 255), h =>
			{
				h.AddItem("Overview", overview, icon: "◈", subtitle: "system status");
				h.AddItem("Processes", processes, icon: "≡", subtitle: "top consumers");
				h.AddItem("Network", network, icon: "▥", subtitle: "in / out throughput");
				h.AddItem("Logs", logs, icon: "▤", subtitle: "live event stream");
			})
			.AddHeader("System", new Color(180, 140, 220), h =>
			{
				h.AddItem("Settings", settings, icon: "⚙", subtitle: "preferences");
			})
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Fill()
			.Build();

		Window? window = null;
		window = new WindowBuilder(ws)
			.WithTitle("Mission Control")
			.Maximized()
			.WithBackgroundGradient(gradient, GradientDirection.DiagonalDown)
			.WithColors(Color.White, Color.Black)
			.WithBorderStyle(BorderStyle.None)
			.HideTitle()
			.HideTitleButtons()
			.Movable(false)
			.Resizable(false)
			.AddControl(nav)
			// The walk runs on the window's OWN thread, so it mutates controls
			// directly (UI-thread safe) — no marshalling, no manual invalidation.
			.WithAsyncWindowThread(async (win, ct) => await Simulation.Run(refs, ct))
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow(window!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
	}
}
