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
/// Fluent builder for creating and configuring DatePicker controls.
/// </summary>
public sealed class DatePickerBuilder : IControlBuilder<DatePickerControl>
{
	private string _prompt = "Date:";
	private DateTime? _selectedDate;
	private DateTime? _minDate;
	private DateTime? _maxDate;
	private CultureInfo? _culture;
	private string? _dateFormat;
	private DayOfWeek? _firstDayOfWeek;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<DateTime?>? _selectedDateChangedHandler;
	private WindowEventHandler<DateTime?>? _selectedDateChangedWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	/// <summary>
	/// Sets the prompt text displayed in the date picker header.
	/// </summary>
	/// <param name="prompt">The prompt text.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithPrompt(string prompt)
	{
		_prompt = prompt;
		return this;
	}

	/// <summary>
	/// Sets the initially selected date.
	/// </summary>
	/// <param name="date">The date to select.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithSelectedDate(DateTime? date)
	{
		_selectedDate = date;
		return this;
	}

	/// <summary>
	/// Sets the minimum selectable date.
	/// </summary>
	/// <param name="date">The minimum date.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithMinDate(DateTime? date)
	{
		_minDate = date;
		return this;
	}

	/// <summary>
	/// Sets the maximum selectable date.
	/// </summary>
	/// <param name="date">The maximum date.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithMaxDate(DateTime? date)
	{
		_maxDate = date;
		return this;
	}

	/// <summary>
	/// Sets the culture for date formatting and calendar layout.
	/// </summary>
	/// <param name="culture">The culture to use.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithCulture(CultureInfo culture)
	{
		_culture = culture;
		return this;
	}

	/// <summary>
	/// Sets a custom date format string.
	/// </summary>
	/// <param name="format">The date format string.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithFormat(string format)
	{
		_dateFormat = format;
		return this;
	}

	/// <summary>
	/// Sets the first day of the week for the calendar.
	/// </summary>
	/// <param name="dayOfWeek">The first day of the week.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithFirstDayOfWeek(DayOfWeek dayOfWeek)
	{
		_firstDayOfWeek = dayOfWeek;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	/// <param name="alignment">The horizontal alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	/// <param name="alignment">The vertical alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithVerticalAlignment(VerticalAlignment alignment)
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
	public DatePickerBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	/// <param name="margin">The margin value for all sides.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	/// <param name="visible">Whether the control is visible.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	/// <param name="width">The width in columns.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	/// <param name="name">The control name.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	/// <param name="tag">The tag object.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	/// <param name="position">The sticky position.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the selected date changed event handler.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder OnSelectedDateChanged(EventHandler<DateTime?> handler)
	{
		_selectedDateChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected date changed event handler with window access.
	/// </summary>
	/// <param name="handler">The event handler with window access.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder OnSelectedDateChanged(WindowEventHandler<DateTime?> handler)
	{
		_selectedDateChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access.
	/// </summary>
	/// <param name="handler">The event handler with window access.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access.
	/// </summary>
	/// <param name="handler">The event handler with window access.</param>
	/// <returns>The builder for chaining.</returns>
	public DatePickerBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the date picker control.
	/// </summary>
	/// <returns>The configured <see cref="DatePickerControl"/>.</returns>
	public DatePickerControl Build()
	{
		var control = new DatePickerControl(_prompt)
		{
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		if (_culture != null) control.Culture = _culture;
		if (_dateFormat != null) control.DateFormatOverride = _dateFormat;
		if (_firstDayOfWeek.HasValue) control.FirstDayOfWeekOverride = _firstDayOfWeek;
		if (_minDate.HasValue) control.MinDate = _minDate;
		if (_maxDate.HasValue) control.MaxDate = _maxDate;
		if (_selectedDate.HasValue) control.SelectedDate = _selectedDate;

		if (_selectedDateChangedHandler != null)
			control.SelectedDateChanged += _selectedDateChangedHandler;

		if (_selectedDateChangedWithWindowHandler != null)
		{
			control.SelectedDateChanged += (sender, date) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedDateChangedWithWindowHandler(sender, date, window);
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
	/// Implicit conversion to <see cref="DatePickerControl"/>.
	/// </summary>
	/// <param name="builder">The builder to convert.</param>
	public static implicit operator DatePickerControl(DatePickerBuilder builder) => builder.Build();
}
