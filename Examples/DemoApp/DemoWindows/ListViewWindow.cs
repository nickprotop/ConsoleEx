using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

public static class ListViewWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var list = Controls.List("Programming Languages")
            .AddItem("C#", "[green]#[/]", Color.Green)
            .AddItem("Python", "[blue]Py[/]", Color.Blue)
            .AddItem("Rust", "[red]R[/]", Color.Red)
            .AddItem("TypeScript", "[cyan]TS[/]", Color.Cyan1)
            .AddItem("Go", "[aqua]Go[/]", Color.Aqua)
            .AddItem("Java", "[orange3]J[/]", Color.Orange3)
            .AddItem("Kotlin", "[magenta]K[/]", Color.Magenta1)
            .AddItem("Swift", "[yellow]S[/]", Color.Yellow)
            .AddItem("Haskell", "[purple]H[/]", Color.Purple)
            .AddItem("Zig", "[gold1]Z[/]", Color.Gold1)
            .AddItem("Elixir", "[mediumpurple]E[/]", Color.MediumPurple)
            .AddItem("Lua", "[blue]L[/]", Color.Blue)
            .AddItem("Ruby", "[red]Rb[/]", Color.Red)
            .AddItem("Scala", "[red]Sc[/]", Color.Red)
            .AddItem("Clojure", "[green]Cl[/]", Color.Green)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .OnItemActivated((sender, item) =>
            {
                ws.NotificationStateService
                    .ShowNotification("Selected", $"You picked: {item.Text}",
                        SharpConsoleUI.Core.NotificationSeverity.Info);
            })
            .Build();

        var statusBar = Controls.Markup("[dim]Enter: Select | Up/Down: Navigate | Esc: Close[/]")
            .StickyBottom()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("List View Demo")
            .WithSize(50, 25)
            .Centered()
            .AddControls(list, statusBar)
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
