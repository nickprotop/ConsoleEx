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
	public partial class TimePickerControl
	{
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			switch (key.Key)
			{
				case ConsoleKey.RightArrow:
				case ConsoleKey.Tab when !key.Modifiers.HasFlag(ConsoleModifiers.Shift):
					return NavigateSegment(1);

				case ConsoleKey.LeftArrow:
				case ConsoleKey.Tab when key.Modifiers.HasFlag(ConsoleModifiers.Shift):
					return NavigateSegment(-1);

				case ConsoleKey.UpArrow:
					_pendingDigit = -1;
					IncrementSegment(_focusedSegment, 1);
					return true;

				case ConsoleKey.DownArrow:
					_pendingDigit = -1;
					IncrementSegment(_focusedSegment, -1);
					return true;

				case ConsoleKey.PageUp:
					_pendingDigit = -1;
					if (!IsAmPmSegment(_focusedSegment))
						IncrementSegment(_focusedSegment, ControlDefaults.TimeLargeIncrementStep);
					else
						IncrementSegment(_focusedSegment, 1);
					return true;

				case ConsoleKey.PageDown:
					_pendingDigit = -1;
					if (!IsAmPmSegment(_focusedSegment))
						IncrementSegment(_focusedSegment, -ControlDefaults.TimeLargeIncrementStep);
					else
						IncrementSegment(_focusedSegment, -1);
					return true;

				case ConsoleKey.Home:
					_pendingDigit = -1;
					if (!IsAmPmSegment(_focusedSegment))
					{
						var (min, _) = GetSegmentRange(_focusedSegment);
						SetSegmentValue(_focusedSegment, min);
					}
					return true;

				case ConsoleKey.End:
					_pendingDigit = -1;
					if (!IsAmPmSegment(_focusedSegment))
					{
						var (_, max) = GetSegmentRange(_focusedSegment);
						SetSegmentValue(_focusedSegment, max);
					}
					return true;

				default:
					return HandleCharacterInput(key);
			}
		}

		private bool NavigateSegment(int direction)
		{
			_pendingDigit = -1;
			int segCount = SegmentCount;
			int next = _focusedSegment + direction;

			if (next < 0 || next >= segCount)
				return false;

			_focusedSegment = next;
			Container?.Invalidate(true);
			return true;
		}

		private bool HandleCharacterInput(ConsoleKeyInfo key)
		{
			char ch = key.KeyChar;

			// AM/PM toggle via A or P
			if (IsAmPmSegment(_focusedSegment))
			{
				if (ch == 'a' || ch == 'A')
				{
					if (IsCurrentlyPm) ToggleAmPm();
					return true;
				}
				if (ch == 'p' || ch == 'P')
				{
					if (!IsCurrentlyPm) ToggleAmPm();
					return true;
				}
				return false;
			}

			// Digit entry for numeric segments
			if (ch >= '0' && ch <= '9')
			{
				int digit = ch - '0';
				var (min, max) = GetSegmentRange(_focusedSegment);

				if (_pendingDigit >= 0)
				{
					// Second digit: combine with pending and commit
					int combined = _pendingDigit * 10 + digit;
					_pendingDigit = -1;

					if (combined > max) combined = max;
					if (combined < min) combined = min;
					SetSegmentValue(_focusedSegment, combined);

					// Auto-advance to next segment
					int segCount = SegmentCount;
					int nextSeg = _focusedSegment + 1;
					if (nextSeg < segCount)
					{
						_focusedSegment = nextSeg;
						Container?.Invalidate(true);
					}
				}
				else
				{
					// First digit: check if it could be a valid tens digit
					int maxTens = max / 10;
					if (digit > maxTens)
					{
						// Single digit can't be a tens place; commit immediately
						int value = digit;
						if (value > max) value = max;
						if (value < min) value = min;
						SetSegmentValue(_focusedSegment, value);

						// Auto-advance
						int nextSeg = _focusedSegment + 1;
						if (nextSeg < SegmentCount)
						{
							_focusedSegment = nextSeg;
							Container?.Invalidate(true);
						}
					}
					else
					{
						// Could be the tens digit; wait for second digit
						_pendingDigit = digit;
						// Show pending digit immediately
						int pendingValue = digit * 10;
						if (pendingValue < min) pendingValue = min;
						if (pendingValue > max) pendingValue = max;
						SetSegmentValue(_focusedSegment, pendingValue);
					}
				}

				return true;
			}

			return false;
		}
	}
}
