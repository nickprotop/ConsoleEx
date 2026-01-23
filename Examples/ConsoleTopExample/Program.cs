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

    static async Task<int> Main(string[] args)
    {
        try
        {
            _windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
            {
                TopStatus = $"ConsoleTop - System Monitor ({SystemStatsFactory.GetPlatformName()})",
                ShowTaskBar = false,
            };

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
                        SpectreRenderableControl
                            .Create()
                            .WithRenderable(BuildCpuChart(0, 0, 0))
                            .WithName("cpuChart")
                            .WithMargin(1, 1, 0, 1)
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
                        SpectreRenderableControl
                            .Create()
                            .WithRenderable(BuildMemoryChart(0, 0, 0))
                            .WithName("memChart")
                            .WithMargin(1, 1, 1, 1)
                            .Build()
                    )
            )
            .Column(col => col.Width(1)) // Spacing between boxes
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]Network[/]").WithMargin(1, 1, 1, 0).Build())
                    .Add(
                        SpectreRenderableControl
                            .Create()
                            .WithRenderable(BuildNetworkChart(0, 0))
                            .WithName("netChart")
                            .WithMargin(1, 1, 1, 1)
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
                            .WithMargin(1, 0, 1, 0)
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

        // Hook up responsive layout handlers for memory and CPU panels
        mainWindow.OnResize += (sender, e) =>
        {
            HandleMemoryPanelResize();
            HandleCpuPanelResize();
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

        // Switch visibility based on active tab
        switch (_activeTab)
        {
            case TabMode.Processes:
                // Show process panel, hide others
                if (processPanel != null)
                    processPanel.Visible = true;
                if (memoryPanel != null)
                    memoryPanel.Visible = false;
                if (cpuPanel != null)
                    cpuPanel.Visible = false;

                // Update process detail content
                UpdateHighlightedProcess();
                break;

            case TabMode.Memory:
                // Hide process panel, show memory panel, hide CPU panel
                if (processPanel != null)
                    processPanel.Visible = false;
                if (memoryPanel != null)
                    memoryPanel.Visible = true;
                if (cpuPanel != null)
                    cpuPanel.Visible = false;

                // Update memory panel content
                UpdateMemoryPanel();
                break;

            case TabMode.Cpu:
                // Hide process and memory panels, show CPU panel
                if (processPanel != null)
                    processPanel.Visible = false;
                if (memoryPanel != null)
                    memoryPanel.Visible = false;
                if (cpuPanel != null)
                    cpuPanel.Visible = true;

                // Update CPU panel content
                UpdateCpuPanel();
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

                // Update CPU chart
                var cpuChart = window.FindControl<SpectreRenderableControl>("cpuChart");
                cpuChart?.SetRenderable(
                    BuildCpuChart(snapshot.Cpu.User, snapshot.Cpu.System, snapshot.Cpu.IoWait)
                );

                // Update memory chart
                var ioScaled = Math.Min(
                    100,
                    Math.Max(snapshot.Network.UpMbps, snapshot.Network.DownMbps)
                );
                var memChart = window.FindControl<SpectreRenderableControl>("memChart");
                memChart?.SetRenderable(
                    BuildMemoryChart(
                        snapshot.Memory.UsedPercent,
                        snapshot.Memory.CachedPercent,
                        ioScaled
                    )
                );

                // Update network chart
                var netChart = window.FindControl<SpectreRenderableControl>("netChart");
                netChart?.SetRenderable(
                    BuildNetworkChart(snapshot.Network.UpMbps, snapshot.Network.DownMbps)
                );

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

    private static BarChart BuildCpuChart(double user, double sys, double io)
    {
        return new BarChart()
            .Label("[bold]CPU load[/]")
            .LeftAlignLabel()
            .WithMaxValue(100)
            .AddItem("User", user, Color.Cyan1)
            .AddItem("System", sys, Color.Grey50)
            .AddItem("IOwait", io, Color.Grey70);
    }

    private static BarChart BuildMemoryChart(double used, double cached, double ioScaled)
    {
        var chart = new BarChart()
            .Label("[bold]Memory / IO[/]")
            .LeftAlignLabel()
            .WithMaxValue(100)
            .AddItem("Used %", used, Color.Cyan1)
            .AddItem("Cached %", cached, Color.Grey50);

        var ioPercent = Math.Min(100, ioScaled);
        chart.AddItem("Disk/IO est %", Math.Round(ioPercent, 1), Color.Grey70);
        return chart;
    }

    private static BarChart BuildNetworkChart(double up, double down)
    {
        // Dynamic scale: find max and round up to nearest 10 MB/s
        var maxMbps = Math.Max(Math.Max(up, down), 1); // Min 1 MB/s for scale
        var scale = Math.Ceiling(maxMbps / 10) * 10;

        return new BarChart()
            .Label("[bold]Network[/]")
            .LeftAlignLabel()
            .WithMaxValue(scale)
            .AddItem("Upload", Math.Round(up, 1), Color.Cyan1)
            .AddItem("Download", Math.Round(down, 1), Color.Grey50);
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
            var line =
                $"  {p.Pid, 5}  [grey70]{p.CpuPercent, 4:F1}%[/]  [grey70]{p.MemPercent, 4:F1}%[/]  [cyan1]{p.Command}[/]";
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

        // RAM Usage Bar - with gradient (green at low usage, yellow at medium, red at high)
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
                .WithStandardGradient()
                .Build()
        );

        // RAM Free Bar - inverse gradient (more free = greener)
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
                .WithGradient(
                    new ColorThreshold(0, Color.Red),
                    new ColorThreshold(20, Color.Yellow),
                    new ColorThreshold(50, Color.Green)
                )
                .Build()
        );

        // Swap Usage Bar - with gradient (any swap usage is potentially concerning)
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
                .WithGradient(
                    new ColorThreshold(0, Color.Green),
                    new ColorThreshold(25, Color.Yellow),
                    new ColorThreshold(50, Color.Red)
                )
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

        // Used sparkline - using braille mode for smoother appearance
        var usedSparkline = new SparklineBuilder()
            .WithName("memoryUsedSparkline")
            .WithTitle("Memory Used %", Color.Cyan1)
            .WithHeight(6)
            .WithMaxValue(100)
            .WithBarColor(Color.Cyan1)
            .WithBackgroundColor(Color.Grey15)
            .WithBorder(BorderStyle.Rounded, Color.Grey50)
            .WithMode(SparklineMode.Braille) // Use braille patterns for smoother rendering
            .WithMargin(2, 0, 2, 1)
            .WithData(_memoryUsedHistory)
            .Build();
        panel.AddControl(usedSparkline);

        // Cached sparkline
        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryCachedSparkline")
                .WithTitle("Memory Cached %", Color.Yellow)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithBarColor(Color.Yellow)
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.Rounded, Color.Grey50)
                .WithMode(SparklineMode.Braille)
                .WithMargin(2, 0, 2, 1)
                .WithData(_memoryCachedHistory)
                .Build()
        );

        // Free sparkline
        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryFreeSparkline")
                .WithTitle("Memory Available %", Color.Green)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithBarColor(Color.Green)
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.Rounded, Color.Grey50)
                .WithMode(SparklineMode.Braille)
                .WithMargin(2, 0, 2, 0)
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
                    var lines = BuildCpuTextContent(snapshot);
                    markup.SetContent(lines);
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
        var lines = BuildCpuTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName("cpuPanel")
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
        var lines = BuildCpuTextContent(snapshot);

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
                BuildCpuGraphsContent(scrollPanel, snapshot);

                col.Add(scrollPanel);
            })
            .Build();

        grid.BackgroundColor = Color.Grey11;
        grid.ForegroundColor = Color.Grey93;

        return grid;
    }

    private static List<string> BuildCpuTextContent(SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        int coreCount = cpu.PerCoreSamples?.Count ?? Environment.ProcessorCount;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        double idleCpu = Math.Max(0, 100 - totalCpu);

        // Get top 5 CPU consumers
        var topCpuProcs = snapshot.Processes.OrderByDescending(p => p.CpuPercent).Take(5).ToList();

        var lines = new List<string>
        {
            "",
            $"[cyan1 bold]System CPU ({coreCount} cores)[/]",
            "",
            "[grey70 bold]Aggregate Usage[/]",
            $"  [grey70]User:[/]      [red]{cpu.User:F1}%[/]",
            $"  [grey70]System:[/]    [yellow]{cpu.System:F1}%[/]",
            $"  [grey70]IoWait:[/]    [blue]{cpu.IoWait:F1}%[/] [grey50](Linux only)[/]",
            $"  [grey70]Total:[/]     [cyan1]{totalCpu:F1}%[/]",
            $"  [grey70]Idle:[/]      [green]{idleCpu:F1}%[/]",
            "",
            "[grey70 bold]Per-Core Usage[/]",
        };

        // Add per-core usage with inline bar charts
        if (cpu.PerCoreSamples != null && cpu.PerCoreSamples.Count > 0)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                double coreTotal = core.User + core.System + core.IoWait;
                string bar = BuildInlineBar(coreTotal, 10);

                // Calculate gradient color for this core
                double ratio = coreCount > 1 ? (double)core.CoreIndex / (coreCount - 1) : 0;
                string color = GetGradientColorMarkup(ratio);

                lines.Add($"  [grey70]C{core.CoreIndex,2}:[/] {bar} [{color}]{coreTotal,5:F1}%[/]");
            }
        }
        else
        {
            // Show placeholder cores if data not yet available
            for (int i = 0; i < coreCount; i++)
            {
                string bar = BuildInlineBar(0, 10);
                lines.Add($"  [grey70]C{i,2}:[/] {bar} [grey50]{0.0,5:F1}%[/]");
            }
        }

        lines.Add("");
        lines.Add("[grey70 bold]Top CPU Consumers[/]");

        foreach (var p in topCpuProcs)
        {
            lines.Add($"  [cyan1]{p.CpuPercent,5:F1}%[/]  [grey70]{p.Pid,6}[/]  {p.Command}");
        }

        return lines;
    }

    private static string BuildInlineBar(double percent, int width)
    {
        int filled = (int)Math.Round((percent / 100.0) * width);
        filled = Math.Clamp(filled, 0, width);
        int empty = width - filled;

        string filledBar = new string('█', filled);
        string emptyBar = new string('░', empty);

        return $"[cyan1]{filledBar}[/][grey35]{emptyBar}[/]";
    }

    private static string GetGradientColorMarkup(double ratio)
    {
        // Green (0.0) → Yellow (0.5) → Red (1.0)
        if (ratio < 0.5)
        {
            // Green to Yellow
            return "green";
        }
        else if (ratio < 0.75)
        {
            // Yellow
            return "yellow";
        }
        else
        {
            // Red
            return "red";
        }
    }

    private static void BuildCpuGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;

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
        // User CPU Bar - gradient shows high user CPU as concerning
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
                .WithStandardGradient()
                .Build()
        );

        // System CPU Bar - gradient shows high system CPU as concerning
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
                .WithGradient(
                    new ColorThreshold(0, Color.Green),
                    new ColorThreshold(30, Color.Yellow),
                    new ColorThreshold(50, Color.Red)
                )
                .Build()
        );

        // IoWait CPU Bar - any significant IoWait is concerning
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
                .WithGradient(
                    new ColorThreshold(0, Color.Green),
                    new ColorThreshold(10, Color.Yellow),
                    new ColorThreshold(25, Color.Red)
                )
                .Build()
        );

        // Total CPU Bar - with gradient (green at low load, yellow at medium, red at high)
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
                .WithStandardGradient()
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

        // User CPU sparkline
        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuUserSparkline")
                .WithTitle("User CPU %", Color.Red)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithBarColor(Color.Red)
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.Rounded, Color.Grey50)
                .WithMode(SparklineMode.Braille)
                .WithMargin(2, 0, 2, 1)
                .WithData(_cpuUserHistory)
                .Build()
        );

        // System CPU sparkline
        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuSystemSparkline")
                .WithTitle("System CPU %", Color.Yellow)
                .WithHeight(6)
                .WithMaxValue(100)
                .WithBarColor(Color.Yellow)
                .WithBackgroundColor(Color.Grey15)
                .WithBorder(BorderStyle.Rounded, Color.Grey50)
                .WithMode(SparklineMode.Braille)
                .WithMargin(2, 0, 2, 1)
                .WithData(_cpuSystemHistory)
                .Build()
        );

        // Total CPU sparkline - using braille mode for smoother appearance
        var totalSparkline = new SparklineBuilder()
            .WithName("cpuTotalSparkline")
            .WithTitle("Total CPU %", Color.Cyan1)
            .WithHeight(6)
            .WithMaxValue(100)
            .WithBarColor(Color.Cyan1)
            .WithBackgroundColor(Color.Grey15)
            .WithBorder(BorderStyle.Rounded, Color.Grey50)
            .WithMode(SparklineMode.Braille) // Use braille patterns for smoother rendering
            .WithMargin(2, 0, 2, 1)
            .WithData(_cpuTotalHistory)
            .Build();
        panel.AddControl(totalSparkline);

        // Per-Core History Section
        // Use actual core count if available, otherwise use Environment.ProcessorCount
        int coreCount = cpu.PerCoreSamples != null && cpu.PerCoreSamples.Count > 0
            ? cpu.PerCoreSamples.Count
            : Environment.ProcessorCount;

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
                        .WithTitle($"Core {coreIndex}", coreColor)
                        .WithHeight(4) // Smaller height for per-core
                        .WithMaxValue(100)
                        .WithBarColor(coreColor)
                        .WithBackgroundColor(Color.Grey15)
                        .WithBorder(BorderStyle.Rounded, Color.Grey50)
                        .WithMode(SparklineMode.Braille) // Braille works better at small heights
                        .WithMargin(2, 0, 2, coreIndex == coreCount - 1 ? 0 : 1)
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

        var lines = BuildCpuTextContent(snapshot);

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
        BuildCpuGraphsContent(rightPanel, snapshot);

        var rightCol = new ColumnContainer(grid);
        rightCol.AddContent(rightPanel);
        grid.AddColumn(rightCol);

        _windowSystem?.LogService.LogDebug($"BuildWideCpuColumns: Added 3 columns (40 | 1 | fill)", "CPU");
    }

    private static void BuildNarrowCpuColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        _windowSystem?.LogService.LogDebug("BuildNarrowCpuColumns: Starting", "CPU");

        var lines = BuildCpuTextContent(snapshot);

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
        BuildCpuGraphsContent(scrollPanel, snapshot);

        var col = new ColumnContainer(grid);
        col.AddContent(scrollPanel);
        grid.AddColumn(col);

        _windowSystem?.LogService.LogDebug($"BuildNarrowCpuColumns: Added 1 column with full content", "CPU");
    }
}
