// -----------------------------------------------------------------------
// SpaceInvaders — pixel-art sprites.
//
// Sprites are authored as text ('#' = lit pixel) and parsed once into masks.
// Each pixel is half a terminal cell tall (see PixelSurface), so a 7×4
// invader occupies 7 columns × 2 rows on screen.
// -----------------------------------------------------------------------

namespace SpaceInvaders;

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

	/// <summary>Returns a fresh, mutable copy of the mask (for destructible shields).</summary>
	public bool[,] CloneMask() => (bool[,])_mask.Clone();
}

internal static class Sprites
{
	public const int InvaderWidth = 7;
	public const int InvaderHeight = 4;

	// Two animation frames per invader — they swap on every formation step.

	public static readonly Sprite[] Squid =
	{
		new("..###..",
			".#####.",
			"##.#.##",
			".#.#.#."),
		new("..###..",
			".#####.",
			"##.#.##",
			"#.....#"),
	};

	public static readonly Sprite[] Crab =
	{
		new(".#...#.",
			"#######",
			"##.#.##",
			"#.#.#.#"),
		new("#.#.#.#",
			"#######",
			".#.#.#.",
			".#...#."),
	};

	public static readonly Sprite[] Octopus =
	{
		new(".#####.",
			"##.#.##",
			"#######",
			".##.##."),
		new(".#####.",
			"##.#.##",
			"#######",
			"##...##"),
	};

	public static readonly Sprite Burst = new(
		"#..#..#",
		".#.#.#.",
		"##...##",
		".#.#.#.");

	public static readonly Sprite Player = new(
		"....#....",
		"...###...",
		".#######.",
		"#########");

	public static readonly Sprite Ufo = new(
		"...#####...",
		".#########.",
		"##.##.##.##",
		"..##...##..");

	/// <summary>Bunker outline; each wave gets fresh mutable copies.</summary>
	public static readonly Sprite Shield = new(
		"..########..",
		".##########.",
		"############",
		"############",
		"####....####",
		"###......###");
}
