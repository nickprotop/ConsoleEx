using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;

namespace DemoApp.DemoWindows;

/// <summary>
/// Demonstrates opt-in text selection on MarkupControl (issue #36): drag-select with the mouse,
/// copy with Ctrl+C as plain text (markup stripped). Two selectable markup blocks plus a multiline
/// editor share a single window selection — selecting in one clears the others.
/// </summary>
internal static class SelectableTextDemoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var consoleOutput = Controls.Markup()
            .AddLine("[bold cyan]$ build --release[/]")
            .AddLine("[grey]Restoring packages...[/] [green]done[/]")
            .AddLine("[bold green]SUCCESS:[/] Built [cyan]v2.1.0[/] in [yellow]3.4s[/]")
            .AddLine("[bold red]ERROR:[/] 0   [bold yellow]WARN:[/] 2")
            .WithSelectionEnabled()
            .WithSelectionColors(Color.Black, new Color(95, 175, 255))
            .Build();

        var paragraph = Controls.Markup()
            .AddLine("[bold]Drag to select[/] any of this [underline]markup[/] text with your mouse,")
            .AddLine("then press [bold cyan]Ctrl+C[/] to copy it. The copied text is [italic]plain[/]")
            .AddLine("— all [red]markup[/] [green]tags[/] are stripped automatically.")
            .WithSelectionEnabled()
            .Build();

        var editor = new MultilineEditControl
        {
            Content = "The editor shares the same selection.\nSelect here and the blocks above clear.",
            ReadOnly = true,
            Height = 4
        };

        var content = Controls.ScrollablePanel()
            .AddControl(Controls.Markup("[bold underline cyan]Selectable Text (issue #36)[/]").Centered().Build())
            .AddControl(Controls.Rule("Console output — selectable & copyable"))
            .AddControl(consoleOutput)
            .AddControl(Controls.Rule("Explanation"))
            .AddControl(paragraph)
            .AddControl(Controls.Rule("Read-only editor (shares the selection)"))
            .AddControl(editor)
            .AddControl(Controls.Markup("[dim]Tip: left-click empty space to clear the selection. Right-click is surfaced to the app.[/]").Build())
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Selectable Text")
            .WithSize(80, 28)
            .Centered()
            .AddControl(content)
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
