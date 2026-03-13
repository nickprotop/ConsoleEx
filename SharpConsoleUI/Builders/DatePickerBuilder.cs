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

	public DatePickerBuilder WithPrompt(string prompt)
	{
		_prompt = prompt;
		return this;
	}

	public DatePickerBuilder WithSelectedDate(DateTime? date)
	{
		_selectedDate = date;
		return this;
	}

	public DatePickerBuilder WithMinDate(DateTime? date)
	{
		_minDate = date;
		return this;
	}

	public DatePickerBuilder WithMaxDate(DateTime? date)
	{
		_maxDate = date;
		return this;
	}

	public DatePickerBuilder WithCulture(CultureInfo culture)
	{
		_culture = culture;
		return this;
	}

	public DatePickerBuilder WithFormat(string format)
	{
		_dateFormat = format;
		return this;
	}

	public DatePickerBuilder WithFirstDayOfWeek(DayOfWeek dayOfWeek)
	{
		_firstDayOfWeek = dayOfWeek;
		return this;
	}

	public DatePickerBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	public DatePickerBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	public DatePickerBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	public DatePickerBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	public DatePickerBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	public DatePickerBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	public DatePickerBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	public DatePickerBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	public DatePickerBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	public DatePickerBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	public DatePickerBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	public DatePickerBuilder OnSelectedDateChanged(EventHandler<DateTime?> handler)
	{
		_selectedDateChangedHandler = handler;
		return this;
	}

	public DatePickerBuilder OnSelectedDateChanged(WindowEventHandler<DateTime?> handler)
	{
		_selectedDateChangedWithWindowHandler = handler;
		return this;
	}

	public DatePickerBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	public DatePickerBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	public DatePickerBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	public DatePickerBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

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

	public static implicit operator DatePickerControl(DatePickerBuilder builder) => builder.Build();
}
