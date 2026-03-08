using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

public static class TableDemoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var table = Controls.Table()
            .WithTitle("Team Members")
            .AddColumn("Name")
            .AddColumn("Role")
            .AddColumn("Status")
            .AddColumn("Score", SharpConsoleUI.Layout.TextJustification.Right)
            .AddRow("Alice Chen", "Lead Engineer", "[green]Active[/]", "98")
            .AddRow("Bob Martinez", "Backend Dev", "[green]Active[/]", "92")
            .AddRow("Carol Wang", "Frontend Dev", "[yellow]Away[/]", "87")
            .AddRow("Dave Kumar", "DevOps", "[green]Active[/]", "95")
            .AddRow("Eve Johnson", "QA Engineer", "[red]Offline[/]", "91")
            .AddRow("Frank Lee", "Data Scientist", "[green]Active[/]", "89")
            .AddRow("Grace Park", "UX Designer", "[yellow]Away[/]", "94")
            .AddRow("Hank Brown", "Intern", "[green]Active[/]", "76")
            .Rounded()
            .ShowRowSeparators()
            .WithHeaderColors(Color.White, Color.DarkBlue)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        var statusBar = Controls.Markup("[dim]Esc: Close | Rounded border style with row separators[/]")
            .StickyBottom()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Table Demo")
            .WithSize(70, 22)
            .Centered()
            .AddControls(table, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
