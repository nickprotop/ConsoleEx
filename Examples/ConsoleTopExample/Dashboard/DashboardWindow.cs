using ConsoleTopExample.Configuration;
using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using ConsoleTopExample.Tabs;
using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace ConsoleTopExample.Dashboard;

internal sealed class DashboardWindow
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly ISystemStatsProvider _stats;
    private readonly ConsoleTopConfig _config;

    private Window? _mainWindow;
    private readonly List<ITab> _tabs = new();
    private TabControl? _tabControl;
    private readonly Dictionary<string, double> _lastBarValues = new();
    private SharpConsoleUI.Windows.WindowRenderer.BufferPaintDelegate? _tabFadeHandler;

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
            .WithColors(UIConstants.PrimaryText, UIConstants.BaseBg)
            .WithBackgroundGradient(
                ColorGradient.FromColors(UIConstants.BaseBg, UIConstants.BaseEnd),
                GradientDirection.DiagonalDown)
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
                if (e.KeyInfo.Key == ConsoleKey.S &&
                    _tabs.ElementAtOrDefault(_tabControl?.ActiveTabIndex ?? -1) is ProcessTab pt)
                {
                    pt.CycleSortMode();
                    e.Handled = true;
                }
            })
            .Build();

        if (_mainWindow == null) return;
        var mainWindow = _mainWindow;

        BuildTopStatusBar(mainWindow);
        mainWindow.AddControl(Controls.RuleBuilder().StickyTop().WithColor(UIConstants.SeparatorColor).Build());

        BuildMetricsGrid(mainWindow);
        mainWindow.AddControl(Controls.RuleBuilder().WithColor(UIConstants.SeparatorColor).Build());

        CreateTabs();
        var initialSnapshot = _stats.ReadSnapshot();
        BuildTabSection(mainWindow, initialSnapshot);

        BuildBottomStatusBar(mainWindow);

        mainWindow.OnResize += (sender, e) =>
        {
            foreach (var tab in _tabs)
                tab.HandleResize(mainWindow.Width, mainWindow.Height);
        };

        _windowSystem.AddWindow(mainWindow);

        WindowAnimations.FadeIn(mainWindow,
            duration: TimeSpan.FromMilliseconds(UIConstants.FadeInMs),
            fadeColor: Color.Black,
            easing: EasingFunctions.EaseInOut);
    }

    #region Top Status Bar

    private void BuildTopStatusBar(Window mainWindow)
    {
        var systemInfo = SystemStatsFactory.GetDetailedSystemInfo();

        var topStatusBar = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(Controls.Markup($"[{UIConstants.Accent.ToMarkup()} bold]ConsoleTop[/] [{UIConstants.MutedText.ToMarkup()}]• {systemInfo}[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .Build()))
            .Column(col =>
                col.Add(Controls.Markup($"[{UIConstants.MutedText.ToMarkup()}]--:--:--[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("topStatusClock")
                    .Build()))
            .Build();

        topStatusBar.BackgroundColor = UIConstants.HeaderBg;
        topStatusBar.ForegroundColor = UIConstants.PrimaryText;
        mainWindow.AddControl(topStatusBar);
    }

    #endregion

    #region Metrics Grid

    private static BarGraphControl BuildMetricsBar(string name, string label, int labelWidth, Color[] gradient, int marginLeft = 1, int marginRight = 0, int marginBottom = 0)
    {
        return new BarGraphBuilder()
            .WithName(name).WithLabel(label).WithLabelWidth(labelWidth)
            .WithValue(0).WithMaxValue(100).WithBarWidth(UIConstants.MetricsBarWidth)
            .WithUnfilledColor(UIConstants.BarUnfilledColor)
            .ShowLabel().ShowValue().WithValueFormat("F1")
            .WithMargin(marginLeft, 0, marginRight, marginBottom)
            .WithSmoothGradient(gradient)
            .Build();
    }

    private void BuildMetricsGrid(Window mainWindow)
    {
        var metricsGrid = Controls.HorizontalGrid()
            .WithMargin(1, 1, 1, 1)
            .Column(col =>
                col.Add(Controls.Markup($"[{UIConstants.MutedText.ToMarkup()} bold]CPU Usage[/]").WithMargin(1, 1, 0, 0).Build())
                    .Add(BuildMetricsBar("cpuUserBar", "User", UIConstants.MetricsCpuLabelWidth, UIConstants.GradientHealthy))
                    .Add(BuildMetricsBar("cpuSystemBar", "System", UIConstants.MetricsCpuLabelWidth, UIConstants.SparkCpuSystem))
                    .Add(BuildMetricsBar("cpuIoWaitBar", "IOwait", UIConstants.MetricsCpuLabelWidth, UIConstants.GradientIoRead, marginBottom: 1)))
            .Column(col => col.Width(1))
            .Column(col =>
                col.Add(Controls.Markup($"[{UIConstants.MutedText.ToMarkup()} bold]Memory / IO[/]").WithMargin(1, 1, 1, 0).Build())
                    .Add(BuildMetricsBar("memUsedBar", "Used %", UIConstants.MetricsMemLabelWidth, UIConstants.GradientHealthy, marginRight: 1))
                    .Add(BuildMetricsBar("memCachedBar", "Cached %", UIConstants.MetricsMemLabelWidth, UIConstants.SparkMemCached, marginRight: 1))
                    .Add(BuildMetricsBar("memIoBar", "Disk/IO est %", UIConstants.MetricsMemLabelWidth, UIConstants.GradientIoWrite, marginRight: 1, marginBottom: 1)))
            .Column(col => col.Width(1))
            .Column(col =>
                col.Add(Controls.Markup($"[{UIConstants.MutedText.ToMarkup()} bold]Network[/]").WithMargin(1, 1, 1, 0).Build())
                    .Add(BuildMetricsBar("netUploadBar", "Upload", UIConstants.MetricsNetLabelWidth, UIConstants.GradientNetUpload, marginRight: 1))
                    .Add(BuildMetricsBar("netDownloadBar", "Download", UIConstants.MetricsNetLabelWidth, UIConstants.GradientNetDownload, marginRight: 1, marginBottom: 1)))
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();

        if (metricsGrid.Columns.Count >= 5)
        {
            metricsGrid.Columns[0].BackgroundColor = UIConstants.PanelBg;
            metricsGrid.Columns[0].ForegroundColor = UIConstants.PrimaryText;
            metricsGrid.Columns[2].BackgroundColor = UIConstants.PanelBg;
            metricsGrid.Columns[2].ForegroundColor = UIConstants.PrimaryText;
            metricsGrid.Columns[4].BackgroundColor = UIConstants.PanelBg;
            metricsGrid.Columns[4].ForegroundColor = UIConstants.PrimaryText;
        }

        mainWindow.AddControl(metricsGrid);
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
    }

    private void BuildTabSection(Window mainWindow, SystemSnapshot initialSnapshot)
    {
        var builder = new TabControlBuilder()
            .WithHeaderStyle(TabHeaderStyle.AccentedSeparator)
            .Fill()
            .WithAlignment(HorizontalAlignment.Stretch);

        foreach (var tab in _tabs)
            builder = builder.AddTab(tab.Name, tab.BuildPanel(initialSnapshot, mainWindow.Width));

        _tabControl = builder.Build();
        _tabControl.BackgroundColor = UIConstants.BaseBg;
        _tabControl.TabChanged += (sender, e) =>
        {
            _windowSystem.Animations.Animate(
                1.0f, 0.0f,
                TimeSpan.FromMilliseconds(UIConstants.TabCrossfadeMs),
                easing: EasingFunctions.EaseInOut,
                onUpdate: intensity =>
                {
                    mainWindow.PostBufferPaint -= _tabFadeHandler;
                    _tabFadeHandler = (buffer, dirty, clip) =>
                    {
                        if (_tabControl is { ActualWidth: > 0 })
                        {
                            var rect = new LayoutRect(_tabControl.ActualX, _tabControl.ActualY, _tabControl.ActualWidth, _tabControl.ActualHeight);
                            ColorBlendHelper.ApplyColorOverlay(buffer, Color.Black, intensity, 0.5f, rect);
                        }
                    };
                    mainWindow.PostBufferPaint += _tabFadeHandler;
                },
                onComplete: () =>
                {
                    mainWindow.PostBufferPaint -= _tabFadeHandler;
                    _tabFadeHandler = null;
                });
        };
        mainWindow.AddControl(_tabControl);

        if (_tabs.FirstOrDefault(t => t is ProcessTab) is ProcessTab processTab)
            processTab.ApplyDetailPanelColors(mainWindow);
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

        if (_tabControl != null)
            _tabControl.ActiveTabIndex = index;
        return true;
    }

    #endregion

    #region Bottom Status Bar

    private void BuildBottomStatusBar(Window mainWindow)
    {
        mainWindow.AddControl(Controls.RuleBuilder().StickyBottom().WithColor(UIConstants.SeparatorColor).Build());

        var bottomStatusBar = Controls.HorizontalGrid()
            .StickyBottom()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col =>
                col.Add(Controls.Markup()
                    .AddLine(GetContextualKeybinds())
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .WithName("actionBarKeybinds")
                    .Build()))
            .Column(col =>
                col.Add(Controls.Markup(
                        $"[{UIConstants.MutedText.ToMarkup()}]CPU [{UIConstants.Accent.ToMarkup()}]0.0%[/] • MEM [{UIConstants.Accent.ToMarkup()}]0.0%[/] • NET ↑[{UIConstants.Accent.ToMarkup()}]0.0[/]/↓[{UIConstants.Accent.ToMarkup()}]0.0[/] MB/s[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("statsLegend")
                    .Build()))
            .Build();

        bottomStatusBar.BackgroundColor = UIConstants.HeaderBg;
        bottomStatusBar.ForegroundColor = UIConstants.MutedText;
        mainWindow.AddControl(bottomStatusBar);
    }

    private string GetContextualKeybinds()
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        int activeTab = _tabControl?.ActiveTabIndex ?? 0;
        var tab = _tabs.ElementAtOrDefault(activeTab);

        if (tab is ProcessTab)
            return $"[{accent}]Tab[/][{muted}] region[/] [{accent}]Enter[/][{muted}] select[/] [{accent}]S[/][{muted}] sort[/] [{accent}]/[/][{muted}] search[/] [{accent}]F1-F5[/][{muted}] tabs[/] [{accent}]F10[/][{muted}] exit[/]";

        return $"[{accent}]F1-F5[/][{muted}] tabs[/] [{accent}]F10/ESC[/][{muted}] exit[/]";
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
            clock.SetContent(new List<string> { $"[{UIConstants.MutedText.ToMarkup()}]{timeStr}[/]" });
        }
    }

    private void UpdateMetricsBars(Window window, SystemSnapshot snapshot)
    {
        AnimateBar(window, "cpuUserBar", snapshot.Cpu.User);
        AnimateBar(window, "cpuSystemBar", snapshot.Cpu.System);
        AnimateBar(window, "cpuIoWaitBar", snapshot.Cpu.IoWait);

        var ioScaled = Math.Min(100, Math.Max(snapshot.Network.UpMbps, snapshot.Network.DownMbps));
        AnimateBar(window, "memUsedBar", snapshot.Memory.UsedPercent);
        AnimateBar(window, "memCachedBar", snapshot.Memory.CachedPercent);
        AnimateBar(window, "memIoBar", Math.Min(100, ioScaled));

        AnimateBar(window, "netUploadBar", snapshot.Network.UpMbps);
        AnimateBar(window, "netDownloadBar", snapshot.Network.DownMbps);
    }

    private void AnimateBar(Window window, string name, double newValue)
    {
        var bar = window.FindControl<BarGraphControl>(name);
        if (bar == null) return;

        _lastBarValues.TryGetValue(name, out var oldValue);
        _lastBarValues[name] = newValue;

        if (Math.Abs(oldValue - newValue) < 0.1)
        {
            bar.Value = newValue;
            return;
        }

        _windowSystem.Animations.Animate(
            (float)oldValue, (float)newValue,
            TimeSpan.FromMilliseconds(UIConstants.BarAnimationMs),
            easing: EasingFunctions.EaseOut,
            onUpdate: v => bar.Value = v);
    }

    private void UpdateActiveTab(SystemSnapshot snapshot)
    {
        if (_tabControl == null) return;
        var i = _tabControl.ActiveTabIndex;
        if (i >= 0 && i < _tabs.Count)
            _tabs[i].UpdatePanel(snapshot);
    }

    private void UpdateBottomStats(Window window, SystemSnapshot snapshot)
    {
        var statsLegend = window.FindControl<MarkupControl>("statsLegend");
        if (statsLegend != null)
        {
            var totalCpu = snapshot.Cpu.User + snapshot.Cpu.System;
            var cpuColor = UIConstants.ThresholdColor(totalCpu);
            var memColor = UIConstants.ThresholdColor(snapshot.Memory.UsedPercent);
            var muted = UIConstants.MutedText.ToMarkup();
            var accent = UIConstants.Accent.ToMarkup();
            statsLegend.SetContent(new List<string>
            {
                $"[{muted}]CPU [{cpuColor}]{totalCpu:F1}%[/] • MEM [{memColor}]{snapshot.Memory.UsedPercent:F1}%[/] • NET ↑[{accent}]{snapshot.Network.UpMbps:F1}[/]/↓[{accent}]{snapshot.Network.DownMbps:F1}[/] MB/s[/]"
            });
        }

        var keybinds = window.FindControl<MarkupControl>("actionBarKeybinds");
        keybinds?.SetContent(new List<string> { GetContextualKeybinds() });
    }

    private void UpdateActionButton(Window window, SystemSnapshot snapshot)
    {
        if (_tabControl == null) return;
        if (_tabs.ElementAtOrDefault(_tabControl.ActiveTabIndex) is not ProcessTab) return;

        var processList = window.FindControl<ListControl>("processList");
        var actionButton = window.FindControl<ButtonControl>("actionButton");
        if (actionButton != null && processList != null)
        {
            actionButton.IsEnabled = processList.SelectedIndex >= 0;
        }
    }

    #endregion
}
