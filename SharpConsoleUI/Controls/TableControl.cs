// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A table control that renders tabular data directly to CharacterBuffer.
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
	private TextJustification _titleAlignment = TextJustification.Center;

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
		_measurementCache = new TextMeasurementCache(MarkupParser.StripLength);
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
		set { PropertySetterHelper.SetProperty(ref _borderStyle, value, Container); InvalidateColumnWidths(); }
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
	public TextJustification TitleAlignment
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
	public void AddColumn(string header, TextJustification alignment = TextJustification.Left, int? width = null)
	{
		lock (_tableLock) { _columns.Add(new TableColumn(header, alignment, width)); }
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	public void AddColumn(TableColumn column)
	{
		lock (_tableLock) { _columns.Add(column); }
		InvalidateColumnWidths();
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
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Clears all columns.
	/// </summary>
	public void ClearColumns()
	{
		lock (_tableLock) { _columns.Clear(); }
		InvalidateColumnWidths();
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
		InvalidateColumnWidths();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Sets the alignment of a column.
	/// </summary>
	public void SetColumnAlignment(int index, TextJustification alignment)
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
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		lock (_tableLock) { _rows.Add(row); }
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public void AddRows(IEnumerable<TableRow> rows)
	{
		lock (_tableLock) { _rows.AddRange(rows); }
		InvalidateColumnWidths();
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
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Clears all rows.
	/// </summary>
	public void ClearRows()
	{
		lock (_tableLock) { _rows.Clear(); }
		InvalidateColumnWidths();
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
		InvalidateColumnWidths();
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
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

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

	#endregion

	#region Column Width Calculation

	private void InvalidateColumnWidths()
	{
		_measurementCache.InvalidateCache();
	}

	/// <summary>
	/// Computes column widths for the given total available width.
	/// </summary>
	private int[] ComputeColumnWidths(int availableWidth, List<TableColumn> cols, List<TableRow> rows)
	{
		int colCount = cols.Count;
		if (colCount == 0) return Array.Empty<int>();

		bool hasBorder = _borderStyle != BorderStyle.None;
		// Border overhead: left border + separators between columns + right border
		int borderOverhead = hasBorder ? (colCount + 1) : 0;
		int contentWidth = availableWidth - borderOverhead;
		if (contentWidth < colCount) contentWidth = colCount; // minimum 1 char per column

		var widths = new int[colCount];
		int fixedTotal = 0;
		int autoCount = 0;

		// First pass: fixed-width columns and measure auto-width columns
		for (int c = 0; c < colCount; c++)
		{
			if (cols[c].Width.HasValue)
			{
				widths[c] = cols[c].Width!.Value;
				fixedTotal += widths[c];
			}
			else
			{
				// Measure max content width
				int maxW = _measurementCache.GetCachedLength(cols[c].Header);
				foreach (var row in rows)
				{
					if (c < row.Cells.Count)
					{
						int cellW = _measurementCache.GetCachedLength(row.Cells[c]);
						if (cellW > maxW) maxW = cellW;
					}
				}
				widths[c] = maxW;
				autoCount++;
			}
		}

		// If HorizontalAlignment is Stretch, distribute remaining space
		int totalNatural = 0;
		for (int c = 0; c < colCount; c++) totalNatural += widths[c];

		if (HorizontalAlignment == HorizontalAlignment.Stretch && totalNatural < contentWidth)
		{
			int remaining = contentWidth - totalNatural;
			// Distribute proportionally among auto columns, or all columns if no auto
			int distributeCount = autoCount > 0 ? autoCount : colCount;
			int perCol = remaining / distributeCount;
			int extraCols = remaining % distributeCount;

			for (int c = 0; c < colCount; c++)
			{
				bool isAutoCol = !cols[c].Width.HasValue;
				if (autoCount > 0 && !isAutoCol) continue;

				widths[c] += perCol;
				if (extraCols > 0) { widths[c]++; extraCols--; }
			}
		}
		else if (totalNatural > contentWidth)
		{
			// Shrink columns proportionally to fit
			double ratio = (double)contentWidth / totalNatural;
			int assigned = 0;
			for (int c = 0; c < colCount - 1; c++)
			{
				widths[c] = Math.Max(1, (int)(widths[c] * ratio));
				assigned += widths[c];
			}
			widths[colCount - 1] = Math.Max(1, contentWidth - assigned);
		}

		return widths;
	}

	#endregion

	#region Direct Rendering Helpers

	private BoxChars GetBoxChars()
	{
		if (_useSafeBorder) return BoxChars.Ascii;
		return BoxChars.FromBorderStyle(_borderStyle);
	}

	/// <summary>
	/// Draws a horizontal border line (top, header separator, row separator, or bottom).
	/// </summary>
	private void DrawHorizontalLine(CharacterBuffer buffer, int x, int y, int[] colWidths, LayoutRect clipRect,
		BoxChars box, Color borderColor, Color bgColor, char left, char middle, char right, char fill)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		int writeX = x;

		// Left border char
		if (writeX >= clipRect.X && writeX < clipRect.Right)
			buffer.SetCell(writeX, y, left, borderColor, bgColor);
		writeX++;

		for (int c = 0; c < colWidths.Length; c++)
		{
			// Fill column width with fill char
			for (int i = 0; i < colWidths[c]; i++)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right)
					buffer.SetCell(writeX, y, fill, borderColor, bgColor);
				writeX++;
			}

			// Column separator (middle) or right border
			if (c < colWidths.Length - 1)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right)
					buffer.SetCell(writeX, y, middle, borderColor, bgColor);
				writeX++;
			}
		}

		// Right border char
		if (writeX >= clipRect.X && writeX < clipRect.Right)
			buffer.SetCell(writeX, y, right, borderColor, bgColor);
	}

	/// <summary>
	/// Draws a data row with vertical borders and aligned cell text.
	/// </summary>
	private void DrawDataRow(CharacterBuffer buffer, int x, int y, int[] colWidths, LayoutRect clipRect,
		BoxChars box, Color borderColor, Color borderBg, List<string> cells, List<TableColumn> cols,
		Color rowFg, Color rowBg, bool hasBorder)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		int writeX = x;

		if (hasBorder)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
				buffer.SetCell(writeX, y, box.Vertical, borderColor, borderBg);
			writeX++;
		}

		for (int c = 0; c < colWidths.Length; c++)
		{
			int colW = colWidths[c];
			string cellText = c < cells.Count ? cells[c] : string.Empty;
			TextJustification align = c < cols.Count ? cols[c].Alignment : TextJustification.Left;

			// Parse markup and get visible length
			var cellCells = MarkupParser.Parse(cellText, rowFg, rowBg);
			int visLen = cellCells.Count;

			// Truncate if needed
			if (visLen > colW)
			{
				cellCells = cellCells.GetRange(0, colW);
				visLen = colW;
			}

			// Calculate alignment offset
			int padLeft = 0;
			int padRight = colW - visLen;
			if (align == TextJustification.Center)
			{
				padLeft = (colW - visLen) / 2;
				padRight = colW - visLen - padLeft;
			}
			else if (align == TextJustification.Right)
			{
				padLeft = colW - visLen;
				padRight = 0;
			}

			// Left padding
			for (int i = 0; i < padLeft; i++)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right)
					buffer.SetCell(writeX, y, ' ', rowFg, rowBg);
				writeX++;
			}

			// Cell content
			foreach (var cell in cellCells)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right)
					buffer.SetCell(writeX, y, cell.Character, cell.Foreground, cell.Background);
				writeX++;
			}

			// Right padding
			for (int i = 0; i < padRight; i++)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right)
					buffer.SetCell(writeX, y, ' ', rowFg, rowBg);
				writeX++;
			}

			// Column separator
			if (hasBorder)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right)
					buffer.SetCell(writeX, y, box.Vertical, borderColor, borderBg);
				writeX++;
			}
		}
	}

	/// <summary>
	/// Draws a title row centered above the table.
	/// </summary>
	private void DrawTitleRow(CharacterBuffer buffer, int x, int y, int totalWidth, LayoutRect clipRect,
		Color fgColor, Color bgColor)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom || string.IsNullOrEmpty(_title)) return;

		var titleCells = MarkupParser.Parse(_title, fgColor, bgColor);
		int titleLen = titleCells.Count;

		// Fill the row with spaces first
		for (int i = 0; i < totalWidth; i++)
		{
			int px = x + i;
			if (px >= clipRect.X && px < clipRect.Right)
				buffer.SetCell(px, y, ' ', fgColor, bgColor);
		}

		// Calculate title offset
		int offset = 0;
		switch (_titleAlignment)
		{
			case TextJustification.Center:
				offset = Math.Max(0, (totalWidth - titleLen) / 2);
				break;
			case TextJustification.Right:
				offset = Math.Max(0, totalWidth - titleLen);
				break;
		}

		// Write title cells
		for (int i = 0; i < titleLen && offset + i < totalWidth; i++)
		{
			int px = x + offset + i;
			if (px >= clipRect.X && px < clipRect.Right)
				buffer.SetCell(px, y, titleCells[i].Character, titleCells[i].Foreground, titleCells[i].Background);
		}
	}

	#endregion

	#region IDOMPaintable Implementation

	/// <inheritdoc/>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		List<TableColumn> colSnapshot;
		List<TableRow> rowSnapshot;
		lock (_tableLock)
		{
			colSnapshot = _columns.ToList();
			rowSnapshot = _rows.ToList();
		}

		int targetWidth = Width ?? constraints.MaxWidth;
		int contentWidth = targetWidth - Margin.Left - Margin.Right;

		int[] colWidths = ComputeColumnWidths(contentWidth, colSnapshot, rowSnapshot);

		bool hasBorder = _borderStyle != BorderStyle.None;
		int borderOverhead = hasBorder ? (colSnapshot.Count + 1) : 0;
		int measuredWidth = 0;
		foreach (int w in colWidths) measuredWidth += w;
		measuredWidth += borderOverhead;

		// Ensure title fits
		if (!string.IsNullOrEmpty(_title))
		{
			int titleWidth = _measurementCache.GetCachedLength(_title);
			if (titleWidth > measuredWidth)
				measuredWidth = titleWidth;
		}

		// Calculate height
		int height = 0;
		if (!string.IsNullOrEmpty(_title)) height++; // title row
		if (hasBorder) height++; // top border
		if (_showHeader) height++; // header row
		if (_showHeader && hasBorder) height++; // header separator
		height += rowSnapshot.Count; // data rows
		if (_showRowSeparators && hasBorder && rowSnapshot.Count > 1)
			height += rowSnapshot.Count - 1; // row separators
		if (hasBorder) height++; // bottom border

		// Calculate final width
		int width;
		if (Width.HasValue)
			width = Width.Value + Margin.Left + Margin.Right;
		else if (HorizontalAlignment == HorizontalAlignment.Stretch)
			width = constraints.MaxWidth;
		else
			width = measuredWidth + Margin.Left + Margin.Right;

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
		Color borderColor = ResolveBorderColor();
		Color headerBg = ResolveHeaderBackgroundColor();
		Color headerFg = ResolveHeaderForegroundColor();
		bool preserveBg = Container?.HasGradientBackground ?? false;

		// Fill margins
		ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, bounds.Y + Margin.Top, fgColor, bgColor, preserveBg);

		List<TableColumn> colSnapshot;
		List<TableRow> rowSnapshot;
		lock (_tableLock)
		{
			colSnapshot = _columns.ToList();
			rowSnapshot = _rows.ToList();
		}

		int targetWidth = bounds.Width - Margin.Left - Margin.Right;
		if (targetWidth <= 0 || colSnapshot.Count == 0)
		{
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, bgColor, preserveBg);
			return;
		}

		int[] colWidths = ComputeColumnWidths(targetWidth, colSnapshot, rowSnapshot);

		int startX = bounds.X + Margin.Left;
		int currentY = bounds.Y + Margin.Top;
		int maxY = bounds.Bottom - Margin.Bottom;

		bool hasBorder = _borderStyle != BorderStyle.None;
		var box = GetBoxChars();

		// Fill left/right margins helper
		void FillSideMargins(int y)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;
			if (Margin.Left > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, bgColor, preserveBg);
			if (Margin.Right > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, bgColor, preserveBg);
		}

		// Title row
		if (!string.IsNullOrEmpty(_title) && currentY < maxY)
		{
			FillSideMargins(currentY);
			DrawTitleRow(buffer, startX, currentY, targetWidth, clipRect, headerFg, bgColor);
			currentY++;
		}

		// Top border
		if (hasBorder && currentY < maxY)
		{
			FillSideMargins(currentY);
			DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				box.TopLeft, box.TopTee, box.TopRight, box.Horizontal);
			currentY++;
		}

		// Header row
		if (_showHeader && currentY < maxY)
		{
			FillSideMargins(currentY);
			var headerCells = colSnapshot.Select(c => c.Header).ToList();
			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				headerCells, colSnapshot, headerFg, headerBg, hasBorder);

			// Update column rendered positions for hit testing
			int colX = startX + (hasBorder ? 1 : 0);
			for (int c = 0; c < colSnapshot.Count; c++)
			{
				colSnapshot[c].RenderedX = colX;
				colSnapshot[c].RenderedWidth = colWidths[c];
				colX += colWidths[c] + (hasBorder ? 1 : 0);
			}

			currentY++;

			// Header separator
			if (hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal);
				currentY++;
			}
		}

		// Data rows
		for (int r = 0; r < rowSnapshot.Count && currentY < maxY; r++)
		{
			// Row separator (between rows, not before first)
			if (r > 0 && _showRowSeparators && hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal);
				currentY++;
			}

			if (currentY >= maxY) break;

			var row = rowSnapshot[r];
			Color rowBg = row.BackgroundColor ?? bgColor;
			Color rowFg = row.ForegroundColor ?? fgColor;

			FillSideMargins(currentY);
			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				row.Cells, colSnapshot, rowFg, rowBg, hasBorder);

			// Update row rendered position for hit testing
			row.RenderedY = currentY;
			row.RenderedHeight = 1;

			currentY++;
		}

		// Bottom border
		if (hasBorder && currentY < maxY)
		{
			FillSideMargins(currentY);
			DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				box.BottomLeft, box.BottomTee, box.BottomRight, box.Horizontal);
			currentY++;
		}

		// Fill remaining height
		while (currentY < maxY)
		{
			if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), fgColor, bgColor, preserveBg);
			currentY++;
		}

		ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, bgColor, preserveBg);
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
