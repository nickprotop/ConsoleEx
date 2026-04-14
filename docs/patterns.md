# SharpConsoleUI Patterns Cookbook

Practical recipes for building TUI applications with SharpConsoleUI. Each pattern shows the real way it's done in production cx apps (cxfiles, cxtop, LazyNuGet).

## 1. App Bootstrap

Every cx app follows the same startup pattern:

```csharp
// Create driver with buffer rendering (always use Buffer mode)
var driver = new NetConsoleDriver(RenderMode.Buffer);

// Create window system — disable default panels for full control
var ws = new ConsoleWindowSystem(driver,
    options: new ConsoleWindowSystemOptions(
        ShowTopPanel: false,
        ShowBottomPanel: false,
        WindowCycleKey: null));  // null to handle tab switching yourself

// Create and show main window
var mainWindow = new WindowBuilder(ws)
    .WithTitle("My App")
    .Maximized()
    .AddControls(toolbar, mainGrid, statusBar)
    .WithAsyncWindowThread(UpdateLoopAsync)
    .OnKeyPressed(OnGlobalKeyPressed)
    .BuildAndShow();

// Run the event loop (blocks until shutdown)
await Task.Run(() => ws.Run());
```

### With Desktop Panels (Start Menu, TaskBar, Clock)

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: panel => panel
        .Left(Elements.StatusText("[bold cyan]My App[/]"))
        .Left(Elements.Separator())
        .Right(Elements.Performance()),
    BottomPanelConfig: panel => panel
        .Left(Elements.StartMenu()
            .WithText("☰ Menu")
            .WithOptions(new StartMenuOptions
            {
                AppName = "My App",
                SidebarStyle = StartMenuSidebarStyle.IconLabel
            }))
        .Center(Elements.TaskBar())
        .Right(Elements.Clock().WithFormat("HH:mm:ss"))
);
var ws = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer), options: options);
```

## 2. Split Layout with Resizable Splitter

Two-panel layout (sidebar + main content):

```csharp
var grid = Controls.HorizontalGrid()
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithAlignment(HorizontalAlignment.Stretch)
    .Column(col => col.Width(40).Add(sidebarPanel))   // Fixed width
    .Column(col => col.Flex(1).Add(contentPanel))      // Fill remaining
    .WithSplitterAfter(0)                              // Draggable splitter
    .Build();
```

Three-panel layout (explorer + editor + side panel):

```csharp
var grid = Controls.HorizontalGrid()
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithAlignment(HorizontalAlignment.Stretch)
    .Column(col => col.Width(25).Add(explorerPanel))
    .Column(col => col.Flex(1).Add(editorPanel))
    .Column(col => col.Width(30).Add(detailPanel))
    .WithSplitterAfter(0)
    .WithSplitterAfter(1)
    .Build();
```

### Toggling a Column at Runtime

```csharp
bool detailVisible = true;

void ToggleDetailPanel()
{
    detailVisible = !detailVisible;
    var columns = grid.Columns;
    var splitters = grid.Splitters;
    
    if (columns.Count >= 3)
        columns[2].Visible = detailVisible;
    if (splitters.Count >= 2)
        splitters[1].Visible = detailVisible;
    
    mainWindow.ForceRebuildLayout();
    mainWindow.Invalidate(true);
}
```

## 3. Async Data Updates (Background → UI)

### Pattern A: Async Window Thread (Continuous Updates)

Best for dashboards, monitors, real-time data:

```csharp
var window = new WindowBuilder(ws)
    .WithAsyncWindowThread(UpdateLoopAsync)
    .BuildAndShow();

private async Task UpdateLoopAsync(Window window, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            var data = await FetchDataAsync(ct);
            
            // Update controls directly — this runs on the UI context
            window.FindControl<SparklineControl>("cpuSparkline")?.SetDataPoints(data.CpuHistory);
            
            var bar = window.FindControl<BarGraphControl>("cpuBar");
            if (bar != null) bar.Value = data.CpuPercent;
            
            var status = window.FindControl<MarkupControl>("status");
            status?.SetContent(new List<string> { $"[green]CPU: {data.CpuPercent:F1}%[/]" });
        }
        catch (Exception) { /* continue on error */ }

        try { await Task.Delay(1000, ct); }
        catch (TaskCanceledException) { break; }
    }
}
```

### Pattern B: Fire-and-Forget with Cancellation

Best for user-triggered async operations (search, load, fetch):

```csharp
private CancellationTokenSource? _loadCts;

