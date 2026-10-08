// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using ScrollbarVisibility = SharpConsoleUI.Controls.ScrollbarVisibility;
using TableColumn = SharpConsoleUI.Controls.TableColumn;
using TableRow = SharpConsoleUI.Controls.TableRow;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Shared fluent builder base for <see cref="TableControl"/> and the tables derived from it. It is
/// self-typed (<typeparamref name="TSelf"/>) so every fluent method returns the concrete builder,
/// keeping a chain intact through inheritance; a derived builder adds its own methods and chooses
/// which table <see cref="Apply{T}"/> is given.
/// </summary>
/// <typeparam name="TSelf">The concrete builder type, returned from every fluent method.</typeparam>
/// <remarks>
/// <para>
/// WHY A BASE. A table derived from <see cref="TableControl"/> has every one of the table's settings,
/// and its builder needs every one of these methods. Copying seventy of them would let the two drift;
/// composing a <see cref="TableControlBuilder"/> inside another builder would break the chain at the
/// first table method. <see cref="FlowControlBuilderBase{TSelf}"/> is the precedent.
/// </para>
/// <para>
/// Rows given as cell text are created when the table is built, through the table's own
/// <c>AddRow(string[])</c>, so a derived table's row factory applies to them. The order of rows given
/// as text and as rows is kept.
/// </para>
/// </remarks>
public abstract class TableControlBuilderBase<TSelf>
	where TSelf : TableControlBuilderBase<TSelf>
{
	private readonly List<TableColumn> _columns = new();
	private readonly List<PendingRow> _rows = new();
	private BorderStyle _borderStyle = BorderStyle.Single;
	private Color? _borderColor;
	private Color? _headerBackgroundColor = Color.Default;
	private Color? _headerForegroundColor = Color.Default;
	private bool _showHeader = true;
	private bool _showRowSeparators = false;
	private bool _useSafeBorder = false;
	private string? _title;
	private TextJustification _titleAlignment = TextJustification.Center;

	// Interactive properties
	private bool _readOnly = true;
	private bool _cellNavigationEnabled = false;
	private bool _multiSelectEnabled = false;
	private bool _hoverEnabled = true;
	private bool _checkboxMode = false;
	private bool _rightClickExtendsSelection = false;
	private bool _sortingEnabled = false;
	private bool _columnResizeEnabled = false;
	private char? _columnSeparator;
	private Color? _columnSeparatorColor;
	private bool _columnSeparatorPadded;
	private bool _scrollbarGutter;
	private bool _inlineEditingEnabled = false;
	private bool _filteringEnabled = false;
	private bool _fuzzyFilterEnabled = false;
	private ScrollbarVisibility _verticalScrollbarVisibility = ScrollbarVisibility.Auto;
	private ScrollbarVisibility _horizontalScrollbarVisibility = ScrollbarVisibility.Auto;
	private int _minScrollbarThumbSize = ControlDefaults.DefaultMinScrollbarThumbSize;
	private int _mouseWheelScrollSpeed = ControlDefaults.DefaultScrollWheelLines;
	private ITableDataSource? _dataSource;

	// Event handlers
	private EventHandler<int>? _onSelectedRowChanged;
	private EventHandler<int>? _onRowActivated;
	private EventHandler<(int Row, int Column)>? _onCellActivated;
	private EventHandler<(int Row, int Column, string OldValue, string NewValue)>? _onCellEditCompleted;
	private EventHandler<SharpConsoleUI.Events.MouseEventArgs>? _onRightClick;

	// Base properties
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private int? _height;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color? _backgroundColor = Color.Default;
	private Color? _foregroundColor = Color.Default;
	private Themes.ColorRole _role = Themes.ColorRole.Default;
	private Themes.ThemeMode? _colorRoleMode;
	private bool _outline = false;

	private TSelf Self => (TSelf)this;

	/// <summary>
	/// Sets the control's semantic colour role (drives the row-selection accent colour).
	/// </summary>
	/// <param name="role">The semantic role determining the selection accent colour.</param>
	/// <param name="mode">Optional <see cref="Themes.ThemeMode"/> override for dark/light role-colour derivation. When null, the active theme's mode is used.</param>
	/// <returns>The builder for chaining.</returns>
	public TSelf WithColorRole(Themes.ColorRole role, Themes.ThemeMode? mode = null)
	{
		_role = role;
		_colorRoleMode = mode;
		return Self;
	}

	/// <summary>
	/// Renders the role accent in outline style.
	/// </summary>
	/// <param name="outline">Whether to use outline style.</param>
	/// <returns>The builder for chaining.</returns>
	public TSelf Outline(bool outline = true)
	{
		_outline = outline;
		return Self;
	}

	#region Column Configuration

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	public TSelf AddColumn(string header, TextJustification alignment = TextJustification.Left, int? width = null)
	{
		_columns.Add(new TableColumn(header, alignment, width));
		return Self;
	}

	/// <summary>
	/// Adds a custom column to the table.
	/// </summary>
	public TSelf AddColumn(TableColumn column)
	{
		_columns.Add(column);
		return Self;
	}

	/// <summary>
	/// Adds a column with a minimum auto-width floor. See <see cref="TableColumn.MinWidth"/>.
	/// </summary>
	public TSelf AddColumn(string header, TextJustification alignment, int? width, int? minWidth)
	{
		_columns.Add(new TableColumn(header, alignment, width) { MinWidth = minWidth });
		return Self;
	}

	/// <summary>
	/// Adds multiple columns from header names.
	/// </summary>
	public TSelf WithColumns(params string[] headers)
	{
		foreach (var header in headers)
			_columns.Add(new TableColumn(header));
		return Self;
	}

	#endregion

	#region Row Data

	/// <summary>
	/// Adds a row with the specified cells.
	/// </summary>
	public TSelf AddRow(params string[] cells)
	{
		_rows.Add(new PendingRow(cells, null));
		return Self;
	}

	/// <summary>
	/// Adds a custom row to the table.
	/// </summary>
	public TSelf AddRow(TableRow row)
	{
		_rows.Add(new PendingRow(null, row));
		return Self;
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public TSelf WithRows(IEnumerable<TableRow> rows)
	{
		foreach (var row in rows)
			_rows.Add(new PendingRow(null, row));
		return Self;
	}

	#endregion

	#region Border Styling

	/// <summary>
	/// Sets the border style.
	/// </summary>
	public TSelf WithBorderStyle(BorderStyle style)
	{
		_borderStyle = style;
		return Self;
	}

	/// <summary>
	/// Uses double-line border style.
	/// </summary>
	public TSelf DoubleLine()
	{
		_borderStyle = BorderStyle.DoubleLine;
		return Self;
	}

	/// <summary>
	/// Uses single-line border style.
	/// </summary>
	public TSelf SingleLine()
	{
		_borderStyle = BorderStyle.Single;
		return Self;
	}

	/// <summary>
	/// Uses rounded border style.
	/// </summary>
	public TSelf Rounded()
	{
		_borderStyle = BorderStyle.Rounded;
		return Self;
	}

	/// <summary>
	/// Uses double-line border style.
	/// </summary>
	public TSelf DoubleLineBorder()
	{
		_borderStyle = BorderStyle.DoubleLine;
		return Self;
	}

	/// <summary>
	/// Uses no border.
	/// </summary>
	public TSelf NoBorder()
	{
		_borderStyle = BorderStyle.None;
		return Self;
	}

	/// <summary>
	/// Sets the border color.
	/// </summary>
	public TSelf WithBorderColor(Color color)
	{
		_borderColor = color;
		return Self;
	}

	/// <summary>
	/// Enables safe border characters for compatibility.
	/// </summary>
	public TSelf WithSafeBorder(bool useSafe = true)
	{
		_useSafeBorder = useSafe;
		return Self;
	}

	#endregion

	#region Header Configuration

	/// <summary>
	/// Shows or hides the header row.
	/// </summary>
	public TSelf ShowHeader(bool show = true)
	{
		_showHeader = show;
		return Self;
	}

	/// <summary>
	/// Hides the header row.
	/// </summary>
	public TSelf HideHeader()
	{
		_showHeader = false;
		return Self;
	}

	/// <summary>
	/// Sets the header colors.
	/// </summary>
	public TSelf WithHeaderColors(Color foreground, Color background)
	{
		_headerForegroundColor = foreground;
		_headerBackgroundColor = background;
		return Self;
	}

	/// <summary>
	/// Sets the table title.
	/// </summary>
	public TSelf WithTitle(string title, TextJustification alignment = TextJustification.Center)
	{
		_title = title;
		_titleAlignment = alignment;
		return Self;
	}

	#endregion

	#region Row Configuration

	/// <summary>
	/// Shows or hides row separators.
	/// </summary>
	public TSelf ShowRowSeparators(bool show = true)
	{
		_showRowSeparators = show;
		return Self;
	}

	#endregion


	#region Layout Configuration

	/// <summary>
	/// Sets the explicit width.
	/// </summary>
	public TSelf WithWidth(int width)
	{
		_width = width;
		return Self;
	}

	/// <summary>
	/// Sets the explicit height.
	/// </summary>
	public TSelf WithHeight(int height)
	{
		_height = height;
		return Self;
	}

	/// <summary>
	/// Sets the margin around the table.
	/// </summary>
	public TSelf WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return Self;
	}

	/// <summary>
	/// Sets the margin with separate horizontal and vertical values.
	/// </summary>
	public TSelf WithMargin(int horizontal, int vertical)
	{
		_margin = new Margin(horizontal, vertical, horizontal, vertical);
		return Self;
	}

	/// <summary>
	/// Sets the margin with individual values.
	/// </summary>
	public TSelf WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return Self;
	}

	/// <summary>
	/// Centers the table horizontally and vertically.
	/// </summary>
	public TSelf Centered()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		_verticalAlignment = VerticalAlignment.Center;
		return Self;
	}

	/// <summary>
	/// Centers the table horizontally.
	/// </summary>
	public TSelf CenterHorizontal()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		return Self;
	}

	/// <summary>
	/// Centers the table vertically.
	/// </summary>
	public TSelf CenterVertical()
	{
		_verticalAlignment = VerticalAlignment.Center;
		return Self;
	}

	/// <summary>
	/// Stretches the table horizontally to fill available width.
	/// </summary>
	public TSelf StretchHorizontal()
	{
		_horizontalAlignment = HorizontalAlignment.Stretch;
		return Self;
	}

	/// <summary>
	/// Aligns the table to the bottom vertically.
	/// </summary>
	public TSelf AlignBottom()
	{
		_verticalAlignment = VerticalAlignment.Bottom;
		return Self;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public TSelf WithHorizontalAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return Self;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public TSelf WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return Self;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public TSelf WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return Self;
	}

	/// <summary>
	/// Makes the table stick to the top during scrolling.
	/// </summary>
	public TSelf StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return Self;
	}

	/// <summary>
	/// Makes the table stick to the bottom during scrolling.
	/// </summary>
	public TSelf StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return Self;
	}

	#endregion

	#region Colors

	/// <summary>
	/// Sets the background color.
	/// </summary>
	public TSelf WithBackgroundColor(Color? color)
	{
		_backgroundColor = color;
		return Self;
	}

	/// <summary>
	/// Sets the foreground (text) color. Pass null to inherit from container.
	/// </summary>
	public TSelf WithForegroundColor(Color? color)
	{
		_foregroundColor = color;
		return Self;
	}

	/// <summary>
	/// Sets both foreground and background colors. Pass null to inherit from container.
	/// </summary>
	public TSelf WithColors(Color? foreground, Color? background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return Self;
	}

	#endregion

	#region Base Properties

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public TSelf WithName(string name)
	{
		_name = name;
		return Self;
	}

	/// <summary>
	/// Sets the tag for custom data.
	/// </summary>
	public TSelf WithTag(object tag)
	{
		_tag = tag;
		return Self;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public TSelf WithVisibility(bool visible)
	{
		_visible = visible;
		return Self;
	}

	/// <summary>
	/// Hides the table.
	/// </summary>
	public TSelf Hidden()
	{
		_visible = false;
		return Self;
	}

	#endregion

	#region Interactive Configuration

	/// <summary>
	/// Enables editing mode (sets ReadOnly to false), allowing inline cell editing and column resizing.
	/// </summary>
	public TSelf Interactive()
	{
		_readOnly = false;
		return Self;
	}

	/// <summary>
	/// Enables cell-level navigation with Tab/Left/Right keys.
	/// </summary>
	public TSelf WithCellNavigation()
	{
		_cellNavigationEnabled = true;
		return Self;
	}

	/// <summary>
	/// Disables mouse hover highlighting on rows.
	/// </summary>
	public TSelf WithHoverDisabled()
	{
		_hoverEnabled = false;
		return Self;
	}

	/// <summary>
	/// Enables multi-selection with Ctrl+Click and Shift+Click.
	/// </summary>
	public TSelf WithMultiSelect()
	{
		_multiSelectEnabled = true;
		return Self;
	}

	/// <summary>
	/// Enables right-click extending the multi-selection from the anchor to the clicked row, for
	/// terminals that never deliver Shift+Click. Implies multi-select.
	/// </summary>
	/// <remarks>
	/// Off by default: right-click otherwise selects a single row and fires the right-click event,
	/// which is what a context menu acting on the current selection expects.
	/// </remarks>
	public TSelf WithRightClickExtendsSelection()
	{
		_rightClickExtendsSelection = true;
		_multiSelectEnabled = true;
		return Self;
	}

	/// <summary>
	/// Enables checkbox mode for multi-selection.
	/// </summary>
	public TSelf WithCheckboxMode()
	{
		_checkboxMode = true;
		_multiSelectEnabled = true;
		return Self;
	}

	/// <summary>
	/// Enables column sorting by clicking headers.
	/// </summary>
	public TSelf WithSorting()
	{
		_sortingEnabled = true;
		return Self;
	}

	/// <summary>
	/// Enables column resizing by dragging column borders. Implies Interactive().
	/// </summary>
	public TSelf WithColumnResize()
	{
		_columnResizeEnabled = true;
		_readOnly = false;
		return Self;
	}

	/// <summary>
	/// Sets an optional character drawn between columns when borders are disabled.
	/// </summary>
	/// <param name="separator">The glyph drawn between columns (e.g. <c>'│'</c>).</param>
	/// <param name="color">Separator color; falls back to the border color when null.</param>
	/// <param name="padded">
	/// When <c>true</c>, the glyph is drawn with one space on each side (<c>" │ "</c>) for breathing
	/// room instead of flush against the adjacent cell text. Defaults to <c>false</c> (flush), which
	/// preserves the original single-cell-wide separator.
	/// </param>
	public TSelf WithColumnSeparator(char separator, Color? color = null, bool padded = false)
	{
		_columnSeparator = separator;
		_columnSeparatorColor = color;
		_columnSeparatorPadded = padded;
		return Self;
	}

	/// <summary>
	/// Reserves a one-cell blank gutter between the column content and the vertical scrollbar, so a
	/// right-aligned final column doesn't sit flush against the scrollbar. Only has effect when a
	/// vertical scrollbar is shown. Off by default.
	/// </summary>
	public TSelf ScrollbarGutter(bool enabled = true)
	{
		_scrollbarGutter = enabled;
		return Self;
	}

	/// <summary>
	/// Enables inline cell editing with F2 to start, Enter to commit, Escape to cancel. Implies Interactive().
	/// </summary>
	public TSelf WithInlineEditing()
	{
		_inlineEditingEnabled = true;
		_cellNavigationEnabled = true;
		_readOnly = false;
		return Self;
	}

	/// <summary>
	/// Enables inline filtering with '/' key. Implies Interactive().
	/// </summary>
	public TSelf WithFiltering()
	{
		_filteringEnabled = true;
		_readOnly = false;
		return Self;
	}

	/// <summary>
	/// Enables fuzzy (character-subsequence) filter matching. Implies WithFiltering().
	/// </summary>
	public TSelf WithFuzzyFilter()
	{
		_fuzzyFilterEnabled = true;
		_filteringEnabled = true;
		_readOnly = false;
		return Self;
	}

	/// <summary>
	/// Sets the vertical scrollbar visibility.
	/// </summary>
	public TSelf WithVerticalScrollbar(ScrollbarVisibility visibility)
	{
		_verticalScrollbarVisibility = visibility;
		return Self;
	}

	/// <summary>
	/// Sets the horizontal scrollbar visibility.
	/// </summary>
	public TSelf WithHorizontalScrollbar(ScrollbarVisibility visibility)
	{
		_horizontalScrollbarVisibility = visibility;
		return Self;
	}

	/// <summary>
	/// Sets the minimum vertical scrollbar thumb height, in rows.
	/// </summary>
	public TSelf WithMinScrollbarThumbSize(int size)
	{
		_minScrollbarThumbSize = size;
		return Self;
	}

	/// <summary>
	/// Sets the number of rows scrolled per mouse wheel notch.
	/// Default: <see cref="ControlDefaults.DefaultScrollWheelLines"/>.
	/// </summary>
	public TSelf WithMouseWheelScrollSpeed(int speed)
	{
		_mouseWheelScrollSpeed = Math.Max(1, speed);
		return Self;
	}

	/// <summary>
	/// Sets the virtual data source for lazy loading.
	/// </summary>
	public TSelf WithDataSource(ITableDataSource dataSource)
	{
		_dataSource = dataSource;
		return Self;
	}

	/// <summary>
	/// Wires the SelectedRowChanged event handler.
	/// </summary>
	public TSelf OnSelectedRowChanged(EventHandler<int> handler)
	{
		_onSelectedRowChanged = handler;
		return Self;
	}

	/// <summary>
	/// Wires the RowActivated event handler.
	/// </summary>
	public TSelf OnRowActivated(EventHandler<int> handler)
	{
		_onRowActivated = handler;
		return Self;
	}

	/// <summary>
	/// Wires the CellActivated event handler.
	/// </summary>
	public TSelf OnCellActivated(EventHandler<(int Row, int Column)> handler)
	{
		_onCellActivated = handler;
		return Self;
	}

	/// <summary>
	/// Wires the CellEditCompleted event handler.
	/// </summary>
	public TSelf OnCellEditCompleted(EventHandler<(int Row, int Column, string OldValue, string NewValue)> handler)
	{
		_onCellEditCompleted = handler;
		return Self;
	}

	/// <summary>
	/// Wires the MouseRightClick event handler for context menu support.
	/// </summary>
	public TSelf OnRightClick(EventHandler<SharpConsoleUI.Events.MouseEventArgs> handler)
	{
		_onRightClick = handler;
		return Self;
	}

	#endregion

	#region Apply

	/// <summary>
	/// Applies the builder's configuration to <paramref name="table"/> and returns it. A builder calls
	/// this from its <c>Build()</c> with the table it constructs.
	/// </summary>
	/// <typeparam name="T">The concrete table type to configure.</typeparam>
	/// <param name="table">The table to configure; a derived builder sets its own properties first.</param>
	/// <returns>The configured table.</returns>
	/// <remarks>
	/// The settings are applied in the order the table needs: multi-select before checkbox mode, the
	/// data source before columns and rows, which are added only without one, then the event handlers,
	/// and the deferred bindings last of all, so they see the finished table.
	/// </remarks>
	protected T Apply<T>(T table) where T : TableControl
	{
		table.BorderStyle = _borderStyle;
		table.BorderColor = _borderColor;
		table.HeaderBackgroundColor = _headerBackgroundColor;
		table.HeaderForegroundColor = _headerForegroundColor;
		table.ShowHeader = _showHeader;
		table.ShowRowSeparators = _showRowSeparators;
		table.UseSafeBorder = _useSafeBorder;
		table.Title = _title;
		table.TitleAlignment = _titleAlignment;

		// Interactive properties
		table.ReadOnly = _readOnly;
		table.CellNavigationEnabled = _cellNavigationEnabled;
		table.MultiSelectEnabled = _multiSelectEnabled;
		table.HoverEnabled = _hoverEnabled;
		table.CheckboxMode = _checkboxMode;
		table.RightClickExtendsSelection = _rightClickExtendsSelection;
		table.SortingEnabled = _sortingEnabled;
		table.ColumnResizeEnabled = _columnResizeEnabled;
		table.ColumnSeparator = _columnSeparator;
		table.ColumnSeparatorColor = _columnSeparatorColor;
		table.ColumnSeparatorPadded = _columnSeparatorPadded;
		table.ScrollbarGutter = _scrollbarGutter;
		table.InlineEditingEnabled = _inlineEditingEnabled;
		table.FilteringEnabled = _filteringEnabled;
		table.FuzzyFilterEnabled = _fuzzyFilterEnabled;
		table.VerticalScrollbarVisibility = _verticalScrollbarVisibility;
		table.HorizontalScrollbarVisibility = _horizontalScrollbarVisibility;
		table.MinScrollbarThumbSize = _minScrollbarThumbSize;
		table.MouseWheelScrollSpeed = _mouseWheelScrollSpeed;

		// Base properties
		table.HorizontalAlignment = _horizontalAlignment;
		table.VerticalAlignment = _verticalAlignment;
		table.Margin = _margin;
		table.Visible = _visible;
		table.Width = _width;
		table.Height = _height;
		table.Name = _name;
		table.Tag = _tag;
		table.StickyPosition = _stickyPosition;
		table.BackgroundColor = _backgroundColor;
		table.ForegroundColor = _foregroundColor;
		table.ColorRole = _role;
		table.ColorRoleMode = _colorRoleMode;
		table.Outline = _outline;

		// Set data source if provided
		if (_dataSource != null)
			table.DataSource = _dataSource;

		// Add columns and rows (only if no data source)
		if (_dataSource == null)
		{
			foreach (var column in _columns)
				table.AddColumn(column);

			foreach (var pending in _rows)
			{
				if (pending.Row != null)
					table.AddRow(pending.Row);
				else
					table.AddRow(pending.Cells!);
			}
		}

		// Wire events
		if (_onSelectedRowChanged != null)
			table.SelectedRowChanged += _onSelectedRowChanged;
		if (_onRowActivated != null)
			table.RowActivated += _onRowActivated;
		if (_onCellActivated != null)
			table.CellActivated += _onCellActivated;
		if (_onCellEditCompleted != null)
			table.CellEditCompleted += _onCellEditCompleted;
		if (_onRightClick != null)
			table.MouseRightClick += _onRightClick;

		BindingHelper.ApplyDeferredBindings(this, table);
		return table;
	}

	/// <summary>A row waiting to be added: either cell text, created by the table, or a row as given.</summary>
	private readonly record struct PendingRow(string[]? Cells, TableRow? Row);

	#endregion
}
