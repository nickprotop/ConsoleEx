// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating <see cref="SurfaceViewControl"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// A surface view shows a grid of cells that came from somewhere else - another
/// application over a diagnostics socket, a remote host, a recorded session.
/// Unlike every other control, what it displays is not its own content but a
/// snapshot of someone else's screen.
/// </para>
/// <para>
/// That makes the explicit size worth setting. Without one the view measures
/// itself from the surface it holds, so a pane resizes whenever the screen it
/// is watching does; with one it holds still and scrolls instead.
/// </para>
/// </remarks>
public sealed class SurfaceViewBuilder : IControlBuilder<SurfaceViewControl>
{
	private int? _width;
	private int? _height;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private IReadOnlyList<IReadOnlyList<Cell>>? _surface;
	private int _scrollX;
	private int _scrollY;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<Point>? _cellClickedHandler;

	/// <summary>Sets the size of the view, in cells.</summary>
	/// <remarks>
	/// An explicit size wins over the surface's own, which is what keeps the
	/// view still while the screen it shows changes size.
	/// </remarks>
	public SurfaceViewBuilder WithSize(int width, int height)
	{
		_width = width;
		_height = height;
		return this;
	}

	/// <summary>Sets the width of the view, in cells.</summary>
	public SurfaceViewBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>Sets the height of the view, in cells.</summary>
	public SurfaceViewBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>Sets the cells to show.</summary>
	/// <param name="rows">One list per row, each holding that row's cells.</param>
	public SurfaceViewBuilder WithSurface(IReadOnlyList<IReadOnlyList<Cell>> rows)
	{
		_surface = rows;
		return this;
	}

	/// <summary>Sets where in the surface the view starts.</summary>
	public SurfaceViewBuilder WithScroll(int x, int y)
	{
		_scrollX = x;
		_scrollY = y;
		return this;
	}

	/// <summary>Sets the colour shown where the surface has no cell.</summary>
	public SurfaceViewBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>Sets the foreground colour.</summary>
	public SurfaceViewBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>Sets both colours.</summary>
	public SurfaceViewBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>Called with the surface cell a click landed on.</summary>
	/// <remarks>
	/// The point is in SURFACE coordinates, with the scroll offset already
	/// applied, so it can be sent straight back to whatever produced the
	/// surface.
	/// </remarks>
	public SurfaceViewBuilder OnCellClicked(EventHandler<Point> handler)
	{
		_cellClickedHandler = handler;
		return this;
	}

	/// <summary>Sets the horizontal alignment.</summary>
	public SurfaceViewBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>Sets the vertical alignment.</summary>
	public SurfaceViewBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>Sets the margin on each side.</summary>
	public SurfaceViewBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>Sets the same margin on every side.</summary>
	public SurfaceViewBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>Sets whether the control is visible.</summary>
	public SurfaceViewBuilder WithVisible(bool visible)
	{
		_visible = visible;
		return this;
	}

	/// <summary>Sets the control's name, which is how other code finds it.</summary>
	public SurfaceViewBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>Attaches caller data to the control.</summary>
	public SurfaceViewBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>Sets the sticky position.</summary>
	public SurfaceViewBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>Builds the control.</summary>
	public SurfaceViewControl Build()
	{
		var control = new SurfaceViewControl
		{
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
		};

		if (_width.HasValue) control.Width = _width.Value;
		if (_height.HasValue) control.Height = _height.Value;
		if (_backgroundColor.HasValue) control.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue) control.ForegroundColor = _foregroundColor.Value;

		// The surface before the scroll offset: the offset is clamped against
		// the surface's size, so setting it on an empty view would lose it.
		if (_surface != null) control.SetSurface(_surface);

		if (_scrollX != 0) control.ScrollX = _scrollX;
		if (_scrollY != 0) control.ScrollY = _scrollY;

		if (_cellClickedHandler != null) control.CellClicked += _cellClickedHandler;

		return control;
	}
}
