using SharpConsoleUI;
using SharpConsoleUI.Builders;

namespace DemoApp.DemoWindows;

public static class TerminalWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        // Experimental: window chrome + terminal default bg both non-opaque so
        // the host terminal's background (and its transparency, if any) shows
        // through. Requires ConsoleWindowSystemOptions.TerminalTransparencyMode
        // = PreserveTerminalTransparency (set in Program.cs).
        return new WindowBuilder(ws)
            .WithTitle("Terminal")
            .WithSize(100, 30)
            .Centered()
            .WithBackgroundColor(new Color(30, 30, 60, 180))
            .AddControl(Controls.Terminal()
                .WithBackgroundColor(Color.Transparent)
                .Build())
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
