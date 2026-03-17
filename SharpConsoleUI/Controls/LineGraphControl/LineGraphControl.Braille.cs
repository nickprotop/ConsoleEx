// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class LineGraphControl
	{
		/// <summary>
		/// Empty braille cell color for background dots.
		/// </summary>
		private static readonly Color EmptyCellColor = Color.Grey19;

		private void PaintBraille(CharacterBuffer buffer, int graphX, int graphY,
			int graphWidth, int graphHeight, LayoutRect clipRect, Color bgColor,
			List<(LineGraphSeries series, List<double> data)> seriesSnapshots,
			double globalMin, double globalMax)
		{
			int pixelWidth = graphWidth * BrailleHelpers.DotsPerCellWidth;
			int pixelHeight = graphHeight * BrailleHelpers.DotsPerCellHeight;

			if (pixelWidth <= 0 || pixelHeight <= 0)
				return;

			double range = globalMax - globalMin;

			// For single series, use the fast path (original logic)
			if (seriesSnapshots.Count <= 1)
			{
				PaintBrailleSingleSeries(buffer, graphX, graphY, graphWidth, graphHeight,
					pixelWidth, pixelHeight, clipRect, bgColor, seriesSnapshots, globalMin, range);
				return;
			}

			// Multi-series: render each series into its own pixel grid,
			// then composite into buffer with per-series colors.
			PaintBrailleMultiSeries(buffer, graphX, graphY, graphWidth, graphHeight,
				pixelWidth, pixelHeight, clipRect, bgColor, seriesSnapshots, globalMin, range);
		}

		private void PaintBrailleSingleSeries(CharacterBuffer buffer, int graphX, int graphY,
			int graphWidth, int graphHeight, int pixelWidth, int pixelHeight,
			LayoutRect clipRect, Color bgColor,
			List<(LineGraphSeries series, List<double> data)> seriesSnapshots,
			double globalMin, double range)
		{
			// Allocate or reuse pixel grid
			if (_pixelGridCache == null ||
			    _pixelGridCache.GetLength(0) != pixelHeight ||
			    _pixelGridCache.GetLength(1) != pixelWidth)
			{
				_pixelGridCache = new bool[pixelHeight, pixelWidth];
			}
			else
			{
				Array.Clear(_pixelGridCache);
			}

			Color seriesColor = Color.Cyan1;
			ColorGradient? gradient = null;

			if (seriesSnapshots.Count == 1)
			{
				var (series, data) = seriesSnapshots[0];
				seriesColor = series.LineColor;
				gradient = series.Gradient;
				if (data.Count > 0)
					DrawSeriesPixels(_pixelGridCache, data, pixelWidth, pixelHeight, globalMin, range);
			}

			for (int row = 0; row < graphHeight; row++)
			{
				int paintY = graphY + row;
				if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
					continue;

				for (int col = 0; col < graphWidth; col++)
				{
					int paintX = graphX + col;
					if (paintX < clipRect.X || paintX >= clipRect.Right)
						continue;

					char brailleChar = BrailleHelpers.GetBrailleChar(_pixelGridCache, col, row);

					if (brailleChar == BrailleHelpers.BrailleEmpty)
					{
						buffer.SetNarrowCell(paintX, paintY, brailleChar, EmptyCellColor, bgColor);
					}
					else
					{
						Color cellColor;
						if (gradient != null)
						{
							double position = graphWidth > 1 ? (double)col / (graphWidth - 1) : 0.5;
							cellColor = gradient.Interpolate(position);
						}
						else
						{
							cellColor = seriesColor;
						}
						buffer.SetNarrowCell(paintX, paintY, brailleChar, cellColor, bgColor);
					}
				}
			}
		}

		private void PaintBrailleMultiSeries(CharacterBuffer buffer, int graphX, int graphY,
			int graphWidth, int graphHeight, int pixelWidth, int pixelHeight,
			LayoutRect clipRect, Color bgColor,
			List<(LineGraphSeries series, List<double> data)> seriesSnapshots,
			double globalMin, double range)
		{
			// First pass: paint empty braille background for all cells
			for (int row = 0; row < graphHeight; row++)
			{
				int paintY = graphY + row;
				if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
					continue;

				for (int col = 0; col < graphWidth; col++)
				{
					int paintX = graphX + col;
					if (paintX < clipRect.X || paintX >= clipRect.Right)
						continue;

					buffer.SetNarrowCell(paintX, paintY, BrailleHelpers.BrailleEmpty, EmptyCellColor, bgColor);
				}
			}

			// Render each series independently with its own pixel grid and color
			var pixelGrid = new bool[pixelHeight, pixelWidth];

			for (int si = 0; si < seriesSnapshots.Count; si++)
			{
				var (series, data) = seriesSnapshots[si];
				if (data.Count == 0)
					continue;

				Array.Clear(pixelGrid);
				DrawSeriesPixels(pixelGrid, data, pixelWidth, pixelHeight, globalMin, range);

				// Write this series' braille chars to buffer with its own color
				for (int row = 0; row < graphHeight; row++)
				{
					int paintY = graphY + row;
					if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
						continue;

					for (int col = 0; col < graphWidth; col++)
					{
						int paintX = graphX + col;
						if (paintX < clipRect.X || paintX >= clipRect.Right)
							continue;

						char brailleChar = BrailleHelpers.GetBrailleChar(pixelGrid, col, row);
						if (brailleChar == BrailleHelpers.BrailleEmpty)
							continue;

						Color cellColor;
						if (series.Gradient != null)
						{
							double position = graphWidth > 1 ? (double)col / (graphWidth - 1) : 0.5;
							cellColor = series.Gradient.Interpolate(position);
						}
						else
						{
							cellColor = series.LineColor;
						}

						// Merge with existing braille content in this cell
						var existing = buffer.GetCell(paintX, paintY);
						int existingBraille = existing.Character.Value;
						int newBraille = brailleChar;

						// If the existing cell is a braille character, OR the patterns together
						if (existingBraille >= 0x2800 && existingBraille <= 0x28FF)
						{
							int merged = existingBraille | newBraille;
							buffer.SetNarrowCell(paintX, paintY, (char)merged, cellColor, bgColor);
						}
						else
						{
							buffer.SetNarrowCell(paintX, paintY, brailleChar, cellColor, bgColor);
						}
					}
				}
			}
		}

		/// <summary>
		/// Draw a series' data points into a pixel grid using Bresenham line segments.
		/// </summary>
		private static void DrawSeriesPixels(bool[,] grid, List<double> data,
			int pixelWidth, int pixelHeight, double globalMin, double range)
		{
			if (data.Count == 1)
			{
				int py = range > 0
					? pixelHeight - 1 - (int)((data[0] - globalMin) / range * (pixelHeight - 1))
					: pixelHeight / 2;
				py = Math.Clamp(py, 0, pixelHeight - 1);
				int px = pixelWidth / 2;
				if (px >= 0 && px < pixelWidth)
					grid[py, px] = true;
				return;
			}

			for (int i = 0; i < data.Count - 1; i++)
			{
				int x1 = (int)(i * (pixelWidth - 1.0) / Math.Max(data.Count - 1, 1));
				int y1 = pixelHeight - 1 - (int)((data[i] - globalMin) / range * (pixelHeight - 1));
				int x2 = (int)((i + 1) * (pixelWidth - 1.0) / Math.Max(data.Count - 1, 1));
				int y2 = pixelHeight - 1 - (int)((data[i + 1] - globalMin) / range * (pixelHeight - 1));

				x1 = Math.Clamp(x1, 0, pixelWidth - 1);
				y1 = Math.Clamp(y1, 0, pixelHeight - 1);
				x2 = Math.Clamp(x2, 0, pixelWidth - 1);
				y2 = Math.Clamp(y2, 0, pixelHeight - 1);

				BrailleHelpers.DrawLinePixels(grid, x1, y1, x2, y2);
			}
		}
	}
}
