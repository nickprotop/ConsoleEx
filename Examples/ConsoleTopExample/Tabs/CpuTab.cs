using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ConsoleTopExample.Tabs;

internal sealed class CpuTab : BaseResponsiveTab
{
    private readonly HistoryTracker _userHistory = new();
    private readonly HistoryTracker _systemHistory = new();
    private readonly HistoryTracker _ioWaitHistory = new();
    private readonly HistoryTracker _totalHistory = new();
    private readonly KeyedHistoryTracker<int> _perCoreHistory = new();

    public CpuTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
        : base(windowSystem, stats) { }

    public override string Name => "CPU";
    public override string PanelControlName => "cpuPanel";
    protected override int LayoutThresholdWidth => UIConstants.CpuLayoutThresholdWidth;

    protected override List<string> BuildTextContent(SystemSnapshot snapshot)
    {
        // CPU tab uses left-panel controls (BarGraphControls), not plain text.
        // Return empty list; left panel content is built via BuildLeftPanelContent.
        return new List<string>();
    }

    public override IWindowControl BuildPanel(SystemSnapshot initialSnapshot, int windowWidth)
    {
        _currentLayout = windowWidth >= UIConstants.CpuLayoutThresholdWidth
            ? ResponsiveLayoutMode.Wide
            : ResponsiveLayoutMode.Narrow;

        if (_currentLayout == ResponsiveLayoutMode.Wide)
        {
            var grid = Controls.HorizontalGrid()
                .WithName(PanelControlName)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(1, 0, 1, 1)
                .Visible(false)
                .Column(col =>
                {
                    col.Width(UIConstants.FixedTextColumnWidth);
                    var leftPanel = BuildScrollablePanel();
                    BuildLeftPanelContent(leftPanel, initialSnapshot);
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
                    var rightPanel = BuildRightPanel();
                    BuildGraphsContentPublic(rightPanel, initialSnapshot);
                    col.Add(rightPanel);
                })
                .Build();

            grid.BackgroundColor = UIConstants.BaseBg;
            grid.ForegroundColor = UIConstants.PrimaryText;
            return grid;
        }
        else
        {
            var grid = Controls.HorizontalGrid()
                .WithName(PanelControlName)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(1, 0, 1, 1)
                .Visible(false)
                .Column(col =>
                {
                    var scrollPanel = BuildScrollablePanel();
                    BuildLeftPanelContent(scrollPanel, initialSnapshot);
                    AddNarrowSeparator(scrollPanel);
                    BuildGraphsContentPublic(scrollPanel, initialSnapshot);
                    col.Add(scrollPanel);
                })
                .Build();

            grid.BackgroundColor = UIConstants.BaseBg;
            grid.ForegroundColor = UIConstants.PrimaryText;
            return grid;
        }
    }

