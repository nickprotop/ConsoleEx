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
	public partial class TimePickerControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int contentW = CalculateContentWidth();
			int controlWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch
				? constraints.MaxWidth - Margin.Left - Margin.Right
				: contentW);
			controlWidth = Math.Max(contentW, controlWidth);

			int width = controlWidth + Margin.Left + Margin.Right;
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);
			_lastLayoutBounds = bounds;

			Color windowBackground = Container?.BackgroundColor ?? defaultBg;
			var effectiveBg = Container?.HasGradientBackground == true ? Color.Transparent : windowBackground;

			// Resolve colors based on state
			Color bgColor, fgColor;
			if (!_isEnabled)
			{
				bgColor = BackgroundColor;
				fgColor = DisabledForegroundColor;
			}
			else if (_hasFocus)
			{
				bgColor = FocusedBackgroundColor;
				fgColor = FocusedForegroundColor;
			}
			else
			{
				bgColor = BackgroundColor;
				fgColor = ForegroundColor;
			}

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int contentW = CalculateContentWidth();
			int controlWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : contentW);
			controlWidth = Math.Min(Math.Max(contentW, controlWidth), targetWidth);

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Alignment offset
			int alignOffset = 0;
			if (controlWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - controlWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - controlWidth;
						break;
				}
			}

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), fgColor, effectiveBg);

				// Fill left alignment padding
				if (alignOffset > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, startY, alignOffset, 1), fgColor, effectiveBg);

				// Render content
				int writeX = startX + alignOffset;
				int segCount = SegmentCount;
				_segmentXPositions = new int[segCount];
				_segmentWidths = new int[segCount];

				// Render prompt
				var promptCells = Parsing.MarkupParser.Parse(_prompt, fgColor, bgColor);
				buffer.WriteCellsClipped(writeX, startY, promptCells, clipRect);
				writeX += promptCells.Count;

				// Space after prompt
				RenderNarrowChar(buffer, ref writeX, startY, ' ', fgColor, bgColor, clipRect);

				// Render time segments
				int sepLen = Parsing.MarkupParser.StripLength(_timeSeparator);
				int numericSegments = _showSeconds ? 3 : 2;

				for (int seg = 0; seg < numericSegments; seg++)
				{
					if (seg > 0)
					{
						// Render separator
						var sepCells = Parsing.MarkupParser.Parse(_timeSeparator, fgColor, bgColor);
						buffer.WriteCellsClipped(writeX, startY, sepCells, clipRect);
						writeX += sepCells.Count;
					}

					// Determine segment colors
					bool isActiveSegment = _hasFocus && _isEnabled && _focusedSegment == seg;
					Color segFg = isActiveSegment ? SegmentForegroundColor : fgColor;
					Color segBg = isActiveSegment ? SegmentBackgroundColor : bgColor;

					_segmentXPositions[seg] = writeX;
					_segmentWidths[seg] = ControlDefaults.TimeSegmentWidth;

					string segText = GetSegmentValue(seg).ToString("D2");
					var segCells = Parsing.MarkupParser.Parse(segText, segFg, segBg);
					buffer.WriteCellsClipped(writeX, startY, segCells, clipRect);
					writeX += segCells.Count;
				}

				// Render AM/PM segment if in 12h mode
				if (!EffectiveUse24Hour)
				{
					int ampmIdx = AmPmSegmentIndex;
					RenderNarrowChar(buffer, ref writeX, startY, ' ', fgColor, bgColor, clipRect);

					bool isActiveAmPm = _hasFocus && _isEnabled && _focusedSegment == ampmIdx;
					Color ampmFg = isActiveAmPm ? SegmentForegroundColor : fgColor;
					Color ampmBg = isActiveAmPm ? SegmentBackgroundColor : bgColor;

					_segmentXPositions[ampmIdx] = writeX;
					string designator = IsCurrentlyPm ? _pmDesignator : _amDesignator;
					int desigWidth = Parsing.MarkupParser.StripLength(designator);
					_segmentWidths[ampmIdx] = desigWidth;

					var ampmCells = Parsing.MarkupParser.Parse(designator, ampmFg, ampmBg);
					buffer.WriteCellsClipped(writeX, startY, ampmCells, clipRect);
					writeX += ampmCells.Count;
				}

				// Fill remaining space to controlWidth
				int renderedWidth = writeX - (startX + alignOffset);
				int fillWidth = controlWidth - renderedWidth;
				if (fillWidth > 0)
				{
					buffer.FillRect(new LayoutRect(writeX, startY, fillWidth, 1), ' ', fgColor, bgColor);
				}

				// Fill right alignment padding
				int rightPadStart = startX + alignOffset + controlWidth;
				int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
				if (rightPadWidth > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightPadStart, startY, rightPadWidth, 1), fgColor, effectiveBg);

				// Fill right margin
				if (Margin.Right > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), fgColor, effectiveBg);
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, effectiveBg);
		}

		private static void RenderNarrowChar(CharacterBuffer buffer, ref int x, int y,
			char ch, Color fg, Color bg, LayoutRect clipRect)
		{
			if (x >= clipRect.X && x < clipRect.Right && y >= clipRect.Y && y < clipRect.Bottom)
			{
				buffer.SetNarrowCell(x, y, ch, fg, bg);
			}
			x++;
		}

		#endregion
	}
}
