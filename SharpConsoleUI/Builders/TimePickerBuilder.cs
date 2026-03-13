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

/// <summary>
/// Fluent builder for creating and configuring TimePicker controls.
/// </summary>
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

	/// <summary>
	/// Sets the prompt text displayed in the time picker header.
	/// </summary>
	/// <param name="prompt">The prompt text.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithPrompt(string prompt)
	{
		_prompt = prompt;
		return this;
	}

	/// <summary>
	/// Sets the initially selected time.
	/// </summary>
	/// <param name="time">The time to select.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithSelectedTime(TimeSpan time)
	{
		_selectedTime = time;
		return this;
	}

	/// <summary>
	/// Sets the minimum selectable time.
	/// </summary>
	/// <param name="minTime">The minimum time.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithMinTime(TimeSpan minTime)
	{
		_minTime = minTime;
		return this;
	}

	/// <summary>
	/// Sets the maximum selectable time.
	/// </summary>
	/// <param name="maxTime">The maximum time.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithMaxTime(TimeSpan maxTime)
	{
		_maxTime = maxTime;
		return this;
	}

	/// <summary>
	/// Sets the culture for time formatting.
	/// </summary>
	/// <param name="culture">The culture to use.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithCulture(CultureInfo culture)
	{
		_culture = culture;
		return this;
	}

	/// <summary>
	/// Enables 24-hour time format.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder With24HourFormat()
	{
		_use24HourFormat = true;
		return this;
	}

	/// <summary>
	/// Enables 12-hour time format with AM/PM.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder With12HourFormat()
	{
		_use24HourFormat = false;
		return this;
	}

	/// <summary>
	/// Sets whether seconds are shown.
	/// </summary>
	/// <param name="show">True to show seconds; false to hide them.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithSeconds(bool show = true)
	{
		_showSeconds = show;
		return this;
	}

	/// <summary>
	/// Sets whether the control is enabled.
	/// </summary>
	/// <param name="enabled">True to enable; false to disable.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder Enabled(bool enabled = true)
	{
		_isEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	/// <param name="alignment">The horizontal alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	/// <param name="alignment">The vertical alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	/// <param name="left">Left margin.</param>
	/// <param name="top">Top margin.</param>
	/// <param name="right">Right margin.</param>
	/// <param name="bottom">Bottom margin.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	/// <param name="margin">The margin value for all sides.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	/// <param name="visible">True to make visible; false to hide.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	/// <param name="width">The width in columns.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	/// <param name="name">The control name.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	/// <param name="tag">The tag object.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	/// <param name="position">The sticky position.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the selected time changed event handler.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder OnSelectedTimeChanged(EventHandler<TimeSpan?> handler)
	{
		_selectedTimeChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected time changed event handler with window access.
	/// </summary>
	/// <param name="handler">The event handler with window parameter.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder OnSelectedTimeChanged(WindowEventHandler<TimeSpan?> handler)
	{
		_selectedTimeChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access.
	/// </summary>
	/// <param name="handler">The event handler with window parameter.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access.
	/// </summary>
	/// <param name="handler">The event handler with window parameter.</param>
	/// <returns>The builder for chaining.</returns>
	public TimePickerBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the time picker control.
	/// </summary>
	/// <returns>The configured <see cref="TimePickerControl"/>.</returns>
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

	/// <summary>
	/// Implicit conversion to TimePickerControl.
	/// </summary>
	/// <param name="builder">The builder to convert.</param>
	public static implicit operator TimePickerControl(TimePickerBuilder builder) => builder.Build();
}