public void LoadPackageDetails(string packageId)
{
    // Cancel previous load
    var previousCts = _loadCts;
    _loadCts = new CancellationTokenSource();
    try { previousCts?.Cancel(); } catch (ObjectDisposedException) { }

    var ct = _loadCts.Token;
    ShowLoadingState();

    AsyncHelper.FireAndForget(async () =>
    {
        var data = await _service.GetDetailsAsync(packageId, ct);
        if (ct.IsCancellationRequested) return;
        
        // Update UI with result
        UpdateDetailsPanel(data);
    },
    ex => ShowError(ex.Message));
}
```

### Pattern C: EnqueueOnUIThread (From Any Thread)

Best for file watchers, timer callbacks, external events:

```csharp
// File watcher callback — runs on background thread
fileWatcher = fileSystem.WatchDirectory(path, _ =>
{
    ws.EnqueueOnUIThread(() =>
    {
        RefreshFileList();
        UpdateStatusBar();
    });
});

// Note: Container?.Invalidate(true) is the ONLY call safe
// to make directly from a background thread without EnqueueOnUIThread
```

## 4. Modal Dialog with Result

### Base Pattern (TaskCompletionSource)

```csharp
public abstract class ModalBase<TResult>
{
    private TaskCompletionSource<TResult>? _tcs;
    protected Window? Modal { get; private set; }
    protected ConsoleWindowSystem WindowSystem { get; }

    public Task<TResult> ShowAsync()
    {
        _tcs = new TaskCompletionSource<TResult>();
        
        Modal = new WindowBuilder(WindowSystem)
            .WithTitle(GetTitle())
            .WithSize(GetWidth(), GetHeight())
            .Centered()
            .AsModal()
            .WithBorderStyle(BorderStyle.Rounded)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    CloseWithResult(GetDefaultResult());
                    e.Handled = true;
                }
            })
            .Build();

        BuildContent();  // Derived class adds controls
        WindowSystem.AddWindow(Modal);
        WindowSystem.SetActiveWindow(Modal);
        return _tcs.Task;
    }

    protected void CloseWithResult(TResult result)
    {
        WindowSystem.CloseWindow(Modal!);
        _tcs?.TrySetResult(result);
    }

    protected abstract string GetTitle();
    protected abstract void BuildContent();
    protected abstract TResult GetDefaultResult();
    protected virtual int GetWidth() => 60;
    protected virtual int GetHeight() => 18;
}
```

### Confirm Dialog Usage

```csharp
// Define
public class ConfirmModal : ModalBase<bool>
{
    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, string title, string message)
        => new ConfirmModal(ws, title, message).ShowAsync();
    
    protected override void BuildContent()
    {
        var label = Controls.Markup().AddLine(_message).Build();
        var yesBtn = Controls.Button().WithText("  Yes  ")
            .OnClick((_, _) => CloseWithResult(true)).Build();
        var noBtn = Controls.Button().WithText("  No  ")
            .OnClick((_, _) => CloseWithResult(false)).Build();
        Modal!.AddControl(label);
        Modal!.AddControl(yesBtn);
        Modal!.AddControl(noBtn);
    }
}

// Use
var confirmed = await ConfirmModal.ShowAsync(ws, "Delete?", "Permanently delete this file?");
if (!confirmed) return;
```

## 5. Keyboard Shortcuts

### Global Key Handler

```csharp
private void OnGlobalKeyPressed(object? sender, KeyPressedEventArgs e)
{
    var key = e.KeyInfo;
    bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
    bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);

    // Check modified keys BEFORE plain keys
    switch (key.Key)
    {
        case ConsoleKey.T when ctrl:
            NewTab();
            e.Handled = true;
            break;

        case ConsoleKey.S when ctrl:
            _ = ShowSearchAsync();
            e.Handled = true;
            break;

        case ConsoleKey.F when ctrl:
            EnterFilterMode();
            e.Handled = true;
            break;

        case ConsoleKey.F2:
            _ = RenameSelectedAsync();
            e.Handled = true;
            break;

        case ConsoleKey.Escape:
            NavigateBack();
            e.Handled = true;
            break;
    }
}

// Register on window builder
var window = new WindowBuilder(ws)
    .OnKeyPressed(OnGlobalKeyPressed)
    .Build();
