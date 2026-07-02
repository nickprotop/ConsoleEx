using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;

namespace DemoApp.DemoWindows;

internal static class LogViewerWindow
{
	public static Window Create(ConsoleWindowSystem ws)
	{
		var logViewer = new LogViewerControl(ws.LogService)
		{
			Title = "Library Logs",
			Name = "logViewer"
		};

		// Capture everything and seed a few sample entries so the viewer isn't empty on open.
		ws.LogService.MinimumLevel = LogLevel.Trace;
		ws.LogService.LogInfo("Log Viewer opened", "DemoApp");
		ws.LogService.LogWarning("This is a sample warning", "DemoApp");
		ws.LogService.LogError("This is a sample error", null, "DemoApp");

		return new WindowBuilder(ws)
			.WithTitle("Log Viewer")
			.WithSize(80, 25)
			.Centered()
			.AddControl(logViewer)
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
}
