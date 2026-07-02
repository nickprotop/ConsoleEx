// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Configuration;
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
			ColCategory => e.Category ?? string.Empty,
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

	/// <summary>Rebuilds the displayed projection from the full buffer under the current view filters.</summary>
	public void RebuildProjection()
	{
		lock (_lock)
		{
			_displayed.Clear();
			foreach (var e in _logService.GetAllLogs())
				if (PassesFilter(e))
					_displayed.Add(e);
		}
		CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
		if (system != null) system.EnqueueOnUIThread(action);
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
