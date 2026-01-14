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
    private static bool _showMemoryInfo = true; // Flag to override and show memory details

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

        var platformName = SystemStatsFactory.GetPlatformName();

        // === TOP STATUS BAR ===
        var topStatusBar = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Add(
                Controls.Markup($"[cyan1 bold]ConsoleTop[/] [grey70]• {platformName}[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .Build()
            ))
            .Column(col => col.Add(
                Controls.Markup("[grey70]--:--:--[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("topStatusClock")
                    .Build()
            ))
            .Build();
        topStatusBar.BackgroundColor = Color.Grey15;
        topStatusBar.ForegroundColor = Color.Grey93;

        _mainWindow.AddControl(topStatusBar);
        _mainWindow.AddControl(Controls.RuleBuilder().StickyTop().WithColor(Color.Grey23).Build());

        // === CPU/MEMORY SECTION ===
        var metricsGrid = Controls.HorizontalGrid()
            .WithMargin(1, 1, 1, 1)
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]⚡ CPU Usage[/]").WithMargin(0, 0, 0, 0).Build())
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
                col.Add(Controls.Markup("[grey70 bold]📊 Memory / IO[/]").WithMargin(0, 0, 0, 0).Build())
                    .Add(
                        SpectreRenderableControl
                            .Create()
                            .WithRenderable(BuildMemoryChart(0, 0, 0))
                            .WithName("memChart")
                            .WithMargin(0, 1, 0, 1)
                            .Build()
                    )
            )
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        metricsGrid.BackgroundColor = Color.Grey11;
        metricsGrid.ForegroundColor = Color.Grey93;

        _mainWindow.AddControl(metricsGrid);

        _mainWindow.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // === PROCESSES SECTION HEADER ===
        var processHeader = Controls.Markup("[grey70 bold]⚙ Processes[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 1, 1, 0)
            .Build();
        processHeader.BackgroundColor = Color.Grey15;
        _mainWindow.AddControl(processHeader);

        // === PROCESS LIST + DETAIL HORIZONTAL LAYOUT (NO SPLITTER) ===
        var mainGrid = Controls.HorizontalGrid()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            // Left column: Process list (no width set = fills remaining space)
            .Column(col => col.Add(
                ListControl.Create()
                    .WithName("processList")
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithColors(Color.Grey11, Color.Grey93)
                    .WithFocusedColors(Color.Grey11, Color.Grey93)
                    .WithHighlightColors(Color.Grey35, Color.White)
                    .WithMargin(1, 0, 0, 1)
                    .OnHighlightChanged((_, item) =>
                    {
                        _showMemoryInfo = false; // Clear flag when navigating
                        UpdateHighlightedProcess();
                    })
                    .OnItemActivated((_, item) =>
                    {
                        if (item?.Tag is ProcessSample ps)
                        {
                            ShowProcessActionsDialog(ps);
                        }
                    })
                    .Build()
            ))
            // Spacing column (1 char wide for visual separation)
            .Column(col => col.Width(1))
            // Right column: Detail panel with fixed width (Grey19 background)
            .Column(col => col
                .Width(40)
                // Top toolbar with buttons
                .Add(Controls.Toolbar()
                    .WithName("detailToolbar")
                    .WithMargin(1, 0, 1, 0)
                    .WithSpacing(2)
                    .AddButton(Controls.Button("Actions")
                        .OnClick((s, e) => ShowProcessActionsDialog())
                        .WithName("actionButton")
                        .Visible(false))
                    .AddButton(Controls.Button("Memory Info")
                        .OnClick((s, e) =>
                        {
                            _showMemoryInfo = true;
                            UpdateHighlightedProcess();
                        })
                        .WithName("memoryInfoButton")
                        .Visible(false))
                    .Build())
                // Detail content (scrollable) - fills remaining vertical space
                .Add(Controls.ScrollablePanel()
                    .WithName("processDetailPanel")
                    .WithVerticalAlignment(VerticalAlignment.Fill)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .WithMargin(1, 0, 1, 0)
                    .AddControl(
                        Controls.Markup()
                            .AddLine("[grey50 italic]Loading...[/]")
                            .WithAlignment(HorizontalAlignment.Left)
                            .WithName("processDetailContent")
                            .Build()
                    )
                    .Build())
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

        // === BOTTOM STATUS BAR ===
        _mainWindow.AddControl(Controls.RuleBuilder().StickyBottom().WithColor(Color.Grey23).Build());

        var bottomStatusBar = Controls.HorizontalGrid()
            .StickyBottom()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Add(
                Controls.Markup()
                    .AddLine("[grey70]F10/ESC: Exit • Click: Select • Double-Click: Actions[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .Build()
            ))
            .Column(col => col.Add(
                Controls.Markup("[grey70]CPU [cyan1]0.0%[/] • MEM [cyan1]0.0%[/] • NET ↑[cyan1]0.0[/]/↓[cyan1]0.0[/] MB/s[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("statsLegend")
                    .Build()
            ))
            .Build();
        bottomStatusBar.BackgroundColor = Color.Grey15;
        bottomStatusBar.ForegroundColor = Color.Grey70;

        _mainWindow.AddControl(bottomStatusBar);

        _windowSystem.AddWindow(_mainWindow);
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
                    statsLegend.SetContent(new List<string>
                    {
                        $"[grey70]CPU [cyan1]{snapshot.Cpu.User:F1}%[/] • MEM [cyan1]{snapshot.Memory.UsedPercent:F1}%[/] • NET ↑[cyan1]{snapshot.Network.UpMbps:F1}[/]/↓[cyan1]{snapshot.Network.DownMbps:F1}[/] MB/s[/]"
                    });
                }

                // Update button states
                var actionButton = window.FindControl<ButtonControl>("actionButton");
                if (actionButton != null)
                {
                    actionButton.IsEnabled = processList?.SelectedIndex >= 0;
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
            .WithMaxValue(100)
            .AddItem("User", user, Color.Cyan1)
            .AddItem("System", sys, Color.Grey50)
            .AddItem("IOwait", io, Color.Grey70);
    }

    private static BarChart BuildMemoryChart(double used, double cached, double ioScaled)
    {
        var chart = new BarChart()
            .Label("[bold]Memory / IO[/]")
            .WithMaxValue(100)
            .AddItem("Used %", used, Color.Cyan1)
            .AddItem("Cached %", cached, Color.Grey50);

        var ioPercent = Math.Min(100, ioScaled);
        chart.AddItem("Disk/IO est %", ioPercent, Color.Grey70);
        return chart;
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
                $"  {p.Pid,5}  [grey70]{p.CpuPercent,4:F1}%[/]  [grey70]{p.MemPercent,4:F1}%[/]  [cyan1]{p.Command}[/]";
            var item = new ListItem(line) { Tag = p };
            items.Add(item);
        }

        if (items.Count == 0)
        {
            items.Add(new ListItem("  [red]No process data available[/]") { IsEnabled = false });
        }

        return items;
    }

    private static void UpdateHighlightedProcess()
    {
        var processList = _mainWindow?.FindControl<ListControl>("processList");
        var detailContent = _mainWindow?.FindControl<MarkupControl>("processDetailContent");
        var detailToolbar = _mainWindow?.FindControl<ToolbarControl>("detailToolbar");
        var actionButton = _mainWindow?.FindControl<ButtonControl>("actionButton");
        var memoryInfoButton = _mainWindow?.FindControl<ButtonControl>("memoryInfoButton");

        if (processList == null || detailContent == null)
            return;

        // Check if we should show memory info (override flag or no highlight)
        bool showMemory = _showMemoryInfo ||
                         processList.HighlightedIndex < 0 ||
                         processList.HighlightedIndex >= processList.Items.Count;

        if (showMemory)
        {
            // Case 1: Show memory breakdown - hide toolbar and buttons
            if (detailToolbar != null)
                detailToolbar.Visible = false;
            if (actionButton != null)
                actionButton.Visible = false;
            if (memoryInfoButton != null)
                memoryInfoButton.Visible = false;

            // Show memory breakdown
            var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
            var lines = BuildMemoryBreakdownContent(snapshot);
            detailContent.SetContent(lines);
            return;
        }

        // Case 2: Process highlighted - show toolbar and buttons
        if (detailToolbar != null)
            detailToolbar.Visible = true;
        if (actionButton != null)
            actionButton.Visible = true;
        if (memoryInfoButton != null)
            memoryInfoButton.Visible = true;

        // Show process details
        var item = processList.Items[processList.HighlightedIndex];
        if (item.Tag is ProcessSample sample)
        {
            var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
            var lines = BuildProcessDetailsContent(sample, snapshot);
            detailContent.SetContent(lines);
        }
    }

    private static List<string> BuildMemoryBreakdownContent(SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;

        // Build progress bars
        var ramBar = BuildProgressBar(mem.UsedPercent);
        var swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        var swapBar = BuildProgressBar(swapPercent);

        // Get top 5 memory consumers
        var topMemProcs = snapshot.Processes
            .OrderByDescending(p => p.MemPercent)
            .Take(5)
            .ToList();

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
            "[grey70 bold]Top Memory Consumers[/]"
        };

        foreach (var p in topMemProcs)
        {
            lines.Add($"  [cyan1]{p.MemPercent,5:F1}%[/]  [grey70]{p.Pid,6}[/]  {p.Command}");
        }

        return lines;
    }

    private static List<string> BuildProcessDetailsContent(ProcessSample sample, SystemSnapshot snapshot)
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
            $"  [grey70]Network:[/] ↑[cyan1]{snapshot.Network.UpMbps:F1}[/] / ↓[cyan1]{snapshot.Network.DownMbps:F1}[/] MB/s"
        };
    }

    private static void ShowProcessActionsDialog(ProcessSample? sample = null)
    {
        // Get selected process if not provided
        if (sample == null)
        {
            var processList = _mainWindow?.FindControl<ListControl>("processList");
            sample = processList?.SelectedItem?.Tag as ProcessSample;
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
            Controls.Markup()
                .AddLine($"[cyan1 bold]Process {liveProc.Pid}[/]")
                .AddLine($"[grey70]{liveProc.Command}[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 0)
                .Build()
        );

        modal.AddControl(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        // Process details with improved formatting
        modal.AddControl(
            Controls.Markup()
                .AddLine($"[grey70]Executable:[/] [cyan1]{extra.ExePath}[/]")
                .AddLine("")
                .AddLine($"[grey70]CPU:[/] [cyan1]{liveProc.CpuPercent:F1}%[/]  [grey70]Memory:[/] [cyan1]{liveProc.MemPercent:F1}%[/]")
                .AddLine($"[grey70]State:[/] [cyan1]{extra.State}[/]  [grey70]Threads:[/] [cyan1]{extra.Threads}[/]")
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
