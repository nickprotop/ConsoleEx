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
	public partial class SparklineControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int borderSize = _borderStyle != BorderStyle.None ? 2 : 0;

			// Check if bidirectional mode
			bool isBidirectional = _mode == SparklineMode.Bidirectional || _mode == SparklineMode.BidirectionalBraille;

			// When title is inline with baseline, don't count title height separately
			bool titleAndBaselineInline = _inlineTitleWithBaseline &&
			                              _titlePosition == _baselinePosition &&
			                              _showBaseline;

			int titleHeight = !string.IsNullOrEmpty(_title) && !titleAndBaselineInline ? 1 : 0;

			// In bidirectional mode, baseline is in the middle (doesn't add height)
			// In standard mode, baseline adds height if not inline with title
			int baselineHeight = _showBaseline && !isBidirectional ? 1 : 0;

			// Calculate minimum width needed for title (visible chars only, not markup)
			int titleWidth = 0;
			if (!string.IsNullOrEmpty(_title))
			{
				titleWidth = Parsing.MarkupParser.StripLength(_title);
			}

			// Width should be max of: explicit width, data points, or title width
			int dataCount;
			lock (_dataLock)
			{
				dataCount = _dataPoints.Count;
			}
			int dataWidth = Width ?? dataCount;
			int contentWidth = Math.Max(dataWidth, titleWidth);
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
			Color bgColor = ColorResolver.ResolveSparklineBackground(_backgroundColorValue, Container);
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? defaultFg;
			var effectiveBg = Color.Transparent;
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, effectiveBg);
				}
			}

			// Calculate content area (after margins, including border, title, and baseline)
			int borderSize = _borderStyle != BorderStyle.None ? 1 : 0;

			// When title is inline with baseline, don't count title height separately
			bool titleAndBaselineInline = _inlineTitleWithBaseline &&
			                              _titlePosition == _baselinePosition &&
			                              _showBaseline;

			int titleHeight = !string.IsNullOrEmpty(_title) && !titleAndBaselineInline ? 1 : 0;

			// Baseline height will be determined after we know if bidirectional mode
			int baselineHeight = 0;  // Will be set later

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

			// Draw border if enabled
			if (_borderStyle != BorderStyle.None)
			{
				Color borderColor = _borderColor ?? fgColor;
				DrawBorder(buffer, startX, startY, contentWidth, contentHeight, clipRect, borderColor, bgColor);
			}

			// Calculate graph area (inside border if present)
			// Title position affects whether graph or title comes first vertically
			int graphStartX = startX + borderSize;
			int graphStartY = _titlePosition == TitlePosition.Top
				? startY + borderSize + titleHeight  // Graph below title
				: startY + borderSize;               // Graph at top, title below
			int graphWidth = contentWidth - (borderSize * 2);
			int graphBottom = graphStartY + _graphHeight - 1;

			// Auto-fit: adjust max data points to match rendered width
			if (_autoFitDataPoints && graphWidth > 0 && graphWidth != _maxDataPoints)
			{
				lock (_dataLock)
				{
					_maxDataPoints = Math.Max(1, graphWidth);
					TrimDataPoints();
				}
			}

			// Draw title if present (position depends on TitlePosition setting)
			if (!string.IsNullOrEmpty(_title))
			{
				int titleY = _titlePosition == TitlePosition.Top
					? startY + borderSize                    // Title above graph
					: graphStartY + _graphHeight;            // Title below graph
				int titlePadding = borderSize > 0 ? 1 : 0; // Only pad when border is present
				int titleX = startX + borderSize + titlePadding;
				int maxTitleWidth = contentWidth - (borderSize * 2) - (titlePadding * 2);

				if (titleY >= clipRect.Y && titleY < clipRect.Bottom && maxTitleWidth > 0)
				{
					// Wrap title with color if TitleColor is set
					string processedTitle = _title;
					if (_titleColor.HasValue)
					{
						string colorName = $"rgb({_titleColor.Value.R},{_titleColor.Value.G},{_titleColor.Value.B})";
						processedTitle = $"[{colorName}]{_title}[/]";
					}

					// Parse markup to cells
					var cells = Parsing.MarkupParser.Parse(processedTitle, fgColor, effectiveBg);
					buffer.WriteCellsClipped(titleX, titleY, cells, clipRect);
				}
			}

			// Snapshot data points under lock for thread safety
			List<double> dataPointsSnapshot;
			List<double> secondaryDataPointsSnapshot;
			lock (_dataLock)
			{
				dataPointsSnapshot = new List<double>(_dataPoints);
				secondaryDataPointsSnapshot = new List<double>(_secondaryDataPoints);
			}

			if (dataPointsSnapshot.Count == 0 && secondaryDataPointsSnapshot.Count == 0)
				return;

			// Check if we're in bidirectional mode
			bool isBidirectional = _mode == SparklineMode.Bidirectional || _mode == SparklineMode.BidirectionalBraille;
			bool useBraille = _mode == SparklineMode.Braille || _mode == SparklineMode.BidirectionalBraille;

			// Now that we know if bidirectional, set baseline height
			// In bidirectional mode, baseline is in the middle (doesn't add height)
			// In standard mode, baseline adds height if not inline with title
			baselineHeight = _showBaseline && !isBidirectional ? 1 : 0;

			// Draw baseline BEFORE graph data so data can draw over it
			if (_showBaseline)
			{
				// Determine baseline Y position based on mode and BaselinePosition
				int baselineY;

				if (isBidirectional)
				{
					// In bidirectional mode, baseline is ALWAYS at the middle (centerline)
					// This is the horizontal line between upload (top) and download (bottom)
					int halfHeight = _graphHeight / 2;
					baselineY = graphStartY + halfHeight;
				}
				else if (_baselinePosition == TitlePosition.Top)
				{
					// Standard mode: Baseline at top (before graph, after title if title is also on top)
					baselineY = _titlePosition == TitlePosition.Top && !string.IsNullOrEmpty(_title) && !titleAndBaselineInline
						? startY + borderSize + 1  // After title
						: startY + borderSize;      // No title or title at bottom or inline
				}
				else
				{
					// Standard mode: Baseline at bottom (after graph, before title if title is also at bottom)
					baselineY = graphBottom + 1;
					// If title is at bottom and not inline, baseline comes first
					if (_titlePosition == TitlePosition.Bottom && !string.IsNullOrEmpty(_title) && !titleAndBaselineInline)
					{
						baselineY = graphBottom + 1;
					}
				}

				if (baselineY >= clipRect.Y && baselineY < clipRect.Bottom)
				{
					// Check if we should inline title with baseline
					// Inline only when title and baseline are on the same side (both top or both bottom)
					bool shouldInlineTitle = titleAndBaselineInline && !string.IsNullOrEmpty(_title);

					int baselineStartX = graphStartX;

					if (shouldInlineTitle)
					{
						// Render title at start of baseline
						int titlePadding = borderSize > 0 ? 1 : 0;
						int titleX = startX + borderSize + titlePadding;

						// Convert title markup to ANSI
						string processedTitle = _title!;
						if (_titleColor.HasValue)
						{
							string colorName = $"rgb({_titleColor.Value.R},{_titleColor.Value.G},{_titleColor.Value.B})";
							processedTitle = $"[{colorName}]{_title}[/]";
						}

						var cells = Parsing.MarkupParser.Parse(processedTitle, _foregroundColorValue ?? fgColor, effectiveBg);
						buffer.WriteCellsClipped(titleX, baselineY, cells, clipRect);

						// Advance baseline start position past title
						int titleWidth = cells.Count;
						baselineStartX = titleX + titleWidth + 1; // +1 for space after title
					}

					// Fill rest of line with baseline characters
					for (int x = baselineStartX; x < graphStartX + graphWidth; x++)
					{
						if (x >= clipRect.X && x < clipRect.Right)
						{
							buffer.SetNarrowCell(x, baselineY, _baselineChar, _baselineColor, bgColor);
						}
					}
				}
			}

			// Paint graph data AFTER baseline so data can draw over it
			if (isBidirectional)
			{
				PaintBidirectional(buffer, graphStartX, graphStartY, graphWidth, _graphHeight, graphBottom, clipRect, bgColor, useBraille, dataPointsSnapshot, secondaryDataPointsSnapshot);
			}
			else
			{
				PaintStandard(buffer, graphStartX, graphStartY, graphWidth, _graphHeight, graphBottom, clipRect, bgColor, useBraille, dataPointsSnapshot);
			}
		}

		private void PaintStandard(CharacterBuffer buffer, int graphStartX, int graphStartY, int graphWidth, int graphHeight, int graphBottom, LayoutRect clipRect, Color bgColor, bool useBraille, List<double> dataPoints)
		{
			if (dataPoints.Count == 0)
				return;

			// Calculate scale
			double min = _minValue ?? Math.Min(0, dataPoints.Min());
			double max = _maxValue ?? Math.Max(dataPoints.Max(), min + 1.0);
			double range = max - min;

			if (range < 0.001)
				range = 1.0;

			// Determine which data points to display
			int availableWidth = graphWidth;
			int displayCount = Math.Min(dataPoints.Count, availableWidth);
			int startIndex = Math.Max(0, dataPoints.Count - displayCount);

			// Paint vertical bars
			for (int i = 0; i < displayCount; i++)
			{
				int dataIndex = startIndex + i;
				if (dataIndex >= dataPoints.Count)
					break;

				double value = dataPoints[dataIndex];
				double normalized = (value - min) / range; // 0.0 to 1.0
				double barHeight = normalized * graphHeight;

				int paintX = graphStartX + i;
				if (paintX < clipRect.X || paintX >= clipRect.Right)
					continue;

				// Paint vertical bar from bottom to top
				for (int y = 0; y < graphHeight; y++)
				{
					int paintY = graphBottom - y;
					if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
						continue;

					char displayChar = GetBarChar(barHeight, y, useBraille);

					// Determine cell color based on whether the character is "empty"
					bool isEmpty = useBraille ? displayChar == BRAILLE_CHARS[0] : displayChar == ' ';
					Color cellColor;
					if (isEmpty)
					{
						cellColor = Color.Grey19;
					}
					else if (_gradient != null)
					{
						// Vertical gradient: color based on row position (bottom to top)
						double rowHeightNormalized = (double)(y + 1) / graphHeight;
						cellColor = _gradient.Interpolate(rowHeightNormalized);
					}
					else
					{
						cellColor = _barColor;
					}
					buffer.SetNarrowCell(paintX, paintY, displayChar, cellColor, bgColor);
				}
			}
		}

		private void PaintBidirectional(CharacterBuffer buffer, int graphStartX, int graphStartY, int graphWidth, int graphHeight, int graphBottom, LayoutRect clipRect, Color bgColor, bool useBraille, List<double> dataPoints, List<double> secondaryDataPoints)
		{
			// In bidirectional mode:
			// - Primary series (upload) goes UP from the middle
			// - Secondary series (download) goes DOWN from the middle
			int topHalfHeight = graphHeight / 2;
			int bottomHalfHeight = graphHeight - topHalfHeight - 1; // -1 for baseline row
			int middleY = graphStartY + topHalfHeight;

			// Calculate scales for both series
			double primaryMax = _maxValue ?? (dataPoints.Count > 0 ? Math.Max(dataPoints.Max(), 0.001) : 1.0);
			double secondaryMax = _secondaryMaxValue ?? (secondaryDataPoints.Count > 0 ? Math.Max(secondaryDataPoints.Max(), 0.001) : primaryMax);

			// Determine display count based on larger dataset
			int availableWidth = graphWidth;
			int primaryCount = dataPoints.Count;
			int secondaryCount = secondaryDataPoints.Count;
			int maxDataCount = Math.Max(primaryCount, secondaryCount);
			int displayCount = Math.Min(maxDataCount, availableWidth);

			int primaryStartIndex = Math.Max(0, primaryCount - displayCount);
			int secondaryStartIndex = Math.Max(0, secondaryCount - displayCount);

			// Paint each column
			for (int i = 0; i < displayCount; i++)
			{
				int paintX = graphStartX + i;
				if (paintX < clipRect.X || paintX >= clipRect.Right)
					continue;

				// Paint primary (upload) - goes UP from middle
				int primaryDataIndex = primaryStartIndex + i;
				if (primaryDataIndex < primaryCount)
				{
					double value = dataPoints[primaryDataIndex];
					double normalized = Math.Clamp(value / primaryMax, 0, 1);
					double barHeight = normalized * topHalfHeight;

					for (int y = 0; y < topHalfHeight; y++)
					{
						int paintY = middleY - 1 - y; // Paint upward from middle
						if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
							continue;

						char displayChar = GetBarChar(barHeight, y, useBraille);
						bool isEmpty = useBraille ? displayChar == BRAILLE_CHARS[0] : displayChar == ' ';
						Color cellColor;
						if (isEmpty)
						{
							cellColor = Color.Grey19;
						}
						else if (_gradient != null)
						{
							double rowHeightNormalized = (double)(y + 1) / topHalfHeight;
							cellColor = _gradient.Interpolate(rowHeightNormalized);
						}
						else
						{
							cellColor = _barColor;
						}
						buffer.SetNarrowCell(paintX, paintY, displayChar, cellColor, bgColor);
					}
				}

				// Paint secondary (download) - goes DOWN from middle
				int secondaryDataIndex = secondaryStartIndex + i;
				if (secondaryDataIndex < secondaryCount)
				{
					double value = secondaryDataPoints[secondaryDataIndex];
					double normalized = Math.Clamp(value / secondaryMax, 0, 1);
					double barHeight = normalized * bottomHalfHeight;

					for (int y = 0; y < bottomHalfHeight; y++)
					{
						int paintY = middleY + 1 + y; // Paint downward from middle (skip baseline row)
						if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
							continue;

						// For downward bars, we use inverted block chars (▔▀ style) or just fill from top
						char displayChar = GetBarCharInverted(barHeight, y, useBraille);
						bool isEmpty = useBraille ? displayChar == BRAILLE_CHARS[0] : displayChar == ' ';
						Color cellColor;
						if (isEmpty)
						{
							cellColor = Color.Grey19;
						}
						else if (_secondaryGradient != null)
						{
							// Secondary has its own gradient
							double rowHeightNormalized = (double)(y + 1) / bottomHalfHeight;
							cellColor = _secondaryGradient.Interpolate(rowHeightNormalized);
						}
						else if (_gradient != null)
						{
							// Fall back to primary gradient if no secondary gradient
							double rowHeightNormalized = (double)(y + 1) / bottomHalfHeight;
							cellColor = _gradient.Interpolate(rowHeightNormalized);
						}
						else
						{
							cellColor = _secondaryBarColor;
						}
						buffer.SetNarrowCell(paintX, paintY, displayChar, cellColor, bgColor);
					}
				}
			}
		}

		#endregion
	}
}
