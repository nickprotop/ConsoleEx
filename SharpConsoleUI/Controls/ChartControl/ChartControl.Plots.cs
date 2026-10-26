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
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		/// <summary>Upper half block: foreground is the top pixel, background the bottom.</summary>
		private const char UpperHalf = '▀';

		/// <summary>Paints the plot area: the surface, every visible series, then present.</summary>
		/// <param name="buffer"></param>
		/// <param name="plot"></param>
		/// <param name="clipRect"></param>
		/// <param name="controlBackground"></param>
		/// <param name="visible"></param>
		/// <param name="xMin"></param>
		/// <param name="xMax"></param>
		/// <param name="yMin"></param>
		/// <param name="yMax"></param>
		/// <param name="yTicks"></param>
		/// <param name="xTicks"></param>
		/// <param name="overlay">
		/// True for a second pass into the same plot, such as the series on the
		/// secondary axis. The surface is not re-filled and the grid is not redrawn,
		/// because both would erase the pass before it.
		/// </param>
		private void PaintPlot(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color controlBackground,
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			double xMin, double xMax, double yMin, double yMax,
			IReadOnlyList<ChartTick> yTicks, IReadOnlyList<ChartTick> xTicks,
			bool overlay = false)
		{
			// Braille and ascii build glyphs rather than pixels, so they take their
			// own path entirely - a half-block surface cannot express either.
			if (_renderMode != ChartRenderMode.Pixel)
			{
				PaintGlyphPlot(buffer, plot, clipRect, controlBackground, visible,
					xMin, xMax, yMin, yMax, yTicks, xTicks);
				return;
			}

			// Transparent means "let what is behind show through", and it arrives two
			// ways: the caller asks for it, or nobody named a plot colour and the theme
			// resolver returned its Color.Transparent default, which means the same.
			Color fill = _plotBackground ?? controlBackground;
			bool transparent = fill == Color.Transparent;

			var surface = _surface ??= new PixelSurface();
			surface.EnsureSize(plot.Width, plot.Height);

			if (!overlay)
			{
				surface.Fill(fill);

				// Grid first, data second: a gridline drawn over a series would hide
				// exactly the points it crosses.
				DrawGridlines(surface, xMin, xMax, yMin, yMax, yTicks, xTicks);
			}

			// Bars share the horizontal space between them, so each needs to know how
			// many bar series there are and which one it is.
			int barSeriesCount = 0;
			foreach (var (s, _) in visible)
			{
				if ((s.PlotType ?? _plotType) == ChartPlotType.Bar) barSeriesCount++;
			}
			int barIndex = 0;

			for (int i = 0; i < visible.Count; i++)
			{
				var (series, points) = visible[i];

				// Split names the first two series; the rest keep the whole plot, so
				// a third series is still visible rather than silently dropped.
				var half = _splitView
					? (i == 0 ? ChartHalf.Upper : i == 1 ? ChartHalf.Lower : ChartHalf.Full)
					: ChartHalf.Full;

				// The lower half may carry its own peak, so a small series beside a
				// large one still uses its whole band.
				double seriesMin = yMin, seriesMax = yMax;
				if (half == ChartHalf.Lower)
				{
					seriesMin = LowerAxis.Min ?? yMin;
					seriesMax = LowerAxis.Max ?? yMax;
				}

				var type = series.PlotType ?? _plotType;

				switch (type)
				{
					case ChartPlotType.Bar:
						if (_orientation == ChartOrientation.Horizontal)
						{
							DrawHorizontalBars(surface, series, points, xMin, xMax, seriesMin, seriesMax,
								barIndex++, barSeriesCount);
						}
						else
						{
							DrawBarSeries(surface, series, points, xMin, xMax, seriesMin, seriesMax, half,
								barIndex++, barSeriesCount);
						}
						break;

					case ChartPlotType.Scatter:
						DrawScatterSeries(surface, series, points, plot, xMin, xMax, seriesMin, seriesMax, half);
						break;

					case ChartPlotType.Area:
						// Stacked, a band fills down to the series beneath it rather
						// than to the baseline - otherwise each one paints over all
						// the bands below and only the topmost is ever visible.
						DrawAreaFill(surface, series, points, plot, xMin, xMax, seriesMin, seriesMax, half, fill,
							_stacked ? FloorFor(visible, i, points) : null);
						// The outline goes over its own fill, so the curve stays crisp
						// against the block beneath it.
						DrawLineSeries(surface, series, points, plot, xMin, xMax, seriesMin, seriesMax, half);
						break;

					default:
						DrawLineSeries(surface, series, points, plot, xMin, xMax, seriesMin, seriesMax, half);
						break;
				}
			}

			var g = new CanvasGraphics(buffer, plot.X, plot.Y, plot.Width, plot.Height, clipRect);

			if (transparent)
				PresentSkippingUntouched(surface, g, plot.Width, plot.Height);
			else
				surface.Present(g, fill);
		}

		/// <summary>
		/// Draws gridlines into the surface at each tick on an axis that asks for them.
		/// </summary>
		/// <remarks>
		/// Into the pixel surface rather than as cell text, so the data drawn after
		/// simply overwrites the pixels it covers and no cell has to carry both.
		/// </remarks>
		private void DrawGridlines(PixelSurface surface,
			double xMin, double xMax, double yMin, double yMax,
			IReadOnlyList<ChartTick> yTicks, IReadOnlyList<ChartTick> xTicks)
		{
			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			Color grid = ControlDefaults.ChartGridlineColor;

			if (YAxis.ShowGridlines)
			{
				foreach (var tick in yTicks)
				{
					int py = PixelY(tick.Value, yMin, yMax, ph);
					for (int px = 0; px < pw; px++) surface.Set(px, py, grid);
				}
			}

			if (XAxis.ShowGridlines)
			{
				foreach (var tick in xTicks)
				{
					int px = PixelX(tick.Value, xMin, xMax, pw);
					for (int py = 0; py < ph; py++) surface.Set(px, py, grid);
				}
			}
		}

		/// <summary>
		/// Draws one bar per point, rising or falling from the baseline.
		/// </summary>
		/// <remarks>
		/// Bars anchor to zero when the axis contains it, so a negative value falls
		/// below the line rather than being clamped to the floor. Several bar series
		/// divide each slot between them and sit side by side, which is what makes a
		/// grouped bar chart rather than one series hiding another.
		/// </remarks>
		private void DrawBarSeries(PixelSurface surface, ChartSeries series, List<ChartPoint> points,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half,
			int seriesIndex, int seriesCount)
		{
			if (points.Count == 0) return;

			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			int baseline = BaselineRow(yMin, yMax, ph, half);

			// The slot is the room one X position gets; the bar is that slot minus a
			// gap, split again between however many bar series share the chart.
			// In fixed-step mode a point's slot is the step itself, not a share of
			// the whole plot: the plot is sized for growth, so dividing its width by
			// the points present would make eight samples sprawl into one block.
			// Half blocks double the vertical resolution, not the horizontal, so one
			// cell is one surface column and the step converts directly.
			double slot = _sizing == ChartSizing.FixedStep
				? _stepWidth
				: (double)pw / Math.Max(1, points.Count);
			int group = Math.Max(1, seriesCount);
			double share = Math.Max(1, (slot - ControlDefaults.ChartBarGapPixels) / group);

			foreach (var point in points)
			{
				// Bars occupy a slot rather than sitting on a position, so the ends
				// are inset by half a slot - otherwise the first and last bars are
				// half outside the plot.
				int centre = BarCentre(point.X, xMin, xMax, pw, slot);
				int top = PixelYInHalf(point.Y, yMin, yMax, ph, half);

				// Offset this series within the slot so grouped bars do not overlap.
				double groupLeft = centre - (share * group) / 2.0;
				int from = (int)Math.Round(groupLeft + share * seriesIndex);
				int to = (int)Math.Round(groupLeft + share * (seriesIndex + 1)) - 1;
				if (to < from) to = from;

				int lo = Math.Min(top, baseline);
				int hi = Math.Max(top, baseline);

				for (int px = from; px <= to; px++)
				{
					if ((uint)px >= (uint)pw) continue;
					for (int py = lo; py <= hi; py++)
					{
						if ((uint)py >= (uint)ph) continue;
						surface.Set(px, py, series.Gradient != null
							? GradientColourAt(series, px, py, pw, ph)
							: series.ColorForValue(point.Y));
					}
				}
			}
		}

		/// <summary>
		/// The pixel a bar is centred on, inset so the end bars stay inside the plot.
		/// </summary>
		private static int BarCentre(double value, double min, double max, int pixelWidth, double slot)
		{
			double usable = Math.Max(1, pixelWidth - slot);
			double range = max - min;
			double fraction = range > 0 ? Math.Clamp((value - min) / range, 0, 1) : 0.5;

			return (int)Math.Round(slot / 2 + fraction * usable);
		}

		/// <summary>
		/// Draws bars running from a baseline at the left.
		/// </summary>
		/// <remarks>
		/// The axes swap roles: a point's VALUE decides how far the bar reaches
		/// across, and its position decides which row it occupies. With one row this
		/// is an inline gauge, where length is the only dimension the reading has.
		/// </remarks>
		private void DrawHorizontalBars(PixelSurface surface, ChartSeries series,
			List<ChartPoint> points, double xMin, double xMax, double yMin, double yMax,
			int seriesIndex, int seriesCount)
		{
			if (points.Count == 0) return;

			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			// Zero is where a bar starts, so a negative value runs left of it.
			int baseline = yMin <= 0 && yMax >= 0 ? PixelX(0, yMin, yMax, pw) : 0;

			double slot = (double)ph / Math.Max(1, points.Count);
			int group = Math.Max(1, seriesCount);
			double share = Math.Max(1, slot / group);

			for (int i = 0; i < points.Count; i++)
			{
				int tip = PixelX(points[i].Y, yMin, yMax, pw);

				double rowFraction = points.Count > 1
					? (double)i / (points.Count - 1)
					: 0.5;
				double centre = points.Count == 1
					? ph / 2.0
					: slot / 2 + rowFraction * Math.Max(1, ph - slot);

				double groupTop = centre - (share * group) / 2.0;
				int from = (int)Math.Round(groupTop + share * seriesIndex);
				int to = (int)Math.Round(groupTop + share * (seriesIndex + 1)) - 1;
				if (to < from) to = from;

				for (int y = from; y <= to; y++)
				{
					if ((uint)y >= (uint)ph) continue;
					for (int x = Math.Min(tip, baseline); x <= Math.Max(tip, baseline); x++)
					{
						if ((uint)x >= (uint)pw) continue;

						// The gradient reads the VALUE SCALE, not the bar's own
						// length: sampled along the bar, every reading would run the
						// whole gradient and 20% would look like 100%.
						Color colour = series.Gradient != null
							? series.Gradient.Interpolate(
								Math.Clamp(pw > 1 ? (double)x / (pw - 1) : 0.5, 0, 1))
							: series.Color;

						surface.Set(x, y, colour);
					}
				}

				RecordPoint(series, i, tip, (from + to) / 4);
			}
		}

		/// <summary>
		/// Draws an unconnected mark at each point.
		/// </summary>
		/// <remarks>
		/// The whole difference from a line: nothing is drawn between consecutive
		/// points, so the gaps carry information rather than being filled in.
		/// </remarks>
		private void DrawScatterSeries(PixelSurface surface, ChartSeries series, List<ChartPoint> points,
			(int X, int Y, int Width, int Height) plot,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half)
		{
			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			for (int i = 0; i < points.Count; i++)
			{
				int px = PixelX(points[i].X, xMin, xMax, pw);
				int py = PixelYInHalf(points[i].Y, yMin, yMax, ph, half);

				Color colour = series.Gradient != null
					? GradientColourAt(series, px, py, pw, ph)
					: series.ColorForValue(points[i].Y);

				// A single pixel is easy to lose; a small plus is still clearly a
				// mark at one position rather than a line. The centre is pulled in
				// from the edges so a point at the extreme of its range shows as a
				// whole mark instead of a clipped half.
				int cx = Math.Clamp(px, 1, Math.Max(1, pw - 2));
				int cy = Math.Clamp(py, 1, Math.Max(1, ph - 2));

				surface.Set(cx, cy, colour);
				surface.Set(cx - 1, cy, colour);
				surface.Set(cx + 1, cy, colour);
				surface.Set(cx, cy - 1, colour);
				surface.Set(cx, cy + 1, colour);

				RecordPoint(series, i, plot.X + px, plot.Y + py / 2);
			}
		}

		/// <summary>
		/// Fills the space between a series and its baseline.
		/// </summary>
		/// <remarks>
		/// The fill is dimmed toward the plot background so the outline drawn over it
		/// stays legible; a solid block in the series colour swallows its own curve.
		/// In a split half the baseline is the centre line, so the fill grows away
		/// from it in the same direction the series does.
		/// </remarks>
		/// <summary>The series a stacked band sits on, or null for the bottom one.</summary>
		private static List<ChartPoint>? FloorFor(
			List<(ChartSeries Series, List<ChartPoint> Points)> visible, int index, List<ChartPoint> own)
		{
			return index > 0 ? visible[index - 1].Points : null;
		}

		private void DrawAreaFill(PixelSurface surface, ChartSeries series, List<ChartPoint> points,
			(int X, int Y, int Width, int Height) plot,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half, Color plotBackground,
			List<ChartPoint>? floor = null)
		{
			if (points.Count == 0) return;

			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			int baseline = BaselineRow(yMin, yMax, ph, half);

			for (int i = 0; i < points.Count - 1 || (points.Count == 1 && i == 0); i++)
			{
				int x0 = PixelX(points[i].X, xMin, xMax, pw);
				int y0 = PixelYInHalf(points[i].Y, yMin, yMax, ph, half);

				int x1 = points.Count == 1 ? x0 : PixelX(points[i + 1].X, xMin, xMax, pw);
				int y1 = points.Count == 1 ? y0 : PixelYInHalf(points[i + 1].Y, yMin, yMax, ph, half);

				int span = Math.Max(1, Math.Abs(x1 - x0));

				for (int step = 0; step <= span; step++)
				{
					int px = x1 >= x0 ? x0 + step : x0 - step;
					if ((uint)px >= (uint)pw) continue;

					// Interpolate the curve's height across the segment, so the fill
					// follows the slope rather than stepping at each data point.
					double t = span == 0 ? 0 : (double)step / span;
					int py = (int)Math.Round(y0 + (y1 - y0) * t);

					// A stacked band stops at the series below; an unstacked one runs
					// all the way to the baseline.
					int bottom = baseline;
					if (floor != null)
					{
						int fi = Math.Min(i, floor.Count - 1);
						if (fi >= 0)
						{
							int f0 = PixelYInHalf(floor[fi].Y, yMin, yMax, ph, half);
							int f1 = fi + 1 < floor.Count
								? PixelYInHalf(floor[fi + 1].Y, yMin, yMax, ph, half)
								: f0;
							bottom = (int)Math.Round(f0 + (f1 - f0) * t);
						}
					}

					int from = Math.Min(py, bottom);
					int to = Math.Max(py, bottom);

					for (int y = from; y <= to; y++)
					{
						if ((uint)y >= (uint)ph) continue;
						Color at = series.Gradient != null
							? GradientColourAt(series, px, y, pw, ph)
							: series.Color;
						surface.Set(px, y, PixelSurface.Lerp(at, plotBackground,
							ControlDefaults.ChartAreaFillDim));
					}
				}

				if (points.Count == 1) break;
			}
		}

		/// <summary>The pixel row a filled area grows from.</summary>
		private static int BaselineRow(double yMin, double yMax, int pixelHeight, ChartHalf half)
		{
			// A split half grows away from the centre, so that is its baseline.
			if (half != ChartHalf.Full) return pixelHeight / 2;

			// Otherwise zero, when the axis contains it, and the floor when it does not -
			// a chart of values from 20 to 80 should fill from the bottom, not from a
			// zero line that is off the plot.
			if (yMin <= 0 && yMax >= 0) return PixelY(0, yMin, yMax, pixelHeight);
			return pixelHeight - 1;
		}

		/// <summary>Which band of the plot a series is mapped into.</summary>
		private enum ChartHalf
		{
			/// <summary>The whole plot, the ordinary case.</summary>
			Full,

			/// <summary>The upper half, growing up from the centre.</summary>
			Upper,

			/// <summary>The lower half, growing down from the centre.</summary>
			Lower,
		}

		/// <summary>Plots one series as connected segments, recording where each point landed.</summary>
		private void DrawLineSeries(PixelSurface surface, ChartSeries series, List<ChartPoint> points,
			(int X, int Y, int Width, int Height) plot,
			double xMin, double xMax, double yMin, double yMax, ChartHalf half = ChartHalf.Full)
		{
			if (points.Count == 0) return;

			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			if (points.Count == 1)
			{
				int onlyX = PixelX(points[0].X, xMin, xMax, pw);
				int onlyY = PixelYInHalf(points[0].Y, yMin, yMax, ph, half);
				surface.Set(onlyX, onlyY, series.Gradient != null
					? GradientColourAt(series, onlyX, onlyY, pw, ph)
					: series.Color);
				RecordPoint(series, 0, plot.X + onlyX, plot.Y + onlyY / 2);
				return;
			}

			for (int i = 0; i < points.Count; i++)
			{
				int px = PixelX(points[i].X, xMin, xMax, pw);
				int py = PixelYInHalf(points[i].Y, yMin, yMax, ph, half);

				// Two pixel rows share a cell row, so the cell is the pixel row halved.
				RecordPoint(series, i, plot.X + px, plot.Y + py / 2);

				if (i == points.Count - 1) break;

				int nx = PixelX(points[i + 1].X, xMin, xMax, pw);
				int ny = PixelYInHalf(points[i + 1].Y, yMin, yMax, ph, half);

				// A threshold colours the segment by the value it leaves, so a series
				// crossing a limit changes colour AT the crossing rather than the
				// whole line taking one colour and saying nothing new.
				DrawGradientLine(surface, series, px, py, nx, ny, pw, ph,
					series.Thresholds.Count > 0 ? series.ColorForValue(points[i].Y) : (Color?)null);
			}
		}

		/// <summary>
		/// Draws a segment, recolouring at every pixel when the series has a gradient.
		/// </summary>
		/// <remarks>
		/// <see cref="PixelSurface.DrawLine"/> paints one colour for the whole run, so a
		/// gradient sampled once per segment renders as a flat band per segment with a
		/// hard step between them - very visible when few points span a wide plot.
		/// Walking the segment here and colouring each pixel by its own X gives a
		/// continuous gradient, and a finer one than a per-cell-column sample can.
		/// </remarks>
		private static void DrawGradientLine(PixelSurface surface, ChartSeries series,
			int x0, int y0, int x1, int y1, int pixelWidth, int pixelHeight,
			Color? thresholdColour = null)
		{
			if (series.Gradient == null)
			{
				surface.DrawLine(x0, y0, x1, y1, thresholdColour ?? series.Color);
				return;
			}

			// Bresenham, mirroring PixelSurface.DrawLine, but choosing the colour per
			// pixel from that pixel's own position along the plot.
			int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;

			while (true)
			{
				surface.Set(x0, y0, GradientColourAt(series, x0, y0, pixelWidth, pixelHeight));

				if (x0 == x1 && y0 == y1) break;

				int e2 = err * 2;
				if (e2 >= dy) { err += dy; x0 += sx; }
				if (e2 <= dx) { err += dx; y0 += sy; }
			}
		}

		/// <summary>
		/// The gradient colour for one pixel, in the direction the series asks for.
		/// </summary>
		/// <remarks>
		/// The Y position is normalised against the plot extent, which is the axis
		/// range - not the series' own spread. That is what makes a colour mean a
		/// fixed thing: pinning YAxis.Min/Max pins the colour scale, so "red is over
		/// 80" holds across every chart that shares the axis, and two series on one
		/// plot stay comparable.
		/// </remarks>
		private static Color GradientColourAt(ChartSeries series, int px, int py,
			int pixelWidth, int pixelHeight)
		{
			double x = pixelWidth > 1 ? (double)px / (pixelWidth - 1) : 0.5;

			// Pixel rows run top-down and the axis runs bottom-up, so Y inverts:
			// the first gradient stop belongs to the lowest value.
			double y = pixelHeight > 1 ? 1.0 - (double)py / (pixelHeight - 1) : 0.5;

			double position = series.GradientAxis switch
			{
				ChartGradientAxis.Y => y,
				ChartGradientAxis.Both => (x + y) / 2.0,
				_ => x,
			};

			return series.Gradient!.Interpolate(Math.Clamp(position, 0, 1));
		}

		/// <summary>Notes where a point was drawn so hit-testing and drawing cannot disagree.</summary>
		private void RecordPoint(ChartSeries series, int index, int cellX, int cellY) =>
			_pointPositions.Add((series, index, cellX, cellY));

		/// <summary>Maps a value to a surface pixel column.</summary>
		private static int PixelX(double value, double min, double max, int pixelWidth)
		{
			if (pixelWidth <= 1) return 0;
			double range = max - min;
			int x = range > 0
				? (int)((value - min) / range * (pixelWidth - 1))
				: pixelWidth / 2;
			return Math.Clamp(x, 0, pixelWidth - 1);
		}

		/// <summary>
		/// Maps a value to a pixel row within its band of the plot.
		/// </summary>
		/// <remarks>
		/// Both split halves measure the same positive range growing away from the
		/// centre, so the two series stay comparable and a larger value always reaches
		/// further out - up in the upper half, down in the lower.
		/// </remarks>
		private static int PixelYInHalf(double value, double min, double max, int pixelHeight, ChartHalf half)
		{
			if (half == ChartHalf.Full) return PixelY(value, min, max, pixelHeight);
			if (pixelHeight <= 1) return 0;

			int centre = pixelHeight / 2;
			double range = max - min;
			double fraction = range > 0 ? Math.Clamp((value - min) / range, 0, 1) : 0.5;

			if (half == ChartHalf.Upper)
			{
				// Row 0 is the top, so a bigger value means a smaller row.
				int span = Math.Max(1, centre);
				return Math.Clamp(centre - (int)(fraction * span), 0, pixelHeight - 1);
			}

			int lowerSpan = Math.Max(1, pixelHeight - 1 - centre);
			return Math.Clamp(centre + (int)(fraction * lowerSpan), 0, pixelHeight - 1);
		}

		/// <summary>Maps a value to a surface pixel row, the top being the maximum.</summary>
		private static int PixelY(double value, double min, double max, int pixelHeight)
		{
			if (pixelHeight <= 1) return 0;
			double range = max - min;
			// A flat series has no range to scale against; it sits in the middle
			// rather than dividing by zero.
			int y = range > 0
				? pixelHeight - 1 - (int)((value - min) / range * (pixelHeight - 1))
				: pixelHeight / 2;
			return Math.Clamp(y, 0, pixelHeight - 1);
		}

		/// <summary>
		/// Presents only the cells a series actually touched.
		/// </summary>
		/// <remarks>
		/// <see cref="PixelSurface"/> stores bare colours with no written/unwritten
		/// bit, which is right for a surface that owns its whole frame. A plot well
		/// that lets the background through needs the distinction only here, so it is
		/// made here with a sentinel rather than by adding a per-pixel flag to the
		/// shared drawing layer.
		/// </remarks>
		private static void PresentSkippingUntouched(PixelSurface surface, CanvasGraphics g,
			int width, int height)
		{
			for (int cy = 0; cy < height; cy++)
			{
				for (int cx = 0; cx < width; cx++)
				{
					var top = surface.Get(cx, cy * 2);
					var bottom = surface.Get(cx, cy * 2 + 1);

					bool topClear = top == Color.Transparent;
					bool bottomClear = bottom == Color.Transparent;

					if (topClear && bottomClear) continue;

					// A half-lit cell still has to put something in its other half.
					// What was already there is the honest choice.
					if (topClear) top = g.GetCell(cx, cy).Background;
					if (bottomClear) bottom = g.GetCell(cx, cy).Background;

					if (top == bottom)
						g.SetNarrowCell(cx, cy, ' ', top, top);
					else
						g.SetNarrowCell(cx, cy, UpperHalf, top, bottom);
				}
			}
		}
	}
}
