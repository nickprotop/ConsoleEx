using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace DemoApp.DemoWindows;

public static class DesktopBackgroundWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var nav = Ctl.NavigationView()
            .WithNavWidth(28)
            .WithPaneHeader("[bold rgb(180,140,255)]  Desktop Background[/]")
            .WithSelectedColors(Color.White, new Color(80, 50, 140))
            .WithSelectionIndicator('\u25b8')
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(80, 60, 120))
            .WithContentBackground(new Color(22, 18, 35))
            .WithContentPadding(1, 0, 1, 0)
            .WithContentHeader(true)
            .AddHeader("Background", new Color(180, 140, 255), header => header
                .AddItem("Solid Color", icon: "\u25cf", subtitle: "Theme default solid color",
                    content: panel => BuildSolidPage(panel, ws))
                .AddItem("Gradient", icon: "\u25a8", subtitle: "Preset gradient backgrounds",
                    content: panel => BuildGradientPage(panel, ws))
                .AddItem("Pattern", icon: "\u25a6", subtitle: "Repeating tile patterns",
                    content: panel => BuildPatternPage(panel, ws)))
            .AddHeader("Dynamic", new Color(120, 200, 160), header => header
                .AddItem("Effects", icon: "\u2738", subtitle: "Animated background effects",
                    content: panel => BuildEffectsPage(panel, ws)))
            .AddHeader("Presets", new Color(255, 200, 100), header => header
                .AddItem("Combined", icon: "\u2726", subtitle: "Gradient + pattern combos",
                    content: panel => BuildCombinedPage(panel, ws)))
            .WithAlignment(HorizontalAlignment.Stretch)
            .Fill()
            .Build();

        var window = new WindowBuilder(ws)
            .WithTitle("Desktop Background")
            .WithSize(80, 28)
            .Centered()
            .WithBackgroundGradient(
                ColorGradient.FromColors(new Color(20, 15, 40), new Color(10, 8, 20)),
                GradientDirection.Vertical)
            .AddControl(nav)
            .BuildAndShow();

        return window;
    }

    private static void BuildSolidPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(180,140,255)]Solid Color[/]")
            .AddEmptyLine()
            .AddLine("[dim]The desktop background defaults to the theme's solid color.[/]")
            .AddLine("[dim]Use the button below to reset to the theme default.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Reset")
            .WithColor(new Color(80, 60, 140))
            .Build());

        panel.AddControl(Ctl.Button("  Reset to Theme Default  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = null;
                            })
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Tip: setting the background to null restores the theme default.[/]")
            .Build());
    }

    private static void BuildGradientPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(180,140,255)]Gradient Backgrounds[/]")
            .AddEmptyLine()
            .AddLine("[dim]Apply a preset gradient to the desktop background.[/]")
            .AddLine("[dim]Gradients replace the solid theme color.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Presets")
            .WithColor(new Color(80, 60, 140))
            .Build());

        var presets = new (string name, Color from, Color to, GradientDirection dir)[]
        {
            ("Midnight Blue", new Color(20, 30, 80), new Color(5, 5, 15), GradientDirection.Vertical),
            ("Ocean Depth", new Color(10, 40, 90), new Color(5, 10, 25), GradientDirection.Vertical),
            ("Forest Night", new Color(10, 50, 25), new Color(5, 15, 10), GradientDirection.Vertical),
            ("Sunset", new Color(120, 40, 20), new Color(40, 10, 60), GradientDirection.Horizontal),
            ("Aurora", new Color(20, 60, 100), new Color(60, 20, 80), GradientDirection.DiagonalDown),
            ("Ember", new Color(80, 15, 10), new Color(40, 10, 50), GradientDirection.DiagonalUp),
        };

        foreach (var (name, from, to, dir) in presets)
        {
            panel.AddControl(Ctl.Button($"  {name}  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .WithMargin(0, 1, 0, 0)
                .OnClick((_, _, _) =>
                {
                    ws.DesktopBackground = DesktopBackgroundConfig.FromGradient(
                        ColorGradient.FromColors(from, to), dir);
                                    })
                .Build());
        }
    }

    private static void BuildPatternPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(180,140,255)]Pattern Backgrounds[/]")
            .AddEmptyLine()
            .AddLine("[dim]Apply a repeating tile pattern to the desktop.[/]")
            .AddLine("[dim]Patterns are rendered on top of the solid theme color.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Presets")
            .WithColor(new Color(80, 60, 140))
            .Build());

        var patterns = new (string name, Func<DesktopPattern> factory)[]
        {
            ("Checkerboard", () => DesktopPatterns.Checkerboard),
            ("Dots", () => DesktopPatterns.Dots),
            ("Hatch Down", () => DesktopPatterns.HatchDown),
            ("Hatch Up", () => DesktopPatterns.HatchUp),
            ("Crosshatch", () => DesktopPatterns.Crosshatch),
            ("Light Shade", () => DesktopPatterns.LightShade),
            ("Medium Shade", () => DesktopPatterns.MediumShade),
            ("Dense Shade", () => DesktopPatterns.DenseShade),
            ("Horizontal Lines", () => DesktopPatterns.HorizontalLines),
            ("Vertical Lines", () => DesktopPatterns.VerticalLines),
            ("Grid", () => DesktopPatterns.Grid),
        };

        foreach (var (name, factory) in patterns)
        {
            panel.AddControl(Ctl.Button($"  {name}  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .WithMargin(0, 1, 0, 0)
                .OnClick((_, _, _) =>
                {
                    ws.DesktopBackground = DesktopBackgroundConfig.FromPattern(factory());
                                    })
                .Build());
        }
    }

    private static void BuildEffectsPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,200,160)]Animated Effects[/]")
            .AddEmptyLine()
            .AddLine("[dim]Timer-based animated backgrounds. These use a paint callback[/]")
            .AddLine("[dim]that runs on an interval to produce dynamic visuals.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Effects")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Ctl.Button("  Color Cycling  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = DesktopEffects.ColorCycling();
                            })
            .Build());

        panel.AddControl(Ctl.Button("  Pulse (Dark Blue)  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = DesktopEffects.Pulse(new Color(15, 25, 60));
                            })
            .Build());

        panel.AddControl(Ctl.Button("  Drifting Gradient  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = DesktopEffects.DriftingGradient(
                    new Color(20, 40, 80), new Color(60, 20, 70));
                            })
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Control")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Ctl.Button("  Stop Animation  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = null;
                            })
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Stop Animation resets to the theme default.[/]")
            .Build());
    }

    private static void BuildCombinedPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(255,200,100)]Combined Presets[/]")
            .AddEmptyLine()
            .AddLine("[dim]These presets combine a gradient with a pattern overlay.[/]")
            .AddLine("[dim]The gradient is rendered first, then the pattern on top.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Presets")
            .WithColor(new Color(140, 110, 50))
            .Build());

        panel.AddControl(Ctl.Button("  Midnight Grid  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = new DesktopBackgroundConfig
                {
                    Gradient = new GradientBackground(
                        ColorGradient.FromColors(new Color(20, 30, 80), new Color(5, 5, 15)),
                        GradientDirection.Vertical),
                    Pattern = DesktopPatterns.Grid
                };
                            })
            .Build());

        panel.AddControl(Ctl.Button("  Ocean Dots  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = new DesktopBackgroundConfig
                {
                    Gradient = new GradientBackground(
                        ColorGradient.FromColors(new Color(10, 40, 90), new Color(5, 10, 25)),
                        GradientDirection.Vertical),
                    Pattern = DesktopPatterns.Dots
                };
                            })
            .Build());

        panel.AddControl(Ctl.Button("  Forest Crosshatch  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = new DesktopBackgroundConfig
                {
                    Gradient = new GradientBackground(
                        ColorGradient.FromColors(new Color(10, 50, 25), new Color(5, 15, 10)),
                        GradientDirection.Vertical),
                    Pattern = DesktopPatterns.Crosshatch
                };
                            })
            .Build());

        panel.AddControl(Ctl.Button("  Sunset Checkerboard  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = new DesktopBackgroundConfig
                {
                    Gradient = new GradientBackground(
                        ColorGradient.FromColors(new Color(120, 40, 20), new Color(40, 10, 60)),
                        GradientDirection.Horizontal),
                    Pattern = DesktopPatterns.Checkerboard
                };
                            })
            .Build());

        panel.AddControl(Ctl.Button("  Aurora Lines  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = new DesktopBackgroundConfig
                {
                    Gradient = new GradientBackground(
                        ColorGradient.FromColors(new Color(20, 60, 100), new Color(60, 20, 80)),
                        GradientDirection.DiagonalDown),
                    Pattern = DesktopPatterns.HorizontalLines
                };
                            })
            .Build());
    }
}
