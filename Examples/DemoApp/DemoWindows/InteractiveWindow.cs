using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

internal static class InteractiveWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var display = Controls.Markup()
            .AddLine("[bold yellow]Key Press Inspector[/]")
            .AddEmptyLine()
            .AddLine("Press any key to see its details...")
            .AddEmptyLine()
            .AddLine("[dim]Key:[/]        -")
            .AddLine("[dim]Char:[/]       -")
            .AddLine("[dim]Modifiers:[/]  -")
            .AddLine("[dim]ConsoleKey:[/] -")
            .AddEmptyLine()
            .AddLine("[dim]Press [bold]ESC[/] to close[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Key Inspector")
            .WithSize(50, 15)
            .Centered()
            .AddControl(display)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)s!);
                    e.Handled = true;
                    return;
                }

                var key = e.KeyInfo;
                var modifiers = key.Modifiers == 0 ? "None" : key.Modifiers.ToString();
                var charDisplay = key.KeyChar == '\0' ? "(none)" : $"'{key.KeyChar}' (0x{(int)key.KeyChar:X2})";

                display.SetContent(new List<string>
                {
                    "[bold yellow]Key Press Inspector[/]",
                    "",
                    "[green]Key detected![/]",
                    "",
                    $"[dim]Key:[/]        [bold]{key.Key}[/]",
                    $"[dim]Char:[/]       [bold]{charDisplay}[/]",
                    $"[dim]Modifiers:[/]  [bold]{modifiers}[/]",
                    $"[dim]ConsoleKey:[/] [bold]{(int)key.Key}[/]",
                    "",
                    "[dim]Press [bold]ESC[/] to close[/]"
                });
                e.Handled = true;
            })
            .BuildAndShow();
    }
}
