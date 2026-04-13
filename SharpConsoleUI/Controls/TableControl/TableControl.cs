// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A table control that renders tabular data directly to CharacterBuffer.
/// Supports both read-only display and interactive mode with selection,
/// keyboard navigation, scrolling, sorting, multi-selection, column resizing,
/// inline editing, draggable scrollbars, and virtual data binding.
/// </summary>
public partial class TableControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
{
	#region Fields

	private Color? _backgroundColorValue = Color.Default;
	private Color? _foregroundColorValue = Color.Default;
	private int? _height;

	// Table-specific fields
	private List<TableColumn> _columns = new();
	private List<TableRow> _rows = new();
	internal readonly object _tableLock = new();
	private BorderStyle _borderStyle = BorderStyle.Single;
	private Color? _borderColorValue;
	private Color? _headerBackgroundColorValue = Color.Default;
	private Color? _headerForegroundColorValue = Color.Default;
	private bool _showHeader = true;
	private bool _showRowSeparators = false;
	private bool _useSafeBorder = false;
	private string? _title;
	private TextJustification _titleAlignment = TextJustification.Center;

	// ReadOnly mode (default true = backward compatible)
	private bool _readOnly = true;
	private bool _isEnabled = true;



	private bool _truncationFade;

	// Selection
	private int _selectedRowIndex = -1;
	private int _selectedColumnIndex = -1;
	private bool _cellNavigationEnabled = false;
	private bool _multiSelectEnabled = false;
	private bool _checkboxMode = false;
	private HashSet<int> _selectedRowIndices = new();

	// Hover
	private int _hoveredRowIndex = -1;
	private bool _hoverEnabled = true;

	// Scrolling
	private int _scrollOffset = 0;
	private int _horizontalScrollOffset = 0;
	private ScrollbarVisibility _verticalScrollbarVisibility = ScrollbarVisibility.Auto;
	private ScrollbarVisibility _horizontalScrollbarVisibility = ScrollbarVisibility.Auto;

	// Scrollbar dragging state
	private bool _isVerticalScrollbarDragging = false;
	private bool _isHorizontalScrollbarDragging = false;
	private int _scrollbarDragStartY = 0;
	private int _scrollbarDragStartX = 0;
	private int _scrollbarDragStartOffset = 0;

	// Sorting
	private bool _sortingEnabled = false;
	private int _sortColumnIndex = -1;
	private SortDirection _sortDirection = SortDirection.None;
	private int[]? _sortIndexMap; // maps display index -> data index when sorted

	// Filtering
	internal bool _filteringEnabled = false;
	internal FilterMode _filterMode = FilterMode.None;
	internal string _filterBuffer = string.Empty;
	internal int _filterCursorPosition = 0;
	internal int[]? _filterIndexMap; // maps display index -> data index when filtered (may include sort)
	internal int _unfilteredRowCount = 0;
	internal CompoundFilterExpression? _activeFilter;
	internal bool _fuzzyFilterEnabled = false;

	// Editing
	private bool _inlineEditingEnabled = false;
	private bool _isEditing = false;
	private string _editBuffer = string.Empty;
	private int _editCursorPosition = 0;

	// Column separator (when no borders)
	private char? _columnSeparator;
	private Color? _columnSeparatorColor;

	// Column resizing
	private bool _columnResizeEnabled = false;
	private bool _isResizingColumn = false;
	private int _resizingColumnIndex = -1;
	private int _resizeDragStartX = 0;
	private int _resizeDragStartWidth = 0;

	// Virtual data source
	private ITableDataSource? _dataSource;

	// Performance caches
	private readonly TextMeasurementCache _measurementCache;
	private int[]? _cachedColumnWidths;
	private int _cachedColumnWidthsForWidth = -1;
	private int _cachedColumnWidthsScrollOffset = -1;

	// Rendered column geometry (always populated during PaintDOM for hit testing)
	private int[] _renderedColumnX = Array.Empty<int>();
	private int[] _renderedColumnWidths = Array.Empty<int>();

	// Column width overrides (for resize in DataSource mode)
	private readonly Dictionary<int, int> _columnWidthOverrides = new();

	// Mouse support
	private bool _wantsMouseEvents = true;

