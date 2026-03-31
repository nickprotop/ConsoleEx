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

        panel.AddControl(Ctl.Button("  Matrix Rain  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.DesktopBackground = CreateMatrixRain();
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

    /// <summary>
    /// Creates a Matrix digital rain effect as a DesktopBackgroundConfig.
    /// State is captured in the closure — each invocation creates independent rain.
    /// </summary>
    private static DesktopBackgroundConfig CreateMatrixRain()
    {
        const int trailLength = 14;
        const double spawnChance = 0.04;
        const string glyphs = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123456789";

        // Per-column state: head row (-1 = inactive), speed (rows per tick), char buffer
        int[]? heads = null;
        int[]? speeds = null;
        int[]? cooldowns = null;
        char[,]? chars = null;
        Random? rng = null;
        int lastW = 0, lastH = 0;

        return new DesktopBackgroundConfig
        {
            AnimationIntervalMs = 70,
            PaintCallback = (buffer, width, height, elapsed) =>
            {
                rng ??= new Random();

                // Re-init state on resize
                if (heads == null || width != lastW || height != lastH)
                {
                    lastW = width;
                    lastH = height;
                    heads = new int[width];
                    speeds = new int[width];
                    cooldowns = new int[width];
                    chars = new char[height, width];
                    for (int c = 0; c < width; c++)
                    {
                        heads[c] = -1;
                        speeds[c] = rng.Next(1, 3);
                        cooldowns[c] = 0;
                    }
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            chars[y, x] = glyphs[rng.Next(glyphs.Length)];
                }

                // Clear to black
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        buffer.SetCell(x, y, new Cell(' ', Color.Black, Color.Black));

                // Update columns
                for (int col = 0; col < width; col++)
                {
                    // Advance active columns
                    if (heads![col] >= 0)
                    {
                        if (cooldowns![col] <= 0)
                        {
                            heads[col] += speeds![col];
                            cooldowns[col] = speeds[col] > 1 ? 0 : 1;
                        }
                        else
                        {
                            cooldowns[col]--;
                        }

                        // Deactivate if trail fully off screen
                        if (heads[col] - trailLength >= height)
                        {
                            heads[col] = -1;
                        }
                    }
                    else
                    {
                        // Randomly spawn new column
                        if (rng.NextDouble() < spawnChance)
                        {
                            heads[col] = 0;
                            speeds![col] = rng.Next(1, 3);
                            cooldowns![col] = 0;
                            // Refresh chars for this column
                            for (int y = 0; y < height; y++)
                                chars![y, col] = glyphs[rng.Next(glyphs.Length)];
                        }
                    }

                    // Draw trail
                    if (heads![col] < 0) continue;

                    for (int i = 0; i < trailLength; i++)
                    {
                        int row = heads[col] - i;
                        if (row < 0 || row >= height) continue;

                        // Randomly mutate chars near the head
                        if (i < 3 && rng.NextDouble() < 0.3)
                            chars![row, col] = glyphs[rng.Next(glyphs.Length)];

                        char ch = chars![row, col];

                        Color fg;
                        if (i == 0)
                        {
                            // Head: bright white-green
                            fg = new Color(200, 255, 200);
                        }
                        else
                        {
                            // Trail: fade from bright green to dark green
                            double fade = 1.0 - (double)i / trailLength;
                            byte g = (byte)(200 * fade);
                            byte r = (byte)(40 * fade);
                            fg = new Color(r, g, 0);
                        }

                        buffer.SetCell(col, row, new Cell(ch, fg, Color.Black));
                    }
                }
            }
        };
    }
}
