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
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		/// <summary>True when the chart is drawn as inline gauges.</summary>
		private bool IsGauge => _plotType == ChartPlotType.Gauge;

		/// <summary>
		/// Draws one inline readout per series: label, bar, value.
		/// </summary>
		/// <remarks>
		/// A gauge has no second dimension to encode anything, so the bar's LENGTH
		/// is the reading and a gradient along it says where that reading falls on
		/// the scale. Drawn as cell text and filled cells rather than through the
		/// pixel surface, because one row has no sub-cell height to exploit.
		/// </remarks>
		private void PaintGauges(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color fg, Color bg, double yMin, double yMax)
		{
			List<ChartSeries> series;
			lock (_dataLock) { series = _series.Where(s => s.IsVisible).ToList(); }
			if (series.Count == 0) return;

			int titleRows = string.IsNullOrEmpty(_title) ? 0 : 1;
			int top = bounds.Y + Margin.Top + titleRows;
			int left = bounds.X + Margin.Left;
			int width = bounds.Width - Margin.Left - Margin.Right;
			if (width <= 0) return;

			// One column for every label, so the bars line up down the stack. A zero
			// width IS the "no labels" state, so nothing downstream re-tests the flag.
			int labelWidth = 0;
			if (_showGaugeLabel)
			{
				foreach (var s in series)
				{
					labelWidth = Math.Max(labelWidth, UnicodeWidth.GetStringWidth(s.Name));
				}
				if (labelWidth > 0) labelWidth += ControlDefaults.ChartGaugeLabelGap;
			}

			int valueWidth = 0;
			if (_showGaugeValue)
			{
				foreach (var s in series)
				{
					if (s.Points.Count == 0) continue;
					string text = YAxis.FormatValue(s.Points[^1].Y);
					valueWidth = Math.Max(valueWidth, UnicodeWidth.GetStringWidth(text));
				}
				if (valueWidth > 0) valueWidth += ControlDefaults.ChartGaugeLabelGap;
			}

			int barWidth = width - labelWidth - valueWidth;
			if (barWidth <= 0) return;

			for (int i = 0; i < series.Count; i++)
			{
				int row = top + i;
				if (row < clipRect.Y || row >= clipRect.Bottom) break;
				if (row >= bounds.Bottom) break;

				PaintOneGauge(buffer, clipRect, fg, bg, series[i], row,
					left, labelWidth, barWidth, yMin, yMax);
			}
		}

		private void PaintOneGauge(CharacterBuffer buffer, LayoutRect clipRect, Color fg, Color bg,
			ChartSeries series, int row, int left, int labelWidth, int barWidth,
			double yMin, double yMax)
		{
			if (labelWidth > 0)
			{
				var cells = MarkupParser.Parse(series.Name, fg, bg);
				buffer.WriteCellsClipped(left, row, cells, clipRect);
			}

			int barLeft = left + labelWidth;
			double value = series.Points.Count > 0 ? series.Points[^1].Y : yMin;

			double range = yMax - yMin;
			double fraction = range > 0 ? Math.Clamp((value - yMin) / range, 0, 1) : 0;
			int filled = (int)Math.Round(fraction * barWidth);

			for (int i = 0; i < barWidth; i++)
			{
				int x = barLeft + i;
				if (x < clipRect.X || x >= clipRect.Right) continue;

				if (i < filled)
				{
					// The gradient is read against the SCALE, not the bar's own
					// length: sampled along the bar, every reading would finish the
					// same colour and the tip would say nothing.
					double at = barWidth > 1 ? (double)i / (barWidth - 1) : 0;
					Color colour = series.Gradient?.Interpolate(at) ?? series.Color;
					buffer.SetNarrowCell(x, row, ControlDefaults.ChartGaugeFillChar, colour, colour);
				}
				else
				{
					buffer.SetNarrowCell(x, row, ControlDefaults.ChartGaugeTrackChar,
						ControlDefaults.ChartGaugeTrackColor, bg);
				}
			}

			if (_showGaugeValue && series.Points.Count > 0)
			{
				string text = YAxis.FormatValue(value);
				var cells = MarkupParser.Parse(text, fg, bg);
				buffer.WriteCellsClipped(barLeft + barWidth + ControlDefaults.ChartGaugeLabelGap,
					row, cells, clipRect);
			}
		}
	}
}
