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
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using SharpConsoleUI.Logging;
using Spectre.Console;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A control that displays log entries from the library's LogService.
/// Automatically updates when new log entries are added.
/// </summary>
public class LogViewerControl : IWindowControl, IInteractiveControl, IDOMPaintable
{
    private readonly ILogService _logService;
    private readonly object _logsLock = new object();  // Lock for thread-safe access to _displayedLogs
    private readonly List<LogEntry> _displayedLogs = new();
    private int _scrollOffset = 0;  // Local fallback for scroll offset
    private int _maxDisplayLines = 10;
    private bool _autoScroll = true;
    private bool _hasFocus;
    private LogLevel _filterLevel = LogLevel.Trace;
    private string? _filterCategory;
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
    private Margin _margin = new Margin(0, 0, 0, 0);
    private StickyPosition _stickyPosition = StickyPosition.None;
    private bool _visible = true;
    private int? _width;
    private string? _title;
    private volatile bool _disposed = false;

    private int CurrentScrollOffset => _scrollOffset;

    // Helper to set scroll offset
    private void SetScrollOffset(int offset)
    {
        _scrollOffset = Math.Max(0, offset);
    }

    /// <summary>
    /// Creates a new LogViewerControl bound to the specified log service
    /// </summary>
    /// <param name="logService">The log service to display entries from</param>
    public LogViewerControl(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
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
            return _width ?? 80 + _margin.Left + _margin.Right;
        }
    }

    /// <inheritdoc/>
    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            _horizontalAlignment = value;
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public VerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set
        {
            _verticalAlignment = value;
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
    public string? Name { get; set; }

    /// <inheritdoc/>
    public object? Tag { get; set; }

    /// <inheritdoc/>
    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
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
    public void Invalidate()
    {
        Container?.Invalidate(true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;  // Set flag FIRST to prevent event handlers from running

        _logService.LogAdded -= OnLogAdded;
        _logService.LogsCleared -= OnLogsCleared;
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
                    _scrollOffset = Math.Max(0, currentOffset - 1);
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

        Container?.Invalidate(true);
    }

    #endregion

    #region IDOMPaintable Implementation

    /// <inheritdoc/>
    public LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        int contentWidth = constraints.MaxWidth - _margin.Left - _margin.Right;

        // Calculate height: title line + visible log lines + possible scroll indicator
        int logCount;
        lock (_logsLock)
        {
            logCount = _displayedLogs.Count;
        }

        int titleHeight = string.IsNullOrEmpty(_title) ? 0 : 1;
        int contentHeight = Math.Min(_maxDisplayLines, logCount);
        if (contentHeight == 0) contentHeight = 1; // "No log entries" message

        // Add scroll indicator line if there are more entries than visible
        int scrollIndicatorHeight = logCount > _maxDisplayLines ? 1 : 0;

        int totalHeight = titleHeight + contentHeight + scrollIndicatorHeight + _margin.Top + _margin.Bottom;
        int width = (_width ?? contentWidth) + _margin.Left + _margin.Right;

        return new LayoutSize(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(totalHeight, constraints.MinHeight, constraints.MaxHeight)
        );
    }

    /// <inheritdoc/>
    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        var bgColor = Container?.BackgroundColor ?? defaultBg;
        var fgColor = Container?.ForegroundColor ?? defaultFg;
        int targetWidth = bounds.Width - _margin.Left - _margin.Right;

        if (targetWidth <= 0) return;

        int startX = bounds.X + _margin.Left;
        int startY = bounds.Y + _margin.Top;
        int currentY = startY;

        // Fill top margin
        for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
        {
            if (y >= clipRect.Y && y < clipRect.Bottom)
            {
                buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
            }
        }

        // Render title if set
        int contentHeight = bounds.Height - _margin.Top - _margin.Bottom;
        if (!string.IsNullOrEmpty(_title))
        {
            if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
            {
                // Fill left margin
                if (_margin.Left > 0)
                {
                    buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', fgColor, bgColor);
                }

                var titleColor = _hasFocus ? Color.Cyan1 : Color.Grey;
                var titleText = _title.Length > targetWidth ? _title.Substring(0, targetWidth) : _title;

                // Apply alignment
                int alignOffset = 0;
                if (titleText.Length < targetWidth)
                {
                    switch (_horizontalAlignment)
                    {
                        case HorizontalAlignment.Center:
                            alignOffset = (targetWidth - titleText.Length) / 2;
                            break;
                        case HorizontalAlignment.Right:
                            alignOffset = targetWidth - titleText.Length;
                            break;
                    }
                }

                // Fill left alignment padding
                if (alignOffset > 0)
                {
                    buffer.FillRect(new LayoutRect(startX, currentY, alignOffset, 1), ' ', fgColor, bgColor);
                }

                // Write title
                for (int i = 0; i < titleText.Length && startX + alignOffset + i < clipRect.Right; i++)
                {
                    int x = startX + alignOffset + i;
                    if (x >= clipRect.X)
                    {
                        buffer.SetCell(x, currentY, titleText[i], titleColor, bgColor);
                    }
                }

                // Fill right padding
                int rightPadStart = startX + alignOffset + titleText.Length;
                int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
                if (rightPadWidth > 0)
                {
                    buffer.FillRect(new LayoutRect(rightPadStart, currentY, rightPadWidth, 1), ' ', fgColor, bgColor);
                }

                // Fill right margin
                if (_margin.Right > 0)
                {
                    buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', fgColor, bgColor);
                }
            }
            currentY++;
            contentHeight--;
        }

        // Get visible logs
        int scrollOffset = CurrentScrollOffset;
        List<LogEntry> visibleLogs;
        int totalFiltered;
        lock (_logsLock)
        {
            visibleLogs = _displayedLogs
                .Skip(scrollOffset)
                .Take(contentHeight - 1) // Leave room for scroll indicator if needed
                .ToList();
            totalFiltered = _displayedLogs.Count;
        }

        // Render log entries or empty message
        if (visibleLogs.Count == 0)
        {
            if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
            {
                // Fill left margin
                if (_margin.Left > 0)
                {
                    buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', fgColor, bgColor);
                }

                var emptyText = "No log entries";
                var emptyColor = Color.Grey;

                // Apply alignment
                int alignOffset = 0;
                if (emptyText.Length < targetWidth)
                {
                    switch (_horizontalAlignment)
                    {
                        case HorizontalAlignment.Center:
                            alignOffset = (targetWidth - emptyText.Length) / 2;
                            break;
                        case HorizontalAlignment.Right:
                            alignOffset = targetWidth - emptyText.Length;
                            break;
                    }
                }

                if (alignOffset > 0)
                {
                    buffer.FillRect(new LayoutRect(startX, currentY, alignOffset, 1), ' ', fgColor, bgColor);
                }

                for (int i = 0; i < emptyText.Length && startX + alignOffset + i < clipRect.Right; i++)
                {
                    int x = startX + alignOffset + i;
                    if (x >= clipRect.X)
                    {
                        buffer.SetCell(x, currentY, emptyText[i], emptyColor, bgColor);
                    }
                }

                int rightPadStart = startX + alignOffset + emptyText.Length;
                int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
                if (rightPadWidth > 0)
                {
                    buffer.FillRect(new LayoutRect(rightPadStart, currentY, rightPadWidth, 1), ' ', fgColor, bgColor);
                }

                if (_margin.Right > 0)
                {
                    buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', fgColor, bgColor);
                }
            }
            currentY++;
        }
        else
        {
            foreach (var entry in visibleLogs)
            {
                if (currentY >= bounds.Bottom) break;

                if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
                {
                    // Fill left margin
                    if (_margin.Left > 0)
                    {
                        buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', fgColor, bgColor);
                    }

                    // Render log entry using Spectre markup
                    var entryMarkup = entry.ToMarkup();
                    var ansiLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(entryMarkup, targetWidth, 1, false, bgColor, fgColor).FirstOrDefault() ?? string.Empty;
                    var cells = AnsiParser.Parse(ansiLine, fgColor, bgColor);
                    buffer.WriteCellsClipped(startX, currentY, cells, clipRect);

                    // Fill any remaining width
                    int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(ansiLine);
                    if (lineWidth < targetWidth)
                    {
                        buffer.FillRect(new LayoutRect(startX + lineWidth, currentY, targetWidth - lineWidth, 1), ' ', fgColor, bgColor);
                    }

                    // Fill right margin
                    if (_margin.Right > 0)
                    {
                        buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', fgColor, bgColor);
                    }
                }
                currentY++;
            }
        }

        // Render scroll indicator if there are more entries
        if (totalFiltered > _maxDisplayLines && visibleLogs.Count > 0)
        {
            if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
            {
                // Fill left margin
                if (_margin.Left > 0)
                {
                    buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', fgColor, bgColor);
                }

                var endVisible = Math.Min(scrollOffset + visibleLogs.Count, totalFiltered);
                var scrollInfo = $"({scrollOffset + 1}-{endVisible} of {totalFiltered})";
                var scrollColor = Color.Grey;

                // Apply alignment
                int alignOffset = 0;
                if (scrollInfo.Length < targetWidth)
                {
                    switch (_horizontalAlignment)
                    {
                        case HorizontalAlignment.Center:
                            alignOffset = (targetWidth - scrollInfo.Length) / 2;
                            break;
                        case HorizontalAlignment.Right:
                            alignOffset = targetWidth - scrollInfo.Length;
                            break;
                    }
                }

                if (alignOffset > 0)
                {
                    buffer.FillRect(new LayoutRect(startX, currentY, alignOffset, 1), ' ', fgColor, bgColor);
                }

                for (int i = 0; i < scrollInfo.Length && startX + alignOffset + i < clipRect.Right; i++)
                {
                    int x = startX + alignOffset + i;
                    if (x >= clipRect.X)
                    {
                        buffer.SetCell(x, currentY, scrollInfo[i], scrollColor, bgColor);
                    }
                }

                int rightPadStart = startX + alignOffset + scrollInfo.Length;
                int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
                if (rightPadWidth > 0)
                {
                    buffer.FillRect(new LayoutRect(rightPadStart, currentY, rightPadWidth, 1), ' ', fgColor, bgColor);
                }

                if (_margin.Right > 0)
                {
                    buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', fgColor, bgColor);
                }
            }
            currentY++;
        }

        // Fill any remaining content area
        for (int y = currentY; y < bounds.Bottom - _margin.Bottom; y++)
        {
            if (y >= clipRect.Y && y < clipRect.Bottom)
            {
                buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
            }
        }

        // Fill bottom margin
        for (int y = bounds.Bottom - _margin.Bottom; y < bounds.Bottom; y++)
        {
            if (y >= clipRect.Y && y < clipRect.Bottom)
            {
                buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
            }
        }
    }

    #endregion
}
