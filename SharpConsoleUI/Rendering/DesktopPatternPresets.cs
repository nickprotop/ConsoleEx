namespace SharpConsoleUI.Rendering;

public static class DesktopPatterns
{
    public static DesktopPattern Checkerboard => new(new char[,]
    {
        { '░', ' ' },
        { ' ', '░' }
    });

    public static DesktopPattern Dots => new(new char[,]
    {
        { '·', ' ', ' ' },
        { ' ', ' ', '·' },
        { ' ', '·', ' ' }
    });

    public static DesktopPattern HatchDown => new(new char[,]
    {
        { '╲', ' ', ' ' },
        { ' ', '╲', ' ' },
        { ' ', ' ', '╲' }
    });

    public static DesktopPattern HatchUp => new(new char[,]
    {
        { ' ', ' ', '╱' },
        { ' ', '╱', ' ' },
        { '╱', ' ', ' ' }
    });

    public static DesktopPattern Crosshatch => new(new char[,]
    {
        { '╳', ' ', ' ' },
        { ' ', '╳', ' ' },
        { ' ', ' ', '╳' }
    });

    public static DesktopPattern LightShade => new(new char[,] { { '░' } });
    public static DesktopPattern MediumShade => new(new char[,] { { '▒' } });
    public static DesktopPattern DenseShade => new(new char[,] { { '▓' } });

    public static DesktopPattern HorizontalLines => new(new char[,]
    {
        { '─' },
        { ' ' }
    });

    public static DesktopPattern VerticalLines => new(new char[,]
    {
        { '│', ' ', ' ' }
    });

    public static DesktopPattern Grid => new(new char[,]
    {
        { '┼', '─', '─' },
        { '│', ' ', ' ' },
        { '│', ' ', ' ' }
    });
}
