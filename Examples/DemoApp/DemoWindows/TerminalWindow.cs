using SharpConsoleUI;
using SharpConsoleUI.Builders;

namespace DemoApp.DemoWindows;

public static class TerminalWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        return new WindowBuilder(ws)
            .WithTitle("Terminal")
            .WithSize(100, 30)
            .Centered()
            .AddControl(Controls.Terminal().Build())
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
