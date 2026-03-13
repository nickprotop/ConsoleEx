// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A locale-aware date picker control with inline segment editing and a calendar portal overlay.
	/// Supports keyboard navigation, digit entry, and mouse interaction.
	/// </summary>
	public partial class DatePickerControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, ICursorShapeProvider
	{
		#region Nested Types

		private enum SegmentType { Month, Day, Year }

		private readonly record struct SegmentInfo(SegmentType Type, int DisplayWidth);

		#endregion

		#region Fields

		private DateTime? _selectedDate;
		private DateTime? _minDate;
		private DateTime? _maxDate;
		private CultureInfo _culture = CultureInfo.CurrentCulture;
		private string? _dateFormatOverride;
		private DayOfWeek? _firstDayOfWeekOverride;
		private int _focusedSegment;
		private int _pendingDigit = -1;
		private string _prompt = ControlDefaults.DatePickerDefaultPrompt;

		private SegmentInfo[] _segments = Array.Empty<SegmentInfo>();
		private char _separator = '-';

		private bool _isCalendarOpen;
		private DateTime _displayMonth;
		private int _highlightedDay;
		private int _mouseHoveredDay = -1;
		private bool _isHeaderPressed;
		private LayoutNode? _calendarPortal;
		private CalendarPortalContent? _portalContent;

		private int _cachedDaysInMonth;
		private int _cachedStartColumn;

		private bool _hasFocus;
		private bool _isEnabled = true;
		private LayoutRect _lastLayoutBounds;

		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _segmentBackgroundColorValue;
		private Color? _segmentForegroundColorValue;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the DatePickerControl class.
		/// </summary>
		/// <param name="prompt">The prompt text displayed in the header.</param>
		public DatePickerControl(string prompt = "Date:")
		{
			_prompt = prompt;
			RebuildSegments();
		}

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the selected date changes.
		/// </summary>
		public event EventHandler<DateTime?>? SelectedDateChanged;
		/// <inheritdoc/>
		public event EventHandler? GotFocus;
		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		#pragma warning disable CS0067, CS0414
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067, CS0414

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the currently selected date.
		/// </summary>
		public DateTime? SelectedDate
		{
			get => _selectedDate;
			set
			{
				if (_selectedDate == value) return;
				_selectedDate = ClampDate(value);
				OnPropertyChanged();
				SelectedDateChanged?.Invoke(this, _selectedDate);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the minimum selectable date.
		/// </summary>
		public DateTime? MinDate
		{
			get => _minDate;
			set => SetProperty(ref _minDate, value);
		}

		/// <summary>
		/// Gets or sets the maximum selectable date.
		/// </summary>
		public DateTime? MaxDate
		{
			get => _maxDate;
			set => SetProperty(ref _maxDate, value);
		}

		/// <summary>
		/// Gets or sets the culture used for date formatting and calendar layout.
		/// </summary>
		public CultureInfo Culture
		{
			get => _culture;
			set
			{
				if (ReferenceEquals(_culture, value)) return;
				_culture = value ?? CultureInfo.CurrentCulture;
				RebuildSegments();
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets a custom date format string override.
		/// </summary>
		public string? DateFormatOverride
		{
			get => _dateFormatOverride;
			set
			{
				if (_dateFormatOverride == value) return;
				_dateFormatOverride = value;
				RebuildSegments();
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the first day of the week for the calendar display.
		/// </summary>
		public DayOfWeek? FirstDayOfWeekOverride
		{
			get => _firstDayOfWeekOverride;
			set => SetProperty(ref _firstDayOfWeekOverride, value);
		}

		/// <summary>
		/// Gets or sets the prompt text displayed in the header.
		/// </summary>
		public string Prompt
		{
			get => _prompt;
			set => SetProperty(ref _prompt, value);
		}

		/// <summary>
		/// Gets or sets the background color.
		/// </summary>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveDatePickerBackground(_backgroundColorValue, Container);
			set => SetProperty(ref _backgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color.
		/// </summary>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveDatePickerForeground(_foregroundColorValue, Container);
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color when focused.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => ColorResolver.ResolveDatePickerFocusedBackground(_focusedBackgroundColorValue, Container);
			set => SetProperty(ref _focusedBackgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color when focused.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => ColorResolver.ResolveDatePickerFocusedForeground(_focusedForegroundColorValue, Container);
			set => SetProperty(ref _focusedForegroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color for the active date segment.
		/// </summary>
		public Color SegmentBackgroundColor
		{
			get => ColorResolver.ResolveDatePickerSegmentBackground(_segmentBackgroundColorValue, Container);
			set => SetProperty(ref _segmentBackgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color for the active date segment.
		/// </summary>
		public Color SegmentForegroundColor
		{
			get => ColorResolver.ResolveDatePickerSegmentForeground(_segmentForegroundColorValue, Container);
			set => SetProperty(ref _segmentForegroundColorValue, (Color?)value);
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_hasFocus != value)
				{
					_hasFocus = value;
					OnPropertyChanged();
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Gets or sets whether the control is enabled.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <inheritdoc/>
		public bool WantsMouseEvents => _isEnabled;
		/// <inheritdoc/>
		public bool CanFocusWithMouse => _isEnabled;

		/// <inheritdoc/>
		public CursorShape? PreferredCursorShape => CursorShape.Hidden;

		/// <summary>
		/// Gets whether the calendar popup is currently open.
		/// </summary>
		public bool IsCalendarOpen => _isCalendarOpen;

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				int promptLen = Parsing.MarkupParser.StripLength(_prompt);
				int segmentsTotalWidth = 0;
				for (int i = 0; i < _segments.Length; i++)
				{
					if (i > 0) segmentsTotalWidth++; // separator
					segmentsTotalWidth += _segments[i].DisplayWidth;
				}
				// prompt + space + segments + space + indicator + trailing space
				int indicatorLen = Parsing.MarkupParser.StripLength(ControlDefaults.DatePickerDropdownIndicator);
				return promptLen + 1 + segmentsTotalWidth + 1 + indicatorLen + 1 + Margin.Left + Margin.Right;
			}
		}

		#endregion

		#region Format Parsing

		private string EffectiveDateFormat => _dateFormatOverride ?? _culture.DateTimeFormat.ShortDatePattern;
		private DayOfWeek EffectiveFirstDayOfWeek => _firstDayOfWeekOverride ?? _culture.DateTimeFormat.FirstDayOfWeek;

		private void RebuildSegments()
		{
			var format = EffectiveDateFormat;
			var segments = new List<SegmentInfo>();
			char separator = '/';

			int i = 0;
			while (i < format.Length)
			{
				char c = format[i];
				if (c == 'M' || c == 'm')
				{
					int count = CountRepeated(format, i, c);
					segments.Add(new SegmentInfo(SegmentType.Month, 2));
					i += count;
				}
				else if (c == 'd' || c == 'D')
				{
					int count = CountRepeated(format, i, c);
					segments.Add(new SegmentInfo(SegmentType.Day, 2));
					i += count;
				}
				else if (c == 'y' || c == 'Y')
				{
					int count = CountRepeated(format, i, c);
					int width = count >= 4 ? 4 : 2;
					segments.Add(new SegmentInfo(SegmentType.Year, width));
					i += count;
				}
				else
				{
					// Separator character
					if (c != '\'' && c != '"')
						separator = c;
					i++;
				}
			}

			if (segments.Count == 0)
			{
				// Fallback to ISO format
				segments.Add(new SegmentInfo(SegmentType.Year, 4));
				segments.Add(new SegmentInfo(SegmentType.Month, 2));
				segments.Add(new SegmentInfo(SegmentType.Day, 2));
				separator = '-';
			}

			_segments = segments.ToArray();
			_separator = separator;
			_focusedSegment = Math.Clamp(_focusedSegment, 0, Math.Max(0, _segments.Length - 1));
		}

		private static int CountRepeated(string s, int start, char c)
		{
			int count = 0;
			char upper = char.ToUpperInvariant(c);
			while (start + count < s.Length && char.ToUpperInvariant(s[start + count]) == upper)
				count++;
			return Math.Max(1, count);
		}

		#endregion

		#region Segment Value Access

		private int GetSegmentValue(SegmentType type)
		{
			var date = _selectedDate ?? DateTime.Today;
			return type switch
			{
				SegmentType.Month => date.Month,
				SegmentType.Day => date.Day,
				SegmentType.Year => date.Year,
				_ => 0
			};
		}

		private string FormatSegmentValue(SegmentInfo seg)
		{
			int value = GetSegmentValue(seg.Type);
			return seg.DisplayWidth == 4
				? value.ToString("D4")
				: value.ToString("D2");
		}

		private void IncrementSegment(int direction)
		{
			if (_segments.Length == 0) return;
			var seg = _segments[_focusedSegment];
			var date = _selectedDate ?? DateTime.Today;

			try
			{
				var newDate = seg.Type switch
				{
					SegmentType.Month => date.AddMonths(direction),
					SegmentType.Day => date.AddDays(direction),
					SegmentType.Year => date.AddYears(direction),
					_ => date
				};
				SelectedDate = newDate;
			}
			catch (ArgumentOutOfRangeException)
			{
				// Date out of range, ignore
			}
		}

		private void ApplyDigitToSegment(int digit)
		{
			if (_segments.Length == 0) return;
			var seg = _segments[_focusedSegment];
			var date = _selectedDate ?? DateTime.Today;

			int currentValue = GetSegmentValue(seg.Type);

			int newValue;
			if (_pendingDigit >= 0)
			{
				// Second digit: combine with pending
				newValue = _pendingDigit * 10 + digit;
				_pendingDigit = -1;
			}
			else
			{
				if (seg.DisplayWidth == 4)
				{
					// Year: accumulate digits
					newValue = currentValue * 10 + digit;
					if (newValue > 9999) newValue = digit;
				}
				else
				{
					// First digit for month/day
					_pendingDigit = digit;
					Container?.Invalidate(true);
					return;
				}
			}

			try
			{
				var newDate = seg.Type switch
				{
					SegmentType.Month => new DateTime(date.Year, Math.Clamp(newValue, 1, 12), Math.Min(date.Day, DateTime.DaysInMonth(date.Year, Math.Clamp(newValue, 1, 12)))),
					SegmentType.Day => new DateTime(date.Year, date.Month, Math.Clamp(newValue, 1, DateTime.DaysInMonth(date.Year, date.Month))),
					SegmentType.Year => new DateTime(Math.Clamp(newValue, 1, 9999), date.Month, Math.Min(date.Day, DateTime.DaysInMonth(Math.Clamp(newValue, 1, 9999), date.Month))),
					_ => date
				};
				SelectedDate = newDate;

				// Auto-advance to next segment after completing a 2-digit entry
				if (seg.DisplayWidth == 2 && _focusedSegment < _segments.Length - 1)
				{
					_focusedSegment++;
					Container?.Invalidate(true);
				}
			}
			catch (ArgumentOutOfRangeException)
			{
				// Invalid date, ignore
			}
		}

		#endregion

		#region Date Clamping

		private DateTime? ClampDate(DateTime? date)
		{
			if (!date.HasValue) return null;
			var d = date.Value;
			if (_minDate.HasValue && d < _minDate.Value) d = _minDate.Value;
			if (_maxDate.HasValue && d > _maxDate.Value) d = _maxDate.Value;
			return d;
		}

		private bool IsDateInRange(DateTime date)
		{
			if (_minDate.HasValue && date < _minDate.Value) return false;
			if (_maxDate.HasValue && date > _maxDate.Value) return false;
			return true;
		}

		#endregion

		#region IFocusableControl

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			bool hadFocus = _hasFocus;
			HasFocus = focus;

			if (focus && !hadFocus)
			{
				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
				if (_isCalendarOpen) CloseCalendar();
				_pendingDigit = -1;
				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			Container?.Invalidate(true);

			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion

		#region Overrides

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = ContentWidth ?? 0;
			int height = 1 + Margin.Top + Margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			if (_isCalendarOpen) CloseCalendar();
			SelectedDateChanged = null;
			GotFocus = null;
			LostFocus = null;
			MouseClick = null;
			MouseDoubleClick = null;
			MouseRightClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
		}

		#endregion

		#region Calendar Cache

		private void UpdateCalendarCache()
		{
			_cachedDaysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
			var firstOfMonth = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
			int firstDow = (int)firstOfMonth.DayOfWeek;
			int firstDowOffset = (int)EffectiveFirstDayOfWeek;
			_cachedStartColumn = (firstDow - firstDowOffset + ControlDefaults.CalendarGridColumns) % ControlDefaults.CalendarGridColumns;
		}

		#endregion
	}
}
