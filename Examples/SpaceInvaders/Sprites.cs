// -----------------------------------------------------------------------
// SpaceInvaders — pixel-art shapes.
//
// Shapes are authored as text ('#' = lit pixel) and parsed once into PixelMask.
// Each pixel is half a terminal cell tall (see PixelSurface), so a 7×4
// invader occupies 7 columns × 2 rows on screen.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drawing;

namespace SpaceInvaders;

internal static class Sprites
{
	public const int InvaderWidth = 7;
	public const int InvaderHeight = 4;

	// Two animation frames per invader — they swap on every formation step.

	public static readonly PixelMask[] Squid =
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

	public static readonly PixelMask[] Crab =
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

	public static readonly PixelMask[] Octopus =
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

	public static readonly PixelMask Burst = new(
		"#..#..#",
		".#.#.#.",
		"##...##",
		".#.#.#.");

	public static readonly PixelMask Player = new(
		"....#....",
		"...###...",
		".#######.",
		"#########");

	public static readonly PixelMask Ufo = new(
		"...#####...",
		".#########.",
		"##.##.##.##",
		"..##...##..");

	/// <summary>Bunker outline; each wave gets fresh mutable copies.</summary>
	public static readonly PixelMask Shield = new(
		"..########..",
		".##########.",
		"############",
		"############",
		"####....####",
		"###......###");
}

