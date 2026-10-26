// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		/// <summary>Reused dot grid, so a redraw does not allocate one per frame.</summary>
		private bool[,]? _dotGrid;

		/// <summary>
		/// Paints the plot with glyphs rather than half-block pixels.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Braille packs 2x4 dots into a cell, which draws a finer line than half
		/// blocks can - at the cost of one foreground colour per cell, so where two
		/// series share a cell only the last one's colour survives. That trade is
		/// the whole reason both modes exist.
		/// </para>
		/// <para>
		/// Each series is rasterised into its own dot grid and written in its own
		/// colour, merging dot patterns where cells are shared so neither series
		/// disappears even though only one colour can.
		/// </para>
		/// </remarks>
		private void PaintGlyphPlot(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			double xMin, double xMax, double yMin, double yMax,
			IReadOnlyList<ChartTick> yTicks, IReadOnlyList<ChartTick> xTicks)
		{
			bool braille = _renderMode == ChartRenderMode.Braille;

			int dotsWide = braille ? BrailleHelpers.DotsPerCellWidth : 1;
			int dotsHigh = braille ? BrailleHelpers.DotsPerCellHeight : 1;

			int gw = plot.Width * dotsWide;
			int gh = plot.Height * dotsHigh;
			if (gw <= 0 || gh <= 0) return;

			PaintGlyphBackground(buffer, plot, clipRect, bg, braille);
			PaintGlyphGridlines(buffer, plot, clipRect, bg, xMin, xMax, yMin, yMax, yTicks, xTicks);

			if (_dotGrid == null || _dotGrid.GetLength(0) != gh || _dotGrid.GetLength(1) != gw)
			{
				_dotGrid = new bool[gh, gw];
			}

			int barSeriesCount = 0;
			foreach (var (s, _) in visible)
			{
				if ((s.PlotType ?? _plotType) == ChartPlotType.Bar) barSeriesCount++;
			}
			int barIndex = 0;

			for (int i = 0; i < visible.Count; i++)
			{
				var (series, points) = visible[i];
				if (points.Count == 0) continue;

				Array.Clear(_dotGrid);

				var half = _splitView
					? (i == 0 ? ChartHalf.Upper : i == 1 ? ChartHalf.Lower : ChartHalf.Full)
					: ChartHalf.Full;

				// The lower half may carry its own peak. The pixel path honours this;
				// without the same here a braille chart measured both halves against
				// the primary axis and the smaller series collapsed to the centre.
				double sMin = yMin, sMax = yMax;
				if (half == ChartHalf.Lower)
				{
					sMin = LowerAxis.Min ?? yMin;
					sMax = LowerAxis.Max ?? yMax;
				}

				var type = series.PlotType ?? _plotType;
				if (type == ChartPlotType.Bar)
				{
					RasteriseBars(_dotGrid, points, gw, gh, xMin, xMax, sMin, sMax, half,
						barIndex++, barSeriesCount);
				}
				else if (type == ChartPlotType.Scatter)
				{
					RasteriseMarks(_dotGrid, points, gw, gh, xMin, xMax, sMin, sMax, half);
				}
				else
				{
					RasteriseLine(_dotGrid, points, gw, gh, xMin, xMax, sMin, sMax, half,
						fillToBaseline: type == ChartPlotType.Area);
				}

				WriteGlyphs(buffer, plot, clipRect, bg, series, braille, gw, gh,
					xMin, xMax, sMin, sMax, half, points);
			}
		}

		/// <summary>Lays down the empty cell each glyph mode starts from.</summary>
		private void PaintGlyphBackground(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg, bool braille)
		{
			Color fill = _plotBackground ?? bg;
			if (fill == Color.Transparent) return;   // let what is behind show through

			char empty = braille ? BrailleHelpers.BrailleEmpty : ' ';

			for (int row = 0; row < plot.Height; row++)
			{
				int y = plot.Y + row;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				for (int col = 0; col < plot.Width; col++)
				{
					int x = plot.X + col;
					if (x < clipRect.X || x >= clipRect.Right) continue;
					buffer.SetNarrowCell(x, y, empty, ControlDefaults.ChartGridlineColor, fill);
				}
			}
		}

		/// <summary>
		/// Draws gridlines as dim characters.
		/// </summary>
		/// <remarks>
		/// Not into a dot grid: a braille cell carries one colour, so a gridline
		/// sharing a cell with data would steal the series' colour. Drawn first and
		/// overwritten by whatever lands on top.
		/// </remarks>
		private void PaintGlyphGridlines(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			double xMin, double xMax, double yMin, double yMax,
			IReadOnlyList<ChartTick> yTicks, IReadOnlyList<ChartTick> xTicks)
		{
			Color grid = ControlDefaults.ChartGridlineColor;
			Color fill = _plotBackground ?? bg;

			if (YAxis.ShowGridlines)
			{
				foreach (var tick in yTicks)
				{
					int y = plot.Y + ValueToRow(tick.Value, yMin, yMax, plot.Height);
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					for (int col = 0; col < plot.Width; col++)
					{
						int x = plot.X + col;
						if (x < clipRect.X || x >= clipRect.Right) continue;
						buffer.SetNarrowCell(x, y, ControlDefaults.ChartGridlineChar, grid, fill);
					}
				}
			}

			if (XAxis.ShowGridlines)
			{
				foreach (var tick in xTicks)
				{
					int x = plot.X + ValueToColumn(tick.Value, xMin, xMax, plot.Width);
					if (x < clipRect.X || x >= clipRect.Right) continue;
					for (int row = 0; row < plot.Height; row++)
					{
						int y = plot.Y + row;
						if (y < clipRect.Y || y >= clipRect.Bottom) continue;
						buffer.SetNarrowCell(x, y, ControlDefaults.ChartGridlineChar, grid, fill);
					}
				}
			}
		}

		/// <summary>Turns a dot grid into glyphs, merging with whatever shares a cell.</summary>
		private void WriteGlyphs(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			ChartSeries series, bool braille, int gw, int gh,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half,
			List<ChartPoint> points)
		{
			Color fill = _plotBackground ?? bg;

			for (int row = 0; row < plot.Height; row++)
			{
				int y = plot.Y + row;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				for (int col = 0; col < plot.Width; col++)
				{
					int x = plot.X + col;
					if (x < clipRect.X || x >= clipRect.Right) continue;

					char glyph;
					if (braille)
					{
						glyph = BrailleHelpers.GetBrailleChar(_dotGrid!, col, row);
						if (glyph == BrailleHelpers.BrailleEmpty) continue;

						// Two series in one cell: OR the dot patterns so neither
						// vanishes, even though only one colour can survive.
						var existing = buffer.GetCell(x, y);
						int prior = existing.Character.Value;
						if (prior > BrailleHelpers.BrailleEmpty && prior <= 0x28FF)
						{
							glyph = (char)(prior | glyph);
						}
					}
					else
					{
						if (!_dotGrid![row, col]) continue;
						glyph = ControlDefaults.ChartAsciiPlotChar;
					}

					Color colour = series.Gradient != null
						? GradientColourAt(series, col, row, plot.Width, plot.Height)
						: series.Color;

					buffer.SetNarrowCell(x, y, glyph, colour, fill);
				}
			}

			// Hit-testing reads the same positions whatever the mode draws with.
			for (int i = 0; i < points.Count; i++)
			{
				int col = ValueToColumn(points[i].X, xMin, xMax, plot.Width);
				int row = half == ChartHalf.Full
					? ValueToRow(points[i].Y, yMin, yMax, plot.Height)
					: RowInHalf(points[i].Y, yMin, yMax, plot.Height, half == ChartHalf.Upper);
				RecordPoint(series, i, plot.X + col, plot.Y + row);
			}
		}

		private static void RasteriseLine(bool[,] grid, List<ChartPoint> points, int gw, int gh,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half, bool fillToBaseline)
		{
			int baseline = half == ChartHalf.Full
				? (yMin <= 0 && yMax >= 0 ? DotY(0, yMin, yMax, gh) : gh - 1)
				: gh / 2;

			for (int i = 0; i < points.Count; i++)
			{
				int x = DotX(points[i].X, xMin, xMax, gw);
				int y = DotYIn(points[i].Y, yMin, yMax, gh, half);

				if (i == points.Count - 1)
				{
					if (fillToBaseline) FillColumn(grid, x, y, baseline, gh, gw);
					break;
				}

				int nx = DotX(points[i + 1].X, xMin, xMax, gw);
				int ny = DotYIn(points[i + 1].Y, yMin, yMax, gh, half);

				if (fillToBaseline)
				{
					// Every column of the segment, not just the data point's own.
					// Filling only at the points leaves the columns between them
					// empty, which draws the area as vertical stripes while the
					// line over it still looks correct.
					int span = Math.Max(1, Math.Abs(nx - x));
					for (int step = 0; step <= span; step++)
					{
						int fx = nx >= x ? x + step : x - step;
						if ((uint)fx >= (uint)gw) continue;

						double t = span == 0 ? 0 : (double)step / span;
						int fy = (int)Math.Round(y + (ny - y) * t);
						FillColumn(grid, fx, fy, baseline, gh, gw);
					}
				}

				BrailleHelpers.DrawLinePixels(grid, x, y, nx, ny);
			}
		}

		/// <summary>Inks one column from a value row down to the baseline.</summary>
		private static void FillColumn(bool[,] grid, int x, int y, int baseline, int gh, int gw)
		{
			if ((uint)x >= (uint)gw) return;

			for (int fy = Math.Min(y, baseline); fy <= Math.Max(y, baseline); fy++)
			{
				if ((uint)fy < (uint)gh) grid[fy, x] = true;
			}
		}

		private void RasteriseBars(bool[,] grid, List<ChartPoint> points, int gw, int gh,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half,
			int seriesIndex, int seriesCount)
		{
			int baseline = half == ChartHalf.Full
				? (yMin <= 0 && yMax >= 0 ? DotY(0, yMin, yMax, gh) : gh - 1)
				: gh / 2;

			double slot = _sizing == ChartSizing.FixedStep
				? _stepWidth * (gw / Math.Max(1, _lastPlotRect.Width == 0 ? gw : _lastPlotRect.Width))
				: (double)gw / Math.Max(1, points.Count);
			int group = Math.Max(1, seriesCount);
			double share = Math.Max(1, (slot - 1) / group);

			foreach (var point in points)
			{
				int centre = DotX(point.X, xMin, xMax, gw);
				int top = DotYIn(point.Y, yMin, yMax, gh, half);

				double groupLeft = centre - (share * group) / 2.0;
				int from = (int)Math.Round(groupLeft + share * seriesIndex);
				int to = (int)Math.Round(groupLeft + share * (seriesIndex + 1)) - 1;
				if (to < from) to = from;

				for (int x = from; x <= to; x++)
				{
					if ((uint)x >= (uint)gw) continue;
					for (int y = Math.Min(top, baseline); y <= Math.Max(top, baseline); y++)
					{
						if ((uint)y < (uint)gh) grid[y, x] = true;
					}
				}
			}
		}

		private static void RasteriseMarks(bool[,] grid, List<ChartPoint> points, int gw, int gh,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half)
		{
			foreach (var point in points)
			{
				int x = DotX(point.X, xMin, xMax, gw);
				int y = DotYIn(point.Y, yMin, yMax, gh, half);
				if ((uint)x < (uint)gw && (uint)y < (uint)gh) grid[y, x] = true;
			}
		}

		private static int DotX(double value, double min, double max, int gw)
		{
			if (gw <= 1) return 0;
			double range = max - min;
			int x = range > 0 ? (int)((value - min) / range * (gw - 1)) : gw / 2;
			return Math.Clamp(x, 0, gw - 1);
		}

		private static int DotY(double value, double min, double max, int gh)
		{
			if (gh <= 1) return 0;
			double range = max - min;
			int y = range > 0 ? gh - 1 - (int)((value - min) / range * (gh - 1)) : gh / 2;
			return Math.Clamp(y, 0, gh - 1);
		}

		private static int DotYIn(double value, double min, double max, int gh, ChartHalf half)
		{
			if (half == ChartHalf.Full) return DotY(value, min, max, gh);
			if (gh <= 1) return 0;

			int centre = gh / 2;
			double range = max - min;
			double fraction = range > 0 ? Math.Clamp((value - min) / range, 0, 1) : 0.5;

			if (half == ChartHalf.Upper)
			{
				return Math.Clamp(centre - (int)(fraction * Math.Max(1, centre)), 0, gh - 1);
			}

			return Math.Clamp(centre + (int)(fraction * Math.Max(1, gh - 1 - centre)), 0, gh - 1);
		}
	}
}
