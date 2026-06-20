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
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="GridControl"/>. Column and row definitions, gaps, padding, size,
/// alignment, colour role, and child placements are accumulated and applied in
/// <see cref="Build"/> in dependency order: track definitions are set first so that placement
/// range-validation in <see cref="GridControl.Place"/> sees the defined tracks, then the deferred
/// <see cref="Place"/>/<see cref="Add"/> intents are replayed in the order they were declared.
/// </summary>
public sealed class GridBuilder : IControlBuilder<GridControl>
{
	private readonly List<GridLength> _columns = new();
	private readonly List<GridLength> _rows = new();

	// Deferred Place/Add intents, stored in a single ordered list so interleaved Place/Add
	// declaration order is preserved when replayed in Build (after the track defs are set).
	private readonly List<Action<GridControl>> _placements = new();

	private int _rowGap;
	private int _columnGap;
	private Padding _padding = new(0, 0, 0, 0);
	private Margin _margin = new(0, 0, 0, 0);
	private int? _width;
	private int? _height;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private string? _name;
	private ColorRole _role = ColorRole.Default;
	private ThemeMode? _colorRoleMode;
	private bool _outline;

	/// <summary>
	/// Adds column track definitions, left to right.
	/// </summary>
	/// <param name="columns">The column lengths (fixed cells, auto-to-content, or star weights).</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder Columns(params GridLength[] columns)
	{
		if (columns != null)
			_columns.AddRange(columns);
		return this;
	}

	/// <summary>
	/// Adds row track definitions, top to bottom.
	/// </summary>
	/// <param name="rows">The row lengths (fixed cells, auto-to-content, or star weights).</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder Rows(params GridLength[] rows)
	{
		if (rows != null)
			_rows.AddRange(rows);
		return this;
	}

	/// <summary>
	/// Sets the gap, in cells, between adjacent rows.
	/// </summary>
	/// <param name="gap">The row gap in cells.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder RowGap(int gap)
	{
		_rowGap = gap;
		return this;
	}

	/// <summary>
	/// Sets the gap, in cells, between adjacent columns.
	/// </summary>
	/// <param name="gap">The column gap in cells.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder ColumnGap(int gap)
	{
		_columnGap = gap;
		return this;
	}

	/// <summary>
	/// Sets the grid's own inner padding.
	/// </summary>
	/// <param name="padding">The padding value.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithPadding(Padding padding)
	{
		_padding = padding;
		return this;
	}

	/// <summary>
	/// Sets the grid's own inner padding.
	/// </summary>
	/// <param name="left">Left padding.</param>
	/// <param name="top">Top padding.</param>
	/// <param name="right">Right padding.</param>
	/// <param name="bottom">Bottom padding.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithPadding(int left, int top, int right, int bottom)
	{
		_padding = new Padding(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets the margin around the grid.
	/// </summary>
	/// <param name="left">Left margin.</param>
	/// <param name="top">Top margin.</param>
	/// <param name="right">Right margin.</param>
	/// <param name="bottom">Bottom margin.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets the margin around the grid.
	/// </summary>
	/// <param name="margin">The margin.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets both the grid's width and height.
	/// </summary>
	/// <param name="width">The width in cells.</param>
	/// <param name="height">The height in cells.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithSize(int width, int height)
	{
		_width = width;
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the grid's width.
	/// </summary>
	/// <param name="width">The width in cells.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the grid's height.
	/// </summary>
	/// <param name="height">The height in cells.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment of the grid within its container.
	/// </summary>
	/// <param name="alignment">The horizontal alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment of the grid within its container.
	/// </summary>
	/// <param name="alignment">The vertical alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the control name for FindControl queries.
	/// </summary>
	/// <param name="name">The control name.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the grid's semantic colour role, which tints the per-cell chrome (cell borders and the
	/// surface fill of cells that opt into chrome) from the theme's role palette.
	/// </summary>
	/// <param name="role">The semantic role determining the chrome colour.</param>
	/// <param name="mode">Optional <see cref="Themes.ThemeMode"/> override for dark/light role-colour derivation. When null, the active theme's mode is used.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder WithColorRole(ColorRole role, ThemeMode? mode = null)
	{
		_role = role;
		_colorRoleMode = mode;
		return this;
	}

	/// <summary>Renders the grid's role chrome in outline style.</summary>
	/// <param name="outline">Whether to render role chrome as an outline.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder Outline(bool outline = true)
	{
		_outline = outline;
		return this;
	}

	/// <summary>
	/// Places a child control at the specified cell, with optional row and column spanning. The
	/// placement is deferred and applied in <see cref="Build"/> after the track definitions are set
	/// so range-validation succeeds.
	/// </summary>
	/// <param name="control">The control to place.</param>
	/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
	/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
	/// <param name="rowSpan">The number of rows the cell occupies. Must be at least 1.</param>
	/// <param name="colSpan">The number of columns the cell occupies. Must be at least 1.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder Place(IWindowControl control, int row, int col, int rowSpan = 1, int colSpan = 1)
	{
		ArgumentNullException.ThrowIfNull(control);
		_placements.Add(grid => grid.Place(control, row, col, rowSpan, colSpan));
		return this;
	}

	/// <summary>
	/// Appends a child control in row-major auto-flow order. The placement is deferred and applied in
	/// <see cref="Build"/> after the track definitions are set, in declaration order relative to any
	/// interleaved <see cref="Place"/> calls.
	/// </summary>
	/// <param name="control">The control to append.</param>
	/// <returns>The builder for chaining.</returns>
	public GridBuilder Add(IWindowControl control)
	{
		ArgumentNullException.ThrowIfNull(control);
		_placements.Add(grid => grid.AddControl(control));
		return this;
	}

	/// <summary>
	/// Builds the configured <see cref="GridControl"/>. Track definitions are applied first so that
	/// the deferred <see cref="Place"/>/<see cref="Add"/> intents (replayed afterwards, in order) see
	/// the defined tracks for range-validation and auto-flow.
	/// </summary>
	/// <returns>The configured control.</returns>
	public GridControl Build()
	{
		var control = new GridControl
		{
			RowGap = _rowGap,
			ColumnGap = _columnGap,
			Padding = _padding,
			Margin = _margin,
			Width = _width,
			Height = _height,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Name = _name,
			ColorRole = _role,
			ColorRoleMode = _colorRoleMode,
			Outline = _outline
		};

		// Track definitions FIRST so placement range-validation and auto-flow see the defined tracks.
		foreach (var column in _columns)
			control.ColumnDefinitions.Add(column);
		foreach (var row in _rows)
			control.RowDefinitions.Add(row);

		// Replay the deferred Place/Add intents in declaration order.
		foreach (var placement in _placements)
			placement(control);

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to <see cref="GridControl"/>.
	/// </summary>
	/// <param name="builder">The builder to convert.</param>
	public static implicit operator GridControl(GridBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.Build();
	}
}
