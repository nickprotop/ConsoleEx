// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drawing;

/// <summary>
/// An immutable 1-bit shape, authored as text and drawn onto a <see cref="PixelSurface"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each row is a string; <c>'#'</c> lights a pixel and anything else leaves it
/// clear. Rows may be ragged — the mask is as wide as its longest row and missing
/// cells read as clear — so a shape can be written without padding every line.
/// </para>
/// <para>
/// Games call these sprites, but nothing here is about games: a 1-bit mask is how
/// you draw an icon, a logo, a legend swatch, a chart marker or a custom cursor at
/// pixel resolution. The name says what it is rather than what it is for.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// static readonly PixelMask Arrow = new(
///     "..#..",
///     ".###.",
///     "#####",
///     "..#..");
///
/// surface.Draw(Arrow, x, y, Color.Yellow);
/// </code>
/// </example>
public sealed class PixelMask
{
	/// <summary>The character that lights a pixel.</summary>
	public const char Lit = '#';

	private readonly bool[,] _mask;

	/// <summary>Width in pixels: the length of the longest row.</summary>
	public int Width { get; }

	/// <summary>Height in pixels: the number of rows.</summary>
	public int Height { get; }

	/// <summary>
	/// Builds a mask from text rows, where <c>'#'</c> is a lit pixel.
	/// </summary>
	/// <param name="rows">One string per pixel row. Ragged rows are padded with clear pixels.</param>
	/// <exception cref="ArgumentException">No rows were given.</exception>
	public PixelMask(params string[] rows)
	{
		ArgumentNullException.ThrowIfNull(rows);
		if (rows.Length == 0)
			throw new ArgumentException("a mask needs at least one row", nameof(rows));

		Height = rows.Length;

		int width = 0;
		foreach (string row in rows) width = Math.Max(width, row.Length);
		Width = width;

		_mask = new bool[Width, Height];
		for (int y = 0; y < Height; y++)
			for (int x = 0; x < rows[y].Length; x++)
				_mask[x, y] = rows[y][x] == Lit;
	}

	/// <summary>True when the pixel is lit. Out-of-range coordinates read as clear.</summary>
	public bool this[int x, int y] =>
		(uint)x < (uint)Width && (uint)y < (uint)Height && _mask[x, y];

	/// <summary>
	/// Returns a fresh, mutable copy of the mask.
	/// </summary>
	/// <remarks>
	/// For a shape that erodes as it is used - a destructible shield, a progressive
	/// reveal. The mask itself stays immutable so it can be shared and cached.
	/// </remarks>
	public bool[,] CloneMask() => (bool[,])_mask.Clone();
}
