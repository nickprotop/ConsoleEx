using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace TermTunes.UI;

/// <summary>Palette for the player.</summary>
public static class ColorScheme
{
    // Dark, slightly blue window gradient (top -> bottom).
    public static ColorGradient WindowGradient => ColorGradient.FromColors(
        new Color(18, 18, 28),
        new Color(6, 6, 10));

    public static readonly Color CardBg = new(22, 22, 34, 170);   // alpha → gradient bleeds through
    public static readonly Color PanelHeaderBg = new(40, 40, 60, 160);
    public static readonly Color SidebarBg = new(16, 16, 26, 200);

    public static readonly Color Primary = Color.Grey93;
    public static readonly Color Muted = Color.Grey50;
    public static readonly Color SeekTrack = Color.Grey27;

    public const string PrimaryMarkup = "grey93";
    public const string MutedMarkup = "grey50";

    /// <summary>Build a 0..1 gradient for a track accent (dim → accent → white-ish).</summary>
    public static ColorGradient AccentRamp(Color accent) => ColorGradient.FromColors(
        new Color((byte)(accent.R / 3), (byte)(accent.G / 3), (byte)(accent.B / 3)),
        accent,
        new Color(
            (byte)Math.Min(255, accent.R + 60),
            (byte)Math.Min(255, accent.G + 60),
            (byte)Math.Min(255, accent.B + 60)));
}