```

### Preview Key Routing (Multi-Level)

For handling keys before controls consume them:

```csharp
mainWindow.PreviewKeyPressed += (_, e) =>
{
    // Context menu gets first priority
    if (contextMenu.ProcessPreviewKey(e)) return;

    // Active portal gets next priority
    if (activePortal != null)
    {
        activePortal.ProcessKey(e.KeyInfo);
        e.Handled = true;
        return;
    }
};
```

## 6. Toolbar

### Simple Toolbar

```csharp
var toolbar = Controls.Toolbar()
    .StickyTop()
    .WithSpacing(1)
    .WithBackgroundColor(Color.Grey11)
    .WithBelowLine()
    .AddButton("New", (_, _) => CreateNew())
    .AddButton("Open", (_, _) => OpenFile())
    .AddButton("Save", (_, _) => SaveFile())
    .Build();
```

### Dynamic Toolbar with Conditional Buttons

```csharp
private void UpdateToolbar()
{
    toolbar.Clear();

    AddToolbarButton("◈ Open [grey50]Enter[/]", OpenSelected);
    AddToolbarButton("↑ Up [grey50]Bksp[/]", NavigateUp);

    if (canCreateTab)
        AddToolbarButton("❒ New Tab [grey50]^T[/]", NewTab);

    toolbar.AddItem(new SeparatorControl());

    if (hasSelection)
    {
        AddToolbarButton("✕ Delete [grey50]Del[/]", () => _ = DeleteAsync());
        AddToolbarButton("✎ Rename [grey50]F2[/]", () => _ = RenameAsync());
    }
}

private void AddToolbarButton(string label, Action action)
{
    var btn = Controls.Button()
        .WithText(label)
        .WithBorder(ButtonBorderStyle.None)
        .WithBackgroundColor(Color.Transparent)
        .OnClick((_, _) => action())
        .Build();
    toolbar.AddItem(btn);
}
```

## 7. Status Bar

### Three-Zone Status Bar

```csharp
var statusBar = Controls.StatusBar()
    .AddLeft("↑↓", "Navigate")
    .AddLeft("Enter", "View")
    .AddLeftSeparator()
    .AddLeft("Esc", "Exit")
    .AddCenterText("[dim]My App[/]")
    .AddRight("Ctrl+S", "Search")
    .AddRightText("[yellow]3 items[/]")
    .WithAboveLine()
    .WithBackgroundColor(Color.Grey15)
    .WithShortcutForegroundColor(Color.Cyan1)
    .StickyBottom()
    .Build();
```

### Dynamic Status Updates with BatchUpdate

```csharp
statusBar.BatchUpdate(() =>
{
    statusBar.ClearAll();
    
    statusBar.AddLeftText($"[dim]{itemCount} items[/]");
    if (selectedCount > 0)
    {
        statusBar.AddLeftSeparator();
        statusBar.AddLeftText($"[cyan]{selectedCount} selected[/]");
    }
    
    statusBar.AddRightText("[grey70]Refresh[/] [grey50]F5[/]",
        () => Refresh());
    statusBar.AddRightText($"[{detailColor}]Detail[/] [grey50]F3[/]",
        () => ToggleDetail());
});
```

## 8. ScrollablePanel with Live Content

```csharp
var panel = Controls.ScrollablePanel()
    .WithVerticalScroll(ScrollMode.Scroll)
    .WithScrollbar(true)
    .WithMouseWheel(true)
    .WithAutoScroll(false)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithColors(Color.Grey93, Color.Black)
    .Build();

// Add static content
panel.AddControl(Controls.Markup()
    .AddLine("[bold]Title[/]")
    .AddLine("[dim]Subtitle[/]")
    .WithMargin(1, 1, 1, 0)
    .Build());

// Add a button
panel.AddControl(Controls.Button()
    .WithText("  Click Me  ")
    .WithMargin(1, 1, 0, 0)
    .WithBorder(ButtonBorderStyle.Rounded)
    .OnClick((_, _) => DoSomething())
    .Build());
```

## 9. List with Selection and Live Updates

```csharp
var list = Controls.List()
    .WithTitle("Items")
    .WithAlignment(HorizontalAlignment.Stretch)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithColors(Color.Grey93, Color.Black)
    .WithHighlightColors(Color.White, Color.Grey35)
    .SimpleMode()
    .Build();

// Selection tracking
list.SelectedIndexChanged += (_, idx) =>
{
    if (idx >= 0 && idx < items.Count)
        ShowDetails(items[idx]);
};

// Activation (Enter key)
list.ItemActivated += (_, item) =>
{
    OpenItem(item);
};

