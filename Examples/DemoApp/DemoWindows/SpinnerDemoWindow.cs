// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

public static class SpinnerDemoWindow
{
    private const int WindowWidth = 70;
    private const int WindowHeight = 28;

    public static Window Create(ConsoleWindowSystem ws)
    {
        // --- Intro ---
        var intro = Controls.Markup("[dim]Spinners provide indeterminate-progress feedback while a task runs.[/]")
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- 1: Braille (yellow) ---
        var brailleRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithStyle(SpinnerStyle.Braille)
                    .WithColor(Color.Yellow)
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[yellow]Braille[/] — default style, reliably narrow on modern terminals")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- 2: Circle (cyan) ---
        var circleRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithStyle(SpinnerStyle.Circle)
                    .WithColor(Color.Cyan1)
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[cyan1]Circle[/] — quarter-circle rotation")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- 3: Line (ASCII) ---
        var lineRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithStyle(SpinnerStyle.Line)
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[white]Line[/] — ASCII - \\ | / sequence")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- 4: Dots ---
        var dotsRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithStyle(SpinnerStyle.Dots)
                    .WithColor(Color.Green)
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[green]Dots[/] — fixed 3-column  .  /  .. / ... sequence")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- 5: Custom markup frames ---
        var customRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithFrames("[green]✔[/]", "[yellow]◐[/]", "[red]✗[/]")
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[dim]Custom[/] — user-supplied markup frames")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- 6: Arc (slow) ---
        var arcRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithStyle(SpinnerStyle.Arc)
                    .WithInterval(300)
                    .WithColor(Color.Orange1)
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[orange1]Arc[/] — arc rotation at 300 ms/frame (slow)")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- 7: Bounce ---
        var bounceRow = Controls.HorizontalGrid()
            .Column(col => col
                .Add(Controls.Spinner()
                    .WithStyle(SpinnerStyle.Bounce)
                    .WithColor(Color.Magenta1)
                    .WithMargin(1, 0, 1, 0)
                    .Build()))
            .Column(col => col.Flex()
                .Add(Controls.Markup("[magenta1]Bounce[/] — bouncing braille dot")
                    .WithMargin(0, 0, 1, 0)
                    .Build()))
            .WithMargin(0, 0, 0, 0)
            .Build();

        // --- Layout ---
        var panel = Controls.ScrollablePanel()
            .AddControl(intro)
            .AddControl(Controls.Header("Preset Styles"))
            .AddControl(brailleRow)
            .AddControl(circleRow)
            .AddControl(lineRow)
            .AddControl(dotsRow)
            .AddControl(arcRow)
            .AddControl(bounceRow)
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Custom Frames (Markup)"))
            .AddControl(customRow)
            .AddControl(Controls.Markup("[dim]Use .WithFrames(...) to supply any string array — markup is supported.[/]")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var statusBar = Controls.Markup("[dim]Esc: close[/]")
            .StickyBottom()
            .Build();

        var gradient = ColorGradient.FromColors(
            new Color(10, 20, 40),
            new Color(20, 35, 60),
            new Color(12, 28, 50));

        return new WindowBuilder(ws)
            .WithTitle("Spinner Controls")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControls(panel, statusBar)
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
