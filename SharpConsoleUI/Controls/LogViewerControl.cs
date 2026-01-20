// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using SharpConsoleUI.Logging;
using Spectre.Console;
using Color = Spectre.Console.Color;
using SharpConsoleUI.Events;
using System.Collections.Concurrent;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A control that displays log entries from the library's LogService.
/// Automatically updates when new log entries are added.
/// Uses ScrollablePanelControl internally for scrolling with AutoScroll support.
/// Thread-safe: log events can be received from any thread.
/// </summary>
public class LogViewerControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDOMPaintable
{
    private readonly ILogService _logService;
    private readonly ScrollablePanelControl _scrollPanel;
    private readonly Dictionary<LogEntry, MarkupControl> _entryControls = new();
    private readonly List<LogEntry> _displayedLogs = new();

    // Thread-safe queue for pending log entries (can be added from any thread)
    private readonly ConcurrentQueue<LogEntry> _pendingEntries = new();
    private volatile bool _pendingClear = false;

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

    /// <summary>
    /// Creates a new LogViewerControl bound to the specified log service
    /// </summary>
    /// <param name="logService">The log service to display entries from</param>
    public LogViewerControl(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        _scrollPanel = new ScrollablePanelControl
        {
            AutoScroll = true,
            VerticalScrollMode = ScrollMode.Scroll,
            ShowScrollbar = true
        };

        _logService.LogAdded += OnLogAdded;
        _logService.LogsCleared += OnLogsCleared;

        // Load existing logs (we're on the creating thread, not UI thread yet)
        // Queue them for processing on first paint
        foreach (var entry in _logService.GetAllLogs())
        {
            if (entry.Level >= _filterLevel)
            {
                if (_filterCategory == null || entry.Category == _filterCategory)
                {
                    _pendingEntries.Enqueue(entry);
                }
            }
        }
    }

    #region Events

    /// <summary>
    /// Event fired when the control gains focus.
    /// </summary>
    public event EventHandler? GotFocus;

    /// <summary>
    /// Event fired when the control loses focus.
    /// </summary>
    public event EventHandler? LostFocus;

    #pragma warning disable CS0067  // Event never raised (interface requirement)
    /// <summary>
    /// Occurs when the control is clicked.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    /// <summary>
    /// Occurs when the control is double-clicked.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseDoubleClick;

    /// <summary>
    /// Occurs when the mouse enters the control area.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseEnter;

    /// <summary>
    /// Occurs when the mouse leaves the control area.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseLeave;

    /// <summary>
    /// Occurs when the mouse moves over the control.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseMove;
    #pragma warning restore CS0067