// Dynamic updates
list.ClearItems();
foreach (var item in newItems)
    list.AddItem(new ListItem(item.DisplayMarkup) { Tag = item });
list.SelectedIndex = 0;
```

## 10. Progress and Resource Visualization

### Bar Graph

```csharp
var bar = new BarGraphBuilder()
    .WithName("cpuBar")
    .WithLabel("CPU")
    .WithLabelWidth(6)
    .WithValue(0)
    .WithMaxValue(100)
    .WithAlignment(HorizontalAlignment.Stretch)
    .WithSmoothGradient(new Color[] {
        new(0x4e, 0xcd, 0xc4),  // teal (low)
        new(0xff, 0xd9, 0x3d),  // yellow (mid)
        new(0xff, 0x6b, 0x6b)   // red (high)
    })
    .ShowLabel()
    .ShowValue()
    .WithValueFormat("F1")
    .WithMargin(1, 0, 1, 0)
    .Build();

// Update
bar.Value = 78.5;
```

### Sparkline (Trend Over Time)

```csharp
var sparkline = new SparklineBuilder()
    .WithName("cpuSparkline")
    .WithTitle("CPU %")
    .WithHeight(4)
    .WithMaxValue(100)
    .WithGradient(gradientColors)
    .WithMode(SparklineMode.Braille)
    .WithBaseline(true, position: TitlePosition.Bottom)
    .WithAlignment(HorizontalAlignment.Stretch)
    .WithMargin(1, 0, 1, 0)
    .WithData(historyData)
    .Build();

// Update with new data point
historyData.Add(newValue);
while (historyData.Count > 50)
    historyData.RemoveAt(0);
sparkline.SetDataPoints(historyData);
```

### Progress Bar

```csharp
var progress = Controls.ProgressBar()
    .WithAlignment(HorizontalAlignment.Stretch)
    .Build();

// Update (0.0 to 1.0)
progress.Value = 0.65;
```

## 11. Live Log Viewer

### Pattern: Rolling Log with Timestamps

```csharp
private readonly List<string> _logLines = new();
private readonly object _logLock = new();
private MarkupControl? _logContent;

private void AddLogLine(string message)
{
    lock (_logLock)
    {
        var elapsed = DateTime.Now - _startTime;
        var timestamp = $"[grey50]{elapsed.TotalSeconds:F1}s[/]";
        var logLine = $"{timestamp} [grey70]{MarkupParser.Escape(message)}[/]";

        _logLines.Add(logLine);

        // Keep last 500 lines
        if (_logLines.Count > 500)
            _logLines.RemoveAt(0);

        _logContent?.SetContent(new List<string>(_logLines));
    }
}
```

## 12. Tabs

```csharp
var tabControl = new TabControlBuilder()
    .WithHeaderStyle(TabHeaderStyle.AccentedSeparator)
    .Fill()
    .WithAlignment(HorizontalAlignment.Stretch)
    .AddTab("Overview", overviewPanel)
    .AddTab("Details", detailsPanel)
    .AddTab("Logs", logsPanel)
    .Build();

tabControl.ActiveTabIndex = 0;

// Tab change event
tabControl.TabChanged += (_, e) =>
{
    OnTabChanged(e.NewIndex);
};

// Dynamic tabs (closable)
tabControl.AddTab("New Tab", contentPanel, isClosable: true);
```

## 13. Debounced Search

```csharp
private CancellationTokenSource? _searchCts;

private void OnSearchInputChanged(object? sender, string query)
{
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    var ct = _searchCts.Token;

    if (query.Length < 2) return;

    ShowSearchingIndicator();

    AsyncHelper.FireAndForget(async () =>
    {
        await Task.Delay(400, ct);  // 400ms debounce
        if (ct.IsCancellationRequested) return;

        var results = await _service.SearchAsync(query, ct);
        if (ct.IsCancellationRequested) return;

        UpdateResultsList(results);
        HideSearchingIndicator();
    },
    ex => HideSearchingIndicator());
}
```

## 14. Responsive Layout (Width-Based)

```csharp
private ResponsiveLayoutMode _currentLayout;
private const int WideThreshold = 120;

public IWindowControl BuildPanel(int windowWidth)
{
    _currentLayout = windowWidth >= WideThreshold
        ? ResponsiveLayoutMode.Wide
        : ResponsiveLayoutMode.Narrow;

    return _currentLayout == ResponsiveLayoutMode.Wide
        ? BuildWideLayout()    // 3-column: text | separator | graphs
        : BuildNarrowLayout(); // 1-column: text then graphs stacked
}

