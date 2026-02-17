// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using Spectre.Console.Rendering;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for Spectre.Console renderable controls
/// </summary>
public sealed class SpectreRenderableBuilder
{
	private IRenderable? _renderable;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private bool _wantsMouseEvents = true;
	private bool _canFocusWithMouse = false;
	private EventHandler<MouseEventArgs>? _mouseClickHandler;
	private EventHandler<MouseEventArgs>? _mouseDoubleClickHandler;
	private EventHandler<MouseEventArgs>? _mouseEnterHandler;
	private EventHandler<MouseEventArgs>? _mouseLeaveHandler;
	private EventHandler<MouseEventArgs>? _mouseMoveHandler;

	/// <summary>
	/// Sets the renderable to display
	/// </summary>
	/// <param name="renderable">The Spectre.Console renderable</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithRenderable(IRenderable renderable)
	{
		_renderable = renderable;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the control horizontally
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder Centered()
	{
		_alignment = HorizontalAlignment.Center;
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
	public SpectreRenderableBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides
	/// </summary>
	/// <param name="margin">The margin value</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="margin">The margin</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">True if visible</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Hides the control
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder Hidden()
	{
		_visible = false;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The width</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for FindControl queries
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the control tag for custom data storage
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithForegroundColor(Color color)
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
	public SpectreRenderableBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Configures whether the control wants to receive mouse events
	/// </summary>
	/// <param name="wants">True to enable mouse events (default), false to disable</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder WithMouseEvents(bool wants = true)
	{
		_wantsMouseEvents = wants;
		return this;
	}

	/// <summary>
	/// Configures whether the control can receive focus via mouse clicks
	/// </summary>
	/// <param name="canFocus">True to enable mouse focus, false to disable (default)</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder CanFocusWithMouse(bool canFocus = true)
	{
		_canFocusWithMouse = canFocus;
		return this;
	}

	/// <summary>
	/// Sets the handler for mouse click events
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder OnClick(EventHandler<MouseEventArgs> handler)
	{
		_mouseClickHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the handler for mouse double-click events
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder OnDoubleClick(EventHandler<MouseEventArgs> handler)
	{
		_mouseDoubleClickHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the handler for mouse enter events
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder OnMouseEnter(EventHandler<MouseEventArgs> handler)
	{
		_mouseEnterHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the handler for mouse leave events
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder OnMouseLeave(EventHandler<MouseEventArgs> handler)
	{
		_mouseLeaveHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the handler for mouse move events
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public SpectreRenderableBuilder OnMouseMove(EventHandler<MouseEventArgs> handler)
	{
		_mouseMoveHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the Spectre renderable control
	/// </summary>
	/// <returns>The configured control</returns>
	public SpectreRenderableControl Build()
	{
		var control = new SpectreRenderableControl
		{
			Renderable = _renderable,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
			WantsMouseEvents = _wantsMouseEvents,
			CanFocusWithMouse = _canFocusWithMouse
		};

		if (_backgroundColor.HasValue)
		{
			control.BackgroundColor = _backgroundColor.Value;
		}

		if (_foregroundColor.HasValue)
		{
			control.ForegroundColor = _foregroundColor.Value;
		}

		// Subscribe event handlers
		if (_mouseClickHandler != null)
			control.MouseClick += _mouseClickHandler;
		if (_mouseDoubleClickHandler != null)
			control.MouseDoubleClick += _mouseDoubleClickHandler;
		if (_mouseEnterHandler != null)
			control.MouseEnter += _mouseEnterHandler;
		if (_mouseLeaveHandler != null)
			control.MouseLeave += _mouseLeaveHandler;
		if (_mouseMoveHandler != null)
			control.MouseMove += _mouseMoveHandler;

		return control;
	}

	/// <summary>
	/// Implicit conversion to SpectreRenderableControl
	/// </summary>
	public static implicit operator SpectreRenderableControl(SpectreRenderableBuilder builder)
	{
		return builder.Build();
	}
}
