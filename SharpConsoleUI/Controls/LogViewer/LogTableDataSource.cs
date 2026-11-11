// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Virtual <see cref="ITableDataSource"/> that projects <see cref="ILogService"/> entries into a
/// four-column table (Time / Level / Category / Message). Only visible rows are queried by the
/// hosting <see cref="TableControl"/>, so it scales to the full log buffer without a control-per-line.
/// View-level filtering (level, category, search) hides entries without discarding them.
/// </summary>
internal sealed class LogTableDataSource : ITableDataSource, IDisposable
{
	private const int ColTime = 0;
	private const int ColLevel = 1;
	private const int ColCategory = 2;
	private const int ColMessage = 3;

	private static readonly string[] Headers = { "Time", "Level", "Category", "Message" };

	private readonly ILogService _logService;
	private readonly object _lock = new();

	// Displayed (post view-filter) projection. Lightweight records, NOT controls.
	private readonly List<LogEntry> _displayed = new();

	private ConsoleWindowSystem? _system;
	private LogLevel _viewFilterLevel = LogLevel.Trace;
	private string? _viewFilterCategory;
	private string? _searchText;
	private bool _subscribed;

	// Sort state. null = the buffer's own order (oldest first), which is what a log reader
	// wants by default and what tail-follow assumes.
	private int _sortColumn = -1;
	private SortDirection _sortDirection = SortDirection.None;

	/// <summary>Creates a data source bound to the given log service and loads its current buffer.</summary>
	public LogTableDataSource(ILogService logService)
	{
		_logService = logService ?? throw new ArgumentNullException(nameof(logService));
		RebuildProjection();
	}

	/// <inheritdoc/>
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	/// <summary>The minimum level shown by the VIEW filter (does not discard captured entries).</summary>
	public LogLevel ViewFilterLevel
	{
		get { lock (_lock) return _viewFilterLevel; }
		set { lock (_lock) _viewFilterLevel = value; RebuildProjection(); }
	}

	/// <summary>Category the view filter restricts to; null shows all categories.</summary>
	public string? ViewFilterCategory
	{
		get { lock (_lock) return _viewFilterCategory; }
		set { lock (_lock) _viewFilterCategory = value; RebuildProjection(); }
	}

	/// <summary>Case-insensitive substring filter over message and category; null/empty shows all.</summary>
	public string? SearchText
	{
		get { lock (_lock) return _searchText; }
		set { lock (_lock) _searchText = value; RebuildProjection(); }
	}

	/// <summary>Snapshot of the currently displayed entries (post view-filter), oldest first.</summary>
	public IReadOnlyList<LogEntry> DisplayedEntries
	{
		get { lock (_lock) return _displayed.ToList(); }
	}

	/// <inheritdoc/>
	public int RowCount { get { lock (_lock) return _displayed.Count; } }

	/// <inheritdoc/>
	public int ColumnCount => 4;

	/// <inheritdoc/>
	public string GetColumnHeader(int columnIndex) => Headers[columnIndex];

	/// <inheritdoc/>
	public TextJustification GetColumnAlignment(int columnIndex) => TextJustification.Left;

	/// <inheritdoc/>
	public int? GetColumnWidth(int columnIndex) => columnIndex switch
	{
		ColTime => ControlDefaults.LogViewerTimeColumnWidth,
		ColLevel => ControlDefaults.LogViewerLevelColumnWidth,
		ColCategory => ControlDefaults.LogViewerCategoryColumnWidth,
		_ => null // Message: auto/fill
	};

	/// <inheritdoc/>
	public string GetCellValue(int rowIndex, int columnIndex)
	{
		LogEntry e;
		lock (_lock)
		{
			if (rowIndex < 0 || rowIndex >= _displayed.Count) return string.Empty;
			e = _displayed[rowIndex];
		}
		return columnIndex switch
		{
			ColTime => e.Timestamp.ToString("HH:mm:ss"),
			ColLevel => LevelText(e.Level),
			ColCategory => CategoryText(e.Category),
			ColMessage => MessageText(e),
			_ => string.Empty
		};
	}

	/// <inheritdoc/>
	public Color? GetRowForegroundColor(int rowIndex)
	{
		lock (_lock)
		{
			if (rowIndex < 0 || rowIndex >= _displayed.Count) return null;
			return LogSeverityColors.ForLevel(_displayed[rowIndex].Level);
		}
	}

	/// <summary>Returns the entry backing a display row, or null if out of range.</summary>
	public LogEntry? EntryAt(int rowIndex)
	{
		lock (_lock)
		{
			if (rowIndex < 0 || rowIndex >= _displayed.Count) return null;
			return _displayed[rowIndex];
		}
	}

