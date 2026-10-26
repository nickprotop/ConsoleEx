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

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		private readonly List<ChartReferenceLine> _referenceLines = new();
		private readonly List<ChartValueMarker> _valueMarkers = new();
		private bool _showHighLowLabels;

		/// <summary>The reference lines drawn across the plot.</summary>
		public IReadOnlyList<ChartReferenceLine> ReferenceLines
		{
			get { lock (_dataLock) { return _referenceLines.ToList().AsReadOnly(); } }
		}

		/// <summary>The value markers.</summary>
		public IReadOnlyList<ChartValueMarker> ValueMarkers
		{
			get { lock (_dataLock) { return _valueMarkers.ToList().AsReadOnly(); } }
		}

		/// <summary>Whether the highest and lowest points are labelled.</summary>
		public bool ShowHighLowLabels
		{
			get => _showHighLowLabels;
			set => SetProperty(ref _showHighLowLabels, value);
		}

		/// <summary>Draws a line across the plot at a fixed value.</summary>
		/// <param name="value">Where the line sits, in data coordinates.</param>
		/// <param name="color">The line colour.</param>
		/// <param name="label">Text at the line's end, if any.</param>
		/// <param name="axis">Which axis the value is measured against.</param>
		public void AddReferenceLine(double value, Color color, string? label = null,
			ChartReferenceAxis axis = ChartReferenceAxis.Y)
		{
			lock (_dataLock)
			{
				_referenceLines.Add(new ChartReferenceLine
				{
					Value = value,
					Color = color,
					Label = label,
					Axis = axis,
				});
			}

			Invalidate(Invalidation.Repaint);
		}

		/// <summary>Removes every reference line.</summary>
		public void ClearReferenceLines()
		{
			lock (_dataLock)
			{
				if (_referenceLines.Count == 0) return;
				_referenceLines.Clear();
			}

			Invalidate(Invalidation.Repaint);
		}

		/// <summary>Points an arrow and a label at a value.</summary>
		/// <param name="value">The value pointed at.</param>
		/// <param name="label">The text beside the arrow.</param>
		/// <param name="color">The marker colour.</param>
		public void AddValueMarker(double value, string label, Color color)
		{
			ArgumentNullException.ThrowIfNull(label);

			lock (_dataLock)
			{
				_valueMarkers.Add(new ChartValueMarker { Value = value, Label = label, Color = color });
			}

			Invalidate(Invalidation.Repaint);
		}

		/// <summary>Removes every value marker.</summary>
		public void ClearValueMarkers()
		{
			lock (_dataLock)
			{
				if (_valueMarkers.Count == 0) return;
				_valueMarkers.Clear();
			}

			Invalidate(Invalidation.Repaint);
		}

		/// <summary>
		/// Draws reference lines beneath the data.
		/// </summary>
		/// <remarks>
		/// Beneath, because a limit is context rather than content: the series must
		/// stay readable where it crosses one. A line outside the visible range is
		/// not drawn at all rather than clamped to an edge, which would claim a
		/// limit sits at the top of the chart when it is off the scale entirely.
		/// </remarks>
		private void PaintReferenceLines(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			double xMin, double xMax, double yMin, double yMax)
		{
			List<ChartReferenceLine> lines;
			lock (_dataLock) { lines = new List<ChartReferenceLine>(_referenceLines); }
			if (lines.Count == 0) return;

			foreach (var line in lines)
			{
				if (line.Axis == ChartReferenceAxis.Y)
				{
					if (line.Value < yMin || line.Value > yMax) continue;

					int y = plot.Y + ValueToRow(line.Value, yMin, yMax, plot.Height);
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;

					for (int x = plot.X; x < plot.X + plot.Width; x++)
					{
						if (x < clipRect.X || x >= clipRect.Right) continue;
						buffer.SetNarrowCell(x, y, ControlDefaults.ChartReferenceLineChar, line.Color, bg);
					}

					WriteLineLabel(buffer, clipRect, line, plot, y, bg);
				}
				else
				{
					if (line.Value < xMin || line.Value > xMax) continue;

					int x = plot.X + ValueToColumn(line.Value, xMin, xMax, plot.Width);
					if (x < clipRect.X || x >= clipRect.Right) continue;

					for (int y = plot.Y; y < plot.Y + plot.Height; y++)
					{
						if (y < clipRect.Y || y >= clipRect.Bottom) continue;
						buffer.SetNarrowCell(x, y, ControlDefaults.ChartReferenceVerticalChar, line.Color, bg);
					}
				}
			}
		}

		private static void WriteLineLabel(CharacterBuffer buffer, LayoutRect clipRect,
			ChartReferenceLine line, (int X, int Y, int Width, int Height) plot, int y, Color bg)
		{
			if (string.IsNullOrEmpty(line.Label)) return;

			int width = MarkupParser.StripLength(line.Label);
			int x = plot.X + plot.Width - width;
			if (x < plot.X) x = plot.X;

			var cells = MarkupParser.Parse(line.Label, line.Color, bg);
			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}

		/// <summary>
		/// Draws the line where a split plot's two halves meet.
		/// </summary>
		/// <remarks>
		/// That line is zero - both halves grow away from it - so it is the one
		/// piece of context a split chart cannot be read without. Drawn after the
		/// data, unlike a reference line, because it is the frame the two
		/// directions are measured from rather than a value within them.
		/// </remarks>
		private void PaintCentreAxis(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg)
		{
			if (!_showCentreAxis || !_splitView) return;

			int y = plot.Y + plot.Height / 2;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			Color line = ControlDefaults.ChartCentreAxisColor;

			for (int x = plot.X; x < plot.X + plot.Width; x++)
			{
				if (x < clipRect.X || x >= clipRect.Right) continue;
				buffer.SetNarrowCell(x, y, _centreAxisChar, line, bg);
			}

			// The caption is written INTO the line rather than above it, so it
			// costs no row - the same shape as SparklineControl's inline title.
			if (!string.IsNullOrEmpty(_centreAxisCaption))
			{
				var cells = MarkupParser.Parse(_centreAxisCaption, line, bg);
				buffer.WriteCellsClipped(plot.X, y, cells, clipRect);
			}

			if (_showCentreAxisValue)
			{
				// Zero when the halves straddle it, which they do by construction.
				string text = YAxis.FormatValue(0);
				int width = MarkupParser.StripLength(text);
				int x = plot.X + plot.Width - width;
				if (x < plot.X) x = plot.X;

				var cells = MarkupParser.Parse(text, line, bg);
				buffer.WriteCellsClipped(x, y, cells, clipRect);
			}
		}

		/// <summary>Draws value markers and the high/low labels, over the data.</summary>
		private void PaintMarkers(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			double xMin, double xMax, double yMin, double yMax)
		{
			List<ChartValueMarker> markers;
			lock (_dataLock) { markers = new List<ChartValueMarker>(_valueMarkers); }

			foreach (var marker in markers)
			{
				if (marker.Value < yMin || marker.Value > yMax) continue;

				int y = plot.Y + ValueToRow(marker.Value, yMin, yMax, plot.Height);
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				string text = $"{ControlDefaults.ChartMarkerArrow} {marker.Label}";
				var cells = MarkupParser.Parse(text, marker.Color, bg);
				buffer.WriteCellsClipped(plot.X, y, cells, clipRect);
			}

			if (_showHighLowLabels) PaintHighLow(buffer, plot, clipRect, bg, visible, xMin, xMax, yMin, yMax);
		}

		/// <summary>Labels each visible series' peak and trough.</summary>
		private void PaintHighLow(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			double xMin, double xMax, double yMin, double yMax)
		{
			foreach (var (series, points) in visible)
			{
				if (points.Count == 0) continue;

				ChartPoint high = points[0], low = points[0];
				foreach (var p in points)
				{
					if (p.Y > high.Y) high = p;
					if (p.Y < low.Y) low = p;
				}

				WritePointLabel(buffer, clipRect, plot, bg, series, high, xMin, xMax, yMin, yMax, above: true);
				WritePointLabel(buffer, clipRect, plot, bg, series, low, xMin, xMax, yMin, yMax, above: false);
			}
		}

		private void WritePointLabel(CharacterBuffer buffer, LayoutRect clipRect,
			(int X, int Y, int Width, int Height) plot, Color bg, ChartSeries series,
			ChartPoint point, double xMin, double xMax, double yMin, double yMax, bool above)
		{
			int col = plot.X + ValueToColumn(point.X, xMin, xMax, plot.Width);
			int row = plot.Y + ValueToRow(point.Y, yMin, yMax, plot.Height);

			// Just clear of the point, so the label does not sit on the data it names.
			int y = above ? row - 1 : row + 1;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;
			if (y < plot.Y || y >= plot.Y + plot.Height) return;

			string text = YAxis.FormatValue(point.Y);
			int width = MarkupParser.StripLength(text);

			int x = col - width / 2;
			if (x < plot.X) x = plot.X;
			if (x + width > plot.X + plot.Width) x = plot.X + plot.Width - width;

			var cells = MarkupParser.Parse(text, series.Color, bg);
			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}
	}
}
