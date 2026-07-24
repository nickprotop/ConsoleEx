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
	private PromptControl? _searchBox;
	private bool _isPaused;
	private bool _showToolbar = true;

	private MarkupControl? _detailContent;
	private ScrollablePanelControl? _detailPanel;
	private bool _showDetailPane = true;
	private const int DetailPaneRow = 2;

	private bool _autoScroll = true;
	private string? _title;
	private bool _systemAttached;

	/// <summary>Creates a new LogViewerControl bound to the specified log service.</summary>
	/// <param name="logService">The log service to display entries from.</param>
	public LogViewerControl(ILogService logService)
	{
		_logService = logService ?? throw new ArgumentNullException(nameof(logService));

		_dataSource = new LogTableDataSource(_logService);

		_table = new TableControl
		{
			ShowHeader = true,
			TruncationFade = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = _autoScroll,   // seed from the viewer's default intent (true)
			DataSource = _dataSource
		};

		_levelDropdown = BuildLevelDropdown();
		_searchBox = BuildSearchBox();
		_toolbar = BuildToolbar(_levelDropdown, _searchBox);

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

	private PromptControl BuildSearchBox()
	{
		var p = new PromptControl { Prompt = "Search:", InputWidth = ControlDefaults.LogViewerCategoryColumnWidth };
		p.InputChanged += (_, text) => _dataSource.SearchText = string.IsNullOrEmpty(text) ? null : text;
		return p;
	}

	private ToolbarControl BuildToolbar(DropdownControl level, PromptControl search)
	{
		var toolbar = new ToolbarControl { Visible = _showToolbar };
		toolbar.AddItem(level);
		toolbar.AddItem(search);
		toolbar.AddItem(Builders.Controls.Button("Pause").OnClick((_, _) => TogglePause()).Build());
		toolbar.AddItem(Builders.Controls.Button("Clear").OnClick((_, _) => _logService.ClearLogs()).Build());
		toolbar.AddItem(Builders.Controls.Button("Export").OnClick((_, _) => ExportAsync()).Build());
		return toolbar;
	}

	private void TogglePause() => IsPaused = !IsPaused;

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
	}

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
}
