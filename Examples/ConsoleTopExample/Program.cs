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

namespace ConsoleTopExample;

internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _mainWindow;

    // State (not UI references)
    private static DetailMode _detailMode = DetailMode.Process;
    private static readonly LinuxSystemStats _stats = new();
    private static SystemSnapshot? _lastSnapshot;

    static async Task<int> Main(string[] args)
    {
        try
        {
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "ConsoleTop - btop-style stats",
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

        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold cyan]ConsoleTop[/] — ntop-inspired live dashboard",
            "[dim]F10/ESC: exit • Live data from /proc[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Top
        });

        _mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        var grid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
            // No VerticalAlignment.Fill - let it use natural height from content
        };

        var cpuColumn = new ColumnContainer(grid) { Width = 48 };
        cpuColumn.AddContent(new MarkupControl(new List<string> { "[yellow]CPU Usage[/]" }));
        var cpuChart = new SpectreRenderableControl(BuildCpuChart(0, 0, 0))
        {
            Name = "cpuChart",
            Margin = new Margin(0, 1, 0, 1)
        };
        cpuColumn.AddContent(cpuChart);
        grid.AddColumn(cpuColumn);

        var memColumn = new ColumnContainer(grid);
        memColumn.AddContent(new MarkupControl(new List<string> { "[yellow]Memory / IO[/]" }));
        var memChart = new SpectreRenderableControl(BuildMemoryChart(0, 0, 0))
        {
            Name = "memChart",
            Margin = new Margin(0, 1, 0, 1)
        };
        memColumn.AddContent(memChart);
        grid.AddColumn(memColumn);

        grid.AddSplitter(0, new SplitterControl());
        _mainWindow.AddControl(grid);

        _mainWindow.AddControl(new RuleControl());

        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold]Processes[/]",
            "[dim]Arrows navigate • Enter shows modal • Right panel updates live[/]"
        }) { HorizontalAlignment = HorizontalAlignment.Left });

        var processesGrid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        var listColumn = new ColumnContainer(processesGrid) { Width = 60 };
        var processList = new ListControl("Processes")
        {
            Name = "processList",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill,
            BackgroundColor = Color.Black,
            ForegroundColor = Color.White,
            FocusedBackgroundColor = Color.Grey11,
            FocusedForegroundColor = Color.White,
            HighlightBackgroundColor = Color.DodgerBlue1,
            HighlightForegroundColor = Color.White
        };
        processList.SelectedIndexChanged += (_, _) =>
        {
            UpdateLegendSelection();
            if (_lastSnapshot != null) UpdateDetailPanel(_lastSnapshot);
        };

        processList.ItemActivated += (_, item) =>
        {
            if (item.Tag is ProcessSample ps)
            {
                ShowProcessActions(ps);
            }
        };

        // Update details pane when highlight changes (moving with arrows without selecting)
        _windowSystem.SelectionStateService.HighlightChanged += (sender, args) =>
        {
            var pList = _mainWindow?.FindControl<ListControl>("processList");
            if (args.Control == pList && _lastSnapshot != null)
            {
                UpdateDetailPanel(_lastSnapshot);
            }
        };
        listColumn.AddContent(processList);

        var detailColumn = new ColumnContainer(processesGrid);

        // Header with mode buttons using ToolbarControl
        var toolbar = ToolbarControl.Create()
            .AddButton("Process", (sender, e, window) =>
            {
                _detailMode = DetailMode.Process;
                if (_lastSnapshot != null)
                {
                    UpdateDetailPanel(_lastSnapshot);
                }
            })
            .AddSeparator(1)
            .AddButton("Memory", (sender, e, window) =>
            {
                _detailMode = DetailMode.Memory;
                if (_lastSnapshot != null)
                {
                    UpdateDetailPanel(_lastSnapshot);
                }
            })
            .WithSpacing(1)
            .Build();

        detailColumn.AddContent(toolbar);
        detailColumn.AddContent(new RuleControl());

        // Process details (shown in Process mode)
        var processDetails = new MarkupControl(new List<string>
        {
            "[bold]Process details[/]",
            "[dim]Select a process to see live stats[/]"
        })
        {
            Name = "processDetails"
        };
        detailColumn.AddContent(processDetails);

        // Memory detail chart (shown in Memory mode)
        var memoryDetailChart = new SpectreRenderableControl(BuildMemoryDetailChart(0, 0, 0, 0))
        {
            Name = "memoryDetailChart",
            Visible = false
        };
        detailColumn.AddContent(memoryDetailChart);

        // Memory detail stats (shown in Memory mode)
        var memoryDetailStats = new MarkupControl(new List<string> { "" })
        {
            Name = "memoryDetailStats",
            Visible = false
        };
        detailColumn.AddContent(memoryDetailStats);

        processesGrid.AddColumn(listColumn);
        processesGrid.AddColumn(detailColumn);
        processesGrid.AddSplitter(0, new SplitterControl());
        _mainWindow.AddControl(processesGrid);

        var legend = new MarkupControl(new List<string>
        {
            "[dim]Legend: CPU = user/sys/io | MEM = used/free/cache | NET = up/down MB/s[/]"
        })
        {
            Name = "legend",
            StickyPosition = StickyPosition.Bottom
        };
        _mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom });
        _mainWindow.AddControl(legend);

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

        var extra = ReadProcessExtra(liveProc.Pid);

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
        var extra = ReadProcessExtra(liveProc.Pid);

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

    private static ProcessExtra ReadProcessExtra(int pid)
    {
        double rssMb = 0;
        int threads = 0;
        string state = "?";
        double readKb = 0;
        double writeKb = 0;
        string exePath = "";

        try
        {
            var statusPath = $"/proc/{pid}/status";
            if (File.Exists(statusPath))
            {
                foreach (var line in File.ReadLines(statusPath))
                {
                    if (line.StartsWith("VmRSS:")) rssMb = ParseLongSafe(line) / 1024.0; // kB -> MB
                    else if (line.StartsWith("Threads:")) threads = (int)ParseLongSafe(line);
                    else if (line.StartsWith("State:"))
                    {
                        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) state = parts[1].Trim();
                    }
                }
            }

            var ioPath = $"/proc/{pid}/io";
            if (File.Exists(ioPath))
            {
                foreach (var line in File.ReadLines(ioPath))
                {
                    if (line.StartsWith("read_bytes:")) readKb = ParseLongSafe(line) / 1024.0;
                    else if (line.StartsWith("write_bytes:")) writeKb = ParseLongSafe(line) / 1024.0;
                }
            }

            var exeLink = $"/proc/{pid}/exe";
            if (File.Exists(exeLink))
            {
                try
                {
                    exePath = Path.GetFullPath(exeLink);
                }
                catch
                {
                    exePath = exeLink;
                }
            }
        }
        catch
        {
            // Processes may exit or be inaccessible; keep defaults
        }

        return new ProcessExtra(state, threads, rssMb, readKb, writeKb, exePath);
    }

    private static long ParseLongSafe(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (long.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
                return val;
        }
        return 0;
    }
}

