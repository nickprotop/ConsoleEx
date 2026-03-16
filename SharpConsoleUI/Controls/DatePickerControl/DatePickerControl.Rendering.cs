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
	public partial class DatePickerControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = ContentWidth ?? 0;
			if (HorizontalAlignment == HorizontalAlignment.Stretch)
				width = constraints.MaxWidth;

			int height = 1 + Margin.Top + Margin.Bottom;

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

			Color windowBackground = Container?.BackgroundColor ?? defaultBg;
			bool preserveBg = Container?.HasGradientBackground ?? false;

			Color backgroundColor;
			Color foregroundColor;

			if (!_isEnabled)
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.DatePickerDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			else if (_hasFocus)
			{
				backgroundColor = FocusedBackgroundColor;
				foregroundColor = FocusedForegroundColor;
			}
			else
			{
				backgroundColor = BackgroundColor;
				foregroundColor = ForegroundColor;
			}

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground, preserveBg);

			int paintY = startY;

			if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, Margin.Left, 1), foregroundColor, windowBackground, preserveBg);

				// Render inline header: "Prompt [MM]/[DD]/[YYYY] ▼"
				int writeX = startX;

				// Paint prompt
				var promptCells = Parsing.MarkupParser.Parse(_prompt + " ", foregroundColor, backgroundColor);
				buffer.WriteCellsClipped(writeX, paintY, promptCells, clipRect);
				writeX += promptCells.Count;

				// Paint segments with separators
				for (int i = 0; i < _segments.Length; i++)
				{
					if (i > 0)
					{
						// Paint separator
						buffer.SetNarrowCell(writeX, paintY, _separator, foregroundColor, backgroundColor);
						writeX++;
					}

					var seg = _segments[i];
					string segText;

					if (_hasFocus && i == _focusedSegment && _pendingDigit >= 0)
					{
						// Show pending digit with placeholder
						segText = seg.DisplayWidth == 2
							? _pendingDigit.ToString() + "_"
							: FormatSegmentValue(seg);
					}
					else
					{
						segText = FormatSegmentValue(seg);
					}

					// Determine segment colors
					Color segBg, segFg;
					if (_hasFocus && i == _focusedSegment)
					{
						segBg = SegmentBackgroundColor;
						segFg = SegmentForegroundColor;
					}
					else
					{
						segBg = backgroundColor;
						segFg = foregroundColor;
					}

					var segCells = Parsing.MarkupParser.Parse(segText, segFg, segBg);
					buffer.WriteCellsClipped(writeX, paintY, segCells, clipRect);
					writeX += segCells.Count;
				}

				// Paint dropdown indicator with padding
				// The ▼ may be a wide Unicode char (2 columns), so use Parse which handles continuation cells
				buffer.SetNarrowCell(writeX, paintY, ' ', foregroundColor, backgroundColor);
				writeX++;

				var indicatorCells = Parsing.MarkupParser.Parse(ControlDefaults.DatePickerDropdownIndicator, foregroundColor, backgroundColor);
				buffer.WriteCellsClipped(writeX, paintY, indicatorCells, clipRect);
				writeX += indicatorCells.Count;

				// Trailing space after indicator ensures the wide char background is clean
				buffer.SetNarrowCell(writeX, paintY, ' ', foregroundColor, backgroundColor);
				writeX++;

				// Cache actual content width for hit-testing
				_lastContentWidth = writeX - startX;

				// Fill remaining space on the line with window background
				int rightFillWidth = bounds.Right - Margin.Right - writeX;
				if (rightFillWidth > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(writeX, paintY, rightFillWidth, 1), foregroundColor, windowBackground, preserveBg);
				}

				// Fill right margin
				if (Margin.Right > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, paintY, Margin.Right, 1), foregroundColor, windowBackground, preserveBg);
			}
			paintY++;

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, paintY, foregroundColor, windowBackground, preserveBg);
		}

		#endregion

		#region Calendar Portal Painting

		/// <summary>
		/// Paints the calendar grid inside the portal. Called by CalendarPortalContent.
		/// </summary>
		internal void PaintCalendarInternal(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
		{
			Color bg = Container?.GetConsoleWindowSystem?.Theme?.DatePickerBackgroundColor ?? BackgroundColor;
			Color fg = Container?.GetConsoleWindowSystem?.Theme?.DatePickerForegroundColor ?? ForegroundColor;
			Color todayColor = Container?.GetConsoleWindowSystem?.Theme?.DatePickerCalendarTodayColor ?? Color.Cyan;
			Color selectedColor = Container?.GetConsoleWindowSystem?.Theme?.DatePickerCalendarSelectedColor ?? Color.Blue;
			Color headerColor = Container?.GetConsoleWindowSystem?.Theme?.DatePickerCalendarHeaderColor ?? Color.Yellow;
			Color disabledFg = Container?.GetConsoleWindowSystem?.Theme?.DatePickerDisabledForegroundColor ?? Color.Grey;

			int paintY = bounds.Y;
			int startX = bounds.X;
			int innerWidth = bounds.Width;

			// Row 1: Header "  ◄  March 2026     ►  "
			if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
			{
				string monthName = _displayMonth.ToString("MMMM yyyy", _culture);
				string header = $"  {ControlDefaults.CalendarPrevMonthArrow}  {monthName}";
				int headerDisplayLen = Parsing.MarkupParser.StripLength(header);
				int arrowRightPos = innerWidth - 3;
				int padding = Math.Max(0, arrowRightPos - headerDisplayLen);
				header += new string(' ', padding) + ControlDefaults.CalendarNextMonthArrow + "  ";

				// Ensure total width
				int totalLen = Parsing.MarkupParser.StripLength(header);
				if (totalLen < innerWidth)
					header += new string(' ', innerWidth - totalLen);

				var headerCells = Parsing.MarkupParser.Parse(header, headerColor, bg);
				buffer.WriteCellsClipped(startX, paintY, headerCells, clipRect);
			}
			paintY++;

			// Row 2: Day-of-week headers
			if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
			{
				string dowHeader = BuildDayOfWeekHeader();
				int dowLen = Parsing.MarkupParser.StripLength(dowHeader);
				if (dowLen < innerWidth)
					dowHeader += new string(' ', innerWidth - dowLen);

				var dowCells = Parsing.MarkupParser.Parse(dowHeader, fg, bg);
				buffer.WriteCellsClipped(startX, paintY, dowCells, clipRect);
			}
			paintY++;

			// Rows 3-8: Day grid (6 rows)
			var today = DateTime.Today;
			for (int row = 0; row < ControlDefaults.CalendarGridRows; row++)
			{
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					PaintCalendarRow(buffer, startX, paintY, innerWidth, row, today, fg, bg, todayColor, selectedColor, disabledFg, clipRect);
				}
				paintY++;
			}

			// Row 9: Today button "       [ Today ]        "
			if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
			{
				string todayLabel = "[ Today ]";
				int todayLabelLen = Parsing.MarkupParser.StripLength(todayLabel);
				int leftPad = Math.Max(0, (innerWidth - todayLabelLen) / 2);
				string todayRow = new string(' ', leftPad) + todayLabel;
				int todayRowLen = Parsing.MarkupParser.StripLength(todayRow);
				if (todayRowLen < innerWidth)
					todayRow += new string(' ', innerWidth - todayRowLen);

				var todayCells = Parsing.MarkupParser.Parse(todayRow, todayColor, bg);
				buffer.WriteCellsClipped(startX, paintY, todayCells, clipRect);
			}
			paintY++;

			// Fill any remaining rows in the portal interior
			while (paintY < bounds.Bottom)
			{
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					for (int x = startX; x < startX + innerWidth; x++)
						buffer.SetNarrowCell(x, paintY, ' ', fg, bg);
				}
				paintY++;
			}
		}

		private void PaintCalendarRow(CharacterBuffer buffer, int startX, int paintY, int innerWidth,
			int row, DateTime today, Color fg, Color bg, Color todayColor, Color selectedColor, Color disabledFg, LayoutRect clipRect)
		{
			int writeX = startX;

			for (int col = 0; col < ControlDefaults.CalendarGridColumns; col++)
			{
				int cellIndex = row * ControlDefaults.CalendarGridColumns + col;
				int dayNumber = cellIndex - _cachedStartColumn + 1;

				string dayText;
				Color dayFg = fg;
				Color dayBg = bg;

				if (dayNumber >= 1 && dayNumber <= _cachedDaysInMonth)
				{
					dayText = dayNumber.ToString().PadLeft(2);

					var dayDate = new DateTime(_displayMonth.Year, _displayMonth.Month, dayNumber);

					bool isToday = dayDate == today;
					bool isSelected = dayNumber == _highlightedDay;
					bool isHovered = dayNumber == _mouseHoveredDay;
					bool inRange = IsDateInRange(dayDate);

					if (!inRange)
					{
						dayFg = disabledFg;
					}
					else if (isSelected || isHovered)
					{
						dayFg = bg;
						dayBg = selectedColor;
					}
					else if (isToday)
					{
						dayFg = todayColor;
					}
				}
				else
				{
					dayText = "  ";
				}

				// Each day cell is CalendarDayColumnWidth chars wide
				string cellText = " " + dayText;

				var cells = Parsing.MarkupParser.Parse(cellText, dayFg, dayBg);
				buffer.WriteCellsClipped(writeX, paintY, cells, clipRect);
				writeX += ControlDefaults.CalendarDayColumnWidth;
			}

			// Fill remaining space
			int remaining = startX + innerWidth - writeX;
			for (int x = 0; x < remaining; x++)
				buffer.SetNarrowCell(writeX + x, paintY, ' ', fg, bg);
		}

		private string BuildDayOfWeekHeader()
		{
			var sb = new System.Text.StringBuilder();
			var firstDow = EffectiveFirstDayOfWeek;
			var abbrevNames = _culture.DateTimeFormat.AbbreviatedDayNames;

			for (int i = 0; i < ControlDefaults.CalendarGridColumns; i++)
			{
				int dow = ((int)firstDow + i) % ControlDefaults.CalendarGridColumns;
				string name = abbrevNames[dow];
				// Take first 2 chars for display
				string shortName = name.Length >= 2 ? name[..2] : name.PadRight(2);
				sb.Append(' ');
				sb.Append(shortName);
			}

			return sb.ToString();
		}

		#endregion
	}
}
