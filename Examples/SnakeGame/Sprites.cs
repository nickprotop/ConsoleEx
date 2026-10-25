// -----------------------------------------------------------------------
// SnakeGame — pixel-art shapes.
//
// Shapes are authored as text ('#' = lit pixel) and parsed once into PixelMask.
// Each pixel is half a terminal cell tall (see PixelSurface), so a 4×4 fruit
// occupies 4 columns × 2 rows on screen.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drawing;

namespace SnakeGame;

internal static class Sprites
{
	/// <summary>Leaf and stalk, drawn on top of the round fruit body.</summary>
	public static readonly PixelMask FruitStalk = new(
		"..#.",
		".##.");

	/// <summary>A small skull, drawn where the snake died.</summary>
	public static readonly PixelMask Skull = new(
		".####.",
		"#.##.#",
		"######",
		".#..#.");
}

