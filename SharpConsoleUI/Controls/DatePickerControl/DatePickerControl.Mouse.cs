// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class DatePickerControl
	{
		#region IMouseAwareControl - Inline Header

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled)
				return false;

			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				MouseLeave?.Invoke(this, args);
				Container?.Invalidate(true);
				return true;
			}

			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return true;
			}

			// Validate within actual rendered content area, not full layout bounds
			int contentHeight = _lastLayoutBounds.Height > 0 ? _lastLayoutBounds.Height : 1;
			if (args.Position.Y < Margin.Top ||
				args.Position.Y >= contentHeight - Margin.Bottom)
			{
				return false;
			}

			int contentX = args.Position.X - Margin.Left;
			if (contentX < 0 || contentX >= _lastContentWidth)
			{
				// Click is outside the visual content — close calendar if open
				if (_isCalendarOpen && args.HasFlag(MouseFlags.Button1Released))
				{
					CloseCalendar();
					Container?.Invalidate(true);
					return true;
				}
				return false;
			}

			if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return true;
			}

			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle mouse down — capture focus and track press state
			if (args.HasAnyFlag(MouseFlags.Button1Pressed))
			{
				if (!(this.GetParentWindow()?.FocusManager.IsFocused(this) ?? false))
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				_isHeaderPressed = true;
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse up — perform action on complete click
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				bool wasPressed = _isHeaderPressed;
				_isHeaderPressed = false;

				if (!wasPressed)
					return true;

				int clickX = args.Position.X - Margin.Left;
				int promptLen = Parsing.MarkupParser.StripLength(_prompt) + 1;

				if (clickX >= promptLen)
				{
					bool hitSegment = false;
					int segX = promptLen;
					for (int i = 0; i < _segments.Length; i++)
					{
						if (i > 0) segX++; // separator
						int segEnd = segX + _segments[i].DisplayWidth;
						if (clickX >= segX && clickX < segEnd)
						{
							_focusedSegment = i;
							_pendingDigit = -1;
							hitSegment = true;
							break;
						}
						segX = segEnd;
					}

					// Only open calendar when clicking the ▼ indicator area (space + indicator + trailing space)
					if (!hitSegment)
					{
						int indicatorAreaStart = segX; // space before ▼
						int indicatorDisplayWidth = Parsing.MarkupParser.StripLength(Configuration.ControlDefaults.DatePickerDropdownIndicator);
						int indicatorAreaEnd = indicatorAreaStart + 1 + indicatorDisplayWidth + 1; // space + indicator + trailing space
						if (clickX >= indicatorAreaStart && clickX < indicatorAreaEnd)
						{
							if (_isCalendarOpen)
								CloseCalendar();
							else
								OpenCalendar();
						}
					}
				}

				MouseClick?.Invoke(this, args);
				Container?.Invalidate(true);
				return true;
			}

			return false;
		}

		#endregion

		#region Calendar Portal Mouse

		internal bool ProcessCalendarMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled || !_isCalendarOpen)
				return false;

			int contentX = args.Position.X;
			int contentY = args.Position.Y;

			// Handle mouse wheel
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				NavigateCalendarMonth(-1);
				args.Handled = true;
				return true;
			}
			if (args.HasFlag(MouseFlags.WheeledDown))
			{
				NavigateCalendarMonth(1);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				_mouseHoveredDay = -1;
				Container?.Invalidate(true);
				return true;
			}

			// Row 0: Header with ◄ ►
			if (contentY == 0)
			{
				if (args.HasFlag(MouseFlags.Button1Released))
				{
					// ◄ is at position ~2, ► is at position ~innerWidth-3
					if (contentX <= 3)
						NavigateCalendarMonth(-1);
					else if (contentX >= ControlDefaults.CalendarPortalWidth - 5)
						NavigateCalendarMonth(1);
				}
				return true;
			}

			// Row 1: Day-of-week headers (non-interactive)
			if (contentY == 1)
				return true;

			// Rows 2-7: Day grid
			int gridRow = contentY - 2;
			if (gridRow >= 0 && gridRow < ControlDefaults.CalendarGridRows)
			{
				int col = contentX / ControlDefaults.CalendarDayColumnWidth;
				if (col >= 0 && col < ControlDefaults.CalendarGridColumns)
				{
					int cellIndex = gridRow * ControlDefaults.CalendarGridColumns + col;
					int dayNumber = cellIndex - _cachedStartColumn + 1;

					if (dayNumber >= 1 && dayNumber <= _cachedDaysInMonth)
					{
						if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
						{
							if (_mouseHoveredDay != dayNumber)
							{
								_mouseHoveredDay = dayNumber;
								Container?.Invalidate(true);
							}
							return true;
						}

						if (args.HasFlag(MouseFlags.Button1Released))
						{
							_highlightedDay = dayNumber;
							SelectHighlightedDay();
							return true;
						}
					}
				}
				return true;
			}

			// Row 8: Today button
			int todayRow = 2 + ControlDefaults.CalendarGridRows;
			if (contentY == todayRow)
			{
				if (args.HasFlag(MouseFlags.Button1Released))
				{
					JumpToToday();
					SelectHighlightedDay();
				}
				return true;
			}

			return false;
		}

		#endregion
	}
}
