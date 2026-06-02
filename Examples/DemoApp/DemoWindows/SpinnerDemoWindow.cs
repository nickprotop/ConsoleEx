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

        // Builds one "[spinner]  Style — description" row. Keeps the (many) preset
        // rows DRY rather than hand-writing a HorizontalGrid per style.
        static IWindowControl Row(SpinnerStyle style, Color color, string label, string desc, int? interval = null)
        {
            var spinner = Controls.Spinner()
                .WithStyle(style)
                .WithColor(color)
                .WithMargin(1, 0, 1, 0);
            if (interval.HasValue) spinner.WithInterval(interval.Value);

            return Controls.HorizontalGrid()
                .Column(col => col.Add(spinner.Build()))
                .Column(col => col.Flex()
                    .Add(Controls.Markup($"{label} — {desc}")
                        .WithMargin(0, 0, 1, 0)
                        .Build()))
                .WithMargin(0, 0, 0, 0)
                .Build();
        }

        // --- Custom markup frames row (kept bespoke — uses WithFrames, not a style) ---
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

        // --- Layout ---
        var panel = Controls.ScrollablePanel()
            .AddControl(intro)
            .AddControl(Controls.Header("Original Preset Styles"))
            .AddControl(Row(SpinnerStyle.Braille, Color.Yellow, "[yellow]Braille[/]", "default style, reliably narrow on modern terminals"))
            .AddControl(Row(SpinnerStyle.Circle, Color.Cyan1, "[cyan1]Circle[/]", "quarter-circle rotation"))
            .AddControl(Row(SpinnerStyle.Line, Color.White, "[white]Line[/]", "ASCII - \\ | / sequence"))
            .AddControl(Row(SpinnerStyle.Dots, Color.Green, "[green]Dots[/]", "fixed 3-column  .  /  .. / ... sequence"))
            .AddControl(Row(SpinnerStyle.Arc, Color.Orange1, "[orange1]Arc[/]", "arc rotation at 300 ms/frame (slow)", interval: 300))
            .AddControl(Row(SpinnerStyle.Bounce, Color.Magenta1, "[magenta1]Bounce[/]", "bouncing braille dot"))
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Contributed Styles (changlv, Discussion #25)"))
            .AddControl(Row(SpinnerStyle.Star, Color.Yellow, "[yellow]Star[/]", "twinkling star ✶✸✹✺"))
            .AddControl(Row(SpinnerStyle.GrowVertical, Color.Cyan1, "[cyan1]GrowVertical[/]", "pulsing vertical bar ▁▃▄▅▆▇"))
            .AddControl(Row(SpinnerStyle.GrowHorizontal, Color.Green, "[green]GrowHorizontal[/]", "pulsing horizontal bar ▏▎▍▌▋▊▉"))
            .AddControl(Row(SpinnerStyle.Toggle, Color.Magenta1, "[magenta1]Toggle[/]", "empty/filled square blink □■"))
            .AddControl(Row(SpinnerStyle.Arrow, Color.Orange1, "[orange1]Arrow[/]", "rotating arrow ←↑→↓"))
            .AddControl(Row(SpinnerStyle.BouncingBar, Color.White, "[white]BouncingBar[/]", "ASCII [[==  ]] bounce"))
            .AddControl(Row(SpinnerStyle.AestheticBar, Color.SpringGreen1, "[springgreen1]AestheticBar[/]", "progress bar ▰▰▰▱▱▱"))
            .AddControl(Row(SpinnerStyle.BrailleDots, Color.Yellow, "[yellow]BrailleDots[/]", "classic braille throbber ⠋⠙⠹⠸"))
            .AddControl(Row(SpinnerStyle.DotsBounce, Color.Cyan1, "[cyan1]DotsBounce[/]", "bouncing ASCII dots .  / ... /  .."))
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Custom Frames (Markup)"))
            .AddControl(customRow)
            .AddControl(Controls.Markup("[dim]Use .WithFrames(...) to supply any string array — markup is supported.[/]")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Inline (markup tag)"))
            .AddControl(Controls.Markup("[dim]Embed [[spinner]] in any markup text — it animates inline:[/]")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup("Loading [yellow][spinner][/] please wait")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup("Connecting [cyan1][spinner circle][/]   Syncing [green][spinner dots][/]")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup(
                    "Star [yellow][spinner star][/]  Arrow [orange1][spinner arrow][/]  " +
                    "Toggle [magenta1][spinner toggle][/]  Bar [green][spinner aestheticbar][/]")
                .WithMargin(1, 0, 1, 1)
                .Build())
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Per-Style Speed"))
            .AddControl(Controls.Markup(
                    "[dim]Each style animates at a sensible default speed. Override inline with a " +
                    "trailing millisecond value or named args — [[spinner <style> <ms>]] / [[spinner <style> ms:<n>]]:[/]")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup(
                    "Default [green][spinner dots][/]    Fast [yellow][spinner dots 80][/]    Slow [cyan1][spinner dots 600][/]")
                .WithMargin(1, 0, 1, 1)
                .Build())
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Reserved Width"))
            .AddControl(Controls.Markup(
                    "[dim]Reserve a fixed field width so toggling content never shifts neighbours — " +
                    "[[spinner <style> width:<n>]]. It is a minimum: a value below the glyph's natural " +
                    "width is clamped up so it never clips.[/]")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup(
                    "natural |[yellow][spinner braille][/]|   width:6 |[yellow][spinner braille width:6][/]|   dots width:8 |[green][spinner dots width:8][/]|")
                .WithMargin(1, 0, 1, 1)
                .Build())
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("In a Status Bar"))
            .AddControl(Controls.Markup(
                    "[dim]The status bar below renders spinners two ways. " +
                    "A [[spinner]] tag works in any label since item text is parsed as markup; " +
                    "SpinnerTextAnimator drives a label frame-by-frame. Press [[T]] to toggle its " +
                    "Visible flag — it hides/shows without losing animation state.[/]")
                .WithMargin(1, 0, 1, 1)
                .Build())
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // --- Status bar with live spinners ---
        // StatusBarControl is not a control container — it renders StatusBarItem
        // labels as markup. That's all a spinner needs: the inline [spinner] tag
        // animates inside any label, and SpinnerTextAnimator can drive a label setter.
        var statusBar = Controls.StatusBar()
            .AddLeft(null!, "[green]Building [spinner][/]")          // inline tag in a label
            .AddLeftSeparator()
            .AddLeftText("[dim]idle[/]")                             // animator target (index 2)
            .AddCenterText("Connecting [cyan1][spinner circle][/]")  // inline tag, center zone
            .AddRight("T", "Toggle animator")
            .AddRight("Esc", "Close")
            .WithAboveLine()
            .WithBackgroundColor(Color.Grey15)
            .WithShortcutForegroundColor(Color.Cyan1)
            .StickyBottom()
            .Build();

        // SpinnerTextAnimator — the direct replacement for a hand-rolled
        // WithAsyncWindowThread + Task.Delay loop. It drives the third left item's
        // label off the shared animation engine; no background thread, no marshaling.
        var animatorTarget = statusBar.LeftItems[2];
        var animator = new SpinnerTextAnimator(
            ws, SpinnerStyle.Braille,
            frame => animatorTarget.Label = $"[yellow]{frame}[/] working");

        // Start once, then toggle Visible (not Start/Stop) — Issue #27: hide/show in a
        // status bar without tearing down the animation. Begins hidden.
        animator.Start();
        animator.Visible = false;
        bool animatorVisible = false;

        void ToggleAnimator()
        {
            animatorVisible = !animatorVisible;
            animator.Visible = animatorVisible;   // hides (blanks label) or shows
            if (!animatorVisible)
                animatorTarget.Label = "[green]done[/]"; // replace the blanked label with a marker
        }

        statusBar.ItemClicked += (_, args) =>
        {
            if (args.Item.Shortcut == "T") ToggleAnimator();
        };

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
                    animator.Dispose();
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
                else if (e.KeyInfo.Key == ConsoleKey.T)
                {
                    ToggleAnimator();
                    e.Handled = true;
                }
            })
            .OnClosed((_, _) => animator.Dispose())
            .BuildAndShow();
    }
}
