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
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Sub-region gesture ownership for <see cref="TableControl"/> mouse handling. A fresh Button1 press
/// hit-tests one of these regions and captures it; every subsequent resent press/drag routes to the
/// captured region without re-hit-testing (SGR re-sends Button1Pressed on motion). This stops a
/// column-resize drag whose pointer wanders off the border, or a cell drag crossing a column border,
/// from being re-hit-tested into the wrong handler.
/// </summary>
internal enum TableGestureRegion
{
	/// <summary>Data/header cells (click, selection, sort-header, cell navigation).</summary>
	Cells,

	/// <summary>A column border being dragged to resize the column.</summary>
	ColumnResize,

	/// <summary>The vertical scrollbar column.</summary>
	VScrollbar,

	/// <summary>The horizontal scrollbar row.</summary>
	HScrollbar
}

/// <summary>
/// A table control that renders tabular data directly to CharacterBuffer.
/// Supports both read-only display and interactive mode with selection,
/// keyboard navigation, scrolling, sorting, multi-selection, column resizing,
/// inline editing, draggable scrollbars, and virtual data binding.
/// </summary>
public partial class TableControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IPasteTarget, IColorRoleableControl
{

	#region ColorRole

	private ColorRole _role = ColorRole.Default;
	private ThemeMode? _colorRoleMode;
	private bool _outline;

	/// <inheritdoc/>
	public ColorRole ColorRole
	{
		get => _role;
		set => SetProperty(ref _role, value);
	}

	/// <inheritdoc/>
	public ThemeMode? ColorRoleMode
	{
		get => _colorRoleMode;
		set => SetProperty(ref _colorRoleMode, value);
	}

	/// <inheritdoc/>
	public bool Outline
	{
		get => _outline;
		set => SetProperty(ref _outline, value);
	}

	#endregion

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
	private bool _rightClickExtendsSelection = false;
	private bool _clearSelectionOnEmptyClick = false;
	private HashSet<int> _selectedRowIndices = new();
	// Anchor for keyboard Shift+Up/Down range selection - the row a shift-extended range grows from/to.
	// Reset to the cursor row on every non-shift cursor move so a fresh Shift+arrow always starts from
	// wherever the cursor currently is (matches Explorer/Excel range-select semantics).
	private int _selectionAnchorRowIndex = -1;
	// True from a plain (no modifier) Button1 press on a data row through its release - lets a
	// click-and-drag extend the multi-select range live, as a mouse-only stand-in for Shift+Click
	// (terminals routinely swallow Shift+Click for their own text selection - see #Shift-click bug).
	private bool _isRowDragSelecting = false;
	// Non-null while a Ctrl+drag is in progress: the selection snapshot taken on press, so each Move
	// can recompute the live preview as (snapshot ∪ anchor..pointer range) instead of accumulating rows
	// the pointer has since backtracked past. Null when no Ctrl+drag is active.
	private HashSet<int>? _ctrlDragBaseSelection = null;

	/// <summary>
	/// When true, a left-click in the data area that does not hit any row
	/// clears the current selection (SelectedRowIndex becomes -1 and any
	/// multi-selection is discarded). Default: false, to preserve the
	/// classic "click below the last row does nothing" behavior.
	/// </summary>
	public bool ClearSelectionOnEmptyClick
	{
		get => _clearSelectionOnEmptyClick;
		set => _clearSelectionOnEmptyClick = value;
	}

	// Hover
	private int _hoveredRowIndex = -1;
	private bool _hoverEnabled = true;

	// Scrolling
	private int _scrollOffset = 0;
	private bool _autoScroll = false;
	private int _horizontalScrollOffset = 0;
	private ScrollbarVisibility _verticalScrollbarVisibility = ScrollbarVisibility.Auto;
	private ScrollbarVisibility _horizontalScrollbarVisibility = ScrollbarVisibility.Auto;
	private int _minScrollbarThumbSize = ControlDefaults.DefaultMinScrollbarThumbSize;
	private int _mouseWheelScrollSpeed = ControlDefaults.DefaultScrollWheelLines;