    #endregion

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
            SetFocus(value, FocusReason.Programmatic);
        }
    }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    #endregion

    #region IFocusableControl Properties

    /// <inheritdoc/>
    public bool CanReceiveFocus => _visible && IsEnabled;

    #endregion

    #region IMouseAwareControl Properties

    /// <inheritdoc/>
    public bool WantsMouseEvents => true;

    /// <inheritdoc/>
    public bool CanFocusWithMouse => CanReceiveFocus;

    #endregion

    #region LogViewerControl Properties

    /// <summary>
    /// Gets or sets whether to automatically scroll to show new log entries.
    /// This now delegates to the internal ScrollablePanelControl.
    /// </summary>
    public bool AutoScroll
    {
        get => _scrollPanel.AutoScroll;
        set => _scrollPanel.AutoScroll = value;
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
        int titleHeight = string.IsNullOrEmpty(_title) ? 0 : 1;
        var panelSize = _scrollPanel.GetLogicalContentSize();
        int width = _width ?? 80;
        return new System.Drawing.Size(width, titleHeight + panelSize.Height);
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
        _disposed = true;

        _logService.LogAdded -= OnLogAdded;
        _logService.LogsCleared -= OnLogsCleared;
        _scrollPanel.Dispose();
        Container = null;
    }

    #endregion

    #region IInteractiveControl Methods

    /// <inheritdoc/>
    public bool ProcessKey(ConsoleKeyInfo keyInfo)
    {
        if (!IsEnabled) return false;

        // Handle clear logs
        if (keyInfo.Key == ConsoleKey.Delete ||
            (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)))
        {
            _logService.ClearLogs();
            return true;
        }

        // Delegate to scroll panel for scrolling keys
        return _scrollPanel.ProcessKey(keyInfo);
    }

    #endregion

    #region IFocusableControl Methods

    /// <inheritdoc/>
    public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
    {
        if (_hasFocus == focus) return;

        _hasFocus = focus;
        _scrollPanel.HasFocus = focus;

        if (focus)
        {
            GotFocus?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            LostFocus?.Invoke(this, EventArgs.Empty);
        }

        Container?.Invalidate(true);
    }

    #endregion

    #region IMouseAwareControl Methods

    /// <inheritdoc/>
    public bool ProcessMouseEvent(MouseEventArgs args)
    {
        // Delegate mouse handling to scroll panel
        return _scrollPanel.ProcessMouseEvent(args);
    }

    #endregion

    #region Private Methods - Event Handlers (called from any thread)

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        if (_disposed) return;

        // Check if entry passes filter
        if (entry.Level < _filterLevel)
            return;

        if (_filterCategory != null && entry.Category != _filterCategory)
            return;

        // Queue for processing on UI thread during paint
        _pendingEntries.Enqueue(entry);

        // Trigger repaint (Invalidate is safe to call from any thread)
        Container?.Invalidate(true);
    }

    private void OnLogsCleared(object? sender, EventArgs e)
    {
        if (_disposed) return;

        // Signal clear - will be processed on UI thread during paint
        _pendingClear = true;

        // Trigger repaint
        Container?.Invalidate(true);
    }

    #endregion

    #region Private Methods - UI Thread Processing

    /// <summary>
    /// Processes pending queue entries. Must be called from UI thread (during paint).
    /// </summary>
    private void ProcessPendingEntries()
    {
        // Handle clear first
        if (_pendingClear)
        {
            _pendingClear = false;

            // Clear the queue of any pending entries
            while (_pendingEntries.TryDequeue(out _)) { }

            // Clear displayed logs
            _displayedLogs.Clear();
            foreach (var control in _entryControls.Values)
            {
                _scrollPanel.RemoveControl(control);
            }
            _entryControls.Clear();
        }

        // Process pending additions
        while (_pendingEntries.TryDequeue(out var entry))
        {
            _displayedLogs.Add(entry);

            // Create MarkupControl for this entry
            var markup = new MarkupControl(new List<string> { entry.ToMarkup() }) { Wrap = true };
            _entryControls[entry] = markup;
            _scrollPanel.AddControl(markup);

            // Trim if we have too many
            while (_displayedLogs.Count > _logService.MaxBufferSize)
            {
                var oldEntry = _displayedLogs[0];
                _displayedLogs.RemoveAt(0);

                if (_entryControls.TryGetValue(oldEntry, out var oldControl))
                {
                    _scrollPanel.RemoveControl(oldControl);
                    _entryControls.Remove(oldEntry);
                }
            }
        }
    }

    private void RefreshLogs()
    {
        // Signal clear and re-queue matching logs
        _pendingClear = true;

        // Queue filtered logs
        foreach (var entry in _logService.GetAllLogs())
        {
            if (entry.Level >= _filterLevel)
            {
                if (_filterCategory == null || entry.Category == _filterCategory)
                {
                    _pendingEntries.Enqueue(entry);
                }
            }
        }

        Container?.Invalidate(true);
    }

    #endregion

    #region IDOMPaintable Implementation

    /// <inheritdoc/>
    public LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        // Process pending entries before measuring
        ProcessPendingEntries();

        int contentWidth = constraints.MaxWidth - _margin.Left - _margin.Right;
        int titleHeight = string.IsNullOrEmpty(_title) ? 0 : 1;

        // Measure the scroll panel
        var panelConstraints = new LayoutConstraints(
            MinWidth: 1,
            MaxWidth: contentWidth,
            MinHeight: 1,
            MaxHeight: Math.Max(1, constraints.MaxHeight - titleHeight - _margin.Top - _margin.Bottom)
        );
        var panelSize = (_scrollPanel as IDOMPaintable).MeasureDOM(panelConstraints);

        int totalHeight = titleHeight + panelSize.Height + _margin.Top + _margin.Bottom;
        int width = (_width ?? contentWidth) + _margin.Left + _margin.Right;

        return new LayoutSize(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(totalHeight, constraints.MinHeight, constraints.MaxHeight)
        );
    }

    /// <inheritdoc/>
    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        // Process pending entries on UI thread
        ProcessPendingEntries();

        var bgColor = Container?.BackgroundColor ?? defaultBg;
        var fgColor = Container?.ForegroundColor ?? defaultFg;
        int targetWidth = bounds.Width - _margin.Left - _margin.Right;

        if (targetWidth <= 0) return;

        int startX = bounds.X + _margin.Left;
        int startY = bounds.Y + _margin.Top;
        int currentY = startY;

        // Fill top margin
        ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

        // Render title if set
        int titleHeight = 0;
        if (!string.IsNullOrEmpty(_title))
        {
            titleHeight = 1;
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
        }

        // Render scroll panel
        int panelHeight = bounds.Height - _margin.Top - _margin.Bottom - titleHeight;
        if (panelHeight > 0)
        {
            var panelBounds = new LayoutRect(startX, currentY, targetWidth, panelHeight);
            var panelClipRect = clipRect.Intersect(panelBounds);

            // Update scroll panel container reference for proper invalidation
            _scrollPanel.Container = this.Container;
            _scrollPanel.BackgroundColor = bgColor;
            _scrollPanel.ForegroundColor = fgColor;

            (_scrollPanel as IDOMPaintable).PaintDOM(buffer, panelBounds, panelClipRect, fgColor, bgColor);
        }

        // Fill bottom margin
        ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - _margin.Bottom, fgColor, bgColor);
    }

    #endregion
}