	// Double-click detection
	private readonly object _clickLock = new();
	private DateTime _lastClickTime = DateTime.MinValue;
	private int _lastClickRowIndex = -1;
	private int _doubleClickThresholdMs = ControlDefaults.DefaultDoubleClickThresholdMs;

	// Auto-highlight on focus
	private bool _autoHighlightOnFocus = true;

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
		set => SetProperty(ref _backgroundColorValue, value);
	}

	/// <inheritdoc/>
	public Color? ForegroundColor
	{
		get => _foregroundColorValue;
		set => SetProperty(ref _foregroundColorValue, value);
	}

	/// <summary>
	/// Gets or sets the explicit height.
	/// </summary>
	public override int? Height
	{
		get => _height;
		set => SetProperty(ref _height, value, v => v.HasValue ? Math.Max(0, v.Value) : v);
	}

	/// <summary>
	/// Gets or sets whether the table is read-only.
	/// When true (default), the table behaves as a static display with no selection or interaction.
	/// When false, enables selection, keyboard navigation, scrolling, and other interactive features.
	/// </summary>
	public bool ReadOnly
	{
		get => _readOnly;
		set
		{
			if (_readOnly != value)
			{
				_readOnly = value;
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}
	}

	/// <summary>
	/// Gets or sets whether this control is enabled and can receive input.
	/// </summary>
	public bool IsEnabled
	{
		get => _isEnabled;
		set => SetProperty(ref _isEnabled, value);
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
	public int RowCount
	{
		get
		{
			if (_filterIndexMap != null) return _filterIndexMap.Length;
			if (_dataSource != null) return _dataSource.RowCount;
			lock (_tableLock) { return _rows.Count; }
		}
	}

	/// <summary>
	/// Gets the number of columns in the table.
	/// </summary>
	public int ColumnCount
	{
		get
		{
			if (_dataSource != null) return _dataSource.ColumnCount;
			lock (_tableLock) { return _columns.Count; }
		}
	}

	/// <summary>
	/// Gets or sets the border style.
	/// </summary>
	public BorderStyle BorderStyle
	{
		get => _borderStyle;
		set { if (SetProperty(ref _borderStyle, value)) InvalidateColumnWidths(); }
	}

	/// <summary>
	/// Gets or sets the border color. Null falls back to theme.
	/// </summary>
	public Color? BorderColor
	{
		get => _borderColorValue;
		set => SetProperty(ref _borderColorValue, value);
	}

	/// <summary>
	/// Gets or sets the header background color. Null falls back to theme.
	/// </summary>
	public Color? HeaderBackgroundColor
	{
		get => _headerBackgroundColorValue;
		set => SetProperty(ref _headerBackgroundColorValue, value);
	}

	/// <summary>
	/// Gets or sets the header foreground color. Null falls back to theme.
	/// </summary>
	public Color? HeaderForegroundColor
	{
		get => _headerForegroundColorValue;
		set => SetProperty(ref _headerForegroundColorValue, value);
	}

	/// <summary>
	/// Gets or sets whether to show the header row.
	/// </summary>
	public bool ShowHeader
	{
		get => _showHeader;
		set => SetProperty(ref _showHeader, value);
	}

	/// <summary>
	/// Gets or sets whether to show row separators.
	/// </summary>
	public bool ShowRowSeparators
	{
		get => _showRowSeparators;
		set => SetProperty(ref _showRowSeparators, value);
	}

	/// <summary>
	/// Gets or sets whether to use safe border characters.
	/// </summary>
	public bool UseSafeBorder
	{
		get => _useSafeBorder;
		set => SetProperty(ref _useSafeBorder, value);
	}

	/// <summary>
	/// Gets or sets whether truncated cell text fades out instead of hard-cutting.
	/// When enabled, the last 4 characters of truncated cells blend toward the background.
	/// Default: false.
	/// </summary>
	public bool TruncationFade
	{
		get => _truncationFade;
		set { _truncationFade = value; OnPropertyChanged(); Container?.Invalidate(true); }
	}

	/// <summary>
	/// Gets or sets the table title.
	/// </summary>
	public string? Title
	{
		get => _title;
		set => SetProperty(ref _title, value);
	}

	/// <summary>
	/// Gets or sets the title alignment.
	/// </summary>
	public TextJustification TitleAlignment
	{
		get => _titleAlignment;
		set => SetProperty(ref _titleAlignment, value);
	}

	/// <summary>
	/// Gets or sets the virtual data source. When set, the control ignores internal rows/columns
	/// and queries only visible rows from the source on demand.
	/// Mutually exclusive with in-memory rows.
	/// </summary>
	public ITableDataSource? DataSource
	{
		get => _dataSource;
		set
		{
			if (_dataSource != null)
				_dataSource.CollectionChanged -= OnDataSourceCollectionChanged;

			_dataSource = value;
			OnPropertyChanged();

			if (_dataSource != null)
				_dataSource.CollectionChanged += OnDataSourceCollectionChanged;

			InvalidateColumnWidths();
			_measurementCache.InvalidateCache();
			_selectedRowIndex = -1;
			_selectedColumnIndex = -1;
			_scrollOffset = 0;
			_horizontalScrollOffset = 0;
			_sortColumnIndex = -1;
			_sortDirection = SortDirection.None;
			_sortIndexMap = null;
			_filterIndexMap = null;
			_filterMode = FilterMode.None;
			_filterBuffer = string.Empty;
			_activeFilter = null;
			Container?.Invalidate(true);
		}
	}

	private void OnDataSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			_selectedRowIndex = RowCount > 0 ? 0 : -1;
			_selectedRowIndices.Clear();
			_scrollOffset = 0;
		}
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	#endregion

	#region IMouseAwareControl Properties

	/// <inheritdoc/>
	public bool WantsMouseEvents
	{
		get => _wantsMouseEvents;
		set => SetProperty(ref _wantsMouseEvents, value);
	}

	/// <inheritdoc/>
	public bool CanFocusWithMouse => _isEnabled;

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

	#region IFocusableControl

	/// <inheritdoc/>
	public bool HasFocus
	{
		get => ComputeHasFocus();
	}

	/// <inheritdoc/>
	public bool CanReceiveFocus => _isEnabled;

	#endregion

	#region Public Methods - Column Management

	/// <summary>
	/// Adds a column with the specified header.
	/// </summary>
	public void AddColumn(string header, TextJustification alignment = TextJustification.Left, int? width = null)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add columns when DataSource is set.");
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
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add columns when DataSource is set.");
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
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		lock (_tableLock) { _rows.Add(new TableRow(cells)); }
		_sortIndexMap = null;
		_filterIndexMap = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		lock (_tableLock) { _rows.Add(row); }
		_sortIndexMap = null;
		_filterIndexMap = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public void AddRows(IEnumerable<TableRow> rows)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		lock (_tableLock) { _rows.AddRange(rows); }
		_sortIndexMap = null;
		_filterIndexMap = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Inserts a row with the specified cells at the given index.
	/// Index is clamped to [0, RowCount].
	/// </summary>
	/// <param name="index">The zero-based index at which to insert.</param>
	/// <param name="cells">The cell values for the new row.</param>
	public void InsertRow(int index, params string[] cells)
	{
		InsertRow(index, new TableRow(cells));
	}

	/// <summary>
	/// Inserts a row at the given index.
	/// Index is clamped to [0, RowCount].
	/// </summary>
	/// <param name="index">The zero-based index at which to insert.</param>
	/// <param name="row">The row to insert.</param>
	public void InsertRow(int index, TableRow row)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot insert rows when DataSource is set.");

		lock (_tableLock)
		{
			index = Math.Clamp(index, 0, _rows.Count);
			_rows.Insert(index, row);
		}

		AdjustSelectionAfterInsert(index, 1);
		_sortIndexMap = null;
		_filterIndexMap = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Inserts multiple rows starting at the given index.
	/// Index is clamped to [0, RowCount].
	/// </summary>
	/// <param name="index">The zero-based index at which to begin inserting.</param>
	/// <param name="rows">The rows to insert.</param>
	public void InsertRows(int index, IEnumerable<TableRow> rows)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot insert rows when DataSource is set.");

		var rowList = rows.ToList();
		if (rowList.Count == 0) return;

		lock (_tableLock)
		{
			index = Math.Clamp(index, 0, _rows.Count);
			_rows.InsertRange(index, rowList);
		}

		AdjustSelectionAfterInsert(index, rowList.Count);
		_sortIndexMap = null;
		_filterIndexMap = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Shifts selection indices forward after rows are inserted.
	/// </summary>
	private void AdjustSelectionAfterInsert(int insertIndex, int count)
	{
		if (_selectedRowIndex >= insertIndex)
		{
			_selectedRowIndex += count;
		}

		if (_selectedRowIndices.Count > 0)
		{
			var adjusted = new HashSet<int>();
			foreach (var idx in _selectedRowIndices)
			{
				adjusted.Add(idx >= insertIndex ? idx + count : idx);
			}
			_selectedRowIndices = adjusted;
		}
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

		// Adjust selection
		if (_selectedRowIndex >= 0)
		{
			int rowCount;
			lock (_tableLock) { rowCount = _rows.Count; }
			if (_selectedRowIndex == index)
			{
				_selectedRowIndex = rowCount > 0 ? Math.Min(_selectedRowIndex, rowCount - 1) : -1;
				SelectedRowChanged?.Invoke(this, _selectedRowIndex);
			}
			else if (_selectedRowIndex > index)
			{
				_selectedRowIndex--;
			}
		}

		_sortIndexMap = null;
		_filterIndexMap = null;
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
		_selectedRowIndex = -1;
		_selectedColumnIndex = -1;
		_scrollOffset = 0;
		_horizontalScrollOffset = 0;
		_selectedRowIndices.Clear();
		_sortIndexMap = null;
		_filterIndexMap = null;
		_filterMode = FilterMode.None;
		_filterBuffer = string.Empty;
		_activeFilter = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		SelectedRowChanged?.Invoke(this, -1);
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
		if (_dataSource != null)
			return _dataSource.GetCellValue(row, column);

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
		_sortIndexMap = null;
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
	internal Color ResolveBackgroundColor(Color defaultBg)
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

	internal Color ResolveForegroundColor(Color defaultFg)
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

	internal Color ResolveHeaderBackgroundColor()
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

	internal Color ResolveHeaderForegroundColor()
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

	internal Color ResolveBorderColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return _borderColorValue
			?? theme?.TableBorderColor
			?? theme?.ActiveBorderForegroundColor
			?? Color.White;
	}

	internal Color ResolveSelectionBackgroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableSelectionBackgroundColor ?? Color.Blue;
	}

	internal Color ResolveSelectionForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableSelectionForegroundColor ?? Color.White;
	}

	internal Color ResolveUnfocusedSelectionBackgroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableUnfocusedSelectionBackgroundColor ?? Color.Navy;
	}

	internal Color ResolveUnfocusedSelectionForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableUnfocusedSelectionForegroundColor ?? Color.Silver;
	}

	internal Color ResolveHoverBackgroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableHoverBackgroundColor ?? Color.Grey27;
	}

	internal Color ResolveHoverForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableHoverForegroundColor ?? Color.White;
	}

	internal Color ResolveScrollbarThumbColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableScrollbarThumbColor ?? ((ComputeHasFocus()) ? Color.Cyan1 : Color.Grey);
	}

	internal Color ResolveScrollbarTrackColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return theme?.TableScrollbarTrackColor ?? ((ComputeHasFocus()) ? Color.Grey : Color.Grey23);
	}

	#endregion

	#region Column Width Calculation

	private void InvalidateColumnWidths()
	{
		_cachedColumnWidths = null;
		_cachedColumnWidthsForWidth = -1;
		_cachedColumnWidthsScrollOffset = -1;
		_measurementCache.InvalidateCache();
	}

	/// <summary>
	/// Computes column widths for the given total available width.
	/// Uses sample-based measurement for auto-width columns (visible rows + small buffer).
	/// </summary>
	internal int[] ComputeColumnWidths(int availableWidth, List<TableColumn> cols, List<TableRow>? rows, int scrollOffset = 0, int visibleRowCount = 50)
	{
		int colCount = cols.Count;
		if (colCount == 0) return Array.Empty<int>();

		// Check cache - invalidate on significant scroll change
		int scrollBucket = scrollOffset / Math.Max(1, visibleRowCount / 2);
		if (_cachedColumnWidths != null && _cachedColumnWidthsForWidth == availableWidth && _cachedColumnWidthsScrollOffset == scrollBucket)
			return _cachedColumnWidths;

		bool hasBorder = _borderStyle != BorderStyle.None;
		int separatorOverhead = hasBorder ? (colCount + 1)
			: (_columnSeparator.HasValue ? Math.Max(0, colCount - 1) : 0);
		int contentWidth = availableWidth - separatorOverhead;
		if (contentWidth < colCount) contentWidth = colCount;

		var widths = new int[colCount];
		int autoCount = 0;

		// Determine sample range for auto-width columns
		int sampleStart = Math.Max(0, scrollOffset);
		int sampleEnd = Math.Min(rows?.Count ?? 0, scrollOffset + Math.Max(50, visibleRowCount));

		for (int c = 0; c < colCount; c++)
		{
			if (cols[c].Width.HasValue)
			{
				widths[c] = cols[c].Width!.Value;
			}
			else
			{
				// Sample-based measurement: header + visible rows + buffer
				int maxW = _measurementCache.GetCachedLength(cols[c].Header);

				if (rows != null)
				{
					for (int r = sampleStart; r < sampleEnd; r++)
					{
						if (c < rows[r].Cells.Count)
						{
							int cellW = _measurementCache.GetCachedLength(rows[r].Cells[c]);
							if (cellW > maxW) maxW = cellW;
						}
					}
				}

				widths[c] = maxW;
				autoCount++;
			}
		}

		// Distribute remaining space
		int totalNatural = 0;
		for (int c = 0; c < colCount; c++) totalNatural += widths[c];

		if (HorizontalAlignment == HorizontalAlignment.Stretch && totalNatural < contentWidth)
		{
			int remaining = contentWidth - totalNatural;
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
			// Shrink only auto-width columns first; preserve fixed-width columns
			int fixedTotal = 0;
			int autoTotal = 0;
			for (int c = 0; c < colCount; c++)
			{
				if (cols[c].Width.HasValue)
					fixedTotal += widths[c];
				else
					autoTotal += widths[c];
			}

			int autoTarget = contentWidth - fixedTotal;
			if (autoTarget > 0 && autoTotal > 0)
			{
				double ratio = (double)autoTarget / autoTotal;
				int assigned = fixedTotal;
				int lastAutoCol = -1;
				for (int c = 0; c < colCount; c++)
				{
					if (cols[c].Width.HasValue) continue;
					widths[c] = Math.Max(1, (int)(widths[c] * ratio));
					assigned += widths[c];
					lastAutoCol = c;
				}
				if (lastAutoCol >= 0)
					widths[lastAutoCol] = Math.Max(1, widths[lastAutoCol] + (contentWidth - assigned));
			}
			else
			{
				// Not enough space even for fixed columns — shrink everything
				double ratio = (double)contentWidth / totalNatural;
				int assigned = 0;
				for (int c = 0; c < colCount - 1; c++)
				{
					widths[c] = Math.Max(1, (int)(widths[c] * ratio));
					assigned += widths[c];
				}
				widths[colCount - 1] = Math.Max(1, contentWidth - assigned);
			}
		}

		// Cache results
		_cachedColumnWidths = widths;
		_cachedColumnWidthsForWidth = availableWidth;
		_cachedColumnWidthsScrollOffset = scrollBucket;

		return widths;
	}

	/// <summary>
	/// Computes column widths for DataSource mode.
	/// </summary>
	internal int[] ComputeColumnWidthsFromDataSource(int availableWidth, int scrollOffset = 0, int visibleRowCount = 50)
	{
		if (_dataSource == null) return Array.Empty<int>();

		int colCount = _dataSource.ColumnCount;
		if (colCount == 0) return Array.Empty<int>();

		bool hasBorder = _borderStyle != BorderStyle.None;
		int borderOverhead = hasBorder ? (colCount + 1)
			: (_columnSeparator.HasValue ? Math.Max(0, colCount - 1) : 0);
		int contentWidth = availableWidth - borderOverhead;
		if (contentWidth < colCount) contentWidth = colCount;

		var widths = new int[colCount];
		int autoCount = 0;

		int sampleStart = Math.Max(0, scrollOffset);
		int sampleEnd = Math.Min(_dataSource.RowCount, scrollOffset + Math.Max(50, visibleRowCount));

		for (int c = 0; c < colCount; c++)
		{
			// Check for user resize override first
			if (_columnWidthOverrides.TryGetValue(c, out int overrideWidth))
			{
				widths[c] = overrideWidth;
			}
			else if (_dataSource.GetColumnWidth(c) is int colWidth)
			{
				widths[c] = colWidth;
			}
			else
			{
				int maxW = _measurementCache.GetCachedLength(_dataSource.GetColumnHeader(c));
				for (int r = sampleStart; r < sampleEnd; r++)
				{
					int cellW = _measurementCache.GetCachedLength(_dataSource.GetCellValue(r, c));
					if (cellW > maxW) maxW = cellW;
				}
				widths[c] = maxW;
				autoCount++;
			}
		}

		int totalNatural = 0;
		for (int c = 0; c < colCount; c++) totalNatural += widths[c];

		if (HorizontalAlignment == HorizontalAlignment.Stretch && totalNatural < contentWidth)
		{
			int remaining = contentWidth - totalNatural;
			int distributeCount = autoCount > 0 ? autoCount : colCount;
			int perCol = remaining / distributeCount;
			int extraCols = remaining % distributeCount;

			for (int c = 0; c < colCount; c++)
			{
				int? dsColWidth = _dataSource.GetColumnWidth(c);
				bool isAutoCol = !dsColWidth.HasValue && !_columnWidthOverrides.ContainsKey(c);
				if (autoCount > 0 && !isAutoCol) continue;
				widths[c] += perCol;
				if (extraCols > 0) { widths[c]++; extraCols--; }
			}
		}
		else if (totalNatural > contentWidth)
		{
			// Shrink only auto-width columns first; preserve fixed/overridden columns
			int fixedTotal = 0;
			int autoTotal = 0;
			for (int c = 0; c < colCount; c++)
			{
				bool isFixed = _columnWidthOverrides.ContainsKey(c) || _dataSource.GetColumnWidth(c).HasValue;
				if (isFixed)
					fixedTotal += widths[c];
				else
					autoTotal += widths[c];
			}

			int autoTarget = contentWidth - fixedTotal;
			if (autoTarget > 0 && autoTotal > 0)
			{
				double ratio = (double)autoTarget / autoTotal;
				int assigned = fixedTotal;
				int lastAutoCol = -1;
				for (int c = 0; c < colCount; c++)
				{
					bool isFixed = _columnWidthOverrides.ContainsKey(c) || _dataSource.GetColumnWidth(c).HasValue;
					if (isFixed) continue;
					widths[c] = Math.Max(1, (int)(widths[c] * ratio));
					assigned += widths[c];
					lastAutoCol = c;
				}
				if (lastAutoCol >= 0)
					widths[lastAutoCol] = Math.Max(1, widths[lastAutoCol] + (contentWidth - assigned));
			}
			else
			{
				// Not enough space even for fixed columns — shrink everything
				double ratio = (double)contentWidth / totalNatural;
				int assigned = 0;
				for (int c = 0; c < colCount - 1; c++)
				{
					widths[c] = Math.Max(1, (int)(widths[c] * ratio));
					assigned += widths[c];
				}
				widths[colCount - 1] = Math.Max(1, contentWidth - assigned);
			}
		}

		return widths;
	}

	#endregion

	#region Internal Helpers

	/// <summary>
	/// Maps a display row index to the actual data row index, accounting for sorting.
	/// </summary>
	internal int MapDisplayToData(int displayIndex)
	{
		if (_filterIndexMap != null && displayIndex >= 0 && displayIndex < _filterIndexMap.Length)
			return _filterIndexMap[displayIndex];
		if (_sortIndexMap != null && displayIndex >= 0 && displayIndex < _sortIndexMap.Length)
			return _sortIndexMap[displayIndex];
		return displayIndex;
	}

	/// <summary>
	/// Maps a data row index to the display row index, accounting for filtering and sorting.
	/// </summary>
	internal int MapDataToDisplay(int dataIndex)
	{
		if (_filterIndexMap != null)
		{
			for (int i = 0; i < _filterIndexMap.Length; i++)
			{
				if (_filterIndexMap[i] == dataIndex) return i;
			}
			return dataIndex;
		}
		if (_sortIndexMap == null) return dataIndex;
		for (int i = 0; i < _sortIndexMap.Length; i++)
		{
			if (_sortIndexMap[i] == dataIndex) return i;
		}
		return dataIndex;
	}

	internal BoxChars GetBoxChars()
	{
		if (_useSafeBorder) return BoxChars.Ascii;
		return BoxChars.FromBorderStyle(_borderStyle);
	}

	/// <summary>
	/// Gets the total column width including border overhead.
	/// </summary>
	internal int GetTotalColumnsWidth(int[] colWidths)
	{
		int total = 0;
		foreach (int w in colWidths) total += w;
		bool hasBorder = _borderStyle != BorderStyle.None;
		if (hasBorder) total += colWidths.Length + 1;
		else if (_columnSeparator.HasValue)
		{
			int dataColCount = _checkboxMode ? Math.Max(0, colWidths.Length - 1) : colWidths.Length;
			total += Math.Max(0, dataColCount - 1);
		}
		return total;
	}

	/// <summary>
	/// Gets the total column width from last rendered column widths.
	/// Used by scrollbar hit-testing when colWidths array is not available.
	/// </summary>
	internal int GetTotalColumnsWidth()
	{
		// Use rendered column widths (works for both DataSource and in-memory)
		if (_renderedColumnWidths.Length > 0)
		{
			int total = 0;
			foreach (int w in _renderedColumnWidths) total += w;
			bool hasBorder = _borderStyle != BorderStyle.None;
			if (hasBorder) total += _renderedColumnWidths.Length + 1;
			else if (_columnSeparator.HasValue) total += Math.Max(0, _renderedColumnWidths.Length - 1);
			return total;
		}

		// Fallback to in-memory columns
		List<TableColumn> cols;
		lock (_tableLock) { cols = _columns.ToList(); }
		if (cols.Count == 0) return 0;

		int total2 = 0;
		foreach (var col in cols) total2 += col.RenderedWidth;
		bool hasBorder2 = _borderStyle != BorderStyle.None;
		if (hasBorder2) total2 += cols.Count + 1;
		return total2;
	}

	/// <summary>
	/// Sets a column width override (used for column resizing in DataSource mode).
	/// </summary>
	internal void SetColumnWidthOverride(int columnIndex, int width)
	{
		_columnWidthOverrides[columnIndex] = width;
	}

	/// <summary>
	/// Calculates the absolute Y position of a rendered row.
	/// Returns -1 if the row is not currently visible.
	/// </summary>
	internal int GetRenderedRowY(int displayRowIndex)
	{
		int dataStartY = ActualY + Margin.Top;
		if (!string.IsNullOrEmpty(_title)) dataStartY++;
		bool hasBorder = _borderStyle != BorderStyle.None;
		if (hasBorder) dataStartY++;
		if (_showHeader) dataStartY++;
		if (_showHeader && hasBorder) dataStartY++;

		int rowOffset = displayRowIndex - _scrollOffset;
		if (rowOffset < 0) return -1;

		int rowHeight = (_showRowSeparators && hasBorder) ? 2 : 1;
		return dataStartY + rowOffset * rowHeight;
	}

	/// <summary>
	/// Traverses up the container hierarchy to find the containing Window.
	/// </summary>
	internal Window? FindContainingWindow()
	{
		IContainer? currentContainer = Container;
		const int MaxLevels = 10;
		int level = 0;

		while (currentContainer != null && level < MaxLevels)
		{
			if (currentContainer is Window window)
				return window;

			if (currentContainer is IWindowControl control)
				currentContainer = control.Container;
			else if (currentContainer is ColumnContainer columnContainer)
				currentContainer = columnContainer.HorizontalGridContent.Container;
			else
				break;

			level++;
		}

		return null;
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

	#region Lifecycle

	/// <inheritdoc/>
	protected override void OnDisposing()
	{
		if (_dataSource != null)
			_dataSource.CollectionChanged -= OnDataSourceCollectionChanged;

		SelectedRowChanged = null;
		SelectedRowItemChanged = null;
		RowActivated = null;
		CellActivated = null;
		CellEditCompleted = null;
		CellEditCancelled = null;
		MouseClick = null;
		MouseDoubleClick = null;
		MouseRightClick = null;
		MouseEnter = null;
		MouseLeave = null;
		MouseMove = null;
	}

	#endregion

	#region Static Factory

	/// <summary>
	/// Creates a new TableControlBuilder for fluent configuration.
	/// </summary>
	public static Builders.TableControlBuilder Create() => new Builders.TableControlBuilder();

	#endregion
}
