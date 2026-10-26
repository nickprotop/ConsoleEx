// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
		private void PaintPlot(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color controlBackground,
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			double xMin, double xMax, double yMin, double yMax,
			IReadOnlyList<ChartTick> yTicks, IReadOnlyList<ChartTick> xTicks)
		{
			// Transparent means "let what is behind show through", and it arrives two
			// ways: the caller asks for it, or nobody named a plot colour and the theme
			// resolver returned its Color.Transparent default, which means the same.
			Color fill = _plotBackground ?? controlBackground;
			bool transparent = fill == Color.Transparent;

			var surface = _surface ??= new PixelSurface();
			surface.EnsureSize(plot.Width, plot.Height);
			surface.Fill(fill);

			foreach (var (series, points) in visible)
			{
				var type = series.PlotType ?? _plotType;
				switch (type)
				{
					case ChartPlotType.Line:
						DrawLineSeries(surface, series, points, plot, xMin, xMax, yMin, yMax);
						break;
					default:
						// Area, Bar and Scatter arrive in later slices; until then a
						// series renders as a line rather than vanishing silently.
						DrawLineSeries(surface, series, points, plot, xMin, xMax, yMin, yMax);
						break;
				}
			}

			var g = new CanvasGraphics(buffer, plot.X, plot.Y, plot.Width, plot.Height, clipRect);

			if (transparent)
				PresentSkippingUntouched(surface, g, plot.Width, plot.Height);
			else
				surface.Present(g, fill);
		}

		/// <summary>Plots one series as connected segments, recording where each point landed.</summary>
		private void DrawLineSeries(PixelSurface surface, ChartSeries series, List<ChartPoint> points,
			(int X, int Y, int Width, int Height) plot,
			double xMin, double xMax, double yMin, double yMax)
		{
			if (points.Count == 0) return;

			int pw = surface.Width, ph = surface.Height;
			if (pw <= 0 || ph <= 0) return;

			if (points.Count == 1)
			{
				int onlyX = PixelX(points[0].X, xMin, xMax, pw);
				int onlyY = PixelY(points[0].Y, yMin, yMax, ph);
				surface.Set(onlyX, onlyY, series.Gradient != null
					? GradientColourAt(series, onlyX, onlyY, pw, ph)
					: series.Color);
				RecordPoint(series, 0, plot.X + onlyX, plot.Y + onlyY / 2);
				return;
			}

			for (int i = 0; i < points.Count; i++)
			{
				int px = PixelX(points[i].X, xMin, xMax, pw);
				int py = PixelY(points[i].Y, yMin, yMax, ph);

				// Two pixel rows share a cell row, so the cell is the pixel row halved.
				RecordPoint(series, i, plot.X + px, plot.Y + py / 2);

				if (i == points.Count - 1) break;

				int nx = PixelX(points[i + 1].X, xMin, xMax, pw);
				int ny = PixelY(points[i + 1].Y, yMin, yMax, ph);

				DrawGradientLine(surface, series, px, py, nx, ny, pw, ph);
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
			int x0, int y0, int x1, int y1, int pixelWidth, int pixelHeight)
		{
			if (series.Gradient == null)
			{
				surface.DrawLine(x0, y0, x1, y1, series.Color);
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
