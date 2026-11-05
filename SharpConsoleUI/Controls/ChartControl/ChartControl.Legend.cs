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
		/// <summary>The swatch drawn before each legend entry.</summary>
		private const char LegendSwatch = '■';

		/// <summary>
		/// Where each legend entry was drawn, so a click can find it.
		/// </summary>
		/// <remarks>
		/// Recorded as the legend paints and consumed by hit-testing, so the two
		/// cannot disagree about where an entry is. Cleared on every paint.
		/// </remarks>
		private readonly List<(ChartSeries Series, LayoutRect Rect)> _legendRegions = new();

		/// <summary>Where each legend entry was drawn. For tests.</summary>
		internal IReadOnlyList<(ChartSeries Series, LayoutRect Rect)> LegendRegionsForTest() =>
			_legendRegions;

		/// <summary>True when a legend should be drawn at all.</summary>
		private bool LegendVisible => _showLegend && _legendPosition != ChartLegendPosition.None;

		/// <summary>Rows the legend reserves from the plot.</summary>
		private int LegendRows()
		{
			if (!LegendVisible) return 0;

			// Only the stacked positions cost a row; left and right take columns.
			return _legendPosition is ChartLegendPosition.Top or ChartLegendPosition.Bottom ? 1 : 0;
		}

		/// <summary>Columns the last layout reserved for a side legend.</summary>
		private int _lastLegendColumns;

		/// <summary>True when the legend stands beside the plot rather than above or below it.</summary>
		private bool LegendIsSide =>
			LegendVisible && _legendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right;

		/// <summary>
		/// Columns a side legend reserves from the plot.
		/// </summary>
		/// <remarks>
		/// Measured from the widest entry rather than fixed, so a legend of short
		/// names does not steal width a long one would need - and a single
		/// runaway name cannot eat the plot, because the result is capped at a
		/// fraction of the control. A colour bar is a scale, not a list, and
		/// stays horizontal; asking for it on a side falls back to the bottom.
		/// </remarks>
		private int LegendColumns(int availableWidth)
		{
			if (!LegendIsSide || _legendStyle == ChartLegendStyle.ColorBar) return 0;

			List<ChartSeries> series;
			lock (_dataLock) { series = new List<ChartSeries>(_series); }
			if (series.Count == 0) return 0;

			int widest = 0;
			foreach (var s in series)
			{
				int w = UnicodeWidth.GetStringWidth(s.Name);
				if (w > widest) widest = w;
			}

			// swatch + space + name, then a gap separating it from the plot.
			int needed = 1 + 1 + widest + ControlDefaults.ChartLegendGap;

			int cap = Math.Max(0, availableWidth * ControlDefaults.ChartSideLegendMaxPercent / 100);
			return Math.Min(needed, cap);
		}

		/// <summary>
		/// Draws the legend and records where each entry landed.
		/// </summary>
		/// <remarks>
		/// Every series is listed, including hidden ones: the legend is how a hidden
		/// series gets turned back on, so dropping it would make it unreachable.
		/// A hidden entry is dimmed rather than removed.
		/// </remarks>
		private void PaintLegend(CharacterBuffer buffer, LayoutRect bounds,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg)
		{
			_legendRegions.Clear();
			if (!LegendVisible) return;

			List<ChartSeries> series;
			lock (_dataLock) { series = new List<ChartSeries>(_series); }
			if (series.Count == 0) return;

			if (_legendStyle == ChartLegendStyle.ColorBar)
			{
				PaintColorBar(buffer, plot, clipRect, fg, bg, series);
				return;
			}

			if (LegendIsSide)
			{
				PaintSideLegend(buffer, plot, clipRect, fg, bg, series);
				return;
			}

			int row = _legendPosition == ChartLegendPosition.Top
				? plot.Y - 1
				: plot.Y + plot.Height + (XAxis.ShowLabels ? 1 : 0);

			if (row < clipRect.Y || row >= clipRect.Bottom) return;

			int x = plot.X;
			int right = Math.Min(plot.X + plot.Width, clipRect.Right);

			foreach (var s in series)
			{
				// swatch + space + name + two trailing spaces
				int nameWidth = UnicodeWidth.GetStringWidth(s.Name);
				int entryWidth = 1 + 1 + nameWidth + ControlDefaults.ChartLegendGap;

				// A legend that ran past its own edge would paint over the plot or
				// the border; stopping is better than overflowing.
				if (x + entryWidth > right) break;

				Color swatch = s.IsVisible ? s.Color : ControlDefaults.ChartLegendHiddenColor;
				Color label = s.IsVisible ? fg : ControlDefaults.ChartLegendHiddenColor;

				buffer.SetNarrowCell(x, row, LegendSwatch, swatch, bg);

				var cells = Parsing.MarkupParser.Parse(s.Name, label, bg);
				buffer.WriteCellsClipped(x + 2, row, cells, clipRect);

				_legendRegions.Add((s, new LayoutRect(x, row, entryWidth, 1)));
				x += entryWidth;
			}
		}

		/// <summary>
		/// Draws a legend beside the plot, one series per row.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Stacked rather than run along a line, because a side legend's whole
		/// purpose is to trade width for height: a reader scans a column of names
		/// the way they scan the series, and a horizontal list squeezed into a
		/// narrow column would wrap into nonsense.
		/// </para>
		/// <para>
		/// Entries start at the plot's top row. A name wider than the reserved
		/// column is clipped by the buffer rather than wrapped - a legend is a key,
		/// and half a name on two rows reads worse than a truncated one.
		/// </para>
		/// </remarks>
		private void PaintSideLegend(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect,
			Color fg, Color bg, List<ChartSeries> series)
		{
			// What PlotRect actually reserved, not a fresh measurement: recomputing
			// here against the already-narrowed plot width would disagree with the
			// layout and put the entries in the wrong column.
			int columns = _lastLegendColumns;
			if (columns <= 0) return;

			// Left sits in the space before the plot; right in the space after it.
			int x = _legendPosition == ChartLegendPosition.Left
				? plot.X - columns
				: plot.X + plot.Width + ControlDefaults.ChartLegendGap;

			if (x < clipRect.X) x = clipRect.X;

			for (int i = 0; i < series.Count; i++)
			{
				int row = plot.Y + i;

				// One row each, and the plot's height is the budget: a chart with
				// more series than rows lists what fits rather than painting over
				// the axis labels underneath.
				if (i >= plot.Height) break;
				if (row < clipRect.Y || row >= clipRect.Bottom) continue;
				if (x >= clipRect.Right) break;

				var s = series[i];
				Color swatch = s.IsVisible ? s.Color : ControlDefaults.ChartLegendHiddenColor;
				Color label = s.IsVisible ? fg : ControlDefaults.ChartLegendHiddenColor;

				buffer.SetNarrowCell(x, row, LegendSwatch, swatch, bg);

				var cells = Parsing.MarkupParser.Parse(s.Name, label, bg);
				buffer.WriteCellsClipped(x + 2, row, cells, clipRect);

				_legendRegions.Add((s, new LayoutRect(x, row, columns, 1)));
			}
		}

		/// <summary>
		/// Draws the scale a gradient series is coloured by, labelled with its ends.
		/// </summary>
		/// <remarks>
		/// The bar is sampled across its own width, so it shows the gradient as a
		/// continuum rather than as the handful of stops it was built from. The end
		/// labels are what make it readable: without them a reader sees that the
		/// colour means something but cannot say what.
		/// <para>
		/// Falls back to swatches when no visible series carries a gradient, since
		/// a scale for a single flat colour would say less than a swatch does.
		/// </para>
		/// </remarks>
		private void PaintColorBar(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect,
			Color fg, Color bg, List<ChartSeries> series)
		{
			var scale = series.FirstOrDefault(s => s.IsVisible && s.Gradient != null);
			if (scale?.Gradient == null) return;

			int row = _legendPosition == ChartLegendPosition.Top
				? plot.Y - 1
				: plot.Y + plot.Height + (XAxis.ShowLabels ? 1 : 0);

			if (row < clipRect.Y || row >= clipRect.Bottom) return;

			// The scale spans the axis the colour is measured against, which is the
			// whole point: the same colour means the same value on every chart that
			// shares it.
			double min = YAxis.Min ?? 0;
			double max = YAxis.Max ?? 1;

			string lowLabel = YAxis.FormatValue(min);
			string highLabel = YAxis.FormatValue(max);

			int lowWidth = UnicodeWidth.GetStringWidth(lowLabel);
			int highWidth = UnicodeWidth.GetStringWidth(highLabel);

			int right = Math.Min(plot.X + plot.Width, clipRect.Right);
			int barLeft = plot.X + lowWidth + 1;
			int barWidth = right - barLeft - highWidth - 1;
			if (barWidth < ControlDefaults.ChartColorBarMinWidth) return;

			var lowCells = Parsing.MarkupParser.Parse(lowLabel, fg, bg);
			buffer.WriteCellsClipped(plot.X, row, lowCells, clipRect);

			for (int i = 0; i < barWidth; i++)
			{
				double t = barWidth == 1 ? 0 : (double)i / (barWidth - 1);
				Color at = scale.Gradient.Interpolate(t);
				buffer.SetNarrowCell(barLeft + i, row,
					ControlDefaults.ChartColorBarChar, at, at);
			}

			var highCells = Parsing.MarkupParser.Parse(highLabel, fg, bg);
			buffer.WriteCellsClipped(barLeft + barWidth + 1, row, highCells, clipRect);

			_legendRegions.Add((scale, new LayoutRect(plot.X, row, right - plot.X, 1)));
		}
	}
}