	// Mouse gesture capture: a fresh Button1 press captures one sub-region; every subsequent resent
	// press/drag routes to it without re-hit-testing (SGR re-sends Button1Pressed on motion). Replaces
	// the former _isVerticalScrollbarDragging / _isHorizontalScrollbarDragging / _isResizingColumn flags.
	private readonly MouseGestureCapture<TableGestureRegion> _gesture = new();

	// Thumb-drag latches: set only when a scrollbar Down landed on the thumb, so a subsequent Move
	// applies a thumb drag. An arrow/track-page Down leaves these false, so its Moves do nothing.
	private bool _vThumbDragging = false;
	private bool _hThumbDragging = false;

	// Scrollbar dragging state. *ThumbPos anchors the drag on the thumb's track position (not the
	// scroll offset) so ScrollbarInput.OffsetForDrag can invert cursor movement back to an offset that
	// round-trips - the same anchor ScrollablePanelControl uses.
	private int _scrollbarDragStartY = 0;
	private int _scrollbarDragStartX = 0;
	private int _scrollbarDragStartThumbPos = 0;
	private int _hScrollbarDragStartThumbPos = 0;

	// Sorting
	private bool _sortingEnabled = false;
	private int _sortColumnIndex = -1;
	private SortDirection _sortDirection = SortDirection.None;

	// Filtering
	internal bool _filteringEnabled = false;
	internal FilterMode _filterMode = FilterMode.None;
	internal string _filterBuffer = string.Empty;
	internal int _filterCursorPosition = 0;

	// Which data rows are displayed, in what order: one map for sort and filter together
	private readonly TableRowView _rowView = new();
	// Above zero while ComputeDisplayRows or ResolveHidden/RemovedSelectedRow runs, when the rows must not change
	private int _displayRowsHookDepth;

	/// <summary>
	/// True when a filter display map is active that the TABLE built and can therefore rebuild.
	/// </summary>
	/// <remarks>
	/// The condition sorting uses to decide between recomputing the combined filter+sort map and
	/// asking the source to sort itself. A map that came from the source is deliberately excluded:
	/// recomputing would rescan rows client-side and overwrite what the source chose to show, so
	/// such a table sorts the way a self-narrowing source already does — through
	/// <see cref="ITableDataSource.Sort"/>.
	/// </remarks>
	private bool HasClientFilterMap => _rowView.HasClientFilterMap && _activeFilter != null;
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
	// When true, the column separator glyph is drawn with one space on each side (" │ ")
	// instead of flush against the adjacent cell content. Opt-in; default flush preserves
	// the original single-cell-wide separator behaviour.
	private bool _columnSeparatorPadded;
	// When true, a one-cell blank gutter is reserved between the column content and the vertical
	// scrollbar, so a right-aligned last column doesn't sit flush against the scrollbar. Opt-in;
	// default false keeps the original flush-to-scrollbar layout.
	private bool _scrollbarGutter;

	/// <summary>
	/// Extra cells reserved to the right of the column content, before the vertical scrollbar:
	/// 1 when <see cref="_scrollbarGutter"/> is set and a vertical scrollbar is shown, else 0.
	/// Single source of truth so the width budget and the scrollbar X position stay in sync.
	/// </summary>
	private int ScrollbarGutterWidth => (_scrollbarGutter && ShouldShowVerticalScrollbar()) ? 1 : 0;

	/// <summary>
	/// Rendered width, in cells, of a single column separator: 3 when padded (" │ "), otherwise 1.
	/// Single source of truth for the separator's contribution to width budgeting and hit-testing,
	/// so the render path and every layout calculation agree.
	/// </summary>
	private int SeparatorWidth => _columnSeparatorPadded ? 3 : 1;

	// Column resizing
	private bool _columnResizeEnabled = false;
	private int _resizingColumnIndex = -1;
	private int _resizeDragStartX = 0;
	private int _resizeDragStartWidth = 0;

	// Virtual data source
	private ITableDataSource? _dataSource;

	// Performance caches
	private readonly TextMeasurementCache _measurementCache;
	private readonly TableColumnWidthCalculator _widthCalculator;

