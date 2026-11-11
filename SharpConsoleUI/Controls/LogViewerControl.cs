// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Displays log entries from an <see cref="ILogService"/> as a live, virtualized, filterable table.
/// Composes a <see cref="GridControl"/> (table today; toolbar / detail in later tasks) internally; the
/// table is fed by a <see cref="LogTableDataSource"/> so only visible rows are rendered. Auto-scroll
/// (tail-follow) sticks to the newest row unless the user scrolls up. Thread-safe: log events may arrive
/// from any thread (they are marshalled onto the UI thread by the data source).
/// </summary>
/// <remarks>
/// This control has no single themed colour surface (rows colour themselves by severity), so it does
/// not implement <see cref="IColorRoleableControl"/>. The inner grid is wired into the layout DOM tree
/// via <see cref="LayoutNodeFactory"/>, so the table's rows measure/arrange/paint through the real
/// engine (not a hand-painted copy) — this is what fixes the previously-empty Log Stream window.
/// </remarks>
public class LogViewerControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer
{
	private readonly ILogService _logService;
	private readonly LogTableDataSource _dataSource;
	private readonly TableControl _table;
	private readonly GridControl _grid;

	private ToolbarControl? _toolbar;
	private DropdownControl? _levelDropdown;
	private bool _isPaused;
	private bool _showToolbar = true;
	private bool _showRowSeparators;

	private MarkupControl? _detailContent;
	private ScrollablePanelControl? _detailPanel;
	private bool _showDetailPane = true;
	private const int DetailPaneRow = 2;

	private bool _autoScroll = true;
	private string? _title;
	private bool _systemAttached;
	private bool _globalFilterHotkey;
	private Window? _hotkeyWindow;

	/// <summary>Creates a new LogViewerControl bound to the specified log service.</summary>
	/// <param name="logService">The log service to display entries from.</param>
	public LogViewerControl(ILogService logService)
	{
		_logService = logService ?? throw new ArgumentNullException(nameof(logService));

		// A log pane is a scrolling list: it wants the room it is given. The inner grid and
		// table already Fill, but the control ITSELF defaulted to its content size, so adding
		// one to a window without setting alignment produced a correct, empty-looking table
		// with no room for rows. Fill is what every caller wanted; a caller who wants
		// something else still sets these afterwards, as before.
		VerticalAlignment = VerticalAlignment.Fill;
		HorizontalAlignment = HorizontalAlignment.Stretch;

		_dataSource = new LogTableDataSource(_logService);

		_table = new TableControl
		{
			ShowHeader = true,
			TruncationFade = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = _autoScroll,   // seed from the viewer's default intent (true)

			// TableControl.ReadOnly defaults to TRUE, meaning "static display, no interaction",
			// and it gates the INTERACTIVE filter ('/' and its footer) and column resize. The log
			// viewer has never been a static display: it scrolls, selects a row and drives the
			// detail pane from SelectedRowChanged. Leaving the default on silently disabled both
			// features below while they looked enabled. Cell editing stays off regardless, since
			// it needs InlineEditingEnabled too, which is false and not set here.
			ReadOnly = false,

			// Drag a column border to widen it. The category column is sized for the library's
			// own short categories, so a reader looking at full type names from an
			// ILogger<T> pipeline needs to be able to take the room from Message.
			ColumnResizeEnabled = true,

			// Click a header to sort. LogTableDataSource answers CanSort for every column and
			// sorts on the underlying values, so Level orders by severity and Time by timestamp.
			SortingEnabled = true,

			// The full filter grammar over Time/Level/Category/Message: press '/' in the table,
			// or type in the toolbar's Filter box. The table evaluates it itself against the
			// source's cell values, including AND/OR, per-column terms and fuzzy fallback.
			FilteringEnabled = true,
			FuzzyFilterEnabled = true,

			ShowRowSeparators = _showRowSeparators,
			DataSource = _dataSource
		};

		_levelDropdown = BuildLevelDropdown();
		_toolbar = BuildToolbar(_levelDropdown);

		_grid = new GridControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		_grid.RowDefinitions.Add(GridLength.Auto()); // row 0: toolbar
		_grid.RowDefinitions.Add(GridLength.Star(1)); // row 1: table
		_grid.ColumnDefinitions.Add(GridLength.Star(1));
		_grid.Place(_toolbar, row: 0, col: 0);
		_grid.Place(_table, row: 1, col: 0);

		_detailContent = new MarkupControl(new List<string> { string.Empty }) { Wrap = true };
		_detailPanel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalScrollMode = ScrollMode.Scroll,
			ShowScrollbar = true
		};
		_detailPanel.AddControl(_detailContent);

