using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace BenchmarkApp;

/// <summary>
/// Describes a single benchmark test scenario with its creation and mutation logic.
/// </summary>
/// <param name="Name">Display name of the test.</param>
/// <param name="Weight">Scoring weight multiplier.</param>
/// <param name="CreateWindow">Factory that builds the test window at a given position.</param>
/// <param name="Mutate">Per-frame mutation callback (window, frame index).</param>
/// <param name="Cleanup">Optional cleanup action to close extra windows created by the test.</param>
public record BenchmarkTest(
    string Name,
    double Weight,
    Func<ConsoleWindowSystem, int, int, Window> CreateWindow,
    Action<Window, int> Mutate,
    Action<ConsoleWindowSystem>? Cleanup = null);

/// <summary>
/// Provides all benchmark test scenes for the benchmark runner.
/// </summary>
public static class BenchmarkScenes
{
    private const int StandardWidth = 60;
    private const int StandardHeight = 20;
    private const int LargeWidth = 70;
    private const int LargeHeight = 30;
    private const int AlphaBlendWidth = 70;
    private const int AlphaBlendHeight = 28;
    private const int OverlapCount = 3;
    private const int OverlapOffsetX = 15;
    private const int OverlapOffsetY = 5;
    private const int StaticLineCount = 10;
    private const int AlphaLadderSteps = 8;
    private const int FadeStripLength = 60;
    private const int GlassPanelCount = 4;
    private const int DeepOuterColumns = 4;
    private const int DeepInnerRows = 5;
    private const int DeepSubColumns = 3;
    private const double GradientPhaseStep = 0.08;
    private const double GradientThirdOffset = 1.0 / 3.0;
    private const double GradientTwoThirdsOffset = 2.0 / 3.0;
    private const double HueFullCycle = 6.0;

    /// <summary>
    /// Returns all benchmark test definitions.
    /// </summary>
    public static List<BenchmarkTest> GetAllTests()
    {
        return new List<BenchmarkTest>
        {
            CreateStaticContentTest(),
            CreateTextScrollingTest(),
            CreateAlphaBlendingTest(),
            CreateWindowOverlapTest(),
            CreateDeepControlsTest(),
            CreateFullRedrawTest(),
        };
    }

    private static BenchmarkTest CreateStaticContentTest()
    {
        return new BenchmarkTest(
            "Static Content",
            0.5,
            (ws, left, top) =>
            {
                var markupBuilder = SharpConsoleUI.Builders.Controls.Markup();
                for (int line = 0; line < StaticLineCount; line++)
                {
                    markupBuilder.AddLine(
                        $"[bold yellow]Line {line + 1}:[/] The quick [red]brown[/] fox " +
                        $"[green]jumps[/] over the [blue]lazy[/] dog");
                }

                var window = new SharpConsoleUI.Builders.WindowBuilder(ws)
                    .WithTitle("Static Content")
                    .AtPosition(left, top)
                    .WithSize(StandardWidth, StandardHeight)
                    .WithBackgroundGradient(
                        ColorGradient.FromColors(Color.Navy, Color.DarkBlue, Color.Blue),
                        GradientDirection.DiagonalDown)
                    .AddControl(markupBuilder.Build())
                    .BuildAndShow(activate: false);

                return window;
            },
            (window, i) => { /* no-op: static content */ });
    }

    private static BenchmarkTest CreateTextScrollingTest()
    {
        MarkupControl? capturedLabel = null;

        return new BenchmarkTest(
            "Text Scrolling",
            1.0,
            (ws, left, top) =>
            {
                capturedLabel = SharpConsoleUI.Builders.Controls.Markup()
                    .AddLine("Counter: 000000")
                    .Build();

                var window = new SharpConsoleUI.Builders.WindowBuilder(ws)
                    .WithTitle("Text Scrolling")
                    .AtPosition(left, top)
                    .WithSize(StandardWidth, StandardHeight)
                    .AddControl(capturedLabel)
                    .BuildAndShow(activate: false);

                return window;
            },
            (window, i) =>
            {
                capturedLabel?.SetContent(new List<string> { $"Counter: {i:D6}" });
            });
    }