	// Where the last paint put the columns, for hit testing; null before the first paint
	private TableGeometry? _geometry;

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
		_widthCalculator = new TableColumnWidthCalculator(_measurementCache);
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
				Invalidate(Invalidation.Repaint);
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
			if (_rowView.Map != null) return _rowView.Map.Length;
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
		set { _truncationFade = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
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
			_hoveredRowIndex = -1;
			_scrollOffset = 0;
			_horizontalScrollOffset = 0;
			_sortColumnIndex = -1;
			_sortDirection = SortDirection.None;
			_rowView.Clear();
			EndRowGestures();
			_filterMode = FilterMode.None;
			_filterBuffer = string.Empty;
			_activeFilter = null;
			Invalidate(Invalidation.Relayout);
		}
	}

	private void OnDataSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			_selectedRowIndex = RowCount > 0 ? 0 : -1;
			_selectedRowIndices.Clear();
			_hoveredRowIndex = -1;
			_scrollOffset = 0;
			EndRowGestures();
		}
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
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
		lock (_tableLock)
		{
			var column = new TableColumn(header, alignment, width) { Owner = this };
			_columns.Add(column);
			_rowView.RecordReset();
		}
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	public void AddColumn(TableColumn column)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add columns when DataSource is set.");
		lock (_tableLock) { column.Owner = this; _columns.Add(column); _rowView.RecordReset(); }
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Removes the column at the specified index.
	/// </summary>
	public void RemoveColumn(int index)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _columns.Count)
			{
				_columns[index].Owner = null;
				_columns.RemoveAt(index);
				_rowView.RecordReset();
			}
			else
				return;
		}
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Clears all columns.
	/// </summary>
	public void ClearColumns()
	{
		lock (_tableLock)
		{
			foreach (var column in _columns)
				column.Owner = null;
			_columns.Clear();
			_rowView.RecordReset();
		}
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
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
		Invalidate(Invalidation.Relayout);
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
		Invalidate(Invalidation.Relayout);
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
		// When a ColorRole is set, normal-row text is tinted with the role (readable on the surface) so the
		// table reads as its role even with no row selected. ColorRoleForeground is null for ColorRole.Default.
		if (_foregroundColorValue == null)
			return ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, mode: ColorRoleMode) ?? Container?.ForegroundColor ?? defaultFg;
		if (_foregroundColorValue.Value == Color.Default)
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			return ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, mode: ColorRoleMode)
				?? theme?.TableForegroundColor
				?? Container?.ForegroundColor
				?? defaultFg;
		}
		return _foregroundColorValue.Value;
	}

	// A role fill mixed toward the window surface at the given strength (0 = surface, 1 = full role),
	// used for the graded selection/hover states. Returns null when no role is set.
	private Color? RoleFillMixed(double towardSurface)
	{
		Color? roleFill = ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, mode: ColorRoleMode);
		if (roleFill == null)
			return null;
		Color surface = Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
		return roleFill.Value.Mix(surface, towardSurface);
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
		return ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, mode: ColorRoleMode)
			?? theme?.TableSelectionBackgroundColor ?? Color.Blue;
	}

	internal Color ResolveSelectionForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, mode: ColorRoleMode)
			?? theme?.TableSelectionForegroundColor ?? Color.White;
	}

	internal Color ResolveUnfocusedSelectionBackgroundColor()
	{
		// Unfocused selection = a dimmed role fill (quieter, still visible) when a role is set.
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return RoleFillMixed(0.35) ?? theme?.TableUnfocusedSelectionBackgroundColor ?? Color.Navy;
	}

	internal Color ResolveUnfocusedSelectionForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		var roleBg = RoleFillMixed(0.35);
		if (roleBg != null)
			return PaletteColors.ReadableOn(roleBg.Value);
		return theme?.TableUnfocusedSelectionForegroundColor ?? Color.Silver;
	}

	internal Color ResolveHoverBackgroundColor()
	{
		// Hover = a mid-intensity role fill when a role is set.
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		return RoleFillMixed(0.55) ?? theme?.TableHoverBackgroundColor ?? Color.Grey27;
	}

	internal Color ResolveHoverForegroundColor()
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		var roleBg = RoleFillMixed(0.55);
		if (roleBg != null)
			return PaletteColors.ReadableOn(roleBg.Value);
		return theme?.TableHoverForegroundColor ?? Color.White;
	}

	/// <summary>
	/// Resolves the table's scrollbar thumb/track colours via the shared engine, reading
	/// <see cref="ITheme.TableScrollbarThumbColor"/>/<see cref="ITheme.TableScrollbarTrackColor"/>
	/// (Table's own theme keys, which have no unfocused variants) instead of the general
	/// <c>Scrollbar*</c> family every other scrollable control uses.
	/// </summary>
	/// <remarks>
	/// Accepted behaviour change from the pre-retrofit code: Table previously read the same
	/// (focused) theme colour regardless of focus, falling back to a focus-aware hardcoded pair only
	/// when no theme was set. The shared resolver is focus-aware for the fallback tier only, since the
	/// two Table theme keys have no unfocused counterpart.
	/// </remarks>
	private ScrollbarPalette ResolveScrollbarPalette(Color bgColor)
	{
		var theme = Container?.GetConsoleWindowSystem?.Theme;
		bool hasFocus = ComputeHasFocus();

		return ScrollbarPaletteResolver.Resolve(new ScrollbarPaletteRequest(
			ThumbOverride: null,
			TrackOverride: null,
			Theme: theme,
			HasFocus: hasFocus,
			IsEnabled: IsEnabled,
			Background: bgColor,
			UseTableThemeKeys: true));
	}

	#endregion

	#region Column Width Calculation

	/// <summary>
	/// Drops the cached column widths and text measurements, so the next layout measures the columns
	/// again.
	/// </summary>
	/// <remarks>
	/// The table invalidates them itself whenever a row, a cell or a column changes. A derived table
	/// calls this when something ELSE it draws into a cell changes width — a prefix that grew because
	/// a row moved deeper, say — which the table cannot see.
	/// </remarks>
	protected void InvalidateColumnWidths()
	{
		_widthCalculator.Invalidate();
		_measurementCache.InvalidateCache();
	}

	/// <summary>
	/// Routes a display-property change on an owned <see cref="TableColumn"/> back to the table:
	/// busts the cached column widths when the change affects sizing, then invalidates the container
	/// with the given mode. No-ops in DataSource mode, where in-memory columns are unused.
	/// </summary>
	internal void OnColumnDisplayChanged(TableColumn column, bool widthAffecting, Invalidation mode)
	{
		if (DataSource != null) return; // in-memory columns unused in data-source mode
		if (widthAffecting)
		{
			InvalidateColumnWidths();
			// A renamed header changes what a column-specific filter matches.
			lock (_tableLock) { _rowView.RecordReset(); }
		}
		Container?.Invalidate(mode);
	}

	/// <summary>
	/// Routes a display-property or cell change on an owned <see cref="TableRow"/> back to the table:
	/// busts the cached column widths when the change affects sizing, then invalidates the container
	/// with the given mode. No-ops in DataSource mode, where in-memory rows are unused.
	/// </summary>
	/// <remarks>
	/// A cell change does not re-sort or re-filter on its own, as it never has; but it does mean the
	/// display rows can no longer be updated incrementally from the last result, so the next change
	/// to the rows recomputes them. It is also passed on to <see cref="OnRowContentChanged"/>.
	/// </remarks>
	internal void OnRowDisplayChanged(TableRow row, bool widthAffecting, Invalidation mode)
	{
		if (DataSource != null) return; // in-memory rows unused in data-source mode
		if (widthAffecting)
		{
			InvalidateColumnWidths();
			lock (_tableLock) { _rowView.RecordReset(); }
		}
		Container?.Invalidate(mode);

		if (widthAffecting)
			OnRowContentChanged(row);
	}

	/// <summary>
	/// Called after one of the table's rows changed its cells: a cell set, added or removed, the
	/// cells replaced, or an inline edit committed.
	/// </summary>
	/// <param name="row">The row whose cells changed.</param>
	/// <remarks>
	/// <para>
	/// The table does not re-sort or re-filter when a cell changes, so a row being edited does not
	/// jump away under the cursor. A derived table whose order or visibility depends on a row's
	/// content can decide otherwise here — typically by calling <see cref="RefreshDisplayRows"/>,
	/// which keeps the selection on its row. The default does nothing.
	/// </para>
	/// <para>
	/// Called on the thread that changed the cells, which by the library's threading rule is the UI
	/// thread. The table never changes a cell while holding <see cref="SyncRoot"/>, so this is not
	/// called with it held unless the code changing the cells holds it. A change to a row's colours,
	/// enabled state or check mark is not a content change and does not call this.
	/// </para>
	/// </remarks>
	protected virtual void OnRowContentChanged(TableRow row)
	{
	}

	/// <summary>
	/// Computes the widths of the table's own columns for the given total available width.
	/// Uses sample-based measurement for auto-width columns (visible rows + small buffer).
	/// </summary>
	/// <param name="availableWidth">Cells available to the columns and their separators.</param>
	/// <param name="cols">The columns to size.</param>
	/// <param name="rows">The data rows to sample.</param>
	/// <param name="scrollOffset">The first displayed row on screen, where sampling starts.</param>
	/// <param name="visibleRowCount">How many rows are on screen.</param>
	/// <param name="displayRows">
	/// The display map to sample through, so the rows measured are the rows shown; null samples
	/// <paramref name="rows"/> in data order.
	/// </param>
	internal int[] ComputeColumnWidths(int availableWidth, List<TableColumn> cols, List<TableRow>? rows, int scrollOffset = 0, int visibleRowCount = ControlDefaults.TableColumnWidthSampleRows, int[]? displayRows = null)
		=> _widthCalculator.Compute(new TableColumnWidthSource(cols, rows, displayRows, GetCellPrefixMarkup),
			CreateWidthLayout(availableWidth, scrollOffset, visibleRowCount), useCache: true);

	/// <summary>
	/// Computes column widths for DataSource mode, sampling the rows displayed.
	/// </summary>
	internal int[] ComputeColumnWidthsFromDataSource(int availableWidth, int scrollOffset = 0, int visibleRowCount = ControlDefaults.TableColumnWidthSampleRows)
	{
		if (_dataSource == null) return Array.Empty<int>();

		return _widthCalculator.Compute(new TableDataSourceWidthSource(_dataSource, _columnWidthOverrides, _rowView.Map, GetCellPrefixMarkup),
			CreateWidthLayout(availableWidth, scrollOffset, visibleRowCount), useCache: false);
	}

	private TableWidthLayout CreateWidthLayout(int availableWidth, int scrollOffset, int visibleRowCount)
		=> new(availableWidth, _borderStyle != BorderStyle.None, _columnSeparator, SeparatorWidth,
			HorizontalAlignment == HorizontalAlignment.Stretch, scrollOffset, visibleRowCount);

	#endregion

	#region Internal Helpers

	/// <summary>
	/// Maps a display row index to the actual data row index, accounting for sorting.
	/// </summary>
	internal int MapDisplayToData(int displayIndex) => _rowView.MapDisplayToData(displayIndex);

	/// <summary>
	/// Maps a data row index to the display row index, accounting for filtering and sorting.
	/// </summary>
	internal int MapDataToDisplay(int dataIndex) => _rowView.MapDataToDisplay(dataIndex);

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
			total += Math.Max(0, dataColCount - 1) * SeparatorWidth;
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
		var geometry = _geometry;
		if (geometry != null && geometry.ColumnCount > 0)
		{
			int total = 0;
			for (int c = 0; c < geometry.ColumnCount; c++) total += geometry.GetColumnWidth(c);
			bool hasBorder = _borderStyle != BorderStyle.None;
			if (hasBorder) total += geometry.ColumnCount + 1;
			else if (_columnSeparator.HasValue) total += Math.Max(0, geometry.ColumnCount - 1) * SeparatorWidth;
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
				currentContainer = columnContainer.OwnerControl?.Container;
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
		// Abandon any captured mouse gesture so a half-finished resize/scrollbar drag cannot linger past disposal.
		_gesture.Reset();
		_vThumbDragging = false;
		_hThumbDragging = false;
		_isRowDragSelecting = false;
		_ctrlDragBaseSelection = null;

		if (_dataSource != null)
			_dataSource.CollectionChanged -= OnDataSourceCollectionChanged;

		SelectedRowChanged = null;
		SelectedRowChangedAsync = null;
		SelectedRowItemChanged = null;
		SelectedRowItemChangedAsync = null;
		RowActivated = null;
		RowActivatedAsync = null;
		CellActivated = null;
		CellActivatedAsync = null;
		CellEditCompleted = null;
		CellEditCompletedAsync = null;
		CellEditCancelled = null;
		CellEditCancelledAsync = null;
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
