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
public class TableControl : BaseControl, IMouseAwareControl
{
	#region Fields

	private Color? _backgroundColorValue = Color.Default;
	private Color? _foregroundColorValue = Color.Default;
	private int? _height;

	// Table-specific fields
	private List<TableColumn> _columns = new();
	private List<TableRow> _rows = new();
	private readonly object _tableLock = new();
	private BorderStyle _borderStyle = BorderStyle.Single;
	private Color? _borderColorValue;
	private Color? _headerBackgroundColorValue = Color.Default;
	private Color? _headerForegroundColorValue = Color.Default;
	private bool _showHeader = true;
	private bool _showRowSeparators = false;
	private bool _useSafeBorder = false;
	private string? _title;
	private Justify _titleAlignment = Justify.Center;

	// Performance caches
	private readonly TextMeasurementCache _measurementCache;

	// Mouse support (minimal - for bubbling only)
	private bool _wantsMouseEvents = true;

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

	#region Properties

	/// <inheritdoc/>
	public override int? ContentWidth => Width;

	/// <summary>
	/// Gets the content height.
	/// </summary>
	public int? ContentHeight => _height;

	/// <inheritdoc/>
	public Color? BackgroundColor
	{
		get => _backgroundColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _backgroundColorValue, value, Container);
	}

	/// <inheritdoc/>
	public Color? ForegroundColor
	{
		get => _foregroundColorValue;
		set => PropertySetterHelper.SetColorProperty(ref _foregroundColorValue, value, Container);
	}

	/// <summary>
	/// Gets or sets the explicit height.
	/// </summary>
	public int? Height
	{
		get => _height;
		set => PropertySetterHelper.SetDimensionProperty(ref _height, value, Container);
	}

	#endregion

	#region Table Properties

	/// <summary>
	/// Gets the read-only list of columns.
	/// </summary>
	public IReadOnlyList<TableColumn> Columns { get { lock (_tableLock) { return _columns.ToList().AsReadOnly(); } } }

	/// <summary>
	/// Gets the read-only list of rows.
	/// </summary>
	public IReadOnlyList<TableRow> Rows { get { lock (_tableLock) { return _rows.ToList().AsReadOnly(); } } }

	/// <summary>
	/// Gets the number of rows in the table.
	/// </summary>
	public int RowCount { get { lock (_tableLock) { return _rows.Count; } } }

	/// <summary>
	/// Gets the number of columns in the table.
	/// </summary>
	public int ColumnCount { get { lock (_tableLock) { return _columns.Count; } } }

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
	public event EventHandler<MouseEventArgs>? MouseRightClick;

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
		lock (_tableLock) { _columns.Add(new TableColumn(header, alignment, width)); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	public void AddColumn(TableColumn column)
	{
		lock (_tableLock) { _columns.Add(column); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Removes the column at the specified index.
	/// </summary>
	public void RemoveColumn(int index)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _columns.Count)
				_columns.RemoveAt(index);
			else
				return;
		}
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Clears all columns.
	/// </summary>
	public void ClearColumns()
	{
		lock (_tableLock) { _columns.Clear(); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Sets the width of a column.
	/// </summary>
	public void SetColumnWidth(int index, int? width)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _columns.Count)
				_columns[index].Width = width;
			else
				return;
		}
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Sets the alignment of a column.
	/// </summary>
	public void SetColumnAlignment(int index, Justify alignment)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _columns.Count)
				_columns[index].Alignment = alignment;
			else
				return;
		}
		Container?.Invalidate(true);
	}

	#endregion

	#region Public Methods - Row Management

	/// <summary>
	/// Adds a row with the specified cells.
	/// </summary>
	public void AddRow(params string[] cells)
	{
		lock (_tableLock) { _rows.Add(new TableRow(cells)); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		lock (_tableLock) { _rows.Add(row); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public void AddRows(IEnumerable<TableRow> rows)
	{
		lock (_tableLock) { _rows.AddRange(rows); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Removes the row at the specified index.
	/// </summary>
	public void RemoveRow(int index)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _rows.Count)
				_rows.RemoveAt(index);
			else
				return;
		}
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Clears all rows.
	/// </summary>
	public void ClearRows()
	{
		lock (_tableLock) { _rows.Clear(); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Updates a cell value.
	/// </summary>
	public void UpdateCell(int row, int column, string value)
	{
		lock (_tableLock)
		{
			if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
				_rows[row].Cells[column] = value;
			else
				return;
		}
		_measurementCache.InvalidateCachedEntry(value);
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Gets a cell value.
	/// </summary>
	public string GetCell(int row, int column)
	{
		lock (_tableLock)
		{
			if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
				return _rows[row].Cells[column];
			return string.Empty;
		}
	}

	/// <summary>
	/// Gets a row.
	/// </summary>
	public TableRow GetRow(int index)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _rows.Count)
				return _rows[index];
			throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	/// <summary>
	/// Sets all rows at once.
	/// </summary>
	public void SetData(IEnumerable<TableRow> rows)
	{
		lock (_tableLock) { _rows = new List<TableRow>(rows); }
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	#endregion

	#region Public Methods - Selection

	#endregion

	#region Color Resolution Methods

	/// <summary>
	/// Three-state resolution: null = inherit from container,
	/// Color.Default = use theme, explicit color = use as-is.
	/// </summary>
	private Color ResolveBackgroundColor(Color defaultBg)
	{
		if (_backgroundColorValue == null)
			return Container?.BackgroundColor ?? defaultBg;
		if (_backgroundColorValue.Value == Color.Default)
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			return theme?.TableBackgroundColor
				?? Container?.BackgroundColor
				?? defaultBg;
		}
		return _backgroundColorValue.Value;
	}

	private Color ResolveForegroundColor(Color defaultFg)
	{
		if (_foregroundColorValue == null)
			return Container?.ForegroundColor ?? defaultFg;
		if (_foregroundColorValue.Value == Color.Default)
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			return theme?.TableForegroundColor
				?? Container?.ForegroundColor
				?? defaultFg;
		}
		return _foregroundColorValue.Value;
	}

	private Color ResolveHeaderBackgroundColor()
	{
		if (_headerBackgroundColorValue == null)
			return Container?.BackgroundColor ?? Color.Black;
		if (_headerBackgroundColorValue.Value == Color.Default)
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			return theme?.TableHeaderBackgroundColor
				?? theme?.TableBackgroundColor
				?? Container?.BackgroundColor
				?? Color.Black;
		}
		return _headerBackgroundColorValue.Value;
	}

	private Color ResolveHeaderForegroundColor()
	{
		if (_headerForegroundColorValue == null)
			return Container?.ForegroundColor ?? Color.White;
		if (_headerForegroundColorValue.Value == Color.Default)
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			return theme?.TableHeaderForegroundColor
				?? theme?.TableForegroundColor
				?? Container?.ForegroundColor
				?? Color.White;
		}
		return _headerForegroundColorValue.Value;
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
	private Spectre.Console.Table CreateSpectreTable()
	{
		List<TableColumn> colSnapshot;
		List<TableRow> rowSnapshot;
		lock (_tableLock)
		{
			colSnapshot = _columns.ToList();
			rowSnapshot = _rows.ToList();
		}

		var table = new Spectre.Console.Table();

		// Set Expand when HorizontalAlignment is Stretch
		// Spectre handles the actual stretching when rendering
		if (HorizontalAlignment == HorizontalAlignment.Stretch)
		{
			table.Expand = true;
		}

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

		foreach (var col in colSnapshot)
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
		for (int i = 0; i < rowSnapshot.Count; i++)
		{
			var row = rowSnapshot[i];
			Color rowBg = row.BackgroundColor ?? ResolveBackgroundColor(Color.Black);
			Color rowFg = row.ForegroundColor ?? ResolveForegroundColor(Color.White);

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
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		// Use explicit width if set, otherwise use available width
		int targetWidth = Width ?? constraints.MaxWidth;
		int contentWidth = targetWidth - Margin.Left - Margin.Right;

		var table = CreateSpectreTable();

		// Pass the content width to Spectre for rendering
		// Spectre's Expand property (if set) will make it stretch to this width
		var lines = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(
			table, contentWidth, null, ResolveBackgroundColor(Color.Black));

		// Measure actual rendered dimensions
		int measuredWidth = lines.Count > 0
			? lines.Max(l => AnsiConsoleHelper.StripAnsiStringLength(l))
			: 0;
		int height = lines.Count;

		// Calculate final width
		int width;
		if (Width.HasValue)
		{
			// Explicit width: return width + margins (margins are additional)
			width = Width.Value + Margin.Left + Margin.Right;
		}
		else if (HorizontalAlignment == HorizontalAlignment.Stretch)
		{
			// Stretch: request full available width
			width = constraints.MaxWidth;
		}
		else
		{
			// Natural sizing: return measured width + margins
			width = measuredWidth + Margin.Left + Margin.Right;
		}

		height += Margin.Top + Margin.Bottom;

		return new LayoutSize(
			Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
		);
	}

	/// <inheritdoc/>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		SetActualBounds(bounds);

		Color bgColor = ResolveBackgroundColor(defaultBg);
		Color fgColor = ResolveForegroundColor(defaultFg);

		// Fill margins
		ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, bounds.Y + Margin.Top, fgColor, bgColor);

		int targetWidth = bounds.Width - Margin.Left - Margin.Right;
		var table = CreateSpectreTable();

		// Pass the allocated width to Spectre - it will handle stretching via Expand property
		var lines = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(table, targetWidth, null, bgColor);

		int startX = bounds.X + Margin.Left;
		int startY = bounds.Y + Margin.Top;

		for (int i = 0; i < lines.Count; i++)
		{
			int y = startY + i;
			if (y < clipRect.Y || y >= clipRect.Bottom) continue;

			var cells = AnsiParser.Parse(lines[i], fgColor, bgColor);
			buffer.WriteCellsClipped(startX, y, cells, clipRect);
		}

		ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, bgColor);
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

		if (args.HasFlag(MouseFlags.Button3Clicked))
		{
			MouseRightClick?.Invoke(this, args);
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

	#region Overrides

	/// <inheritdoc/>
	public override System.Drawing.Size GetLogicalContentSize()
	{
		int maxWidth = Width ?? LayoutDefaults.DefaultUnboundedMeasureWidth;
		var constraints = new LayoutConstraints(0, maxWidth, 0, int.MaxValue);
		var size = MeasureDOM(constraints);
		return new System.Drawing.Size(size.Width, size.Height);
	}

	#endregion

	#region Static Factory

	/// <summary>
	/// Creates a new TableControlBuilder for fluent configuration.
	/// </summary>
	public static Builders.TableControlBuilder Create() => new Builders.TableControlBuilder();

	#endregion
}
