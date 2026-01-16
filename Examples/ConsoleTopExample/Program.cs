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
    }

    private static TabMode _activeTab = TabMode.Processes;
    private static ProcessSample? _lastHighlightedProcess;

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

        _mainWindow.AddControl(topStatusBar);
        _mainWindow.AddControl(Controls.RuleBuilder().StickyTop().WithColor(Color.Grey23).Build());

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

        _mainWindow.AddControl(metricsGrid);

        _mainWindow.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

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
            .WithSpacing(1)
            .Build();
        _mainWindow.AddControl(tabToolbar);

        _mainWindow.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // === PROCESS LIST + DETAIL HORIZONTAL LAYOUT (NO SPLITTER) ===
        var mainGrid = Controls
            .HorizontalGrid()
            .WithName("processPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            // Left column: Process list (no width set = fills remaining space)
            .Column(col =>
                col.Add(
                    ListControl
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
                        .Build()
                )
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

        _mainWindow.AddControl(mainGrid);

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

        // === MEMORY PANEL (FULL WIDTH) ===
        var memoryPanel = Controls
            .ScrollablePanel()
            .WithName("memoryPanel")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .AddControl(
                Controls
                    .Markup()
                    .AddLine("[grey50 italic]Loading memory information...[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithName("memoryPanelContent")
                    .Build()
            )
            .Visible(false) // Hidden by default, Processes tab is active
            .Build();
        memoryPanel.BackgroundColor = Color.Grey11;
        memoryPanel.ForegroundColor = Color.Grey93;
        _mainWindow.AddControl(memoryPanel);

        // === BOTTOM STATUS BAR ===
        _mainWindow.AddControl(
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

        _mainWindow.AddControl(bottomStatusBar);

        _windowSystem.AddWindow(_mainWindow);
    }

    private static void UpdateDisplay()
    {
        if (_mainWindow == null)
            return;

        // Find both panels
        var processPanel = _mainWindow.FindControl<HorizontalGridControl>("processPanel");
        var memoryPanel = _mainWindow.FindControl<ScrollablePanelControl>("memoryPanel");

        // Switch visibility based on active tab
        if (_activeTab == TabMode.Processes)
        {
            // Show process panel, hide memory panel
            if (processPanel != null)
                processPanel.Visible = true;
            if (memoryPanel != null)
                memoryPanel.Visible = false;

            // Update process detail content
            UpdateHighlightedProcess();
        }
        else // TabMode.Memory
        {
            // Hide process panel, show memory panel
            if (processPanel != null)
                processPanel.Visible = false;
            if (memoryPanel != null)
                memoryPanel.Visible = true;

            // Update memory panel content
            UpdateMemoryPanel();
        }

        _mainWindow.Invalidate(true);
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

                // Update detail panel
                UpdateHighlightedProcess();

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
        var items = new List<ListItem>();

        foreach (var p in processes)
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

    private static void UpdateMemoryPanel()
    {
        var memoryContent = _mainWindow?.FindControl<MarkupControl>("memoryPanelContent");
        if (memoryContent == null)
            return;

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        var lines = BuildMemoryBreakdownContent(snapshot);
        memoryContent.SetContent(lines);
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

    private static List<string> BuildMemoryBreakdownContent(SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;

        // Build progress bars
        var ramBar = BuildProgressBar(mem.UsedPercent);
        var swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        var swapBar = BuildProgressBar(swapPercent);

        // Get top 5 memory consumers
        var topMemProcs = snapshot.Processes.OrderByDescending(p => p.MemPercent).Take(5).ToList();

        var lines = new List<string>
        {
            "",
            "[cyan1 bold]System Memory[/]",
            "",
            "[grey70 bold]Usage[/]",
            $"  RAM:  {ramBar}",
            $"        [cyan1]{mem.UsedPercent:F1}%[/] [grey70]({mem.UsedMb:F0}/{mem.TotalMb:F0} MB)[/]",
            $"  Swap: {swapBar}",
            $"        [cyan1]{swapPercent:F1}%[/] [grey70]({mem.SwapUsedMb:F0}/{mem.SwapTotalMb:F0} MB)[/]",
            "",
            "[grey70 bold]Statistics[/]",
            $"  [grey70]Total:[/]     [cyan1]{mem.TotalMb:F0} MB[/]",
            $"  [grey70]Used:[/]      [cyan1]{mem.UsedMb:F0} MB[/]",
            $"  [grey70]Available:[/] [cyan1]{mem.AvailableMb:F0} MB[/]",
            $"  [grey70]Cached:[/]    [cyan1]{mem.CachedMb:F0} MB[/]",
            $"  [grey70]Buffers:[/]   [cyan1]{mem.BuffersMb:F0} MB[/]",
            "",
            "[grey70 bold]Top Memory Consumers[/]",
        };

        foreach (var p in topMemProcs)
        {
            lines.Add($"  [cyan1]{p.MemPercent, 5:F1}%[/]  [grey70]{p.Pid, 6}[/]  {p.Command}");
        }

        return lines;
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
}
