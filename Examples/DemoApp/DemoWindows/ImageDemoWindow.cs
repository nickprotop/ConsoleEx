using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

internal static class ImageDemoWindow
{
    #region Constants

    private const int WindowWidth = 65;
    private const int WindowHeight = 24;
    private const int ButtonWidth = 22;
    private const int ButtonLeftMargin = 2;
    private const int SectionLabelLeftMargin = 1;
    private const int SectionTopMargin = 1;
    private const int PatternSize = 48;
    private const int HalfPatternSize = PatternSize / 2;
    private const int CircleRadius = 20;
    private const int TriangleBase = 40;
    private const byte FullChannel = 255;

    #endregion

    private static readonly string[] PatternNames = { "Rainbow Bars", "Checkerboard", "Shapes" };
    private static readonly ImageScaleMode[] ScaleModes = { ImageScaleMode.Fit, ImageScaleMode.Fill, ImageScaleMode.Stretch, ImageScaleMode.None };
    private static readonly string[] ScaleModeLabels = { "Fit", "Fill", "Stretch", "None" };

    public static Window Create(ConsoleWindowSystem ws)
    {
        int patternIndex = 0;
        int scaleModeIndex = 0;

        var patterns = new PixelBuffer[] { CreateRainbowBars(), CreateCheckerboard(), CreateShapes() };

        var imageControl = Controls.Image(patterns[patternIndex]);
        imageControl.ScaleMode = ScaleModes[scaleModeIndex];
        imageControl.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

        var statusLabel = Controls.Markup()
            .AddLine($"[dim]Pattern:[/] [bold]{PatternNames[patternIndex]}[/]  [dim]Scale:[/] [bold]{ScaleModeLabels[scaleModeIndex]}[/]")
            .WithMargin(SectionLabelLeftMargin, SectionTopMargin, 0, 0)
            .Build();

        Window? window = null;

        var cyclePatternBtn = Controls.Button("Next Pattern")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                patternIndex = (patternIndex + 1) % patterns.Length;
                imageControl.Source = patterns[patternIndex];
                UpdateStatusLabel(statusLabel, patternIndex, scaleModeIndex);
            })
            .Build();
        cyclePatternBtn.Margin = new Margin { Left = ButtonLeftMargin, Top = SectionTopMargin };

        var cycleScaleBtn = Controls.Button("Next Scale Mode")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                scaleModeIndex = (scaleModeIndex + 1) % ScaleModes.Length;
                imageControl.ScaleMode = ScaleModes[scaleModeIndex];
                UpdateStatusLabel(statusLabel, patternIndex, scaleModeIndex);
            })
            .Build();
        cycleScaleBtn.Margin = new Margin { Left = ButtonLeftMargin };

        window = new WindowBuilder(ws)
            .WithTitle("Image Rendering Demo")
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
            .AddControl(Controls.Markup("[bold underline]Image Control Showcase[/]")
                .Centered()
                .Build())
            .AddControl(statusLabel)
            .AddControl(cyclePatternBtn)
            .AddControl(cycleScaleBtn)
            .AddControl(imageControl)
            .BuildAndShow();

        return window;
    }

    private static void UpdateStatusLabel(MarkupControl label, int patternIdx, int scaleIdx)
    {
        label.SetContent(new List<string>
        {
            $"[dim]Pattern:[/] [bold]{PatternNames[patternIdx]}[/]  [dim]Scale:[/] [bold]{ScaleModeLabels[scaleIdx]}[/]"
        });
    }

    #region Test Pattern Generators

    private static PixelBuffer CreateRainbowBars()
    {
        var buffer = new PixelBuffer(PatternSize, PatternSize);
        var colors = new (byte R, byte G, byte B)[]
        {
            (FullChannel, 0, 0),
            (FullChannel, FullChannel, 0),
            (0, FullChannel, 0),
            (0, FullChannel, FullChannel),
            (0, 0, FullChannel),
            (FullChannel, 0, FullChannel)
        };

        int barHeight = PatternSize / colors.Length;
        for (int y = 0; y < PatternSize; y++)
        {
            int colorIndex = Math.Min(y / Math.Max(barHeight, 1), colors.Length - 1);
            var (r, g, b) = colors[colorIndex];
            for (int x = 0; x < PatternSize; x++)
            {
                buffer.SetPixel(x, y, new ImagePixel(r, g, b));
            }
        }

        return buffer;
    }

    private static PixelBuffer CreateCheckerboard()
    {
        var buffer = new PixelBuffer(PatternSize, PatternSize);
        const int cellSize = 6;

        for (int y = 0; y < PatternSize; y++)
        {
            for (int x = 0; x < PatternSize; x++)
            {
                bool isLight = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                byte val = isLight ? FullChannel : (byte)40;
                buffer.SetPixel(x, y, new ImagePixel(val, val, val));
            }
        }

        return buffer;
    }

    private static PixelBuffer CreateShapes()
    {
        var buffer = new PixelBuffer(PatternSize, PatternSize);

        // Dark background
        for (int y = 0; y < PatternSize; y++)
            for (int x = 0; x < PatternSize; x++)
                buffer.SetPixel(x, y, new ImagePixel(20, 20, 30));

        // Circle (top-left quadrant area)
        int cx = HalfPatternSize / 2;
        int cy = HalfPatternSize / 2;
        int radiusSq = CircleRadius * CircleRadius;
        for (int y = 0; y < PatternSize; y++)
        {
            for (int x = 0; x < HalfPatternSize; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= radiusSq)
                    buffer.SetPixel(x, y, new ImagePixel(FullChannel, 80, 80));
            }
        }

        // Triangle (right half)
        int triTopY = 4;
        int triBottomY = triTopY + TriangleBase;
        int triCenterX = HalfPatternSize + HalfPatternSize / 2;
        for (int y = triTopY; y < triBottomY && y < PatternSize; y++)
        {
            int progress = y - triTopY;
            int halfWidth = progress / 2;
            for (int x = triCenterX - halfWidth; x <= triCenterX + halfWidth; x++)
            {
                if (x >= 0 && x < PatternSize)
                    buffer.SetPixel(x, y, new ImagePixel(80, 200, 80));
            }
        }

        return buffer;
    }

    #endregion
}
