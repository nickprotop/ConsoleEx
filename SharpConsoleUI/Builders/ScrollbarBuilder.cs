// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for standalone <see cref="ScrollbarControl"/> instances.
/// </summary>
public sealed class ScrollbarBuilder : IControlBuilder<ScrollbarControl>
{
	private readonly List<EventHandler<int>> _valueChangedHandlers = new();
	private readonly List<EventHandler<int>> _userValueChangedHandlers = new();

	private ScrollbarOrientation _orientation = ScrollbarOrientation.Vertical;
	private int _maximum;
	private int _viewportLength = 1;
	private int _value;
	private int _smallChange = ControlDefaults.DefaultScrollWheelLines;
	private int? _largeChange;
	private bool _isActive;
	private bool _isEnabled = true;
	private Color? _scrollbarColor;
	private Color? _scrollbarThumbColor;

	private HorizontalAlignment _alignment = HorizontalAlignment.Stretch;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Fill;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private int? _height;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;

	/// <summary>
	/// Sets the bar's orientation.
	/// </summary>
	/// <param name="orientation">Vertical or horizontal.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithOrientation(ScrollbarOrientation orientation)
	{
		_orientation = orientation;
		return this;
	}

	/// <summary>
	/// Sets the bar to run horizontally.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder Horizontal()
	{
		_orientation = ScrollbarOrientation.Horizontal;
		return this;
	}

	/// <summary>
	/// Sets the bar to run vertically (the default).
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder Vertical()
	{
		_orientation = ScrollbarOrientation.Vertical;
		return this;
	}

	/// <summary>
	/// Sets the total length of the content being scrolled.
	/// </summary>
	/// <param name="maximum">The content length.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithMaximum(int maximum)
	{
		_maximum = maximum;
		return this;
	}

	/// <summary>
	/// Sets the visible length of the view the bar represents.
	/// </summary>
	/// <param name="viewportLength">The viewport length.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithViewportLength(int viewportLength)
	{
		_viewportLength = viewportLength;
		return this;
	}

	/// <summary>
	/// Sets the initial scroll offset.
	/// </summary>
	/// <param name="value">The initial offset.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithValue(int value)
	{
		_value = value;
		return this;
	}

	/// <summary>
	/// Sets the amount <see cref="ScrollbarControl.Value"/> moves for an arrow click or wheel notch.
	/// Default: <see cref="ControlDefaults.DefaultScrollWheelLines"/>.
	/// </summary>
	/// <param name="smallChange">The small-change amount.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithSmallChange(int smallChange)
	{
		_smallChange = smallChange;
		return this;
	}

	/// <summary>
	/// Sets the amount <see cref="ScrollbarControl.Value"/> moves for a track (page) click. Defaults to
	/// the viewport length when not set.
	/// </summary>
	/// <param name="largeChange">The large-change amount.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithLargeChange(int largeChange)
	{
		_largeChange = largeChange;
		return this;
	}

	/// <summary>
	/// Sets whether the bar paints itself as focused while a sibling view actually holds focus.
	/// </summary>
	/// <param name="isActive">True to paint as focused.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithIsActive(bool isActive = true)
	{
		_isActive = isActive;
		return this;
	}

	/// <summary>
	/// Sets whether the bar is enabled.
	/// </summary>
	/// <param name="enabled">True if enabled.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithEnabled(bool enabled = true)
	{
		_isEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets the track and thumb colors. When not called, the bar uses the built-in focus-aware
	/// default colors.
	/// </summary>
	/// <param name="trackColor">The track color.</param>
	/// <param name="thumbColor">The thumb color.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithScrollbarColors(Color trackColor, Color thumbColor)
	{
		_scrollbarColor = trackColor;
		_scrollbarThumbColor = thumbColor;
		return this;
	}

	/// <summary>
	/// Attaches a handler for the <see cref="ScrollbarControl.ValueChanged"/> event, raised on every
	/// value change including a code-set one.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder OnValueChanged(EventHandler<int> handler)
	{
		_valueChangedHandlers.Add(handler);
		return this;
	}

	/// <summary>
	/// Attaches a handler for the <see cref="ScrollbarControl.UserValueChanged"/> event, raised only
	/// for changes made through the bar itself.
	/// </summary>
	/// <param name="handler">The event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder OnUserValueChanged(EventHandler<int> handler)
	{
		_userValueChangedHandlers.Add(handler);
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	/// <param name="alignment">The alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	/// <param name="alignment">The vertical alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithVerticalAlignment(VerticalAlignment alignment)
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
	public ScrollbarBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	/// <param name="margin">The margin value.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	/// <param name="margin">The margin.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	/// <param name="visible">True if visible.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	/// <param name="width">The width.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the height.
	/// </summary>
	/// <param name="height">The height.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the control name for FindControl queries.
	/// </summary>
	/// <param name="name">The control name.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the control tag for custom data storage.
	/// </summary>
	/// <param name="tag">The tag object.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	/// <param name="position">The sticky position.</param>
	/// <returns>The builder for chaining.</returns>
	public ScrollbarBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Builds the scrollbar control.
	/// </summary>
	/// <returns>The configured control.</returns>
	public ScrollbarControl Build()
	{
		var control = new ScrollbarControl
		{
			Orientation = _orientation,
			Maximum = _maximum,
			ViewportLength = _viewportLength,
			SmallChange = _smallChange,
			IsActive = _isActive,
			IsEnabled = _isEnabled,
			ScrollbarColor = _scrollbarColor,
			ScrollbarThumbColor = _scrollbarThumbColor,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Height = _height,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		if (_largeChange.HasValue)
			control.LargeChange = _largeChange.Value;

		// Set after Maximum/ViewportLength so it is not re-clamped to 0 by their setters.
		control.Value = _value;

		foreach (var handler in _valueChangedHandlers)
		{
			control.ValueChanged += handler;
		}
		foreach (var handler in _userValueChangedHandlers)
		{
			control.UserValueChanged += handler;
		}

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to <see cref="ScrollbarControl"/>.
	/// </summary>
	/// <param name="builder">The builder.</param>
	public static implicit operator ScrollbarControl(ScrollbarBuilder builder)
	{
		return builder.Build();
	}
}
