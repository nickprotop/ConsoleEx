// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Spectre.Console.Rendering;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A table control that wraps Spectre.Console's Table widget.
/// Provides read-only display of tabular data with theming support.
/// </summary>
public class TableControl : IWindowControl, IDOMPaintable, IMouseAwareControl
{
	#region Fields

	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Color? _backgroundColorValue;
	private Color? _foregroundColorValue;
	private Margin _margin = new Margin(0, 0, 0, 0);
	private StickyPosition _stickyPosition = StickyPosition.None;
	private bool _visible = true;
	private int? _width;
	private int? _height;

	// Table-specific fields
	private List<TableColumn> _columns = new();
	private List<TableRow> _rows = new();
	private BorderStyle _borderStyle = BorderStyle.Single;
	private Color? _borderColorValue;
	private Color? _headerBackgroundColorValue;
	private Color? _headerForegroundColorValue;
	private bool _showHeader = true;
	private bool _showRowSeparators = false;
	private bool _useSafeBorder = false;
	private string? _title;
	private Justify _titleAlignment = Justify.Center;

	// Performance caches
	private readonly TextMeasurementCache _measurementCache;

	// Mouse support (minimal - for bubbling only)
	private bool _wantsMouseEvents = true;

	private int _actualX;
	private int _actualY;
	private int _actualWidth;
	private int _actualHeight;

	#endregion

	#region Constructors

	/// <summary>
	/// Initializes a new instance of the <see cref="TableControl"/> class.
	/// </summary>
	public TableControl()
	{
		_measurementCache = new TextMeasurementCache(AnsiConsoleHelper.StripSpectreLength);
	}

	#endregion

	#region IWindowControl Properties

	/// <inheritdoc/>
	public int? ContentWidth => _width;

	/// <inheritdoc/>
	public int? ContentHeight => _height;

	/// <inheritdoc/>
	public int ActualX => _actualX;

	/// <inheritdoc/>
	public int ActualY => _actualY;

	/// <inheritdoc/>
	public int ActualWidth => _actualWidth;

	/// <inheritdoc/>
	public int ActualHeight => _actualHeight;

