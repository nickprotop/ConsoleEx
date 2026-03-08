using SharpConsoleUI;
using SharpConsoleUI.Builders;

namespace DemoApp.DemoWindows;

internal static class WelcomeWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var banner = Controls.Figlet("SharpConsoleUI")
            .Centered()
            .WithColor(Color.Cyan)
            .WithWrapMode(SharpConsoleUI.Controls.WrapMode.WrapWords)
            .Build();

        var info = Controls.Markup()
            .AddEmptyLine()
            .AddLine("[bold yellow]SharpConsoleUI[/] [dim]v2.0[/]")
            .AddEmptyLine()
            .AddLine("A modern .NET 9.0 console windowing system")
            .AddLine("with fluent builders, async patterns, and")
            .AddLine("a rich set of built-in controls.")
            .AddEmptyLine()
            .AddLine("[dim]Press [bold]ESC[/] to close this window[/]")
            .Centered()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Welcome")
            .WithSize(70, 15)
            .Centered()
            .AddControl(banner)
            .AddControl(info)
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
