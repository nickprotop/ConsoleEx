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

		/// <summary>
		/// Initializes a new instance of the TimePickerControl class.
		/// </summary>
		/// <param name="prompt">The prompt text displayed in the header.</param>
		public TimePickerControl(string prompt = ControlDefaults.TimePickerDefaultPrompt)
		{
			_prompt = prompt;
			_timeSeparator = _culture.DateTimeFormat.TimeSeparator;
			_amDesignator = _culture.DateTimeFormat.AMDesignator;
			_pmDesignator = _culture.DateTimeFormat.PMDesignator;
		}

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the selected time changes.
		/// </summary>
		public event EventHandler<TimeSpan?>? SelectedTimeChanged;

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

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the currently selected time.
		/// </summary>
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

		/// <summary>
		/// Gets or sets the minimum selectable time.
		/// </summary>
		public TimeSpan? MinTime
		{
			get => _minTime;
			set => SetProperty(ref _minTime, value);
		}

		/// <summary>
		/// Gets or sets the maximum selectable time.
		/// </summary>
		public TimeSpan? MaxTime
		{
			get => _maxTime;
			set => SetProperty(ref _maxTime, value);
		}

		/// <summary>
		/// Gets or sets the culture used for time formatting.
		/// </summary>
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

		/// <summary>
		/// Gets or sets whether seconds are displayed.
		/// </summary>
		public bool ShowSeconds
		{
			get => _showSeconds;
			set => SetProperty(ref _showSeconds, value);
		}

		/// <summary>
		/// Gets or sets whether 24-hour format is used.
		/// </summary>
		public bool? Use24HourFormat
		{
			get => _use24HourFormatOverride;
			set => SetProperty(ref _use24HourFormatOverride, value);
		}

		/// <summary>
		/// Gets or sets the prompt text displayed in the header.
		/// </summary>
		public string Prompt
		{
			get => _prompt;
			set => SetProperty(ref _prompt, value ?? string.Empty);
		}

		/// <summary>
		/// Gets or sets whether the control is enabled.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <inheritdoc/>
				public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public bool WantsMouseEvents => _isEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => _isEnabled;

		/// <inheritdoc/>
		public CursorShape? PreferredCursorShape => (ComputeHasFocus()) ? CursorShape.Hidden : null;

		/// <summary>
		/// Gets or sets the background color.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color.
		/// </summary>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveTimePickerForeground(_foregroundColorValue, Container);
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color when focused.
		/// </summary>
		public Color? FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue;
			set => SetProperty(ref _focusedBackgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color when focused.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => ColorResolver.ResolveTimePickerFocusedForeground(_focusedForegroundColorValue, Container);
			set => SetProperty(ref _focusedForegroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color for the active time segment.
		/// </summary>
		public Color? SegmentBackgroundColor
		{
			get => _segmentBackgroundColorValue;
			set => SetProperty(ref _segmentBackgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color for the active time segment.
		/// </summary>
		public Color SegmentForegroundColor
		{
			get => ColorResolver.ResolveTimePickerSegmentForeground(_segmentForegroundColorValue, Container);
			set => SetProperty(ref _segmentForegroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color when disabled.
		/// </summary>
		public Color DisabledForegroundColor
		{
			get => ColorResolver.ResolveTimePickerDisabledForeground(_disabledForegroundColorValue, Container);
			set => SetProperty(ref _disabledForegroundColorValue, (Color?)value);
		}

		/// <inheritdoc/>
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

		private Window? _subscribedWindow;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				// Subscribe to FocusManager.FocusChanged to clear _pendingDigit on focus loss
				var newWindow = this.GetParentWindow();
				if (!ReferenceEquals(newWindow, _subscribedWindow))
				{
					if (_subscribedWindow != null)
						_subscribedWindow.FocusManager.FocusChanged -= OnFocusChanged;
					_subscribedWindow = newWindow;
					if (_subscribedWindow != null)
						_subscribedWindow.FocusManager.FocusChanged += OnFocusChanged;
				}
			}
		}

		private void OnFocusChanged(object? sender, Core.FocusChangedEventArgs e)
		{
			if (ReferenceEquals(e.Previous, this))
				_pendingDigit = -1;
		}

		#endregion

		#region Disposal

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			if (_subscribedWindow != null)
			{
				_subscribedWindow.FocusManager.FocusChanged -= OnFocusChanged;
				_subscribedWindow = null;
			}
			SelectedTimeChanged = null;
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
