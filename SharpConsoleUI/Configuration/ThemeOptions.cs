// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for themes
/// </summary>
public sealed record ThemeOptions
{
    /// <summary>
    /// Gets or sets the desktop background character
    /// </summary>
    public char DesktopBackgroundChar { get; init; } = ' ';

    /// <summary>
    /// Gets or sets the desktop background color name
    /// </summary>
    public string DesktopBackgroundColor { get; init; } = "Black";

    /// <summary>
    /// Gets or sets the desktop foreground color name
    /// </summary>
    public string DesktopForegroundColor { get; init; } = "White";

    /// <summary>
    /// Gets or sets the window background color name
    /// </summary>
    public string WindowBackgroundColor { get; init; } = "Grey15";

    /// <summary>
    /// Gets or sets the window foreground color name
    /// </summary>
    public string WindowForegroundColor { get; init; } = "White";

    /// <summary>
    /// Gets or sets the active border foreground color name
    /// </summary>
    public string ActiveBorderForegroundColor { get; init; } = "Green";

    /// <summary>
    /// Gets or sets the inactive border foreground color name
    /// </summary>
    public string InactiveBorderForegroundColor { get; init; } = "Grey";

    /// <summary>
    /// Gets or sets the active title foreground color name
    /// </summary>
    public string ActiveTitleForegroundColor { get; init; } = "Green";

    /// <summary>
    /// Gets or sets the inactive title foreground color name
    /// </summary>
    public string InactiveTitleForegroundColor { get; init; } = "Grey";

    /// <summary>
    /// Gets or sets the button background color name
    /// </summary>
    public string ButtonBackgroundColor { get; init; } = "Grey39";

    /// <summary>
    /// Gets or sets the button foreground color name
    /// </summary>
    public string ButtonForegroundColor { get; init; } = "White";

    /// <summary>
    /// Gets or sets the button focused background color name
    /// </summary>
    public string ButtonFocusedBackgroundColor { get; init; } = "Blue";

    /// <summary>
    /// Gets or sets the button focused foreground color name
    /// </summary>
    public string ButtonFocusedForegroundColor { get; init; } = "White";

    /// <summary>
    /// Gets or sets whether to use double line borders for modal windows
    /// </summary>
    public bool UseDoubleLineBorderForModal { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to show modal window shadows
    /// </summary>
    public bool ShowModalShadow { get; init; } = true;

    /// <summary>
    /// Gets or sets custom color definitions
    /// </summary>
    public Dictionary<string, string> CustomColors { get; init; } = new();
}

/// <summary>
/// Extension methods for theme configuration
/// </summary>
public static class ThemeOptionsExtensions
{
    /// <summary>
    /// Converts a color name to a Spectre.Console Color
    /// </summary>
    /// <param name="colorName">The color name</param>
    /// <returns>The converted color</returns>
    public static Color ToSpectreColor(this string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
            return Color.White;

        // Try to parse as a standard color name
        if (Enum.TryParse<Color>(colorName, true, out var color))
            return color;

        // Try to parse as hex color
        if (colorName.StartsWith('#') && colorName.Length == 7)
        {
            try
            {
                var r = Convert.ToByte(colorName.Substring(1, 2), 16);
                var g = Convert.ToByte(colorName.Substring(3, 2), 16);
                var b = Convert.ToByte(colorName.Substring(5, 2), 16);
                return new Color(r, g, b);
            }
            catch
            {
                return Color.White;
            }
        }

        // Default fallback
        return Color.White;
    }

    /// <summary>
    /// Creates a theme from theme options
    /// </summary>
    /// <param name="options">The theme options</param>
    /// <returns>A configured theme</returns>
    public static Themes.Theme ToTheme(this ThemeOptions options)
    {
        return new Themes.Theme
        {
            DesktopBackroundChar = options.DesktopBackgroundChar,
            DesktopBackgroundColor = options.DesktopBackgroundColor.ToSpectreColor(),
            DesktopForegroundColor = options.DesktopForegroundColor.ToSpectreColor(),
            WindowBackgroundColor = options.WindowBackgroundColor.ToSpectreColor(),
            WindowForegroundColor = options.WindowForegroundColor.ToSpectreColor(),
            ActiveBorderForegroundColor = options.ActiveBorderForegroundColor.ToSpectreColor(),
            InactiveBorderForegroundColor = options.InactiveBorderForegroundColor.ToSpectreColor(),
            ActiveTitleForegroundColor = options.ActiveTitleForegroundColor.ToSpectreColor(),
            InactiveTitleForegroundColor = options.InactiveTitleForegroundColor.ToSpectreColor(),
            ButtonBackgroundColor = options.ButtonBackgroundColor.ToSpectreColor(),
            ButtonForegroundColor = options.ButtonForegroundColor.ToSpectreColor(),
            ButtonFocusedBackgroundColor = options.ButtonFocusedBackgroundColor.ToSpectreColor(),
            ButtonFocusedForegroundColor = options.ButtonFocusedForegroundColor.ToSpectreColor(),
            UseDoubleLineBorderForModal = options.UseDoubleLineBorderForModal,
            ShowModalShadow = options.ShowModalShadow
        };
    }
}