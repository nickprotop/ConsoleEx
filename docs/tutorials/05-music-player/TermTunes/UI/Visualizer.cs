using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;

namespace TermTunes.UI;

/// <summary>Animated spectrum bars drawn on a CanvasControl. Levels are 0..1 per bar.</summary>
public sealed class Visualizer
{
    private readonly CanvasControl _canvas;
    public IWindowControl Control => _canvas;

    public Visualizer()
    {
        _canvas = Controls.Canvas(60, 10).Build();
        _canvas.AutoSize = true;
    }

    /// <summary>Render the given normalized levels (one per bar) using the accent gradient.</summary>
    public void Render(IReadOnlyList<double> levels, Color accent)
    {
        int w = _canvas.CanvasWidth;
        int h = _canvas.CanvasHeight;
        if (w < 2 || h < 1 || levels.Count == 0) return;

        var ramp = ColorScheme.AccentRamp(accent);
        var bg = new Color(10, 10, 16);

        var g = _canvas.BeginPaint();
        try
        {
            g.Clear(bg);
            int bars = Math.Min(levels.Count, w);
            int barW = Math.Max(1, w / bars);
            for (int b = 0; b < bars; b++)
            {
                double level = Math.Clamp(levels[b], 0, 1);
                int barHeight = (int)Math.Round(level * h);
                for (int y = 0; y < barHeight; y++)
                {
                    double t = h <= 1 ? 1 : (double)y / (h - 1);
                    var color = ramp.Interpolate(t);
                    int row = h - 1 - y;
                    for (int dx = 0; dx < barW; dx++)
                    {
                        int x = b * barW + dx;
                        if (x < w) g.SetNarrowCell(x, row, '█', color, bg);
                    }
                }
            }
        }
        finally
        {
            _canvas.EndPaint();
        }
    }
}