internal enum DetailMode
{
    Process,
    Memory
}

internal sealed class LinuxSystemStats
{
    private CpuTimes? _previousCpu;
    private Dictionary<string, NetCounters>? _previousNet;
    private DateTime _previousNetSample = DateTime.MinValue;

    public SystemSnapshot ReadSnapshot()
    {
        var cpu = ReadCpu();
        var mem = ReadMemory();
        var net = ReadNetwork();
        var procs = ReadTopProcesses();
        return new SystemSnapshot(cpu, mem, net, procs);
    }

    private CpuSample ReadCpu()
    {
        var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
        if (line == null) return new CpuSample(0, 0, 0);

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8) return new CpuSample(0, 0, 0);

        long user = ParseLong(parts[1]);
        long nice = ParseLong(parts[2]);
        long system = ParseLong(parts[3]);
        long idle = ParseLong(parts[4]);
        long iowait = ParseLong(parts[5]);
        long irq = ParseLong(parts[6]);
        long softirq = ParseLong(parts[7]);
        long steal = parts.Length > 8 ? ParseLong(parts[8]) : 0;

        var current = new CpuTimes
        {
            User = user + nice,
            System = system + irq + softirq,
            IoWait = iowait,
            Idle = idle,
            Steal = steal
        };

        if (_previousCpu == null)
        {
            _previousCpu = current;
            return new CpuSample(0, 0, 0);
        }

        var deltaUser = current.User - _previousCpu.User;
        var deltaSystem = current.System - _previousCpu.System;
        var deltaIo = current.IoWait - _previousCpu.IoWait;
        var deltaIdle = current.Idle - _previousCpu.Idle;
        var deltaSteal = current.Steal - _previousCpu.Steal;

        double total = deltaUser + deltaSystem + deltaIo + deltaIdle + deltaSteal;
        if (total <= 0)
        {
            _previousCpu = current;
            return new CpuSample(0, 0, 0);
        }

        var sample = new CpuSample(
            Percent(deltaUser, total),
            Percent(deltaSystem, total),
            Percent(deltaIo, total)
        );

