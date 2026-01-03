// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A control that displays log entries from the library's LogService.
/// Automatically updates when new log entries are added.
/// </summary>
public class LogViewerControl : IWindowControl, IInteractiveControl
{
    private readonly ILogService _logService;
    private readonly ThreadSafeCache<List<string>> _contentCache;
    private readonly object _logsLock = new object();  // Lock for thread-safe access to _displayedLogs
    private readonly List<LogEntry> _displayedLogs = new();
    private int _localScrollOffset = 0;  // Local fallback for scroll offset
    private int _maxDisplayLines = 10;
    private bool _autoScroll = true;
    private bool _hasFocus;
    private LogLevel _filterLevel = LogLevel.Trace;
    private string? _filterCategory;
    private Alignment _alignment = Alignment.Left;
    private Margin _margin = new Margin(0, 0, 0, 0);
    private StickyPosition _stickyPosition = StickyPosition.None;
    private bool _visible = true;
    private int? _width;
    private string? _title;
    private volatile bool _disposed = false;

    // Convenience property to access ScrollStateService
    private ScrollStateService? ScrollService => Container?.GetConsoleWindowSystem?.ScrollStateService;

    // Read scroll offset from state service (single source of truth)
    private int CurrentScrollOffset => ScrollService?.GetVerticalOffset(this) ?? _localScrollOffset;

    // Helper to set scroll offset - updates both service and local fallback
    private void SetScrollOffset(int offset)
    {
        _localScrollOffset = Math.Max(0, offset);
        int logCount;
        lock (_logsLock)
        {
            logCount = _displayedLogs.Count;
        }
        ScrollService?.UpdateDimensions(this, 0, logCount, 0, _maxDisplayLines);
        ScrollService?.SetVerticalOffset(this, _localScrollOffset);
    }

    /// <summary>
    /// Creates a new LogViewerControl bound to the specified log service
    /// </summary>
    /// <param name="logService">The log service to display entries from</param>
    public LogViewerControl(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _contentCache = this.CreateThreadSafeCache<List<string>>();
        _logService.LogAdded += OnLogAdded;
        _logService.LogsCleared += OnLogsCleared;
        RefreshLogs();
    }

    #region IWindowControl Properties

    /// <summary>
    /// Gets the actual rendered width of the control content in characters.
    /// </summary>
    public int? ActualWidth
    {
        get
        {
            if (_contentCache.Content == null) return null;
            int maxLength = 0;
            foreach (var line in _contentCache.Content)
            {
                int length = AnsiConsoleHelper.StripAnsiStringLength(line);
                if (length > maxLength) maxLength = length;
            }
            return maxLength;
        }
    }

