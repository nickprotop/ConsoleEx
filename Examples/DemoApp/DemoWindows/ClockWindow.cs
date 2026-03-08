using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

internal static class ClockWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var clock = Controls.Figlet(DateTime.Now.ToString("HH:mm:ss"))
            .Centered()
            .WithColor(Color.Green)
            .Build();

        var label = Controls.Markup()
            .AddLine("[dim]Live clock - updates every second[/]")
            .AddLine("[dim]Press [bold]ESC[/] to close[/]")
            .Centered()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Clock")
            .WithSize(50, 12)
            .Centered()
            .AddControl(clock)
            .AddControl(label)
            .WithAsyncWindowThread(async (window, ct) =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(1000, ct);
                    clock.Text = DateTime.Now.ToString("HH:mm:ss");
                    window.Invalidate(true);
                }
            })
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
