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
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for toolbar controls
/// </summary>
public sealed class ToolbarBuilder
{
	private readonly List<IWindowControl> _items = new();
	private int _itemSpacing = 0;
	private Margin _margin = new(0, 0, 0, 0);
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private int? _height = 1;
	private int? _width;
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	/// <summary>
	/// Adds a button to the toolbar with the specified text and click handler
	/// </summary>
	/// <param name="text">The button text</param>
	/// <param name="onClick">The click handler with button reference</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder AddButton(string text, EventHandler<ButtonControl> onClick)
	{
		var button = new ButtonBuilder()
			.WithText(text)
			.OnClick(onClick)
			.Build();
		_items.Add(button);
		return this;
	}

	/// <summary>
	/// Adds a button to the toolbar with window-aware click handler
	/// </summary>
	/// <param name="text">The button text</param>
	/// <param name="onClick">The click handler with window access</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder AddButton(string text, WindowEventHandler<ButtonControl> onClick)
	{
		var button = new ButtonBuilder()
			.WithText(text)
			.OnClick(onClick)
			.Build();
		_items.Add(button);
		return this;
	}

	/// <summary>
	/// Adds a button to the toolbar using a button builder
	/// </summary>
	/// <param name="builder">The button builder</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder AddButton(ButtonBuilder builder)
	{
		_items.Add(builder.Build());
		return this;
	}

	/// <summary>
	/// Adds an existing button control to the toolbar
	/// </summary>
	/// <param name="button">The button control</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder AddButton(ButtonControl button)
	{
		_items.Add(button);
		return this;
	}

	/// <summary>
	/// Adds a separator to the toolbar
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder AddSeparator()
	{
		_items.Add(new SeparatorControl());
		return this;
	}

	/// <summary>
	/// Adds a separator with custom margin to the toolbar
	/// </summary>
	/// <param name="horizontalMargin">The horizontal margin on each side</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder AddSeparator(int horizontalMargin)
	{
		_items.Add(new SeparatorControl
		{
			Margin = new Margin(horizontalMargin, 0, horizontalMargin, 0)
		});
		return this;
	}

	/// <summary>
	/// Adds any control to the toolbar
	/// </summary>
	/// <param name="control">The control to add</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder Add(IWindowControl control)
	{
		_items.Add(control);
		return this;
	}

	/// <summary>
	/// Sets the spacing between toolbar items
	/// </summary>
	/// <param name="spacing">The spacing in characters</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithSpacing(int spacing)
	{
		_itemSpacing = Math.Max(0, spacing);
		return this;
	}

	/// <summary>
	/// Sets the toolbar margin
	/// </summary>
	/// <param name="left">Left margin</param>
	/// <param name="top">Top margin</param>
	/// <param name="right">Right margin</param>
	/// <param name="bottom">Bottom margin</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	/// <param name="margin">The margin value for all sides</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the toolbar margin
	/// </summary>
	/// <param name="margin">The margin</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the toolbar height
	/// </summary>
	/// <param name="height">The height in rows</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithHeight(int height)
	{
		_height = Math.Max(1, height);
		return this;
	}

	/// <summary>
	/// Sets the toolbar width
	/// </summary>
	/// <param name="width">The width in characters</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the toolbar is visible</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the background color (null for transparent/inherit)
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithBackgroundColor(Color? color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color (null for transparent/inherit)
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder WithForegroundColor(Color? color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the toolbar receives focus</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the toolbar loses focus</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ToolbarBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the toolbar control
	/// </summary>
	/// <returns>The configured toolbar control</returns>
	public ToolbarControl Build()
	{
		var toolbar = new ToolbarControl
		{
			ItemSpacing = _itemSpacing,
			Margin = _margin,
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Height = _height,
			Width = _width,
			Visible = _visible,
			Name = _name,
			Tag = _tag
		};

		// Only set colors if explicitly specified (null = inherit)
		if (_backgroundColor.HasValue)
			toolbar.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue)
			toolbar.ForegroundColor = _foregroundColor.Value;

		foreach (var item in _items)
		{
			toolbar.AddItem(item);
		}

		// Attach GotFocus handlers
		if (_gotFocusHandler != null)
		{
			toolbar.GotFocus += _gotFocusHandler;
		}

		if (_gotFocusWithWindowHandler != null)
		{
			toolbar.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach LostFocus handlers
		if (_lostFocusHandler != null)
		{
			toolbar.LostFocus += _lostFocusHandler;
		}

		if (_lostFocusWithWindowHandler != null)
		{
			toolbar.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		return toolbar;
	}

	/// <summary>
	/// Implicit conversion to ToolbarControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built toolbar control</returns>
	public static implicit operator ToolbarControl(ToolbarBuilder builder) => builder.Build();
}
