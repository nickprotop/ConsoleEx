using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

internal static class AnimationDemoWindow
{
    #region Constants

    private const int WindowWidth = 60;
    private const int WindowHeight = 27;
    private const int SpawnedWindowWidth = 35;
    private const int SpawnedWindowHeight = 10;
    private const int ButtonWidth = 25;
    private const int ButtonLeftMargin = 2;
    private const int SectionLabelLeftMargin = 1;
    private const int SectionTopMargin = 1;
    private const int EasingBarLength = 40;

    #endregion

    private static readonly string[] EasingNames = { "Linear", "EaseIn", "EaseOut", "EaseInOut", "Bounce", "Elastic" };
    private static readonly EasingFunction[] EasingFuncs =
    {
        EasingFunctions.Linear,
        EasingFunctions.EaseIn,
        EasingFunctions.EaseOut,
        EasingFunctions.EaseInOut,
        EasingFunctions.Bounce,
        EasingFunctions.Elastic
    };

    public static Window Create(ConsoleWindowSystem ws)
    {
        int easingIndex = 3; // Start with EaseInOut

        var easingLabel = Controls.Markup()
            .AddLine($"[dim]Current easing:[/] [bold]{EasingNames[easingIndex]}[/]")
            .WithMargin(SectionLabelLeftMargin, SectionTopMargin, 0, 0)
            .Build();

        Window? window = null;

        #region Slide Buttons

        var slideSectionLabel = Controls.Label("[bold cyan]Slide Animations[/]");
        slideSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

        var slideLeftBtn = Controls.Button("Slide In (Left)")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                var spawned = BuildSpawnedWindow(ws, "Slide Left");
                WindowAnimations.SlideIn(spawned, SlideDirection.Left, easing: EasingFuncs[easingIndex]);
            })
            .Build();
        slideLeftBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var slideRightBtn = Controls.Button("Slide In (Right)")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                var spawned = BuildSpawnedWindow(ws, "Slide Right");
                WindowAnimations.SlideIn(spawned, SlideDirection.Right, easing: EasingFuncs[easingIndex]);
            })
            .Build();
        slideRightBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var slideTopBtn = Controls.Button("Slide In (Top)")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                var spawned = BuildSpawnedWindow(ws, "Slide Top");
                WindowAnimations.SlideIn(spawned, SlideDirection.Top, easing: EasingFuncs[easingIndex]);
            })
            .Build();
        slideTopBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var slideBottomBtn = Controls.Button("Slide In (Bottom)")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                var spawned = BuildSpawnedWindow(ws, "Slide Bottom");
                WindowAnimations.SlideIn(spawned, SlideDirection.Bottom, easing: EasingFuncs[easingIndex]);
            })
            .Build();
        slideBottomBtn.Margin = new Margin { Left = ButtonLeftMargin };

        #endregion

        #region Fade Buttons

        var fadeSectionLabel = Controls.Label("[bold cyan]Fade Animations[/]");
        fadeSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

        var fadeInBtn = Controls.Button("Fade In")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                var spawned = BuildSpawnedWindow(ws, "Fade In");
                WindowAnimations.FadeIn(spawned, easing: EasingFuncs[easingIndex]);
            })
            .Build();
        fadeInBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var fadeOutCloseBtn = Controls.Button("Fade Out & Close")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                var spawned = BuildSpawnedWindow(ws, "Fade Out");
                WindowAnimations.FadeOut(spawned, easing: EasingFuncs[easingIndex],
                    onComplete: () => ws.CloseWindow(spawned));
            })
            .Build();
        fadeOutCloseBtn.Margin = new Margin { Left = ButtonLeftMargin };

        #endregion

        #region Easing Selector

        var easingSectionLabel = Controls.Label("[bold cyan]Easing Function[/]");
        easingSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

        var cycleEasingBtn = Controls.Button("Cycle Easing")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                easingIndex = (easingIndex + 1) % EasingNames.Length;
                easingLabel.SetContent(new List<string>
                {
                    $"[dim]Current easing:[/] [bold]{EasingNames[easingIndex]}[/]"
                });
            })
            .Build();
        cycleEasingBtn.Margin = new Margin { Left = ButtonLeftMargin };

        #endregion

        window = new WindowBuilder(ws)
            .WithTitle("Animation Demo")
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
            .AddControl(Controls.Markup("[bold underline]Animation Showcase[/]")
                .Centered()
                .Build())
            .AddControl(easingSectionLabel)
            .AddControl(easingLabel)
            .AddControl(cycleEasingBtn)
            .AddControl(fadeSectionLabel)
            .AddControl(fadeInBtn)
            .AddControl(fadeOutCloseBtn)
            .AddControl(slideSectionLabel)
            .AddControl(slideLeftBtn)
            .AddControl(slideRightBtn)
            .AddControl(slideTopBtn)
            .AddControl(slideBottomBtn)
            .BuildAndShow();

        return window;
    }

    private static Window BuildSpawnedWindow(ConsoleWindowSystem ws, string title)
    {
        Window? spawned = null;
        spawned = new WindowBuilder(ws)
            .WithTitle(title)
            .WithSize(SpawnedWindowWidth, SpawnedWindowHeight)
            .Centered()
            .AddControl(Controls.Markup()
                .AddLine($"[bold]{title}[/]")
                .AddEmptyLine()
                .AddLine("Animated window content.")
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
            })
            .BuildAndShow();

        return spawned;
    }
}
