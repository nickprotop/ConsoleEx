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
/// Fluent builder for multiline edit controls
/// </summary>
public sealed class MultilineEditControlBuilder
{
	private int _viewportHeight = 10;
	private string? _content;
	private int? _width;
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private bool _isEnabled = true;
	private bool _isEditing = false;
	private bool _readOnly = false;
	private WrapMode _wrapMode = WrapMode.Wrap;
	private ScrollbarVisibility _horizontalScrollbarVisibility = ScrollbarVisibility.Auto;
	private ScrollbarVisibility _verticalScrollbarVisibility = ScrollbarVisibility.Auto;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private Color? _focusedBackgroundColor;
	private Color? _focusedForegroundColor;
	private Color? _borderColor;
	private Color? _selectionBackgroundColor;
	private Color? _selectionForegroundColor;
	private Color? _scrollbarColor;
	private Color? _scrollbarThumbColor;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private string? _name;
	private object? _tag;

	// Event handlers
	private EventHandler<string>? _contentChangedHandler;
	private EventHandler? _gotFocusHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<string>? _contentChangedWithWindowHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	/// <summary>
	/// Sets the initial content
	/// </summary>
	/// <param name="content">The text content</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithContent(string content)
	{
		_content = content;
		return this;
	}

	/// <summary>
	/// Sets the initial content from multiple lines
	/// </summary>
	/// <param name="lines">The content lines</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithContentLines(params string[] lines)
	{
		_content = string.Join(Environment.NewLine, lines);
		return this;
	}

	/// <summary>
	/// Sets the initial content from multiple lines
	/// </summary>
	/// <param name="lines">The content lines</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithContentLines(IEnumerable<string> lines)
	{
		_content = string.Join(Environment.NewLine, lines);
		return this;
	}

