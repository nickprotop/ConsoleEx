using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class AlphaBlendingDemoWindow
{
    private const int WindowWidth = 110;
    private const int WindowHeight = 38;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var animToggle = Controls.Checkbox("Animate background gradient")
            .WithName("animToggle")
            .Checked(true)
            .Build();

        var topBar = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col
                .Flex(3)
                .Add(Controls.Markup()
                    .AddLine("[bold]Alpha Blending Showcase[/]  [dim]— five zones, one compositor[/]")
                    .Build()))
            .Column(col => col
                .Flex(1)
                .Add(animToggle))
            .Build();

        var zone1Heading = Controls.Markup()
            .AddLine("[bold]1. Alpha Ladder[/]  [dim]same color, eight opacity levels[/]")
            .Build();

        byte[] alphaLevels = { 0, 36, 73, 109, 146, 182, 219, 255 };

        var zone1GridBuilder = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch);

        foreach (var alpha in alphaLevels)
        {
            var capturedAlpha = alpha;
            var panel = Controls.ScrollablePanel()
                .WithBackgroundColor(new Color(255, 140, 0, capturedAlpha))
                .WithBorderStyle(BorderStyle.None)
                .Build();
            panel.AddControl(Controls.Markup()
                .AddLine($"α={capturedAlpha}")
                .Build());
            zone1GridBuilder = zone1GridBuilder.Column(col => col.Flex(1).Add(panel));
        }

        var zone1Grid = zone1GridBuilder.Build();

        var zone2Heading = Controls.Markup()
            .AddLine("[bold]2. Fade to Transparent[/]  [dim]foreground alpha composites against window gradient[/]")
            .Build();

        // Fade strip: 60 block characters, each with cyan foreground alpha stepping 255→0
        const int FadeChars = 60;
        var fadeLineBuilder = new System.Text.StringBuilder();
        for (int i = 0; i < FadeChars; i++)
        {
            byte fadeAlpha = (byte)(255 - (int)(i * 255.0 / (FadeChars - 1)));
            fadeLineBuilder.Append($"[#00DCDC{fadeAlpha:X2}]█[/]");
        }

        var zone2Strip = Controls.Markup()
            .AddLine(fadeLineBuilder.ToString())
            .Build();

        var zone3Heading = Controls.Markup()
            .AddLine("[bold]3. Glass Panels[/]  [dim]Color.WithAlpha() at 25 / 50 / 75 / 100 %[/]")
            .Build();

        var zone3Grid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col => col.Flex(1).Add(MakeGlassPanel(64,  "25%")))
            .Column(col => col.Flex(1).Add(MakeGlassPanel(128, "50%")))
            .Column(col => col.Flex(1).Add(MakeGlassPanel(192, "75%")))
            .Column(col => col.Flex(1).Add(MakeGlassPanel(255, "100%")))
            .Build();

        var zone4Heading = Controls.Markup()
            .AddLine("[bold]4. Live Compositor[/]  [dim]Color.Blend(src, dst) — drag to change source alpha[/]")
            .Build();

        var alphaSlider = Controls.Slider()
            .Horizontal()
            .WithRange(0, 255)
            .WithValue(128)
            .WithName("alphaSlider")
            .WithStep(1)
            .Build();

        var alphaLabel = Controls.Markup()
            .WithName("alphaLabel")
            .Build();

        var blendPreview = Controls.Markup()
            .WithName("blendPreview")
            .Build();

        // Helper to compute and format the three-row blend preview.
        // Each string is one line — MarkupControl renders each list element as a separate row.
        static List<string> MakeBlendPreview(int alpha)
        {
            var src = new Color(255, 100, 50, (byte)alpha);
            var dst = new Color(30, 144, 255, 255);
            var blended = Color.Blend(src, dst);

            string Swatch(Color c) => $"[#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}]████[/]";

            return new List<string>
            {
                $"src  {Swatch(src)}  rgba(255,100,50,{alpha})",
                $"dst  {Swatch(dst)}  rgba(30,144,255,255)",
                $"out  {Swatch(blended)}  Color.Blend(src, dst)",
            };
        }

        // Initial state
        alphaLabel.SetContent(new List<string> { "Alpha: 128 / 255" });
        blendPreview.SetContent(MakeBlendPreview(128));

        // Wire value-changed after controls are built
        alphaSlider.ValueChanged += (_, e) =>
        {
            int a = (int)alphaSlider.Value;
            alphaLabel.SetContent(new List<string> { $"Alpha: {a} / 255" });
            blendPreview.SetContent(MakeBlendPreview(a));
        };

        var zone4Controls = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col
                .Flex(1)
                .Add(alphaSlider)
                .Add(alphaLabel))
            .Column(col => col
                .Flex(2)
                .Add(blendPreview))
            .Build();

        var zone5Heading = Controls.Markup()
            .AddLine("[bold]5. Pulse Panel[/]  [dim]alpha animated 0 → 255 → 0 via async thread[/]")
            .Build();

        var pulsePanel = Controls.ScrollablePanel()
            .WithName("pulsePanel")
            .WithBorderStyle(BorderStyle.Rounded)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();
        pulsePanel.AddControl(Controls.Markup().AddLine("background alpha pulses").Build());

        // Left column: zone 3
        var leftCol = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();
        leftCol.AddControl(zone3Heading);
        leftCol.AddControl(zone3Grid);

        // Right column: zones 4 and 5
        var rightCol = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();
        rightCol.AddControl(zone4Heading);
        rightCol.AddControl(zone4Controls);
        rightCol.AddControl(zone5Heading);
        rightCol.AddControl(pulsePanel);

        var zonesBottomRow = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col => col.Flex(1).Add(leftCol))
            .Column(col => col.Flex(1).Add(rightCol))
            .Build();

        var window = new WindowBuilder(ws)
            .WithTitle("Alpha Blending")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithBackgroundGradient(
                ColorGradient.FromColors(Color.Blue, Color.MediumPurple, Color.Orange1),
                GradientDirection.DiagonalDown)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)s!);
                    e.Handled = true;
                }
            })
            .WithAsyncWindowThread(async (win, ct) =>
            {
                var startTime = DateTime.Now;

                // Convert a hue (0.0–1.0) to a fully-saturated RGB color.
                static Color HueToColor(double h)
                {
                    h = ((h % 1.0) + 1.0) % 1.0; // normalise to [0,1)
                    double s = h * 6.0;
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

                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(50, ct);

                    double t = (DateTime.Now - startTime).TotalSeconds;

                    // Pulse panel
                    var pulse = win.FindControl<ScrollablePanelControl>("pulsePanel");
                    if (pulse != null)
                    {
                        byte a = (byte)((Math.Sin(t * Math.PI) + 1.0) / 2.0 * 255);
                        pulse.BackgroundColor = new Color(255, 50, 100, a);
                    }

                    // Smooth gradient cycle — three hues spaced 120° apart, rotating over time
                    var toggle = win.FindControl<CheckboxControl>("animToggle");
                    if (toggle?.Checked == true)
                    {
                        double phase = t * 0.08; // full hue cycle every ~12 s
                        var c1 = HueToColor(phase);
                        var c2 = HueToColor(phase + 1.0 / 3.0);
                        var c3 = HueToColor(phase + 2.0 / 3.0);
                        win.BackgroundGradient = new GradientBackground(
                            ColorGradient.FromColors(c1, c2, c3),
                            GradientDirection.DiagonalDown);
                    }
                }
            })
            .AddControl(topBar)
            .AddControl(zone1Heading)
            .AddControl(zone1Grid)
            .AddControl(zone2Heading)
            .AddControl(zone2Strip)
            .AddControl(zonesBottomRow)
            .BuildAndShow();

        return window;
    }

    private static ScrollablePanelControl MakeGlassPanel(byte alpha, string label)
    {
        var panel = Controls.ScrollablePanel()
            .WithBackgroundColor(new Color(30, 144, 255, alpha))
            .WithBorderStyle(BorderStyle.Rounded)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();
        panel.AddControl(Controls.Markup().AddLine(label).Build());
        return panel;
    }
}
