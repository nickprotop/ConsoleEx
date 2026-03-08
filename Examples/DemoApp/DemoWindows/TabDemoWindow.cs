using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

public static class TabDemoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var overviewContent = Controls.Markup("[bold yellow]Tab Control Demo[/]")
            .AddLine("")
            .AddLine("This window demonstrates the [blue]TabControl[/] with multiple tabs.")
            .AddLine("")
            .AddLine("[dim]Use [green]Tab[/] or click headers to switch between tabs.[/]")
            .AddLine("[dim]Each tab can contain any control.[/]")
            .Build();

        var tableContent = Controls.Table()
            .WithTitle("Sample Data")
            .WithColumns("Language", "Paradigm", "Year", "Typing")
            .AddRow("C#", "Multi-paradigm", "2000", "Static")
            .AddRow("Python", "Multi-paradigm", "1991", "Dynamic")
            .AddRow("Rust", "Multi-paradigm", "2010", "Static")
            .AddRow("Haskell", "Functional", "1990", "Static")
            .AddRow("Elixir", "Functional", "2011", "Dynamic")
            .Rounded()
            .Build();

        var settingsContent = Controls.ScrollablePanel()
            .AddControl(Controls.Markup("[bold]Settings[/]").AddLine("").Build())
            .AddControl(Controls.Checkbox("Enable dark mode").Checked().Build())
            .AddControl(Controls.Checkbox("Show line numbers").Checked().Build())
            .AddControl(Controls.Checkbox("Auto-save").Build())
            .AddControl(Controls.Checkbox("Word wrap").Checked().Build())
            .AddControl(Controls.Separator())
            .AddControl(Controls.Markup("").AddLine("[bold]Theme[/]").Build())
            .AddControl(Controls.Dropdown("Select theme")
                .AddItem("Default")
                .AddItem("Dark")
                .AddItem("Light")
                .AddItem("Solarized")
                .Build())
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        var tabs = Controls.TabControl()
            .AddTab("Overview", overviewContent)
            .AddTab("Table", tableContent)
            .AddTab("Settings", settingsContent)
            .Fill()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Tab Demo")
            .WithSize(70, 25)
            .Centered()
            .AddControl(tabs)
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
