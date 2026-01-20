// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for scrollable panel controls
/// </summary>
public sealed class ScrollablePanelBuilder
{
	private readonly List<IWindowControl> _children = new();
	private readonly List<EventHandler<ScrollEventArgs>> _scrolledHandlers = new();
	private readonly List<EventHandler> _gotFocusHandlers = new();
	private readonly List<EventHandler> _lostFocusHandlers = new();

	private bool _showScrollbar = true;
	private ScrollbarPosition _scrollbarPosition = ScrollbarPosition.Right;
	private ScrollMode _horizontalScrollMode = ScrollMode.None;
	private ScrollMode _verticalScrollMode = ScrollMode.Scroll;
	private bool _enableMouseWheel = true;
	private bool _autoScroll = false;

	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color _backgroundColor = Color.Black;
	private Color _foregroundColor = Color.White;

	/// <summary>
	/// Adds a child control to the panel
	/// </summary>
	/// <param name="control">The control to add</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder AddControl(IWindowControl control)
	{
		_children.Add(control);
		return this;
	}

	/// <summary>
	/// Sets the vertical scroll mode
	/// </summary>
	/// <param name="mode">The scroll mode (None, Scroll, or Wrap)</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithVerticalScroll(ScrollMode mode = ScrollMode.Scroll)
	{
		_verticalScrollMode = mode;
		return this;
	}

	/// <summary>
	/// Sets the horizontal scroll mode
	/// </summary>
	/// <param name="mode">The scroll mode (None, Scroll, or Wrap)</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithHorizontalScroll(ScrollMode mode = ScrollMode.Scroll)
	{
		_horizontalScrollMode = mode;
		return this;
	}

	/// <summary>
	/// Sets whether to show the scrollbar
	/// </summary>
	/// <param name="show">True to show scrollbar</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithScrollbar(bool show = true)
	{
		_showScrollbar = show;
		return this;
	}

	/// <summary>
	/// Sets the scrollbar position
	/// </summary>
	/// <param name="position">The scrollbar position (Left or Right)</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithScrollbarPosition(ScrollbarPosition position)
	{
		_scrollbarPosition = position;
		return this;
	}

	/// <summary>
	/// Sets the scrollbar position to left
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder ScrollbarLeft()
	{
		_scrollbarPosition = ScrollbarPosition.Left;
		return this;
	}

	/// <summary>
	/// Sets the scrollbar position to right
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder ScrollbarRight()
	{
		_scrollbarPosition = ScrollbarPosition.Right;
		return this;
	}

	/// <summary>
	/// Sets whether mouse wheel scrolling is enabled
	/// </summary>
	/// <param name="enable">True to enable mouse wheel scrolling</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithMouseWheel(bool enable = true)
	{
		_enableMouseWheel = enable;
		return this;
	}

	/// <summary>
	/// Enables or disables automatic scrolling to bottom when content is added.
	/// When enabled, new content auto-scrolls to bottom if already at bottom,
	/// disables when user scrolls up, and re-enables when user scrolls to bottom.
	/// </summary>
	/// <param name="enabled">True to enable auto-scroll</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithAutoScroll(bool enabled = true)
	{
		_autoScroll = enabled;
		return this;
	}

	/// <summary>
	/// Attaches a handler for the Scrolled event
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder OnScrolled(EventHandler<ScrollEventArgs> handler)
	{
		_scrolledHandlers.Add(handler);
		return this;
	}

	/// <summary>
	/// Attaches a handler for the GotFocus event
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandlers.Add(handler);
		return this;
	}

	/// <summary>
	/// Attaches a handler for the LostFocus event
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandlers.Add(handler);
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithVerticalAlignment(VerticalAlignment alignment)
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
	public ScrollablePanelBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides
	/// </summary>
	/// <param name="margin">The margin value</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="margin">The margin</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">True if visible</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The width</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for FindControl queries
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the control tag for custom data storage
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both foreground and background colors
	/// </summary>
	/// <param name="foreground">The foreground color</param>
	/// <param name="background">The background color</param>
	/// <returns>The builder for chaining</returns>
	public ScrollablePanelBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Builds the scrollable panel control
	/// </summary>
	/// <returns>The configured control</returns>
	public ScrollablePanelControl Build()
	{
		var control = new ScrollablePanelControl
		{
			ShowScrollbar = _showScrollbar,
			ScrollbarPosition = _scrollbarPosition,
			HorizontalScrollMode = _horizontalScrollMode,
			VerticalScrollMode = _verticalScrollMode,
			EnableMouseWheel = _enableMouseWheel,
			AutoScroll = _autoScroll,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
			BackgroundColor = _backgroundColor,
			ForegroundColor = _foregroundColor
		};

		// Add all children
		foreach (var child in _children)
		{
			control.AddControl(child);
		}

		// Attach event handlers
		foreach (var handler in _scrolledHandlers)
		{
			control.Scrolled += handler;
		}
		foreach (var handler in _gotFocusHandlers)
		{
			control.GotFocus += handler;
		}
		foreach (var handler in _lostFocusHandlers)
		{
			control.LostFocus += handler;
		}

		return control;
	}

	/// <summary>
	/// Implicit conversion to ScrollablePanelControl
	/// </summary>
	public static implicit operator ScrollablePanelControl(ScrollablePanelBuilder builder)
	{
		return builder.Build();
	}
}
