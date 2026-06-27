// -----------------------------------------------------------------------
// GridGallery — a "Control Gallery" example for SharpConsoleUI.
//
// A full-screen NavigationView shell whose every page is a GridControl set
// as the page's DIRECT root (via nav.SetItemContent) so each grid FILLS the
// content area. Each page lays out LIVE instances of the library's controls
// as captioned, bordered tiles. The Charts page is animated by a single
// deterministic background walk (Simulation) on the window's own thread.
//
// Run:  dotnet run --project Examples/GridGallery
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace GridGallery;

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

		// Live charts references the simulation animates each tick.
		var refs = new GalleryRefs();

		// Build the page grids up front so we can hand them to the nav AND the simulation.
		var charts = Pages.BuildChartsGrid(refs);
		var inputs = Pages.BuildInputsGrid();
		var data = Pages.BuildDataGrid();
		var text = Pages.BuildTextGrid();
		var more = Pages.BuildMoreGrid();

		var nav = Controls.NavigationView()
			.WithNavWidth(26)
			.WithPaneHeader("[bold rgb(120,180,255)]  ◆  Control Gallery[/]")
			.WithSelectedColors(Color.White, new Color(40, 80, 160))
			.WithContentBorder(BorderStyle.Rounded)
			.WithContentBorderColor(new Color(60, 80, 120))
			.WithContentBackground(new Color(16, 22, 42))
			.WithContentPadding(0, 0, 0, 0)
			.WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
			.WithExpandedThreshold(80)
			.WithCompactThreshold(50)
			.AddHeader("Controls", new Color(100, 180, 255), h =>
			{
				h.AddItem("Charts", charts, icon: "▮", subtitle: "live data viz");
				h.AddItem("Inputs", inputs, icon: "▣", subtitle: "interactive");
				h.AddItem("Data", data, icon: "▤", subtitle: "tables · lists · trees");
				h.AddItem("Text & Markup", text, icon: "¶", subtitle: "rich text");
				h.AddItem("More", more, icon: "▦", subtitle: "sliders · dates · edit");
			})
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Fill()
			.Build();

		// A single content-toolbar button that toggles the gridlines on the
		// Charts page grid — demonstrates the public ShowColumn/RowGridlines API.
		nav.AddContentToolbarButton("Toggle Gridlines", (sender, btn) =>
		{
			charts.ShowColumnGridlines = !charts.ShowColumnGridlines;
			charts.ShowRowGridlines = !charts.ShowRowGridlines;
		});

		Window? window = null;
		window = new WindowBuilder(ws)
			.WithTitle("Control Gallery")
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