    /// <inheritdoc/>
    public Alignment Alignment
    {
        get => _alignment;
        set
        {
            _alignment = value;
            _contentCache.Invalidate(InvalidationReason.PropertyChanged);
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public IContainer? Container { get; set; }

    /// <inheritdoc/>
    public Margin Margin
    {
        get => _margin;
        set
        {
            _margin = value;
            _contentCache.Invalidate(InvalidationReason.PropertyChanged);
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public StickyPosition StickyPosition
    {
        get => _stickyPosition;
        set
        {
            _stickyPosition = value;
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public object? Tag { get; set; }

    /// <inheritdoc/>
    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            _contentCache.Invalidate(InvalidationReason.PropertyChanged);
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public int? Width
    {
        get => _width;
        set
        {
            var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
            if (_width != validatedValue)
            {
                _width = validatedValue;
                _contentCache.Invalidate(InvalidationReason.SizeChanged);
                Container?.Invalidate(true);
            }
        }
    }

    #endregion

    #region IInteractiveControl Properties

    /// <inheritdoc/>
    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            _hasFocus = value;
            _contentCache.Invalidate(InvalidationReason.FocusChanged);
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    #endregion

    #region LogViewerControl Properties

    /// <summary>
    /// Gets or sets the maximum number of log lines to display
    /// </summary>
    public int MaxDisplayLines
    {
        get => _maxDisplayLines;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            _maxDisplayLines = value;
            _contentCache.Invalidate(InvalidationReason.SizeChanged);
            Container?.Invalidate(true);
        }
    }

    /// <summary>
    /// Gets or sets whether to automatically scroll to show new log entries
    /// </summary>
    public bool AutoScroll
    {
        get => _autoScroll;
        set => _autoScroll = value;
    }

    /// <summary>
    /// Gets or sets the minimum log level to display (filters out lower levels)
    /// </summary>
    public LogLevel FilterLevel
    {
        get => _filterLevel;
        set
        {
            _filterLevel = value;
            RefreshLogs();
        }
    }

    /// <summary>
    /// Gets or sets the category filter (null means show all categories)
    /// </summary>
    public string? FilterCategory
    {
        get => _filterCategory;
        set
        {
            _filterCategory = value;
            RefreshLogs();
        }
    }

    /// <summary>
    /// Gets or sets a title to display above the log entries
    /// </summary>
    public string? Title
    {
        get => _title;
        set
        {
            _title = value;
            _contentCache.Invalidate(InvalidationReason.PropertyChanged);
            Container?.Invalidate(true);
        }
    }

    #endregion

    #region IWindowControl Methods

    /// <inheritdoc/>
    public System.Drawing.Size GetLogicalContentSize()
    {
        int height = _maxDisplayLines + (string.IsNullOrEmpty(_title) ? 0 : 1);
        int width = _width ?? 80;
        return new System.Drawing.Size(width, height);
    }

    /// <inheritdoc/>
    public List<string> RenderContent(int? availableWidth, int? availableHeight)
    {
        var layoutService = Container?.GetConsoleWindowSystem?.LayoutStateService;

        // Smart invalidation: check if re-render is needed due to size change
        if (layoutService == null || layoutService.NeedsRerender(this, availableWidth, availableHeight))
        {
            _contentCache.Invalidate(InvalidationReason.SizeChanged);
        }
        else
        {
            var cached = _contentCache.Content;
            if (cached != null) return cached;
        }

        // Update available space tracking
        layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

        return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
    }

    private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
    {
        var lines = new List<string>();
        int targetWidth = _width ?? availableWidth ?? 80;
        int displayHeight = availableHeight ?? _maxDisplayLines;
        var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
        var fgColor = Container?.ForegroundColor ?? Spectre.Console.Color.White;

        // Account for title
        int contentHeight = string.IsNullOrEmpty(_title) ? displayHeight : displayHeight - 1;

        // Add title if set
        if (!string.IsNullOrEmpty(_title))
        {
            var titleColor = _hasFocus ? "cyan" : "dim";
            var titleMarkup = $"[{titleColor}]{_title}[/]";
            var titleLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(titleMarkup, targetWidth, 1, false, bgColor, fgColor);
            lines.AddRange(titleLines);
        }

        // Get visible logs - lock to prevent concurrent modification
        int scrollOffset = CurrentScrollOffset;
        List<LogEntry> visibleLogs;
        int totalFiltered;
        lock (_logsLock)
        {
            visibleLogs = _displayedLogs
                .Skip(scrollOffset)
                .Take(contentHeight)
                .ToList();  // Copy inside lock
            totalFiltered = _displayedLogs.Count;
        }

        if (visibleLogs.Count == 0)
        {
            var emptyLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi("[dim]No log entries[/]", targetWidth, 1, false, bgColor, fgColor);
            lines.AddRange(emptyLines);
        }
        else
        {
            foreach (var entry in visibleLogs)
            {
                var entryLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(entry.ToMarkup(), targetWidth, 1, false, bgColor, fgColor);
                lines.AddRange(entryLines);
            }
        }

        // Add scroll indicator if there are more entries
        if (totalFiltered > contentHeight && visibleLogs.Count > 0)
        {
            var scrollInfo = $"[dim]({scrollOffset + 1}-{Math.Min(scrollOffset + contentHeight, totalFiltered)} of {totalFiltered})[/]";
            var scrollLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(scrollInfo, targetWidth, 1, false, bgColor, fgColor);
            lines.AddRange(scrollLines);
        }

        // Apply alignment padding
        for (int i = 0; i < lines.Count; i++)
        {
            int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(lines[i]);
            if (lineWidth < targetWidth)
            {
                int totalPadding = targetWidth - lineWidth;

                switch (_alignment)
                {
                    case Alignment.Center:
                        int leftPad = totalPadding / 2;
                        int rightPad = totalPadding - leftPad;
                        lines[i] = AnsiConsoleHelper.AnsiEmptySpace(leftPad, bgColor)
                            + lines[i]
                            + AnsiConsoleHelper.AnsiEmptySpace(rightPad, bgColor);
                        break;
                    case Alignment.Right:
                        lines[i] = AnsiConsoleHelper.AnsiEmptySpace(totalPadding, bgColor)
                            + lines[i];
                        break;
                    default: // Left or Stretch
                        lines[i] = lines[i]
                            + AnsiConsoleHelper.AnsiEmptySpace(totalPadding, bgColor);
                        break;
                }
            }
            else if (lineWidth > targetWidth)
            {
                lines[i] = AnsiConsoleHelper.SubstringAnsi(lines[i], 0, targetWidth);
            }
        }

        return lines;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        _contentCache.Invalidate(InvalidationReason.ContentChanged);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;  // Set flag FIRST to prevent event handlers from running

        _logService.LogAdded -= OnLogAdded;
        _logService.LogsCleared -= OnLogsCleared;
        _contentCache.Dispose();
        Container = null;
    }

    #endregion

    #region IInteractiveControl Methods

    /// <inheritdoc/>
    public bool ProcessKey(ConsoleKeyInfo keyInfo)
    {
        if (!IsEnabled) return false;

        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                ScrollUp();
                return true;

            case ConsoleKey.DownArrow:
                ScrollDown();
                return true;

            case ConsoleKey.PageUp:
                ScrollUp(_maxDisplayLines);
                return true;

            case ConsoleKey.PageDown:
                ScrollDown(_maxDisplayLines);
                return true;

            case ConsoleKey.Home:
                SetScrollOffset(0);
                _autoScroll = false;
                _contentCache.Invalidate(InvalidationReason.StateChanged);
                Container?.Invalidate(true);
                return true;

            case ConsoleKey.End:
                ScrollToEnd();
                _autoScroll = true;
                return true;

            case ConsoleKey.Delete:
            case ConsoleKey.C when keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control):
                _logService.ClearLogs();
                return true;

            default:
                return false;
        }
    }

    #endregion

    #region Scroll Methods

    /// <summary>
    /// Scrolls up by the specified number of lines.
    /// </summary>
    /// <param name="lines">The number of lines to scroll.</param>
    public void ScrollUp(int lines = 1)
    {
        int newOffset = Math.Max(0, CurrentScrollOffset - lines);
        SetScrollOffset(newOffset);
        _autoScroll = false;
        _contentCache.Invalidate(InvalidationReason.StateChanged);
        Container?.Invalidate(true);
    }

    /// <summary>
    /// Scrolls down by the specified number of lines.
    /// </summary>
    /// <param name="lines">The number of lines to scroll.</param>
    public void ScrollDown(int lines = 1)
    {
        int logCount;
        lock (_logsLock)
        {
            logCount = _displayedLogs.Count;
        }
        var maxOffset = Math.Max(0, logCount - _maxDisplayLines);
        int newOffset = Math.Min(maxOffset, CurrentScrollOffset + lines);
        SetScrollOffset(newOffset);
        _contentCache.Invalidate(InvalidationReason.StateChanged);
        Container?.Invalidate(true);
    }

    /// <summary>
    /// Scrolls to show the most recent log entries.
    /// </summary>
    public void ScrollToEnd()
    {
        int logCount;
        lock (_logsLock)
        {
            logCount = _displayedLogs.Count;
        }
        int newOffset = Math.Max(0, logCount - _maxDisplayLines);
        SetScrollOffset(newOffset);
        _contentCache.Invalidate(InvalidationReason.StateChanged);
        Container?.Invalidate(true);
    }

    #endregion

    #region Private Methods

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        // Early exit if disposed - event might fire during disposal
        if (_disposed) return;

        // Check if entry passes filter
        if (entry.Level < _filterLevel)
            return;

        if (_filterCategory != null && entry.Category != _filterCategory)
            return;

        int newOffset = 0;
        bool needsScrollUpdate = false;

        lock (_logsLock)
        {
            _displayedLogs.Add(entry);

            // Trim if we have too many
            while (_displayedLogs.Count > _logService.MaxBufferSize)
            {
                _displayedLogs.RemoveAt(0);
                int currentOffset = CurrentScrollOffset;
                if (currentOffset > 0)
                {
                    _localScrollOffset = Math.Max(0, currentOffset - 1);
                }
            }

            if (_autoScroll)
            {
                newOffset = Math.Max(0, _displayedLogs.Count - _maxDisplayLines);
                needsScrollUpdate = true;
            }
        }

        // Update scroll state OUTSIDE the lock to avoid potential deadlock
        if (needsScrollUpdate)
        {
            SetScrollOffset(newOffset);
        }

        // Invalidate OUTSIDE the lock to avoid deadlock
        _contentCache.Invalidate(InvalidationReason.ContentChanged);
        Container?.Invalidate(true);
    }

    private void OnLogsCleared(object? sender, EventArgs e)
    {
        // Early exit if disposed - event might fire during disposal
        if (_disposed) return;

        lock (_logsLock)
        {
            _displayedLogs.Clear();
        }
        SetScrollOffset(0);
        _contentCache.Invalidate(InvalidationReason.ContentChanged);
        Container?.Invalidate(true);
    }

    private void RefreshLogs()
    {
        int newOffset = 0;

        lock (_logsLock)
        {
            _displayedLogs.Clear();
        }
        SetScrollOffset(0);

        var allLogs = _logService.GetAllLogs();
        lock (_logsLock)
        {
            foreach (var entry in allLogs)
            {
                if (entry.Level >= _filterLevel)
                {
                    if (_filterCategory == null || entry.Category == _filterCategory)
                    {
                        _displayedLogs.Add(entry);
                    }
                }
            }

            if (_autoScroll)
            {
                newOffset = Math.Max(0, _displayedLogs.Count - _maxDisplayLines);
            }
        }

        if (_autoScroll)
        {
            SetScrollOffset(newOffset);
        }

        _contentCache.Invalidate(InvalidationReason.ContentChanged);
        Container?.Invalidate(true);
    }

    #endregion
}
