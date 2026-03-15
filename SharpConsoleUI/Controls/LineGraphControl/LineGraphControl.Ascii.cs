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
		#region ASCII Constants

		private const char ASCII_HORIZONTAL = '─';
		private const char ASCII_VERTICAL = '│';
		private const char ASCII_RISE = '╱';
		private const char ASCII_FALL = '╲';

		#endregion

		private void PaintAscii(CharacterBuffer buffer, int graphX, int graphY,
			int graphWidth, int graphHeight, LayoutRect clipRect, Color bgColor,
			List<(LineGraphSeries series, List<double> data)> seriesSnapshots,
			double globalMin, double globalMax)
		{
			if (graphWidth <= 0 || graphHeight <= 0)
				return;

			double range = globalMax - globalMin;

			// Character grid [row, col] with color info
			var grid = new char[graphHeight, graphWidth];
			var colorGrid = new int[graphHeight, graphWidth];

			// Initialize grids
			for (int y = 0; y < graphHeight; y++)
			{
				for (int x = 0; x < graphWidth; x++)
				{
					grid[y, x] = ' ';
					colorGrid[y, x] = -1;
				}
			}

			// Draw each series
			for (int si = 0; si < seriesSnapshots.Count; si++)
			{
				var (series, data) = seriesSnapshots[si];
				if (data.Count == 0)
					continue;

				if (data.Count == 1)
				{
					// Single point
					int row = graphHeight - 1 - (int)((data[0] - globalMin) / range * (graphHeight - 1));
					row = Math.Clamp(row, 0, graphHeight - 1);
					int col = graphWidth / 2;
					if (col >= 0 && col < graphWidth)
					{
						grid[row, col] = ASCII_HORIZONTAL;
						colorGrid[row, col] = si;
					}
					continue;
				}

				// Draw line segments
				for (int i = 0; i < data.Count - 1; i++)
				{
					int x1 = (int)(i * (graphWidth - 1.0) / Math.Max(data.Count - 1, 1));
					int y1 = graphHeight - 1 - (int)((data[i] - globalMin) / range * (graphHeight - 1));
					int x2 = (int)((i + 1) * (graphWidth - 1.0) / Math.Max(data.Count - 1, 1));
					int y2 = graphHeight - 1 - (int)((data[i + 1] - globalMin) / range * (graphHeight - 1));

					x1 = Math.Clamp(x1, 0, graphWidth - 1);
					y1 = Math.Clamp(y1, 0, graphHeight - 1);
					x2 = Math.Clamp(x2, 0, graphWidth - 1);
					y2 = Math.Clamp(y2, 0, graphHeight - 1);

					DrawLineAscii(grid, colorGrid, x1, y1, x2, y2, si);
				}
			}

			// Write grid to buffer
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

					char ch = grid[row, col];

					if (ch == ' ')
					{
						buffer.SetNarrowCell(paintX, paintY, ' ', EmptyCellColor, bgColor);
					}
					else
					{
						Color cellColor;
						int seriesIndex = colorGrid[row, col];
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

						buffer.SetNarrowCell(paintX, paintY, ch, cellColor, bgColor);
					}
				}
			}
		}

		private static void DrawLineAscii(char[,] grid, int[,] colorGrid, int x0, int y0, int x1, int y1, int seriesIndex)
		{
			int height = grid.GetLength(0);
			int width = grid.GetLength(1);

			int dx = Math.Abs(x1 - x0);
			int dy = Math.Abs(y1 - y0);
			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;
			int err = dx - dy;

			while (true)
			{
				if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
				{
					char ch;
					if (dx == 0)
						ch = ASCII_VERTICAL;
					else if (dy == 0)
						ch = ASCII_HORIZONTAL;
					else if ((sx > 0 && sy > 0) || (sx < 0 && sy < 0))
						ch = ASCII_FALL;
					else
						ch = ASCII_RISE;

					grid[y0, x0] = ch;
					colorGrid[y0, x0] = seriesIndex;
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
	}
}
