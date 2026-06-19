// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating HorizontalSplitterControl instances.
/// </summary>
public sealed class HorizontalSplitterBuilder : IControlBuilder<HorizontalSplitterControl>
{
	private Color? _focusedForegroundColor;
	private Color? _focusedBackgroundColor;
	private Color? _draggingForegroundColor;
	private Color? _draggingBackgroundColor;
	private int? _minHeightAbove;
	private int? _minHeightBelow;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private IWindowControl? _aboveControl;
	private IWindowControl? _belowControl;
	private EventHandler<HorizontalSplitterMovedEventArgs>? _splitterMovedHandler;
	private Themes.ColorRole _role = Themes.ColorRole.Default;
	private Themes.ThemeMode? _colorRoleMode;
	private bool _outline = false;

	/// <summary>
	/// Sets the control's semantic colour role (drives the splitter line colour).
	/// </summary>
	/// <param name="role">The semantic role determining the splitter line colour.</param>
	/// <param name="mode">Optional <see cref="Themes.ThemeMode"/> override for dark/light role-colour derivation. When null, the active theme's mode is used.</param>
	/// <returns>The builder for chaining.</returns>
	public HorizontalSplitterBuilder WithColorRole(Themes.ColorRole role, Themes.ThemeMode? mode = null)
	{
		_role = role;
		_colorRoleMode = mode;
		return this;
	}

	/// <summary>
	/// Renders the role accent in outline style.
	/// </summary>
	/// <param name="outline">Whether to use outline style.</param>
	/// <returns>The builder for chaining.</returns>
	public HorizontalSplitterBuilder Outline(bool outline = true)
	{
		_outline = outline;
		return this;
	}

	/// <summary>
	/// Sets the focused foreground and background colors.
	/// </summary>
	public HorizontalSplitterBuilder WithFocusedColors(Color foreground, Color background)
	{
		_focusedForegroundColor = foreground;
		_focusedBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the dragging foreground and background colors.
	/// </summary>
	public HorizontalSplitterBuilder WithDraggingColors(Color foreground, Color background)
	{
		_draggingForegroundColor = foreground;
		_draggingBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the minimum height for the control above the splitter.
	/// </summary>
	public HorizontalSplitterBuilder WithMinHeightAbove(int minHeight)
	{
		_minHeightAbove = minHeight;
		return this;
	}

	/// <summary>
	/// Sets the minimum height for the control below the splitter.
	/// </summary>
	public HorizontalSplitterBuilder WithMinHeightBelow(int minHeight)
	{
		_minHeightBelow = minHeight;
		return this;
	}

	/// <summary>
	/// Sets the minimum heights for both controls adjacent to the splitter.
	/// </summary>
	public HorizontalSplitterBuilder WithMinHeights(int above, int below)
	{
		_minHeightAbove = above;
		_minHeightBelow = below;
		return this;
	}

	/// <summary>
	/// Sets the adjacent controls that this splitter resizes.
	/// If not set, neighbors are auto-discovered from the parent container.
	/// </summary>
	public HorizontalSplitterBuilder WithControls(IWindowControl above, IWindowControl below)
	{
		_aboveControl = above;
		_belowControl = below;
		return this;
	}

	/// <summary>
	/// Sets the SplitterMoved event handler.
	/// </summary>
	public HorizontalSplitterBuilder OnSplitterMoved(EventHandler<HorizontalSplitterMovedEventArgs> handler)
	{
		_splitterMovedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public HorizontalSplitterBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	public HorizontalSplitterBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public HorizontalSplitterBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public HorizontalSplitterBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public HorizontalSplitterBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public HorizontalSplitterBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Builds the HorizontalSplitterControl instance.
	/// </summary>
	public HorizontalSplitterControl Build()
	{
		var control = (_aboveControl != null && _belowControl != null)
			? new HorizontalSplitterControl(_aboveControl, _belowControl)
			: new HorizontalSplitterControl();

		control.Margin = _margin;
		control.Visible = _visible;
		control.Name = _name;
		control.Tag = _tag;
		control.StickyPosition = _stickyPosition;
		control.ColorRole = _role;
		control.ColorRoleMode = _colorRoleMode;
		control.Outline = _outline;

		if (_focusedForegroundColor.HasValue)
			control.FocusedForegroundColor = _focusedForegroundColor.Value;
		if (_focusedBackgroundColor.HasValue)
			control.FocusedBackgroundColor = _focusedBackgroundColor.Value;
		if (_draggingForegroundColor.HasValue)
			control.DraggingForegroundColor = _draggingForegroundColor.Value;
		if (_draggingBackgroundColor.HasValue)
			control.DraggingBackgroundColor = _draggingBackgroundColor.Value;
		if (_minHeightAbove.HasValue)
			control.MinHeightAbove = _minHeightAbove.Value;
		if (_minHeightBelow.HasValue)
			control.MinHeightBelow = _minHeightBelow.Value;

		if (_splitterMovedHandler != null)
			control.SplitterMoved += _splitterMovedHandler;

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to HorizontalSplitterControl.
	/// </summary>
	public static implicit operator HorizontalSplitterControl(HorizontalSplitterBuilder builder) => builder.Build();
}
