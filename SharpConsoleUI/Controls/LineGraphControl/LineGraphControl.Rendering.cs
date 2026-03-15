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

			int yAxisWidth = _showYAxisLabels ? GetYAxisLabelWidth() + Y_AXIS_LABEL_PADDING : 0;

			int titleWidth = 0;
			if (!string.IsNullOrEmpty(_title))
			{
				titleWidth = MarkupParser.StripLength(_title);
			}

			int dataCount;
			lock (_dataLock)
			{
				dataCount = _series.Count > 0
					? _series.Max(s => s.DataPoints.Count)
					: 0;
			}

			int dataWidth = Width ?? Math.Max(dataCount, titleWidth);
			int contentWidth = dataWidth + yAxisWidth;
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
			bool preserveBg = Container?.HasGradientBackground ?? false;

			// Fill margins with container background
			Color containerBg = Container?.BackgroundColor ?? defaultBg;
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, containerBg, preserveBg);
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

			// Fill content area with control background
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

			// Draw border
			if (_borderStyle != BorderStyle.None)
			{
				Color borderColor = _borderColor ?? fgColor;
				DrawBorder(buffer, startX, startY, contentWidth, contentHeight, clipRect, borderColor, bgColor);
			}

			// Calculate graph area
			int yAxisWidth = _showYAxisLabels ? GetYAxisLabelWidth() + Y_AXIS_LABEL_PADDING : 0;
			int graphStartX = startX + borderSize + yAxisWidth;
			int graphStartY = _titlePosition == TitlePosition.Top
				? startY + borderSize + titleHeight
				: startY + borderSize;
			int graphWidth = contentWidth - (borderSize * 2) - yAxisWidth;
			int graphBottom = graphStartY + _graphHeight - 1;

			// Auto-fit
			if (_autoFitDataPoints && graphWidth > 0 && graphWidth != _maxDataPoints)
			{
				lock (_dataLock)
				{
					_maxDataPoints = Math.Max(1, graphWidth);
					foreach (var series in _series)
						TrimSeriesData(series);
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

					var cells = MarkupParser.Parse(processedTitle, fgColor, bgColor);
					if (preserveBg)
						buffer.WriteCellsClippedPreservingBackground(titleX, titleY, cells, clipRect, bgColor);
					else
						buffer.WriteCellsClipped(titleX, titleY, cells, clipRect);
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

			// Draw Y-axis labels
			if (_showYAxisLabels && yAxisWidth > 0)
			{
				int labelX = startX + borderSize;
				int labelWidth = yAxisWidth - Y_AXIS_LABEL_PADDING;

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

						var cells = MarkupParser.Parse(processedTitle, _foregroundColorValue ?? fgColor, bgColor);
						if (preserveBg)
							buffer.WriteCellsClippedPreservingBackground(titleX, baselineY, cells, clipRect, bgColor);
						else
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

		#endregion
	}
}
