// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Imaging
{
	/// <summary>
	/// Renders a PixelBuffer to Cell arrays using the half-block technique.
	/// Each cell represents 2 vertical pixels: fg = top pixel, bg = bottom pixel,
	/// using the upper half block character (U+2580).
	/// </summary>
	public static class HalfBlockRenderer
	{
		/// <summary>
		/// Renders a PixelBuffer to a 2D Cell array.
		/// Output height = ceil(image.Height / 2). Output width = image.Width.
		/// </summary>
		/// <param name="image">The pixel buffer to render.</param>
		/// <param name="windowBackground">Background color for odd-height last row bottom pixel.</param>
		public static Cell[,] Render(PixelBuffer image, Color windowBackground)
		{
			int cellWidth = image.Width;
			int cellHeight = (image.Height + 1) / ImagingDefaults.PixelsPerCell;
			var cells = new Cell[cellWidth, cellHeight];

			for (int cy = 0; cy < cellHeight; cy++)
			{
				int topPixelY = cy * ImagingDefaults.PixelsPerCell;
				int bottomPixelY = topPixelY + 1;
				bool hasBottomPixel = bottomPixelY < image.Height;

				for (int cx = 0; cx < cellWidth; cx++)
				{
					var topPixel = image.GetPixel(cx, topPixelY);
					Color fg = new Color(topPixel.R, topPixel.G, topPixel.B);

					Color bg;
					if (hasBottomPixel)
					{
						var bottomPixel = image.GetPixel(cx, bottomPixelY);
						bg = new Color(bottomPixel.R, bottomPixel.G, bottomPixel.B);
					}
					else
					{
						bg = windowBackground;
					}

					cells[cx, cy] = new Cell(ImagingDefaults.HalfBlockChar, fg, bg);
				}
			}

			return cells;
		}

		/// <summary>
		/// Renders a PixelBuffer scaled to the specified cell dimensions.
		/// The image is first resized to (cols, rows * 2) pixels, then rendered.
		/// </summary>
		/// <param name="image">The pixel buffer to render.</param>
		/// <param name="cols">Target width in cells (characters).</param>
		/// <param name="rows">Target height in cells (character rows).</param>
		/// <param name="windowBackground">Background color for odd-height last row bottom pixel.</param>
		public static Cell[,] RenderScaled(PixelBuffer image, int cols, int rows, Color windowBackground)
		{
			int targetPixelWidth = Math.Max(1, cols);
			int targetPixelHeight = Math.Max(1, rows * ImagingDefaults.PixelsPerCell);

			var scaled = image.Resize(targetPixelWidth, targetPixelHeight);
			return Render(scaled, windowBackground);
		}
	}
}
