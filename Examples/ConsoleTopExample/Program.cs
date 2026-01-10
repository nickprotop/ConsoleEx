// -----------------------------------------------------------------------
// ConsoleTopExample - ntop/btop-inspired live dashboard
// Demonstrates full-screen window with Spectre renderables and SharpConsoleUI controls
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
    private static DetailMode _detailMode = DetailMode.Process;
    private static readonly ISystemStatsProvider _stats = SystemStatsFactory.Create();
    private static SystemSnapshot? _lastSnapshot;

    static async Task<int> Main(string[] args)
    {
        try
        {
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
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

        // === HEADER SECTION (declarative) ===
        var platformName = SystemStatsFactory.GetPlatformName();
        _mainWindow.AddControl(
            MarkupControl
                .Create()
                .AddLines(
                    $"[bold cyan]ConsoleTop[/] — System Monitor ({platformName})",
                    "[dim]F10/ESC: exit • Live system statistics[/]"
                )
                .Centered()
                .StickyTop()
                .Build()
        );

        _mainWindow.AddControl(RuleControl.Create().StickyTop().Build());

        // === CPU/MEMORY GRID (declarative) ===
        _mainWindow.AddControl(
            HorizontalGridControl
                .Create()
                .Column(col =>
                    col.Width(48)
                        .Add(MarkupControl.Create().AddLine("[yellow]CPU Usage[/]").Build())
                        .Add(
                            SpectreRenderableControl
                                .Create()
                                .WithRenderable(BuildCpuChart(0, 0, 0))
                                .WithName("cpuChart")
                                .WithMargin(0, 1, 0, 1)
                                .Build()
                        )
                )
                .Column(col =>
                    col.Add(MarkupControl.Create().AddLine("[yellow]Memory / IO[/]").Build())
                        .Add(
                            SpectreRenderableControl
                                .Create()
                                .WithRenderable(BuildMemoryChart(0, 0, 0))
                                .WithName("memChart")
                                .WithMargin(0, 1, 0, 1)
                                .Build()
                        )
                )
                .WithSplitterAfter(0)
                .WithAlignment(HorizontalAlignment.Stretch)
                .Build()
        );

        _mainWindow.AddControl(RuleControl.Create().Build());

        // === PROCESSES SECTION HEADER (declarative) ===
        _mainWindow.AddControl(
            MarkupControl
                .Create()
                .AddLines(
                    "[bold]Processes[/]",
                    "[dim]Arrows navigate • Enter shows modal • Right panel updates live[/]"
                )
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );

        // === PROCESSES GRID (declarative) ===
        var processList = ListControl
            .Create()
            .WithTitle("Processes")
            .WithName("processList")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Black, Color.White)
            .WithFocusedColors(Color.Grey11, Color.White)
            .WithHighlightColors(Color.DodgerBlue1, Color.White)
            .OnSelectedItemChanged(
                (_, item) =>
                {
                    UpdateLegendSelection();
                    if (_lastSnapshot != null)
                        UpdateSelectedProcessSection(_lastSnapshot);
                }
            )
            .OnItemActivated(
                (_, item) =>
                {
                    if (item.Tag is ProcessSample ps)
                    {
                        ShowProcessActions(ps);
                    }
                }
            )
            .OnHighlightChanged(
                (_, index) =>
                {
                    if (_lastSnapshot != null)
                    {
                        UpdateHighlightedProcessSection(_lastSnapshot);
                    }
                }
            )
            .Build();

        _mainWindow.AddControl(
            HorizontalGridControl
                .Create()
                .Column(col => col.Width(60).Add(processList))
                .Column(col =>
                    col.Add(
                            ToolbarControl
                                .Create()
                                .AddButton(
                                    "Process",
                                    (sender, e, window) =>
                                    {
                                        _detailMode = DetailMode.Process;
                                        if (_lastSnapshot != null)
                                            UpdateDetailPanel(_lastSnapshot);
                                    }
                                )
                                .AddSeparator(1)
                                .AddButton(
                                    "Memory",
                                    (sender, e, window) =>
                                    {
                                        _detailMode = DetailMode.Memory;
                                        if (_lastSnapshot != null)
                                            UpdateDetailPanel(_lastSnapshot);
                                    }
                                )
                                .WithSpacing(1)
                                .Build()
                        )
                        .Add(RuleControl.Create().Build())
                        // Section 1: Highlighted Process (always visible in Process mode)
                        .Add(
                            MarkupControl
                                .Create()
                                .AddLines(
                                    "[bold]Highlighted Process[/]",
                                    "[dim]Arrow keys to navigate[/]"
                                )
                                .WithName("highlightedProcessHeader")
                                .Build()
                        )
                        .Add(
                            ScrollablePanelControl
                                .Create()
                                .AddControl(
                                    MarkupControl
                                        .Create()
                                        .AddLine("[dim]No process highlighted[/]")
                                        .WithName("highlightedProcessDetails")
                                        .Build()
                                )
                                .WithName("highlightedProcessPanel")
                                .WithVerticalAlignment(VerticalAlignment.Fill)
                                .Build()
                        )
                        // Ruler (visible only when something selected)
                        .Add(
                            RuleControl
                                .Create()
                                .WithName("selectedSectionRuler")
                                .Visible(false)
                                .Build()
                        )
                        // Section 2: Selected Process (visible only when selected)
                        .Add(
                            MarkupControl
                                .Create()
                                .AddLines(
                                    "[bold]Selected Process[/]",
                                    "[dim]Press Enter again to show dialog[/]"
                                )
                                .WithName("selectedProcessHeader")
                                .Visible(false)
                                .Build()
                        )
                        .Add(
                            ScrollablePanelControl
                                .Create()
                                .AddControl(
                                    MarkupControl
                                        .Create()
                                        .AddLine("")
                                        .WithName("selectedProcessDetails")
                                        .Build()
                                )
                                .WithName("selectedProcessPanel")
                                .WithVerticalAlignment(VerticalAlignment.Fill)
                                .Visible(false)
                                .Build()
                        )
                        // Memory mode controls (unchanged)
                        .Add(
                            SpectreRenderableControl
                                .Create()
                                .WithRenderable(BuildMemoryDetailChart(0, 0, 0, 0))
                                .WithName("memoryDetailChart")
                                .Visible(false)
                                .Build()
                        )
                        .Add(
                            MarkupControl
                                .Create()
                                .AddLine("")
                                .WithName("memoryDetailStats")
                                .Visible(false)
                                .Build()
                        )
                )
                .WithSplitterAfter(0)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .Build()
        );

        // === FOOTER SECTION (declarative) ===
        _mainWindow.AddControl(RuleControl.Create().StickyBottom().Build());
        _mainWindow.AddControl(
            MarkupControl
                .Create()
                .AddLine(
                    "[dim]Legend: CPU = user/sys/io | MEM = used/free/cache | NET = up/down MB/s[/]"
                )
                .WithName("legend")
                .StickyBottom()
                .Build()
        );

        _windowSystem.AddWindow(_mainWindow);
        _mainWindow.State = WindowState.Maximized;
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

                UpdateLegendSelection();
                UpdateDetailPanel(snapshot);

                // Update legend
                var legend = window.FindControl<MarkupControl>("legend");
                legend?.SetContent(
                    new List<string>
                    {
                        $"[dim]CPU user:{snapshot.Cpu.User:F1}% sys:{snapshot.Cpu.System:F1}% io:{snapshot.Cpu.IoWait:F1}% | MEM used:{snapshot.Memory.UsedPercent:F1}% cached:{snapshot.Memory.CachedPercent:F1}% | NET up:{snapshot.Network.UpMbps:F1}MB/s down:{snapshot.Network.DownMbps:F1}MB/s[/]",
                    }
                );
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
            .WithMaxValue(100)
            .AddItem("User", user, Color.Lime)
            .AddItem("System", sys, Color.DodgerBlue1)
            .AddItem("IOwait", io, Color.Yellow);
    }

    private static BarChart BuildMemoryChart(double used, double cached, double ioScaled)
    {
        var chart = new BarChart()
            .Label("[bold]Memory / IO[/]")
            .WithMaxValue(100)
            .AddItem("Used %", used, Color.HotPink)
            .AddItem("Cached %", cached, Color.MediumPurple);

        var ioPercent = Math.Min(100, ioScaled);
        chart.AddItem("Disk/IO est %", ioPercent, Color.Orange1);
        return chart;
    }

    private static BarChart BuildMemoryDetailChart(
        double usedMb,
        double cachedMb,
        double buffersMb,
        double availMb
    )
    {
        return new BarChart()
            .Label("[bold]Memory Breakdown (MB)[/]")
            .WithMaxValue(usedMb + cachedMb + buffersMb + availMb)
            .AddItem("Used", usedMb, Color.HotPink)
            .AddItem("Cached", cachedMb, Color.MediumPurple)
            .AddItem("Buffers", buffersMb, Color.Cyan1)
            .AddItem("Available", availMb, Color.Green);
    }

    private static string BuildProgressBar(double percent, int width = 20)
    {
        int filled = (int)Math.Round(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        int empty = width - filled;
        return $"[green]{"█".PadRight(filled, '█')}[/][dim]{"░".PadRight(empty, '░')}[/]";
    }

    private static List<ListItem> BuildProcessList(IReadOnlyList<ProcessSample> processes)
    {
        var items = new List<ListItem>();

        foreach (var p in processes)
        {
            var line =
                $"  {p.Pid, 5}  {p.CpuPercent, 4:F1}%  {p.MemPercent, 4:F1}%  [green]{p.Command}[/]";
            var item = new ListItem(line) { Tag = p };
            items.Add(item);
        }

        if (items.Count == 0)
        {
            items.Add(new ListItem("  [red]No process data available[/]") { IsEnabled = false });
        }

        return items;
    }

    private static void UpdateLegendSelection()
    {
        var legend = _mainWindow?.FindControl<MarkupControl>("legend");
        if (legend == null)
            return;

        var selection = GetCurrentProcessItem()?.Tag as ProcessSample;
        if (selection == null)
        {
            legend.SetContent(
                new List<string>
                {
                    "[dim]Legend: CPU = user/sys/io | MEM = used/cache | NET = up/down MB/s • Enter for process details[/]",
                }
            );
            return;
        }

        legend.SetContent(
            new List<string>
            {
                $"[dim]Selected PID {selection.Pid}: CPU {selection.CpuPercent:F1}% MEM {selection.MemPercent:F1}% • Enter opens details[/]",
            }
        );
    }

    private static void UpdateHighlightedProcessSection(SystemSnapshot snapshot)
    {
        var detailsMarkup = _mainWindow?.FindControl<MarkupControl>("highlightedProcessDetails");
        if (detailsMarkup == null)
            return;

        var processList = _mainWindow?.FindControl<ListControl>("processList");
        if (processList == null)
            return;

        var highlightIdx = processList.HighlightedIndex;
        if (highlightIdx < 0 || highlightIdx >= processList.Items.Count)
        {
            detailsMarkup.SetContent(new List<string> { "[dim]No process highlighted[/]" });
            return;
        }

        var item = processList.Items[highlightIdx];
        if (item.Tag is not ProcessSample sample)
            return;

        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        var lines = new List<string>
        {
            $"PID: {liveProc.Pid}",
            $"Cmd: {liveProc.Command}",
            $"CPU: {liveProc.CpuPercent:F1}%",
            $"Mem: {liveProc.MemPercent:F1}%",
            $"State/Threads: {extra.State} / {extra.Threads}",
            $"RSS: {extra.RssMb:F1} MB",
        };

        detailsMarkup.SetContent(lines);
    }

    private static void UpdateSelectedProcessSection(SystemSnapshot snapshot)
    {
        var processList = _mainWindow?.FindControl<ListControl>("processList");
        var ruler = _mainWindow?.FindControl<RuleControl>("selectedSectionRuler");
        var header = _mainWindow?.FindControl<MarkupControl>("selectedProcessHeader");
        var panel = _mainWindow?.FindControl<ScrollablePanelControl>("selectedProcessPanel");
        var detailsMarkup = _mainWindow?.FindControl<MarkupControl>("selectedProcessDetails");

        if (processList == null)
            return;

        var selectedIdx = processList.SelectedIndex;

        // No selection - hide everything
        if (selectedIdx < 0 || selectedIdx >= processList.Items.Count)
        {
            if (ruler != null)
                ruler.Visible = false;
            if (header != null)
                header.Visible = false;
            if (panel != null)
                panel.Visible = false;
            return;
        }

        // Has selection - show everything
        if (ruler != null)
            ruler.Visible = true;
        if (header != null)
            header.Visible = true;
        if (panel != null)
            panel.Visible = true;

        var item = processList.Items[selectedIdx];
        if (item.Tag is not ProcessSample sample)
            return;

        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        var lines = new List<string>
        {
            $"PID: {liveProc.Pid}",
            $"Cmd: {liveProc.Command}",
            $"Exe: {extra.ExePath}",
            $"CPU: {liveProc.CpuPercent:F1}%",
            $"Mem: {liveProc.MemPercent:F1}%",
            $"State/Threads: {extra.State} / {extra.Threads}",
            $"RSS: {extra.RssMb:F1} MB",
            $"IO r/w: {extra.ReadKb:F0}/{extra.WriteKb:F0} KB/s",
            "",
            "[bold]System snapshot[/]",
            $"CPU user/sys/io: {snapshot.Cpu.User:F1}/{snapshot.Cpu.System:F1}/{snapshot.Cpu.IoWait:F1}%",
            $"Mem used/cache: {snapshot.Memory.UsedPercent:F1}%/{snapshot.Memory.CachedPercent:F1}%",
            $"Net up/down: {snapshot.Network.UpMbps:F1}/{snapshot.Network.DownMbps:F1} MB/s",
        };

        if (detailsMarkup != null)
        {
            detailsMarkup.SetContent(lines);
        }
    }

    private static void UpdateDetailPanel(SystemSnapshot snapshot)
    {
        if (_detailMode == DetailMode.Memory)
        {
            // Show memory controls, hide process sections
            var highlightedHeader = _mainWindow?.FindControl<MarkupControl>(
                "highlightedProcessHeader"
            );
            var highlightedPanel = _mainWindow?.FindControl<ScrollablePanelControl>(
                "highlightedProcessPanel"
            );
            var selectedRuler = _mainWindow?.FindControl<RuleControl>("selectedSectionRuler");
            var selectedHeader = _mainWindow?.FindControl<MarkupControl>("selectedProcessHeader");
            var selectedPanel = _mainWindow?.FindControl<ScrollablePanelControl>(
                "selectedProcessPanel"
            );

            if (highlightedHeader != null)
                highlightedHeader.Visible = false;
            if (highlightedPanel != null)
                highlightedPanel.Visible = false;
            if (selectedRuler != null)
                selectedRuler.Visible = false;
            if (selectedHeader != null)
                selectedHeader.Visible = false;
            if (selectedPanel != null)
                selectedPanel.Visible = false;

            var memoryDetailChart = _mainWindow?.FindControl<SpectreRenderableControl>(
                "memoryDetailChart"
            );
            var memoryDetailStats = _mainWindow?.FindControl<MarkupControl>("memoryDetailStats");
            if (memoryDetailChart != null)
                memoryDetailChart.Visible = true;
            if (memoryDetailStats != null)
                memoryDetailStats.Visible = true;

            // Update memory chart
            var mem = snapshot.Memory;
            memoryDetailChart?.SetRenderable(
                BuildMemoryDetailChart(mem.UsedMb, mem.CachedMb, mem.BuffersMb, mem.AvailableMb)
            );

            // Build progress bars
            var ramBar = BuildProgressBar(mem.UsedPercent);
            var swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
            var swapBar = BuildProgressBar(swapPercent);

            // Get top 5 memory consumers
            var topMemProcs = snapshot
                .Processes.OrderByDescending(p => p.MemPercent)
                .Take(5)
                .ToList();

            var statsLines = new List<string>
            {
                "",
                "[bold]Progress[/]",
                $"RAM:  {ramBar} {mem.UsedPercent:F1}% ({mem.UsedMb:F0}/{mem.TotalMb:F0} MB)",
                $"Swap: {swapBar} {swapPercent:F1}% ({mem.SwapUsedMb:F0}/{mem.SwapTotalMb:F0} MB)",
                "",
                "[bold]Stats[/]",
                $"  Total:     {mem.TotalMb, 8:F0} MB    Buffers:  {mem.BuffersMb, 6:F0} MB",
                $"  Used:      {mem.UsedMb, 8:F0} MB    Dirty:    {mem.DirtyMb, 6:F0} MB",
                $"  Available: {mem.AvailableMb, 8:F0} MB    Cached:   {mem.CachedMb, 6:F0} MB",
                "",
                "[bold]Top 5 Memory Consumers[/]",
            };

            foreach (var p in topMemProcs)
            {
                statsLines.Add($"  {p.MemPercent, 5:F1}%  {p.Pid, 6}  [green]{p.Command}[/]");
            }

            memoryDetailStats?.SetContent(statsLines);
            return;
        }

        // Process mode - show process sections, hide memory controls
        var hlHeader = _mainWindow?.FindControl<MarkupControl>("highlightedProcessHeader");
        var hlPanel = _mainWindow?.FindControl<ScrollablePanelControl>("highlightedProcessPanel");
        if (hlHeader != null)
            hlHeader.Visible = true;
        if (hlPanel != null)
            hlPanel.Visible = true;

        var memChart = _mainWindow?.FindControl<SpectreRenderableControl>("memoryDetailChart");
        var memStats = _mainWindow?.FindControl<MarkupControl>("memoryDetailStats");
        if (memChart != null)
            memChart.Visible = false;
        if (memStats != null)
            memStats.Visible = false;

        UpdateHighlightedProcessSection(snapshot);
        UpdateSelectedProcessSection(snapshot);
    }

    private static ListItem? GetCurrentProcessItem()
    {
        var processList = _mainWindow?.FindControl<ListControl>("processList");
        if (processList == null)
            return null;
        var items = processList.Items;

        // Prefer highlighted item if available, otherwise fall back to selected
        var highlightIdx = processList.HighlightedIndex;
        var selectedIdx = processList.SelectedIndex;

        int idx = highlightIdx >= 0 ? highlightIdx : selectedIdx;
        if (idx >= 0 && idx < items.Count)
        {
            return items[idx];
        }

        return null;
    }

    private static void ShowProcessActions(ProcessSample sample)
    {
        if (_windowSystem == null)
            return;

        var snapshot = _stats.ReadSnapshot();
        _lastSnapshot = snapshot;

        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        var modal = new WindowBuilder(_windowSystem)
            .WithTitle($"Process {liveProc.Pid}")
            .Centered()
            .WithSize(80, 18)
            .AsModal()
            .Resizable(false)
            .Movable(true)
            .Build();

        modal.AddControl(
            new MarkupControl(
                new List<string>
                {
                    $"[bold]Command:[/] {liveProc.Command}",
                    $"PID: {liveProc.Pid}",
                    $"Exe: {extra.ExePath}",
                    $"CPU: {liveProc.CpuPercent:F1}%  MEM: {liveProc.MemPercent:F1}%",
                    $"State/Threads: {extra.State} / {extra.Threads}",
                    $"RSS: {extra.RssMb:F1} MB  IO r/w: {extra.ReadKb:F0}/{extra.WriteKb:F0} KB/s",
                    $"System CPU user/sys/io: {snapshot.Cpu.User:F1}/{snapshot.Cpu.System:F1}/{snapshot.Cpu.IoWait:F1}%",
                    $"System MEM used/cache: {snapshot.Memory.UsedMb:F0}MB/{snapshot.Memory.CachedMb:F0}MB ({snapshot.Memory.UsedPercent:F1}%/{snapshot.Memory.CachedPercent:F1}%)",
                    $"System NET up/down: {snapshot.Network.UpMbps:F1}/{snapshot.Network.DownMbps:F1} MB/s",
                    "",
                    "Actions:",
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Left,
            }
        );

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

            buttonRow = HorizontalGridControl.ButtonRow(terminateButton, forceKillButton, closeButton);
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
                    $"Force killed {pid}",
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
                    $"Terminated {pid}",
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
                $"Terminate failed for {pid}",
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
                $"{killMethod} {pid}",
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
                $"Kill failed for {pid}",
                ex.Message,
                NotificationSeverity.Warning,
                blockUi: false,
                timeout: 3000,
                parentWindow: _mainWindow
            );
        }
    }
}

internal enum DetailMode
{
    Process,
    Memory,
}
