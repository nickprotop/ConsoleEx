using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

internal static class BorderStyleWindow
{
    private const int WindowWidth = 62;
    private const int WindowHeight = 28;
    private const int SpawnedWidth = 44;
    private const int SpawnedHeight = 12;
    private const int ButtonWidth = 25;
    private const int ButtonLeftMargin = 2;
    private const int SectionLeftMargin = 1;
    private const int SectionTopMargin = 1;

    private static readonly BorderStyle[] AllStyles =
        { BorderStyle.DoubleLine, BorderStyle.Single, BorderStyle.Rounded, BorderStyle.None };

    public static Window Create(ConsoleWindowSystem ws)
    {
        int cycleIndex = 0;

        var cycleLabel = Controls.Markup()
            .AddLine($"[dim]Current style:[/] [bold]{AllStyles[cycleIndex]}[/]")
            .WithMargin(SectionLeftMargin, 0, 0, 0)
            .Build();

        // --- Show style buttons ---

        var showSectionLabel = Controls.Label("[bold cyan]Show Border Style[/]");
        showSectionLabel.Margin = new Margin { Left = SectionLeftMargin, Top = SectionTopMargin };

        var doubleLineBtn = Controls.Button("DoubleLine (Default)")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => SpawnStyleWindow(ws, BorderStyle.DoubleLine))
            .Build();
        doubleLineBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var singleBtn = Controls.Button("Single")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => SpawnStyleWindow(ws, BorderStyle.Single))
            .Build();
        singleBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var roundedBtn = Controls.Button("Rounded")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => SpawnStyleWindow(ws, BorderStyle.Rounded))
            .Build();
        roundedBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var noneBtn = Controls.Button("None (Borderless)")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => SpawnStyleWindow(ws, BorderStyle.None))
            .Build();
        noneBtn.Margin = new Margin { Left = ButtonLeftMargin };

        // --- Interactive toggle section ---

        var toggleSectionLabel = Controls.Label("[bold cyan]Interactive Toggle[/]");
        toggleSectionLabel.Margin = new Margin { Left = SectionLeftMargin, Top = SectionTopMargin };

        Window? window = null;

        var cycleBtn = Controls.Button("Cycle This Window")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                cycleIndex = (cycleIndex + 1) % AllStyles.Length;
                var style = AllStyles[cycleIndex];
                cycleLabel.SetContent(new List<string>
                {
                    $"[dim]Current style:[/] [bold]{style}[/]"
                });

                window!.BorderStyle = style;
            })
            .Build();
        cycleBtn.Margin = new Margin { Left = ButtonLeftMargin };

        window = new WindowBuilder(ws)
            .WithTitle("Border Styles")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(window!);
                    e.Handled = true;
                }
            })
            .AddControl(Controls.Markup("[bold underline]Border Style Showcase[/]")
                .Centered()
                .Build())
            .AddControl(showSectionLabel)
            .AddControl(doubleLineBtn)
            .AddControl(singleBtn)
            .AddControl(roundedBtn)
            .AddControl(noneBtn)
            .AddControl(toggleSectionLabel)
            .AddControl(cycleLabel)
            .AddControl(cycleBtn)
            .AddControl(Controls.Markup()
                .AddEmptyLine()
                .AddLine("[dim]  Cycle changes this window's border between[/]")
                .AddLine("[dim]  DoubleLine → Single → Rounded → None[/]")
                .Build())
            .BuildAndShow();

        return window;
    }

    private static void SpawnStyleWindow(ConsoleWindowSystem ws, BorderStyle style)
    {
        Window? spawned = null;

        var builder = new WindowBuilder(ws)
            .WithTitle($"{style} Border")
            .WithSize(SpawnedWidth, SpawnedHeight)
            .WithBorderStyle(style)
            .Centered()
            .AddControl(Controls.Markup()
                .AddLine($"[bold cyan]BorderStyle.{style}[/]")
                .AddEmptyLine()
                .AddLine(GetStyleDescription(style))
                .AddEmptyLine()
                .AddLine("[dim]Press ESC to close[/]")
                .WithMargin(1, 1, 1, 0)
                .Build())
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(spawned!);
                    e.Handled = true;
                }
            });

        spawned = builder.BuildAndShow();
    }

    private static string GetStyleDescription(BorderStyle style) => style switch
    {
        BorderStyle.DoubleLine => "Active: ╔═╗║╚╝  Inactive: ┌─┐│└┘",
        BorderStyle.Single => "Consistent single-line: ┌─┐│└┘",
        BorderStyle.Rounded => "Rounded corners: ╭─╮│╰╯",
        BorderStyle.None => "No visible border, title, or buttons",
        _ => ""
    };
}