    private static BenchmarkTest CreateAlphaBlendingTest()
    {
        return new BenchmarkTest(
            "Alpha Blending",
            2.0,
            (ws, left, top) =>
            {
                // Alpha-ladder panels: 8 panels with increasing alpha
                var alphaLadder = SharpConsoleUI.Builders.Controls.HorizontalGrid();
                byte[] alphaSteps = { 0, 36, 73, 109, 146, 182, 219, 255 };
                for (int step = 0; step < AlphaLadderSteps; step++)
                {
                    byte alpha = alphaSteps[step];
                    var color = new Color(255, 140, 0, alpha);
                    var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
                        .WithBackgroundColor(color)
                        .WithScrollbar(false)
                        .AddControl(SharpConsoleUI.Builders.Controls.Markup()
                            .AddLine($"[bold]A={alpha}[/]")
                            .Build())
                        .Build();
                    int stepCopy = step;
                    alphaLadder.Column(col => col.Flex().Add(panel));
                }
                var alphaLadderGrid = alphaLadder.Build();

                // Fade strip: 60 block chars with varying foreground alpha
                var fadeBuilder = new System.Text.StringBuilder();
                for (int col = 0; col < FadeStripLength; col++)
                {
                    byte alpha = (byte)(col * 255 / (FadeStripLength - 1));
                    fadeBuilder.Append($"[#00DCDC{alpha:X2}]\u2588[/]");
                }
                var fadeStrip = SharpConsoleUI.Builders.Controls.Markup()
                    .AddLine(fadeBuilder.ToString())
                    .Build();

                // Glass panels: 4 panels with varying blue alpha
                var glassGrid = SharpConsoleUI.Builders.Controls.HorizontalGrid();
                byte[] glassAlphas = { 64, 128, 192, 255 };
                for (int g = 0; g < GlassPanelCount; g++)
                {
                    byte alpha = glassAlphas[g];
                    var color = new Color(30, 144, 255, alpha);
                    var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
                        .WithBackgroundColor(color)
                        .WithScrollbar(false)
                        .AddControl(SharpConsoleUI.Builders.Controls.Markup()
                            .AddLine($"[bold]Glass A={alpha}[/]")
                            .Build())
                        .Build();
                    glassGrid.Column(col => col.Flex().Add(panel));
                }
                var glassGridControl = glassGrid.Build();

                // Blend preview
                var blendPreview = SharpConsoleUI.Builders.Controls.Markup()
                    .AddLine("[bold]Blend Preview:[/]")
                    .AddLine("  [#FF8C00]SRC: rgba(255,140,0,128)[/]")
                    .AddLine("  [#1E90FF]DST: rgba(30,144,255,255)[/]")
                    .Build();

                var window = new SharpConsoleUI.Builders.WindowBuilder(ws)
                    .WithTitle("Alpha Blending")
                    .AtPosition(left, top)
                    .WithSize(AlphaBlendWidth, AlphaBlendHeight)
                    .WithBackgroundGradient(
                        ColorGradient.FromColors(Color.DarkSlateGray1, Color.Grey23, Color.DarkMagenta),
                        GradientDirection.DiagonalDown)
                    .AddControl(alphaLadderGrid)
                    .AddControl(fadeStrip)
                    .AddControl(glassGridControl)
                    .AddControl(blendPreview)
                    .BuildAndShow(activate: false);

                return window;
            },
            (window, i) =>
            {
                RotateGradient(window, i);
            });
    }

    private static BenchmarkTest CreateWindowOverlapTest()
    {
        Window[]? capturedWindows = null;
        ConsoleWindowSystem? capturedWs = null;

        return new BenchmarkTest(
            "Window Overlap",
            1.5,
            (ws, left, top) =>
            {
                capturedWs = ws;
                capturedWindows = new Window[OverlapCount];

                for (int w = 0; w < OverlapCount; w++)
                {
                    var markupBuilder = SharpConsoleUI.Builders.Controls.Markup();
                    markupBuilder.AddLine($"[bold yellow]Window {w + 1} of {OverlapCount}[/]");
                    markupBuilder.AddLine("");
                    for (int line = 0; line < StaticLineCount; line++)
                    {
                        markupBuilder.AddLine(
                            $"[dim]Content line {line + 1} in overlap window {w + 1}[/]");
                    }

                    var winLeft = left + (w * OverlapOffsetX);
                    var winTop = top + (w * OverlapOffsetY);

                    capturedWindows[w] = new SharpConsoleUI.Builders.WindowBuilder(ws)
                        .WithTitle($"Overlap {w + 1}")
                        .AtPosition(winLeft, winTop)
                        .WithSize(StandardWidth, StandardHeight)
                        .WithBackgroundGradient(
                            ColorGradient.FromColors(
                                HueToColor((double)w / OverlapCount),
                                HueToColor((double)w / OverlapCount + 0.5)),
                            GradientDirection.DiagonalDown)
                        .AddControl(markupBuilder.Build())
                        .BuildAndShow(activate: false);
                }

                return capturedWindows[0];
            },
            (window, i) =>
            {
                if (capturedWindows == null || capturedWs == null)
                    return;

                int idx = i % OverlapCount;
                capturedWs.WindowStateService.SetActiveWindow(capturedWindows[idx]);
                capturedWindows[idx].IsDirty = true;
            },
            Cleanup: ws =>
            {
                // Close all overlap windows (including the first one returned by CreateWindow)
                if (capturedWindows == null)
                    return;

                foreach (var w in capturedWindows)
                {
                    if (w != null)
                        ws.CloseWindow(w);
                }
                capturedWindows = null;
            });
    }

