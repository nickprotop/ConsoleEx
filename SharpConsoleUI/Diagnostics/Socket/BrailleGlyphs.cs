// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// Draws the 256 braille patterns from geometry, for screenshots.
/// </summary>
/// <remarks>
/// <para>
/// A terminal renders braille by composing several font faces: the one it uses
/// for text usually has no braille, and fontconfig falls through to one that
/// does. The embedded screenshot face cannot do that - consulting installed
/// fonts is exactly the machine dependence <see cref="Surfaces.FontSource.Embedded"/>
/// exists to remove - and DejaVu Sans Mono is the one monospace face in its
/// family with none of the 256 patterns. So a braille chart photographed blank
/// while the running app drew it correctly.
/// </para>
/// <para>
/// Braille is the one block where drawing beats embedding: eight dots on a 2x4
/// grid is pure geometry, so generating them costs no bytes in the package, asks
/// no licence question, and renders identically on every machine - which is the
/// guarantee the embedded mode is for. The dot RADIUS scales with the cell, so
/// the pattern reads at any size rather than becoming a smudge or a sparse grid.
/// </para>
/// </remarks>
internal static class BrailleGlyphs
{
	/// <summary>First codepoint of the Braille Patterns block.</summary>
	internal const int First = 0x2800;

	/// <summary>Last codepoint of the Braille Patterns block.</summary>
	internal const int Last = 0x28FF;

	/// <summary>Dot columns in a braille cell.</summary>
	private const int Columns = 2;

	/// <summary>Dot rows in a braille cell.</summary>
	private const int Rows = 4;

	/// <summary>
	/// Bit position of each dot, by (column, row).
	/// </summary>
	/// <remarks>
	/// Unicode numbers the dots down the left column then down the right, but
	/// bits 6 and 7 are the eight-dot extension and sit at the BOTTOM of each
	/// column rather than continuing the sequence. Reading the block as a plain
	/// row-major grid puts the lowest two dots in the wrong places.
	/// </remarks>
	private static readonly int[,] DotBit =
	{
		// column 0: dots 1,2,3 then 7     column 1: dots 4,5,6 then 8
		{ 0, 3 },
		{ 1, 4 },
		{ 2, 5 },
		{ 6, 7 },
	};

	/// <summary>Whether this codepoint is a braille pattern.</summary>
	internal static bool Covers(int codepoint) => codepoint >= First && codepoint <= Last;

	/// <summary>
	/// Renders one pattern as an alpha bitmap filling the cell.
	/// </summary>
	/// <param name="codepoint">A codepoint in the Braille Patterns block.</param>
	/// <param name="cellWidth">Cell width in pixels.</param>
	/// <param name="cellHeight">Cell height in pixels.</param>
	/// <returns>
	/// Coverage values, row-major, <paramref name="cellWidth"/> by
	/// <paramref name="cellHeight"/>; empty for U+2800, which has no dots.
	/// </returns>
	internal static byte[] Render(int codepoint, int cellWidth, int cellHeight)
	{
		if (!Covers(codepoint) || cellWidth <= 0 || cellHeight <= 0)
			return Array.Empty<byte>();

		int pattern = codepoint - First;
		if (pattern == 0) return Array.Empty<byte>();

		var alpha = new byte[cellWidth * cellHeight];

		// Each dot owns a sub-rectangle of the cell and is drawn centred in it, so
		// the dots stay evenly spaced however the cell divides.
		double cellW = (double)cellWidth / Columns;
		double cellH = (double)cellHeight / Rows;

		// Radius from whichever sub-cell dimension is tighter, so a tall narrow
		// cell gives round dots rather than vertical bars. The 0.36 leaves a gap
		// between neighbours: touching dots read as a solid block, which loses
		// exactly the information the pattern carries.
		double radius = Math.Max(0.5, Math.Min(cellW, cellH) * 0.36);

		for (int row = 0; row < Rows; row++)
		{
			for (int col = 0; col < Columns; col++)
			{
				if ((pattern & (1 << DotBit[row, col])) == 0) continue;

				double cx = (col + 0.5) * cellW;
				double cy = (row + 0.5) * cellH;
				Stamp(alpha, cellWidth, cellHeight, cx, cy, radius);
			}
		}

		return alpha;
	}

	/// <summary>Draws one antialiased dot into the coverage buffer.</summary>
	/// <remarks>
	/// Coverage falls off across the final pixel of the radius rather than
	/// stopping dead, because a hard edge on a 3px dot reads as a square.
	/// </remarks>
	private static void Stamp(byte[] alpha, int width, int height,
		double cx, double cy, double radius)
	{
		int x0 = Math.Max(0, (int)Math.Floor(cx - radius - 1));
		int x1 = Math.Min(width - 1, (int)Math.Ceiling(cx + radius + 1));
		int y0 = Math.Max(0, (int)Math.Floor(cy - radius - 1));
		int y1 = Math.Min(height - 1, (int)Math.Ceiling(cy + radius + 1));

		for (int y = y0; y <= y1; y++)
		{
			for (int x = x0; x <= x1; x++)
			{
				// Pixel centres, so a dot centred on a boundary is symmetric.
				double dx = x + 0.5 - cx;
				double dy = y + 0.5 - cy;
				double distance = Math.Sqrt(dx * dx + dy * dy);

				double coverage = radius + 0.5 - distance;
				if (coverage <= 0) continue;

				int value = (int)Math.Round(Math.Min(1.0, coverage) * 255);
				int i = y * width + x;
				if (value > alpha[i]) alpha[i] = (byte)value;
			}
		}
	}
}