	/// <summary>
	/// Every column of the log table is sortable.
	/// </summary>
	/// <remarks>
	/// <see cref="TableControl.SortByColumn"/> refuses outright when the attached source says a
	/// column cannot be sorted, so this is what makes header-click sorting work at all for logs.
	/// </remarks>
	public bool CanSort(int columnIndex) => columnIndex >= ColTime && columnIndex <= ColMessage;

	/// <inheritdoc/>
	public void Sort(int columnIndex, SortDirection direction)
	{
		lock (_lock)
		{
			_sortColumn = columnIndex;
			_sortDirection = direction;
		}
		RebuildProjection();
	}

	/// <summary>Restores the buffer's own order, oldest first.</summary>
	public void ClearSort()
	{
		lock (_lock)
		{
			_sortColumn = -1;
			_sortDirection = SortDirection.None;
		}
		RebuildProjection();
	}

	/// <summary>Rebuilds the displayed projection from the full buffer under the current view filters.</summary>
	public void RebuildProjection()
	{
		lock (_lock)
		{
			_displayed.Clear();
			foreach (var e in _logService.GetAllLogs())
				if (PassesFilter(e))
					_displayed.Add(e);

			ApplySortLocked();
		}
		CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	/// <summary>
	/// Orders the projection in place. Caller holds <c>_lock</c>.
	/// </summary>
	/// <remarks>
	/// Sorts on the UNDERLYING values, not on the rendered cell text: Level sorts by severity
	/// (Trace..Critical) rather than alphabetically, where "ERROR" would fall before "WARNING",
	/// and Time sorts by the timestamp rather than its "HH:mm:ss" rendering, which collapses
	/// entries from different days and loses sub-second order.
	/// </remarks>
	private void ApplySortLocked()
	{
		if (_sortColumn < 0 || _sortDirection == SortDirection.None) return;

		Comparison<LogEntry> compare = _sortColumn switch
		{
			ColTime => (a, b) => a.Timestamp.CompareTo(b.Timestamp),
			ColLevel => (a, b) => ((int)a.Level).CompareTo((int)b.Level),
			ColCategory => (a, b) => string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase),
			ColMessage => (a, b) => string.Compare(a.Message, b.Message, StringComparison.OrdinalIgnoreCase),
			_ => (_, _) => 0
		};

		// Stable within equal keys: List.Sort is not stable, so ties fall back to arrival order,
		// which for a log is the meaningful secondary key (every Error at the same second stays
		// in the order it happened).
		//
		// Decorate-sort-undecorate rather than a dictionary of entry -> index: LogEntry is a
		// record, so two identical lines (same timestamp, level, category and message, which a
		// log produces all the time) are EQUAL by value and would share one dictionary slot,
		// corrupting the tie-break. The index travels beside the entry instead.
		int descending = _sortDirection == SortDirection.Descending ? -1 : 1;

		var indexed = new (LogEntry Entry, int Arrival)[_displayed.Count];
		for (int i = 0; i < _displayed.Count; i++) indexed[i] = (_displayed[i], i);

		Array.Sort(indexed, (x, y) =>
		{
			int primary = compare(x.Entry, y.Entry);
			if (primary != 0) return descending * primary;
			return x.Arrival.CompareTo(y.Arrival);
		});

		_displayed.Clear();
		foreach (var item in indexed) _displayed.Add(item.Entry);
	}

	private bool PassesFilter(LogEntry e)
	{
		if (e.Level < _viewFilterLevel) return false;
		if (_viewFilterCategory != null && e.Category != _viewFilterCategory) return false;
		if (!string.IsNullOrEmpty(_searchText))
		{
			var s = _searchText;
			bool inMsg = e.Message.Contains(s, StringComparison.OrdinalIgnoreCase);
			bool inCat = e.Category != null && e.Category.Contains(s, StringComparison.OrdinalIgnoreCase);
			if (!inMsg && !inCat) return false;
		}
		return true;
	}

