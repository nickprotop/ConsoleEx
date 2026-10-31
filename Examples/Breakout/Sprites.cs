// -----------------------------------------------------------------------
// Breakout — pixel-art shapes and the brick palette.
//
// Shapes are authored as text ('#' = lit pixel) and parsed once into
// PixelMask. Each pixel is half a terminal cell tall (see PixelSurface), so
// a 2-pixel-tall brick occupies one screen row.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drawing;

namespace Breakout;

internal static class Sprites
{
	/// <summary>The heart drawn once per spare life in the HUD rail.</summary>
	public static readonly PixelMask Heart = new(
		".#.#.",
		"#####",
		".###.",
		"..#..");

	/// <summary>A capsule, drawn for every drop falling from a broken brick.</summary>
	public static readonly PixelMask Capsule = new(
		".####.",
		"######",
		".####.");

	/// <summary>Row colours, top row first. A row's colour is also its score tier.</summary>
	public static readonly Color[] RowColors =
	{
		new(226, 70, 95),
		new(238, 128, 72),
		new(240, 190, 80),
		new(118, 206, 120),
		new(92, 170, 238),
		new(150, 130, 230),
	};
}
