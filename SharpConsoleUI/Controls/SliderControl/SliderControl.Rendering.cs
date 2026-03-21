// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class SliderControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width, height;

			if (_orientation == SliderOrientation.Horizontal)
			{
				int contentWidth = constraints.MaxWidth - Margin.Left - Margin.Right;
				width = contentWidth + Margin.Left + Margin.Right;
				height = 1 + Margin.Top + Margin.Bottom;
			}
			else
			{
				// Use explicit Height if set; Fill alignment uses max available height;
				// otherwise use a sensible default (constraints.MaxHeight may be unbounded
				// inside ScrollablePanel)
				int maxContent = constraints.MaxHeight - Margin.Top - Margin.Bottom;
				int contentHeight;
				if (Height.HasValue)
					contentHeight = Height.Value;
				else if (VerticalAlignment == VerticalAlignment.Fill && maxContent < 10000)
					contentHeight = maxContent;
				else
					contentHeight = Math.Min(maxContent, ControlDefaults.DefaultVisibleItems);
				if (_showMinMaxLabels)
					contentHeight += 2; // min and max label rows

				int contentWidth = 1;
				if (_showMinMaxLabels || _showValueLabel)
				{
					int labelWidth = Math.Max(
						UnicodeWidth.GetStringWidth(FormatValue(_maxValue)),
						UnicodeWidth.GetStringWidth(FormatValue(_minValue)));
					if (_showValueLabel)
						labelWidth = Math.Max(labelWidth, UnicodeWidth.GetStringWidth(FormatValue(_value)));
					contentWidth += labelWidth + ControlDefaults.SliderLabelSpacing;
				}
				width = contentWidth + Margin.Left + Margin.Right;
				height = contentHeight + Margin.Top + Margin.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);
			_lastLayoutBounds = bounds;

			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			var effectiveBg = Container?.HasGradientBackground == true ? Color.Transparent : bgColor;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill margins
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, defaultFg, effectiveBg);

			if (_orientation == SliderOrientation.Horizontal)
			{
				PaintHorizontal(buffer, bounds, clipRect, startX, startY, defaultFg, effectiveBg);
			}
			else
			{
				PaintVertical(buffer, bounds, clipRect, startX, startY, defaultFg, effectiveBg);
			}

			int contentEndY = _orientation == SliderOrientation.Horizontal
				? startY + 1
				: bounds.Bottom - Margin.Bottom;
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, defaultFg, effectiveBg);
		}

		#endregion

		#region Horizontal Rendering

		private void PaintHorizontal(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			int startX, int y, Color defaultFg, Color bgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom)
				return;

			// Fill line background
			ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), defaultFg, bgColor);

			int currentX = startX;

			// Min label
			if (_showMinMaxLabels)
			{
				string minLabel = FormatValue(_minValue);
				PaintLabel(buffer, clipRect, bounds, currentX, y, minLabel, defaultFg, bgColor);
				currentX += UnicodeWidth.GetStringWidth(minLabel) + ControlDefaults.SliderLabelSpacing;
			}

			// Calculate track width
			int trackStart = currentX;
			int availableWidth = bounds.Right - Margin.Right - currentX;
			if (_showMinMaxLabels)
				availableWidth -= UnicodeWidth.GetStringWidth(FormatValue(_maxValue)) + ControlDefaults.SliderLabelSpacing;
			if (_showValueLabel)
				availableWidth -= UnicodeWidth.GetStringWidth(FormatValue(_maxValue)) + ControlDefaults.SliderLabelSpacing;

			// Reserve 2 chars for end-caps
			int trackLength = Math.Max(ControlDefaults.SliderMinTrackLength, availableWidth - 2);

			// Resolve colors
			Color trackColor = TrackColor ?? Color.Grey35;
			Color filledColor = FilledTrackColor ?? Color.Cyan1;
			Color thumbColor = ResolveThumbColor();

			// Paint left end-cap
			if (trackStart >= clipRect.X && trackStart < clipRect.Right && trackStart < bounds.Right)
				buffer.SetNarrowCell(trackStart, y, ControlDefaults.SliderHorizontalLeftCap, trackColor, bgColor);
			int trackContentStart = trackStart + 1;

			// Calculate thumb position
			int thumbPos = SliderRenderingHelper.ValueToPosition(_value, _minValue, _maxValue, trackLength);

			// Paint filled portion
			if (thumbPos > 0)
			{
				SliderRenderingHelper.PaintHorizontalTrackSegment(
					buffer, clipRect, bounds, trackContentStart, y, thumbPos,
					ControlDefaults.SliderFilledTrackChar, filledColor, bgColor);
			}

			// Paint thumb
			int thumbX = trackContentStart + thumbPos;
			if (thumbX >= clipRect.X && thumbX < clipRect.Right && thumbX < bounds.Right)
			{
				buffer.SetNarrowCell(thumbX, y, ControlDefaults.SliderThumbChar, thumbColor, bgColor);
			}

			// Paint unfilled portion
			int unfilledStart = thumbPos + 1;
			int unfilledLength = trackLength - unfilledStart;
			if (unfilledLength > 0)
			{
				SliderRenderingHelper.PaintHorizontalTrackSegment(
					buffer, clipRect, bounds, trackContentStart + unfilledStart, y, unfilledLength,
					ControlDefaults.SliderUnfilledTrackChar, trackColor, bgColor);
			}

			// Paint right end-cap
			int rightCapX = trackContentStart + trackLength;
			if (rightCapX >= clipRect.X && rightCapX < clipRect.Right && rightCapX < bounds.Right)
				buffer.SetNarrowCell(rightCapX, y, ControlDefaults.SliderHorizontalRightCap, trackColor, bgColor);

			currentX = rightCapX + 1;

			// Max label
			if (_showMinMaxLabels)
			{
				currentX += ControlDefaults.SliderLabelSpacing;
				string maxLabel = FormatValue(_maxValue);
				PaintLabel(buffer, clipRect, bounds, currentX, y, maxLabel, defaultFg, bgColor);
				currentX += UnicodeWidth.GetStringWidth(maxLabel);
			}

			// Value label
			if (_showValueLabel)
			{
				currentX += ControlDefaults.SliderLabelSpacing;
				string valueLabel = FormatValue(_value);
				PaintLabel(buffer, clipRect, bounds, currentX, y, valueLabel, defaultFg, bgColor);
			}
		}

		#endregion

		#region Vertical Rendering

		private void PaintVertical(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			int startX, int startY, Color defaultFg, Color bgColor)
		{
			int currentY = startY;

			// Max label (top)
			if (_showMinMaxLabels)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), defaultFg, bgColor);
					string maxLabel = FormatValue(_maxValue);
					PaintLabel(buffer, clipRect, bounds, startX, currentY, maxLabel, defaultFg, bgColor);
				}
				currentY++;
			}

			// Calculate track height (reserve 2 for end-caps)
			int trackStart = currentY;
			int availableHeight = bounds.Bottom - Margin.Bottom - currentY;
			if (_showMinMaxLabels) availableHeight -= 1; // min label row
			int trackLength = Math.Max(ControlDefaults.SliderMinTrackLength, availableHeight - 2);

			// Resolve colors
			Color trackColor = TrackColor ?? Color.Grey35;
			Color filledColor = FilledTrackColor ?? Color.Cyan1;
			Color thumbColor = ResolveThumbColor();

			// Paint top end-cap
			if (trackStart >= clipRect.Y && trackStart < clipRect.Bottom && trackStart < bounds.Bottom)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, trackStart, bounds.Width, 1), defaultFg, bgColor);
				if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
					buffer.SetNarrowCell(startX, trackStart, ControlDefaults.SliderVerticalTopCap, trackColor, bgColor);
			}
			int trackContentStart = trackStart + 1;

			// Calculate thumb position (inverted: top=max, bottom=min)
			int thumbPos = SliderRenderingHelper.ValueToPosition(_value, _minValue, _maxValue, trackLength);
			int thumbRow = trackLength - 1 - thumbPos; // Invert for vertical

			// Paint track
			for (int i = 0; i < trackLength; i++)
			{
				int paintY = trackContentStart + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, bounds.Width, 1), defaultFg, bgColor);

					if (i == thumbRow)
					{
						// Thumb
						if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderThumbChar, thumbColor, bgColor);
					}
					else if (i > thumbRow)
					{
						// Filled (below thumb = lower values)
						if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderVerticalFilledTrackChar, filledColor, bgColor);
					}
					else
					{
						// Unfilled (above thumb = higher values)
						if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderVerticalTrackChar, trackColor, bgColor);
					}

					// Value label next to thumb
					if (i == thumbRow && _showValueLabel)
					{
						string valueLabel = FormatValue(_value);
						PaintLabel(buffer, clipRect, bounds, startX + ControlDefaults.SliderLabelSpacing + 1, paintY, valueLabel, defaultFg, bgColor);
					}
				}
			}

			// Paint bottom end-cap
			int bottomCapY = trackContentStart + trackLength;
			if (bottomCapY >= clipRect.Y && bottomCapY < clipRect.Bottom && bottomCapY < bounds.Bottom)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, bottomCapY, bounds.Width, 1), defaultFg, bgColor);
				if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
					buffer.SetNarrowCell(startX, bottomCapY, ControlDefaults.SliderVerticalBottomCap, trackColor, bgColor);
			}

			currentY = bottomCapY + 1;

			// Min label (bottom)
			if (_showMinMaxLabels)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), defaultFg, bgColor);
					string minLabel = FormatValue(_minValue);
					PaintLabel(buffer, clipRect, bounds, startX, currentY, minLabel, defaultFg, bgColor);
				}
			}
		}

		#endregion

		#region Shared Rendering Helpers

		private Color ResolveThumbColor()
		{
			if (!_isEnabled) return Color.Grey;
			if (_isDragging) return Color.Yellow;
			if (_hasFocus) return FocusedThumbColor ?? Color.Yellow;
			return ThumbColor ?? Color.White;
		}

		private static void PaintLabel(CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int x, int y, string label, Color fg, Color bg)
		{
			for (int i = 0; i < label.Length; i++)
			{
				int paintX = x + i;
				if (paintX >= clipRect.X && paintX < clipRect.Right && paintX < bounds.Right)
				{
					Color cellBg = bg;
					buffer.SetNarrowCell(paintX, y, label[i], fg, cellBg);
				}
			}
		}

		#endregion
	}
}
