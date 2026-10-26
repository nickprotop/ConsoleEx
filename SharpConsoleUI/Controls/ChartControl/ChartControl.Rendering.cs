// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set => SetProperty(ref _role, value);
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set => SetProperty(ref _colorRoleMode, value);
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set => SetProperty(ref _outline, value);
		}

		#endregion

		/// <summary>Rows the chrome needs: the title and the X axis labels.</summary>
		private int ChromeHeight()
		{
			int rows = 0;
			if (!string.IsNullOrEmpty(_title)) rows++;
			if (XAxis.ShowLabels) rows++;
			return rows;
		}

		/// <summary>The plot area in buffer cells, after the chrome takes its share.</summary>
		/// <remarks>
		/// Returned with a non-negative width and height even when the control is too
		/// small to show anything: callers then skip drawing rather than working with
		/// a negative extent.
		/// </remarks>
		private (int X, int Y, int Width, int Height) PlotRect(LayoutRect bounds, int gutter)
		{
			int titleRows = string.IsNullOrEmpty(_title) ? 0 : 1;
			int xLabelRows = XAxis.ShowLabels ? 1 : 0;

			int x = bounds.X + Margin.Left + gutter;
			int y = bounds.Y + Margin.Top + titleRows;
			int width = bounds.Width - Margin.Left - Margin.Right - gutter;
			int height = bounds.Height - Margin.Top - Margin.Bottom - titleRows - xLabelRows;

			return (x, y, Math.Max(0, width), Math.Max(0, height));
		}

		/// <summary>
		/// The width the Y axis labels need.
		/// </summary>
		/// <remarks>
		/// Measured with <see cref="UnicodeWidth.GetStringWidth"/>, never string.Length:
		/// a label can carry a wide glyph or a combining mark, and a column count that
		/// disagrees with the terminal's shifts the whole plot.
		/// </remarks>
		private int MeasureYGutter(IReadOnlyList<ChartTick> ticks)
		{
			if (!YAxis.ShowLabels || ticks.Count == 0) return 0;

			int widest = 0;
			foreach (var t in ticks)
			{
				widest = Math.Max(widest, UnicodeWidth.GetStringWidth(t.Label));
			}

			return widest > 0 ? widest + ControlDefaults.ChartAxisLabelPadding : 0;
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultForeground, Color defaultBackground)
		{
			ArgumentNullException.ThrowIfNull(buffer);

			_pointPositions.Clear();

			Color bg = ColorResolver.ResolveLineGraphBackground(_backgroundColorValue, Container);
			Color fg = _foregroundColorValue ?? defaultForeground;

			var snapshot = SnapshotSeries();
			var visible = snapshot.Where(s => s.Series.IsVisible && s.Points.Count > 0).ToList();

			ComputeRanges(visible, out double xMin, out double xMax, out double yMin, out double yMax);

			// The circular dependency, broken with one correction pass: tick labels
			// steal width from the plot, and the plot's height decides how many Y ticks
			// fit. Measure against the full extent, reserve the gutter, then re-select
			// for what is left. Charting libraries do exactly this rather than iterate.
			var provisional = YAxis.SelectTicks(yMin, yMax, Math.Max(0, bounds.Height));
			int gutter = MeasureYGutter(provisional);

			var plot = PlotRect(bounds, gutter);
			if (plot.Width <= 0 || plot.Height <= 0) return;

			var yTicks = YAxis.SelectTicks(yMin, yMax, plot.Height);
			var xTicks = XAxis.SelectTicks(xMin, xMax, plot.Width);

			PaintTitle(buffer, bounds, clipRect, fg, bg);
			PaintPlot(buffer, plot, clipRect, bg, visible, xMin, xMax, yMin, yMax, yTicks, xTicks);
			PaintYLabels(buffer, bounds, plot, clipRect, fg, bg, yTicks, yMin, yMax);
			PaintXLabels(buffer, plot, clipRect, fg, bg, xTicks, xMin, xMax);
		}

		/// <summary>Finds the data bounds, honouring any the axes pin.</summary>
		private void ComputeRanges(
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			out double xMin, out double xMax, out double yMin, out double yMax)
		{
			xMin = double.MaxValue; xMax = double.MinValue;
			yMin = double.MaxValue; yMax = double.MinValue;

			foreach (var (_, points) in visible)
			{
				foreach (var p in points)
				{
					if (p.X < xMin) xMin = p.X;
					if (p.X > xMax) xMax = p.X;
					if (p.Y < yMin) yMin = p.Y;
					if (p.Y > yMax) yMax = p.Y;
				}
			}

			// No data at all: a unit range keeps every downstream divisor sane and
			// still draws the frame, which is what an empty dashboard panel shows
			// before its first sample lands.
			if (xMin > xMax) { xMin = 0; xMax = 1; }
			if (yMin > yMax) { yMin = 0; yMax = 1; }

			xMin = XAxis.Min ?? xMin;
			xMax = XAxis.Max ?? xMax;
			yMin = YAxis.Min ?? yMin;
			yMax = YAxis.Max ?? yMax;
		}

		private void PaintTitle(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fg, Color bg)
		{
			if (string.IsNullOrEmpty(_title)) return;

			int y = bounds.Y + Margin.Top;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			var cells = MarkupParser.Parse(_title, fg, bg);
			buffer.WriteCellsClipped(bounds.X + Margin.Left, y, cells, clipRect);
		}

		private void PaintYLabels(CharacterBuffer buffer, LayoutRect bounds,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg,
			IReadOnlyList<ChartTick> ticks, double yMin, double yMax)
		{
			if (!YAxis.ShowLabels) return;

			foreach (var tick in ticks)
			{
				int row = ValueToRow(tick.Value, yMin, yMax, plot.Height);
				int y = plot.Y + row;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				var cells = MarkupParser.Parse(tick.Label, fg, bg);
				int width = UnicodeWidth.GetStringWidth(tick.Label);

				// Right-aligned against the plot edge, so the numbers line up.
				int x = plot.X - width - ControlDefaults.ChartAxisLabelPadding;
				if (x < bounds.X) x = bounds.X;

				buffer.WriteCellsClipped(x, y, cells, clipRect);
			}
		}

		private void PaintXLabels(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg,
			IReadOnlyList<ChartTick> ticks, double xMin, double xMax)
		{
			if (!XAxis.ShowLabels) return;

			int y = plot.Y + plot.Height;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int lastRight = int.MinValue;

			foreach (var tick in ticks)
			{
				int width = UnicodeWidth.GetStringWidth(tick.Label);
				if (width == 0) continue;

				int centre = plot.X + ValueToColumn(tick.Value, xMin, xMax, plot.Width);
				int x = centre - width / 2;
				if (x < plot.X) x = plot.X;
				if (x + width > plot.X + plot.Width) x = plot.X + plot.Width - width;

				// Labels are placed independently, so two can overlap where ticks are
				// close. Dropping the later one keeps the axis readable rather than
				// painting one label's tail over another's head.
				if (x <= lastRight) continue;

				var cells = MarkupParser.Parse(tick.Label, fg, bg);
				buffer.WriteCellsClipped(x, y, cells, clipRect);
				lastRight = x + width;
			}
		}

		/// <summary>Maps a value to a row inside the plot, top being the maximum.</summary>
		private static int ValueToRow(double value, double min, double max, int height)
		{
			if (height <= 1) return 0;
			double range = max - min;
			int row = range > 0
				? height - 1 - (int)((value - min) / range * (height - 1))
				: height / 2;
			return Math.Clamp(row, 0, height - 1);
		}

		/// <summary>Maps a value to a column inside the plot.</summary>
		private static int ValueToColumn(double value, double min, double max, int width)
		{
			if (width <= 1) return 0;
			double range = max - min;
			int col = range > 0
				? (int)((value - min) / range * (width - 1))
				: width / 2;
			return Math.Clamp(col, 0, width - 1);
		}
	}
}
