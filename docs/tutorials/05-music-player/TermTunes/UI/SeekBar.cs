using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;

namespace TermTunes.UI;

/// <summary>A canvas-drawn gradient progress/seek bar (ProgressBar has no gradient).</summary>
public sealed class SeekBar
{
    private readonly CanvasControl _canvas;
    public IWindowControl Control => _canvas;

    public SeekBar()
    {
        _canvas = Controls.Canvas(60, 1).Build();
        _canvas.AutoSize = true; // span the column width
    }

    /// <summary>Draw the filled (gradient) portion up to position/duration, plus a playhead.</summary>
    public void Render(TimeSpan position, TimeSpan duration, Color accent)
    {
        int w = _canvas.CanvasWidth;
        int h = _canvas.CanvasHeight;
        if (w < 2 || h < 1) return;

        double frac = duration.TotalSeconds <= 0 ? 0 : Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1);
        int filled = (int)Math.Round(frac * (w - 1));

        var g = _canvas.BeginPaint();
        try
        {
            g.Clear(new Color(10, 10, 16));
            // unfilled track
            g.FillRect(0, 0, w, h, '─', ColorScheme.SeekTrack, new Color(10, 10, 16));
            // filled gradient
            if (filled > 0)
            {
                var dim = new Color((byte)(accent.R / 2), (byte)(accent.G / 2), (byte)(accent.B / 2));
                g.GradientFillHorizontal(0, 0, filled, h, '━', dim, accent, new Color(10, 10, 16));
            }
            // playhead
            g.SetNarrowCell(Math.Min(filled, w - 1), 0, '●', Color.White, new Color(10, 10, 16));
        }
        finally
        {
            _canvas.EndPaint();
        }
    }
}