	/// <summary>
	/// Sets the viewport height (number of visible lines)
	/// </summary>
	/// <param name="height">The viewport height</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithViewportHeight(int height)
	{
		_viewportHeight = Math.Max(1, height);
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The control width</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the wrap mode
	/// </summary>
	/// <param name="mode">The wrap mode</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithWrapMode(WrapMode mode)
	{
		_wrapMode = mode;
		return this;
	}

	/// <summary>
	/// Disables text wrapping
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder NoWrap()
	{
		_wrapMode = WrapMode.NoWrap;
		return this;
	}

	/// <summary>
	/// Enables word wrapping
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WrapWords()
	{
		_wrapMode = WrapMode.WrapWords;
		return this;
	}

	/// <summary>
	/// Enables character wrapping
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WrapCharacters()
	{
		_wrapMode = WrapMode.Wrap;
		return this;
	}

	/// <summary>
	/// Sets the vertical scrollbar visibility
	/// </summary>
	/// <param name="visibility">The scrollbar visibility</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithVerticalScrollbar(ScrollbarVisibility visibility)
	{
		_verticalScrollbarVisibility = visibility;
		return this;
	}

	/// <summary>
	/// Sets the horizontal scrollbar visibility
	/// </summary>
	/// <param name="visibility">The scrollbar visibility</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithHorizontalScrollbar(ScrollbarVisibility visibility)
	{
		_horizontalScrollbarVisibility = visibility;
		return this;
	}

	/// <summary>
	/// Sets the background and foreground colors
	/// </summary>
	/// <param name="foreground">The foreground color</param>
	/// <param name="background">The background color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the focused background and foreground colors
	/// </summary>
	/// <param name="foreground">The focused foreground color</param>
	/// <param name="background">The focused background color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithFocusedColors(Color foreground, Color background)
	{
		_focusedForegroundColor = foreground;
		_focusedBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the selection background and foreground colors
	/// </summary>
	/// <param name="foreground">The selection foreground color</param>
	/// <param name="background">The selection background color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithSelectionColors(Color foreground, Color background)
	{
		_selectionForegroundColor = foreground;
		_selectionBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the scrollbar colors
	/// </summary>
	/// <param name="trackColor">The scrollbar track color</param>
	/// <param name="thumbColor">The scrollbar thumb color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithScrollbarColors(Color trackColor, Color thumbColor)
	{
		_scrollbarColor = trackColor;
		_scrollbarThumbColor = thumbColor;
		return this;
	}

	/// <summary>
	/// Sets the border color
	/// </summary>
	/// <param name="color">The border color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithBorderColor(Color color)
	{
		_borderColor = color;
		return this;
	}

	/// <summary>
	/// Sets the background color
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The horizontal alignment</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the control horizontally
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder Centered()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="left">Left margin</param>
	/// <param name="top">Top margin</param>
	/// <param name="right">Right margin</param>
	/// <param name="bottom">Bottom margin</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	/// <param name="margin">The margin value for all sides</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the control is visible</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the enabled state
	/// </summary>
	/// <param name="enabled">Whether the control is enabled</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder Enabled(bool enabled = true)
	{
		_isEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Disables the control
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder Disabled()
	{
		_isEnabled = false;
		return this;
	}

	/// <summary>
	/// Sets the read-only state
	/// </summary>
	/// <param name="readOnly">Whether the control is read-only</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder AsReadOnly(bool readOnly = true)
	{
		_readOnly = readOnly;
		return this;
	}

	/// <summary>
	/// Sets the editing state
	/// </summary>
	/// <param name="isEditing">Whether the control is in editing mode</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder IsEditing(bool isEditing = true)
	{
		_isEditing = isEditing;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the content changed event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder OnContentChanged(EventHandler<string> handler)
	{
		_contentChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the content changed event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, content, and window</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder OnContentChanged(WindowEventHandler<string> handler)
	{
		_contentChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the got focus event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the got focus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the lost focus event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the lost focus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public MultilineEditControlBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the multiline edit control
	/// </summary>
	/// <returns>The configured multiline edit control</returns>
	public MultilineEditControl Build()
	{
		var control = new MultilineEditControl(_viewportHeight)
		{
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			IsEnabled = _isEnabled,
			IsEditing = _isEditing,
			ReadOnly = _readOnly,
			WrapMode = _wrapMode,
			HorizontalScrollbarVisibility = _horizontalScrollbarVisibility,
			VerticalScrollbarVisibility = _verticalScrollbarVisibility,
			StickyPosition = _stickyPosition,
			Width = _width,
			Name = _name,
			Tag = _tag
		};

		// Set optional colors
		if (_backgroundColor.HasValue)
			control.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue)
			control.ForegroundColor = _foregroundColor.Value;
		if (_focusedBackgroundColor.HasValue)
			control.FocusedBackgroundColor = _focusedBackgroundColor.Value;
		if (_focusedForegroundColor.HasValue)
			control.FocusedForegroundColor = _focusedForegroundColor.Value;
		if (_borderColor.HasValue)
			control.BorderColor = _borderColor.Value;
		if (_selectionBackgroundColor.HasValue)
			control.SelectionBackgroundColor = _selectionBackgroundColor.Value;
		if (_selectionForegroundColor.HasValue)
			control.SelectionForegroundColor = _selectionForegroundColor.Value;
		if (_scrollbarColor.HasValue)
			control.ScrollbarColor = _scrollbarColor.Value;
		if (_scrollbarThumbColor.HasValue)
			control.ScrollbarThumbColor = _scrollbarThumbColor.Value;

		// Set content if provided
		if (_content != null)
			control.Content = _content;

		// Attach standard event handlers
		if (_contentChangedHandler != null)
			control.ContentChanged += _contentChangedHandler;
		if (_gotFocusHandler != null)
			control.GotFocus += _gotFocusHandler;
		if (_lostFocusHandler != null)
			control.LostFocus += _lostFocusHandler;

		// Attach window-aware event handlers
		if (_contentChangedWithWindowHandler != null)
		{
			control.ContentChanged += (sender, content) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_contentChangedWithWindowHandler(sender, content, window);
			};
		}
		if (_gotFocusWithWindowHandler != null)
		{
			control.GotFocus += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, args, window);
			};
		}
		if (_lostFocusWithWindowHandler != null)
		{
			control.LostFocus += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, args, window);
			};
		}

		return control;
	}

	/// <summary>
	/// Implicit conversion to MultilineEditControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built multiline edit control</returns>
	public static implicit operator MultilineEditControl(MultilineEditControlBuilder builder) => builder.Build();
}