    protected override void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        int coreCount = cpu.PerCoreSamples is { Count: > 0 }
            ? cpu.PerCoreSamples.Count
            : Environment.ProcessorCount;

        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine($"[{accent} bold]═══ CPU Visualization ═══[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            Controls.Markup()
                .AddLine($"[{muted} bold]Current Aggregate Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuUserBar")
                .WithLabel("User CPU")
                .WithLabelWidth(UIConstants.CpuBarLabelWidth)
                .WithValue(cpu.User)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.SparkCpuUser)
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuSystemBar")
                .WithLabel("System CPU")
                .WithLabelWidth(UIConstants.CpuBarLabelWidth)
                .WithValue(cpu.System)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.SparkCpuSystem)
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuIoWaitBar")
                .WithLabel("IoWait")
                .WithLabelWidth(UIConstants.CpuBarLabelWidth)
                .WithValue(cpu.IoWait)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.GradientIoRead)
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("cpuTotalBar")
                .WithLabel("Total CPU")
                .WithLabelWidth(UIConstants.CpuBarLabelWidth)
                .WithValue(totalCpu)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 2)
                .WithSmoothGradient(UIConstants.SparkCpuTotal)
                .Build()
        );

        AddSectionSeparator(panel);

        AddSectionHeader(panel, "Aggregate Historical Trends");

        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuUserSparkline")
                .WithTitle("User CPU %")
                .WithTitleColor(UIConstants.Critical)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkCpuUser)
                .WithBackgroundColor(UIConstants.PanelBg)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_userHistory.DataMutable)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuSystemSparkline")
                .WithTitle("System CPU %")
                .WithTitleColor(UIConstants.Warning)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkCpuSystem)
                .WithBackgroundColor(UIConstants.PanelBg)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_systemHistory.DataMutable)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuTotalSparkline")
                .WithTitle("Total CPU %")
                .WithTitleColor(UIConstants.Accent)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkCpuTotal)
                .WithBackgroundColor(UIConstants.PanelBg)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_totalHistory.DataMutable)
                .Build()
        );

        if (coreCount > 0)
        {
            AddSectionSeparator(panel);
            AddSectionHeader(panel, "Per-Core History");

            for (int coreIndex = 0; coreIndex < coreCount; coreIndex++)
            {
                double ratio = coreCount > 1 ? (double)coreIndex / (coreCount - 1) : 0;
                int red = (int)(ratio * 255);
                int green = (int)((1 - ratio) * 255);
                var coreColor = new Color((byte)red, (byte)green, 0);

                panel.AddControl(
                    new SparklineBuilder()
                        .WithName($"cpuCore{coreIndex}Sparkline")
                        .WithTitle($"Core {coreIndex}")
                        .WithTitleColor(coreColor)
                        .WithTitlePosition(TitlePosition.Bottom)
                        .WithHeight(UIConstants.CpuCoreSparklineHeight)
                        .WithMaxValue(100)
                        .WithGradient(UIConstants.SparkCpuPerCore)
                        .WithBackgroundColor(UIConstants.PanelBg)
                        .WithBorder(BorderStyle.None)
                        .WithMode(SparklineMode.Braille)
                        .WithBaseline(true, position: TitlePosition.Bottom)
                        .WithInlineTitleBaseline(true)
                        .WithMargin(2, 0, 1, 0)
                        .WithData(_perCoreHistory.GetMutable(coreIndex))
                        .Build()
                );
            }
        }
    }

    public void BuildGraphsContentPublic(ScrollablePanelControl panel, SystemSnapshot snapshot)
        => BuildGraphsContent(panel, snapshot);

    #region Left Panel (CPU uses cards with controls)

    public void BuildLeftPanelContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        int coreCount = cpu.PerCoreSamples is { Count: > 0 }
            ? cpu.PerCoreSamples.Count
            : Environment.ProcessorCount;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        double idleCpu = Math.Max(0, 100 - totalCpu);
        var topCpuProcs = snapshot.Processes
            .OrderByDescending(p => p.CpuPercent)
            .Take(UIConstants.TopConsumerCount)
            .ToList();

        // "System CPU (N cores)" card
        var cpuCard = BuildCard($"System CPU ({coreCount} cores)");
        cpuCard.AddControl(
            Controls.Markup()
                .WithName("cpuAggStats")
                .AddLine(BuildCpuAggStatsContent(cpu, totalCpu, idleCpu))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );
        cpuCard.AddControl(
            new BarGraphBuilder()
                .WithName("cpuAggUserBar")
                .WithLabel("User")
                .WithLabelWidth(UIConstants.CpuCoreLabelWidth)
                .WithValue(cpu.User)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );
        cpuCard.AddControl(
            new BarGraphBuilder()
                .WithName("cpuAggSystemBar")
                .WithLabel("Sys")
                .WithLabelWidth(UIConstants.CpuCoreLabelWidth)
                .WithValue(cpu.System)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.SparkCpuSystem)
                .Build()
        );
        cpuCard.AddControl(
            new BarGraphBuilder()
                .WithName("cpuAggIoBar")
                .WithLabel("I/O")
                .WithLabelWidth(UIConstants.CpuCoreLabelWidth)
                .WithValue(cpu.IoWait)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientIoRead)
                .Build()
        );
        cpuCard.AddControl(
            new BarGraphBuilder()
                .WithName("cpuAggTotalBar")
                .WithLabel("Tot")
                .WithLabelWidth(UIConstants.CpuCoreLabelWidth)
                .WithValue(totalCpu)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.SparkCpuTotal)
                .Build()
        );
        panel.AddControl(cpuCard);

        // "Per-Core Usage" card
        var coreCard = BuildCard("Per-Core Usage");
        if (cpu.PerCoreSamples is { Count: > 0 })
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                double coreTotal = core.User + core.System + core.IoWait;
                coreCard.AddControl(
                    new BarGraphBuilder()
                        .WithName($"cpuCoreLeftBar{core.CoreIndex}")
                        .WithLabel($"C{core.CoreIndex,2}")
                        .WithLabelWidth(UIConstants.CpuCoreLabelWidth)
                        .WithValue(coreTotal)
                        .WithMaxValue(100)
                        .WithBarWidth(UIConstants.CpuCoreBarWidth)
                        .WithUnfilledColor(UIConstants.BarUnfilledColor)
                        .ShowLabel().ShowValue()
                        .WithValueFormat("F1")
                        .WithMargin(0, 0, 0, 0)
                        .WithSmoothGradient(UIConstants.GradientHealthy)
                        .Build()
                );
            }
        }
        else
        {
            for (int i = 0; i < coreCount; i++)
            {
                coreCard.AddControl(
                    new BarGraphBuilder()
                        .WithName($"cpuCoreLeftBar{i}")
                        .WithLabel($"C{i,2}")
                        .WithLabelWidth(UIConstants.CpuCoreLabelWidth)
                        .WithValue(0)
                        .WithMaxValue(100)
                        .WithBarWidth(UIConstants.CpuCoreBarWidth)
                        .WithUnfilledColor(UIConstants.BarUnfilledColor)
                        .ShowLabel().ShowValue()
                        .WithValueFormat("F1")
                        .WithMargin(0, 0, 0, 0)
                        .WithSmoothGradient(UIConstants.GradientHealthy)
                        .Build()
                );
            }
        }
        panel.AddControl(coreCard);

        // "Top CPU Consumers" card
        var consumersCard = BuildCard("Top CPU Consumers");
        consumersCard.AddControl(
            Controls.Markup()
                .WithName("cpuTopConsumers")
                .AddLine(BuildTopConsumersContent(topCpuProcs))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );
        panel.AddControl(consumersCard);
    }

    private static string BuildCpuAggStatsContent(CpuSample cpu, double totalCpu, double idleCpu)
    {
        var muted = UIConstants.MutedText.ToMarkup();
        return $"  [{muted}]User:[/]    [{UIConstants.ThresholdColor(cpu.User)}]{cpu.User:F1}%[/]\n" +
               $"  [{muted}]System:[/] [{UIConstants.ThresholdColor(cpu.System)}]{cpu.System:F1}%[/]\n" +
               $"  [{muted}]IoWait:[/] [{UIConstants.ThresholdColor(cpu.IoWait)}]{cpu.IoWait:F1}%[/]\n" +
               $"  [{muted}]Total:[/]  [{UIConstants.ThresholdColor(totalCpu)}]{totalCpu:F1}%[/]\n" +
               $"  [{muted}]Idle:[/]   [{UIConstants.ThresholdColor(100 - idleCpu)}]{idleCpu:F1}%[/]";
    }

    private static string BuildTopConsumersContent(List<ProcessSample> topCpuProcs)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        var lines = new List<string>();
        foreach (var p in topCpuProcs)
            lines.Add($"  [{accent}]{p.CpuPercent,5:F1}%[/]  [{muted}]{p.Pid,6}[/]  {p.Command}");
        return string.Join("\n", lines);
    }

    #endregion

    protected override void UpdateHistory(SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        _userHistory.Add(cpu.User);
        _systemHistory.Add(cpu.System);
        _ioWaitHistory.Add(cpu.IoWait);
        _totalHistory.Add(cpu.User + cpu.System + cpu.IoWait);

        if (cpu.PerCoreSamples != null)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                double coreTotal = core.User + core.System + core.IoWait;
                _perCoreHistory.Add(core.CoreIndex, coreTotal);
            }
        }
    }

    protected override void UpdateGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var rightPanel = FindGraphPanel(grid);
        if (rightPanel == null)
            return;

        var cpu = snapshot.Cpu;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;

        var userBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuUserBar") as BarGraphControl;
        if (userBar != null) userBar.Value = cpu.User;

        var systemBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuSystemBar") as BarGraphControl;
        if (systemBar != null) systemBar.Value = cpu.System;

        var ioWaitBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuIoWaitBar") as BarGraphControl;
        if (ioWaitBar != null) ioWaitBar.Value = cpu.IoWait;

        var totalBar = rightPanel.Children.FirstOrDefault(c => c.Name == "cpuTotalBar") as BarGraphControl;
        if (totalBar != null) totalBar.Value = totalCpu;

        (rightPanel.Children.FirstOrDefault(c => c.Name == "cpuUserSparkline") as SparklineControl)
            ?.SetDataPoints(_userHistory.DataMutable);
        (rightPanel.Children.FirstOrDefault(c => c.Name == "cpuSystemSparkline") as SparklineControl)
            ?.SetDataPoints(_systemHistory.DataMutable);
        (rightPanel.Children.FirstOrDefault(c => c.Name == "cpuTotalSparkline") as SparklineControl)
            ?.SetDataPoints(_totalHistory.DataMutable);

        if (cpu.PerCoreSamples != null)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                var coreSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"cpuCore{core.CoreIndex}Sparkline") as SparklineControl;
                coreSparkline?.SetDataPoints(_perCoreHistory.GetMutable(core.CoreIndex));
            }
        }
    }

    protected override void UpdateLeftColumnText(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        double idleCpu = Math.Max(0, 100 - totalCpu);

        // Update aggregate stats markup
        var cpuAggStats = FindMainWindow()?.FindControl<MarkupControl>("cpuAggStats");
        cpuAggStats?.SetContent(new List<string> { BuildCpuAggStatsContent(cpu, totalCpu, idleCpu) });

        // Update aggregate bar values
        var aggUserBar = FindMainWindow()?.FindControl<BarGraphControl>("cpuAggUserBar");
        if (aggUserBar != null) aggUserBar.Value = cpu.User;

        var aggSystemBar = FindMainWindow()?.FindControl<BarGraphControl>("cpuAggSystemBar");
        if (aggSystemBar != null) aggSystemBar.Value = cpu.System;

        var aggIoBar = FindMainWindow()?.FindControl<BarGraphControl>("cpuAggIoBar");
        if (aggIoBar != null) aggIoBar.Value = cpu.IoWait;

        var aggTotalBar = FindMainWindow()?.FindControl<BarGraphControl>("cpuAggTotalBar");
        if (aggTotalBar != null) aggTotalBar.Value = totalCpu;

        // Update per-core bars
        if (cpu.PerCoreSamples != null)
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                double coreTotal = core.User + core.System + core.IoWait;
                var coreBar = FindMainWindow()?.FindControl<BarGraphControl>($"cpuCoreLeftBar{core.CoreIndex}");
                if (coreBar != null)
                    coreBar.Value = coreTotal;
            }
        }

        // Update top consumers markup
        var topCpuProcs = snapshot.Processes
            .OrderByDescending(p => p.CpuPercent)
            .Take(UIConstants.TopConsumerCount)
            .ToList();

        var consumers = FindMainWindow()?.FindControl<MarkupControl>("cpuTopConsumers");
        consumers?.SetContent(new List<string> { BuildTopConsumersContent(topCpuProcs) });
    }
}
