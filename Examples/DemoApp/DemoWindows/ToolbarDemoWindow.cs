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
            .WithBelowLine()
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
            .WithAboveLine()
            .WithBelowLine()
            .WithContentPadding(1, 0, 1, 0)
            .Build();

        // Mixed toolbar: bordered button + plain controls with vertical centering
        var mixedToolbar = Controls.Toolbar()
            .WithSpacing(1)
            .WithBackgroundColor(Color.Grey19)
            .WithAboveLine()
            .WithBelowLine()
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

        // Wrapping toolbar — many buttons that overflow to second row
        var label4 = Controls.Markup()
            .AddLine("[dim]Wrapping toolbar (click second row buttons):[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        var wrapToolbar = Controls.Toolbar()
            .AddButton("File", (_, btn) => log.SetContent(new List<string> { "[magenta]File[/] clicked" }))
            .AddButton("Edit", (_, btn) => log.SetContent(new List<string> { "[magenta]Edit[/] clicked" }))
            .AddButton("View", (_, btn) => log.SetContent(new List<string> { "[magenta]View[/] clicked" }))
            .AddButton("Build", (_, btn) => log.SetContent(new List<string> { "[magenta]Build[/] clicked" }))
            .AddButton("Debug", (_, btn) => log.SetContent(new List<string> { "[magenta]Debug[/] clicked" }))
            .AddButton("Tools", (_, btn) => log.SetContent(new List<string> { "[magenta]Tools[/] clicked" }))
            .AddButton("Window", (_, btn) => log.SetContent(new List<string> { "[magenta]Window[/] clicked" }))
            .AddButton("Help", (_, btn) => log.SetContent(new List<string> { "[magenta]Help[/] clicked" }))
            .AddButton("Terminal", (_, btn) => log.SetContent(new List<string> { "[magenta]Terminal[/] clicked" }))
            .AddButton("Extensions", (_, btn) => log.SetContent(new List<string> { "[magenta]Extensions[/] clicked" }))
            .AddButton("Settings", (_, btn) => log.SetContent(new List<string> { "[magenta]Settings[/] clicked" }))
            .AddButton("About", (_, btn) => log.SetContent(new List<string> { "[magenta]About[/] clicked" }))
            .WithSpacing(1)
            .WithBackgroundColor(Color.Grey11)
            .WithAboveLine()
            .WithBelowLine()
            .WithWrap()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Toolbar Demo")
            .WithSize(60, 30)
            .Centered()
            .AddControl(basicToolbar)
            .AddControl(header)
            .AddControl(label2)
            .AddControl(borderedToolbar)
            .AddControl(label3)
            .AddControl(mixedToolbar)
            .AddControl(label4)
            .AddControl(wrapToolbar)
            .AddControl(log)
            .BuildAndShow();
    }
}
