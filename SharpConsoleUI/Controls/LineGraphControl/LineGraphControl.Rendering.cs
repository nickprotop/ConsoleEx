// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	public partial class LineGraphControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int borderSize = _borderStyle != BorderStyle.None ? 2 : 0;

			bool titleAndBaselineInline = _inlineTitleWithBaseline &&
			                              _titlePosition == _baselinePosition &&
			                              _showBaseline;

			int titleHeight = !string.IsNullOrEmpty(_title) && !titleAndBaselineInline ? 1 : 0;
			int baselineHeight = _showBaseline ? 1 : 0;

			int yAxisWidth = _showYAxisLabels ? GetYAxisLabelWidth() + ControlDefaults.LineGraphYAxisLabelPadding : 0;

			int titleWidth = 0;
			if (!string.IsNullOrEmpty(_title))
			{
				titleWidth = MarkupParser.StripLength(_title);
			}

			int dataCount;
			List<(LineGraphSeries series, List<double> data)> measureSnapshots;
			lock (_dataLock)
			{
				dataCount = _series.Count > 0
					? _series.Max(s => s.DataPoints.Count)
					: 0;
				measureSnapshots = _series.Select(s => (s, new List<double>(s.DataPoints))).ToList();
			}
			ComputeGlobalMinMaxFromSnapshots(measureSnapshots, out double measureMin, out double measureMax);
			int leftOverlayWidth = GetLeftOverlayWidth(measureMin, measureMax);
			int rightOverlayWidth = GetRightOverlayWidth(measureMin, measureMax);
			int leftReserved = Math.Max(yAxisWidth, leftOverlayWidth);

			int dataWidth = Width ?? Math.Max(dataCount, titleWidth);
			int contentWidth = dataWidth + leftReserved + rightOverlayWidth;
			int width = contentWidth + Margin.Left + Margin.Right + borderSize;
			int height = _graphHeight + Margin.Top + Margin.Bottom + borderSize + titleHeight + baselineHeight;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// Resolve colors
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? defaultFg;

			// Fill margins with container background
			Color containerBg = Container?.BackgroundColor ?? defaultBg;
			var effectiveBg = Color.Transparent;
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, effectiveBg);
				}
			}

			// Calculate content area
			int borderSize = _borderStyle != BorderStyle.None ? 1 : 0;

			bool titleAndBaselineInline = _inlineTitleWithBaseline &&
			                              _titlePosition == _baselinePosition &&
			                              _showBaseline;

			int titleHeight = !string.IsNullOrEmpty(_title) && !titleAndBaselineInline ? 1 : 0;
			int baselineHeight = _showBaseline ? 1 : 0;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int contentWidth = bounds.Width - Margin.Left - Margin.Right;
			int contentHeight = _graphHeight + (borderSize * 2) + titleHeight + baselineHeight;

			// Fill content area with control background (skip if same as container bg — already filled above)
			if (bgColor != containerBg)
			{
				for (int y = startY; y < startY + contentHeight && y < bounds.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						int fillX = Math.Max(startX, clipRect.X);
						int fillWidth = Math.Min(startX + contentWidth, clipRect.Right) - fillX;
						if (fillWidth > 0)
						{
							buffer.FillRect(new LayoutRect(fillX, y, fillWidth, 1), ' ', fgColor, bgColor);
						}
					}
				}
			}

			// Draw border
			if (_borderStyle != BorderStyle.None)
			{
				Color borderColor = _borderColor ?? fgColor;
				DrawBorder(buffer, startX, startY, contentWidth, contentHeight, clipRect, borderColor, bgColor);
			}

			// Calculate graph area
			int yAxisWidth = _showYAxisLabels ? GetYAxisLabelWidth() + ControlDefaults.LineGraphYAxisLabelPadding : 0;
			int graphStartY = _titlePosition == TitlePosition.Top
				? startY + borderSize + titleHeight
				: startY + borderSize;

			// Auto-fit (uses preliminary graphWidth before overlay widths are known)
			{
				int preliminaryGraphWidth = contentWidth - (borderSize * 2) - yAxisWidth;
				if (_autoFitDataPoints && preliminaryGraphWidth > 0 && preliminaryGraphWidth != _maxDataPoints)
				{
					lock (_dataLock)
					{
						_maxDataPoints = Math.Max(1, preliminaryGraphWidth);
						foreach (var series in _series)
							TrimSeriesData(series);
					}
				}
			}

			// Draw title
			if (!string.IsNullOrEmpty(_title))
			{
				int titleY = _titlePosition == TitlePosition.Top
					? startY + borderSize
					: graphStartY + _graphHeight + baselineHeight;
				int titlePadding = borderSize > 0 ? 1 : 0;
				int titleX = startX + borderSize + titlePadding;
				int maxTitleWidth = contentWidth - (borderSize * 2) - (titlePadding * 2);

				if (titleY >= clipRect.Y && titleY < clipRect.Bottom && maxTitleWidth > 0)
				{
					string processedTitle = _title;
					if (_titleColor.HasValue)
					{
						string colorName = $"rgb({_titleColor.Value.R},{_titleColor.Value.G},{_titleColor.Value.B})";
						processedTitle = $"[{colorName}]{_title}[/]";
					}

					var cells = MarkupParser.Parse(processedTitle, fgColor, effectiveBg);
					buffer.WriteCellsClipped(titleX, titleY, cells, clipRect);
				}
			}

			// Draw legend (right-aligned on title row)
			if (_showLegend && _series.Count > 0)
			{
				int legendY = _titlePosition == TitlePosition.Top
					? startY + borderSize
					: graphStartY + _graphHeight + baselineHeight;
				if (legendY >= clipRect.Y && legendY < clipRect.Bottom)
				{
					PaintLegend(buffer, legendY, startX + borderSize, contentWidth - (borderSize * 2), clipRect, fgColor, bgColor);
				}
			}

			// Snapshot data under lock
			List<(LineGraphSeries series, List<double> data)> seriesSnapshots;
			lock (_dataLock)
			{
				seriesSnapshots = _series
					.Select(s => (s, new List<double>(s.DataPoints)))
					.ToList();
			}

			// Compute global min/max for Y-axis labels
			double globalMin, globalMax;
			ComputeGlobalMinMaxFromSnapshots(seriesSnapshots, out globalMin, out globalMax);

			// Compute overlay widths and graph area horizontal bounds
			int leftOverlayWidth = GetLeftOverlayWidth(globalMin, globalMax);
			int rightOverlayWidth = GetRightOverlayWidth(globalMin, globalMax);
			int leftReserved = Math.Max(yAxisWidth, leftOverlayWidth);
			int graphStartX = startX + borderSize + leftReserved;
			int graphWidth = contentWidth - (borderSize * 2) - leftReserved - rightOverlayWidth;
			int graphBottom = graphStartY + _graphHeight - 1;

			// Draw Y-axis labels
			if (_showYAxisLabels && yAxisWidth > 0)
			{
				int labelX = startX + borderSize;
				int labelWidth = yAxisWidth - ControlDefaults.LineGraphYAxisLabelPadding;

				// Max label at top
				string maxLabel = globalMax.ToString(_axisLabelFormat);
				int maxLabelDisplayWidth = UnicodeWidth.GetStringWidth(maxLabel);
				int maxLabelX = labelX + labelWidth - maxLabelDisplayWidth;
				if (graphStartY >= clipRect.Y && graphStartY < clipRect.Bottom)
				{
					var maxCells = MarkupParser.Parse(maxLabel, _axisLabelColor, bgColor);
					buffer.WriteCellsClipped(maxLabelX, graphStartY, maxCells, clipRect);
				}

				// Min label at bottom
				string minLabel = globalMin.ToString(_axisLabelFormat);
				int minLabelDisplayWidth = UnicodeWidth.GetStringWidth(minLabel);
				int minLabelX = labelX + labelWidth - minLabelDisplayWidth;
				if (graphBottom >= clipRect.Y && graphBottom < clipRect.Bottom)
				{
					var minCells = MarkupParser.Parse(minLabel, _axisLabelColor, bgColor);
					buffer.WriteCellsClipped(minLabelX, graphBottom, minCells, clipRect);
				}
			}

			// Draw baseline
			if (_showBaseline)
			{
				int baselineY = _baselinePosition == TitlePosition.Top
					? (_titlePosition == TitlePosition.Top && !string.IsNullOrEmpty(_title) && !titleAndBaselineInline
						? startY + borderSize + 1
						: startY + borderSize)
					: graphBottom + 1;

				if (baselineY >= clipRect.Y && baselineY < clipRect.Bottom)
				{
					bool shouldInlineTitle = titleAndBaselineInline && !string.IsNullOrEmpty(_title);
					int baselineStartX = graphStartX;

					if (shouldInlineTitle)
					{
						int titlePadding = borderSize > 0 ? 1 : 0;
						int titleX = startX + borderSize + titlePadding;

						string processedTitle = _title!;
						if (_titleColor.HasValue)
						{
							string colorName = $"rgb({_titleColor.Value.R},{_titleColor.Value.G},{_titleColor.Value.B})";
							processedTitle = $"[{colorName}]{_title}[/]";
						}

						var cells = MarkupParser.Parse(processedTitle, _foregroundColorValue ?? fgColor, effectiveBg);
						buffer.WriteCellsClipped(titleX, baselineY, cells, clipRect);

						baselineStartX = titleX + cells.Count + 1;
					}

					for (int x = baselineStartX; x < graphStartX + graphWidth; x++)
					{
						if (x >= clipRect.X && x < clipRect.Right)
						{
							buffer.SetNarrowCell(x, baselineY, _baselineChar, _baselineColor, bgColor);
						}
					}
				}
			}

			// Paint graph data
			if (graphWidth <= 0 || _graphHeight <= 0)
				return;

			if (_mode == LineGraphMode.Braille)
				PaintBraille(buffer, graphStartX, graphStartY, graphWidth, _graphHeight, clipRect, bgColor, seriesSnapshots, globalMin, globalMax);
			else
				PaintAscii(buffer, graphStartX, graphStartY, graphWidth, _graphHeight, clipRect, bgColor, seriesSnapshots, globalMin, globalMax);

			// Snapshot reference lines under lock
			List<ReferenceLine> refLineSnapshot;
			lock (_dataLock) { refLineSnapshot = new List<ReferenceLine>(_referenceLines); }

			// Paint reference lines AFTER series data, only on empty cells
			// (braille fills all cells including empty U+2800, so reference lines
			// are painted on top of empty braille cells while preserving series dots)
			if (refLineSnapshot.Count > 0)
			{
				PaintReferenceLines(buffer, graphStartX, graphStartY, graphWidth, _graphHeight,
					clipRect, bgColor, refLineSnapshot, globalMin, globalMax, startX, borderSize);
			}

			// Snapshot value markers under lock
			List<ValueMarker> markerSnapshot;
			lock (_dataLock) { markerSnapshot = new List<ValueMarker>(_valueMarkers); }

			// Generate high/low markers (needs to know which rows user markers occupy)
			var userMarkerRows = new HashSet<int>();
			foreach (var m in markerSnapshot)
				userMarkerRows.Add(ComputeYRow(m.Value, graphStartY, _graphHeight, globalMin, globalMax));

			var highLowMarkers = GenerateHighLowMarkers(seriesSnapshots, globalMin, globalMax,
				userMarkerRows, graphStartY, _graphHeight);
			markerSnapshot.AddRange(highLowMarkers);

			// Paint all value markers AFTER series data
			if (markerSnapshot.Count > 0)
			{
				PaintValueMarkers(buffer, graphStartX, graphStartY, graphWidth, _graphHeight,
					clipRect, bgColor, markerSnapshot, globalMin, globalMax, startX, borderSize);
			}
		}

		#endregion

		#region Private Helpers

		private void ComputeGlobalMinMaxFromSnapshots(
			List<(LineGraphSeries series, List<double> data)> snapshots,
			out double min, out double max)
		{
			min = double.MaxValue;
			max = double.MinValue;

			foreach (var (_, data) in snapshots)
			{
				foreach (var v in data)
				{
					if (v < min) min = v;
					if (v > max) max = v;
				}
			}

			if (min == double.MaxValue)
			{
				min = 0;
				max = 1;
			}

			if (_minValue.HasValue) min = _minValue.Value;
			if (_maxValue.HasValue) max = _maxValue.Value;

			if (Math.Abs(max - min) < 0.001)
				max = min + 1.0;
		}

		private void PaintLegend(CharacterBuffer buffer, int y, int areaX, int areaWidth,
			LayoutRect clipRect, Color fgColor, Color bgColor)
		{
			// Build legend: "━ Series1  ━ Series2  ━ Series3"
			// Each entry: line char + space + name + 2 spaces gap
			List<LineGraphSeries> snapshot;
			lock (_dataLock) { snapshot = new List<LineGraphSeries>(_series); }

			var entries = new List<(string text, Color color)>();
			int totalWidth = 0;
			foreach (var s in snapshot)
			{
				// "━ Name" = 2 + name length
				int entryWidth = ControlDefaults.LineGraphLegendMarkerWidth + UnicodeWidth.GetStringWidth(s.Name);
				if (entries.Count > 0)
					totalWidth += ControlDefaults.LineGraphLegendEntryGap;
				totalWidth += entryWidth;
				entries.Add((s.Name, s.LineColor));
			}

			int legendX = areaX + areaWidth - totalWidth;
			int writeX = legendX;

			for (int i = 0; i < entries.Count; i++)
			{
				var (text, color) = entries[i];
				if (i > 0)
					writeX += ControlDefaults.LineGraphLegendEntryGap;

				// Draw line marker char
				if (writeX >= clipRect.X && writeX < clipRect.Right)
				{
					buffer.SetNarrowCell(writeX, y, ControlDefaults.LineGraphLegendMarkerChar, color, bgColor);
				}
				writeX++;

				// Space after marker
				writeX++;

				// Draw series name
				var nameCells = Parsing.MarkupParser.Parse(text, fgColor, bgColor);
				for (int c = 0; c < nameCells.Count && writeX < clipRect.Right; c++)
				{
					if (writeX >= clipRect.X)
					{
						buffer.SetCell(writeX, y, nameCells[c]);
					}
					writeX++;
				}
			}
		}

		#endregion
	}
}