public void HandleResize(int newWidth)
{
    var desired = newWidth >= WideThreshold
        ? ResponsiveLayoutMode.Wide
        : ResponsiveLayoutMode.Narrow;

    if (desired == _currentLayout) return;

    _currentLayout = desired;
    RebuildLayout();
}
```

## 15. History Tracker (Rolling Data Window)

For sparklines and trend graphs:

```csharp
internal sealed class HistoryTracker
{
    private readonly List<double> _data = new();
    private readonly int _maxPoints;

    public HistoryTracker(int maxPoints = 50) => _maxPoints = maxPoints;
    public List<double> Data => _data;

    public void Add(double value)
    {
        _data.Add(value);
        while (_data.Count > _maxPoints)
            _data.RemoveAt(0);
    }
}

// Usage with sparkline
var cpuHistory = new HistoryTracker(50);

// In update loop
cpuHistory.Add(snapshot.CpuPercent);
sparkline.SetDataPoints(cpuHistory.Data);
```

## 16. Portal (Floating Popup)

For dropdowns, context menus, and floating panels anchored to a control:

```csharp
internal class MyPortal : PortalContentContainer
{
    public MyPortal(int anchorX, int anchorY, int windowWidth, int windowHeight)
    {
        BackgroundColor = new Color(30, 30, 40);
        BorderStyle = BoxChars.Rounded;
        DismissOnOutsideClick = true;

        // Add content
        AddChild(headerLabel);
        AddChild(contentPanel);

        // Smart positioning — stays within window bounds
        var pos = PortalPositioner.CalculateFromPoint(
            new Point(anchorX, anchorY),
            new Size(popupWidth, popupHeight),
            new Rectangle(1, 1, windowWidth - 2, windowHeight - 2),
            PortalPlacement.AboveOrBelow,
            new Size(16, 3));
        PortalBounds = pos.Bounds;
    }
}

// Show portal
var portal = new MyPortal(x, y, window.Width, window.Height);
portal.Container = window;
var portalNode = window.CreatePortal(anchorControl, portal);
portal.Dismissed += (_, _) => DismissPortal();

// Dismiss portal
private void DismissPortal()
{
    if (portalNode != null)
    {
        window.RemovePortal(anchorControl, portalNode);
        portalNode = null;
    }
}
```

## 17. Notifications (Toast Messages)

```csharp
// Info toast (auto-dismisses after 3s)
ws.NotificationStateService.ShowNotification(
    "Information", "Operation completed.",
    NotificationSeverity.Info);

// Warning toast (auto-dismisses after 5s)
ws.NotificationStateService.ShowNotification(
    "Warning", "Disk space is running low.",
    NotificationSeverity.Warning);

// Persistent (user must dismiss)
ws.NotificationStateService.ShowNotification(
    "Error", "Connection lost.",
    NotificationSeverity.Danger, timeout: null);

// Modal (blocks UI until dismissed)
ws.NotificationStateService.ShowNotification(
    "Confirm", "This action cannot be undone.",
    NotificationSeverity.Warning, blockUi: true, timeout: null);

// Dismiss all
ws.NotificationStateService.DismissAll();
```

## 18. Table with Interactive Features

```csharp
var table = Controls.Table()
    .WithTitle("Data Grid")
    .AddColumn("ID", TextJustification.Right, 8)
    .AddColumn("Name")
    .AddColumn("Status", TextJustification.Center, 12)
    .Interactive()
    .WithSorting()
    .WithFiltering()
    .WithFuzzyFilter()
    .WithInlineEditing()
    .WithCellNavigation()
    .WithColumnResize()
    .Rounded()
    .ShowRowSeparators()
    .WithHeaderColors(Color.White, Color.DarkBlue)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
    .OnSelectedRowChanged((_, rowIdx) => ShowRowDetails(rowIdx))
    .OnRowActivated((_, rowIdx) => OpenRow(rowIdx))
    .OnCellEditCompleted((_, e) => SaveCell(e.Row, e.Column, e.NewValue))
    .Build();

