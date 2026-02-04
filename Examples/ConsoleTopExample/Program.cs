// -----------------------------------------------------------------------
// ConsoleTopExample - ntop/btop-inspired live dashboard
// Demonstrates full-screen window with Spectre renderables and SharpConsoleUI controls
// Modernized with AgentStudio aesthetics and simplified UX
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace ConsoleTopExample;

internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _mainWindow;

    // State (not UI references)
    private static readonly ISystemStatsProvider _stats = SystemStatsFactory.Create();
    private static SystemSnapshot? _lastSnapshot;

    // Tab mode for UI
    private enum TabMode
    {
        Processes,
        Memory,
        Cpu,
        Network,
        Storage,
    }

    private static TabMode _activeTab = TabMode.Processes;
    private static ProcessSample? _lastHighlightedProcess;

    // Process sorting
    private enum ProcessSortMode
    {
        Cpu,      // CPU% descending (default)
        Memory,   // Memory% descending
        Pid,      // PID ascending
        Name      // Command name ascending
    }

    private static ProcessSortMode _processSortMode = ProcessSortMode.Cpu;

    // Memory history for sparkline graphs
    private static readonly List<double> _memoryUsedHistory = new();
    private static readonly List<double> _memoryAvailableHistory = new();
    private static readonly List<double> _memoryCachedHistory = new();
    private static readonly List<double> _swapUsedHistory = new();
    private const int MAX_HISTORY_POINTS = 50;

    // Responsive layout tracking for memory panel
    private enum MemoryLayoutMode { Wide, Narrow }
    private static MemoryLayoutMode _currentMemoryLayout = MemoryLayoutMode.Wide;
    private const int MEMORY_LAYOUT_THRESHOLD_WIDTH = 80; // Switch to narrow below this width

    // CPU history for sparkline graphs
    private static readonly List<double> _cpuUserHistory = new();
    private static readonly List<double> _cpuSystemHistory = new();
    private static readonly List<double> _cpuIoWaitHistory = new();
    private static readonly List<double> _cpuTotalHistory = new();
    private static readonly Dictionary<int, List<double>> _cpuPerCoreHistory = new();

    // Responsive layout tracking for CPU panel
    private enum CpuLayoutMode { Wide, Narrow }
    private static CpuLayoutMode _currentCpuLayout = CpuLayoutMode.Wide;
    private const int CPU_LAYOUT_THRESHOLD_WIDTH = 80; // Switch to narrow below this width

    // Network history for sparkline graphs (aggregate)
    private static readonly List<double> _networkUpHistory = new();
    private static readonly List<double> _networkDownHistory = new();

    // Per-interface network history (like _cpuPerCoreHistory)
    private static readonly Dictionary<string, List<double>> _networkPerInterfaceUpHistory = new();
    private static readonly Dictionary<string, List<double>> _networkPerInterfaceDownHistory = new();

    // Peak tracking for network stats display
    private static double _peakUpMbps = 0;
    private static double _peakDownMbps = 0;

    // Responsive layout tracking for network panel
    private enum NetworkLayoutMode { Wide, Narrow }
    private static NetworkLayoutMode _currentNetworkLayout = NetworkLayoutMode.Wide;
    private const int NETWORK_LAYOUT_THRESHOLD_WIDTH = 80; // Switch to narrow below this width

    // Storage history for sparkline graphs (per-disk tracking)
    private static readonly Dictionary<string, List<double>> _diskReadHistory = new();
    private static readonly Dictionary<string, List<double>> _diskWriteHistory = new();

    // Responsive layout tracking for storage panel
    private enum StorageLayoutMode { Wide, Narrow }
    private static StorageLayoutMode _currentStorageLayout = StorageLayoutMode.Wide;
    private const int STORAGE_LAYOUT_THRESHOLD_WIDTH = 90; // Switch to narrow below this width

    static async Task<int> Main(string[] args)
    {
        try
        {
            _windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    StatusBarOptions: new StatusBarOptions(
                        ShowTaskBar: false
                    )
                ));
            _windowSystem.StatusBarStateService.TopStatus = $"ConsoleTop - System Monitor ({SystemStatsFactory.GetPlatformName()})";

            Console.CancelKeyPress += (sender, e) =>
            {
                _windowSystem?.LogService.LogInfo("Ctrl+C received, shutting down...");
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            CreateDashboardWindow();

            _windowSystem.LogService.LogInfo("Starting ConsoleTopExample");
            await Task.Run(() => _windowSystem.Run());
            _windowSystem.LogService.LogInfo("ConsoleTopExample stopped");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void CreateDashboardWindow()
    {
        if (_windowSystem == null)
            return;

        _mainWindow = new WindowBuilder(_windowSystem)
            .WithTitle("ConsoleTop - Live System Pulse")
            .WithColors(Color.Grey11, Color.Grey93)
            .Borderless()
            .Maximized()
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .OnKeyPressed(
                (sender, e) =>
                {
                    if (e.KeyInfo.Key == ConsoleKey.F10 || e.KeyInfo.Key == ConsoleKey.Escape)
                    {
                        _windowSystem?.Shutdown();
                        e.Handled = true;
                    }
                }
            )
            .Build();

        if (_mainWindow == null)
            return;

        var mainWindow = _mainWindow; // Capture non-null reference for nullable flow analysis

        var systemInfo = SystemStatsFactory.GetDetailedSystemInfo();

        // === TOP STATUS BAR ===
        var topStatusBar = Controls
            .HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(
                    Controls
                        .Markup($"[cyan1 bold]ConsoleTop[/] [grey70]• {systemInfo}[/]")
                        .WithAlignment(HorizontalAlignment.Left)
                        .WithMargin(1, 0, 0, 0)
                        .Build()
                )
            )
            .Column(col =>
                col.Add(
                    Controls
                        .Markup("[grey70]--:--:--[/]")
                        .WithAlignment(HorizontalAlignment.Right)
                        .WithMargin(0, 0, 1, 0)
                        .WithName("topStatusClock")
                        .Build()
                )
            )
            .Build();
        topStatusBar.BackgroundColor = Color.Grey15;
        topStatusBar.ForegroundColor = Color.Grey93;

        mainWindow.AddControl(topStatusBar);
        mainWindow.AddControl(Controls.RuleBuilder().StickyTop().WithColor(Color.Grey23).Build());

        // === CPU/MEMORY SECTION ===
        var metricsGrid = Controls
            .HorizontalGrid()
            .WithMargin(1, 1, 1, 1)
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]CPU Usage[/]").WithMargin(1, 1, 0, 0).Build())
                    .Add(
                        new BarGraphBuilder()
                            .WithName("cpuUserBar")
                            .WithLabel("User")
                            .WithLabelWidth(6)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 0, 0)
                            .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                            .Build()
                    )
                    .Add(
                        new BarGraphBuilder()
                            .WithName("cpuSystemBar")
                            .WithLabel("System")
                            .WithLabelWidth(6)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 0, 0)
                            .WithSmoothGradient(Color.Cyan1, Color.Yellow, Color.Orange1)
                            .Build()
                    )
                    .Add(
                        new BarGraphBuilder()
                            .WithName("cpuIoWaitBar")
                            .WithLabel("IOwait")
                            .WithLabelWidth(6)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 0, 1)
                            .WithSmoothGradient(Color.Blue, Color.Cyan1, Color.Yellow)
                            .Build()
                    )
            )
            .Column(col => col.Width(1)) // Spacing between boxes
            .Column(col =>
                col.Add(
                        Controls
                            .Markup("[grey70 bold]Memory / IO[/]")
                            .WithMargin(1, 1, 1, 0)
                            .Build()
                    )
                    .Add(
                        new BarGraphBuilder()
                            .WithName("memUsedBar")
                            .WithLabel("Used %")
                            .WithLabelWidth(12)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 1, 0)
                            .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                            .Build()
                    )
                    .Add(
                        new BarGraphBuilder()
                            .WithName("memCachedBar")
                            .WithLabel("Cached %")
                            .WithLabelWidth(12)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 1, 0)
                            .WithSmoothGradient(Color.Blue, Color.Cyan1, Color.Green)
                            .Build()
                    )
                    .Add(
                        new BarGraphBuilder()
                            .WithName("memIoBar")
                            .WithLabel("Disk/IO est %")
                            .WithLabelWidth(12)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 1, 1)
                            .WithSmoothGradient(Color.Grey50, Color.Yellow, Color.Orange1)
                            .Build()
                    )
            )
            .Column(col => col.Width(1)) // Spacing between boxes
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]Network[/]").WithMargin(1, 1, 1, 0).Build())
                    .Add(
                        new BarGraphBuilder()
                            .WithName("netUploadBar")
                            .WithLabel("Upload")
                            .WithLabelWidth(8)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 1, 0)
                            .WithSmoothGradient("cool")
                            .Build()
                    )
                    .Add(
                        new BarGraphBuilder()
                            .WithName("netDownloadBar")
                            .WithLabel("Download")
                            .WithLabelWidth(8)
                            .WithValue(0)
                            .WithMaxValue(100)
                            .WithBarWidth(12)
                            .WithUnfilledColor(Color.Grey35)
                            .ShowLabel()
                            .ShowValue()
                            .WithValueFormat("F1")
                            .WithMargin(1, 0, 1, 1)
                            .WithSmoothGradient("warm")
                            .Build()
                    )
            )
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();

        // Set background colors for metrics boxes
        if (metricsGrid.Columns.Count >= 5)
        {
            metricsGrid.Columns[0].BackgroundColor = Color.Grey15; // CPU box
            metricsGrid.Columns[0].ForegroundColor = Color.Grey93;

            metricsGrid.Columns[2].BackgroundColor = Color.Grey15; // Memory box
            metricsGrid.Columns[2].ForegroundColor = Color.Grey93;

            metricsGrid.Columns[4].BackgroundColor = Color.Grey15; // Network box
            metricsGrid.Columns[4].ForegroundColor = Color.Grey93;
        }

        mainWindow.AddControl(metricsGrid);

        mainWindow.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // === TAB TOOLBAR ===
        var tabToolbar = Controls
            .Toolbar()
            .WithName("tabToolbar")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 1, 1, 1)
            .AddButton(
                Controls
                    .Button("Processes")
                    .WithName("tabProcesses")
                    .OnClick(
                        (s, e) =>
                        {
                            _activeTab = TabMode.Processes;
                            UpdateDisplay();
                        }
                    )
            )
            .AddButton(
                Controls
                    .Button("Memory")
                    .WithName("tabMemory")
                    .OnClick(
                        (s, e) =>
                        {
                            _activeTab = TabMode.Memory;
                            UpdateDisplay();
                        }
                    )
            )
            .AddButton(
                Controls
                    .Button("CPU")
                    .WithName("tabCpu")
                    .OnClick(
                        (s, e) =>
                        {
                            _activeTab = TabMode.Cpu;
                            UpdateDisplay();
                        }
                    )
            )
            .AddButton(
                Controls
                    .Button("Network")
                    .WithName("tabNetwork")
                    .OnClick(
                        (s, e) =>
                        {
                            _activeTab = TabMode.Network;
                            UpdateDisplay();
                        }
                    )
            )
            .AddButton(
                Controls
                    .Button("Storage")
                    .WithName("tabStorage")
                    .OnClick(
                        (s, e) =>
                        {
                            _activeTab = TabMode.Storage;
                            UpdateDisplay();
                        }
                    )
            )
            .WithSpacing(1)
            .Build();
        mainWindow.AddControl(tabToolbar);

        mainWindow.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // === PROCESS LIST + DETAIL HORIZONTAL LAYOUT (NO SPLITTER) ===
        // Build the sorting toolbar for the process list
        var processSortToolbar = Controls
            .Toolbar()
            .WithName("processSortToolbar")
            .WithMargin(1, 0, 0, 0)
            .Add(
                Controls.Dropdown()
                    .WithName("processSortDropdown")
                    .WithPrompt("Sort:")
                    .AddItem("CPU %")
                    .AddItem("Memory %")
                    .AddItem("PID")
                    .AddItem("Name")
                    .SelectedIndex(0)
                    .WithWidth(20)
                    .OnSelectionChanged((sender, index, window) =>
                    {
                        _processSortMode = (ProcessSortMode)index;
                        UpdateProcessList();
                    })
                    .Build()
            )
            .Build();

        // Build the process list control
        var processListControl = ListControl
            .Create()
            .WithName("processList")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Grey11, Color.Grey93)
            .WithFocusedColors(Color.Grey11, Color.Grey93)
            .WithHighlightColors(Color.Grey35, Color.White)
            .WithMargin(1, 0, 0, 1)
            .OnHighlightChanged(
                (_, idx) =>
                {
                    // Store the highlighted process (only update when valid, never clear)
                    var processList = _mainWindow?.FindControl<ListControl>(
                        "processList"
                    );
                    if (
                        processList != null
                        && idx >= 0
                        && idx < processList.Items.Count
                    )
                    {
                        _lastHighlightedProcess =
                            processList.Items[idx].Tag as ProcessSample;
                    }
                    // Note: Don't clear _lastHighlightedProcess when idx == -1
                    // This preserves it when list loses focus before button click

                    // Only update if on Processes tab
                    if (_activeTab == TabMode.Processes)
                    {
                        UpdateHighlightedProcess();
                    }
                }
            )
            .OnItemActivated(
                (_, item) =>
                {
                    if (item?.Tag is ProcessSample ps)
                    {
                        ShowProcessActionsDialog(ps);
                    }
                }
            )
            .SimpleMode()
            .Build();

        var mainGrid = Controls
            .HorizontalGrid()
            .WithName("processPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            // Left column: Toolbar + Process list (multiple controls stack vertically)
            .Column(col =>
                col.Add(processSortToolbar)
                   .Add(processListControl)
            )
            // Spacing column (1 char wide for visual separation)
            .Column(col => col.Width(1))
            // Right column: Detail panel with fixed width (Grey19 background)
            .Column(col =>
                col.Width(40)
                    // Top toolbar with Actions button only (Memory is now a tab)
                    .Add(
                        Controls
                            .Toolbar()
                            .WithName("detailToolbar")
                            .WithMargin(1, 0, 1, 1)
                            .WithSpacing(2)
                            .AddButton(
                                Controls
                                    .Button("Actions...")
                                    .WithWidth(15)
                                    .OnClick((s, e) => ShowProcessActionsDialog())
                                    .WithName("actionButton")
                                    .Visible(false)
                            )
                            .Build()
                    )
                    // Detail content (scrollable) - fills remaining vertical space
                    .Add(
                        Controls
                            .ScrollablePanel()
                            .WithName("processDetailPanel")
                            .WithVerticalAlignment(VerticalAlignment.Fill)
                            .WithAlignment(HorizontalAlignment.Stretch)
                            .WithMargin(2, 0, 1, 0)
                            .AddControl(
                                Controls
                                    .Markup()
                                    .AddLine("[grey50 italic]Loading...[/]")
                                    .WithAlignment(HorizontalAlignment.Left)
                                    .WithName("processDetailContent")
                                    .Build()
                            )
                            .Build()
                    )
            )
            .Build();

        // Set right column background to Grey19 for visual distinction
        if (mainGrid.Columns.Count > 2)
        {
            mainGrid.Columns[2].BackgroundColor = Color.Grey19;
            mainGrid.Columns[2].ForegroundColor = Color.Grey93;
        }

        mainWindow.AddControl(mainGrid);

        // Set detail toolbar background to Grey19
        var detailToolbar = _mainWindow?.FindControl<ToolbarControl>("detailToolbar");
        if (detailToolbar != null)
        {
            detailToolbar.BackgroundColor = Color.Grey19;
            detailToolbar.ForegroundColor = Color.Grey93;
        }

        // Set detail panel background to Grey19
        var detailPanel = _mainWindow?.FindControl<ScrollablePanelControl>("processDetailPanel");
        if (detailPanel != null)
        {
            detailPanel.BackgroundColor = Color.Grey19;
            detailPanel.ForegroundColor = Color.Grey93;
        }

        // === MEMORY PANEL (RESPONSIVE LAYOUT) ===
        // Build memory panel at startup with responsive layout based on window width
        var initialSnapshot = _stats.ReadSnapshot();
        var memoryPanelInitial = BuildResponsiveMemoryGrid(mainWindow.Width, initialSnapshot);
        mainWindow.AddControl(memoryPanelInitial);

        // === CPU PANEL (RESPONSIVE LAYOUT) ===
        // Build CPU panel at startup with responsive layout based on window width
        var cpuPanelInitial = BuildResponsiveCpuGrid(mainWindow.Width, initialSnapshot);
        mainWindow.AddControl(cpuPanelInitial);

        // === NETWORK PANEL (RESPONSIVE LAYOUT) ===
        // Build Network panel at startup with responsive layout based on window width
        var networkPanelInitial = BuildResponsiveNetworkGrid(mainWindow.Width, initialSnapshot);
        mainWindow.AddControl(networkPanelInitial);

        // === STORAGE PANEL (RESPONSIVE LAYOUT) ===
        // Build Storage panel at startup with responsive layout based on window width
        var storagePanelInitial = BuildResponsiveStorageGrid(mainWindow.Width, initialSnapshot);
        mainWindow.AddControl(storagePanelInitial);

        // === BOTTOM STATUS BAR ===
        mainWindow.AddControl(
            Controls.RuleBuilder().StickyBottom().WithColor(Color.Grey23).Build()
        );

        var bottomStatusBar = Controls
            .HorizontalGrid()
            .StickyBottom()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(
                    Controls
                        .Markup()
                        .AddLine("[grey70]F10/ESC: Exit • Click: Select • Double-Click: Actions[/]")
                        .WithAlignment(HorizontalAlignment.Left)
                        .WithMargin(1, 0, 0, 0)
                        .Build()
                )
            )
            .Column(col =>
                col.Add(
                    Controls
                        .Markup(
                            "[grey70]CPU [cyan1]0.0%[/] • MEM [cyan1]0.0%[/] • NET ↑[cyan1]0.0[/]/↓[cyan1]0.0[/] MB/s[/]"
                        )
                        .WithAlignment(HorizontalAlignment.Right)
                        .WithMargin(0, 0, 1, 0)
                        .WithName("statsLegend")
                        .Build()
                )
            )
            .Build();
        bottomStatusBar.BackgroundColor = Color.Grey15;
        bottomStatusBar.ForegroundColor = Color.Grey70;

        mainWindow.AddControl(bottomStatusBar);

        // Hook up responsive layout handlers for memory, CPU, and network panels
        mainWindow.OnResize += (sender, e) =>
        {
            HandleMemoryPanelResize();
            HandleCpuPanelResize();
            HandleNetworkPanelResize();
        };

        _windowSystem.AddWindow(mainWindow);
    }

    private static void UpdateDisplay()
    {
        if (_mainWindow == null)
            return;

        // Find all panels
        var processPanel = _mainWindow?.FindControl<HorizontalGridControl>("processPanel");
        var memoryPanel = _mainWindow?.FindControl<HorizontalGridControl>("memoryPanel");
        var cpuPanel = _mainWindow?.FindControl<HorizontalGridControl>("cpuPanel");
        var networkPanel = _mainWindow?.FindControl<HorizontalGridControl>("networkPanel");

        // Switch visibility based on active tab
        switch (_activeTab)
        {
            case TabMode.Processes:
                // Show process panel, hide others
                var storagePanel1 = _mainWindow?.FindControl<HorizontalGridControl>("storagePanel");
                if (processPanel != null)
                    processPanel.Visible = true;
                if (memoryPanel != null)
                    memoryPanel.Visible = false;
                if (cpuPanel != null)
                    cpuPanel.Visible = false;
                if (networkPanel != null)
                    networkPanel.Visible = false;
                if (storagePanel1 != null)
                    storagePanel1.Visible = false;

                // Update process detail content
                UpdateHighlightedProcess();
                break;

            case TabMode.Memory:
                // Hide process panel, show memory panel, hide CPU and network panels
                var storagePanel2 = _mainWindow?.FindControl<HorizontalGridControl>("storagePanel");
                if (processPanel != null)
                    processPanel.Visible = false;
                if (memoryPanel != null)
                    memoryPanel.Visible = true;
                if (cpuPanel != null)
                    cpuPanel.Visible = false;
                if (networkPanel != null)
                    networkPanel.Visible = false;
                if (storagePanel2 != null)
                    storagePanel2.Visible = false;

                // Update memory panel content
                UpdateMemoryPanel();
                break;

            case TabMode.Cpu:
                // Hide process, memory, and network panels, show CPU panel
                var storagePanel3 = _mainWindow?.FindControl<HorizontalGridControl>("storagePanel");
                if (processPanel != null)
                    processPanel.Visible = false;
                if (memoryPanel != null)
                    memoryPanel.Visible = false;
                if (cpuPanel != null)
                    cpuPanel.Visible = true;
                if (networkPanel != null)
                    networkPanel.Visible = false;
                if (storagePanel3 != null)
                    storagePanel3.Visible = false;

                // Update CPU panel content
                UpdateCpuPanel();
                break;

            case TabMode.Network:
                // Hide process, memory, and CPU panels, show network panel
                var storagePanel4 = _mainWindow?.FindControl<HorizontalGridControl>("storagePanel");
                if (processPanel != null)
                    processPanel.Visible = false;
                if (memoryPanel != null)
                    memoryPanel.Visible = false;
                if (cpuPanel != null)
                    cpuPanel.Visible = false;
                if (networkPanel != null)
                    networkPanel.Visible = true;
                if (storagePanel4 != null)
                    storagePanel4.Visible = false;

                // Update network panel content
                UpdateNetworkPanel();
                break;

            case TabMode.Storage:
                // Hide all other panels, show storage panel
                var storagePanel = _mainWindow?.FindControl<HorizontalGridControl>("storagePanel");
                if (processPanel != null)
                    processPanel.Visible = false;
                if (memoryPanel != null)
                    memoryPanel.Visible = false;
                if (cpuPanel != null)
                    cpuPanel.Visible = false;
                if (networkPanel != null)
                    networkPanel.Visible = false;
                if (storagePanel != null)
                    storagePanel.Visible = true;

                // Update storage panel content
                UpdateStoragePanel();
                break;
        }

        _mainWindow?.Invalidate(true);
    }

    private static async Task UpdateLoopAsync(Window window, CancellationToken cancellationToken)
    {
        // Prime baselines so deltas have meaning
        await PrimeStatsAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = _stats.ReadSnapshot();
                _lastSnapshot = snapshot;

                // Update clock
                var clock = window.FindControl<MarkupControl>("topStatusClock");
                if (clock != null)
                {
                    var timeStr = DateTime.Now.ToString("HH:mm:ss");
                    clock.SetContent(new List<string> { $"[grey70]{timeStr}[/]" });
                }

                // Update CPU bars
                var cpuUserBar = window.FindControl<BarGraphControl>("cpuUserBar");
                if (cpuUserBar != null) cpuUserBar.Value = snapshot.Cpu.User;

                var cpuSystemBar = window.FindControl<BarGraphControl>("cpuSystemBar");
                if (cpuSystemBar != null) cpuSystemBar.Value = snapshot.Cpu.System;

                var cpuIoWaitBar = window.FindControl<BarGraphControl>("cpuIoWaitBar");
                if (cpuIoWaitBar != null) cpuIoWaitBar.Value = snapshot.Cpu.IoWait;

                // Update memory bars
                var ioScaled = Math.Min(
                    100,
                    Math.Max(snapshot.Network.UpMbps, snapshot.Network.DownMbps)
                );

                var memUsedBar = window.FindControl<BarGraphControl>("memUsedBar");
                if (memUsedBar != null) memUsedBar.Value = snapshot.Memory.UsedPercent;

                var memCachedBar = window.FindControl<BarGraphControl>("memCachedBar");
                if (memCachedBar != null) memCachedBar.Value = snapshot.Memory.CachedPercent;

                var memIoBar = window.FindControl<BarGraphControl>("memIoBar");
                if (memIoBar != null) memIoBar.Value = Math.Min(100, ioScaled);

                // Update network bars
                var netUploadBar = window.FindControl<BarGraphControl>("netUploadBar");
                if (netUploadBar != null) netUploadBar.Value = snapshot.Network.UpMbps;

                var netDownloadBar = window.FindControl<BarGraphControl>("netDownloadBar");
                if (netDownloadBar != null) netDownloadBar.Value = snapshot.Network.DownMbps;

                // Update process list
                var processList = window.FindControl<ListControl>("processList");
                if (processList != null)
                {
                    var selectedPid = (processList.SelectedItem?.Tag as ProcessSample)?.Pid;
                    var items = BuildProcessList(snapshot.Processes);
                    processList.Items = items;

                    if (selectedPid.HasValue)
                    {
                        int idx = items.FindIndex(i =>
                            (i.Tag as ProcessSample)?.Pid == selectedPid.Value
                        );
                        if (idx >= 0)
                            processList.SelectedIndex = idx;
                    }
                }

                // Update detail panel (Processes tab)
                UpdateHighlightedProcess();

                // Update memory panel (Memory tab)
                if (_activeTab == TabMode.Memory)
                {
                    UpdateMemoryPanel();
                }

                // Update CPU panel (CPU tab)
                if (_activeTab == TabMode.Cpu)
                {
                    UpdateCpuPanel();
                }

                // Update Network panel (Network tab)
                if (_activeTab == TabMode.Network)
                {
                    UpdateNetworkPanel();
                }

                // Update Storage panel (Storage tab)
                if (_activeTab == TabMode.Storage)
                {
                    UpdateStoragePanel();
                }

                // Update bottom stats legend
                var statsLegend = window.FindControl<MarkupControl>("statsLegend");
                if (statsLegend != null)
                {
                    statsLegend.SetContent(
                        new List<string>
                        {
                            $"[grey70]CPU [cyan1]{snapshot.Cpu.User:F1}%[/] • MEM [cyan1]{snapshot.Memory.UsedPercent:F1}%[/] • NET ↑[cyan1]{snapshot.Network.UpMbps:F1}[/]/↓[cyan1]{snapshot.Network.DownMbps:F1}[/] MB/s[/]",
                        }
                    );
                }

                // Update button states
                var actionButton = window.FindControl<ButtonControl>("actionButton");
                if (actionButton != null)
                {
                    // Enable button if we have a highlighted process or cached process
                    bool hasProcess =
                        (processList?.HighlightedIndex >= 0) || (_lastHighlightedProcess != null);
                    actionButton.IsEnabled = hasProcess;
                }
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Update loop error", ex, "ConsoleTop");
            }

            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private static async Task PrimeStatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _stats.ReadSnapshot();
            await Task.Delay(300, cancellationToken);
        }
        catch
        {
            // ignore priming errors; main loop will log if persistent
        }
    }


    private static string BuildProgressBar(double percent, int width = 20)
    {
        int filled = (int)Math.Round(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        int empty = width - filled;
        return $"[cyan1]{"█".PadRight(filled, '█')}[/][dim]{"░".PadRight(empty, '░')}[/]";
    }

    private static List<ListItem> BuildProcessList(IReadOnlyList<ProcessSample> processes)
    {
        // Apply sorting based on current sort mode
        var sorted = _processSortMode switch
        {
            ProcessSortMode.Cpu => processes.OrderByDescending(p => p.CpuPercent),
            ProcessSortMode.Memory => processes.OrderByDescending(p => p.MemPercent),
            ProcessSortMode.Pid => processes.OrderBy(p => p.Pid),
            ProcessSortMode.Name => processes.OrderBy(p => p.Command, StringComparer.OrdinalIgnoreCase),
            _ => processes.OrderByDescending(p => p.CpuPercent)
        };

        var items = new List<ListItem>();

        foreach (var p in sorted)
        {
            // Format values with consistent padding before applying markup
            var pidStr = p.Pid.ToString().PadLeft(8);
            var cpuStr = $"{p.CpuPercent,5:F1}%".PadLeft(7);
            var memStr = $"{p.MemPercent,5:F1}%".PadLeft(7);
            var line = $"{pidStr}  [grey70]{cpuStr}[/]  [grey70]{memStr}[/]  [cyan1]{p.Command}[/]";
            var item = new ListItem(line) { Tag = p };
            items.Add(item);
        }

        if (items.Count == 0)
        {
            items.Add(new ListItem("  [red]No process data available[/]") { IsEnabled = false });
        }

        return items;
    }

    private static void UpdateProcessList()
    {
        if (_mainWindow == null || _lastSnapshot == null)
            return;

        var processList = _mainWindow.FindControl<ListControl>("processList");
        if (processList != null)
        {
            // Remember current selection by PID
            var selectedPid = (processList.SelectedItem?.Tag as ProcessSample)?.Pid;

            // Rebuild and update the list
            var items = BuildProcessList(_lastSnapshot.Processes);
            processList.Items = items;

            // Restore selection if possible
            if (selectedPid.HasValue)
            {
                int idx = items.FindIndex(i => (i.Tag as ProcessSample)?.Pid == selectedPid.Value);
                if (idx >= 0)
                    processList.SelectedIndex = idx;
            }
        }
    }

    private static void UpdateMemoryPanel()
    {
        if (_mainWindow == null)
            return;

        // Find the memory panel grid
        var memoryPanel = _mainWindow.FindControl<HorizontalGridControl>("memoryPanel");
        if (memoryPanel == null)
        {
            _windowSystem?.LogService.LogWarning("UpdateMemoryPanel: Panel not found", "Memory");
            return;
        }

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        UpdateMemoryHistory(snapshot.Memory);

        // Update the graph controls with new data
        UpdateMemoryGraphControls(memoryPanel, snapshot);
    }

    private static void UpdateMemoryGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        // Find the right column (column 2) which has the graphs
        if (grid.Columns.Count < 3)
        {
            _windowSystem?.LogService.LogDebug($"UpdateMemoryGraphControls: Grid has {grid.Columns.Count} columns, expected 3", "Memory");
            return;
        }

        var rightCol = grid.Columns[2];
        var rightPanel = rightCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        if (rightPanel == null)
        {
            _windowSystem?.LogService.LogDebug("UpdateMemoryGraphControls: Right panel not found", "Memory");
            return;
        }

        _windowSystem?.LogService.LogDebug($"UpdateMemoryGraphControls: Right panel has {rightPanel.Children.Count} children", "Memory");

        var mem = snapshot.Memory;

        // Update bar graphs
        var ramUsedBar = rightPanel.Children.FirstOrDefault(c => c.Name == "ramUsedBar") as BarGraphControl;
        if (ramUsedBar != null)
        {
            _windowSystem?.LogService.LogDebug($"UpdateMemoryGraphControls: Updating ramUsedBar to {mem.UsedPercent:F1}%", "Memory");
            ramUsedBar.Value = mem.UsedPercent;
        }
        else
        {
            _windowSystem?.LogService.LogDebug("UpdateMemoryGraphControls: ramUsedBar not found", "Memory");
        }

        var ramFreeBar = rightPanel.Children.FirstOrDefault(c => c.Name == "ramFreeBar") as BarGraphControl;
        if (ramFreeBar != null)
        {
            var freePercent = (mem.AvailableMb / mem.TotalMb) * 100;
            _windowSystem?.LogService.LogDebug($"UpdateMemoryGraphControls: Updating ramFreeBar to {freePercent:F1}%", "Memory");
            ramFreeBar.Value = freePercent;
        }
        else
        {
            _windowSystem?.LogService.LogDebug("UpdateMemoryGraphControls: ramFreeBar not found", "Memory");
        }

        var swapBar = rightPanel.Children.FirstOrDefault(c => c.Name == "swapUsedBar") as BarGraphControl;
        if (swapBar != null)
        {
            double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
            swapBar.Value = swapPercent;
        }

        // Update sparklines
        var usedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "memoryUsedSparkline") as SparklineControl;
        if (usedSparkline != null)
            usedSparkline.SetDataPoints(_memoryUsedHistory);

        var cachedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "memoryCachedSparkline") as SparklineControl;
        if (cachedSparkline != null)
            cachedSparkline.SetDataPoints(_memoryCachedHistory);

        var freeSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "memoryFreeSparkline") as SparklineControl;
        if (freeSparkline != null)
            freeSparkline.SetDataPoints(_memoryAvailableHistory);

        // Update left column text stats
        if (grid.Columns.Count > 0)
        {
            var leftCol = grid.Columns[0];
            var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
            if (leftPanel != null && leftPanel.Children.Count > 0)
            {
                var markup = leftPanel.Children[0] as MarkupControl;
                if (markup != null)
                {
                    var lines = BuildMemoryTextContent(snapshot);
                    markup.SetContent(lines);
                }
            }
        }
    }

    private static void UpdateMemoryHistory(MemorySample memory)
    {
        _memoryUsedHistory.Add(memory.UsedPercent);
        _memoryAvailableHistory.Add((memory.AvailableMb / memory.TotalMb) * 100);
        _memoryCachedHistory.Add(memory.CachedPercent);

        double swapPercent = memory.SwapTotalMb > 0 ? (memory.SwapUsedMb / memory.SwapTotalMb * 100) : 0;
        _swapUsedHistory.Add(swapPercent);

        // Trim to max points
        while (_memoryUsedHistory.Count > MAX_HISTORY_POINTS)
            _memoryUsedHistory.RemoveAt(0);
        while (_memoryAvailableHistory.Count > MAX_HISTORY_POINTS)
            _memoryAvailableHistory.RemoveAt(0);
        while (_memoryCachedHistory.Count > MAX_HISTORY_POINTS)
            _memoryCachedHistory.RemoveAt(0);
        while (_swapUsedHistory.Count > MAX_HISTORY_POINTS)
            _swapUsedHistory.RemoveAt(0);
    }

    private static HorizontalGridControl BuildResponsiveMemoryGrid(int windowWidth, SystemSnapshot snapshot)
    {
        var desiredLayout = windowWidth >= MEMORY_LAYOUT_THRESHOLD_WIDTH
            ? MemoryLayoutMode.Wide
            : MemoryLayoutMode.Narrow;

        _currentMemoryLayout = desiredLayout;

        _windowSystem?.LogService.LogDebug(
            $"BuildResponsiveMemoryGrid: Building initial layout in {desiredLayout} mode (width={windowWidth})",
            "Memory");

        if (desiredLayout == MemoryLayoutMode.Wide)
        {
            return BuildWideMemoryGridInitial(snapshot);
        }
        else
        {
            return BuildNarrowMemoryGridInitial(snapshot);
        }
    }

    private static HorizontalGridControl BuildWideMemoryGridInitial(SystemSnapshot snapshot)
    {
        var lines = BuildMemoryTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("memoryPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Left column: Scrollable text info (fixed width)
            .Column(col =>
            {
                col.Width(40); // Fixed width for text stats
                var leftPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                leftPanel.BackgroundColor = Color.Grey11;
                leftPanel.ForegroundColor = Color.Grey93;

                var markup = Controls.Markup();
                foreach (var line in lines)
                {
                    markup = markup.AddLine(line);
                }
                leftPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());
                col.Add(leftPanel);
            })
            // Middle column: Separator (1 char wide)
            .Column(col =>
            {
                col.Width(1);
                col.Add(new SeparatorControl
                {
                    ForegroundColor = Color.Grey23,
                    VerticalAlignment = VerticalAlignment.Fill
                });
            })
            // Right column: Scrollable graphs (fills remaining space)
            .Column(col =>
            {
                // No width set - fills remaining space responsively
                var rightPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                rightPanel.BackgroundColor = Color.Grey11;
                rightPanel.ForegroundColor = Color.Grey93;
                BuildMemoryGraphsContent(rightPanel, snapshot);
                col.Add(rightPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static HorizontalGridControl BuildNarrowMemoryGridInitial(SystemSnapshot snapshot)
    {
        var lines = BuildMemoryTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("memoryPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Single column: Scrollable panel with ALL content (text + graphs)
            .Column(col =>
            {
                var scrollPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                scrollPanel.BackgroundColor = Color.Grey11;
                scrollPanel.ForegroundColor = Color.Grey93;

                // Add text content
                var markup = Controls.Markup();
                foreach (var line in lines)
                {
                    markup = markup.AddLine(line);
                }
                scrollPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

                // Add separator
                scrollPanel.AddControl(
                    Controls
                        .Markup()
                        .AddLine("")
                        .AddLine("[grey23]────────────────────────────────────────[/]")
                        .AddLine("")
                        .WithAlignment(HorizontalAlignment.Left)
                        .WithMargin(2, 1, 2, 0)
                        .Build()
                );

                // Add all graphs (same as wide mode right panel)
                BuildMemoryGraphsContent(scrollPanel, snapshot);

                col.Add(scrollPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static void BuildWideMemoryColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildWideMemoryColumns: Starting", "Memory");

        var lines = BuildMemoryTextContent(snapshot);

        // Left column: Scrollable text info
        var leftPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        leftPanel.BackgroundColor = Color.Grey11;
        leftPanel.ForegroundColor = Color.Grey93;

        var markup = Controls.Markup();
        foreach (var line in lines)
        {
            markup = markup.AddLine(line);
        }
        leftPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

        var leftCol = new ColumnContainer(grid);
        leftCol.Width = 40; // Fixed width for text stats
        leftCol.AddContent(leftPanel);
        grid.AddColumn(leftCol);

        // Middle column: Separator (1 char wide)
        var sepCol = new ColumnContainer(grid);
        sepCol.Width = 1;
        sepCol.AddContent(new SeparatorControl
        {
            ForegroundColor = Color.Grey23,
            VerticalAlignment = VerticalAlignment.Fill
        });
        grid.AddColumn(sepCol);

        // Right column: Scrollable graphs
        var rightPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        rightPanel.BackgroundColor = Color.Grey11;
        rightPanel.ForegroundColor = Color.Grey93;
        BuildMemoryGraphsContent(rightPanel, snapshot);

        var rightCol = new ColumnContainer(grid);
        rightCol.AddContent(rightPanel);
        grid.AddColumn(rightCol);

        _windowSystem?.LogService.LogDebug($"BuildWideMemoryColumns: Added 3 columns", "Memory");
    }

    private static void BuildNarrowMemoryColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildNarrowMemoryColumns: Starting", "Memory");

        var lines = BuildMemoryTextContent(snapshot);

        // Single column: Scrollable panel with ALL content (text + graphs)
        var scrollPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        scrollPanel.BackgroundColor = Color.Grey11;
        scrollPanel.ForegroundColor = Color.Grey93;

        // Add text content
        var markup = Controls.Markup();
        foreach (var line in lines)
        {
            markup = markup.AddLine(line);
        }
        scrollPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

        // Add separator
        scrollPanel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine("[grey23]────────────────────────────────────────[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 1, 2, 0)
                .Build()
        );

        // Add all graphs (same as wide mode right panel)
        BuildMemoryGraphsContent(scrollPanel, snapshot);

        var col = new ColumnContainer(grid);
        col.AddContent(scrollPanel);
        grid.AddColumn(col);

        _windowSystem?.LogService.LogDebug($"BuildNarrowMemoryColumns: Added 1 column with full content", "Memory");
    }

    private static void BuildMemoryGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;

        // Title
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine("[cyan1 bold]═══ Memory Visualization ═══[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Current Usage Section
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey70 bold]Current Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // RAM Usage Bar - smooth warm gradient (cool at low, warm at high)
        // LabelWidth 9 aligns all memory bars (longest label: "Swap Used")
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("ramUsedBar")
                .WithLabel("RAM Used")
                .WithLabelWidth(9)
                .WithValue(mem.UsedPercent)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(Color.Green, Color.Yellow, Color.Orange1, Color.Red)  // Smooth gradient
                .Build()
        );

        // RAM Free Bar - reversed smooth gradient (more free = greener)
        var freePercent = (mem.AvailableMb / mem.TotalMb) * 100;
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("ramFreeBar")
                .WithLabel("RAM Free")
                .WithLabelWidth(9)
                .WithValue(freePercent)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(Color.Red, Color.Orange1, Color.Yellow, Color.Green)  // Red → Green
                .Build()
        );

        // Swap Usage Bar - smooth gradient (any swap usage is potentially concerning)
        double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("swapUsedBar")
                .WithLabel("Swap Used")
                .WithLabelWidth(9)
                .WithValue(swapPercent)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 2)
                .WithSmoothGradient("warm")  // Predefined warm gradient
                .Build()
        );

        // Separator
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey23]────────────────────────────────────────[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Memory History Sparklines
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey70 bold]Historical Trends[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        // Used sparkline - with cool gradient (blue→cyan) and baseline
        var usedSparkline = new SparklineBuilder()
            .WithName("memoryUsedSparkline")
            .WithTitle("Memory Used %")
            .WithTitleColor(Color.Cyan1)
            .WithTitlePosition(TitlePosition.Bottom)
            .WithHeight(6)
            .WithMaxValue(100)
            .WithGradient("cool")  // Predefined cool gradient (blue→cyan)
            .WithBackgroundColor(Color.Grey15)
            .WithBorder(BorderStyle.None)
            .WithMode(SparklineMode.Block) // 8 levels for better detail and readability
            .WithBaseline(true, position: TitlePosition.Bottom)
            .WithInlineTitleBaseline(true)  // Compact: "Memory Used % ┈┈┈"
            .WithMargin(2, 0, 1, 0)
            .WithData(_memoryUsedHistory)
            .Build();
        panel.AddControl(usedSparkline);

        // Cached sparkline - with warm gradient
        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryCachedSparkline")
                .WithTitle("Memory Cached %")
                .WithTitleColor(Color.Yellow)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithGradient(Color.Yellow, Color.Orange1)  // Yellow→Orange gradient
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_memoryCachedHistory)
                .Build()
        );

        // Free sparkline - with spectrum gradient
        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryFreeSparkline")
                .WithTitle("Memory Available %")
                .WithTitleColor(Color.Green)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithGradient(Color.Blue, Color.Green)  // Blue→Green gradient
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_memoryAvailableHistory)
                .Build()
        );
    }

    private static List<string> BuildMemoryTextContent(SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;

        // Get top 5 memory consumers
        var topMemProcs = snapshot.Processes.OrderByDescending(p => p.MemPercent).Take(5).ToList();

        var lines = new List<string>
        {
            "",
            "[cyan1 bold]System Memory[/]",
            "",
            "[grey70 bold]Statistics[/]",
            $"  [grey70]Total:[/]     [cyan1]{mem.TotalMb:F0} MB[/]",
            $"  [grey70]Used:[/]      [cyan1]{mem.UsedMb:F0} MB[/] [grey50]({mem.UsedPercent:F1}%)[/]",
            $"  [grey70]Available:[/] [cyan1]{mem.AvailableMb:F0} MB[/]",
            $"  [grey70]Cached:[/]    [cyan1]{mem.CachedMb:F0} MB[/] [grey50]({mem.CachedPercent:F1}%)[/]",
            $"  [grey70]Buffers:[/]   [cyan1]{mem.BuffersMb:F0} MB[/]",
             "",
             "[grey70 bold]Swap[/]",
            $"  [grey70]Total:[/] [cyan1]{mem.SwapTotalMb:F0} MB[/]",
            $"  [grey70]Used:[/]  [cyan1]{mem.SwapUsedMb:F0} MB[/]",
            $"  [grey70]Free:[/]  [cyan1]{mem.SwapFreeMb:F0} MB[/]",
            "",
            "[grey70 bold]Top Memory Consumers[/]",
        };

        foreach (var p in topMemProcs)
        {
            lines.Add($"  [cyan1]{p.MemPercent, 5:F1}%[/]  [grey70]{p.Pid, 6}[/]  {p.Command}");
        }

        return lines;
    }

    private static void HandleMemoryPanelResize()
    {
        if (_mainWindow == null)
            return;

        var memoryPanel = _mainWindow.FindControl<HorizontalGridControl>("memoryPanel");
        if (memoryPanel == null || !memoryPanel.Visible)
            return; // Only handle resize if memory panel is visible

        int windowWidth = _mainWindow.Width;
        var desiredLayout = windowWidth >= MEMORY_LAYOUT_THRESHOLD_WIDTH
            ? MemoryLayoutMode.Wide
            : MemoryLayoutMode.Narrow;

        // Only rebuild if layout mode changed
        if (desiredLayout != _currentMemoryLayout)
        {
            _windowSystem?.LogService.LogDebug(
                $"HandleMemoryPanelResize: Switching from {_currentMemoryLayout} to {desiredLayout} (width={windowWidth})",
                "Memory");

            _currentMemoryLayout = desiredLayout;
            RebuildMemoryPanelColumns(memoryPanel, desiredLayout);
        }
    }

    private static void RebuildMemoryPanelColumns(HorizontalGridControl grid, MemoryLayoutMode mode)
    {
        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();

        _windowSystem?.LogService.LogDebug($"RebuildMemoryPanelColumns: Starting rebuild in {mode} mode", "Memory");

        // Clear existing columns
        // Must clear in reverse order to avoid index issues with splitters
        for (int i = grid.Columns.Count - 1; i >= 0; i--)
        {
            grid.RemoveColumn(grid.Columns[i]);
        }

        _windowSystem?.LogService.LogDebug($"RebuildMemoryPanelColumns: Cleared {grid.Columns.Count} columns", "Memory");

        // Rebuild based on mode
        if (mode == MemoryLayoutMode.Wide)
        {
            BuildWideMemoryColumns(grid, snapshot);
        }
        else
        {
            BuildNarrowMemoryColumns(grid, snapshot);
        }

        // Force complete DOM tree rebuild (not just invalidation)
        // This is necessary because the column structure changed, not just properties
        _mainWindow?.ForceRebuildLayout();

        _windowSystem?.LogService.LogDebug($"RebuildMemoryPanelColumns: Rebuild complete, grid now has {grid.Columns.Count} columns", "Memory");
    }

    private static void UpdateHighlightedProcess()
    {
        var processList = _mainWindow?.FindControl<ListControl>("processList");
        var detailContent = _mainWindow?.FindControl<MarkupControl>("processDetailContent");
        var detailToolbar = _mainWindow?.FindControl<ToolbarControl>("detailToolbar");
        var actionButton = _mainWindow?.FindControl<ButtonControl>("actionButton");

        if (processList == null || detailContent == null)
            return;

        // Determine which process to show
        ProcessSample? processToShow = null;

        // First try: current highlighted item in list
        if (
            processList.HighlightedIndex >= 0
            && processList.HighlightedIndex < processList.Items.Count
        )
        {
            processToShow = processList.Items[processList.HighlightedIndex].Tag as ProcessSample;
        }
        // Second try: last highlighted process (when list lost focus)
        else if (_lastHighlightedProcess != null)
        {
            processToShow = _lastHighlightedProcess;
        }

        if (processToShow == null)
        {
            // No process to show: Show placeholder
            if (detailToolbar != null)
                detailToolbar.Visible = false;
            if (actionButton != null)
                actionButton.Visible = false;

            detailContent.SetContent(
                new List<string> { "", "[grey50 italic]Select a process to view details[/]" }
            );
            return;
        }

        // Show process details with Actions button
        if (detailToolbar != null)
            detailToolbar.Visible = true;
        if (actionButton != null)
            actionButton.Visible = true;

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        var lines = BuildProcessDetailsContent(processToShow, snapshot);
        detailContent.SetContent(lines);
    }


    private static List<string> BuildProcessDetailsContent(
        ProcessSample sample,
        SystemSnapshot snapshot
    )
    {
        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        return new List<string>
        {
            "",
            $"[cyan1 bold]{liveProc.Command}[/]",
            "",
            $"[grey70]PID:[/] [cyan1]{liveProc.Pid}[/]",
            $"[grey70]Executable:[/] [cyan1]{extra.ExePath}[/]",
            "",
            $"[grey70 bold]Process Metrics[/]",
            $"  [grey70]CPU:[/] [cyan1]{liveProc.CpuPercent:F1}%[/]",
            $"  [grey70]Memory:[/] [cyan1]{liveProc.MemPercent:F1}%[/]",
            $"  [grey70]State:[/] [cyan1]{extra.State}[/]  [grey70]Threads:[/] [cyan1]{extra.Threads}[/]",
            $"  [grey70]RSS:[/] [cyan1]{extra.RssMb:F1} MB[/]",
            $"  [grey70]I/O:[/] [cyan1]↑{extra.ReadKb:F0} / ↓{extra.WriteKb:F0} KB/s[/]",
            "",
            $"[grey70 bold]System Snapshot[/]",
            $"  [grey70]CPU:[/] usr [cyan1]{snapshot.Cpu.User:F1}%[/] / sys [cyan1]{snapshot.Cpu.System:F1}%[/] / io [cyan1]{snapshot.Cpu.IoWait:F1}%[/]",
            $"  [grey70]Memory:[/] used [cyan1]{snapshot.Memory.UsedPercent:F1}%[/] / cached [cyan1]{snapshot.Memory.CachedPercent:F1}%[/]",
            $"  [grey70]Network:[/] ↑[cyan1]{snapshot.Network.UpMbps:F1}[/] / ↓[cyan1]{snapshot.Network.DownMbps:F1}[/] MB/s",
        };
    }

    private static void ShowProcessActionsDialog(ProcessSample? sample = null)
    {
        // Get highlighted process if not provided
        if (sample == null)
        {
            var processList = _mainWindow?.FindControl<ListControl>("processList");
            if (
                processList != null
                && processList.HighlightedIndex >= 0
                && processList.HighlightedIndex < processList.Items.Count
            )
            {
                sample = processList.Items[processList.HighlightedIndex].Tag as ProcessSample;
            }

            // Fallback to last highlighted process (in case list lost focus and cleared highlight)
            if (sample == null)
            {
                sample = _lastHighlightedProcess;
            }
        }

        if (sample == null || _windowSystem == null)
            return;

        var snapshot = _stats.ReadSnapshot();
        _lastSnapshot = snapshot;

        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        var modal = new WindowBuilder(_windowSystem)
            .WithTitle("Process Actions")
            .Centered()
            .WithSize(70, 18)
            .AsModal()
            .Borderless()
            .Resizable(false)
            .Movable(false)
            .WithColors(Color.Grey15, Color.Grey93)
            .Build();

        // Modern header
        modal.AddControl(
            Controls
                .Markup()
                .AddLine($"[cyan1 bold]Process {liveProc.Pid}[/]")
                .AddLine($"[grey70]{liveProc.Command}[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 0)
                .Build()
        );

        modal.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // Process details with improved formatting
        modal.AddControl(
            Controls
                .Markup()
                .AddLine($"[grey70]Executable:[/] [cyan1]{extra.ExePath}[/]")
                .AddLine("")
                .AddLine(
                    $"[grey70]CPU:[/] [cyan1]{liveProc.CpuPercent:F1}%[/]  [grey70]Memory:[/] [cyan1]{liveProc.MemPercent:F1}%[/]"
                )
                .AddLine(
                    $"[grey70]State:[/] [cyan1]{extra.State}[/]  [grey70]Threads:[/] [cyan1]{extra.Threads}[/]"
                )
                .AddLine($"[grey70]RSS:[/] [cyan1]{extra.RssMb:F1} MB[/]")
                .AddLine($"[grey70]I/O:[/] [cyan1]↑{extra.ReadKb:F0} / ↓{extra.WriteKb:F0} KB/s[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 1)
                .Build()
        );

        modal.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // Create buttons with event handlers (platform-specific)
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        HorizontalGridControl buttonRow;
        ButtonControl closeButton;

        if (isWindows)
        {
            // Windows: Terminate (graceful) and Force Kill options
            var terminateButton = new ButtonControl { Text = "Terminate", Width = 14 };
            terminateButton.Click += (_, _) =>
            {
                TryTerminateProcess(liveProc.Pid, liveProc.Command);
                modal.Close();
            };

            var forceKillButton = new ButtonControl { Text = "Force Kill", Width = 14 };
            forceKillButton.Click += (_, _) =>
            {
                TryKillProcess(liveProc.Pid, liveProc.Command);
                modal.Close();
            };

            closeButton = new ButtonControl { Text = "Close", Width = 10 };
            closeButton.Click += (_, _) => modal.Close();

            buttonRow = HorizontalGridControl.ButtonRow(
                terminateButton,
                forceKillButton,
                closeButton
            );
        }
        else
        {
            // Linux: SIGTERM and SIGKILL options
            var sigtermButton = new ButtonControl { Text = "SIGTERM", Width = 12 };
            sigtermButton.Click += (_, _) =>
            {
                TryTerminateProcess(liveProc.Pid, liveProc.Command);
                modal.Close();
            };

            var sigkillButton = new ButtonControl { Text = "SIGKILL", Width = 12 };
            sigkillButton.Click += (_, _) =>
            {
                TryKillProcess(liveProc.Pid, liveProc.Command);
                modal.Close();
            };

            closeButton = new ButtonControl { Text = "Close", Width = 10 };
            closeButton.Click += (_, _) => modal.Close();

            buttonRow = HorizontalGridControl.ButtonRow(sigtermButton, sigkillButton, closeButton);
        }

        modal.AddControl(buttonRow);

        // Focus the close button by default
        closeButton.SetFocus(true, FocusReason.Programmatic);

        _windowSystem.AddWindow(modal);
        _windowSystem.SetActiveWindow(modal);
    }

    private static void TryTerminateProcess(int pid, string command)
    {
        if (_windowSystem == null)
            return;

        try
        {
            var proc = Process.GetProcessById(pid);

            // Try graceful termination
            // On Windows: Sends WM_CLOSE to process (like closing a window)
            // On Linux: Sends SIGTERM (allows cleanup)
            if (!proc.CloseMainWindow())
            {
                // Process has no main window or didn't respond, fall back to Kill
                proc.Kill();
                _windowSystem.NotificationStateService.ShowNotification(
                    $"✓ Force killed {pid}",
                    $"{command} (PID {pid}) had no main window, force terminated",
                    NotificationSeverity.Warning,
                    blockUi: false,
                    timeout: 2500,
                    parentWindow: _mainWindow
                );
            }
            else
            {
                _windowSystem.NotificationStateService.ShowNotification(
                    $"✓ Terminated {pid}",
                    $"{command} (PID {pid}) gracefully terminated",
                    NotificationSeverity.Info,
                    blockUi: false,
                    timeout: 2000,
                    parentWindow: _mainWindow
                );
            }
        }
        catch (Exception ex)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"⚠ Terminate failed for {pid}",
                ex.Message,
                NotificationSeverity.Warning,
                blockUi: false,
                timeout: 3000,
                parentWindow: _mainWindow
            );
        }
    }

    private static void TryKillProcess(int pid, string command)
    {
        if (_windowSystem == null)
            return;

        try
        {
            var proc = Process.GetProcessById(pid);
            proc.Kill();

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var killMethod = isWindows ? "Force killed" : "SIGKILL sent to";

            _windowSystem.NotificationStateService.ShowNotification(
                $"✓ {killMethod} {pid}",
                $"{command} (PID {pid}) force terminated",
                NotificationSeverity.Info,
                blockUi: false,
                timeout: 2000,
                parentWindow: _mainWindow
            );
        }
        catch (Exception ex)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"⚠ Kill failed for {pid}",
                ex.Message,
                NotificationSeverity.Warning,
                blockUi: false,
                timeout: 3000,
                parentWindow: _mainWindow
            );
        }
    }

    // ========================================================================
    // CPU PANEL METHODS
    // ========================================================================

    private static void UpdateCpuPanel()
    {
        if (_mainWindow == null)
            return;

        // Find the CPU panel grid
        var cpuPanel = _mainWindow.FindControl<HorizontalGridControl>("cpuPanel");
        if (cpuPanel == null)
        {
            _windowSystem?.LogService.LogWarning("UpdateCpuPanel: Panel not found", "CPU");
            return;
        }

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        UpdateCpuHistory(snapshot.Cpu);

        // Update the graph controls with new data
        UpdateCpuGraphControls(cpuPanel, snapshot);
    }

    private static void UpdateCpuHistory(CpuSample cpu)
    {
        // Update aggregate history
        _cpuUserHistory.Add(cpu.User);
        _cpuSystemHistory.Add(cpu.System);
        _cpuIoWaitHistory.Add(cpu.IoWait);

        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        _cpuTotalHistory.Add(totalCpu);

        // Update per-core history
        if (cpu.PerCoreSamples != null)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                if (!_cpuPerCoreHistory.ContainsKey(core.CoreIndex))
                {
                    _cpuPerCoreHistory[core.CoreIndex] = new List<double>();
                }

                double coreTotal = core.User + core.System + core.IoWait;
                _cpuPerCoreHistory[core.CoreIndex].Add(coreTotal);

                // Trim per-core history to max points
                while (_cpuPerCoreHistory[core.CoreIndex].Count > MAX_HISTORY_POINTS)
                {
                    _cpuPerCoreHistory[core.CoreIndex].RemoveAt(0);
                }
            }
        }

        // Trim aggregate histories to max points
        while (_cpuUserHistory.Count > MAX_HISTORY_POINTS)
            _cpuUserHistory.RemoveAt(0);
        while (_cpuSystemHistory.Count > MAX_HISTORY_POINTS)
            _cpuSystemHistory.RemoveAt(0);
        while (_cpuIoWaitHistory.Count > MAX_HISTORY_POINTS)
            _cpuIoWaitHistory.RemoveAt(0);
        while (_cpuTotalHistory.Count > MAX_HISTORY_POINTS)
            _cpuTotalHistory.RemoveAt(0);
    }

    private static void UpdateCpuGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        // Find the right column (column 2) which has the graphs
        if (grid.Columns.Count < 3)
        {
            _windowSystem?.LogService.LogDebug($"UpdateCpuGraphControls: Grid has {grid.Columns.Count} columns, expected 3", "CPU");
            return;
        }

        var rightCol = grid.Columns[2];
        var rightPanel = rightCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        if (rightPanel == null)
        {
            _windowSystem?.LogService.LogDebug("UpdateCpuGraphControls: Right panel not found", "CPU");
            return;
        }

        var cpu = snapshot.Cpu;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;

        // Update aggregate bar graphs
        var userBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuUserBar") as BarGraphControl;
        if (userBar != null)
            userBar.Value = cpu.User;

        var systemBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuSystemBar") as BarGraphControl;
        if (systemBar != null)
            systemBar.Value = cpu.System;

        var ioWaitBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuIoWaitBar") as BarGraphControl;
        if (ioWaitBar != null)
            ioWaitBar.Value = cpu.IoWait;

        var totalBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuTotalBar") as BarGraphControl;
        if (totalBar != null)
            totalBar.Value = totalCpu;

        // Update aggregate sparklines
        var userSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuUserSparkline") as SparklineControl;
        if (userSparkline != null)
            userSparkline.SetDataPoints(_cpuUserHistory);

        var systemSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuSystemSparkline") as SparklineControl;
        if (systemSparkline != null)
            systemSparkline.SetDataPoints(_cpuSystemHistory);

        var totalSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuTotalSparkline") as SparklineControl;
        if (totalSparkline != null)
            totalSparkline.SetDataPoints(_cpuTotalHistory);

        // Update per-core sparklines
        if (cpu.PerCoreSamples != null)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                var coreSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"cpuCore{core.CoreIndex}Sparkline") as SparklineControl;
                if (coreSparkline != null && _cpuPerCoreHistory.ContainsKey(core.CoreIndex))
                {
                    coreSparkline.SetDataPoints(_cpuPerCoreHistory[core.CoreIndex]);
                }
            }
        }

        // Update left column per-core bars
        if (grid.Columns.Count > 0 && cpu.PerCoreSamples != null)
        {
            var leftCol = grid.Columns[0];
            var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
            if (leftPanel != null)
            {
                foreach (var core in cpu.PerCoreSamples)
                {
                    double coreTotal = core.User + core.System + core.IoWait;
                    var coreBar = leftPanel.Children.FirstOrDefault(c => c.Name == $"cpuCoreLeftBar{core.CoreIndex}") as BarGraphControl;
                    if (coreBar != null)
                    {
                        coreBar.Value = coreTotal;
                    }
                }
            }
        }
    }

    private static HorizontalGridControl BuildResponsiveCpuGrid(int windowWidth, SystemSnapshot snapshot)
    {
        var desiredLayout = windowWidth >= CPU_LAYOUT_THRESHOLD_WIDTH
            ? CpuLayoutMode.Wide
            : CpuLayoutMode.Narrow;

        _currentCpuLayout = desiredLayout;

        _windowSystem?.LogService.LogDebug(
            $"BuildResponsiveCpuGrid: Building initial layout in {desiredLayout} mode (width={windowWidth})",
            "CPU");

        if (desiredLayout == CpuLayoutMode.Wide)
        {
            return BuildWideCpuGridInitial(snapshot);
        }
        else
        {
            return BuildNarrowCpuGridInitial(snapshot);
        }
    }

    private static HorizontalGridControl BuildWideCpuGridInitial(SystemSnapshot snapshot)
    {
        var grid = Controls
            .HorizontalGrid()
            .WithName("cpuPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Left column: Scrollable panel with controls (fixed width)
            .Column(col =>
            {
                col.Width(40); // Fixed width for text stats
                var leftPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                leftPanel.BackgroundColor = Color.Grey11;
                leftPanel.ForegroundColor = Color.Grey93;
                BuildCpuLeftPanelContent(leftPanel, snapshot);
                col.Add(leftPanel);
            })
            // Middle column: Separator (1 char wide)
            .Column(col =>
            {
                col.Width(1);
                col.Add(new SeparatorControl
                {
                    ForegroundColor = Color.Grey23,
                    VerticalAlignment = VerticalAlignment.Fill
                });
            })
            // Right column: Scrollable graphs (fills remaining space)
            .Column(col =>
            {
                // No width set - fills remaining space responsively
                var rightPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                rightPanel.BackgroundColor = Color.Grey11;
                rightPanel.ForegroundColor = Color.Grey93;
                BuildCpuGraphsContent(rightPanel, snapshot);
                col.Add(rightPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static HorizontalGridControl BuildNarrowCpuGridInitial(SystemSnapshot snapshot)
    {
        var grid = Controls
            .HorizontalGrid()
            .WithName("cpuPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Single column: Scrollable panel with ALL content (text + graphs)
            .Column(col =>
            {
                var scrollPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                scrollPanel.BackgroundColor = Color.Grey11;
                scrollPanel.ForegroundColor = Color.Grey93;

                // Add left panel content with BarGraphControls
                BuildCpuLeftPanelContent(scrollPanel, snapshot);

                // Add separator
                scrollPanel.AddControl(
                    Controls
                        .Markup()
                        .AddLine("")
                        .AddLine("[grey23]────────────────────────────────────────[/]")
                        .AddLine("")
                        .WithAlignment(HorizontalAlignment.Left)
                        .WithMargin(2, 1, 2, 0)
                        .Build()
                );

                // Add all graphs (same as wide mode right panel)
                BuildCpuGraphsContent(scrollPanel, snapshot);

                col.Add(scrollPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }


    private static void BuildCpuLeftPanelContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        int coreCount = cpu.PerCoreSamples != null && cpu.PerCoreSamples.Count > 0
            ? cpu.PerCoreSamples.Count
            : Environment.ProcessorCount;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        double idleCpu = Math.Max(0, 100 - totalCpu);

        // Get top 5 CPU consumers
        var topCpuProcs = snapshot.Processes.OrderByDescending(p => p.CpuPercent).Take(5).ToList();

        // Header and aggregate stats
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine($"[cyan1 bold]System CPU ({coreCount} cores)[/]")
                .AddLine("")
                .AddLine("[grey70 bold]Aggregate Usage[/]")
                .AddLine($"  [grey70]User:[/]      [red]{cpu.User:F1}%[/]")
                .AddLine($"  [grey70]System:[/]    [yellow]{cpu.System:F1}%[/]")
                .AddLine($"  [grey70]IoWait:[/]    [blue]{cpu.IoWait:F1}%[/] [grey50](Linux only)[/]")
                .AddLine($"  [grey70]Total:[/]     [cyan1]{totalCpu:F1}%[/]")
                .AddLine($"  [grey70]Idle:[/]      [green]{idleCpu:F1}%[/]")
                .AddLine("")
                .AddLine("[grey70 bold]Per-Core Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );

        // Per-core BarGraphControls
        if (cpu.PerCoreSamples != null && cpu.PerCoreSamples.Count > 0)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                double coreTotal = core.User + core.System + core.IoWait;
                panel.AddControl(
                    new BarGraphBuilder()
                        .WithName($"cpuCoreLeftBar{core.CoreIndex}")
                        .WithLabel($"C{core.CoreIndex,2}")
                        .WithLabelWidth(3)
                        .WithValue(coreTotal)
                        .WithMaxValue(100)
                        .WithBarWidth(16)
                        .WithUnfilledColor(Color.Grey35)
                        .ShowLabel()
                        .ShowValue()
                        .WithValueFormat("F1")
                        .WithMargin(0, 0, 0, 0)
                        .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                        .Build()
                );
            }
        }
        else
        {
            // Show placeholder bars if data not yet available
            for (int i = 0; i < coreCount; i++)
            {
                panel.AddControl(
                    new BarGraphBuilder()
                        .WithName($"cpuCoreLeftBar{i}")
                        .WithLabel($"C{i,2}")
                        .WithLabelWidth(3)
                        .WithValue(0)
                        .WithMaxValue(100)
                        .WithBarWidth(16)
                        .WithUnfilledColor(Color.Grey35)
                        .ShowLabel()
                        .ShowValue()
                        .WithValueFormat("F1")
                        .WithMargin(0, 0, 0, 0)
                        .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                        .Build()
                );
            }
        }

        // Top CPU Consumers
        var markup = Controls.Markup()
            .AddLine("")
            .AddLine("[grey70 bold]Top CPU Consumers[/]");
        foreach (var p in topCpuProcs)
        {
            markup = markup.AddLine($"  [cyan1]{p.CpuPercent,5:F1}%[/]  [grey70]{p.Pid,6}[/]  {p.Command}");
        }

        panel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());
    }

    private static void BuildCpuGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        int coreCount = cpu.PerCoreSamples != null && cpu.PerCoreSamples.Count > 0
            ? cpu.PerCoreSamples.Count
            : Environment.ProcessorCount;

        // Title
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine("[cyan1 bold]═══ CPU Visualization ═══[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Current Aggregate Usage Section
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey70 bold]Current Aggregate Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // LabelWidth 10 aligns all CPU bars (longest label: "System CPU")
        // User CPU Bar - smooth spectrum gradient
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuUserBar")
                .WithLabel("User CPU")
                .WithLabelWidth(10)
                .WithValue(cpu.User)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient("spectrum")  // Blue→Green→Yellow→Red
                .Build()
        );

        // System CPU Bar - smooth warm gradient
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuSystemBar")
                .WithLabel("System CPU")
                .WithLabelWidth(10)
                .WithValue(cpu.System)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient("warm")  // Yellow→Orange→Red
                .Build()
        );

        // IoWait CPU Bar - cool gradient (high IoWait is concerning)
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuIoWaitBar")
                .WithLabel("IoWait")
                .WithLabelWidth(10)
                .WithValue(cpu.IoWait)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(Color.Cyan1, Color.Yellow, Color.Red)  // Cyan→Yellow→Red
                .Build()
        );

        // Total CPU Bar - smooth gradient (green→yellow→red)
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuTotalBar")
                .WithLabel("Total CPU")
                .WithLabelWidth(10)
                .WithValue(totalCpu)
                .WithMaxValue(100)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 2)
                .WithSmoothGradient(Color.Green, Color.Yellow, Color.Orange1, Color.Red)
                .Build()
        );

        // Separator
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey23]────────────────────────────────────────[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Aggregate History Sparklines
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey70 bold]Aggregate Historical Trends[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        // User CPU sparkline - with warm gradient and inline baseline
        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuUserSparkline")
                .WithTitle("User CPU %")
                .WithTitleColor(Color.Red)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithGradient("warm")  // Yellow→Orange→Red gradient
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_cpuUserHistory)
                .Build()
        );

        // System CPU sparkline - with cool gradient
        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuSystemSparkline")
                .WithTitle("System CPU %")
                .WithTitleColor(Color.Yellow)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithGradient(Color.Yellow, Color.Orange1, Color.Red)  // Custom gradient
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_cpuSystemHistory)
                .Build()
        );

        // Total CPU sparkline - with spectrum gradient and inline baseline
        var totalSparkline = new SparklineBuilder()
            .WithName("cpuTotalSparkline")
            .WithTitle("Total CPU %")
            .WithTitleColor(Color.Cyan1)
            .WithTitlePosition(TitlePosition.Bottom)
            .WithHeight(6)
            .WithMaxValue(100)
            .WithGradient("spectrum")  // Blue→Green→Yellow→Red gradient
            .WithBackgroundColor(Color.Grey15)
            .WithBorder(BorderStyle.None)
            .WithMode(SparklineMode.Block) // 8 levels for better detail on CPU trends
            .WithBaseline(true, position: TitlePosition.Bottom)
            .WithInlineTitleBaseline(true)
            .WithMargin(2, 0, 1, 0)
            .WithData(_cpuTotalHistory)
            .Build();
        panel.AddControl(totalSparkline);

        // Per-Core History Section
        if (coreCount > 0)
        {
            // Separator
            panel.AddControl(
                Controls
                    .Markup()
                    .AddLine("[grey23]────────────────────────────────────────[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(2, 0, 2, 0)
                    .Build()
            );

            panel.AddControl(
                Controls
                    .Markup()
                    .AddLine("[grey70 bold]Per-Core History[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(2, 0, 2, 1)
                    .Build()
            );

            // Create sparklines for all cores (even if data not yet available)
            // Use braille mode for per-core graphs - smoother at smaller heights
            for (int coreIndex = 0; coreIndex < coreCount; coreIndex++)
            {
                // Calculate gradient color from Green to Red based on core index
                double ratio = coreCount > 1 ? (double)coreIndex / (coreCount - 1) : 0;
                int red = (int)(ratio * 255);
                int green = (int)((1 - ratio) * 255);
                var coreColor = new Color((byte)red, (byte)green, 0);

                var coreData = _cpuPerCoreHistory.ContainsKey(coreIndex)
                    ? _cpuPerCoreHistory[coreIndex]
                    : new List<double>();

                panel.AddControl(
                    new SparklineBuilder()
                        .WithName($"cpuCore{coreIndex}Sparkline")
                        .WithTitle($"Core {coreIndex}")
                        .WithTitleColor(coreColor)
                        .WithTitlePosition(TitlePosition.Bottom)
                        .WithHeight(4) // Smaller height for per-core
                        .WithMaxValue(100)
                        .WithGradient(Color.Blue, Color.Cyan1, Color.Yellow, Color.Red)  // Cool→Warm gradient
                        .WithBackgroundColor(Color.Grey15)
                        .WithBorder(BorderStyle.None)
                        .WithMode(SparklineMode.Braille) // Braille works better at small heights
                        .WithBaseline(true, position: TitlePosition.Bottom)
                        .WithInlineTitleBaseline(true)
                        .WithMargin(2, 0, 1, 0)
                        .WithData(coreData)
                        .Build()
                );
            }
        }
    }

    private static void HandleCpuPanelResize()
    {
        if (_mainWindow == null)
            return;

        var cpuPanel = _mainWindow.FindControl<HorizontalGridControl>("cpuPanel");
        if (cpuPanel == null || !cpuPanel.Visible)
            return; // Only handle resize if CPU panel is visible

        int windowWidth = _mainWindow.Width;
        var desiredLayout = windowWidth >= CPU_LAYOUT_THRESHOLD_WIDTH
            ? CpuLayoutMode.Wide
            : CpuLayoutMode.Narrow;

        // Only rebuild if layout mode changed
        if (desiredLayout != _currentCpuLayout)
        {
            _windowSystem?.LogService.LogDebug(
                $"HandleCpuPanelResize: Layout mode changed from {_currentCpuLayout} to {desiredLayout} (width={windowWidth})",
                "CPU");

            _currentCpuLayout = desiredLayout;
            RebuildCpuPanelColumns(cpuPanel);
        }
    }

    private static void RebuildCpuPanelColumns(HorizontalGridControl grid)
    {
        if (_lastSnapshot == null)
            return;

        _windowSystem?.LogService.LogDebug(
            $"RebuildCpuPanelColumns: Rebuilding in {_currentCpuLayout} mode",
            "CPU");

        // Clear existing columns in reverse order to avoid index issues
        for (int i = grid.Columns.Count - 1; i >= 0; i--)
        {
            grid.RemoveColumn(grid.Columns[i]);
        }

        if (_currentCpuLayout == CpuLayoutMode.Wide)
        {
            BuildWideCpuColumns(grid, _lastSnapshot);
        }
        else
        {
            BuildNarrowCpuColumns(grid, _lastSnapshot);
        }

        // Force complete DOM tree rebuild
        _mainWindow?.ForceRebuildLayout();
        _mainWindow?.Invalidate(true);
    }

    private static void BuildWideCpuColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildWideCpuColumns: Starting", "CPU");

        // Left column: Scrollable panel with controls
        var leftPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        leftPanel.BackgroundColor = Color.Grey11;
        leftPanel.ForegroundColor = Color.Grey93;
        BuildCpuLeftPanelContent(leftPanel, snapshot);

        var leftCol = new ColumnContainer(grid);
        leftCol.Width = 40;
        leftCol.AddContent(leftPanel);
        grid.AddColumn(leftCol);

        // Middle column: Separator
        var sepCol = new ColumnContainer(grid);
        sepCol.Width = 1;
        sepCol.AddContent(new SeparatorControl
        {
            ForegroundColor = Color.Grey23,
            VerticalAlignment = VerticalAlignment.Fill
        });
        grid.AddColumn(sepCol);

        // Right column: Scrollable graphs
        var rightPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        rightPanel.BackgroundColor = Color.Grey11;
        rightPanel.ForegroundColor = Color.Grey93;
        BuildCpuGraphsContent(rightPanel, snapshot);

        var rightCol = new ColumnContainer(grid);
        rightCol.AddContent(rightPanel);
        grid.AddColumn(rightCol);

        _windowSystem?.LogService.LogDebug($"BuildWideCpuColumns: Added 3 columns (40 | 1 | fill)", "CPU");
    }

    private static void BuildNarrowCpuColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildNarrowCpuColumns: Starting", "CPU");

        // Single column with all content
        var scrollPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        scrollPanel.BackgroundColor = Color.Grey11;
        scrollPanel.ForegroundColor = Color.Grey93;

        // Add left panel content with BarGraphControls
        BuildCpuLeftPanelContent(scrollPanel, snapshot);

        // Add separator
        scrollPanel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine("[grey23]────────────────────────────────────────[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 1, 2, 0)
                .Build()
        );

        // Add all graphs
        BuildCpuGraphsContent(scrollPanel, snapshot);

        var col = new ColumnContainer(grid);
        col.AddContent(scrollPanel);
        grid.AddColumn(col);

        _windowSystem?.LogService.LogDebug($"BuildNarrowCpuColumns: Added 1 column with full content", "CPU");
    }

    // ========================================================================
    // NETWORK PANEL METHODS
    // ========================================================================

    private static void UpdateNetworkPanel()
    {
        if (_mainWindow == null)
            return;

        // Find the network panel grid
        var networkPanel = _mainWindow.FindControl<HorizontalGridControl>("networkPanel");
        if (networkPanel == null)
        {
            _windowSystem?.LogService.LogWarning("UpdateNetworkPanel: Panel not found", "Network");
            return;
        }

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        UpdateNetworkHistory(snapshot.Network);

        // Update the graph controls with new data
        UpdateNetworkGraphControls(networkPanel, snapshot);
    }

    private static void UpdateNetworkHistory(NetworkSample network)
    {
        // Aggregate history
        _networkUpHistory.Add(network.UpMbps);
        _networkDownHistory.Add(network.DownMbps);

        // Peak tracking
        _peakUpMbps = Math.Max(_peakUpMbps, network.UpMbps);
        _peakDownMbps = Math.Max(_peakDownMbps, network.DownMbps);

        // Per-interface history
        if (network.PerInterfaceSamples != null)
        {
            foreach (var iface in network.PerInterfaceSamples)
            {
                if (!_networkPerInterfaceUpHistory.ContainsKey(iface.InterfaceName))
                {
                    _networkPerInterfaceUpHistory[iface.InterfaceName] = new List<double>();
                    _networkPerInterfaceDownHistory[iface.InterfaceName] = new List<double>();
                }
                _networkPerInterfaceUpHistory[iface.InterfaceName].Add(iface.UpMbps);
                _networkPerInterfaceDownHistory[iface.InterfaceName].Add(iface.DownMbps);

                // Trim per-interface history
                while (_networkPerInterfaceUpHistory[iface.InterfaceName].Count > MAX_HISTORY_POINTS)
                    _networkPerInterfaceUpHistory[iface.InterfaceName].RemoveAt(0);
                while (_networkPerInterfaceDownHistory[iface.InterfaceName].Count > MAX_HISTORY_POINTS)
                    _networkPerInterfaceDownHistory[iface.InterfaceName].RemoveAt(0);
            }
        }

        // Trim aggregate history
        while (_networkUpHistory.Count > MAX_HISTORY_POINTS)
            _networkUpHistory.RemoveAt(0);
        while (_networkDownHistory.Count > MAX_HISTORY_POINTS)
            _networkDownHistory.RemoveAt(0);
    }

    private static void UpdateNetworkGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        // Find the right column (column 2) which has the graphs
        if (grid.Columns.Count < 3)
        {
            _windowSystem?.LogService.LogDebug($"UpdateNetworkGraphControls: Grid has {grid.Columns.Count} columns, expected 3", "Network");
            return;
        }

        var rightCol = grid.Columns[2];
        var rightPanel = rightCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        if (rightPanel == null)
        {
            _windowSystem?.LogService.LogDebug("UpdateNetworkGraphControls: Right panel not found", "Network");
            return;
        }

        var net = snapshot.Network;

        // Update bar graphs
        var uploadBar = rightPanel.Children.FirstOrDefault(c => c.Name == "netUploadBar") as BarGraphControl;
        if (uploadBar != null)
            uploadBar.Value = net.UpMbps;

        var downloadBar = rightPanel.Children.FirstOrDefault(c => c.Name == "netDownloadBar") as BarGraphControl;
        if (downloadBar != null)
            downloadBar.Value = net.DownMbps;

        // Update combined bidirectional sparkline
        var combinedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "netCombinedSparkline") as SparklineControl;
        if (combinedSparkline != null)
        {
            combinedSparkline.SetBidirectionalData(_networkUpHistory, _networkDownHistory);
            // Update max values dynamically based on peaks
            combinedSparkline.MaxValue = Math.Max(_peakUpMbps, 1.0);
            combinedSparkline.SecondaryMaxValue = Math.Max(_peakDownMbps, 1.0);
        }

        // Update per-interface combined sparklines
        if (net.PerInterfaceSamples != null)
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                var ifaceSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"net{iface.InterfaceName}Sparkline") as SparklineControl;
                if (ifaceSparkline != null)
                {
                    var upData = _networkPerInterfaceUpHistory.ContainsKey(iface.InterfaceName)
                        ? _networkPerInterfaceUpHistory[iface.InterfaceName]
                        : new List<double>();
                    var downData = _networkPerInterfaceDownHistory.ContainsKey(iface.InterfaceName)
                        ? _networkPerInterfaceDownHistory[iface.InterfaceName]
                        : new List<double>();

                    ifaceSparkline.SetBidirectionalData(upData, downData);
                    ifaceSparkline.MaxValue = Math.Max(_peakUpMbps, 0.1);
                    ifaceSparkline.SecondaryMaxValue = Math.Max(_peakDownMbps, 0.1);
                }
            }
        }

        // Update left column text stats
        if (grid.Columns.Count > 0)
        {
            var leftCol = grid.Columns[0];
            var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
            if (leftPanel != null && leftPanel.Children.Count > 0)
            {
                var markup = leftPanel.Children[0] as MarkupControl;
                if (markup != null)
                {
                    var lines = BuildNetworkTextContent(snapshot);
                    markup.SetContent(lines);
                }
            }
        }
    }

    private static HorizontalGridControl BuildResponsiveNetworkGrid(int windowWidth, SystemSnapshot snapshot)
    {
        var desiredLayout = windowWidth >= NETWORK_LAYOUT_THRESHOLD_WIDTH
            ? NetworkLayoutMode.Wide
            : NetworkLayoutMode.Narrow;

        _currentNetworkLayout = desiredLayout;

        _windowSystem?.LogService.LogDebug(
            $"BuildResponsiveNetworkGrid: Building initial layout in {desiredLayout} mode (width={windowWidth})",
            "Network");

        if (desiredLayout == NetworkLayoutMode.Wide)
        {
            return BuildWideNetworkGridInitial(snapshot);
        }
        else
        {
            return BuildNarrowNetworkGridInitial(snapshot);
        }
    }

    private static HorizontalGridControl BuildWideNetworkGridInitial(SystemSnapshot snapshot)
    {
        var lines = BuildNetworkTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("networkPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Left column: Scrollable text info (fixed width)
            .Column(col =>
            {
                col.Width(40); // Fixed width for text stats
                var leftPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                leftPanel.BackgroundColor = Color.Grey11;
                leftPanel.ForegroundColor = Color.Grey93;

                var markup = Controls.Markup();
                foreach (var line in lines)
                {
                    markup = markup.AddLine(line);
                }
                leftPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());
                col.Add(leftPanel);
            })
            // Middle column: Separator (1 char wide)
            .Column(col =>
            {
                col.Width(1);
                col.Add(new SeparatorControl
                {
                    ForegroundColor = Color.Grey23,
                    VerticalAlignment = VerticalAlignment.Fill
                });
            })
            // Right column: Scrollable graphs (fills remaining space)
            .Column(col =>
            {
                // No width set - fills remaining space responsively
                var rightPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                rightPanel.BackgroundColor = Color.Grey11;
                rightPanel.ForegroundColor = Color.Grey93;
                BuildNetworkGraphsContent(rightPanel, snapshot);
                col.Add(rightPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static HorizontalGridControl BuildNarrowNetworkGridInitial(SystemSnapshot snapshot)
    {
        var lines = BuildNetworkTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("networkPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Single column: Scrollable panel with ALL content (text + graphs)
            .Column(col =>
            {
                var scrollPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                scrollPanel.BackgroundColor = Color.Grey11;
                scrollPanel.ForegroundColor = Color.Grey93;

                // Add text content
                var markup = Controls.Markup();
                foreach (var line in lines)
                {
                    markup = markup.AddLine(line);
                }
                scrollPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

                // Add separator
                scrollPanel.AddControl(
                    Controls
                        .Markup()
                        .AddLine("")
                        .AddLine("[grey23]────────────────────────────────────────[/]")
                        .AddLine("")
                        .WithAlignment(HorizontalAlignment.Left)
                        .WithMargin(2, 1, 2, 0)
                        .Build()
                );

                // Add all graphs (same as wide mode right panel)
                BuildNetworkGraphsContent(scrollPanel, snapshot);

                col.Add(scrollPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static List<string> BuildNetworkTextContent(SystemSnapshot snapshot)
    {
        var net = snapshot.Network;
        int interfaceCount = net.PerInterfaceSamples?.Count ?? 0;

        var lines = new List<string>
        {
            "",
            $"[cyan1 bold]Network ({interfaceCount} interface{(interfaceCount != 1 ? "s" : "")})[/]",
            "",
            "[grey70 bold]Current Rates[/]",
            $"  [grey70]Upload:[/]   [cyan1]{net.UpMbps:F2} MB/s[/]",
            $"  [grey70]Download:[/] [green]{net.DownMbps:F2} MB/s[/]",
            "",
            "[grey70 bold]Peak Rates (session)[/]",
            $"  [grey70]Upload:[/]   [cyan1]{_peakUpMbps:F2} MB/s[/]",
            $"  [grey70]Download:[/] [green]{_peakDownMbps:F2} MB/s[/]",
            "",
            "[grey70 bold]Active Interfaces[/]",
        };

        if (net.PerInterfaceSamples != null && net.PerInterfaceSamples.Count > 0)
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                // Truncate long interface names for display
                string ifaceName = iface.InterfaceName.Length > 15
                    ? iface.InterfaceName.Substring(0, 12) + "..."
                    : iface.InterfaceName;

                lines.Add($"  [cyan1]{ifaceName,-15}[/] ↑[grey70]{iface.UpMbps:F2}[/] ↓[grey70]{iface.DownMbps:F2}[/]");
            }
        }
        else
        {
            lines.Add("  [grey50]No active interfaces[/]");
        }

        return lines;
    }

    private static void BuildNetworkGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var net = snapshot.Network;

        // Title
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine("[cyan1 bold]═══ Network Visualization ═══[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Current Usage Section
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey70 bold]Current Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Calculate max value for bar graphs - use peak or at least 1 MB/s for scale
        double maxRate = Math.Max(Math.Max(_peakUpMbps, _peakDownMbps), 1.0);
        double barMax = Math.Ceiling(maxRate / 10) * 10; // Round up to nearest 10

        // Upload Bar - with cool gradient (blue→cyan)
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("netUploadBar")
                .WithLabel("Upload")
                .WithLabelWidth(10)
                .WithValue(net.UpMbps)
                .WithMaxValue(barMax)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F2")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient("cool")  // Blue→Cyan gradient
                .Build()
        );

        // Download Bar - with spectrum gradient
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("netDownloadBar")
                .WithLabel("Download")
                .WithLabelWidth(10)
                .WithValue(net.DownMbps)
                .WithMaxValue(barMax)
                .WithBarWidth(35)
                .WithUnfilledColor(Color.Grey35)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F2")
                .WithMargin(2, 0, 2, 2)
                .WithSmoothGradient(Color.Blue, Color.Green, Color.Yellow)  // Blue→Green→Yellow
                .Build()
        );

        // Separator
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey23]────────────────────────────────────────[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        // Aggregate History Sparkline (bidirectional: upload up, download down)
        panel.AddControl(
            Controls
                .Markup()
                .AddLine("[grey70 bold]Network History[/] [grey50](↑ Upload  ↓ Download)[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        // Combined download/upload sparkline using bidirectional mode with separate gradients
        panel.AddControl(
            new SparklineBuilder()
                .WithName("netCombinedSparkline")
                .WithTitle("↓ Download  ↑ Upload")
                .WithTitleColor(Color.Grey70)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(10) // Taller to accommodate both directions
                .WithMaxValue(Math.Max(_peakDownMbps, 1.0))
                .WithSecondaryMaxValue(Math.Max(_peakUpMbps, 1.0))
                .WithGradient("warm")                      // Download: yellow→orange→red (warm colors) on top
                .WithSecondaryGradient("cool")             // Upload: blue→cyan (cool colors) on bottom
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.BidirectionalBraille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)  // Compact layout: "↓ Download  ↑ Upload ┈┈┈"
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(2, 0, 1, 0)
                .WithBidirectionalData(_networkDownHistory, _networkUpHistory)
                .Build()
        );

        // Per-Interface History Section
        if (net.PerInterfaceSamples != null && net.PerInterfaceSamples.Count > 0)
        {
            // Separator
            panel.AddControl(
                Controls
                    .Markup()
                    .AddLine("[grey23]────────────────────────────────────────[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(2, 0, 2, 0)
                    .Build()
            );

            panel.AddControl(
                Controls
                    .Markup()
                    .AddLine("[grey70 bold]Per-Interface History[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(2, 0, 2, 1)
                    .Build()
            );

            // Create combined bidirectional sparklines for each interface
            int ifaceIndex = 0;
            foreach (var iface in net.PerInterfaceSamples)
            {
                // Calculate gradient colors based on interface index
                int ifaceCount = net.PerInterfaceSamples.Count;
                double ratio = ifaceCount > 1 ? (double)ifaceIndex / (ifaceCount - 1) : 0;

                // Cyan (0, 255, 255) to Magenta (255, 0, 255) gradient for uploads
                int upRed = (int)(ratio * 255);
                int upGreen = (int)((1 - ratio) * 255);
                var upColor = new Color((byte)upRed, (byte)upGreen, (byte)255);

                // Green (0, 255, 0) to Yellow (255, 255, 0) gradient for downloads
                int downRed = (int)(ratio * 255);
                var downColor = new Color((byte)downRed, (byte)255, (byte)0);

                // Truncate long interface names
                string ifaceNameDisplay = iface.InterfaceName.Length > 15
                    ? iface.InterfaceName.Substring(0, 12) + "..."
                    : iface.InterfaceName;

                // Get upload and download data for this interface
                var upData = _networkPerInterfaceUpHistory.ContainsKey(iface.InterfaceName)
                    ? _networkPerInterfaceUpHistory[iface.InterfaceName]
                    : new List<double>();
                var downData = _networkPerInterfaceDownHistory.ContainsKey(iface.InterfaceName)
                    ? _networkPerInterfaceDownHistory[iface.InterfaceName]
                    : new List<double>();

                // Combined bidirectional sparkline for this interface with gradients
                panel.AddControl(
                    new SparklineBuilder()
                        .WithName($"net{iface.InterfaceName}Sparkline")
                        .WithTitle(ifaceNameDisplay)
                        .WithTitleColor(Color.Grey70)
                        .WithTitlePosition(TitlePosition.Bottom)
                        .WithHeight(6) // Height for bidirectional display
                        .WithMaxValue(Math.Max(_peakDownMbps, 0.1))
                        .WithSecondaryMaxValue(Math.Max(_peakUpMbps, 0.1))
                        .WithGradient(Color.Green, Color.Yellow)      // Download: green→yellow on top
                        .WithSecondaryGradient(Color.Blue, Color.Cyan1)  // Upload: blue→cyan on bottom
                        .WithBackgroundColor(Color.Grey15)
                        .WithBorder(BorderStyle.None)
                        .WithMode(SparklineMode.BidirectionalBraille)
                        .WithBaseline(true, position: TitlePosition.Bottom)
                        .WithInlineTitleBaseline(true)
                        .WithAlignment(HorizontalAlignment.Stretch)
                        .WithMargin(2, 0, 1, 0)
                        .WithBidirectionalData(downData, upData)
                        .Build()
                );

                ifaceIndex++;
            }
        }
    }

    private static void HandleNetworkPanelResize()
    {
        if (_mainWindow == null)
            return;

        var networkPanel = _mainWindow.FindControl<HorizontalGridControl>("networkPanel");
        if (networkPanel == null || !networkPanel.Visible)
            return; // Only handle resize if network panel is visible

        int windowWidth = _mainWindow.Width;
        var desiredLayout = windowWidth >= NETWORK_LAYOUT_THRESHOLD_WIDTH
            ? NetworkLayoutMode.Wide
            : NetworkLayoutMode.Narrow;

        // Only rebuild if layout mode changed
        if (desiredLayout != _currentNetworkLayout)
        {
            _windowSystem?.LogService.LogDebug(
                $"HandleNetworkPanelResize: Layout mode changed from {_currentNetworkLayout} to {desiredLayout} (width={windowWidth})",
                "Network");

            _currentNetworkLayout = desiredLayout;
            RebuildNetworkPanelColumns(networkPanel);
        }
    }

    private static void RebuildNetworkPanelColumns(HorizontalGridControl grid)
    {
        if (_lastSnapshot == null)
            return;

        _windowSystem?.LogService.LogDebug(
            $"RebuildNetworkPanelColumns: Rebuilding in {_currentNetworkLayout} mode",
            "Network");

        // Clear existing columns in reverse order to avoid index issues
        for (int i = grid.Columns.Count - 1; i >= 0; i--)
        {
            grid.RemoveColumn(grid.Columns[i]);
        }

        if (_currentNetworkLayout == NetworkLayoutMode.Wide)
        {
            BuildWideNetworkColumns(grid, _lastSnapshot);
        }
        else
        {
            BuildNarrowNetworkColumns(grid, _lastSnapshot);
        }

        // Force complete DOM tree rebuild
        _mainWindow?.ForceRebuildLayout();
        _mainWindow?.Invalidate(true);
    }

    private static void BuildWideNetworkColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildWideNetworkColumns: Starting", "Network");

        var lines = BuildNetworkTextContent(snapshot);

        // Left column: Scrollable text info
        var leftPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        leftPanel.BackgroundColor = Color.Grey11;
        leftPanel.ForegroundColor = Color.Grey93;

        var markup = Controls.Markup();
        foreach (var line in lines)
        {
            markup = markup.AddLine(line);
        }
        leftPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

        var leftCol = new ColumnContainer(grid);
        leftCol.Width = 40;
        leftCol.AddContent(leftPanel);
        grid.AddColumn(leftCol);

        // Middle column: Separator
        var sepCol = new ColumnContainer(grid);
        sepCol.Width = 1;
        sepCol.AddContent(new SeparatorControl
        {
            ForegroundColor = Color.Grey23,
            VerticalAlignment = VerticalAlignment.Fill
        });
        grid.AddColumn(sepCol);

        // Right column: Scrollable graphs
        var rightPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        rightPanel.BackgroundColor = Color.Grey11;
        rightPanel.ForegroundColor = Color.Grey93;
        BuildNetworkGraphsContent(rightPanel, snapshot);

        var rightCol = new ColumnContainer(grid);
        rightCol.AddContent(rightPanel);
        grid.AddColumn(rightCol);

        _windowSystem?.LogService.LogDebug($"BuildWideNetworkColumns: Added 3 columns (40 | 1 | fill)", "Network");
    }

    private static void BuildNarrowNetworkColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildNarrowNetworkColumns: Starting", "Network");

        var lines = BuildNetworkTextContent(snapshot);

        // Single column with all content
        var scrollPanel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        scrollPanel.BackgroundColor = Color.Grey11;
        scrollPanel.ForegroundColor = Color.Grey93;

        // Add text content
        var markup = Controls.Markup();
        foreach (var line in lines)
        {
            markup = markup.AddLine(line);
        }
        scrollPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

        // Add separator
        scrollPanel.AddControl(
            Controls
                .Markup()
                .AddLine("")
                .AddLine("[grey23]────────────────────────────────────────[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 1, 2, 0)
                .Build()
        );

        // Add all graphs
        BuildNetworkGraphsContent(scrollPanel, snapshot);

        var col = new ColumnContainer(grid);
        col.AddContent(scrollPanel);
        grid.AddColumn(col);

        _windowSystem?.LogService.LogDebug($"BuildNarrowNetworkColumns: Added 1 column with full content", "Network");
    }

    // ========================================================================
    // STORAGE PANEL METHODS
    // ========================================================================

    private static HorizontalGridControl BuildResponsiveStorageGrid(int windowWidth, SystemSnapshot snapshot)
    {
        var desiredLayout = windowWidth >= STORAGE_LAYOUT_THRESHOLD_WIDTH
            ? StorageLayoutMode.Wide
            : StorageLayoutMode.Narrow;

        _currentStorageLayout = desiredLayout;

        _windowSystem?.LogService.LogDebug(
            $"BuildResponsiveStorageGrid: Building initial layout in {desiredLayout} mode (width={windowWidth})",
            "Storage");

        if (desiredLayout == StorageLayoutMode.Wide)
        {
            return BuildWideStorageGridInitial(snapshot);
        }
        else
        {
            return BuildNarrowStorageGridInitial(snapshot);
        }
    }

    private static HorizontalGridControl BuildWideStorageGridInitial(SystemSnapshot snapshot)
    {
        var lines = BuildStorageTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("storagePanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Left column: Scrollable text info (fixed width)
            .Column(col =>
            {
                col.Width(40); // Fixed width for text stats
                var leftPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                leftPanel.BackgroundColor = Color.Grey11;
                leftPanel.ForegroundColor = Color.Grey93;

                var markup = Controls.Markup();
                foreach (var line in lines)
                {
                    markup = markup.AddLine(line);
                }
                leftPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());
                col.Add(leftPanel);
            })
            // Middle column: Separator (1 char wide)
            .Column(col =>
            {
                col.Width(1);
                col.Add(new SeparatorControl
                {
                    ForegroundColor = Color.Grey23,
                    VerticalAlignment = VerticalAlignment.Fill
                });
            })
            // Right column: Scrollable graphs (fills remaining space)
            .Column(col =>
            {
                // No width set - fills remaining space responsively
                var rightPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                rightPanel.BackgroundColor = Color.Grey11;
                rightPanel.ForegroundColor = Color.Grey93;
                BuildStorageGraphsContent(rightPanel, snapshot);
                col.Add(rightPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static HorizontalGridControl BuildNarrowStorageGridInitial(SystemSnapshot snapshot)
    {
        var lines = BuildStorageTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("storagePanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false) // Hidden by default
            // Single column: Scrollable panel with ALL content (text + graphs)
            .Column(col =>
            {
                var scrollPanel = Controls
                    .ScrollablePanel()
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .Build();
                scrollPanel.BackgroundColor = Color.Grey11;
                scrollPanel.ForegroundColor = Color.Grey93;

                // Add text content
                var markup = Controls.Markup();
                foreach (var line in lines)
                {
                    markup = markup.AddLine(line);
                }
                scrollPanel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());

                // Add separator
                scrollPanel.AddControl(
                    Controls
                        .Markup()
                        .AddLine("")
                        .AddLine("[grey23]────────────────────────────────────────[/]")
                        .AddLine("")
                        .WithAlignment(HorizontalAlignment.Left)
                        .WithMargin(2, 1, 2, 0)
                        .Build()
                );

                // Add all graphs (same as wide mode right panel)
                BuildStorageGraphsContent(scrollPanel, snapshot);

                col.Add(scrollPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static List<string> BuildStorageTextContent(SystemSnapshot snapshot)
    {
        var lines = new List<string>();
        var storage = snapshot.Storage;

        // Aggregate section
        lines.Add("[bold cyan1]Total Storage[/]");
        lines.Add($"  Capacity:  {storage.TotalCapacityGb,6:F1} GB");
        lines.Add($"  Used:      {storage.TotalUsedGb,6:F1} GB ([cyan1]{storage.TotalUsedPercent:F1}%[/])");
        lines.Add($"  Free:      {storage.TotalFreeGb,6:F1} GB");
        lines.Add("");

        lines.Add("[bold grey70]Mounted Filesystems[/]");
        lines.Add("");

        // Per-disk details
        foreach (var disk in storage.Disks)
        {
            var mountIcon = disk.IsRemovable ? "📀" : "💾";
            lines.Add($"[cyan1]{mountIcon} {disk.MountPoint}[/] [grey50]({Path.GetFileName(disk.DeviceName)})[/]");
            lines.Add($"  Type:    [grey70]{disk.FileSystemType}[/]");

            if (!string.IsNullOrEmpty(disk.Label))
                lines.Add($"  Label:   [yellow]{disk.Label}[/]");

            lines.Add($"  Size:    {disk.TotalGb,6:F1} GB");
            lines.Add($"  Used:    {disk.UsedGb,6:F1} GB ([cyan1]{disk.UsedPercent:F1}%[/])");
            lines.Add($"  Free:    {disk.FreeGb,6:F1} GB");

            if (!string.IsNullOrEmpty(disk.MountOptions))
                lines.Add($"  Options: [grey50]{disk.MountOptions}[/]");

            lines.Add("");
        }

        if (storage.Disks.Count == 0)
        {
            lines.Add("[grey50]No storage devices found[/]");
        }

        return lines;
    }

    private static void BuildStorageGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var storage = snapshot.Storage;

        if (storage.Disks.Count == 0)
        {
            panel.AddControl(
                Controls.Markup()
                    .AddLine("[grey50]No storage devices to display[/]")
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );
            return;
        }

        foreach (var disk in storage.Disks)
        {
            // Use device name as key for history tracking
            var deviceKey = disk.DeviceName;

            // Disk header
            var headerText = !string.IsNullOrEmpty(disk.Label)
                ? $"[bold cyan1]{disk.MountPoint}[/] [grey50]({Path.GetFileName(disk.DeviceName)} - {disk.FileSystemType} - \"{disk.Label}\")[/]"
                : $"[bold cyan1]{disk.MountPoint}[/] [grey50]({Path.GetFileName(disk.DeviceName)} - {disk.FileSystemType})[/]";

            panel.AddControl(
                Controls.Markup()
                    .AddLine(headerText)
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );

            // Usage bar with gradient (disk space)
            panel.AddControl(
                new BarGraphBuilder()
                    .WithName($"disk_{deviceKey}_usage")
                    .WithLabel("Used %")
                    .WithLabelWidth(10)
                    .WithValue(disk.UsedPercent)
                    .WithMaxValue(100)
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithSmoothGradient(Color.Green, Color.Yellow, Color.Orange1, Color.Red)
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );

            // Current Read bar
            panel.AddControl(
                new BarGraphBuilder()
                    .WithName($"disk_{deviceKey}_read_current")
                    .WithLabel("Read MB/s")
                    .WithLabelWidth(10)
                    .WithValue(disk.ReadMbps)
                    .WithMaxValue(100) // Will auto-scale based on actual values
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithSmoothGradient(Color.Blue, Color.Cyan1)
                    .WithMargin(2, 0, 1, 0)
                    .Build()
            );

            // Current Write bar
            panel.AddControl(
                new BarGraphBuilder()
                    .WithName($"disk_{deviceKey}_write_current")
                    .WithLabel("Write MB/s")
                    .WithLabelWidth(10)
                    .WithValue(disk.WriteMbps)
                    .WithMaxValue(100) // Will auto-scale based on actual values
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithSmoothGradient(Color.Yellow, Color.Orange1, Color.Red)
                    .WithMargin(2, 0, 1, 0)
                    .Build()
            );

            // Initialize history if needed
            if (!_diskReadHistory.ContainsKey(deviceKey))
            {
                _diskReadHistory[deviceKey] = new List<double>();
                _diskWriteHistory[deviceKey] = new List<double>();
            }

            // Calculate dynamic max values for bidirectional sparkline
            double maxRead = Math.Max(10, _diskReadHistory[deviceKey].DefaultIfEmpty(0).Max());
            double maxWrite = Math.Max(10, _diskWriteHistory[deviceKey].DefaultIfEmpty(0).Max());

            // Bidirectional sparkline: Read (up) / Write (down)
            panel.AddControl(
                new SparklineBuilder()
                    .WithName($"disk_{deviceKey}_io")
                    .WithTitle("↑ Read  ↓ Write")
                    .WithTitleColor(Color.Grey70)
                    .WithTitlePosition(TitlePosition.Bottom)
                    .WithHeight(8) // Taller for bidirectional
                    .WithMaxValue(maxRead)
                    .WithSecondaryMaxValue(maxWrite)
                    .WithGradient("cool")  // Read: blue→cyan
                    .WithSecondaryGradient("warm")  // Write: yellow→orange→red
                    .WithBackgroundColor(Color.Grey15)
                    .WithBorder(BorderStyle.None)
                    .WithMode(SparklineMode.BidirectionalBraille)
                    .WithBaseline(true, position: TitlePosition.Bottom)
                    .WithInlineTitleBaseline(true)
                    .WithMargin(2, 1, 1, 0)
                    .WithBidirectionalData(_diskReadHistory[deviceKey], _diskWriteHistory[deviceKey])
                    .Build()
            );

            // Separator between disks
            panel.AddControl(
                Controls.Markup()
                    .AddLine("[grey23]────────────────────────────────[/]")
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );
        }
    }

    private static void UpdateStoragePanel()
    {
        if (_mainWindow == null)
            return;

        // Find the storage panel grid
        var storagePanel = _mainWindow.FindControl<HorizontalGridControl>("storagePanel");
        if (storagePanel == null)
        {
            _windowSystem?.LogService.LogWarning("UpdateStoragePanel: Panel not found", "Storage");
            return;
        }

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        UpdateStorageHistory(snapshot.Storage);

        // Update the graph controls with new data
        UpdateStorageGraphControls(storagePanel, snapshot);

        // Update left column text stats
        if (storagePanel.Columns.Count > 0)
        {
            var leftCol = storagePanel.Columns[0];
            var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
            if (leftPanel != null && leftPanel.Children.Count > 0)
            {
                var markup = leftPanel.Children[0] as MarkupControl;
                if (markup != null)
                {
                    var lines = BuildStorageTextContent(snapshot);
                    markup.SetContent(lines);
                }
            }
        }
    }

    private static void UpdateStorageHistory(StorageSample storage)
    {
        foreach (var disk in storage.Disks)
        {
            var key = disk.DeviceName;

            if (!_diskReadHistory.ContainsKey(key))
            {
                _diskReadHistory[key] = new List<double>();
                _diskWriteHistory[key] = new List<double>();
            }

            _diskReadHistory[key].Add(disk.ReadMbps);
            _diskWriteHistory[key].Add(disk.WriteMbps);

            while (_diskReadHistory[key].Count > MAX_HISTORY_POINTS)
                _diskReadHistory[key].RemoveAt(0);
            while (_diskWriteHistory[key].Count > MAX_HISTORY_POINTS)
                _diskWriteHistory[key].RemoveAt(0);
        }
    }

    private static void UpdateStorageGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var storage = snapshot.Storage;

        // Find the right panel (column 2 in wide mode, column 0 in narrow mode)
        ScrollablePanelControl? rightPanel = null;

        if (_currentStorageLayout == StorageLayoutMode.Wide && grid.Columns.Count >= 3)
        {
            var rightCol = grid.Columns[2];
            rightPanel = rightCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        }
        else if (_currentStorageLayout == StorageLayoutMode.Narrow && grid.Columns.Count >= 1)
        {
            var singleCol = grid.Columns[0];
            rightPanel = singleCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        }

        if (rightPanel == null)
        {
            _windowSystem?.LogService.LogDebug("UpdateStorageGraphControls: Right panel not found", "Storage");
            return;
        }

        _windowSystem?.LogService.LogDebug($"UpdateStorageGraphControls: Right panel has {rightPanel.Children.Count} children", "Storage");

        // Update per-disk graphs
        foreach (var disk in storage.Disks)
        {
            var deviceKey = disk.DeviceName;

            // Update usage bar (disk space)
            var usageBar = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_usage") as BarGraphControl;
            if (usageBar != null)
            {
                _windowSystem?.LogService.LogDebug($"UpdateStorageGraphControls: Updating {deviceKey} usage to {disk.UsedPercent:F1}%", "Storage");
                usageBar.Value = disk.UsedPercent;
            }
            else
            {
                _windowSystem?.LogService.LogDebug($"UpdateStorageGraphControls: Usage bar for {deviceKey} not found", "Storage");
            }

            // Update read bar (current rate)
            var readBar = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_read_current") as BarGraphControl;
            if (readBar != null)
            {
                readBar.Value = disk.ReadMbps;
            }

            // Update write bar (current rate)
            var writeBar = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_write_current") as BarGraphControl;
            if (writeBar != null)
            {
                writeBar.Value = disk.WriteMbps;
            }

            // Update bidirectional sparkline
            var ioSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_io") as SparklineControl;
            if (ioSparkline != null && _diskReadHistory.ContainsKey(deviceKey) && _diskWriteHistory.ContainsKey(deviceKey))
            {
                ioSparkline.SetBidirectionalData(_diskReadHistory[deviceKey], _diskWriteHistory[deviceKey]);
            }
        }
    }
}
