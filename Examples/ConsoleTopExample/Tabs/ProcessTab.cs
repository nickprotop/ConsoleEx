using System.Diagnostics;
using System.Runtime.InteropServices;
using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace ConsoleTopExample.Tabs;

internal enum ProcessSortMode { Cpu, Memory, Pid, Name }

internal sealed class ProcessTab : ITab
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly ISystemStatsProvider _stats;
    private ProcessSortMode _sortMode = ProcessSortMode.Cpu;
    private string _searchFilter = string.Empty;
    private ProcessSample? _lastHighlightedProcess;
    private SystemSnapshot? _lastSnapshot;
    private readonly Dictionary<int, ProcessExtra> _processExtraCache = new();

    public ProcessTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
    {
        _windowSystem = windowSystem;
        _stats = stats;
    }

    public string Name => "Processes";
    public string PanelControlName => "processPanel";

    public IWindowControl BuildPanel(SystemSnapshot initialSnapshot, int windowWidth)
    {
        _lastSnapshot = initialSnapshot;

        var processSortToolbar = Controls
            .Toolbar()
            .WithName("processSortToolbar")
            .WithMargin(1, 0, 0, 0)
            .Add(
                Controls.Prompt()
                    .WithName("processSearch")
                    .WithPrompt("Filter: ")
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .OnInputChanged((_, text) =>
                    {
                        _searchFilter = text;
                        UpdateProcessList();
                    })
                    .Build()
            )
            .Add(
                Controls.Dropdown()
                    .WithName("processSortDropdown")
                    .WithPrompt("Sort:")
                    .AddItem("CPU %")
                    .AddItem("Memory %")
                    .AddItem("PID")
                    .AddItem("Name")
                    .SelectedIndex(0)
                    .WithWidth(UIConstants.SortDropdownWidth)
                    .OnSelectionChanged((sender, index, window) =>
                    {
                        _sortMode = (ProcessSortMode)index;
                        UpdateProcessList();
                    })
                    .Build()
            )
            .Build();

        var processListControl = ListControl
            .Create()
            .WithName("processList")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(UIConstants.WindowBackground, UIConstants.WindowForeground)
            .WithFocusedColors(UIConstants.WindowBackground, UIConstants.WindowForeground)
            .WithHighlightColors(UIConstants.ProcessHighlightBg, UIConstants.ProcessHighlightFg)
            .WithMargin(1, 0, 0, 1)
            .OnHighlightChanged((_, idx) =>
            {
                var processList = FindMainWindow()?.FindControl<ListControl>("processList");
                if (processList != null && idx >= 0 && idx < processList.Items.Count)
                    _lastHighlightedProcess = processList.Items[idx].Tag as ProcessSample;

                UpdateHighlightedProcess();
            })
            .OnItemActivated((_, item) =>
            {
                if (item?.Tag is ProcessSample ps)
                    ShowProcessActionsDialog(ps);
            })
            .SimpleMode()
            .Build();

        var mainGrid = Controls
            .HorizontalGrid()
            .WithName(PanelControlName)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(processSortToolbar)
                   .Add(processListControl)
            )
            .Column(col => col.Width(UIConstants.SeparatorColumnWidth))
            .Column(col =>
                col.Width(UIConstants.ProcessDetailColumnWidth)
                    .Add(
                        Controls.Toolbar()
                            .WithName("detailToolbar")
                            .WithMargin(1, 0, 1, 1)
                            .WithSpacing(2)
                            .AddButton(
                                Controls.Button("Actions...")
                                    .WithWidth(UIConstants.ActionsButtonWidth)
                                    .OnClick((s, e) => ShowProcessActionsDialog())
                                    .WithName("actionButton")
                                    .Visible(false)
                            )
                            .Build()
                    )
                    .Add(
                        Controls.ScrollablePanel()
                            .WithName("processDetailPanel")
                            .WithVerticalAlignment(VerticalAlignment.Fill)
                            .WithAlignment(HorizontalAlignment.Stretch)
                            .WithMargin(2, 0, 1, 0)
                            .AddControl(
                                Controls.Markup()
                                    .AddLine("[grey50 italic]Loading...[/]")
                                    .WithAlignment(HorizontalAlignment.Left)
                                    .WithName("processDetailContent")
                                    .Build()
                            )
                            .Build()
                    )
            )
            .Build();

        if (mainGrid.Columns.Count > 2)
        {
            mainGrid.Columns[2].BackgroundColor = UIConstants.DetailPanelBackground;
            mainGrid.Columns[2].ForegroundColor = UIConstants.DetailPanelForeground;
        }

        return mainGrid;
    }

    public void UpdatePanel(SystemSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _processExtraCache.Clear();
        var mainWindow = FindMainWindow();
        if (mainWindow == null) return;

        var processList = mainWindow.FindControl<ListControl>("processList");
        if (processList != null)
        {
            var selectedPid = (processList.SelectedItem?.Tag as ProcessSample)?.Pid;
            var items = BuildProcessList(snapshot.Processes);
            processList.Items = items;

            if (selectedPid.HasValue)
            {
                int idx = items.FindIndex(i => (i.Tag as ProcessSample)?.Pid == selectedPid.Value);
                if (idx >= 0)
                    processList.SelectedIndex = idx;
            }
        }

        UpdateHighlightedProcess();

        var actionButton = mainWindow.FindControl<ButtonControl>("actionButton");
        if (actionButton != null)
        {
            bool hasProcess = (processList?.HighlightedIndex >= 0) || (_lastHighlightedProcess != null);
            actionButton.IsEnabled = hasProcess;
        }
    }

    public void HandleResize(int newWidth, int newHeight) { }

    public void CycleSortMode()
    {
        _sortMode = _sortMode switch
        {
            ProcessSortMode.Cpu    => ProcessSortMode.Memory,
            ProcessSortMode.Memory => ProcessSortMode.Pid,
            ProcessSortMode.Pid    => ProcessSortMode.Name,
            _                      => ProcessSortMode.Cpu
        };
        var dropdown = FindMainWindow()?.FindControl<DropdownControl>("processSortDropdown");
        if (dropdown != null)
            dropdown.SelectedIndex = (int)_sortMode;
        UpdateProcessList();
    }

    #region Post-Build Setup

    public void ApplyDetailPanelColors(Window mainWindow)
    {
        var detailToolbar = mainWindow.FindControl<ToolbarControl>("detailToolbar");
        if (detailToolbar != null)
        {
            detailToolbar.BackgroundColor = UIConstants.DetailPanelBackground;
            detailToolbar.ForegroundColor = UIConstants.DetailPanelForeground;
        }

        var detailPanel = mainWindow.FindControl<ScrollablePanelControl>("processDetailPanel");
        if (detailPanel != null)
        {
            detailPanel.BackgroundColor = UIConstants.DetailPanelBackground;
            detailPanel.ForegroundColor = UIConstants.DetailPanelForeground;
        }
    }

    #endregion

    #region Process List Building

    private List<ListItem> BuildProcessList(IReadOnlyList<ProcessSample> processes)
    {
        IEnumerable<ProcessSample> source = string.IsNullOrEmpty(_searchFilter)
            ? processes
            : processes.Where(p => p.Command.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

        var sorted = _sortMode switch
        {
            ProcessSortMode.Cpu    => source.OrderByDescending(p => p.CpuPercent),
            ProcessSortMode.Memory => source.OrderByDescending(p => p.MemPercent),
            ProcessSortMode.Pid    => source.OrderBy(p => p.Pid),
            ProcessSortMode.Name   => source.OrderBy(p => p.Command, StringComparer.OrdinalIgnoreCase),
            _                      => source.OrderByDescending(p => p.CpuPercent)
        };

        var items = new List<ListItem>();
        foreach (var p in sorted)
        {
            var pidStr = p.Pid.ToString().PadLeft(UIConstants.PidPadLeft);
            var cpuStr = $"{p.CpuPercent,5:F1}%".PadLeft(UIConstants.CpuPercentPadLeft);
            var memStr = $"{p.MemPercent,5:F1}%".PadLeft(UIConstants.MemPercentPadLeft);
            var cpuColor = UIConstants.ThresholdColor(p.CpuPercent);
            var memColor = UIConstants.ThresholdColor(p.MemPercent);
            var memMb = (_lastSnapshot?.Memory.TotalMb ?? 0) > 0
                ? p.MemPercent / 100.0 * _lastSnapshot!.Memory.TotalMb
                : 0;
            string memMbRaw = memMb > 0 ? $"{memMb:F0}M" : "";
            string memMbPadded = memMbRaw.PadLeft(UIConstants.MemMbPadLeft);
            var line = $"{pidStr}  [{cpuColor}]{cpuStr}[/]  [{memColor}]{memStr}[/] [grey50]{memMbPadded}[/]  [cyan1]{p.Command}[/]";
            items.Add(new ListItem(line) { Tag = p });
        }

        if (items.Count == 0)
            items.Add(new ListItem("  [red]No process data available[/]") { IsEnabled = false });

        return items;
    }

    private void UpdateProcessList()
    {
        if (_lastSnapshot == null) return;
        var mainWindow = FindMainWindow();
        if (mainWindow == null) return;

        var processList = mainWindow.FindControl<ListControl>("processList");
        if (processList == null) return;

        var selectedPid = (processList.SelectedItem?.Tag as ProcessSample)?.Pid;
        var items = BuildProcessList(_lastSnapshot.Processes);
        processList.Items = items;

        if (selectedPid.HasValue)
        {
            int idx = items.FindIndex(i => (i.Tag as ProcessSample)?.Pid == selectedPid.Value);
            if (idx >= 0)
                processList.SelectedIndex = idx;
        }
    }

    #endregion

    #region Process Details

    public void UpdateHighlightedProcess()
    {
        var mainWindow = FindMainWindow();
        if (mainWindow == null) return;

        var processList = mainWindow.FindControl<ListControl>("processList");
        var detailContent = mainWindow.FindControl<MarkupControl>("processDetailContent");
        var detailToolbar = mainWindow.FindControl<ToolbarControl>("detailToolbar");
        var actionButton = mainWindow.FindControl<ButtonControl>("actionButton");

        if (processList == null || detailContent == null)
            return;

        ProcessSample? processToShow = null;
        if (processList.HighlightedIndex >= 0 && processList.HighlightedIndex < processList.Items.Count)
            processToShow = processList.Items[processList.HighlightedIndex].Tag as ProcessSample;
        else if (_lastHighlightedProcess != null)
            processToShow = _lastHighlightedProcess;

        if (processToShow == null)
        {
            if (detailToolbar != null) detailToolbar.Visible = false;
            if (actionButton != null) actionButton.Visible = false;
            detailContent.SetContent(new List<string> { "", "[grey50 italic]Select a process to view details[/]" });
            return;
        }

        if (detailToolbar != null) detailToolbar.Visible = true;
        if (actionButton != null) actionButton.Visible = true;

        var snapshot = _lastSnapshot ?? _stats.ReadSnapshot();
        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == processToShow.Pid) ?? processToShow;
        if (!_processExtraCache.TryGetValue(liveProc.Pid, out var extra))
        {
            extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");
            _processExtraCache[liveProc.Pid] = extra;
        }

        var exeDisplay = extra.ExePath.Length > 24
            ? "…" + extra.ExePath[^23..]
            : extra.ExePath;

        detailContent.SetContent(new List<string>
        {
            "",
            $"[cyan1 bold]{liveProc.Command}[/]",
            "",
            $"[grey70]PID:[/] [cyan1]{liveProc.Pid}[/]",
            $"[grey70]Executable:[/] [cyan1]{exeDisplay}[/]",
            "",
            $"[grey70 bold]Process Metrics[/]",
            $"  [grey70]CPU:[/] [{UIConstants.ThresholdColor(liveProc.CpuPercent)}]{liveProc.CpuPercent:F1}%[/]",
            $"  [grey70]Memory:[/] [{UIConstants.ThresholdColor(liveProc.MemPercent)}]{liveProc.MemPercent:F1}%[/] [grey50]({extra.RssMb:F0} MB RSS)[/]",
            $"  [grey70]State:[/] [cyan1]{extra.State}[/]  [grey70]Threads:[/] [cyan1]{extra.Threads}[/]",
            $"  [grey70]RSS:[/] [cyan1]{extra.RssMb:F1} MB[/]",
            $"  [grey70]I/O:[/] [cyan1]R:{extra.ReadKb:F0} / W:{extra.WriteKb:F0} KB/s[/]",
        });
    }

    #endregion

    #region Process Actions

    private void ShowProcessActionsDialog(ProcessSample? sample = null)
    {
        if (sample == null)
        {
            var mainWindow = FindMainWindow();
            var processList = mainWindow?.FindControl<ListControl>("processList");
            if (processList != null && processList.HighlightedIndex >= 0 && processList.HighlightedIndex < processList.Items.Count)
                sample = processList.Items[processList.HighlightedIndex].Tag as ProcessSample;
            sample ??= _lastHighlightedProcess;
        }

        if (sample == null) return;

        var snapshot = _stats.ReadSnapshot();
        _lastSnapshot = snapshot;
        var liveProc = snapshot.Processes.FirstOrDefault(p => p.Pid == sample.Pid) ?? sample;
        var extra = _stats.ReadProcessExtra(liveProc.Pid) ?? new ProcessExtra("?", 0, 0, 0, 0, "");

        var modal = new WindowBuilder(_windowSystem)
            .WithTitle("Process Actions")
            .Centered()
            .WithSize(UIConstants.ProcessActionsModalWidth, UIConstants.ProcessActionsModalHeight)
            .AsModal()
            .Borderless()
            .Resizable(false)
            .Movable(false)
            .WithColors(UIConstants.MetricsBoxBackground, UIConstants.WindowForeground)
            .Build();

        modal.AddControl(
            Controls.Markup()
                .AddLine($"[cyan1 bold]Process {liveProc.Pid}[/]")
                .AddLine($"[grey70]{liveProc.Command}[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 0)
                .Build()
        );

        modal.AddControl(Controls.RuleBuilder().WithColor(UIConstants.RuleColor).Build());

        modal.AddControl(
            Controls.Markup()
                .AddLine($"[grey70]Executable:[/] [cyan1]{extra.ExePath}[/]")
                .AddLine("")
                .AddLine($"[grey70]CPU:[/] [cyan1]{liveProc.CpuPercent:F1}%[/]  [grey70]Memory:[/] [cyan1]{liveProc.MemPercent:F1}%[/]")
                .AddLine($"[grey70]State:[/] [cyan1]{extra.State}[/]  [grey70]Threads:[/] [cyan1]{extra.Threads}[/]")
                .AddLine($"[grey70]RSS:[/] [cyan1]{extra.RssMb:F1} MB[/]")
                .AddLine($"[grey70]I/O:[/] [cyan1]R:{extra.ReadKb:F0} / W:{extra.WriteKb:F0} KB/s[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 1)
                .Build()
        );

        modal.AddControl(Controls.RuleBuilder().WithColor(UIConstants.RuleColor).Build());

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        ButtonControl closeButton;
        HorizontalGridControl buttonRow;

        if (isWindows)
        {
            var terminateButton = new ButtonControl { Text = "Terminate", Width = UIConstants.TerminateButtonWidth };
            terminateButton.Click += (_, _) => { TryTerminateProcess(liveProc.Pid, liveProc.Command); modal.Close(); };

            var forceKillButton = new ButtonControl { Text = "Force Kill", Width = UIConstants.ForceKillButtonWidth };
            forceKillButton.Click += (_, _) => { TryKillProcess(liveProc.Pid, liveProc.Command); modal.Close(); };

            closeButton = new ButtonControl { Text = "Close", Width = UIConstants.CloseButtonWidth };
            closeButton.Click += (_, _) => modal.Close();

            buttonRow = HorizontalGridControl.ButtonRow(terminateButton, forceKillButton, closeButton);
        }
        else
        {
            var sigtermButton = new ButtonControl { Text = "SIGTERM", Width = UIConstants.SigtermButtonWidth };
            sigtermButton.Click += (_, _) => { TryTerminateProcess(liveProc.Pid, liveProc.Command); modal.Close(); };

            var sigkillButton = new ButtonControl { Text = "SIGKILL", Width = UIConstants.SigkillButtonWidth };
            sigkillButton.Click += (_, _) => { TryKillProcess(liveProc.Pid, liveProc.Command); modal.Close(); };

            closeButton = new ButtonControl { Text = "Close", Width = UIConstants.CloseButtonWidth };
            closeButton.Click += (_, _) => modal.Close();

            buttonRow = HorizontalGridControl.ButtonRow(sigtermButton, sigkillButton, closeButton);
        }

        modal.AddControl(buttonRow);
        closeButton.SetFocus(true, FocusReason.Programmatic);

        _windowSystem.AddWindow(modal);
        _windowSystem.SetActiveWindow(modal);
    }

    private void TryTerminateProcess(int pid, string command)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (!proc.CloseMainWindow())
            {
                proc.Kill();
                _windowSystem.NotificationStateService.ShowNotification(
                    $"✓ Force killed {pid}",
                    $"{command} (PID {pid}) had no main window, force terminated",
                    NotificationSeverity.Warning,
                    blockUi: false, timeout: UIConstants.NotificationTimeoutMediumMs,
                    parentWindow: FindMainWindow());
            }
            else
            {
                _windowSystem.NotificationStateService.ShowNotification(
                    $"✓ Terminated {pid}",
                    $"{command} (PID {pid}) gracefully terminated",
                    NotificationSeverity.Info,
                    blockUi: false, timeout: UIConstants.NotificationTimeoutShortMs,
                    parentWindow: FindMainWindow());
            }
        }
        catch (ArgumentException)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"Process {pid} no longer exists",
                $"{command} (PID {pid}) has already exited",
                NotificationSeverity.Info,
                blockUi: false, timeout: UIConstants.NotificationTimeoutShortMs,
                parentWindow: FindMainWindow());
        }
        catch (Exception ex)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"⚠ Terminate failed for {pid}", ex.Message,
                NotificationSeverity.Warning,
                blockUi: false, timeout: UIConstants.NotificationTimeoutLongMs,
                parentWindow: FindMainWindow());
        }
    }

    private void TryKillProcess(int pid, string command)
    {
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
                blockUi: false, timeout: UIConstants.NotificationTimeoutShortMs,
                parentWindow: FindMainWindow());
        }
        catch (ArgumentException)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"Process {pid} no longer exists",
                $"{command} (PID {pid}) has already exited",
                NotificationSeverity.Info,
                blockUi: false, timeout: UIConstants.NotificationTimeoutShortMs,
                parentWindow: FindMainWindow());
        }
        catch (Exception ex)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                $"⚠ Kill failed for {pid}", ex.Message,
                NotificationSeverity.Warning,
                blockUi: false, timeout: UIConstants.NotificationTimeoutLongMs,
                parentWindow: FindMainWindow());
        }
    }

    #endregion

    private Window? FindMainWindow() => _windowSystem.Windows.Values.FirstOrDefault();
}
