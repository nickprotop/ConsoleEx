using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class ToolbarDemoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var log = Controls.Markup()
            .AddLine("[dim]Click toolbar buttons to see actions here...[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        // Basic toolbar with plain buttons (sticky top)
        var basicToolbar = Controls.Toolbar()
            .AddButton("New", (_, btn) => log.SetContent(new List<string> { "[green]New[/] clicked" }))
            .AddButton("Open", (_, btn) => log.SetContent(new List<string> { "[green]Open[/] clicked" }))
            .AddButton("Save", (_, btn) => log.SetContent(new List<string> { "[green]Save[/] clicked" }))
            .WithSpacing(1)
            .WithBackgroundColor(Color.Grey11)
            .StickyTop()
            .Build();

        // Toolbar with bordered buttons (multi-height, auto-sized to 3 rows)
        var borderedToolbar = Controls.Toolbar()
            .AddButton(Controls.Button()
                .WithText("  Compile  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .OnClick((_, btn) => log.SetContent(new List<string> { "[cyan]Compile[/] triggered" })))
            .AddButton(Controls.Button()
                .WithText("  Run  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .OnClick((_, btn) => log.SetContent(new List<string> { "[cyan]Run[/] triggered" })))
            .AddButton(Controls.Button()
                .WithText("  Debug  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .OnClick((_, btn) => log.SetContent(new List<string> { "[cyan]Debug[/] triggered" })))
            .WithSpacing(1)
            .WithBackgroundColor(Color.Grey15)
            .Build();

        // Mixed toolbar: bordered button + plain controls with vertical centering
        var mixedToolbar = Controls.Toolbar()
            .WithSpacing(1)
            .WithBackgroundColor(Color.Grey19)
            .Build();

        var mixedBorderedBtn = Controls.Button()
            .WithText("  Apply  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .OnClick((_, btn) => log.SetContent(new List<string> { "[yellow]Apply[/] clicked" }))
            .Build();

        var mixedLabel = new MarkupControl(new List<string> { "[dim]Ready[/]" })
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var mixedPlainBtn = Controls.Button()
            .WithText("Cancel")
            .OnClick((_, btn) => log.SetContent(new List<string> { "[yellow]Cancel[/] clicked" }))
            .Build();
        mixedPlainBtn.VerticalAlignment = VerticalAlignment.Center;

        mixedToolbar.AddItem(mixedBorderedBtn);
        mixedToolbar.AddItem(mixedLabel);
        mixedToolbar.AddItem(mixedPlainBtn);

        var header = Controls.Markup()
            .AddLine("[bold cyan]Toolbar Demo[/]")
            .AddLine("")
            .AddLine("Demonstrates ToolbarControl with auto-height support.")
            .AddLine("Toolbars automatically size to fit their tallest item.")
            .WithMargin(1, 1, 1, 0)
            .Build();

        var label2 = Controls.Markup()
            .AddLine("[dim]Bordered buttons (auto-height = 3 rows):[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        var label3 = Controls.Markup()
            .AddLine("[dim]Mixed: bordered + plain with vertical centering:[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Toolbar Demo")
            .WithSize(80, 30)
            .Centered()
            .AddControl(basicToolbar)
            .AddControl(header)
            .AddControl(label2)
            .AddControl(borderedToolbar)
            .AddControl(label3)
            .AddControl(mixedToolbar)
            .AddControl(log)
            .BuildAndShow();
    }
}
