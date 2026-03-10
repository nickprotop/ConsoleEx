using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class MarkupSyntaxWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var content = Controls.ScrollablePanel()
            .AddControl(Controls.Markup("[bold underline cyan]Markup Syntax Showcase[/]")
                .Centered().Build())
            .AddControl(Controls.Rule("Basic Named Colors"))

            .AddControl(Controls.Markup(
                "  [black on white]black[/]  [red]red[/]  [green]green[/]  [blue]blue[/]  " +
                "[cyan]cyan[/]  [magenta]magenta[/]  [yellow]yellow[/]  [white]white[/]").Build())
            .AddControl(Controls.Markup(
                "  [maroon]maroon[/]  [navy]navy[/]  [olive]olive[/]  [purple]purple[/]  " +
                "[teal]teal[/]  [silver]silver[/]  [grey]grey[/]").Build())

            .AddControl(Controls.Rule("Extended Colors"))
            .AddControl(Controls.Markup(
                "  [orange1]orange1[/]  [hotpink]hotpink[/]  [deeppink1]deeppink1[/]  " +
                "[mediumorchid]mediumorchid[/]  [dodgerblue1]dodgerblue1[/]  " +
                "[springgreen1]springgreen1[/]").Build())
            .AddControl(Controls.Markup(
                "  [gold1]gold1[/]  [violet]violet[/]  [salmon1]salmon1[/]  " +
                "[lightskyblue1]lightskyblue1[/]  [chartreuse1]chartreuse1[/]  " +
                "[coral]coral[/]").Build())

            .AddControl(Controls.Rule("Hex Colors (#RGB and #RRGGBB)"))
            .AddControl(Controls.Markup(
                "  [#F00]#F00[/]  [#0F0]#0F0[/]  [#00F]#00F[/]  " +
                "[#FF8000]#FF8000[/]  [#8040FF]#8040FF[/]  [#00CED1]#00CED1[/]").Build())
            .AddControl(Controls.Markup(
                "  [#FF1493]#FF1493[/]  [#7FFF00]#7FFF00[/]  [#FFD700]#FFD700[/]  " +
                "[#DC143C]#DC143C[/]  [#00FA9A]#00FA9A[/]  [#FF69B4]#FF69B4[/]").Build())

            .AddControl(Controls.Rule("RGB Colors"))
            .AddControl(Controls.Markup(
                "  [rgb(255,0,0)]rgb(255,0,0)[/]  [rgb(0,255,0)]rgb(0,255,0)[/]  " +
                "[rgb(0,0,255)]rgb(0,0,255)[/]").Build())
            .AddControl(Controls.Markup(
                "  [rgb(255,165,0)]rgb(255,165,0)[/]  [rgb(128,0,255)]rgb(128,0,255)[/]  " +
                "[rgb(0,206,209)]rgb(0,206,209)[/]").Build())

            .AddControl(Controls.Rule("Grayscale"))
            .AddControl(Controls.Markup(
                "  [grey0 on white]grey0[/]  [grey15]grey15[/]  [grey30]grey30[/]  " +
                "[grey42]grey42[/]  [grey54]grey54[/]  [grey66]grey66[/]  " +
                "[grey78]grey78[/]  [grey89]grey89[/]  [grey100]grey100[/]").Build())

            .AddControl(Controls.Rule("Text Decorations"))
            .AddControl(Controls.Markup(
                "  [bold]bold[/]  [dim]dim[/]  [italic]italic[/]  " +
                "[underline]underline[/]  [strikethrough]strikethrough[/]  " +
                "[invert]invert[/]  [blink]blink[/]").Build())

            .AddControl(Controls.Rule("Combined Styles"))
            .AddControl(Controls.Markup(
                "  [bold red]bold red[/]  [italic underline blue]italic underline blue[/]  " +
                "[dim italic grey]dim italic grey[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold yellow on darkblue]bold yellow on darkblue[/]  " +
                "[underline #FF8000]underline hex orange[/]  " +
                "[bold italic green]bold italic green[/]").Build())
            .AddControl(Controls.Markup(
                "  [strikethrough red]strikethrough red[/]  " +
                "[bold underline rgb(255,105,180)]bold underline hotpink[/]").Build())

            .AddControl(Controls.Rule("Background Colors"))
            .AddControl(Controls.Markup(
                "  [white on red] white on red [/]  [black on yellow] black on yellow [/]  " +
                "[white on blue] white on blue [/]").Build())
            .AddControl(Controls.Markup(
                "  [white on green] white on green [/]  [white on magenta] white on magenta [/]  " +
                "[on cyan] on cyan [/]").Build())
            .AddControl(Controls.Markup(
                "  [bold white on #8B0000] on #8B0000 [/]  " +
                "[black on rgb(255,215,0)] on rgb(255,215,0) [/]  " +
                "[white on #2E8B57] on #2E8B57 [/]").Build())

            .AddControl(Controls.Rule("Nested Tags"))
            .AddControl(Controls.Markup(
                "  [bold]Bold [red]bold+red[/] just bold[/]").Build())
            .AddControl(Controls.Markup(
                "  [green]Green [underline]green+underline[/] green[/]").Build())
            .AddControl(Controls.Markup(
                "  [on blue]Blue bg [bold yellow]bold yellow on blue[/] back to blue bg[/]").Build())

            .AddControl(Controls.Rule("Escaping Brackets"))
            .AddControl(Controls.Markup(
                "  Literal brackets: [[bold]] displays as [bold]").Build())
            .AddControl(Controls.Markup(
                "  Array access: array[[0]] displays as array[0]").Build())

            .AddControl(Controls.Rule("Real-World Examples"))
            .AddControl(Controls.Markup(
                "  [bold red]ERROR:[/] Connection to [underline]database.local[/] failed").Build())
            .AddControl(Controls.Markup(
                "  [bold green]SUCCESS:[/] Deployed [cyan]v2.1.0[/] to [yellow]production[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold yellow]WARNING:[/] Memory usage at [bold red]92%[/] - consider scaling").Build())
            .AddControl(Controls.Markup(
                "  [dim]INFO:[/] Processing [bold]1,234[/] records... [green]done[/] in [cyan]1.2s[/]").Build())

            .AddControl(Controls.Rule("Color Gradient"))
            .AddControl(Controls.Markup(BuildGradientLine()).Build())
            .AddControl(Controls.Markup(BuildRainbowLine()).Build())

            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        var gradient = ColorGradient.FromColors(
            new Color(45, 15, 70),
            new Color(15, 50, 80),
            new Color(60, 20, 75));

        return new WindowBuilder(ws)
            .WithTitle("Markup Syntax")
            .WithSize(85, 35)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
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

    private static string BuildGradientLine()
    {
        var parts = new List<string> { "  " };
        for (int i = 0; i < 40; i++)
        {
            int r = (int)(255 * (1.0 - i / 39.0));
            int g = (int)(255 * (i / 39.0));
            parts.Add($"[rgb({r},{g},0)]\u2588[/]");
        }
        parts.Add("  Red \u2192 Green gradient");
        return string.Join("", parts);
    }

    private static string BuildRainbowLine()
    {
        var parts = new List<string> { "  " };
        var colors = new[] { "red", "orange1", "yellow", "green", "cyan", "blue", "purple" };
        foreach (var color in colors)
        {
            parts.Add($"[bold {color}]\u2588\u2588\u2588[/]");
        }
        parts.Add("  Rainbow");
        return string.Join("", parts);
    }
}
