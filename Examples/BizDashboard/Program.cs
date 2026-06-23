// -----------------------------------------------------------------------
// BizDashboard — a "Business Analytics + Point of Sale" example for
// SharpConsoleUI.
//
// A full-screen NavigationView shell whose every page is a Fill GridControl
// set as the page's DIRECT root (via nav.SetItemContent). A deterministic
// background walk (Simulation) animates ONLY the Sales KPIs + chart on the
// window's own thread. The Point of Sale page is the interactive flagship —
// click products to ring up a sale, type tender on the keypad to see change.
//
// Run:  dotnet run --project Examples/BizDashboard
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace BizDashboard;

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
			new Color(10, 14, 26),
			new Color(22, 28, 52),
			new Color(14, 18, 38));

		// Shared live-control references the simulation mutates each tick.
		var refs = new BizRefs();

		// Build the page grids up front so we can hand them to the nav AND the simulation.
		var sales = Pages.BuildSalesGrid(refs);
		var customers = Pages.BuildCustomersGrid();
		var inventory = Pages.BuildInventoryGrid();
		var reports = Pages.BuildReportsGrid();
		var pos = Pos.BuildPosGrid();

		var nav = Controls.NavigationView()
			.WithNavWidth(28)
			.WithPaneHeader("[bold rgb(120,200,255)]  ◆  BizDashboard[/]")
			.WithSelectedColors(Color.White, new Color(40, 90, 170))
			.WithContentBorder(BorderStyle.Rounded)
			.WithContentBorderColor(new Color(60, 80, 120))
			.WithContentBackground(new Color(16, 22, 42))
			.WithContentPadding(0, 0, 0, 0)
			.WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
			.WithExpandedThreshold(80)
			.WithCompactThreshold(50)
			.AddHeader("Analytics", new Color(100, 190, 255), h =>
			{
				h.AddItem("Sales", sales, icon: "▮", subtitle: "revenue & orders");
				h.AddItem("Customers", customers, icon: "☻", subtitle: "accounts & MRR");
				h.AddItem("Inventory", inventory, icon: "▤", subtitle: "stock levels");
				h.AddItem("Reports", reports, icon: "¶", subtitle: "executive summary");
			})
			.AddHeader("Operations", new Color(180, 220, 140), h =>
			{
				h.AddItem("Point of Sale", pos, icon: "▦", subtitle: "ring up a sale");
			})
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Fill()
			.Build();

		Window? window = null;
		window = new WindowBuilder(ws)
			.WithTitle("BizDashboard")
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