    private static BenchmarkTest CreateDeepControlsTest()
    {
        return new BenchmarkTest(
            "Deep Controls",
            2.5,
            (ws, left, top) =>
            {
                // 4-level nesting: outer grid -> columns -> panels -> inner grids -> markup
                var outerGrid = SharpConsoleUI.Builders.Controls.HorizontalGrid();
                for (int c = 0; c < DeepOuterColumns; c++)
                {
                    var panelBuilder = SharpConsoleUI.Builders.Controls.ScrollablePanel()
                        .WithScrollbar(false);

                    for (int r = 0; r < DeepInnerRows; r++)
                    {
                        var innerGrid = SharpConsoleUI.Builders.Controls.HorizontalGrid();
                        for (int s = 0; s < DeepSubColumns; s++)
                        {
                            var markup = SharpConsoleUI.Builders.Controls.Markup()
                                .AddLine($"[bold]C{c}R{r}S{s}[/]")
                                .Build();
                            int sCopy = s;
                            innerGrid.Column(col => col.Flex().Add(markup));
                        }
                        panelBuilder.AddControl(innerGrid.Build());
                    }

                    var panel = panelBuilder.Build();
                    int cCopy = c;
                    outerGrid.Column(col => col.Flex().Add(panel));
                }

                var window = new SharpConsoleUI.Builders.WindowBuilder(ws)
                    .WithTitle("Deep Controls")
                    .AtPosition(left, top)
                    .WithSize(LargeWidth, LargeHeight)
                    .WithBackgroundGradient(
                        ColorGradient.FromColors(Color.DarkGreen, Color.Green, Color.Chartreuse1),
                        GradientDirection.DiagonalDown)
                    .AddControl(outerGrid.Build())
                    .BuildAndShow(activate: false);

                return window;
            },
            (window, i) =>
            {
                RotateGradient(window, i);
            });
    }

    private static BenchmarkTest CreateFullRedrawTest()
    {
        return new BenchmarkTest(
            "Full Redraw",
            2.5,
            (ws, left, top) =>
            {
                var markup = SharpConsoleUI.Builders.Controls.Markup()
                    .AddLine("[bold yellow]Full Redraw Test[/]")
                    .AddLine("")
                    .AddLine("This window forces a complete repaint every frame")
                    .AddLine("by rotating the background gradient continuously.")
                    .AddLine("")
                    .AddLine("[dim]Stresses the entire render pipeline:[/]")
                    .AddLine("  - Buffer clearing")
                    .AddLine("  - Gradient computation")
                    .AddLine("  - Control layout and painting")
                    .AddLine("  - ANSI diff and flush")
                    .Build();

                var window = new SharpConsoleUI.Builders.WindowBuilder(ws)
                    .WithTitle("Full Redraw")
                    .AtPosition(left, top)
                    .WithSize(LargeWidth, LargeHeight)
                    .WithBackgroundGradient(
                        ColorGradient.FromColors(Color.Red, Color.Yellow, Color.Green),
                        GradientDirection.DiagonalDown)
                    .AddControl(markup)
                    .BuildAndShow(activate: false);

                return window;
            },
            (window, i) =>
            {
                RotateGradient(window, i);
            });
    }

    /// <summary>
    /// Rotates the background gradient of a window based on the current frame.
    /// </summary>
    internal static void RotateGradient(Window window, int frame)
    {
        double phase = frame * GradientPhaseStep;
        var c1 = HueToColor(phase);
        var c2 = HueToColor(phase + GradientThirdOffset);
        var c3 = HueToColor(phase + GradientTwoThirdsOffset);
        window.BackgroundGradient = new GradientBackground(
            ColorGradient.FromColors(c1, c2, c3),
            GradientDirection.DiagonalDown);
    }

    /// <summary>
    /// Converts a hue value (0.0-1.0) to a fully saturated RGB color.
    /// </summary>
    internal static Color HueToColor(double h)
    {
        h = ((h % 1.0) + 1.0) % 1.0;
        double s = h * HueFullCycle;
        int i = (int)s;
        double f = s - i;
        double q = 1.0 - f;
        return i switch
        {
            0 => new Color(255, (byte)(f * 255), 0),
            1 => new Color((byte)(q * 255), 255, 0),
            2 => new Color(0, 255, (byte)(f * 255)),
            3 => new Color(0, (byte)(q * 255), 255),
            4 => new Color((byte)(f * 255), 0, 255),
            _ => new Color(255, 0, (byte)(q * 255)),
        };
    }
}
