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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A time picker control with segmented hour/minute/second/AM-PM fields.
	/// Supports 12h and 24h formats, keyboard digit entry, and mouse interaction.
	/// </summary>
	public partial class TimePickerControl : BaseControl, IInteractiveControl,
		IFocusableControl, IMouseAwareControl, ICursorShapeProvider
	{
		#region Fields

		private TimeSpan? _selectedTime;
		private TimeSpan? _minTime;
		private TimeSpan? _maxTime;
		private CultureInfo _culture = CultureInfo.CurrentCulture;
		private bool? _use24HourFormatOverride;
		private bool _showSeconds;
		private string _timeSeparator;
		private string _amDesignator;
		private string _pmDesignator;
		private int _focusedSegment;
		private int _pendingDigit = -1;
		private string _prompt;
		private bool _hasFocus;
		private bool _isEnabled = true;

		// Color overrides
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _segmentBackgroundColorValue;
		private Color? _segmentForegroundColorValue;
		private Color? _disabledForegroundColorValue;

		// Segment hit-testing state cached during PaintDOM
		private int[] _segmentXPositions = Array.Empty<int>();
		private int[] _segmentWidths = Array.Empty<int>();
		private LayoutRect _lastLayoutBounds;

		#endregion

		#region Constructors

		public TimePickerControl(string prompt = ControlDefaults.TimePickerDefaultPrompt)
		{
			_prompt = prompt;
			_timeSeparator = _culture.DateTimeFormat.TimeSeparator;
			_amDesignator = _culture.DateTimeFormat.AMDesignator;
			_pmDesignator = _culture.DateTimeFormat.PMDesignator;
		}

		#endregion

		#region Events

		public event EventHandler<TimeSpan?>? SelectedTimeChanged;
		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;
		public event EventHandler<MouseEventArgs>? MouseClick;
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
		public event EventHandler<MouseEventArgs>? MouseRightClick;
		public event EventHandler<MouseEventArgs>? MouseEnter;
		public event EventHandler<MouseEventArgs>? MouseLeave;
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		#region Properties

		public TimeSpan? SelectedTime
		{
			get => _selectedTime;
			set
			{
				var clamped = ClampTime(value);
				if (SetProperty(ref _selectedTime, clamped))
					SelectedTimeChanged?.Invoke(this, _selectedTime);
			}
		}

		public TimeSpan? MinTime
		{
			get => _minTime;
			set => SetProperty(ref _minTime, value);
		}

		public TimeSpan? MaxTime
		{
			get => _maxTime;
			set => SetProperty(ref _maxTime, value);
		}

		public CultureInfo Culture
		{
			get => _culture;
			set
			{
				if (SetProperty(ref _culture, value ?? CultureInfo.CurrentCulture))
				{
					_timeSeparator = _culture.DateTimeFormat.TimeSeparator;
					_amDesignator = _culture.DateTimeFormat.AMDesignator;
					_pmDesignator = _culture.DateTimeFormat.PMDesignator;
				}
			}
		}

		public bool ShowSeconds
		{
			get => _showSeconds;
			set => SetProperty(ref _showSeconds, value);
		}

		public bool? Use24HourFormat
		{
			get => _use24HourFormatOverride;
			set => SetProperty(ref _use24HourFormatOverride, value);
		}

		public string Prompt
		{
			get => _prompt;
			set => SetProperty(ref _prompt, value ?? string.Empty);
		}

		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

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

					if (value)
						GotFocus?.Invoke(this, EventArgs.Empty);
					else
					{
						_pendingDigit = -1;
						LostFocus?.Invoke(this, EventArgs.Empty);
					}
				}
			}
		}

		public bool CanReceiveFocus => IsEnabled;
		public bool WantsMouseEvents => _isEnabled;
		public bool CanFocusWithMouse => _isEnabled;
		public CursorShape? PreferredCursorShape => _hasFocus ? CursorShape.Hidden : null;

		// Color properties
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveTimePickerBackground(_backgroundColorValue, Container);
			set => SetProperty(ref _backgroundColorValue, (Color?)value);
		}

		public Color ForegroundColor
		{
			get => ColorResolver.ResolveTimePickerForeground(_foregroundColorValue, Container);
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		public Color FocusedBackgroundColor
		{
			get => ColorResolver.ResolveTimePickerFocusedBackground(_focusedBackgroundColorValue, Container);
			set => SetProperty(ref _focusedBackgroundColorValue, (Color?)value);
		}

		public Color FocusedForegroundColor
		{
			get => ColorResolver.ResolveTimePickerFocusedForeground(_focusedForegroundColorValue, Container);
			set => SetProperty(ref _focusedForegroundColorValue, (Color?)value);
		}

		public Color SegmentBackgroundColor
		{
			get => ColorResolver.ResolveTimePickerSegmentBackground(_segmentBackgroundColorValue, Container);
			set => SetProperty(ref _segmentBackgroundColorValue, (Color?)value);
		}

		public Color SegmentForegroundColor
		{
			get => ColorResolver.ResolveTimePickerSegmentForeground(_segmentForegroundColorValue, Container);
			set => SetProperty(ref _segmentForegroundColorValue, (Color?)value);
		}

		public Color DisabledForegroundColor
		{
			get => ColorResolver.ResolveTimePickerDisabledForeground(_disabledForegroundColorValue, Container);
			set => SetProperty(ref _disabledForegroundColorValue, (Color?)value);
		}

		public override int? ContentWidth => CalculateContentWidth() + Margin.Left + Margin.Right;

		#endregion

		#region Segment Logic

		private bool EffectiveUse24Hour =>
			_use24HourFormatOverride ?? !_culture.DateTimeFormat.ShortTimePattern.Contains('h');

		private int SegmentCount
		{
			get
			{
				int count = _showSeconds ? 3 : 2; // hour, min, [sec]
				if (!EffectiveUse24Hour) count++; // AM/PM
				return count;
			}
		}

		private int AmPmSegmentIndex => _showSeconds ? 3 : 2;

		private bool IsAmPmSegment(int segment) => !EffectiveUse24Hour && segment == AmPmSegmentIndex;

		private TimeSpan EffectiveTime => _selectedTime ?? TimeSpan.Zero;

		private int GetSegmentValue(int segment)
		{
			var time = EffectiveTime;
			if (segment == 0)
			{
				if (!EffectiveUse24Hour)
				{
					int h12 = time.Hours % 12;
					return h12 == 0 ? 12 : h12;
				}
				return time.Hours;
			}
			if (segment == 1) return time.Minutes;
			if (segment == 2 && _showSeconds) return time.Seconds;
			return 0;
		}

		private (int min, int max) GetSegmentRange(int segment)
		{
			if (segment == 0)
				return EffectiveUse24Hour ? (0, 23) : (1, 12);
			if (segment == 1) return (0, 59);
			if (segment == 2 && _showSeconds) return (0, 59);
			return (0, 0);
		}

		private void SetSegmentValue(int segment, int value)
		{
			var time = EffectiveTime;
			int hours = time.Hours;
			int minutes = time.Minutes;
			int seconds = time.Seconds;

			if (segment == 0)
			{
				if (!EffectiveUse24Hour)
				{
					bool isPm = hours >= 12;
					int h24 = value % 12;
					if (isPm) h24 += 12;
					hours = h24;
				}
				else
				{
					hours = value;
				}
			}
			else if (segment == 1)
			{
				minutes = value;
			}
			else if (segment == 2 && _showSeconds)
			{
				seconds = value;
			}

			SelectedTime = ClampTime(new TimeSpan(hours, minutes, seconds));
		}

		private void IncrementSegment(int segment, int delta)
		{
			if (IsAmPmSegment(segment))
			{
				ToggleAmPm();
				return;
			}

			var (min, max) = GetSegmentRange(segment);
			int current = GetSegmentValue(segment);
			int range = max - min + 1;
			int newValue = ((current - min + delta) % range + range) % range + min;
			SetSegmentValue(segment, newValue);
		}

		private void ToggleAmPm()
		{
			var time = EffectiveTime;
			int hours = time.Hours;
			hours = hours >= 12 ? hours - 12 : hours + 12;
			SelectedTime = ClampTime(new TimeSpan(hours, time.Minutes, time.Seconds));
		}

		private bool IsCurrentlyPm => EffectiveTime.Hours >= 12;

		private TimeSpan? ClampTime(TimeSpan? time)
		{
			if (!time.HasValue) return null;
			var t = time.Value;
			if (_minTime.HasValue && t < _minTime.Value) t = _minTime.Value;
			if (_maxTime.HasValue && t > _maxTime.Value) t = _maxTime.Value;
			return t;
		}

		private int CalculateContentWidth()
		{
			int promptLen = Parsing.MarkupParser.StripLength(_prompt);
			int sepLen = Parsing.MarkupParser.StripLength(_timeSeparator);

			// prompt + space + segments
			int width = promptLen + 1;

			// hour + sep + min
			width += ControlDefaults.TimeSegmentWidth + sepLen + ControlDefaults.TimeSegmentWidth;

			if (_showSeconds)
				width += sepLen + ControlDefaults.TimeSegmentWidth;

			if (!EffectiveUse24Hour)
			{
				// space + AM/PM designator width
				int ampmWidth = Math.Max(
					Parsing.MarkupParser.StripLength(_amDesignator),
					Parsing.MarkupParser.StripLength(_pmDesignator));
				width += 1 + ampmWidth;
			}

			return width;
		}

		#endregion

		#region Focus

		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			bool hadFocus = _hasFocus;
			HasFocus = focus;

			if (hadFocus != focus)
				this.NotifyParentWindowOfFocusChange(focus);
		}

		#endregion

		#region Disposal

		protected override void OnDisposing()
		{
			SelectedTimeChanged = null;
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
	}
}
