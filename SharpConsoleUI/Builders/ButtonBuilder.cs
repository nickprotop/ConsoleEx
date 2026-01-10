// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for button controls
/// </summary>
public sealed class ButtonBuilder
{
	private string _text = "Button";
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _enabled = true;
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<ButtonControl>? _clickHandler;
	private WindowEventHandler<ButtonControl>? _clickWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	/// <summary>
	/// Sets the button text
	/// </summary>
	/// <param name="text">The button text</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithText(string text)
	{
		_text = text ?? "Button";
		return this;
	}

	/// <summary>
	/// Sets the button alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the button
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder Centered()
	{
		_alignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Sets the button margin
	/// </summary>
	/// <param name="left">Left margin</param>
	/// <param name="top">Top margin</param>
	/// <param name="right">Right margin</param>
	/// <param name="bottom">Bottom margin</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	/// <param name="margin">The margin value for all sides</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the enabled state
	/// </summary>
	/// <param name="enabled">Whether the button is enabled</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder Enabled(bool enabled = true)
	{
		_enabled = enabled;
		return this;
	}

	/// <summary>
	/// Disables the button
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder Disabled()
	{
		_enabled = false;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the button is visible</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the button width
	/// </summary>
	/// <param name="width">The button width</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the click event handler
	/// </summary>
	/// <param name="handler">The click handler</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder OnClick(EventHandler<ButtonControl> handler)
	{
		_clickHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the click event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder OnClick(WindowEventHandler<ButtonControl> handler)
	{
		_clickWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the button receives focus</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the button loses focus</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ButtonBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the button control
	/// </summary>
	/// <returns>The configured button control</returns>
	public ButtonControl Build()
	{
		var button = new ButtonControl
		{
			Text = _text,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			IsEnabled = _enabled,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		// Attach standard handler
		if (_clickHandler != null)
		{
			button.Click += _clickHandler;
		}

		// Attach window-aware handler
		if (_clickWithWindowHandler != null)
		{
			button.Click += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_clickWithWindowHandler(sender, e, window);
			};
		}

		// Attach GotFocus handlers
		if (_gotFocusHandler != null)
		{
			button.GotFocus += _gotFocusHandler;
		}

		if (_gotFocusWithWindowHandler != null)
		{
			button.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach LostFocus handlers
		if (_lostFocusHandler != null)
		{
			button.LostFocus += _lostFocusHandler;
		}

		if (_lostFocusWithWindowHandler != null)
		{
			button.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		return button;
	}

	/// <summary>
	/// Implicit conversion to ButtonControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built button control</returns>
	public static implicit operator ButtonControl(ButtonBuilder builder) => builder.Build();
}