// Add rows
table.AddRow("1", "Alice", "[green]Active[/]");
table.AddRow("2", "Bob", "[yellow]Pending[/]");
```

## 19. NavigationView (Sidebar + Content)

```csharp
var nav = Controls.NavigationView()
    .WithNavWidth(30)
    .WithPaneHeader("[bold white]  My App[/]")
    .WithContentBorder(BorderStyle.Rounded)
    .WithContentBorderColor(Color.Grey37)
    .WithContentBackground(new Color(30, 30, 40))
    .AddHeader("Section A", Color.Cyan1, header => header
        .AddItem("Item 1", subtitle: "Description",
            content: panel => {
                panel.AddControl(Controls.Markup()
                    .AddLine("[bold]Item 1 Details[/]")
                    .Build());
            })
        .AddItem("Item 2", subtitle: "Description",
            content: panel => BuildItem2Content(panel)))
    .AddHeader("Section B", Color.Green, header => header
        .AddItem("Item 3", content: panel => BuildItem3Content(panel)))
    .Fill()
    .Build();

nav.ItemInvoked += (_, args) =>
{
    if (args.NewItem != null)
        HandleNavigation(args.NewItem.Text);
};
```

## 20. Color Theming

Centralize colors in a constants class:

```csharp
public static class AppColors
{
    // Backgrounds
    public static readonly Color BaseBg = new(0x0d, 0x11, 0x17);
    public static readonly Color PanelBg = new(15, 20, 30, 200);
    public static readonly Color CardBg = new(20, 28, 40, 180);

    // Semantic colors
    public static readonly Color Accent = Color.Cyan1;
    public static readonly Color Success = new(0x4e, 0xcd, 0xc4);
    public static readonly Color Warning = new(0xff, 0xd9, 0x3d);
    public static readonly Color Danger = new(0xff, 0x6b, 0x6b);

    // Gradients (for bar graphs and sparklines)
    public static readonly Color[] HealthGradient = {
        new(0x4e, 0xcd, 0xc4),  // teal (good)
        new(0xff, 0xd9, 0x3d),  // yellow (warning)
        new(0xff, 0x6b, 0x6b)   // red (critical)
    };

    // Threshold-based color selection
    public static string ThresholdColor(double value) => value switch
    {
        < 60 => FormatHex(Success),
        < 85 => FormatHex(Warning),
        _    => FormatHex(Danger)
    };

    private static string FormatHex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
}

// Usage in markup
var cpuColor = AppColors.ThresholdColor(cpuPercent);
markup.SetContent(new List<string> { $"[{cpuColor}]CPU: {cpuPercent:F1}%[/]" });
```

## 21. Event Handler Cleanup

Always unsubscribe event handlers to prevent memory leaks, especially in modals:

```csharp
private EventHandler<string>? _inputChangedHandler;
private EventHandler<int>? _selectionHandler;

protected override void BuildContent()
{
    _inputChangedHandler = (_, text) => OnSearchChanged(text);
    searchInput.InputChanged += _inputChangedHandler;

    _selectionHandler = (_, idx) => OnSelectionChanged(idx);
    list.SelectedIndexChanged += _selectionHandler;
}

protected override void OnCleanup()
{
    if (searchInput != null && _inputChangedHandler != null)
        searchInput.InputChanged -= _inputChangedHandler;

    if (list != null && _selectionHandler != null)
        list.SelectedIndexChanged -= _selectionHandler;

    _searchCts?.Cancel();
    _searchCts?.Dispose();
}
```

## 22. Control Discovery by Name

Name controls for later lookup in async threads:

```csharp
// When building
var sparkline = new SparklineBuilder()
    .WithName("cpuSparkline")  // Named for lookup
    .Build();

// In async update loop
window.FindControl<SparklineControl>("cpuSparkline")?.SetDataPoints(data);
window.FindControl<BarGraphControl>("cpuBar")?.SetValue(value);
window.FindControl<MarkupControl>("status")?.SetContent(lines);
```

## 23. Multi-View Navigation

```csharp
public enum ViewState { Projects, Packages }

public class NavigationController
{
    private ViewState _currentViewState = ViewState.Projects;

    public void NavigateForward(ProjectInfo project)
    {
        _currentViewState = ViewState.Packages;
        UpdateHeaders();
        PopulatePackagesList(project.Packages);
    }

    public void NavigateBack()
    {
        if (_currentViewState == ViewState.Packages)
        {
            _currentViewState = ViewState.Projects;
            CancelPendingLoads();
            UpdateHeaders();
            PopulateProjectsList();
        }
    }

    public void HandleEnterKey() { /* navigate forward */ }
    public void HandleEscapeKey() { /* navigate back */ }
}
```