	/// <summary>
	/// Fits a category into the category column, keeping the END of a dotted name.
	/// </summary>
	/// <remarks>
	/// The table painter truncates from the left, which is right for a general cell but wrong
	/// for a logger category: a <c>Microsoft.Extensions.Logging</c> category is a full type name
	/// (<c>MyApp.Services.OrderProcessor</c>), and its leading namespace is the part that carries
	/// no information while the trailing type name is the part the reader is looking for. Cutting
	/// from the left of that string shows "MyApp.Services" and hides "OrderProcessor".
	/// <para>
	/// So a dotted name that does not fit is reduced from the FRONT, dropping whole segments while
	/// they can be dropped, then character-trimmed if even the last segment is too long. The
	/// library's own categories ("Window", "Focus", "Render") hold no dot and always fit, so they
	/// are returned untouched. Only the DISPLAYED text changes: <see cref="LogEntry.Category"/>
	/// keeps the full value, and the category filter and the search both read that, so a full type
	/// name stays matchable even when it is shown shortened.
	/// </para>
	/// </remarks>
	internal static string CategoryText(string? category)
	{
		if (string.IsNullOrEmpty(category)) return string.Empty;

		int width = ControlDefaults.LogViewerCategoryColumnWidth;
		if (width <= 0) return string.Empty;

		// Measured in display columns, not UTF-16 units: a category may hold wide or
		// combining characters, and the column budget is columns.
		if (UnicodeWidth.GetStringWidth(category) <= width) return category;

		// Drop leading dotted segments while a later one still fits.
		int dot;
		string candidate = category;
		while ((dot = candidate.IndexOf('.')) >= 0)
		{
			candidate = candidate[(dot + 1)..];
			if (UnicodeWidth.GetStringWidth(candidate) <= width) return candidate;
		}

		// The last segment alone is still too wide: keep its tail, which is the
		// most specific part of a type name.
		return TrimToWidthFromEnd(candidate, width);
	}

	/// <summary>Keeps the trailing <paramref name="width"/> display columns of <paramref name="text"/>.</summary>
	private static string TrimToWidthFromEnd(string text, int width)
	{
		var runes = text.EnumerateRunes().ToList();
		int used = 0;
		int firstKept = runes.Count;

		for (int i = runes.Count - 1; i >= 0; i--)
		{
			int w = UnicodeWidth.GetRuneWidth(runes[i]);
			if (w < 0) w = 0;
			if (used + w > width) break;
			used += w;
			firstKept = i;
		}

		var sb = new System.Text.StringBuilder(width);
		for (int i = firstKept; i < runes.Count; i++)
			sb.Append(runes[i].ToString());
		return sb.ToString();
	}

	private static string LevelText(LogLevel level) => level switch
	{
		LogLevel.Information => "INFO",
		_ => level.ToString().ToUpperInvariant()
	};

	private static string MessageText(LogEntry e)
	{
		// Single-line for the dense row; full text + exception live in the detail pane.
		var msg = e.Message.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
		if (e.Exception != null)
			msg += $" | {e.Exception.GetType().Name}: {e.Exception.Message}";
		return msg;
	}

	/// <summary>
	/// Attaches the window system used to marshal background log events onto the UI thread, and
	/// subscribes to the log service. Log events arrive on background threads (async window threads);
	/// mutating the projection and raising CollectionChanged must happen on the UI thread and never
	/// during a measure/arrange pass, so all handling is marshalled via EnqueueOnUIThread.
	/// </summary>
	public void AttachSystem(ConsoleWindowSystem system)
	{
		_system = system ?? throw new ArgumentNullException(nameof(system));
		if (_subscribed) return;
		_logService.LogAdded += OnLogAdded;
		_logService.LogsCleared += OnLogsCleared;
		_subscribed = true;
	}

	private void OnLogAdded(object? sender, LogEntry entry) => Marshal(() => ApplyAdded(entry));

	private void OnLogsCleared(object? sender, EventArgs e) => Marshal(RebuildProjection);

	private void Marshal(Action action)
	{
		var system = _system;
		if (system != null) system.EnqueueOnUIThread(action, "logTableDataSource.Marshal");
		else action(); // not attached yet: apply directly (creation-thread path)
	}

	private void ApplyAdded(LogEntry entry)
	{
		// When the source buffer is at (or over) capacity, an add has likely evicted its oldest entry.
		// The log service trims silently with no event, so an incremental append would leave _displayed
		// holding stale/evicted entries and — under a restrictive view filter that keeps _displayed small
		// — never trim, letting it grow unbounded until a filter change collapses it abruptly.
		//
		// Reconcile by rebuilding the projection straight from the live source under the current filter.
		// This keeps _displayed a true subset of the source (so it can never exceed MaxBufferSize) and
		// drops evicted entries eagerly. Below capacity, nothing is evicted, so take the fast incremental
		// Add path. RebuildProjection raises CollectionChanged outside the lock (its own guarantee).
		if (_logService.Count >= _logService.MaxBufferSize)
		{
			RebuildProjection();
			return;
		}

		int newIndex;
		lock (_lock)
		{
			if (!PassesFilter(entry)) return;
			_displayed.Add(entry);
			newIndex = _displayed.Count - 1;
		}
		CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, entry, newIndex));
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (!_subscribed) return;
		_logService.LogAdded -= OnLogAdded;
		_logService.LogsCleared -= OnLogsCleared;
		_subscribed = false;
	}
}