		_grid.RowDefinitions.Add(GridLength.Cells(0)); // row 2: detail, collapsed until selection
		_grid.Place(_detailPanel, row: DetailPaneRow, col: 0);

		_table.SelectedRowChanged += (_, index) => UpdateDetail(index);

		// Reactively re-apply tail-follow whenever rows change. Every action type (single Add, batch,
		// eviction/filter Reset) takes the same apply-only path; follow intent is owned by the input
		// handlers, never inferred from which action the data source happened to raise.
		_dataSource.CollectionChanged += (_, _) => ApplyTailFollow();

		// Show the level the service is ACTUALLY on. The dropdown's first item is "All", so a
		// viewer built over a service already set to Information used to read "All" while
		// capturing nothing below Information: the control claimed a state it did not have.
		SyncLevelDropdownSelection();
	}

	/// <summary>
	/// The inner grid that hosts the log table. Exposed to <see cref="LayoutNodeFactory"/> so the engine
	/// builds the grid (and therefore the virtualized table) into the layout DOM tree as this control's
	/// single Fill child. Not part of the public API.
	/// </summary>
	internal GridControl InnerGrid => _grid;

	#region Events (interface requirement)

#pragma warning disable CS0067
	/// <summary>Occurs when the control is clicked.</summary>
	public event EventHandler<MouseEventArgs>? MouseClick;
	/// <summary>Occurs when the control is double-clicked.</summary>
	public event EventHandler<MouseEventArgs>? MouseDoubleClick;
	/// <summary>Occurs when the control is right-clicked.</summary>
	public event EventHandler<MouseEventArgs>? MouseRightClick;
	/// <summary>Occurs when the mouse enters the control area.</summary>
	public event EventHandler<MouseEventArgs>? MouseEnter;
	/// <summary>Occurs when the mouse leaves the control area.</summary>
	public event EventHandler<MouseEventArgs>? MouseLeave;
	/// <summary>Occurs when the mouse moves over the control.</summary>
	public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

	#endregion

	#region Public Properties (preserved API)

	/// <summary>Gets or sets whether to keep the newest log entry visible (tail-follow).</summary>
	/// <remarks>
	/// This is sticky USER INTENT and is not changed by scrolling. It writes DOWN to the inner
	/// table's <see cref="TableControl.AutoScroll"/>, which tracks the transient "currently pinned
	/// to the bottom" state and detaches itself when the user scrolls up. The table never writes
	/// back up, so this property always reads back what the caller set.
	/// </remarks>
	public bool AutoScroll
	{
		get => _autoScroll;
		set
		{
			if (SetProperty(ref _autoScroll, value))
			{
				_table.AutoScroll = value;
				if (value) ResumeFollow();
			}
		}
	}

	/// <summary>Gets or sets the minimum log level shown by the VIEW filter (does not discard entries).</summary>
	public LogLevel FilterLevel
	{
		get => _dataSource.ViewFilterLevel;
		set { _dataSource.ViewFilterLevel = value; OnPropertyChanged(); }
	}

	/// <summary>Gets or sets the category the view filter restricts to; null shows all.</summary>
	public string? FilterCategory
	{
		get => _dataSource.ViewFilterCategory;
		set { _dataSource.ViewFilterCategory = value; OnPropertyChanged(); }
	}

	/// <summary>Gets or sets whether a separator line is drawn between rows.</summary>
	/// <remarks>
	/// Off by default: a log pane is usually dense, and separators halve how many entries fit.
	/// Worth turning on for a short pane, or when entries wrap to several lines each.
	/// </remarks>
	public bool ShowRowSeparators
	{
		get => _showRowSeparators;
		set { if (SetProperty(ref _showRowSeparators, value)) _table.ShowRowSeparators = value; }
	}

	/// <summary>Gets or sets whether a column border can be dragged to resize the column.</summary>
	/// <remarks>
	/// On by default. The category column is sized for short category names, so a reader looking
	/// at full type names from an <c>ILogger&lt;T&gt;</c> pipeline widens it from Message.
	/// </remarks>
	public bool ColumnResizeEnabled
	{
		get => _table.ColumnResizeEnabled;
		set { _table.ColumnResizeEnabled = value; OnPropertyChanged(); }
	}

	/// <summary>Gets or sets whether clicking a column header sorts by it.</summary>
	/// <remarks>
	/// On by default, cycling ascending, descending, then back to arrival order. Sorting is on
	/// the underlying values, so Level orders by severity rather than alphabetically and Time by
	/// the full timestamp rather than its displayed "HH:mm:ss". Entries with equal keys stay in
	/// the order they arrived.
	/// </remarks>
	public bool SortingEnabled
	{
		get => _table.SortingEnabled;
		set { _table.SortingEnabled = value; OnPropertyChanged(); }
	}

	/// <summary>Gets or sets whether the table's filter grammar is available.</summary>
	/// <remarks>
	/// On by default. Press '/' over the table, or type in the toolbar's Filter box. Plain text
	/// matches any column; <c>category:Order</c> restricts to one; terms combine with AND and OR.
	/// This is separate from <see cref="FilterLevel"/> and <see cref="FilterCategory"/>, which
	/// narrow the projection before the table ever sees it.
	/// </remarks>
	public bool FilteringEnabled
	{
		get => _table.FilteringEnabled;
		set { _table.FilteringEnabled = value; OnPropertyChanged(); }
	}

	/// <summary>
	/// Gets or sets whether pressing <c>/</c> anywhere on the window opens the table's filter,
	/// even when the viewer does not have focus. Off by default.
	/// </summary>
	/// <remarks>
	/// <para>
	/// With focus already in the viewer, <c>/</c> opens the filter regardless of this property;
	/// this is about claiming the key for the whole window. It hooks
	/// <see cref="Window.PreviewKeyPressed"/>, which runs before any control sees the key, so it
	/// is opt-in: a page where another control legitimately takes <c>/</c> (a terminal, a text
	/// editor, a prompt) must not have the log pane intercepting it.
	/// </para>
	/// <para>
	/// Even when on, the hotkey DECLINES while focus is on an interactive control other than this
	/// viewer, so typing a slash into an edit box, a terminal or a prompt still types a slash.
	/// It fires when focus is on a non-interactive control, on nothing at all, or on the viewer
	/// itself. A plain <c>/</c> only: any modifier is left alone.
	/// </para>
	/// </remarks>
	public bool GlobalFilterHotkey
	{
		get => _globalFilterHotkey;
		set
		{
			if (!SetProperty(ref _globalFilterHotkey, value)) return;
			if (value) HookHotkey(); else UnhookHotkey();
		}
	}

	/// <summary>
	/// Gets whether a filter is currently active, whether being typed or already applied.
	/// </summary>
	public bool IsFiltering => _table.IsFiltering;

	/// <summary>Gets the active filter text, or null when no filter is applied.</summary>
	public string? ActiveFilterText => _table.ActiveFilterText;

	/// <summary>
	/// Applies a filter using the table's grammar, as if it had been typed into the toolbar.
	/// </summary>
	/// <param name="filterText">
	/// Plain text matches any column. <c>category:Order</c> restricts to one column, and terms
	/// combine with AND and OR. Null or empty clears the filter.
	/// </param>
	/// <remarks>
	/// This is the view's filter, applied by the table over the rows it is shown. It is separate
	/// from <see cref="FilterLevel"/> and <see cref="FilterCategory"/>, which narrow the
	/// projection before the table sees it, and from <see cref="ILogService.MinimumLevel"/>,
	/// which decides what is captured at all.
	/// </remarks>
	public void ApplyFilterText(string? filterText)
	{
		if (string.IsNullOrEmpty(filterText))
			_table.ClearFilter();
		else
			_table.ApplyFilter(filterText);
	}

	/// <summary>Clears any filter applied through the grammar.</summary>
	public void ClearFilterText() => _table.ClearFilter();

	/// <summary>Gets or sets a title shown above the log table.</summary>
	public string? Title
	{
		get => _title;
		set { if (SetProperty(ref _title, value)) _table.Title = value; }
	}

	#endregion

	#region Toolbar API (Task 6)

	/// <summary>Gets or sets whether tail-follow is frozen. Paused entries still buffer.</summary>
	public bool IsPaused
	{
		get => _isPaused;
		set { if (SetProperty(ref _isPaused, value) && !value) ResumeFollow(); }
	}

	/// <summary>Gets or sets whether the toolbar is shown.</summary>
	public bool ShowToolbar
	{
		get => _showToolbar;
		set { if (SetProperty(ref _showToolbar, value) && _toolbar != null) _toolbar.Visible = value; }
	}

	/// <summary>
	/// Sets the log service capture level (what the app records going forward). This is the
	/// dropdown's action: it changes <see cref="ILogService.MinimumLevel"/>, not just the view filter.
	/// </summary>
	/// <param name="level">The minimum level the log service should record from now on.</param>
	public void SetCaptureLevel(LogLevel level)
	{
		_logService.MinimumLevel = level;
		SyncLevelDropdownSelection();
	}

	private DropdownControl BuildLevelDropdown()
	{
		var items = new List<DropdownItem>
		{
			new DropdownItem("All") { Tag = LogLevel.Trace },
			new DropdownItem("Trace") { Tag = LogLevel.Trace },
			new DropdownItem("Debug") { Tag = LogLevel.Debug },
			new DropdownItem("Information") { Tag = LogLevel.Information },
			new DropdownItem("Warning") { Tag = LogLevel.Warning },
			new DropdownItem("Error") { Tag = LogLevel.Error },
			new DropdownItem("Critical") { Tag = LogLevel.Critical },
		};
		var dd = new DropdownControl("Level:", items);
		dd.SelectedItemChanged += (_, item) =>
		{
			if (item?.Tag is LogLevel level) _logService.MinimumLevel = level;
		};
		return dd;
	}

	private ToolbarControl BuildToolbar(DropdownControl level)
	{
		// No filter box here on purpose. Filtering is the table's, reached with '/' over the
		// table or set programmatically through ApplyFilterText, so the toolbar does not carry
		// a second, weaker entry point to the same feature.
		var toolbar = new ToolbarControl
		{
			Visible = _showToolbar,

			// ToolbarControl packs items edge to edge by default, which runs the level
			// dropdown straight into the first button and the buttons into each other.
			ItemSpacing = 2
		};
		toolbar.AddItem(level);
		toolbar.AddItem(Builders.Controls.Button("Pause").OnClick((_, _) => TogglePause()).Build());
		toolbar.AddItem(Builders.Controls.Button("Clear").OnClick((_, _) => _logService.ClearLogs()).Build());
		toolbar.AddItem(Builders.Controls.Button("Export").OnClick((_, _) => ExportAsync()).Build());
		return toolbar;
	}

	private void TogglePause() => IsPaused = !IsPaused;

	/// <summary>
	/// Points the dropdown at the service's current level.
	/// </summary>
	/// <remarks>
	/// Called at construction and from <see cref="SetCaptureLevel"/>, and again on every
	/// refresh, because an application can set <see cref="ILogService.MinimumLevel"/> directly
	/// and the control has no event to learn that from.
	/// </remarks>
	private void SyncLevelDropdownSelection()
	{
		if (_levelDropdown == null) return;
		var current = _logService.MinimumLevel;
		for (int i = 0; i < _levelDropdown.Items.Count; i++)
			if (_levelDropdown.Items[i].Tag is LogLevel l && l == current && _levelDropdown.Items[i].Text != "All")
			{
				_levelDropdown.SelectedIndex = i;
				return;
			}
	}

	private async void ExportAsync()
	{
		var system = Container?.GetConsoleWindowSystem;
		if (system == null) return;
		try
		{
			var path = await FileDialogs.ShowSaveFileAsync(
				system, startPath: null,
				filter: ControlDefaults.LogViewerExportFilter,
				defaultFileName: ControlDefaults.LogViewerExportDefaultFileName);
			if (string.IsNullOrEmpty(path)) return;

			var lines = _dataSource.DisplayedEntries.Select(e => e.ToString());
			await System.IO.File.WriteAllLinesAsync(path, lines);

			// The continuation runs on a threadpool thread (InstallSynchronizationContext=false),
			// so marshal the UI-state mutation (ShowNotification) back onto the UI thread (Rule 13).
			int count = _dataSource.RowCount;
			system.EnqueueOnUIThread(() =>
				system.NotificationStateService.ShowNotification("Export", $"Wrote {count} entries.", NotificationSeverity.Info));
		}
		catch (Exception ex)
		{
			string message = ex.Message;
			system.EnqueueOnUIThread(() =>
				system.NotificationStateService.ShowNotification("Export failed", message, NotificationSeverity.Danger));
		}
	}

	#endregion

	#region Detail Pane (Task 7)

	/// <summary>Gets or sets whether the detail pane is shown when a row is selected.</summary>
	public bool ShowDetailPane
	{
		get => _showDetailPane;
		set { if (SetProperty(ref _showDetailPane, value)) UpdateDetail(_table.SelectedRowIndex); }
	}

	/// <summary>Programmatically selects a display row (shows its detail).</summary>
	/// <param name="rowIndex">The zero-based display-row index to select.</param>
	public void SelectEntry(int rowIndex)
	{
		_table.SelectedRowIndex = rowIndex;
		UpdateDetail(rowIndex);
	}

	private void UpdateDetail(int rowIndex)
	{
		var entry = _dataSource.EntryAt(rowIndex);
		bool show = _showDetailPane && entry != null;

		_grid.RowDefinitions[DetailPaneRow] = show
			? GridLength.Cells(ControlDefaults.DefaultMinimumVisibleItems + ControlDefaults.DefaultBorderWidth)
			: GridLength.Cells(0);

		if (entry != null && _detailContent != null)
		{
			static string Esc(string s) => s.Replace("[", "[[").Replace("]", "]]");
			var text = Esc(entry.Message);
			if (entry.Exception != null)
				text += $"\n[red]{Esc(entry.Exception.GetType().Name)}: {Esc(entry.Exception.Message)}[/]\n{Esc(entry.Exception.StackTrace ?? string.Empty)}";
			_detailContent.SetContent(new List<string> { text });
		}
	}

	#endregion

	#region Focus / Interaction (preserved API)

	/// <inheritdoc/>
	public bool HasFocus => ComputeHasFocus();
	/// <inheritdoc/>
	public bool IsEnabled { get; set; } = true;
	/// <inheritdoc/>
	public bool CanReceiveFocus => Visible && IsEnabled;
	/// <inheritdoc/>
	public bool WantsMouseEvents => true;
	/// <inheritdoc/>
	public bool CanFocusWithMouse => CanReceiveFocus;

	/// <inheritdoc/>
	public bool ProcessKey(ConsoleKeyInfo keyInfo)
	{
		if (!IsEnabled || !HasFocus) return false;

		if (keyInfo.Key == ConsoleKey.Delete ||
			(keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)))
		{
			// A clear is an explicit "start fresh": re-attach to the tail regardless of scroll position.
			_table.AutoScroll = _autoScroll;
			_logService.ClearLogs();
			return true;
		}

		// Delegate scroll/selection to the table. The table owns follow state (TableControl.AutoScroll),
		// so nothing needs re-deriving here — which is what makes this correct even though the window
		// dispatcher may route scroll input straight to the table without calling this method at all.
		return ((IInteractiveControl)_table).ProcessKey(keyInfo);
	}

	/// <inheritdoc/>
	public bool ProcessMouseEvent(MouseEventArgs args)
	{
		if (args.HasFlag(MouseFlags.Button3Clicked))
		{
			MouseRightClick?.Invoke(this, args);
			return true;
		}

		// Delegate to the table, which owns follow state. Note the window dispatcher bubbles wheel
		// events deepest-first and stops at the first consumer, so the inner table often handles the
		// wheel WITHOUT this method ever running — which is exactly why follow state must not live here.
		return ((IMouseAwareControl)_table).ProcessMouseEvent(args);
	}

	#endregion

	#region Layout

	/// <inheritdoc/>
	/// <remarks>
	/// This control is a tree-participating container: the inner grid's <see cref="IContainer"/> is set to
	/// <c>this</c> (mirrors <c>NavigationView</c>), so <c>_grid.Container.GetConsoleWindowSystem</c> resolves
	/// through this control's own Container. Setting it here (rather than in <see cref="EnsureAttached"/>)
	/// keeps the wiring correct the moment this control is attached to its parent.
	/// </remarks>
	public override IContainer? Container
	{
		get => base.Container;
		set
		{
			base.Container = value;
			_grid.Container = this;
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The grid resolves its own width against available space, so this returns <c>null</c> to let the
	/// layout engine decide (mirrors <see cref="GridControl.ContentWidth"/>).
	/// </remarks>
	public override int? ContentWidth => null;

	/// <inheritdoc/>
	public override System.Drawing.Size GetLogicalContentSize() => _grid.GetLogicalContentSize();

	/// <inheritdoc/>
	/// <remarks>
	/// This control is a tree-participating container (see <see cref="LayoutNodeFactory"/>): the inner
	/// grid is built as its Fill child, so the engine drives measurement of the grid/table subtree via
	/// <see cref="VerticalStackLayout"/>. This node's own <c>MeasureDOM</c> is only reached when the tree
	/// has no children (defensive), so it just measures the grid directly.
	/// </remarks>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		EnsureAttached();
		return ((IDOMPaintable)_grid).MeasureDOM(constraints);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Records this control's bounds and ensures the data source is attached. The grid and table paint
	/// themselves through their own layout nodes (built by <see cref="LayoutNodeFactory"/>), so this
	/// paints nothing of the table itself.
	/// </remarks>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		SetActualBounds(bounds);
		EnsureAttached();
	}

	#endregion

	#region IContainer Implementation

	// This control hosts its inner grid as a child (grid.Container = this via the Container override),
	// so it must satisfy IContainer to provide the window-system + invalidation seam the grid resolves
	// through. Colors are pass-through (the log rows colour themselves by severity).

	private Color _backgroundColor = Color.Transparent;
	private Color _foregroundColor = Color.White;

	/// <inheritdoc/>
	public Color BackgroundColor
	{
		get => _backgroundColor;
		set { _backgroundColor = value; Invalidate(Invalidation.Repaint); }
	}

	/// <inheritdoc/>
	public Color ForegroundColor
	{
		get => _foregroundColor;
		set { _foregroundColor = value; Invalidate(Invalidation.Repaint); }
	}

	/// <inheritdoc/>
	public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

	/// <inheritdoc/>
	public void Invalidate(Invalidation work, IWindowControl? callerControl = null) => Container?.Invalidate(work, callerControl ?? this);

	/// <inheritdoc/>
	public int? GetVisibleHeightForControl(IWindowControl control) => Container?.GetVisibleHeightForControl(control);

	#endregion

	#region Private

	private void EnsureAttached()
	{
		if (_systemAttached) return;
		var system = Container?.GetConsoleWindowSystem;
		if (system == null) return;
		_dataSource.AttachSystem(system);
		_dataSource.RebuildProjection(); // reconcile anything logged before attach
		_systemAttached = true;

		// The window is only reachable once attached, so a hotkey asked for before that
		// (set on the control, or through the builder) is hooked here.
		if (_globalFilterHotkey) HookHotkey();
	}

	/// <summary>
	/// Subscribes to the containing window's key preview. Safe to call repeatedly.
	/// </summary>
	private void HookHotkey()
	{
		var window = Container?.GetConsoleWindowSystem is null ? null : FindWindow();
		if (window == null || ReferenceEquals(window, _hotkeyWindow)) return;

		UnhookHotkey();
		_hotkeyWindow = window;
		window.PreviewKeyPressed += OnWindowPreviewKey;
	}

	private void UnhookHotkey()
	{
		if (_hotkeyWindow == null) return;
		_hotkeyWindow.PreviewKeyPressed -= OnWindowPreviewKey;
		_hotkeyWindow = null;
	}

	/// <summary>Walks up the container chain to the window hosting this control.</summary>
	private Window? FindWindow()
	{
		for (IContainer? c = Container; c != null; c = (c as IWindowControl)?.Container)
			if (c is Window w) return w;
		return null;
	}

	private void OnWindowPreviewKey(object? sender, KeyPressedEventArgs e)
	{
		if (!_globalFilterHotkey || !IsEnabled || !Visible) return;
		if (e.Handled) return;

		// A plain '/' only. Ctrl+/ and friends belong to whoever else wants them.
		if (e.KeyInfo.KeyChar != '/' || e.KeyInfo.Modifiers != 0) return;

		// Decline while another interactive control holds focus: a slash typed into a terminal,
		// an edit box or a prompt is a slash, not a command to us. Focus on a non-interactive
		// control, on nothing, or on this viewer is ours to claim.
		var focused = _hotkeyWindow?.FocusManager?.FocusedControl;
		if (focused is IInteractiveControl && !ReferenceEquals(focused, this) && !OwnsFocused(focused))
			return;

		// Take the keyboard with the filter, onto the TABLE and not merely onto this control.
		// The hotkey fires without the viewer having focus, and the characters typed next travel
		// the ordinary routing path: TableControl.ProcessKey opens with "if (!_isEnabled ||
		// !HasFocus) return false", so the table itself has to hold focus or the prompt opens
		// and silently swallows nothing while the user types.
		_hotkeyWindow?.FocusManager?.SetFocus(_table, FocusReason.Keyboard);

		_table.EnterFilterMode();
		e.Handled = true;
	}

	/// <summary>True when the focused control is this viewer's own inner table or grid.</summary>
	private bool OwnsFocused(object? focused)
		=> ReferenceEquals(focused, _table) || ReferenceEquals(focused, _grid);

	// Explicit AutoScroll-on / unpause: re-attach the table to the tail and jump to the bottom.
	private void ResumeFollow()
	{
		_table.AutoScroll = _autoScroll;
		ApplyTailFollow();
	}

	// Apply-only: pins the viewport to the newest row when the table is still attached to the tail.
	// Follow state is owned by TableControl.AutoScroll, which detaches itself when the user scrolls
	// up — so this runs safely on EVERY CollectionChanged action (single Add, batch, eviction Reset,
	// filter Reset) without needing to know which one arrived.
	private void ApplyTailFollow()
	{
		if (!_autoScroll || _isPaused || !_table.AutoScroll) return;
		int visible = Math.Max(1, _table.GetVisibleRowCount());
		_table.ScrollOffset = Math.Max(0, _table.RowCount - visible);
	}

	#endregion

	#region Dispose

	/// <inheritdoc/>
	protected override void OnDisposing()
	{
		_dataSource.Dispose();
		_grid.Dispose();
	}

	#endregion

	/// <inheritdoc/>
	public override void DescribeState(IStateWriter w)
	{
		ArgumentNullException.ThrowIfNull(w);
		base.DescribeState(w);

		// Paused is the first thing to check when a log "stops working".
		w.Write("paused", IsPaused);
	}

}
