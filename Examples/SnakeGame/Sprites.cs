// -----------------------------------------------------------------------
// SnakeGame — pixel-art sprites.
//
// Sprites are authored as text ('#' = lit pixel) and parsed once into masks.
// Each pixel is half a terminal cell tall (see PixelSurface), so a 4×4 fruit
// occupies 4 columns × 2 rows on screen.
// -----------------------------------------------------------------------

namespace SnakeGame;

/// <summary>An immutable 1-bit pixel mask.</summary>
internal sealed class Sprite
{
	private readonly bool[,] _mask;

	public int Width { get; }
	public int Height { get; }

	public Sprite(params string[] rows)
	{
		Height = rows.Length;
		Width = rows.Max(r => r.Length);
		_mask = new bool[Width, Height];
		for (int y = 0; y < Height; y++)
			for (int x = 0; x < rows[y].Length; x++)
				_mask[x, y] = rows[y][x] == '#';
	}

	public bool this[int x, int y] =>
		(uint)x < (uint)Width && (uint)y < (uint)Height && _mask[x, y];
}

internal static class Sprites
{
	/// <summary>Leaf and stalk, drawn on top of the round fruit body.</summary>
	public static readonly Sprite FruitStalk = new(
		"..#.",
		".##.");

	/// <summary>A small skull, drawn where the snake died.</summary>
	public static readonly Sprite Skull = new(
		".####.",
		"#.##.#",
		"######",
		".#..#.");
}
