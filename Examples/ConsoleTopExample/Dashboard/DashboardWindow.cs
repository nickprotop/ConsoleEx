using ConsoleTopExample.Configuration;
using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using ConsoleTopExample.Tabs;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace ConsoleTopExample.Dashboard;

internal sealed class DashboardWindow
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly ISystemStatsProvider _stats;
    private readonly ConsoleTopConfig _config;

    private Window? _mainWindow;
    private readonly List<ITab> _tabs = new();
    private ITab? _activeTab;

    public DashboardWindow(
        ConsoleWindowSystem windowSystem,
        ISystemStatsProvider stats,
        ConsoleTopConfig config)
    {
        _windowSystem = windowSystem;
        _stats = stats;
        _config = config;
    }

    public void Create()
    {
        _mainWindow = new WindowBuilder(_windowSystem)
            .WithTitle("ConsoleTop - Live System Pulse")
            .WithColors(UIConstants.WindowBackground, UIConstants.WindowForeground)
            .Borderless()
            .Maximized()
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.F10 || e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    _windowSystem.Shutdown();
                    e.Handled = true;
                    return;
                }

                if (HandleTabShortcut(e.KeyInfo.Key))
                    e.Handled = true;
            })
            .Build();

        if (_mainWindow == null) return;
        var mainWindow = _mainWindow;

        BuildTopStatusBar(mainWindow);
        mainWindow.AddControl(Controls.RuleBuilder().StickyTop().WithColor(UIConstants.RuleColor).Build());

        BuildMetricsGrid(mainWindow);
        mainWindow.AddControl(Controls.RuleBuilder().WithColor(UIConstants.RuleColor).Build());

        CreateTabs();
        BuildTabToolbar(mainWindow);
        mainWindow.AddControl(Controls.RuleBuilder().WithColor(UIConstants.RuleColor).Build());

        var initialSnapshot = _stats.ReadSnapshot();
        BuildTabPanels(mainWindow, initialSnapshot);

        BuildBottomStatusBar(mainWindow);

        mainWindow.OnResize += (sender, e) =>
        {
            foreach (var tab in _tabs)
                tab.HandleResize(mainWindow.Width, mainWindow.Height);
        };

        _windowSystem.AddWindow(mainWindow);
    }

    #region Top Status Bar

    private void BuildTopStatusBar(Window mainWindow)
    {
        var systemInfo = SystemStatsFactory.GetDetailedSystemInfo();

        var topStatusBar = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(Controls.Markup($"[cyan1 bold]ConsoleTop[/] [grey70]• {systemInfo}[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .Build()))
            .Column(col =>
                col.Add(Controls.Markup("[grey70]--:--:--[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("topStatusClock")
                    .Build()))
            .Build();

        topStatusBar.BackgroundColor = UIConstants.StatusBarBackground;
        topStatusBar.ForegroundColor = UIConstants.StatusBarForeground;
        mainWindow.AddControl(topStatusBar);
    }

    #endregion

    #region Metrics Grid

    private void BuildMetricsGrid(Window mainWindow)
    {
        var metricsGrid = Controls.HorizontalGrid()
            .WithMargin(1, 1, 1, 1)
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]CPU Usage[/]").WithMargin(1, 1, 0, 0).Build())
                    .Add(BuildMetricsBar("cpuUserBar", "User", UIConstants.MetricsCpuLabelWidth, Color.Green, Color.Yellow, Color.Red))
                    .Add(BuildMetricsBar("cpuSystemBar", "System", UIConstants.MetricsCpuLabelWidth, Color.Cyan1, Color.Yellow, Color.Orange1))
                    .Add(BuildMetricsBarWithBottomMargin("cpuIoWaitBar", "IOwait", UIConstants.MetricsCpuLabelWidth, Color.Blue, Color.Cyan1, Color.Yellow)))
            .Column(col => col.Width(1))
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]Memory / IO[/]").WithMargin(1, 1, 1, 0).Build())
                    .Add(BuildMetricsBarMarginRight("memUsedBar", "Used %", UIConstants.MetricsMemLabelWidth, Color.Green, Color.Yellow, Color.Red))
                    .Add(BuildMetricsBarMarginRight("memCachedBar", "Cached %", UIConstants.MetricsMemLabelWidth, Color.Blue, Color.Cyan1, Color.Green))
                    .Add(BuildMetricsBarMarginRightBottom("memIoBar", "Disk/IO est %", UIConstants.MetricsMemLabelWidth, Color.Grey50, Color.Yellow, Color.Orange1)))
            .Column(col => col.Width(1))
            .Column(col =>
                col.Add(Controls.Markup("[grey70 bold]Network[/]").WithMargin(1, 1, 1, 0).Build())
                    .Add(BuildMetricsBarMarginRight("netUploadBar", "Upload", UIConstants.MetricsNetLabelWidth, "cool"))
                    .Add(BuildMetricsBarMarginRightBottom("netDownloadBar", "Download", UIConstants.MetricsNetLabelWidth, "warm")))
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();

        if (metricsGrid.Columns.Count >= 5)
        {
            metricsGrid.Columns[0].BackgroundColor = UIConstants.MetricsBoxBackground;
            metricsGrid.Columns[0].ForegroundColor = UIConstants.MetricsBoxForeground;
            metricsGrid.Columns[2].BackgroundColor = UIConstants.MetricsBoxBackground;
            metricsGrid.Columns[2].ForegroundColor = UIConstants.MetricsBoxForeground;
            metricsGrid.Columns[4].BackgroundColor = UIConstants.MetricsBoxBackground;
            metricsGrid.Columns[4].ForegroundColor = UIConstants.MetricsBoxForeground;
        }

        mainWindow.AddControl(metricsGrid);
    }

    private static BarGraphControl BuildMetricsBar(string name, string label, int labelWidth, Color c1, Color c2, Color c3)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(1, 0, 0, 0)
            .WithSmoothGradient(c1, c2, c3)
            .Build();
    }

    private static BarGraphControl BuildMetricsBarWithBottomMargin(string name, string label, int labelWidth, Color c1, Color c2, Color c3)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(1, 0, 0, 1)
            .WithSmoothGradient(c1, c2, c3)
            .Build();
    }

    private static BarGraphControl BuildMetricsBarMarginRight(string name, string label, int labelWidth, Color c1, Color c2, Color c3)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(1, 0, 1, 0)
            .WithSmoothGradient(c1, c2, c3)
            .Build();
    }

    private static BarGraphControl BuildMetricsBarMarginRightBottom(string name, string label, int labelWidth, Color c1, Color c2, Color c3)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(1, 0, 1, 1)
            .WithSmoothGradient(c1, c2, c3)
            .Build();
    }

    private static BarGraphControl BuildMetricsBarMarginRight(string name, string label, int labelWidth, string gradient)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(1, 0, 1, 0)
            .WithSmoothGradient(gradient)
            .Build();
    }

    private static BarGraphControl BuildMetricsBarMarginRightBottom(string name, string label, int labelWidth, string gradient)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(1, 0, 1, 1)
            .WithSmoothGradient(gradient)
            .Build();
    }

    #endregion

    #region Tabs

    private void CreateTabs()
    {
        if (_config.ShowProcessesTab)
            _tabs.Add(new ProcessTab(_windowSystem, _stats));
        if (_config.ShowMemoryTab)
            _tabs.Add(new MemoryTab(_windowSystem, _stats));
        if (_config.ShowCpuTab)
            _tabs.Add(new CpuTab(_windowSystem, _stats));
        if (_config.ShowNetworkTab)
            _tabs.Add(new NetworkTab(_windowSystem, _stats));
        if (_config.ShowStorageTab)
            _tabs.Add(new StorageTab(_windowSystem, _stats));

        _activeTab = _tabs.FirstOrDefault();
    }

    private void BuildTabToolbar(Window mainWindow)
    {
        var toolbarBuilder = Controls.Toolbar()
            .WithName("tabToolbar")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 1, 1, 1)
            .WithSpacing(1);

        for (int i = 0; i < _tabs.Count; i++)
        {
            var capturedTab = _tabs[i];
            int fKey = i + 1;
            toolbarBuilder = toolbarBuilder.AddButton(
                Controls.Button($"F{fKey} {capturedTab.Name}")
                    .WithName($"tab{capturedTab.Name}")
                    .OnClick((s, e) => SwitchTab(capturedTab))
            );
        }

        mainWindow.AddControl(toolbarBuilder.Build());
    }

    private void BuildTabPanels(Window mainWindow, SystemSnapshot initialSnapshot)
    {
        foreach (var tab in _tabs)
        {
            IWindowControl panel;

            if (tab is CpuTab cpuTab)
            {
                panel = BuildCpuPanel(cpuTab, initialSnapshot, mainWindow.Width);
            }
            else
            {
                panel = tab.BuildPanel(initialSnapshot, mainWindow.Width);
            }

            mainWindow.AddControl(panel);
        }

        // Apply process tab specific styling after panels are added
        if (_tabs.FirstOrDefault(t => t is ProcessTab) is ProcessTab processTab)
            processTab.ApplyDetailPanelColors(mainWindow);

        // Show initial tab
        ShowActiveTab();
    }

    private IWindowControl BuildCpuPanel(CpuTab cpuTab, SystemSnapshot snapshot, int windowWidth)
    {
        // CPU tab needs special handling: its left panel uses BarGraphControls
        // We need to build the panel manually to inject BuildLeftPanelContent
        var desiredLayout = windowWidth >= UIConstants.CpuLayoutThresholdWidth
            ? ResponsiveLayoutMode.Wide
            : ResponsiveLayoutMode.Narrow;

        if (desiredLayout == ResponsiveLayoutMode.Wide)
        {
            var grid = Controls.HorizontalGrid()
                .WithName(cpuTab.PanelControlName)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(1, 0, 1, 1)
                .Visible(false)
                .Column(col =>
                {
                    col.Width(UIConstants.FixedTextColumnWidth);
                    var leftPanel = BaseResponsiveTab.BuildScrollablePanel();
                    cpuTab.BuildLeftPanelContent(leftPanel, snapshot);
                    col.Add(leftPanel);
                })
                .Column(col =>
                {
                    col.Width(UIConstants.SeparatorColumnWidth);
                    col.Add(new SeparatorControl
                    {
                        ForegroundColor = UIConstants.SeparatorColor,
                        VerticalAlignment = VerticalAlignment.Fill
                    });
                })
                .Column(col =>
                {
                    var rightPanel = BaseResponsiveTab.BuildScrollablePanel();
                    cpuTab.BuildGraphsContentPublic(rightPanel, snapshot);
                    col.Add(rightPanel);
                })
                .Build();

            grid.BackgroundColor = UIConstants.WindowBackground;
            grid.ForegroundColor = UIConstants.WindowForeground;
            return grid;
        }
        else
        {
            var grid = Controls.HorizontalGrid()
                .WithName(cpuTab.PanelControlName)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(1, 0, 1, 1)
                .Visible(false)
                .Column(col =>
                {
                    var scrollPanel = BaseResponsiveTab.BuildScrollablePanel();
                    cpuTab.BuildLeftPanelContent(scrollPanel, snapshot);
                    BaseResponsiveTab.AddNarrowSeparator(scrollPanel);
                    cpuTab.BuildGraphsContentPublic(scrollPanel, snapshot);
                    col.Add(scrollPanel);
                })
                .Build();

            grid.BackgroundColor = UIConstants.WindowBackground;
            grid.ForegroundColor = UIConstants.WindowForeground;
            return grid;
        }
    }

    private bool HandleTabShortcut(ConsoleKey key)
    {
        int index = key switch
        {
            ConsoleKey.F1 => 0,
            ConsoleKey.F2 => 1,
            ConsoleKey.F3 => 2,
            ConsoleKey.F4 => 3,
            ConsoleKey.F5 => 4,
            _ => -1
        };

        if (index < 0 || index >= _tabs.Count)
            return false;

        SwitchTab(_tabs[index]);
        return true;
    }

    private void SwitchTab(ITab tab)
    {
        _activeTab = tab;
        ShowActiveTab();
        FocusTabButton(tab);
        _mainWindow?.Invalidate(true);
    }

    private void FocusTabButton(ITab tab)
    {
        if (_mainWindow == null) return;

        var btn = _mainWindow.FindControl<ButtonControl>($"tab{tab.Name}");
        btn?.SetFocus(true, FocusReason.Programmatic);
    }

    private void ShowActiveTab()
    {
        if (_mainWindow == null) return;

        foreach (var tab in _tabs)
        {
            var panel = _mainWindow.FindControl<HorizontalGridControl>(tab.PanelControlName);
            if (panel != null)
                panel.Visible = (tab == _activeTab);
        }

        // Trigger initial content update for active tab
        if (_activeTab is ProcessTab processTab)
            processTab.UpdateHighlightedProcess();
    }

    #endregion

    #region Bottom Status Bar

    private void BuildBottomStatusBar(Window mainWindow)
    {
        mainWindow.AddControl(Controls.RuleBuilder().StickyBottom().WithColor(UIConstants.RuleColor).Build());

        var bottomStatusBar = Controls.HorizontalGrid()
            .StickyBottom()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(Controls.Markup()
                    .AddLine("[grey70]F1-F5: Tabs • F10/ESC: Exit • Click: Select • Double-Click: Actions[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .Build()))
            .Column(col =>
                col.Add(Controls.Markup(
                        "[grey70]CPU [cyan1]0.0%[/] • MEM [cyan1]0.0%[/] • NET ↑[cyan1]0.0[/]/↓[cyan1]0.0[/] MB/s[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("statsLegend")
                    .Build()))
            .Build();

        bottomStatusBar.BackgroundColor = UIConstants.StatusBarBackground;
        bottomStatusBar.ForegroundColor = UIConstants.BottomBarForeground;
        mainWindow.AddControl(bottomStatusBar);
    }

    #endregion

    #region Update Loop

    private async Task UpdateLoopAsync(Window window, CancellationToken cancellationToken)
    {
        await PrimeStatsAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = _stats.ReadSnapshot();

                UpdateClock(window);
                UpdateMetricsBars(window, snapshot);
                UpdateActiveTab(snapshot);
                UpdateBottomStats(window, snapshot);
                UpdateActionButton(window, snapshot);
            }
            catch (Exception ex)
            {
                _windowSystem.LogService.LogError("Update loop error", ex, "ConsoleTop");
            }

            try
            {
                await Task.Delay(_config.RefreshIntervalMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task PrimeStatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _stats.ReadSnapshot();
            await Task.Delay(_config.PrimeDelayMs, cancellationToken);
        }
        catch
        {
            // ignore priming errors
        }
    }

    private static void UpdateClock(Window window)
    {
        var clock = window.FindControl<MarkupControl>("topStatusClock");
        if (clock != null)
        {
            var timeStr = DateTime.Now.ToString("HH:mm:ss");
            clock.SetContent(new List<string> { $"[grey70]{timeStr}[/]" });
        }
    }

    private static void UpdateMetricsBars(Window window, SystemSnapshot snapshot)
    {
        var cpuUserBar = window.FindControl<BarGraphControl>("cpuUserBar");
        if (cpuUserBar != null) cpuUserBar.Value = snapshot.Cpu.User;

        var cpuSystemBar = window.FindControl<BarGraphControl>("cpuSystemBar");
        if (cpuSystemBar != null) cpuSystemBar.Value = snapshot.Cpu.System;

        var cpuIoWaitBar = window.FindControl<BarGraphControl>("cpuIoWaitBar");
        if (cpuIoWaitBar != null) cpuIoWaitBar.Value = snapshot.Cpu.IoWait;

        var ioScaled = Math.Min(100, Math.Max(snapshot.Network.UpMbps, snapshot.Network.DownMbps));

        var memUsedBar = window.FindControl<BarGraphControl>("memUsedBar");
        if (memUsedBar != null) memUsedBar.Value = snapshot.Memory.UsedPercent;

        var memCachedBar = window.FindControl<BarGraphControl>("memCachedBar");
        if (memCachedBar != null) memCachedBar.Value = snapshot.Memory.CachedPercent;

        var memIoBar = window.FindControl<BarGraphControl>("memIoBar");
        if (memIoBar != null) memIoBar.Value = Math.Min(100, ioScaled);

        var netUploadBar = window.FindControl<BarGraphControl>("netUploadBar");
        if (netUploadBar != null) netUploadBar.Value = snapshot.Network.UpMbps;

        var netDownloadBar = window.FindControl<BarGraphControl>("netDownloadBar");
        if (netDownloadBar != null) netDownloadBar.Value = snapshot.Network.DownMbps;
    }

    private void UpdateActiveTab(SystemSnapshot snapshot)
    {
        _activeTab?.UpdatePanel(snapshot);
    }

    private static void UpdateBottomStats(Window window, SystemSnapshot snapshot)
    {
        var statsLegend = window.FindControl<MarkupControl>("statsLegend");
        if (statsLegend != null)
        {
            statsLegend.SetContent(new List<string>
            {
                $"[grey70]CPU [cyan1]{snapshot.Cpu.User:F1}%[/] • MEM [cyan1]{snapshot.Memory.UsedPercent:F1}%[/] • NET ↑[cyan1]{snapshot.Network.UpMbps:F1}[/]/↓[cyan1]{snapshot.Network.DownMbps:F1}[/] MB/s[/]"
            });
        }
    }

    private void UpdateActionButton(Window window, SystemSnapshot snapshot)
    {
        if (_activeTab is not ProcessTab) return;

        var processList = window.FindControl<ListControl>("processList");
        var actionButton = window.FindControl<ButtonControl>("actionButton");
        if (actionButton != null && processList != null)
        {
            actionButton.IsEnabled = processList.HighlightedIndex >= 0;
        }
    }

    #endregion
}
