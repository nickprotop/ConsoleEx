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

			double range = globalMax - globalMin;

			// Track which series contributes to each cell column for coloring
			// Last series rendered wins per cell
			var seriesColorMap = new int[graphWidth];
			Array.Fill(seriesColorMap, -1);

			// Draw all series into pixel grid
			for (int si = 0; si < seriesSnapshots.Count; si++)
			{
				var (series, data) = seriesSnapshots[si];
				if (data.Count == 0)
					continue;

				if (data.Count == 1)
				{
					// Single point: draw a dot at the mapped position
					int py = pixelHeight - 1 - (int)((data[0] - globalMin) / range * (pixelHeight - 1));
					py = Math.Clamp(py, 0, pixelHeight - 1);
					int px = pixelWidth / 2;
					if (px >= 0 && px < pixelWidth)
					{
						_pixelGridCache[py, px] = true;
						seriesColorMap[px / BrailleHelpers.DotsPerCellWidth] = si;
					}
					continue;
				}

				// Draw line segments between consecutive points
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

					BrailleHelpers.DrawLinePixels(_pixelGridCache, x1, y1, x2, y2);

					// Mark cell columns touched by this segment for coloring
					int cellStart = Math.Min(x1, x2) / BrailleHelpers.DotsPerCellWidth;
					int cellEnd = Math.Max(x1, x2) / BrailleHelpers.DotsPerCellWidth;
					for (int c = cellStart; c <= cellEnd && c < graphWidth; c++)
					{
						seriesColorMap[c] = si;
					}
				}
			}

			// Convert pixel grid to braille characters and write to buffer
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
						int seriesIndex = seriesColorMap[col];
						if (seriesIndex >= 0 && seriesIndex < seriesSnapshots.Count)
						{
							var (series, _) = seriesSnapshots[seriesIndex];
							if (series.Gradient != null)
							{
								double position = graphWidth > 1 ? (double)col / (graphWidth - 1) : 0.5;
								cellColor = series.Gradient.Interpolate(position);
							}
							else
							{
								cellColor = series.LineColor;
							}
						}
						else
						{
							cellColor = Color.Cyan1;
						}

						buffer.SetNarrowCell(paintX, paintY, brailleChar, cellColor, bgColor);
					}
				}
			}
		}
	}
}