	/// <inheritdoc/>
	public Color? BackgroundColor
	{
		get => _backgroundColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _backgroundColorValue, value, Container);
	}

	/// <inheritdoc/>
	public IContainer? Container { get; set; }

	/// <inheritdoc/>
	public Color? ForegroundColor
	{
		get => _foregroundColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _foregroundColorValue, value, Container);
	}

	/// <inheritdoc/>
	public int? Height
	{
		get => _height;
		set => PropertySetterHelper.SetDimensionProperty(ref _height, value, Container);
	}

	/// <inheritdoc/>
	public HorizontalAlignment HorizontalAlignment
	{
		get => _horizontalAlignment;
		set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
	}

	/// <inheritdoc/>
	public Margin Margin
	{
		get => _margin;
		set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
	}

	/// <inheritdoc/>
	public string? Name { get; set; }

	/// <inheritdoc/>
	public StickyPosition StickyPosition
	{
		get => _stickyPosition;
		set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
	}

	/// <inheritdoc/>
	public object? Tag { get; set; }

	/// <inheritdoc/>
	public VerticalAlignment VerticalAlignment
	{
		get => _verticalAlignment;
		set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
	}

	/// <inheritdoc/>
	public bool Visible
	{
		get => _visible;
		set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
	}

	/// <inheritdoc/>
	public int? Width
	{
		get => _width;
		set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
	}

	#endregion

	#region Table Properties

	/// <summary>
	/// Gets the read-only list of columns.
	/// </summary>
	public IReadOnlyList<TableColumn> Columns => _columns;

	/// <summary>
	/// Gets the read-only list of rows.
	/// </summary>
	public IReadOnlyList<TableRow> Rows => _rows;

	/// <summary>
	/// Gets the number of rows in the table.
	/// </summary>
	public int RowCount => _rows.Count;

	/// <summary>
	/// Gets the number of columns in the table.
	/// </summary>
	public int ColumnCount => _columns.Count;

	/// <summary>
	/// Gets or sets the border style.
	/// </summary>
	public BorderStyle BorderStyle
	{
		get => _borderStyle;
		set => PropertySetterHelper.SetProperty(ref _borderStyle, value, Container);
	}

	/// <summary>
	/// Gets or sets the border color. Null falls back to theme.
	/// </summary>
	public Color? BorderColor
	{
		get => _borderColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _borderColorValue, value, Container);
	}

	/// <summary>
	/// Gets or sets the header background color. Null falls back to theme.
	/// </summary>
	public Color? HeaderBackgroundColor
	{
		get => _headerBackgroundColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _headerBackgroundColorValue, value, Container);
	}

	/// <summary>
	/// Gets or sets the header foreground color. Null falls back to theme.
	/// </summary>
	public Color? HeaderForegroundColor
	{
		get => _headerForegroundColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _headerForegroundColorValue, value, Container);
	}

	/// <summary>
	/// Gets or sets whether to show the header row.
	/// </summary>
	public bool ShowHeader
	{
		get => _showHeader;
		set => PropertySetterHelper.SetBoolProperty(ref _showHeader, value, Container);
	}

	/// <summary>
	/// Gets or sets whether to show row separators.
	/// </summary>
	public bool ShowRowSeparators
	{
		get => _showRowSeparators;
		set => PropertySetterHelper.SetBoolProperty(ref _showRowSeparators, value, Container);
	}

	/// <summary>
	/// Gets or sets whether to use safe border characters.
	/// </summary>
	public bool UseSafeBorder
	{
		get => _useSafeBorder;
		set => PropertySetterHelper.SetBoolProperty(ref _useSafeBorder, value, Container);
	}

	/// <summary>
	/// Gets or sets the table title.
	/// </summary>
	public string? Title
	{
		get => _title;
		set => PropertySetterHelper.SetProperty(ref _title, value, Container);
	}

	/// <summary>
	/// Gets or sets the title alignment.
	/// </summary>
	public Justify TitleAlignment
	{
		get => _titleAlignment;
		set => PropertySetterHelper.SetEnumProperty(ref _titleAlignment, value, Container);
	}

	#endregion

	#region IMouseAwareControl Properties

	/// <inheritdoc/>
	public bool WantsMouseEvents
	{
		get => _wantsMouseEvents;
		set => PropertySetterHelper.SetBoolProperty(ref _wantsMouseEvents, value, Container);
	}

	/// <inheritdoc/>
	public bool CanFocusWithMouse { get; set; } = false;

	#endregion

	#region IMouseAwareControl Events

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseClick;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseDoubleClick;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseEnter;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseLeave;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseMove;

	#endregion

	#region Public Methods - Column Management

	/// <summary>
	/// Adds a column with the specified header.
	/// </summary>
	public void AddColumn(string header, Justify alignment = Justify.Left, int? width = null)
	{
		_columns.Add(new TableColumn(header, alignment, width));
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	public void AddColumn(TableColumn column)
	{
		_columns.Add(column);
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Removes the column at the specified index.
	/// </summary>
	public void RemoveColumn(int index)
	{
		if (index >= 0 && index < _columns.Count)
		{
			_columns.RemoveAt(index);
			_measurementCache.InvalidateCache();
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Clears all columns.
	/// </summary>
	public void ClearColumns()
	{
		_columns.Clear();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Sets the width of a column.
	/// </summary>
	public void SetColumnWidth(int index, int? width)
	{
		if (index >= 0 && index < _columns.Count)
		{
			_columns[index].Width = width;
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Sets the alignment of a column.
	/// </summary>
	public void SetColumnAlignment(int index, Justify alignment)
	{
		if (index >= 0 && index < _columns.Count)
		{
			_columns[index].Alignment = alignment;
			Container?.Invalidate(true);
		}
	}

	#endregion

	#region Public Methods - Row Management

	/// <summary>
	/// Adds a row with the specified cells.
	/// </summary>
	public void AddRow(params string[] cells)
	{
		_rows.Add(new TableRow(cells));
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		_rows.Add(row);
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public void AddRows(IEnumerable<TableRow> rows)
	{
		_rows.AddRange(rows);
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Removes the row at the specified index.
	/// </summary>
	public void RemoveRow(int index)
	{
		if (index >= 0 && index < _rows.Count)
		{
			_rows.RemoveAt(index);
			_measurementCache.InvalidateCache();
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Clears all rows.
	/// </summary>
	public void ClearRows()
	{
		_rows.Clear();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Updates a cell value.
	/// </summary>
	public void UpdateCell(int row, int column, string value)
	{
		if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
		{
			_rows[row].Cells[column] = value;
			_measurementCache.InvalidateCachedEntry(value);
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets a cell value.
	/// </summary>
	public string GetCell(int row, int column)
	{
		if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
			return _rows[row].Cells[column];
		return string.Empty;
	}

	/// <summary>
	/// Gets a row.
	/// </summary>
	public TableRow GetRow(int index)
	{
		if (index >= 0 && index < _rows.Count)
			return _rows[index];
		throw new ArgumentOutOfRangeException(nameof(index));
	}

	/// <summary>
	/// Sets all rows at once.
	/// </summary>
	public void SetData(IEnumerable<TableRow> rows)
	{
		_rows = new List<TableRow>(rows);
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	#endregion

	#region Public Methods - Selection

	#endregion

	#region Color Resolution Methods

	private Color ResolveBackgroundColor(Color defaultBg)
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return _backgroundColorValue
			?? theme?.TableBackgroundColor
			?? Container?.BackgroundColor
			?? defaultBg;
	}

	private Color ResolveForegroundColor(Color defaultFg)
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return _foregroundColorValue
			?? theme?.TableForegroundColor
			?? Container?.ForegroundColor
			?? defaultFg;
	}

	private Color ResolveHeaderBackgroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return _headerBackgroundColorValue
			?? theme?.TableHeaderBackgroundColor
			?? theme?.TableBackgroundColor
			?? Container?.BackgroundColor
			?? Color.Black;
	}

	private Color ResolveHeaderForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return _headerForegroundColorValue
			?? theme?.TableHeaderForegroundColor
			?? theme?.TableForegroundColor
			?? Color.White;
	}

	private Color ResolveBorderColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return _borderColorValue
			?? theme?.TableBorderColor
			?? theme?.ActiveBorderForegroundColor
			?? Color.White;
	}

	private Color GetRowBackgroundColor(int rowIndex)
	{
		var row = _rows[rowIndex];
		return row.BackgroundColor ?? ResolveBackgroundColor(Color.Black);
	}

	private Color GetRowForegroundColor(int rowIndex)
	{
		var row = _rows[rowIndex];
		return row.ForegroundColor ?? ResolveForegroundColor(Color.White);
	}

	#endregion

	#region Rendering Methods

	/// <summary>
	/// Creates a Spectre.Console Table with theme-aware styling.
	/// </summary>
	private Spectre.Console.Table CreateSpectreTable(int targetWidth)
	{
		var table = new Spectre.Console.Table();

		// Border style with theme-aware color
		table.Border = ConvertBorderStyle(_borderStyle);
		Color borderColor = ResolveBorderColor();
		table.BorderStyle = new Style(foreground: borderColor);

		if (_useSafeBorder)
			table.UseSafeBorder = true;

		// Title with header colors
		if (!string.IsNullOrEmpty(_title))
		{
			Color headerBg = ResolveHeaderBackgroundColor();
			Color headerFg = ResolveHeaderForegroundColor();

			var titleStyle = new Style(headerFg, headerBg);
			table.Title = new TableTitle(_title, titleStyle);
		}

		if (!_showHeader)
			table.HideHeaders();

		if (_showRowSeparators)
			table.ShowRowSeparators();

		// Add columns with header colors
		Color colHeaderBg = ResolveHeaderBackgroundColor();
		Color colHeaderFg = ResolveHeaderForegroundColor();

		foreach (var col in _columns)
		{
			var colStyle = new Style(colHeaderFg, colHeaderBg);
			var tableCol = new Spectre.Console.TableColumn(col.Header);

			if (col.Width.HasValue)
				tableCol.Width = col.Width.Value;
			tableCol.Alignment = col.Alignment;
			if (col.NoWrap)
				tableCol.NoWrap = true;

			table.AddColumn(tableCol);
		}

		// Add rows with theme-aware colors
		for (int i = 0; i < _rows.Count; i++)
		{
			var row = _rows[i];
			Color rowBg = GetRowBackgroundColor(i);
			Color rowFg = GetRowForegroundColor(i);

			var styledCells = row.Cells.Select(cell =>
				new Markup(cell, new Style(rowFg, rowBg))
			).ToArray();

			table.AddRow(styledCells);
		}

		return table;
	}

	/// <summary>
	/// Converts BorderStyle to Spectre.Console TableBorder.
	/// </summary>
	private TableBorder ConvertBorderStyle(BorderStyle style)
	{
		return style switch
		{
			BorderStyle.None => TableBorder.None,
			BorderStyle.Single => TableBorder.Square,
			BorderStyle.DoubleLine => TableBorder.Double,
			BorderStyle.Rounded => TableBorder.Rounded,
			_ => TableBorder.Square
		};
	}

	#endregion

	#region IDOMPaintable Implementation

	/// <inheritdoc/>
	public LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		int availableWidth = constraints.MaxWidth - _margin.Left - _margin.Right;
		var table = CreateSpectreTable(availableWidth);

		var lines = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(
			table, availableWidth, null, ResolveBackgroundColor(Color.Black));

		int width = lines.Count > 0
			? lines.Max(l => AnsiConsoleHelper.StripAnsiStringLength(l))
			: 0;
		int height = lines.Count;

		width += _margin.Left + _margin.Right;
		height += _margin.Top + _margin.Bottom;

		return new LayoutSize(
			Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
		);
	}

	/// <inheritdoc/>
	public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		_actualX = bounds.X;
		_actualY = bounds.Y;
		_actualWidth = bounds.Width;
		_actualHeight = bounds.Height;

		Color bgColor = ResolveBackgroundColor(defaultBg);
		Color fgColor = ResolveForegroundColor(defaultFg);

		// Fill margins
		ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, bounds.Y + _margin.Top, fgColor, bgColor);

		int targetWidth = bounds.Width - _margin.Left - _margin.Right;
		var table = CreateSpectreTable(targetWidth);

		var lines = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(table, targetWidth, null, bgColor);

		int startX = bounds.X + _margin.Left;
		int startY = bounds.Y + _margin.Top;

		for (int i = 0; i < lines.Count; i++)
		{
			int y = startY + i;
			if (y < clipRect.Y || y >= clipRect.Bottom) continue;

			var cells = AnsiParser.Parse(lines[i], fgColor, bgColor);
			buffer.WriteCellsClipped(startX, y, cells, clipRect);
		}

		ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - _margin.Bottom, fgColor, bgColor);
	}

	#endregion

	#region IMouseAwareControl Implementation

	/// <inheritdoc/>
	public bool ProcessMouseEvent(MouseEventArgs args)
	{
		// Handle mouse events - don't bubble (except scroll)
		if (args.HasFlag(MouseFlags.MouseEnter))
		{
			MouseEnter?.Invoke(this, args);
			return true;
		}

		if (args.HasFlag(MouseFlags.MouseLeave))
		{
			MouseLeave?.Invoke(this, args);
			return true;
		}

		if (args.HasFlag(MouseFlags.ReportMousePosition))
		{
			MouseMove?.Invoke(this, args);
			return true;
		}

		if (args.HasFlag(MouseFlags.Button1Clicked))
		{
			MouseClick?.Invoke(this, args);
			return true;
		}

		if (args.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			MouseDoubleClick?.Invoke(this, args);
			return true;
		}

		// Let scroll events bubble to scrollable containers
		if (args.HasFlag(MouseFlags.WheeledUp) || args.HasFlag(MouseFlags.WheeledDown))
		{
			return false;
		}

		return false;
	}

	#endregion

	#region IWindowControl Additional Methods

	/// <inheritdoc/>
	public System.Drawing.Size GetLogicalContentSize()
	{
		int maxWidth = _width ?? LayoutDefaults.DefaultUnboundedMeasureWidth;
		var constraints = new LayoutConstraints(0, maxWidth, 0, int.MaxValue);
		var size = MeasureDOM(constraints);
		return new System.Drawing.Size(size.Width, size.Height);
	}

	/// <inheritdoc/>
	public void Invalidate()
	{
		Container?.Invalidate(true);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// No resources to dispose
	}

	#endregion

	#region Static Factory

	/// <summary>
	/// Creates a new TableControlBuilder for fluent configuration.
	/// </summary>
	public static Builders.TableControlBuilder Create() => new Builders.TableControlBuilder();

	#endregion
}
