namespace SharpConsoleUI.Rendering;

/// <summary>
/// Built-in desktop pattern presets for use with <see cref="DesktopBackgroundConfig.Pattern"/>.
/// </summary>
public static class DesktopPatterns
{
    /// <summary>Alternating filled/empty blocks: ░ and space.</summary>
    public static DesktopPattern Checkerboard => new(new char[,]
    {
        { '░', ' ' },
        { ' ', '░' }
    });

    /// <summary>Sparse dots on empty background.</summary>
    public static DesktopPattern Dots => new(new char[,]
    {
        { '·', ' ', ' ' },
        { ' ', ' ', '·' },
        { ' ', '·', ' ' }
    });

    /// <summary>Diagonal hatching lines going down-right.</summary>
    public static DesktopPattern HatchDown => new(new char[,]
    {
        { '╲', ' ', ' ' },
        { ' ', '╲', ' ' },
        { ' ', ' ', '╲' }
    });

    /// <summary>Diagonal hatching lines going up-right.</summary>
    public static DesktopPattern HatchUp => new(new char[,]
    {
        { ' ', ' ', '╱' },
        { ' ', '╱', ' ' },
        { '╱', ' ', ' ' }
    });

    /// <summary>Cross-hatching (both diagonal directions).</summary>
    public static DesktopPattern Crosshatch => new(new char[,]
    {
        { '╳', ' ', ' ' },
        { ' ', '╳', ' ' },
        { ' ', ' ', '╳' }
    });

    /// <summary>Light shade fill (░).</summary>
    public static DesktopPattern LightShade => new(new char[,] { { '░' } });

    /// <summary>Medium shade fill (▒).</summary>
    public static DesktopPattern MediumShade => new(new char[,] { { '▒' } });

    /// <summary>Dense shade fill (▓).</summary>
    public static DesktopPattern DenseShade => new(new char[,] { { '▓' } });

    /// <summary>Horizontal lines every 2 rows.</summary>
    public static DesktopPattern HorizontalLines => new(new char[,]
    {
        { '─' },
        { ' ' }
    });

    /// <summary>Vertical lines every 3 columns.</summary>
    public static DesktopPattern VerticalLines => new(new char[,]
    {
        { '│', ' ', ' ' }
    });

    /// <summary>Grid of thin lines.</summary>
    public static DesktopPattern Grid => new(new char[,]
    {
        { '┼', '─', '─' },
        { '│', ' ', ' ' },
        { '│', ' ', ' ' }
    });
}
