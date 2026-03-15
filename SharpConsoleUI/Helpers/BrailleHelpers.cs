// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Shared static helpers for pixel-grid braille rendering.
	/// Provides Bresenham's line algorithm and braille character mapping
	/// for controls that render using Unicode Braille patterns (U+2800 block).
	/// </summary>
	public static class BrailleHelpers
	{
		/// <summary>
		/// Number of horizontal pixels per terminal cell in braille mode.
		/// </summary>
		public const int DotsPerCellWidth = 2;

		/// <summary>
		/// Number of vertical pixels per terminal cell in braille mode.
		/// </summary>
		public const int DotsPerCellHeight = 4;

		/// <summary>
		/// Unicode Braille base character (all dots off).
		/// </summary>
		public const char BrailleEmpty = '\u2800';

		// Braille dot bit positions mapping: pixel index (row*2+col) -> bit position
		// Braille pattern dots are numbered:
		//   1 4    -> bits 0, 3
		//   2 5    -> bits 1, 4
		//   3 6    -> bits 2, 5
		//   7 8    -> bits 6, 7
		private static readonly int[] DotBits = { 0, 3, 1, 4, 2, 5, 6, 7 };

		/// <summary>
		/// Draws a line on a boolean pixel grid using Bresenham's line algorithm.
		/// </summary>
		/// <param name="pixels">The pixel grid [height, width]. True = filled.</param>
		/// <param name="x0">Start X coordinate.</param>
		/// <param name="y0">Start Y coordinate.</param>
		/// <param name="x1">End X coordinate.</param>
		/// <param name="y1">End Y coordinate.</param>
		public static void DrawLinePixels(bool[,] pixels, int x0, int y0, int x1, int y1)
		{
			int height = pixels.GetLength(0);
			int width = pixels.GetLength(1);

			int dx = Math.Abs(x1 - x0);
			int dy = Math.Abs(y1 - y0);
			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;
			int err = dx - dy;

			while (true)
			{
				if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
				{
					pixels[y0, x0] = true;
				}

				if (x0 == x1 && y0 == y1)
					break;

				int e2 = 2 * err;
				if (e2 > -dy)
				{
					err -= dy;
					x0 += sx;
				}
				if (e2 < dx)
				{
					err += dx;
					y0 += sy;
				}
			}
		}

		/// <summary>
		/// Converts a 2x4 pixel block at the given cell position to a Unicode Braille character.
		/// </summary>
		/// <param name="pixels">The pixel grid [height, width].</param>
		/// <param name="cellCol">The cell column index (each cell is 2 pixels wide).</param>
		/// <param name="cellRow">The cell row index (each cell is 4 pixels tall).</param>
		/// <returns>A Unicode Braille character (U+2800 to U+28FF).</returns>
		public static char GetBrailleChar(bool[,] pixels, int cellCol, int cellRow)
		{
			int pixelHeight = pixels.GetLength(0);
			int pixelWidth = pixels.GetLength(1);

			int brailleValue = 0x2800;

			int baseY = cellRow * DotsPerCellHeight;
			int baseX = cellCol * DotsPerCellWidth;

			for (int py = 0; py < DotsPerCellHeight; py++)
			{
				for (int px = 0; px < DotsPerCellWidth; px++)
				{
					int y = baseY + py;
					int x = baseX + px;

					if (y < pixelHeight && x < pixelWidth && pixels[y, x])
					{
						int dotIndex = py * DotsPerCellWidth + px;
						brailleValue |= (1 << DotBits[dotIndex]);
					}
				}
			}

			return (char)brailleValue;
		}
	}
}
