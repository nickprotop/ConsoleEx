// -----------------------------------------------------------------------
// ConsoleTopExample - ntop/btop-inspired live dashboard
// Demonstrates full-screen window with Spectre renderables and SharpConsoleUI controls
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using Spectre.Console;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using ConsoleTopExample.Stats;

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
                ShowTaskBar = false
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
        if (_windowSystem == null) return;

        _mainWindow = new WindowBuilder(_windowSystem)
            .WithTitle("ConsoleTop - Live System Pulse")
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        // === HEADER SECTION (declarative) ===
        var platformName = SystemStatsFactory.GetPlatformName();
        _mainWindow.AddControl(MarkupControl.Create()
            .AddLines(
                $"[bold cyan]ConsoleTop[/] — System Monitor ({platformName})",
                "[dim]F10/ESC: exit • Live system statistics[/]")
            .Centered()
            .StickyTop()
            .Build());

        _mainWindow.AddControl(RuleControl.Create().StickyTop().Build());

        // === CPU/MEMORY GRID (declarative) ===
        _mainWindow.AddControl(HorizontalGridControl.Create()
            .Column(col => col
                .Width(48)
                .Add(MarkupControl.Create().AddLine("[yellow]CPU Usage[/]").Build())
                .Add(SpectreRenderableControl.Create()
                    .WithRenderable(BuildCpuChart(0, 0, 0))
                    .WithName("cpuChart")
                    .WithMargin(0, 1, 0, 1)
                    .Build()))
            .Column(col => col
                .Add(MarkupControl.Create().AddLine("[yellow]Memory / IO[/]").Build())
                .Add(SpectreRenderableControl.Create()
                    .WithRenderable(BuildMemoryChart(0, 0, 0))
                    .WithName("memChart")
                    .WithMargin(0, 1, 0, 1)
                    .Build()))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());

        _mainWindow.AddControl(RuleControl.Create().Build());

        // === PROCESSES SECTION HEADER (declarative) ===
        _mainWindow.AddControl(MarkupControl.Create()
            .AddLines(
                "[bold]Processes[/]",
                "[dim]Arrows navigate • Enter shows modal • Right panel updates live[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .Build());

        // === PROCESSES GRID (declarative) ===
        var processList = ListControl.Create()
            .WithTitle("Processes")
            .WithName("processList")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Black, Color.White)
            .WithFocusedColors(Color.Grey11, Color.White)
            .WithHighlightColors(Color.DodgerBlue1, Color.White)
            .OnSelectedItemChanged((_, item) =>
            {
                UpdateLegendSelection();
                if (_lastSnapshot != null) UpdateDetailPanel(_lastSnapshot);
            })
            .OnItemActivated((_, item) =>
            {
                if (item.Tag is ProcessSample ps)
                {
                    ShowProcessActions(ps);
                }
            })
            .Build();

        // Update details pane when highlight changes (moving with arrows without selecting)
        _windowSystem.SelectionStateService.HighlightChanged += (sender, args) =>
        {
            var pList = _mainWindow?.FindControl<ListControl>("processList");
            if (args.Control == pList && _lastSnapshot != null)
            {
                UpdateDetailPanel(_lastSnapshot);
            }
        };

        _mainWindow.AddControl(HorizontalGridControl.Create()
            .Column(col => col
                .Width(60)
                .Add(processList))
            .Column(col => col
                .Add(ToolbarControl.Create()
                    .AddButton("Process", (sender, e, window) =>
                    {
                        _detailMode = DetailMode.Process;
                        if (_lastSnapshot != null) UpdateDetailPanel(_lastSnapshot);
                    })
                    .AddSeparator(1)
                    .AddButton("Memory", (sender, e, window) =>
                    {
                        _detailMode = DetailMode.Memory;
                        if (_lastSnapshot != null) UpdateDetailPanel(_lastSnapshot);
                    })
                    .WithSpacing(1)
                    .Build())
                .Add(RuleControl.Create().Build())
                .Add(MarkupControl.Create()
                    .AddLines(
                        "[bold]Process details[/]",
                        "[dim]Select a process to see live stats[/]")
                    .WithName("processDetails")
                    .Build())
                .Add(SpectreRenderableControl.Create()
                    .WithRenderable(BuildMemoryDetailChart(0, 0, 0, 0))
                    .WithName("memoryDetailChart")
                    .Visible(false)
                    .Build())
                .Add(MarkupControl.Create()
                    .AddLine("")
                    .WithName("memoryDetailStats")
                    .Visible(false)
                    .Build()))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build());

        // === FOOTER SECTION (declarative) ===
        _mainWindow.AddControl(RuleControl.Create().StickyBottom().Build());
        _mainWindow.AddControl(MarkupControl.Create()
            .AddLine("[dim]Legend: CPU = user/sys/io | MEM = used/free/cache | NET = up/down MB/s[/]")
            .WithName("legend")
            .StickyBottom()
            .Build());

        // === EVENT HANDLERS ===
        _mainWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.F10 || e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.Shutdown();
                e.Handled = true;
            }
        };

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
                cpuChart?.SetRenderable(BuildCpuChart(snapshot.Cpu.User, snapshot.Cpu.System, snapshot.Cpu.IoWait));

                // Update memory chart
                var ioScaled = Math.Min(100, Math.Max(snapshot.Network.UpMbps, snapshot.Network.DownMbps));
                var memChart = window.FindControl<SpectreRenderableControl>("memChart");
                memChart?.SetRenderable(BuildMemoryChart(snapshot.Memory.UsedPercent, snapshot.Memory.CachedPercent, ioScaled));

                // Update process list
                var processList = window.FindControl<ListControl>("processList");
                if (processList != null)
                {
                    var selectedPid = (processList.SelectedItem?.Tag as ProcessSample)?.Pid;
                    var items = BuildProcessList(snapshot.Processes);
                    processList.Items = items;

                    if (selectedPid.HasValue)
                    {
                        int idx = items.FindIndex(i => (i.Tag as ProcessSample)?.Pid == selectedPid.Value);
                        if (idx >= 0) processList.SelectedIndex = idx;
                    }
                }

                UpdateLegendSelection();
                UpdateDetailPanel(snapshot);

                // Update legend
                var legend = window.FindControl<MarkupControl>("legend");
                legend?.SetContent(new List<string>
                {
                    $"[dim]CPU user:{snapshot.Cpu.User:F1}% sys:{snapshot.Cpu.System:F1}% io:{snapshot.Cpu.IoWait:F1}% | MEM used:{snapshot.Memory.UsedPercent:F1}% cached:{snapshot.Memory.CachedPercent:F1}% | NET up:{snapshot.Network.UpMbps:F1}MB/s down:{snapshot.Network.DownMbps:F1}MB/s[/]"
                });
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

    private static BarChart BuildMemoryDetailChart(double usedMb, double cachedMb, double buffersMb, double availMb)
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
            var line = $"  {p.Pid,5}  {p.CpuPercent,4:F1}%  {p.MemPercent,4:F1}%  [green]{p.Command}[/]";
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
        if (legend == null) return;

        var selection = GetCurrentProcessItem()?.Tag as ProcessSample;
        if (selection == null)
        {
            legend.SetContent(new List<string>
            {
                "[dim]Legend: CPU = user/sys/io | MEM = used/cache | NET = up/down MB/s • Enter for process details[/]"
            });
            return;
        }

        legend.SetContent(new List<string>
        {
            $"[dim]Selected PID {selection.Pid}: CPU {selection.CpuPercent:F1}% MEM {selection.MemPercent:F1}% • Enter opens details[/]"
        });
    }

    private static void UpdateDetailPanel(SystemSnapshot snapshot)
    {
        var processDetails = _mainWindow?.FindControl<MarkupControl>("processDetails");
        if (processDetails == null) return;

        if (_detailMode == DetailMode.Memory)
        {
            // Show memory controls, hide process details
            processDetails.Visible = false;
            var memoryDetailChart = _mainWindow?.FindControl<SpectreRenderableControl>("memoryDetailChart");
            var memoryDetailStats = _mainWindow?.FindControl<MarkupControl>("memoryDetailStats");
            if (memoryDetailChart != null) memoryDetailChart.Visible = true;
            if (memoryDetailStats != null) memoryDetailStats.Visible = true;

            // Update memory chart
            var mem = snapshot.Memory;
            memoryDetailChart?.SetRenderable(BuildMemoryDetailChart(
                mem.UsedMb, mem.CachedMb, mem.BuffersMb, mem.AvailableMb));

            // Build progress bars
            var ramBar = BuildProgressBar(mem.UsedPercent);
            var swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
            var swapBar = BuildProgressBar(swapPercent);

            // Get top 5 memory consumers
            var topMemProcs = snapshot.Processes
                .OrderByDescending(p => p.MemPercent)
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
                $"  Total:     {mem.TotalMb,8:F0} MB    Buffers:  {mem.BuffersMb,6:F0} MB",
                $"  Used:      {mem.UsedMb,8:F0} MB    Dirty:    {mem.DirtyMb,6:F0} MB",
                $"  Available: {mem.AvailableMb,8:F0} MB    Cached:   {mem.CachedMb,6:F0} MB",
                "",
                "[bold]Top 5 Memory Consumers[/]"
            };

            foreach (var p in topMemProcs)
            {
                statsLines.Add($"  {p.MemPercent,5:F1}%  {p.Pid,6}  [green]{p.Command}[/]");
            }

            memoryDetailStats?.SetContent(statsLines);
            return;
        }

        // Process mode - show process details, hide memory controls
        processDetails.Visible = true;
        var memChart = _mainWindow?.FindControl<SpectreRenderableControl>("memoryDetailChart");
        var memStats = _mainWindow?.FindControl<MarkupControl>("memoryDetailStats");
        if (memChart != null) memChart.Visible = false;
        if (memStats != null) memStats.Visible = false;

        var selection = GetCurrentProcessItem()?.Tag as ProcessSample;
        if (selection == null)
        {
            processDetails.SetContent(new List<string>
            {
                "[bold]Process details[/]",
                "[dim]Select a process to see live stats[/]"
            });
            return;
        }

        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == selection.Pid) ?? selection;

        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        processDetails.SetContent(new List<string>
        {
            "[bold]Process details[/]",
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
            $"Net up/down: {snapshot.Network.UpMbps:F1}/{snapshot.Network.DownMbps:F1} MB/s"
        });
    }

    private static ListItem? GetCurrentProcessItem()
    {
        var processList = _mainWindow?.FindControl<ListControl>("processList");
        if (processList == null) return null;
        var items = processList.Items;

        // Prefer highlighted item if available, otherwise fall back to selected
        var highlightIdx = _windowSystem?.SelectionStateService.GetHighlightedIndex(processList) ?? -1;
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
        if (_windowSystem == null) return;

        var snapshot = _stats.ReadSnapshot();
        _lastSnapshot = snapshot;

        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        var modal = new WindowBuilder(_windowSystem)
            .WithTitle($"Process {liveProc.Pid}")
            .WithSize(80, 18)
            .AsModal()
            .Resizable(false)
            .Movable(true)
            .Build();

        modal.AddControl(new MarkupControl(new List<string>
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
            "Actions:"
        }) { HorizontalAlignment = HorizontalAlignment.Left });

        // Create buttons with event handlers
        var killButton = new ButtonControl { Text = "Kill (SIGKILL)", Width = 18 };
        killButton.Click += (_, _) =>
        {
            TryKillProcess(liveProc.Pid, liveProc.Command);
            modal.Close();
        };

        var closeButton = new ButtonControl { Text = "Close", Width = 10 };
        closeButton.Click += (_, _) => modal.Close();

        // Use new ButtonRow factory method (reduces 16 lines to 1!)
        var buttonRow = HorizontalGridControl.ButtonRow(killButton, closeButton);

        modal.AddControl(buttonRow);

        _windowSystem.AddWindow(modal);
        _windowSystem.SetActiveWindow(modal);
    }

    private static void TryKillProcess(int pid, string command)
    {
        if (_windowSystem == null) return;

        try
        {
            var proc = Process.GetProcessById(pid);
            proc.Kill();
            _windowSystem.NotificationStateService.ShowNotification(
                $"Killed {pid}",
                $"{command} (PID {pid}) terminated",
                NotificationSeverity.Info,
                blockUi: false,
                timeout: 2000,
                parentWindow: _mainWindow);
        }
        catch (Exception ex)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"Kill failed for {pid}",
                ex.Message,
                NotificationSeverity.Warning,
                blockUi: false,
                timeout: 3000,
                parentWindow: _mainWindow);
        }
    }

}

internal enum DetailMode
{
    Process,
    Memory
}
