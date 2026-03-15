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
	public partial class RangeSliderControl
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
				// otherwise use a sensible default
				int maxContent = constraints.MaxHeight - Margin.Top - Margin.Bottom;
				int contentHeight;
				if (Height.HasValue)
					contentHeight = Height.Value;
				else if (VerticalAlignment == VerticalAlignment.Fill && maxContent < 10000)
					contentHeight = maxContent;
				else
					contentHeight = Math.Min(maxContent, ControlDefaults.DefaultVisibleItems);
				if (_showMinMaxLabels)
					contentHeight += 2;

				int contentWidth = 1;
				if (_showMinMaxLabels || _showValueLabel)
				{
					int labelWidth = Math.Max(
						FormatValue(_maxValue).Length,
						FormatValue(_minValue).Length);
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
			bool preserveBg = Container?.HasGradientBackground ?? false;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, defaultFg, bgColor, preserveBg);

			if (_orientation == SliderOrientation.Horizontal)
			{
				PaintHorizontal(buffer, bounds, clipRect, startX, startY, defaultFg, bgColor, preserveBg);
			}
			else
			{
				PaintVertical(buffer, bounds, clipRect, startX, startY, defaultFg, bgColor, preserveBg);
			}

			int contentEndY = _orientation == SliderOrientation.Horizontal
				? startY + 1
				: bounds.Bottom - Margin.Bottom;
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, defaultFg, bgColor, preserveBg);
		}

		#endregion

		#region Horizontal Rendering

		private void PaintHorizontal(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			int startX, int y, Color defaultFg, Color bgColor, bool preserveBg)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom)
				return;

			ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), defaultFg, bgColor, preserveBg);

			int currentX = startX;

			// Min label
			if (_showMinMaxLabels)
			{
				string minLabel = FormatValue(_minValue);
				PaintLabel(buffer, clipRect, bounds, currentX, y, minLabel, defaultFg, bgColor, preserveBg);
				currentX += minLabel.Length + ControlDefaults.SliderLabelSpacing;
			}

			// Calculate track width
			int trackStart = currentX;
			int availableWidth = bounds.Right - Margin.Right - currentX;
			if (_showMinMaxLabels)
				availableWidth -= FormatValue(_maxValue).Length + ControlDefaults.SliderLabelSpacing;
			if (_showValueLabel)
				availableWidth -= FormatValue(_maxValue).Length * 2 + 1 + ControlDefaults.SliderLabelSpacing;

			// Reserve 2 chars for end-caps
			int trackLength = Math.Max(ControlDefaults.SliderMinTrackLength, availableWidth - 2);

			// Resolve colors
			Color trackColor = TrackColor ?? Color.Grey35;
			Color filledColor = FilledTrackColor ?? Color.Cyan1;
			Color lowThumbColor = ResolveThumbColor(ActiveThumb.Low);
			Color highThumbColor = ResolveThumbColor(ActiveThumb.High);

			// Paint left end-cap
			if (trackStart >= clipRect.X && trackStart < clipRect.Right && trackStart < bounds.Right)
				buffer.SetNarrowCell(trackStart, y, ControlDefaults.SliderHorizontalLeftCap, trackColor, bgColor);
			int trackContentStart = trackStart + 1;

			// Calculate thumb positions
			int lowPos = SliderRenderingHelper.ValueToPosition(_lowValue, _minValue, _maxValue, trackLength);
			int highPos = SliderRenderingHelper.ValueToPosition(_highValue, _minValue, _maxValue, trackLength);

			// Paint unfilled portion (before low thumb)
			if (lowPos > 0)
			{
				SliderRenderingHelper.PaintHorizontalTrackSegment(
					buffer, clipRect, bounds, trackContentStart, y, lowPos,
					ControlDefaults.SliderUnfilledTrackChar, trackColor, bgColor);
			}

			// Paint low thumb
			int lowThumbX = trackContentStart + lowPos;
			if (lowThumbX >= clipRect.X && lowThumbX < clipRect.Right && lowThumbX < bounds.Right)
			{
				buffer.SetNarrowCell(lowThumbX, y, ControlDefaults.SliderThumbChar, lowThumbColor, bgColor);
			}

			// Paint filled portion (between thumbs)
			int filledStart = lowPos + 1;
			int filledLength = highPos - filledStart;
			if (filledLength > 0)
			{
				SliderRenderingHelper.PaintHorizontalTrackSegment(
					buffer, clipRect, bounds, trackContentStart + filledStart, y, filledLength,
					ControlDefaults.SliderFilledTrackChar, filledColor, bgColor);
			}

			// Paint high thumb
			int highThumbX = trackContentStart + highPos;
			if (highThumbX >= clipRect.X && highThumbX < clipRect.Right && highThumbX < bounds.Right)
			{
				buffer.SetNarrowCell(highThumbX, y, ControlDefaults.SliderThumbChar, highThumbColor, bgColor);
			}

			// Paint unfilled portion (after high thumb)
			int unfilledStart = highPos + 1;
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
				PaintLabel(buffer, clipRect, bounds, currentX, y, maxLabel, defaultFg, bgColor, preserveBg);
				currentX += maxLabel.Length;
			}

			// Value label (shows range)
			if (_showValueLabel)
			{
				currentX += ControlDefaults.SliderLabelSpacing;
				string valueLabel = $"{FormatValue(_lowValue)}-{FormatValue(_highValue)}";
				PaintLabel(buffer, clipRect, bounds, currentX, y, valueLabel, defaultFg, bgColor, preserveBg);
			}
		}

		#endregion

		#region Vertical Rendering

		private void PaintVertical(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			int startX, int startY, Color defaultFg, Color bgColor, bool preserveBg)
		{
			int currentY = startY;

			// Max label (top)
			if (_showMinMaxLabels)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), defaultFg, bgColor, preserveBg);
					string maxLabel = FormatValue(_maxValue);
					PaintLabel(buffer, clipRect, bounds, startX, currentY, maxLabel, defaultFg, bgColor, preserveBg);
				}
				currentY++;
			}

			int trackStart = currentY;
			int availableHeight = bounds.Bottom - Margin.Bottom - currentY;
			if (_showMinMaxLabels) availableHeight -= 1;
			int trackLength = Math.Max(ControlDefaults.SliderMinTrackLength, availableHeight - 2);

			Color trackColor = TrackColor ?? Color.Grey35;
			Color filledColor = FilledTrackColor ?? Color.Cyan1;
			Color lowThumbColor = ResolveThumbColor(ActiveThumb.Low);
			Color highThumbColor = ResolveThumbColor(ActiveThumb.High);

			// Paint top end-cap
			if (trackStart >= clipRect.Y && trackStart < clipRect.Bottom && trackStart < bounds.Bottom)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, trackStart, bounds.Width, 1), defaultFg, bgColor, preserveBg);
				if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
					buffer.SetNarrowCell(startX, trackStart, ControlDefaults.SliderVerticalTopCap, trackColor, bgColor);
			}
			int trackContentStart = trackStart + 1;

			// Inverted positions for vertical (top=max, bottom=min)
			int lowPos = SliderRenderingHelper.ValueToPosition(_lowValue, _minValue, _maxValue, trackLength);
			int highPos = SliderRenderingHelper.ValueToPosition(_highValue, _minValue, _maxValue, trackLength);
			int lowRow = trackLength - 1 - lowPos;
			int highRow = trackLength - 1 - highPos;

			for (int i = 0; i < trackLength; i++)
			{
				int paintY = trackContentStart + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, bounds.Width, 1), defaultFg, bgColor, preserveBg);

					if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
					{
						if (i == highRow)
						{
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderThumbChar, highThumbColor, bgColor);
						}
						else if (i == lowRow)
						{
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderThumbChar, lowThumbColor, bgColor);
						}
						else if (i > highRow && i < lowRow)
						{
							// Filled between thumbs
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderVerticalFilledTrackChar, filledColor, bgColor);
						}
						else
						{
							// Unfilled
							buffer.SetNarrowCell(startX, paintY, ControlDefaults.SliderVerticalTrackChar, trackColor, bgColor);
						}
					}
				}
			}

			// Paint bottom end-cap
			int bottomCapY = trackContentStart + trackLength;
			if (bottomCapY >= clipRect.Y && bottomCapY < clipRect.Bottom && bottomCapY < bounds.Bottom)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, bottomCapY, bounds.Width, 1), defaultFg, bgColor, preserveBg);
				if (startX >= clipRect.X && startX < clipRect.Right && startX < bounds.Right)
					buffer.SetNarrowCell(startX, bottomCapY, ControlDefaults.SliderVerticalBottomCap, trackColor, bgColor);
			}

			currentY = bottomCapY + 1;

			// Min label (bottom)
			if (_showMinMaxLabels)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), defaultFg, bgColor, preserveBg);
					string minLabel = FormatValue(_minValue);
					PaintLabel(buffer, clipRect, bounds, startX, currentY, minLabel, defaultFg, bgColor, preserveBg);
				}
			}
		}

		#endregion

		#region Shared Rendering Helpers

		private Color ResolveThumbColor(ActiveThumb thumb)
		{
			if (!_isEnabled) return Color.Grey;
			if (_isDragging && _activeThumb == thumb) return Color.Yellow;
			if (_hasFocus && _activeThumb == thumb) return FocusedThumbColor ?? Color.Yellow;
			return ThumbColor ?? Color.White;
		}

		private static void PaintLabel(CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int x, int y, string label, Color fg, Color bg, bool preserveBg)
		{
			for (int i = 0; i < label.Length; i++)
			{
				int paintX = x + i;
				if (paintX >= clipRect.X && paintX < clipRect.Right && paintX < bounds.Right)
				{
					Color cellBg = preserveBg ? buffer.GetCell(paintX, y).Background : bg;
					buffer.SetNarrowCell(paintX, y, label[i], fg, cellBg);
				}
			}
		}

		#endregion
	}
}
