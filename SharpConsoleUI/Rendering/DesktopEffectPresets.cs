using Spectre.Console;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Rendering;

/// <summary>
/// Built-in animated effect presets for desktop backgrounds.
/// Each method returns a <see cref="DesktopBackgroundConfig"/> with a PaintCallback
/// that produces the named animation effect.
/// </summary>
public static class DesktopEffects
{
    /// <summary>
    /// Smoothly cycles through hues over time using HSL color space.
    /// Three hue-shifted colors are computed from elapsed time and used to build a
    /// <see cref="ColorGradient"/> that fills the desktop.
    /// </summary>
    /// <param name="cycleDurationSeconds">How long a full hue cycle takes in seconds. Default: 12.</param>
    /// <param name="direction">Gradient direction. Default: Vertical.</param>
    /// <param name="intervalMs">Animation repaint interval in milliseconds. Default: 100.</param>
    /// <returns>A <see cref="DesktopBackgroundConfig"/> configured for the color-cycling effect.</returns>
    public static DesktopBackgroundConfig ColorCycling(
        double cycleDurationSeconds = 12,
        GradientDirection direction = GradientDirection.Vertical,
        int intervalMs = 100)
    {
        return new DesktopBackgroundConfig
        {
            AnimationIntervalMs = intervalMs,
            PaintCallback = (CharacterBuffer buffer, int width, int height, TimeSpan elapsed) =>
            {
                double cycleSeconds = cycleDurationSeconds > 0 ? cycleDurationSeconds : 12;
                double t = elapsed.TotalSeconds / cycleSeconds;

                // Compute 3 hues evenly spaced and shifted by time
                double h0 = t % 1.0;
                double h1 = (t + 1.0 / 3.0) % 1.0;
                double h2 = (t + 2.0 / 3.0) % 1.0;

                var c0 = HslToColor(h0, 0.8, 0.55);
                var c1 = HslToColor(h1, 0.8, 0.55);
                var c2 = HslToColor(h2, 0.8, 0.55);

                var gradient = ColorGradient.FromColors(c0, c1, c2);
                var rect = new LayoutRect(0, 0, width, height);
                GradientRenderer.FillGradientBackground(buffer, rect, gradient, direction);
            }
        };
    }

    /// <summary>
    /// Subtle pulsing brightness on a base color using a sine wave.
    /// The brightness oscillates around the base color's perceived luminance.
    /// </summary>
    /// <param name="baseColor">The base color to pulse.</param>
    /// <param name="pulseRange">How far the brightness varies above and below the base. Default: 0.15.</param>
    /// <param name="pulseDurationSeconds">Duration of one full pulse cycle in seconds. Default: 4.</param>
    /// <param name="intervalMs">Animation repaint interval in milliseconds. Default: 100.</param>
    /// <returns>A <see cref="DesktopBackgroundConfig"/> configured for the pulse effect.</returns>
    public static DesktopBackgroundConfig Pulse(
        Color baseColor,
        double pulseRange = 0.15,
        double pulseDurationSeconds = 4,
        int intervalMs = 100)
    {
        return new DesktopBackgroundConfig
        {
            AnimationIntervalMs = intervalMs,
            PaintCallback = (CharacterBuffer buffer, int width, int height, TimeSpan elapsed) =>
            {
                double cycleSec = pulseDurationSeconds > 0 ? pulseDurationSeconds : 4;
                double phase = elapsed.TotalSeconds / cycleSec * 2.0 * Math.PI;
                double brightnessMult = 1.0 + pulseRange * Math.Sin(phase);

                byte r = (byte)Math.Clamp(baseColor.R * brightnessMult, 0, 255);
                byte g = (byte)Math.Clamp(baseColor.G * brightnessMult, 0, 255);
                byte b = (byte)Math.Clamp(baseColor.B * brightnessMult, 0, 255);

                var pulseColor = new Color(r, g, b);
                var rect = new LayoutRect(0, 0, width, height);
                buffer.FillRect(rect, pulseColor);
            }
        };
    }

    /// <summary>
    /// A two-color gradient that cycles through all four <see cref="GradientDirection"/> values over time.
    /// The gradient color order is alternated between direction steps for smooth visual transitions.
    /// </summary>
    /// <param name="color1">First gradient color.</param>
    /// <param name="color2">Second gradient color.</param>
    /// <param name="cycleDurationSeconds">How long a full direction cycle takes in seconds. Default: 8.</param>
    /// <param name="intervalMs">Animation repaint interval in milliseconds. Default: 150.</param>
    /// <returns>A <see cref="DesktopBackgroundConfig"/> configured for the drifting gradient effect.</returns>
    public static DesktopBackgroundConfig DriftingGradient(
        Color color1,
        Color color2,
        double cycleDurationSeconds = 8,
        int intervalMs = 150)
    {
        var directions = new[]
        {
            GradientDirection.Horizontal,
            GradientDirection.DiagonalDown,
            GradientDirection.Vertical,
            GradientDirection.DiagonalUp
        };

        return new DesktopBackgroundConfig
        {
            AnimationIntervalMs = intervalMs,
            PaintCallback = (CharacterBuffer buffer, int width, int height, TimeSpan elapsed) =>
            {
                double cycleSec = cycleDurationSeconds > 0 ? cycleDurationSeconds : 8;
                double t = elapsed.TotalSeconds / cycleSec;

                // Determine which direction step we're in (4 steps per cycle)
                int step = (int)(t * directions.Length) % directions.Length;
                var direction = directions[step];

                // Alternate color order for smooth transition feel
                bool swapped = step % 2 == 1;
                var gradient = swapped
                    ? ColorGradient.FromColors(color2, color1)
                    : ColorGradient.FromColors(color1, color2);

                var rect = new LayoutRect(0, 0, width, height);
                GradientRenderer.FillGradientBackground(buffer, rect, gradient, direction);
            }
        };
    }

    /// <summary>
    /// Converts an HSL color value to a <see cref="Color"/>.
    /// </summary>
    /// <param name="h">Hue in [0, 1).</param>
    /// <param name="s">Saturation in [0, 1].</param>
    /// <param name="l">Lightness in [0, 1].</param>
    /// <returns>The corresponding <see cref="Color"/>.</returns>
    private static Color HslToColor(double h, double s, double l)
    {
        h = ((h % 1.0) + 1.0) % 1.0; // Normalise to [0, 1)

        double r, g, b;

        if (s == 0.0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            r = HueToComponent(p, q, h + 1.0 / 3.0);
            g = HueToComponent(p, q, h);
            b = HueToComponent(p, q, h - 1.0 / 3.0);
        }

        return new Color(
            (byte)Math.Clamp(r * 255.0, 0, 255),
            (byte)Math.Clamp(g * 255.0, 0, 255),
            (byte)Math.Clamp(b * 255.0, 0, 255));
    }

    private static double HueToComponent(double p, double q, double t)
    {
        t = ((t % 1.0) + 1.0) % 1.0; // Wrap to [0, 1)
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }
}
