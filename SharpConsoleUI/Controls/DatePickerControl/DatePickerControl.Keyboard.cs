// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Controls
{
	public partial class DatePickerControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			if (_isCalendarOpen)
				return ProcessCalendarKey(key);

			return ProcessInlineKey(key);
		}

		#region Inline Keyboard

		private bool ProcessInlineKey(ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.Tab:
					if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						if (_focusedSegment > 0)
						{
							_pendingDigit = -1;
							_focusedSegment--;
							Container?.Invalidate(true);
							return true;
						}
					}
					else
					{
						if (_focusedSegment < _segments.Length - 1)
						{
							_pendingDigit = -1;
							_focusedSegment++;
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.RightArrow:
					if (_focusedSegment < _segments.Length - 1)
					{
						_pendingDigit = -1;
						_focusedSegment++;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.LeftArrow:
					if (_focusedSegment > 0)
					{
						_pendingDigit = -1;
						_focusedSegment--;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					_pendingDigit = -1;
					IncrementSegment(1);
					return true;

				case ConsoleKey.DownArrow:
					_pendingDigit = -1;
					IncrementSegment(-1);
					return true;

				case ConsoleKey.Enter:
				case ConsoleKey.Spacebar:
					_pendingDigit = -1;
					OpenCalendar();
					return true;

				default:
					if (key.KeyChar >= '0' && key.KeyChar <= '9')
					{
						ApplyDigitToSegment(key.KeyChar - '0');
						return true;
					}
					return false;
			}
		}

		#endregion

		#region Calendar Keyboard

		private bool ProcessCalendarKey(ConsoleKeyInfo key)
		{
			bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);

			switch (key.Key)
			{
				case ConsoleKey.Escape:
					CloseCalendar();
					return true;

				case ConsoleKey.Enter:
				case ConsoleKey.Spacebar:
					SelectHighlightedDay();
					return true;

				case ConsoleKey.LeftArrow:
					MoveHighlightedDay(-1);
					return true;

				case ConsoleKey.RightArrow:
					MoveHighlightedDay(1);
					return true;

				case ConsoleKey.UpArrow:
					MoveHighlightedDay(-ControlDefaults.CalendarGridColumns);
					return true;

				case ConsoleKey.DownArrow:
					MoveHighlightedDay(ControlDefaults.CalendarGridColumns);
					return true;

				case ConsoleKey.PageUp:
					if (ctrl)
						NavigateCalendarMonth(-12);
					else
						NavigateCalendarMonth(-1);
					return true;

				case ConsoleKey.PageDown:
					if (ctrl)
						NavigateCalendarMonth(12);
					else
						NavigateCalendarMonth(1);
					return true;

				case ConsoleKey.Home:
					_highlightedDay = 1;
					Container?.Invalidate(true);
					return true;

				case ConsoleKey.End:
					_highlightedDay = _cachedDaysInMonth;
					Container?.Invalidate(true);
					return true;

				case ConsoleKey.T:
					JumpToToday();
					return true;

				default:
					return false;
			}
		}

		private void MoveHighlightedDay(int delta)
		{
			int newDay = _highlightedDay + delta;

			if (newDay < 1)
			{
				// Move to previous month
				NavigateCalendarMonth(-1);
				_highlightedDay = _cachedDaysInMonth;
			}
			else if (newDay > _cachedDaysInMonth)
			{
				// Move to next month
				NavigateCalendarMonth(1);
				_highlightedDay = 1;
			}
			else
			{
				_highlightedDay = newDay;
			}

			Container?.Invalidate(true);
		}

		private void NavigateCalendarMonth(int monthDelta)
		{
			try
			{
				_displayMonth = _displayMonth.AddMonths(monthDelta);
				UpdateCalendarCache();
				_highlightedDay = Math.Clamp(_highlightedDay, 1, _cachedDaysInMonth);
				Container?.Invalidate(true);
			}
			catch (ArgumentOutOfRangeException)
			{
				// Date out of range, ignore
			}
		}

		private void SelectHighlightedDay()
		{
			try
			{
				var newDate = new DateTime(_displayMonth.Year, _displayMonth.Month, _highlightedDay);
				if (IsDateInRange(newDate))
				{
					SelectedDate = newDate;
				}
			}
			catch (ArgumentOutOfRangeException)
			{
				// Invalid date, ignore
			}
			CloseCalendar();
		}

		private void JumpToToday()
		{
			var today = DateTime.Today;
			_displayMonth = new DateTime(today.Year, today.Month, 1);
			UpdateCalendarCache();
			_highlightedDay = today.Day;
			Container?.Invalidate(true);
		}

		#endregion
	}
}
