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
