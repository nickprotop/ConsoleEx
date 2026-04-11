using Spectre.Console;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Rendering
{
    /// <summary>
    /// Configuration for the desktop background. Layers compose in order:
    /// 1. Base fill (char + fg/bg from theme)
    /// 2. Gradient overlay (if set)
    /// 3. Pattern overlay (if set)
    /// When a gradient is active, the theme's DesktopBackgroundChar is rendered
    /// with gradient-interpolated colors. When both gradient and pattern are set,
    /// gradient colors are applied first, then pattern chars overwrite the character grid.
    /// </summary>
    public sealed record DesktopBackgroundConfig
    {
        /// <summary>Optional solid background color. Overrides theme DesktopBackgroundColor when set.</summary>
        public Color? BackgroundColor { get; init; }

        /// <summary>Optional gradient applied to the desktop. Overrides solid color.</summary>
        public GradientBackground? Gradient { get; init; }

        /// <summary>Optional repeating pattern rendered on top of the gradient/solid fill.</summary>
        public DesktopPattern? Pattern { get; init; }

        /// <summary>
        /// Optional callback for animated/dynamic backgrounds. Called on a timer to paint
        /// into the desktop buffer. When set, the desktop buffer is repainted at
        /// AnimationIntervalMs intervals.
        /// Parameters: (CharacterBuffer buffer, int width, int height, TimeSpan elapsed)
        /// </summary>
        public Action<CharacterBuffer, int, int, TimeSpan>? PaintCallback { get; init; }

        /// <summary>Animation repaint interval in milliseconds. Default: 100 (10 FPS).</summary>
        public int AnimationIntervalMs { get; init; } = 100;

        /// <summary>Creates an empty config (uses theme defaults).</summary>
        public static DesktopBackgroundConfig Default => new();

        /// <summary>Creates a config with just a solid color.</summary>
        public static DesktopBackgroundConfig FromColor(Color color)
            => new() { BackgroundColor = color };

        /// <summary>Creates a config with just a gradient.</summary>
        public static DesktopBackgroundConfig FromGradient(ColorGradient gradient, GradientDirection direction)
            => new() { Gradient = new GradientBackground(gradient, direction) };

        /// <summary>Creates a config with just a pattern.</summary>
        public static DesktopBackgroundConfig FromPattern(DesktopPattern pattern)
            => new() { Pattern = pattern };
    }

    /// <summary>
    /// A repeating tile pattern for the desktop background.
    /// The tile is defined as a 2D grid of characters with optional per-cell colors.
    /// The tile repeats across the entire desktop area.
    /// </summary>
    public sealed class DesktopPattern
    {
        /// <summary>The character grid (row-major). Tile dimensions are inferred from this.</summary>
        public char[,] Characters { get; }

        /// <summary>Per-cell foreground colors. Null entries use the base foreground.</summary>
        public Color?[,]? ForegroundColors { get; init; }

        /// <summary>Per-cell background colors. Null entries use the base background.</summary>
        public Color?[,]? BackgroundColors { get; init; }

        /// <summary>Tile width (columns).</summary>
        public int Width => Characters.GetLength(1);

        /// <summary>Tile height (rows).</summary>
        public int Height => Characters.GetLength(0);

        /// <summary>
        /// Creates a new pattern from a character grid.
        /// </summary>
        /// <param name="characters">Row-major character grid. Must be at least 1x1.</param>
        public DesktopPattern(char[,] characters)
        {
            if (characters.GetLength(0) == 0 || characters.GetLength(1) == 0)
                throw new ArgumentException("Pattern must have at least 1x1 dimensions.", nameof(characters));
            Characters = characters;
        }
    }
}
