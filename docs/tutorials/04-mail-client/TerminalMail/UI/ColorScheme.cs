using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace TerminalMail.UI;

/// <summary>Centralized palette (mirrors the CXPost convention).</summary>
public static class ColorScheme
{
    // Window gradient (dark blue → near-black), like CXPost.
    public static ColorGradient WindowGradient => ColorGradient.FromColors(
        new Color(25, 32, 52),
        new Color(7, 7, 13));

    // Semi-transparent panel headers let the gradient bleed through (per-cell alpha).
    public static readonly Color PanelHeaderBg = new(40, 50, 70, 160);

    // Borders / accents
    public static readonly Color ActiveBorder = Color.SteelBlue;
    public static readonly Color InactiveBorder = Color.Grey23;

    // Panel backgrounds
    public static readonly Color SidebarBg = new(18, 22, 34, 200);
    public static readonly Color ReadingBg = new(12, 14, 22, 200);

    // Text
    public static readonly Color Primary = Color.Cyan1;
    public static readonly Color Muted = Color.Grey50;
    public static readonly Color Body = Color.Grey85;

    // Markup strings (for inline [..] usage)
    public const string PrimaryMarkup = "cyan1";
    public const string MutedMarkup = "grey50";
    public const string AccentMarkup = "steelblue1";
}
