// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

public sealed class TimePickerBuilder : IControlBuilder<TimePickerControl>
{
	private string _prompt = Configuration.ControlDefaults.TimePickerDefaultPrompt;
	private TimeSpan? _selectedTime;
	private TimeSpan? _minTime;
	private TimeSpan? _maxTime;
	private CultureInfo? _culture;
	private bool? _use24HourFormat;
	private bool _showSeconds;
	private bool _isEnabled = true;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<TimeSpan?>? _selectedTimeChangedHandler;
	private WindowEventHandler<TimeSpan?>? _selectedTimeChangedWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	public TimePickerBuilder WithPrompt(string prompt)
	{
		_prompt = prompt;
		return this;
	}

	public TimePickerBuilder WithSelectedTime(TimeSpan time)
	{
		_selectedTime = time;
		return this;
	}

	public TimePickerBuilder WithMinTime(TimeSpan minTime)
	{
		_minTime = minTime;
		return this;
	}

	public TimePickerBuilder WithMaxTime(TimeSpan maxTime)
	{
		_maxTime = maxTime;
		return this;
	}

	public TimePickerBuilder WithCulture(CultureInfo culture)
	{
		_culture = culture;
		return this;
	}

	public TimePickerBuilder With24HourFormat()
	{
		_use24HourFormat = true;
		return this;
	}

	public TimePickerBuilder With12HourFormat()
	{
		_use24HourFormat = false;
		return this;
	}

	public TimePickerBuilder WithSeconds(bool show = true)
	{
		_showSeconds = show;
		return this;
	}

	public TimePickerBuilder Enabled(bool enabled = true)
	{
		_isEnabled = enabled;
		return this;
	}

	public TimePickerBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	public TimePickerBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	public TimePickerBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	public TimePickerBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	public TimePickerBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	public TimePickerBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	public TimePickerBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	public TimePickerBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	public TimePickerBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	public TimePickerBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	public TimePickerBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	public TimePickerBuilder OnSelectedTimeChanged(EventHandler<TimeSpan?> handler)
	{
		_selectedTimeChangedHandler = handler;
		return this;
	}

	public TimePickerBuilder OnSelectedTimeChanged(WindowEventHandler<TimeSpan?> handler)
	{
		_selectedTimeChangedWithWindowHandler = handler;
		return this;
	}

	public TimePickerBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	public TimePickerBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	public TimePickerBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	public TimePickerBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	public TimePickerControl Build()
	{
		var control = new TimePickerControl(_prompt)
		{
			ShowSeconds = _showSeconds,
			IsEnabled = _isEnabled,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		if (_culture != null)
			control.Culture = _culture;

		if (_use24HourFormat.HasValue)
			control.Use24HourFormat = _use24HourFormat;

		if (_minTime.HasValue)
			control.MinTime = _minTime;
		if (_maxTime.HasValue)
			control.MaxTime = _maxTime;
		if (_selectedTime.HasValue)
			control.SelectedTime = _selectedTime;

		if (_selectedTimeChangedHandler != null)
			control.SelectedTimeChanged += _selectedTimeChangedHandler;

		if (_selectedTimeChangedWithWindowHandler != null)
		{
			control.SelectedTimeChanged += (sender, time) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedTimeChangedWithWindowHandler(sender, time, window);
			};
		}

		if (_gotFocusHandler != null)
			control.GotFocus += _gotFocusHandler;

		if (_gotFocusWithWindowHandler != null)
		{
			control.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		if (_lostFocusHandler != null)
			control.LostFocus += _lostFocusHandler;

		if (_lostFocusWithWindowHandler != null)
		{
			control.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	public static implicit operator TimePickerControl(TimePickerBuilder builder) => builder.Build();
}
