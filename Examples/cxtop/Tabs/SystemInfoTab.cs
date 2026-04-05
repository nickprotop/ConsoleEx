using cxtop.Helpers;
using cxtop.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace cxtop.Tabs;

internal sealed class SystemInfoTab : ITab
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly ISystemStatsProvider _stats;
    private readonly SystemInfo _systemInfo;
    private readonly HistoryTracker _cpuHistory = new();
    private readonly HistoryTracker _memHistory = new();
    private readonly HistoryTracker _netDownHistory = new();

    private enum LayoutMode { WideTall, WideShort, Medium, Narrow }
    private LayoutMode _currentLayout;

    public string Name => "System";
    public string PanelControlName => "systemInfoPanel";

    public SystemInfoTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
    {
        _windowSystem = windowSystem;
        _stats = stats;
        _systemInfo = stats.ReadSystemInfo();
    }

    public IWindowControl BuildPanel(SystemSnapshot initialSnapshot, int windowWidth)
    {
        var window = _windowSystem.Windows.Values.FirstOrDefault();
        int windowHeight = window?.Height ?? 40;
        _currentLayout = ClassifyLayout(windowWidth, windowHeight);
        return BuildGrid(initialSnapshot);
    }

    public void UpdatePanel(SystemSnapshot snapshot)
    {
        var window = _windowSystem.Windows.Values.FirstOrDefault();
        if (window == null) return;

        _cpuHistory.Add(snapshot.Cpu.User + snapshot.Cpu.System + snapshot.Cpu.IoWait);
        _memHistory.Add(snapshot.Memory.UsedPercent);
        _netDownHistory.Add(snapshot.Network.DownMbps);

        UpdateSystemSection(window, snapshot);
        UpdateCpuSection(window, snapshot);
        UpdateMemorySection(window, snapshot);
        UpdateStorageSection(window, snapshot);
        UpdateNetworkSection(window, snapshot);

    }

    public void HandleResize(int newWidth, int newHeight)
    {
        var window = _windowSystem.Windows.Values.FirstOrDefault();
        var grid = window?.FindControl<HorizontalGridControl>(PanelControlName);
        if (grid == null || !grid.Visible) return;

        var desired = ClassifyLayout(newWidth, newHeight);
        if (desired == _currentLayout) return;

        _currentLayout = desired;
        RebuildGrid(grid);
    }

    // Estimated row counts per live section (label + separator + content + margins)
    private const int CpuSectionRows = 10;    // markup(3) + bar(1) + sparkline(3) + label/sep(2) + margin(1)
    private const int MemorySectionRows = 10;  // bar(1) + details(1) + swap bar(1) + sparkline(3) + label/sep(2) + margin(2)
    private const int StorageSectionRows = 7;  // bar(1) + details(3) + label/sep(2) + margin(1)
    private const int NetworkSectionRows = 10;  // details(2) + sparkline(5) + label/sep(2) + margin(1)
    private const int TopSpacerRows = 2;

    private static LayoutMode ClassifyLayout(int width, int height)
    {
        if (width >= UIConstants.SystemInfoWideThresholdWidth)
        {
            // Available height for live metrics column (subtract status bars, tab header, margins)
            int availableHeight = height - 5; // top bar + rule + tab header + bottom bar + rule
            int allStackedHeight = TopSpacerRows + CpuSectionRows + MemorySectionRows +
                                   StorageSectionRows + NetworkSectionRows;

            return availableHeight >= allStackedHeight ? LayoutMode.WideTall : LayoutMode.WideShort;
        }
        if (width >= UIConstants.SystemInfoMediumThresholdWidth) return LayoutMode.Medium;
        return LayoutMode.Narrow;
    }

    #region Grid Building

    private HorizontalGridControl BuildGrid(SystemSnapshot snapshot)
    {
        var builder = Controls.HorizontalGrid()
            .WithName(PanelControlName)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(0, 0, 0, 0)
            .Visible(false);

        switch (_currentLayout)
        {
            case LayoutMode.WideTall:
                builder = BuildWideTallColumns(builder, snapshot);
                break;
            case LayoutMode.WideShort:
                builder = BuildWideShortColumns(builder, snapshot);
                break;
            case LayoutMode.Medium:
                builder = BuildMediumColumns(builder, snapshot);
                break;
            case LayoutMode.Narrow:
                builder = BuildNarrowColumns(builder, snapshot);
                break;
        }

        var grid = builder.Build();
        grid.BackgroundColor = UIConstants.BaseBg;
        grid.ForegroundColor = UIConstants.PrimaryText;
        return grid;
    }

    private HorizontalGridBuilder BuildWideTallColumns(HorizontalGridBuilder builder, SystemSnapshot snapshot)
    {
        // 2 columns: static info | all live metrics stacked
        return builder
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsCpuBg);
                BuildBanner(panel);
                BuildSystemSection(panel, snapshot);
                col.Add(panel);
            })
            .Column(col => AddSeparatorColumn(col))
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsMemBg);
                AddTopSpacer(panel);
                BuildCpuSection(panel, snapshot);
                BuildMemorySection(panel, snapshot);
                BuildStorageSection(panel, snapshot);
                BuildNetworkSection(panel, snapshot);
                col.Add(panel);
            });
    }

    private HorizontalGridBuilder BuildWideShortColumns(HorizontalGridBuilder builder, SystemSnapshot snapshot)
    {
        // 3 columns: static info | CPU+Memory | Storage+Network
        return builder
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsCpuBg);
                BuildBanner(panel);
                BuildSystemSection(panel, snapshot);
                col.Add(panel);
            })
            .Column(col => AddSeparatorColumn(col))
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsMemBg);
                AddTopSpacer(panel);
                BuildCpuSection(panel, snapshot);
                BuildMemorySection(panel, snapshot);
                col.Add(panel);
            })
            .Column(col => AddSeparatorColumn(col))
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsNetBg);
                AddTopSpacer(panel);
                BuildStorageSection(panel, snapshot);
                BuildNetworkSection(panel, snapshot);
                col.Add(panel);
            });
    }

    private HorizontalGridBuilder BuildMediumColumns(HorizontalGridBuilder builder, SystemSnapshot snapshot)
    {
        return builder
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsCpuBg);
                BuildBanner(panel);
                BuildSystemSection(panel, snapshot);
                BuildCpuSection(panel, snapshot);
                col.Add(panel);
            })
            .Column(col => AddSeparatorColumn(col))
            .Column(col =>
            {
                col.Flex(1);
                var panel = BuildScrollablePanel(UIConstants.MetricsNetBg);
                AddTopSpacer(panel);
                BuildMemorySection(panel, snapshot);
                BuildStorageSection(panel, snapshot);
                BuildNetworkSection(panel, snapshot);

                col.Add(panel);
            });
    }

    private HorizontalGridBuilder BuildNarrowColumns(HorizontalGridBuilder builder, SystemSnapshot snapshot)
    {
        return builder
            .Column(col =>
            {
                var panel = BuildScrollablePanel();
                BuildBanner(panel);
                BuildSystemSection(panel, snapshot);
                BuildCpuSection(panel, snapshot);
                BuildMemorySection(panel, snapshot);
                BuildStorageSection(panel, snapshot);
                BuildNetworkSection(panel, snapshot);

                col.Add(panel);
            });
    }

    private void RebuildGrid(HorizontalGridControl grid)
    {
        for (int i = grid.Columns.Count - 1; i >= 0; i--)
            grid.RemoveColumn(grid.Columns[i]);

        var snapshot = _stats.ReadSnapshot();

        switch (_currentLayout)
        {
            case LayoutMode.WideTall:
                BuildWideTallColumnsRebuild(grid, snapshot);
                break;
            case LayoutMode.WideShort:
                BuildWideShortColumnsRebuild(grid, snapshot);
                break;
            case LayoutMode.Medium:
                BuildMediumColumnsRebuild(grid, snapshot);
                break;
            case LayoutMode.Narrow:
                BuildNarrowColumnsRebuild(grid, snapshot);
                break;
        }

        var window = _windowSystem.Windows.Values.FirstOrDefault();
        window?.ForceRebuildLayout();
        window?.Invalidate(true);
    }

    private void BuildWideTallColumnsRebuild(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var col1 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel1 = BuildScrollablePanel(UIConstants.MetricsCpuBg);
        BuildBanner(panel1);
        BuildSystemSection(panel1, snapshot);
        col1.AddContent(panel1);
        grid.AddColumn(col1);

        AddSeparatorColumnRebuild(grid);

        var col2 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel2 = BuildScrollablePanel(UIConstants.MetricsMemBg);
        AddTopSpacer(panel2);
        BuildCpuSection(panel2, snapshot);
        BuildMemorySection(panel2, snapshot);
        BuildStorageSection(panel2, snapshot);
        BuildNetworkSection(panel2, snapshot);
        col2.AddContent(panel2);
        grid.AddColumn(col2);
    }

    private void BuildWideShortColumnsRebuild(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var col1 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel1 = BuildScrollablePanel(UIConstants.MetricsCpuBg);
        BuildBanner(panel1);
        BuildSystemSection(panel1, snapshot);
        col1.AddContent(panel1);
        grid.AddColumn(col1);

        AddSeparatorColumnRebuild(grid);

        var col2 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel2 = BuildScrollablePanel(UIConstants.MetricsMemBg);
        AddTopSpacer(panel2);
        BuildCpuSection(panel2, snapshot);
        BuildMemorySection(panel2, snapshot);
        col2.AddContent(panel2);
        grid.AddColumn(col2);

        AddSeparatorColumnRebuild(grid);

        var col3 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel3 = BuildScrollablePanel(UIConstants.MetricsNetBg);
        AddTopSpacer(panel3);
        BuildStorageSection(panel3, snapshot);
        BuildNetworkSection(panel3, snapshot);
        col3.AddContent(panel3);
        grid.AddColumn(col3);
    }

    private void BuildMediumColumnsRebuild(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var col1 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel1 = BuildScrollablePanel(UIConstants.MetricsCpuBg);
        BuildBanner(panel1);
        BuildSystemSection(panel1, snapshot);
        BuildCpuSection(panel1, snapshot);
        col1.AddContent(panel1);
        grid.AddColumn(col1);

        AddSeparatorColumnRebuild(grid);

        var col2 = new ColumnContainer(grid) { FlexFactor = 1 };
        var panel2 = BuildScrollablePanel(UIConstants.MetricsNetBg);
        AddTopSpacer(panel2);
        BuildMemorySection(panel2, snapshot);
        BuildStorageSection(panel2, snapshot);
        BuildNetworkSection(panel2, snapshot);
        col2.AddContent(panel2);
        grid.AddColumn(col2);
    }

    private void BuildNarrowColumnsRebuild(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var col = new ColumnContainer(grid);
        var panel = BuildScrollablePanel();
        BuildBanner(panel);
        BuildSystemSection(panel, snapshot);
        BuildCpuSection(panel, snapshot);
        BuildMemorySection(panel, snapshot);
        BuildStorageSection(panel, snapshot);
        BuildNetworkSection(panel, snapshot);
        col.AddContent(panel);
        grid.AddColumn(col);
    }

    #endregion

    #region Section Builders

    private void BuildBanner(ScrollablePanelControl panel)
    {
        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();
        panel.AddControl(
            Controls.Markup()
                .AddLine($"  [{accent} bold]{_systemInfo.Hostname}[/] [{muted}]— {_systemInfo.OsDescription} ({_systemInfo.CpuArchitecture})[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 0)
                .Build()
        );
    }

    private void BuildSystemSection(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        AddSectionLabel(panel, "System");

        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();
        panel.AddControl(
            Controls.Markup()
                .WithName("sysInfoSystemMarkup")
                .AddLine(BuildSystemMarkup(muted, accent, FormatUptime(), snapshot))
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );
    }

    private string BuildSystemMarkup(string muted, string accent, string uptime, SystemSnapshot? snapshot = null)
    {
        var sb = $"[{muted}]OS[/]       [{accent}]{_systemInfo.OsDescription}[/]\n" +
                 $"[{muted}]Kernel[/]   [{accent}]{_systemInfo.KernelVersion}[/]\n" +
                 $"[{muted}]Host[/]     [{accent}]{_systemInfo.Hostname}[/]\n" +
                 $"[{muted}]Vendor[/]   [{accent}]{_systemInfo.MachineVendor}[/]\n" +
                 $"[{muted}]Board[/]    [{accent}]{_systemInfo.MotherboardModel}[/]\n" +
                 $"[{muted}]BIOS[/]     [{accent}]{_systemInfo.BiosVersion}[/]\n" +
                 $"[{muted}]RAM[/]      [{accent}]{_systemInfo.TotalRamGb:F1} GB[/]\n" +
                 $"[{muted}]GPU[/]      [{accent}]{_systemInfo.GpuName}[/]\n" +
                 $"[{muted}]Audio[/]    [{accent}]{_systemInfo.AudioDevice}[/]\n" +
                 $"[{muted}]Display[/]  [{accent}]{_systemInfo.DisplayOutput}[/]\n" +
                 $"[{muted}]USB[/]      [{accent}]{_systemInfo.UsbDeviceCount} devices[/]";

        if (!string.IsNullOrEmpty(_systemInfo.Resolution))
            sb += $"\n[{muted}]Res[/]      [{accent}]{_systemInfo.Resolution}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.DesktopEnvironment))
            sb += $"\n[{muted}]DE[/]       [{accent}]{_systemInfo.DesktopEnvironment}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.WindowManager))
            sb += $"\n[{muted}]WM[/]       [{accent}]{_systemInfo.WindowManager}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.Theme))
            sb += $"\n[{muted}]Theme[/]    [{accent}]{_systemInfo.Theme}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.Icons))
            sb += $"\n[{muted}]Icons[/]    [{accent}]{_systemInfo.Icons}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.Terminal))
            sb += $"\n[{muted}]Term[/]     [{accent}]{_systemInfo.Terminal}[/]";

        sb += $"\n[{muted}]Shell[/]    [{accent}]{_systemInfo.Shell}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.Locale))
            sb += $"\n[{muted}]Locale[/]   [{accent}]{_systemInfo.Locale}[/]";

        if (_systemInfo.PackageCount > 0)
            sb += $"\n[{muted}]Pkgs[/]     [{accent}]{_systemInfo.PackageManagers}[/]";

        sb += $"\n[{muted}]Uptime[/]   [{accent}]{uptime}[/]";

        if (!string.IsNullOrEmpty(_systemInfo.BatteryStatus))
            sb += $"\n[{muted}]Battery[/]  [{accent}]{_systemInfo.BatteryStatus}[/]";

        sb += $"\n[{muted}]Runtime[/]  [{accent}]{_systemInfo.DotNetRuntime}[/]";

        if (snapshot?.LoadAvg != null)
        {
            var la = snapshot.LoadAvg;
            sb += $"\n[{muted}]Load[/]     [{accent}]{la.Load1:F2}[/] [{muted}]/[/] [{accent}]{la.Load5:F2}[/] [{muted}]/[/] [{accent}]{la.Load15:F2}[/]";
        }

        if (snapshot != null)
            sb += $"\n[{muted}]Procs[/]    [{accent}]{snapshot.Processes.Count}[/]";

        return sb;
    }

    private void BuildCpuSection(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        AddSectionLabel(panel, "Processor");

        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();
        double totalCpu = snapshot.Cpu.User + snapshot.Cpu.System + snapshot.Cpu.IoWait;

        panel.AddControl(
            Controls.Markup()
                .AddLine($"[{muted}]Model[/]  [{accent}]{_systemInfo.CpuModelName}[/]")
                .AddLine($"[{muted}]Arch[/]   [{accent}]{_systemInfo.CpuArchitecture}[/]")
                .AddLine($"[{muted}]Cores[/]  [{accent}]{_systemInfo.LogicalCoreCount}[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("sysInfoCpuBar")
                .WithLabel("Usage")
                .WithLabelWidth(UIConstants.SystemInfoBarLabelWidth)
                .WithValue(totalCpu)
                .WithMaxValue(100)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue().WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("sysInfoCpuSparkline")
                .WithTitle("CPU %")
                .WithTitleColor(UIConstants.Accent)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SystemInfoSparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkCpuTotal)
                .WithBackgroundColor(Color.Transparent)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(2, 0, 2, 1)
                .WithData(_cpuHistory.DataMutable)
                .Build()
        );
    }

    private void BuildMemorySection(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        AddSectionLabel(panel, "Memory");

        var mem = snapshot.Memory;
        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("sysInfoMemUsedBar")
                .WithLabel("Used")
                .WithLabelWidth(UIConstants.SystemInfoBarLabelWidth)
                .WithValue(mem.UsedPercent)
                .WithMaxValue(100)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue().WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );

        panel.AddControl(
            Controls.Markup()
                .WithName("sysInfoMemDetails")
                .AddLine(BuildMemoryDetailsMarkup(mem, muted, accent))
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        if (mem.SwapTotalMb > 0)
        {
            double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
            panel.AddControl(
                new BarGraphBuilder()
                    .WithName("sysInfoSwapBar")
                    .WithLabel("Swap")
                    .WithLabelWidth(UIConstants.SystemInfoBarLabelWidth)
                    .WithValue(swapPercent)
                    .WithMaxValue(100)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .WithUnfilledColor(UIConstants.BarUnfilledColor)
                    .ShowLabel().ShowValue().WithValueFormat("F1")
                    .WithMargin(2, 0, 2, 0)
                    .WithSmoothGradient(UIConstants.SparkMemCached)
                    .Build()
            );
        }

        panel.AddControl(
            new SparklineBuilder()
                .WithName("sysInfoMemSparkline")
                .WithTitle("Mem %")
                .WithTitleColor(UIConstants.Accent)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SystemInfoSparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkMemUsed)
                .WithBackgroundColor(Color.Transparent)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(2, 0, 2, 1)
                .WithData(_memHistory.DataMutable)
                .Build()
        );
    }

    private static string BuildMemoryDetailsMarkup(MemorySample mem, string muted, string accent)
    {
        return $"[{muted}]Total[/] [{accent}]{mem.TotalMb:F0} MB[/]  " +
               $"[{muted}]Used[/] [{accent}]{mem.UsedMb:F0} MB[/]  " +
               $"[{muted}]Avail[/] [{accent}]{mem.AvailableMb:F0} MB[/]";
    }

    private void BuildStorageSection(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        AddSectionLabel(panel, "Storage");

        var stor = snapshot.Storage;
        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("sysInfoStorageBar")
                .WithLabel("Used")
                .WithLabelWidth(UIConstants.SystemInfoBarLabelWidth)
                .WithValue(stor.TotalUsedPercent)
                .WithMaxValue(100)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue().WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );

        panel.AddControl(
            Controls.Markup()
                .WithName("sysInfoStorageDetails")
                .AddLine(BuildStorageDetailsMarkup(stor, muted, accent))
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );
    }

    private static string BuildStorageDetailsMarkup(StorageSample stor, string muted, string accent)
    {
        return $"[{muted}]Total[/] [{accent}]{stor.TotalCapacityGb:F1} GB[/]  " +
               $"[{muted}]Used[/] [{accent}]{stor.TotalUsedGb:F1} GB[/]  " +
               $"[{muted}]Free[/] [{accent}]{stor.TotalFreeGb:F1} GB[/]\n" +
               $"[{muted}]Read[/]  [{accent}]{stor.TotalReadMbps:F2} MB/s[/]  " +
               $"[{muted}]Write[/] [{accent}]{stor.TotalWriteMbps:F2} MB/s[/]\n" +
               $"[{muted}]Disks[/] [{accent}]{stor.Disks.Count}[/]";
    }

    private void BuildNetworkSection(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        AddSectionLabel(panel, "Network");

        var net = snapshot.Network;
        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();

        panel.AddControl(
            Controls.Markup()
                .WithName("sysInfoNetDetails")
                .AddLine(BuildNetworkDetailsMarkup(net, muted, accent))
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("sysInfoNetSparkline")
                .WithTitle("Download")
                .WithTitleColor(UIConstants.AccentSecondary)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(5)
                .WithGradient(UIConstants.SparkNetDownload)
                .WithBackgroundColor(Color.Transparent)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(2, 0, 2, 1)
                .WithData(_netDownHistory.DataMutable)
                .Build()
        );
    }

    private static string BuildNetworkDetailsMarkup(NetworkSample net, string muted, string accent)
    {
        var ifaceCount = net.PerInterfaceSamples?.Count ?? 0;
        return $"[{muted}]Upload[/]   [{accent}]{net.UpMbps:F2} MB/s[/]   " +
               $"[{muted}]Download[/] [{accent}]{net.DownMbps:F2} MB/s[/]\n" +
               $"[{muted}]Ifaces[/]   [{accent}]{ifaceCount}[/]";
    }

    #endregion

    #region Update Methods

    private void UpdateSystemSection(Window window, SystemSnapshot snapshot)
    {
        var markup = window.FindControl<MarkupControl>("sysInfoSystemMarkup");
        if (markup == null) return;

        var muted = UIConstants.MutedText.ToMarkup();
        var accent = UIConstants.Accent.ToMarkup();
        markup.SetContent(new List<string> { BuildSystemMarkup(muted, accent, FormatUptime(), snapshot) });
    }

    private void UpdateCpuSection(Window window, SystemSnapshot snapshot)
    {
        double totalCpu = snapshot.Cpu.User + snapshot.Cpu.System + snapshot.Cpu.IoWait;

        var bar = window.FindControl<BarGraphControl>("sysInfoCpuBar");
        if (bar != null) bar.Value = totalCpu;

        var sparkline = window.FindControl<SparklineControl>("sysInfoCpuSparkline");
        sparkline?.SetDataPoints(_cpuHistory.DataMutable);
    }

    private void UpdateMemorySection(Window window, SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;

        var usedBar = window.FindControl<BarGraphControl>("sysInfoMemUsedBar");
        if (usedBar != null) usedBar.Value = mem.UsedPercent;

        var details = window.FindControl<MarkupControl>("sysInfoMemDetails");
        if (details != null)
        {
            var muted = UIConstants.MutedText.ToMarkup();
            var accent = UIConstants.Accent.ToMarkup();
            details.SetContent(new List<string> { BuildMemoryDetailsMarkup(mem, muted, accent) });
        }

        if (mem.SwapTotalMb > 0)
        {
            double swapPercent = mem.SwapUsedMb / mem.SwapTotalMb * 100;
            var swapBar = window.FindControl<BarGraphControl>("sysInfoSwapBar");
            if (swapBar != null) swapBar.Value = swapPercent;
        }

        var memSparkline = window.FindControl<SparklineControl>("sysInfoMemSparkline");
        memSparkline?.SetDataPoints(_memHistory.DataMutable);
    }

    private void UpdateStorageSection(Window window, SystemSnapshot snapshot)
    {
        var stor = snapshot.Storage;

        var bar = window.FindControl<BarGraphControl>("sysInfoStorageBar");
        if (bar != null) bar.Value = stor.TotalUsedPercent;

        var details = window.FindControl<MarkupControl>("sysInfoStorageDetails");
        if (details != null)
        {
            var muted = UIConstants.MutedText.ToMarkup();
            var accent = UIConstants.Accent.ToMarkup();
            details.SetContent(new List<string> { BuildStorageDetailsMarkup(stor, muted, accent) });
        }
    }

    private void UpdateNetworkSection(Window window, SystemSnapshot snapshot)
    {
        var net = snapshot.Network;

        var details = window.FindControl<MarkupControl>("sysInfoNetDetails");
        if (details != null)
        {
            var muted = UIConstants.MutedText.ToMarkup();
            var accent = UIConstants.Accent.ToMarkup();
            details.SetContent(new List<string> { BuildNetworkDetailsMarkup(net, muted, accent) });
        }

        var sparkline = window.FindControl<SparklineControl>("sysInfoNetSparkline");
        sparkline?.SetDataPoints(_netDownHistory.DataMutable);
    }

    #endregion

    #region Helpers

    private static void AddSeparatorColumn(ColumnBuilder col)
    {
        col.Width(UIConstants.SeparatorColumnWidth);
        col.Add(new SeparatorControl
        {
            ForegroundColor = UIConstants.SeparatorColor,
            VerticalAlignment = VerticalAlignment.Fill
        });
    }

    private static void AddSeparatorColumnRebuild(HorizontalGridControl grid)
    {
        var sepCol = new ColumnContainer(grid) { Width = UIConstants.SeparatorColumnWidth };
        sepCol.AddContent(new SeparatorControl
        {
            ForegroundColor = UIConstants.SeparatorColor,
            VerticalAlignment = VerticalAlignment.Fill
        });
        grid.AddColumn(sepCol);
    }

    private static void AddTopSpacer(ScrollablePanelControl panel)
    {
        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .WithMargin(0, 0, 0, 0)
                .Build()
        );
    }

    private static void AddSectionLabel(ScrollablePanelControl panel, string title)
    {
        panel.AddControl(
            Controls.Markup()
                .AddLine($"[{UIConstants.Accent.ToMarkup()}]{title}[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(1, 1, 1, 0)
                .Build()
        );
        panel.AddControl(
            Controls.RuleBuilder()
                .WithColor(UIConstants.SeparatorColor)
                .Build()
        );
    }

    private string FormatUptime()
    {
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (elapsed.Days > 0)
            return $"{elapsed.Days}d {elapsed.Hours}h {elapsed.Minutes}m";
        if (elapsed.Hours > 0)
            return $"{elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s";
        return $"{elapsed.Minutes}m {elapsed.Seconds}s";
    }

    private static ScrollablePanelControl BuildScrollablePanel(Color? bg = null)
    {
        var panel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        panel.BackgroundColor = bg ?? UIConstants.BaseBg;
        panel.ForegroundColor = UIConstants.PrimaryText;
        return panel;
    }

    #endregion
}
