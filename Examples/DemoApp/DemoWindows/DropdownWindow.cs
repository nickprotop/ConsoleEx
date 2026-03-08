using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

internal static class DropdownWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var header = Controls.Markup()
            .AddLine("[bold yellow]Dropdown Controls[/]")
            .AddEmptyLine()
            .WithMargin(1, 0, 1, 0)
            .Build();

        var statusDisplay = Controls.Markup()
            .AddLine("[dim]Select an item from any dropdown above[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        var countries = Controls.Dropdown("Select Country")
            .AddItems("United States", "United Kingdom", "Germany",
                      "France", "Japan", "Australia", "Canada",
                      "Brazil", "India", "South Korea")
            .WithMargin(1, 0, 1, 1)
            .OnSelectedValueChanged((s, value) =>
            {
                statusDisplay.SetContent(new List<string> { $"[green]Country:[/] [bold]{value}[/]" });
            })
            .Build();

        var colors = Controls.Dropdown("Favorite Color")
            .AddItem("Red", color: Color.Red)
            .AddItem("Green", color: Color.Green)
            .AddItem("Blue", color: Color.Blue)
            .AddItem("Yellow", color: Color.Yellow)
            .AddItem("Cyan", color: Color.Cyan)
            .AddItem("Magenta", color: Color.Magenta)
            .WithMargin(1, 0, 1, 1)
            .OnSelectedValueChanged((s, value) =>
            {
                statusDisplay.SetContent(new List<string> { $"[green]Color:[/] [bold]{value}[/]" });
            })
            .Build();

        var footer = Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Press [bold]ESC[/] to close[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Dropdowns")
            .WithSize(50, 20)
            .Centered()
            .AddControl(header)
            .AddControl(countries)
            .AddControl(colors)
            .AddControl(statusDisplay)
            .AddControl(footer)
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
