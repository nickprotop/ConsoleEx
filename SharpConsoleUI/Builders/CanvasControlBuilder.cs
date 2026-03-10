// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating CanvasControl instances.
/// </summary>
public sealed class CanvasControlBuilder : IControlBuilder<CanvasControl>
{
	private int? _canvasWidth;
	private int? _canvasHeight;
	private bool? _autoSize;
	private bool? _autoClear;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private bool _isEnabled = true;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<CanvasPaintEventArgs>? _paintHandler;
	private EventHandler<ConsoleKeyInfo>? _keyPressedHandler;
	private EventHandler<CanvasMouseEventArgs>? _mouseClickHandler;
	private EventHandler<CanvasMouseEventArgs>? _mouseMoveHandler;
	private EventHandler<CanvasMouseEventArgs>? _mouseRightClickHandler;
	private EventHandler? _gotFocusHandler;
	private EventHandler? _lostFocusHandler;

	/// <summary>
	/// Sets the canvas size in characters.
	/// </summary>
	public CanvasControlBuilder WithSize(int width, int height)
	{
		_canvasWidth = width;
		_canvasHeight = height;
		return this;
	}

	/// <summary>
	/// Sets the canvas width in characters.
	/// </summary>
	public CanvasControlBuilder WithCanvasWidth(int width)
	{
		_canvasWidth = width;
		return this;
	}

	/// <summary>
	/// Sets the canvas height in characters.
	/// </summary>
	public CanvasControlBuilder WithCanvasHeight(int height)
	{
		_canvasHeight = height;
		return this;
	}

	/// <summary>
	/// Enables auto-size mode where the canvas resizes to match layout bounds.
	/// </summary>
	public CanvasControlBuilder AutoSize(bool autoSize = true)
	{
		_autoSize = autoSize;
		return this;
	}

	/// <summary>
	/// Enables auto-clear mode where the buffer is cleared each frame.
	/// </summary>
	public CanvasControlBuilder AutoClear(bool autoClear = true)
	{
		_autoClear = autoClear;
		return this;
	}

	/// <summary>
	/// Sets the background color.
	/// </summary>
	public CanvasControlBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color.
	/// </summary>
	public CanvasControlBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both foreground and background colors.
	/// </summary>
	public CanvasControlBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets whether the control is enabled.
	/// </summary>
	public CanvasControlBuilder Enabled(bool enabled = true)
	{
		_isEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets the Paint event handler fired during each render cycle.
	/// </summary>
	public CanvasControlBuilder OnPaint(EventHandler<CanvasPaintEventArgs> handler)
	{
		_paintHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the key pressed event handler.
	/// </summary>
	public CanvasControlBuilder OnKeyPressed(EventHandler<ConsoleKeyInfo> handler)
	{
		_keyPressedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the mouse click event handler with canvas-local coordinates.
	/// </summary>
	public CanvasControlBuilder OnMouseClick(EventHandler<CanvasMouseEventArgs> handler)
	{
		_mouseClickHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the mouse move event handler with canvas-local coordinates.
	/// </summary>
	public CanvasControlBuilder OnMouseMove(EventHandler<CanvasMouseEventArgs> handler)
	{
		_mouseMoveHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the mouse right-click event handler with canvas-local coordinates.
	/// </summary>
	public CanvasControlBuilder OnMouseRightClick(EventHandler<CanvasMouseEventArgs> handler)
	{
		_mouseRightClickHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler.
	/// </summary>
	public CanvasControlBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler.
	/// </summary>
	public CanvasControlBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public CanvasControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public CanvasControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public CanvasControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	public CanvasControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public CanvasControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public CanvasControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public CanvasControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public CanvasControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window.
	/// </summary>
	public CanvasControlBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window.
	/// </summary>
	public CanvasControlBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Builds the CanvasControl instance.
	/// </summary>
	public CanvasControl Build()
	{
		var control = (_canvasWidth.HasValue && _canvasHeight.HasValue)
			? new CanvasControl(_canvasWidth.Value, _canvasHeight.Value)
			: new CanvasControl();

		control.HorizontalAlignment = _alignment;
		control.VerticalAlignment = _verticalAlignment;
		control.Margin = _margin;
		control.Visible = _visible;
		control.Name = _name;
		control.Tag = _tag;
		control.StickyPosition = _stickyPosition;
		control.IsEnabled = _isEnabled;

		if (_autoSize.HasValue)
			control.AutoSize = _autoSize.Value;
		if (_autoClear.HasValue)
			control.AutoClear = _autoClear.Value;
		if (_backgroundColor.HasValue)
			control.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue)
			control.ForegroundColor = _foregroundColor.Value;

		if (_paintHandler != null)
			control.Paint += _paintHandler;
		if (_keyPressedHandler != null)
			control.CanvasKeyPressed += _keyPressedHandler;
		if (_mouseClickHandler != null)
			control.CanvasMouseClick += _mouseClickHandler;
		if (_mouseMoveHandler != null)
			control.CanvasMouseMove += _mouseMoveHandler;
		if (_mouseRightClickHandler != null)
			control.CanvasMouseRightClick += _mouseRightClickHandler;
		if (_gotFocusHandler != null)
			control.GotFocus += _gotFocusHandler;
		if (_lostFocusHandler != null)
			control.LostFocus += _lostFocusHandler;

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to CanvasControl.
	/// </summary>
	public static implicit operator CanvasControl(CanvasControlBuilder builder) => builder.Build();
}
