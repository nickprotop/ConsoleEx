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
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating SplitterControl instances.
/// </summary>
public sealed class SplitterControlBuilder : IControlBuilder<SplitterControl>
{
	private Color? _borderColor;
	private Color? _focusedForegroundColor;
	private Color? _focusedBackgroundColor;
	private Color? _draggingForegroundColor;
	private Color? _draggingBackgroundColor;
	private int? _width;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private ColumnContainer? _leftColumn;
	private ColumnContainer? _rightColumn;
	private EventHandler<SplitterMovedEventArgs>? _splitterMovedHandler;

	/// <summary>
	/// Sets the border color.
	/// </summary>
	public SplitterControlBuilder WithBorderColor(Color color)
	{
		_borderColor = color;
		return this;
	}

	/// <summary>
	/// Sets the focused foreground and background colors.
	/// </summary>
	public SplitterControlBuilder WithFocusedColors(Color foreground, Color background)
	{
		_focusedForegroundColor = foreground;
		_focusedBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the dragging foreground and background colors.
	/// </summary>
	public SplitterControlBuilder WithDraggingColors(Color foreground, Color background)
	{
		_draggingForegroundColor = foreground;
		_draggingBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the splitter width in characters.
	/// </summary>
	public SplitterControlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the adjacent columns that this splitter resizes.
	/// </summary>
	public SplitterControlBuilder WithColumns(ColumnContainer left, ColumnContainer right)
	{
		_leftColumn = left;
		_rightColumn = right;
		return this;
	}

	/// <summary>
	/// Sets the SplitterMoved event handler.
	/// </summary>
	public SplitterControlBuilder OnSplitterMoved(EventHandler<SplitterMovedEventArgs> handler)
	{
		_splitterMovedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public SplitterControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	public SplitterControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public SplitterControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public SplitterControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public SplitterControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public SplitterControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Builds the SplitterControl instance.
	/// </summary>
	public SplitterControl Build()
	{
		var control = (_leftColumn != null && _rightColumn != null)
			? new SplitterControl(_leftColumn, _rightColumn)
			: new SplitterControl();

		control.Margin = _margin;
		control.Visible = _visible;
		control.Name = _name;
		control.Tag = _tag;
		control.StickyPosition = _stickyPosition;

		if (_borderColor.HasValue)
			control.BorderColor = _borderColor.Value;
		if (_focusedForegroundColor.HasValue)
			control.FocusedForegroundColor = _focusedForegroundColor.Value;
		if (_focusedBackgroundColor.HasValue)
			control.FocusedBackgroundColor = _focusedBackgroundColor.Value;
		if (_draggingForegroundColor.HasValue)
			control.DraggingForegroundColor = _draggingForegroundColor.Value;
		if (_draggingBackgroundColor.HasValue)
			control.DraggingBackgroundColor = _draggingBackgroundColor.Value;
		if (_width.HasValue)
			control.Width = _width.Value;

		if (_splitterMovedHandler != null)
			control.SplitterMoved += _splitterMovedHandler;

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to SplitterControl.
	/// </summary>
	public static implicit operator SplitterControl(SplitterControlBuilder builder) => builder.Build();
}