        _previousCpu = current;
        return sample;
    }

    private MemorySample ReadMemory()
    {
        double total = 0;
        double available = 0;
        double cached = 0;
        double swapTotal = 0;
        double swapFree = 0;
        double buffers = 0;
        double dirty = 0;

        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:")) total = ExtractKb(line);
            else if (line.StartsWith("MemAvailable:")) available = ExtractKb(line);
            else if (line.StartsWith("Cached:")) cached = ExtractKb(line);
            else if (line.StartsWith("SwapTotal:")) swapTotal = ExtractKb(line);
            else if (line.StartsWith("SwapFree:")) swapFree = ExtractKb(line);
            else if (line.StartsWith("Buffers:")) buffers = ExtractKb(line);
            else if (line.StartsWith("Dirty:")) dirty = ExtractKb(line);
        }

        if (total <= 0) return new MemorySample(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var used = Math.Max(0, total - available);
        var usedPercent = Percent(used, total);
        var cachedPercent = Percent(cached, total);
        var swapUsed = Math.Max(0, swapTotal - swapFree);

        double totalMb = total / 1024.0;
        double usedMb = used / 1024.0;
        double availMb = available / 1024.0;
        double cachedMb = cached / 1024.0;
        double swapTotalMb = swapTotal / 1024.0;
        double swapUsedMb = swapUsed / 1024.0;
        double swapFreeMb = swapFree / 1024.0;
        double buffersMb = buffers / 1024.0;
        double dirtyMb = dirty / 1024.0;

        return new MemorySample(usedPercent, cachedPercent, totalMb, usedMb, availMb, cachedMb,
            swapTotalMb, swapUsedMb, swapFreeMb, buffersMb, dirtyMb);
    }

    private NetworkSample ReadNetwork()
    {
        var lines = File.ReadAllLines("/proc/net/dev");
        var now = DateTime.UtcNow;

        var current = new Dictionary<string, NetCounters>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(2))
        {
            var parts = line.Split(':');
            if (parts.Length != 2) continue;
            var name = parts[0].Trim();
            if (name == "lo") continue;

            var fields = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 16) continue;

            var rxBytes = ParseLong(fields[0]);
            var txBytes = ParseLong(fields[8]);
            current[name] = new NetCounters(rxBytes, txBytes);
        }

        if (_previousNet == null || _previousNetSample == DateTime.MinValue)
        {
            _previousNet = current;
            _previousNetSample = now;
            return new NetworkSample(0, 0);
        }

        var seconds = Math.Max(0.1, (now - _previousNetSample).TotalSeconds);
        double rxDiff = 0;
        double txDiff = 0;

        foreach (var kvp in current)
        {
            if (_previousNet.TryGetValue(kvp.Key, out var prev))
            {
                rxDiff += Math.Max(0, kvp.Value.RxBytes - prev.RxBytes);
                txDiff += Math.Max(0, kvp.Value.TxBytes - prev.TxBytes);
            }
        }

        _previousNet = current;
        _previousNetSample = now;

        var upMbps = (txDiff / seconds) / (1024 * 1024);
        var downMbps = (rxDiff / seconds) / (1024 * 1024);
        return new NetworkSample(upMbps, downMbps);
    }

    private List<ProcessSample> ReadTopProcesses()
    {
        try
        {
            var psPath = File.Exists("/bin/ps") ? "/bin/ps" : "ps";
            var startInfo = new ProcessStartInfo
            {
                FileName = psPath,
                Arguments = "-eo pid,pcpu,pmem,comm --sort=-pcpu",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            if (proc == null) return new List<ProcessSample>();

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(500);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
            var result = new List<ProcessSample>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var cpu)) cpu = 0;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var mem)) mem = 0;
                var cmd = string.Join(' ', parts.Skip(3));

                result.Add(new ProcessSample(pid, cpu, mem, cmd));
            }

            return result;
        }
        catch
        {
            return new List<ProcessSample>();
        }
    }

    private static double ExtractKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return 0;
        return ParseLong(parts[1]);
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static double Percent(double part, double total)
    {
        if (total <= 0) return 0;
        return Math.Round(part / total * 100.0, 1);
    }

    private sealed class CpuTimes
    {
        public long User { get; init; }
        public long System { get; init; }
        public long IoWait { get; init; }
        public long Idle { get; init; }
        public long Steal { get; init; }
    }
}

internal record CpuSample(double User, double System, double IoWait);
internal record MemorySample(
    double UsedPercent, double CachedPercent,
    double TotalMb, double UsedMb, double AvailableMb, double CachedMb,
    double SwapTotalMb, double SwapUsedMb, double SwapFreeMb,
    double BuffersMb, double DirtyMb);
internal record NetworkSample(double UpMbps, double DownMbps);
internal record ProcessSample(int Pid, double CpuPercent, double MemPercent, string Command);
internal record SystemSnapshot(CpuSample Cpu, MemorySample Memory, NetworkSample Network, IReadOnlyList<ProcessSample> Processes);
internal record NetCounters(long RxBytes, long TxBytes);
internal record ProcessExtra(string State, int Threads, double RssMb, double ReadKb, double WriteKb, string ExePath);
