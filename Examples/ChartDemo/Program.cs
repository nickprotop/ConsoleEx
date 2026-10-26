// -----------------------------------------------------------------------
// ChartDemo - a chart driven from outside.
//
// Run it normally to look at it, or with --headless --socket to drive it
// from a shell with no terminal involved:
//
//   dotnet run --project Examples/ChartDemo -- --headless --socket /tmp/chart.sock
//   echo '{"cmd":"snapshot"}' | socat - UNIX-CONNECT:/tmp/chart.sock | tail -1
//
// The image verb renders with an embedded font that has no braille glyphs, so
// a braille-mode chart photographs blank while being perfectly visible in the
// terminal and in snapshot. Pass a font that has them when capturing one:
//
//   {"cmd":"image","format":"png","font":"/usr/share/fonts/truetype/freefont/FreeMono.ttf"}
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace ChartDemo;

internal static class Program
{
	private static int Main(string[] args)
	{
		// Requested in code, so the socket opens without a consent prompt.
		string? socket = Flag(args, "--socket") is { } path
			? (path.Length > 0 ? path : Path.Combine(Path.GetTempPath(), "chart.sock"))
			: null;
		bool headless = args.Contains("--headless");

		try
		{
			IConsoleDriver driver = headless
				? new HeadlessConsoleDriver(Size(args, "--width", 100), Size(args, "--height", 34))
				: new NetConsoleDriver(RenderMode.Buffer);

			var ws = new ConsoleWindowSystem(
				driver,
				options: new ConsoleWindowSystemOptions(
					ShowTopPanel: false,
					ShowBottomPanel: false,
					DiagnosticsSocket: socket));

			Console.CancelKeyPress += (_, e) =>
			{
				e.Cancel = true;
				ws.Shutdown(0);
			};

			BuildWindow(ws);
			return ws.Run();
		}
		catch (Exception ex)
		{
			// Before the window system starts there is no UI to report into, and
			// after it starts this is unreachable - so stderr is safe here only.
			Console.Error.WriteLine(ex);
			return 1;
		}
	}

	private static void BuildWindow(ConsoleWindowSystem ws)
	{
		var window = new Window(ws)
		{
			Title = "ChartControl",
			Left = 1,
			Top = 1,
			Width = 96,
			Height = 30,
		};

		var chart = new ChartControl
		{
			// Named so the diagnostics socket can focus it by name.
			Name = "chart",
			Title = "Requests and errors",
			PlotType = ChartPlotType.Line,
			RenderMode = ChartRenderMode.Pixel,
			VerticalAlignment = VerticalAlignment.Fill,
		};

		chart.YAxis.Min = 0;
		chart.YAxis.Max = 100;

		// Two series that track each other closely, which is where a single
		// foreground colour per cell would lose one of them.
		// Interactive: hover reports the nearest point. Off unless asked for.
		chart.EnableHover = true;
		chart.EnableSelection = true;
		chart.EnableZoom = true;
		chart.ShowLegend = true;
		chart.ShowAxisLines = true;
		chart.YAxis.ShowGridlines = true;
		chart.YAxis.Min = 0;
		chart.YAxis.Max = 100;

		chart.AddSeries("requests", Color.Green).SetValues(
			new[] { 20d, 55, 40, 85, 60, 90, 45, 70, 35, 65, 80, 50 });
		chart.AddSeries("errors", Color.Red).SetValues(
			new[] { 5d, 12, 8, 30, 15, 22, 10, 18, 7, 14, 25, 11 });

		window.AddControl(chart);
		ws.AddWindow(window);
	}

	/// <summary>Reads "--flag value", returning an empty string for a bare flag.</summary>
	private static string? Flag(string[] args, string name)
	{
		int i = Array.IndexOf(args, name);
		if (i < 0) return null;
		return i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
			? args[i + 1]
			: string.Empty;
	}

	private static int Size(string[] args, string name, int fallback) =>
		Flag(args, name) is { Length: > 0 } v && int.TryParse(v, out int n) ? n : fallback;
}
