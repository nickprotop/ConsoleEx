using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

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
                    var rightPanel = BuildScrollablePanel();
                    BuildGraphsContentPublic(rightPanel, initialSnapshot);
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

            grid.BackgroundColor = UIConstants.WindowBackground;
            grid.ForegroundColor = UIConstants.WindowForeground;
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

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine("[cyan1 bold]═══ CPU Visualization ═══[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            Controls.Markup()
                .AddLine("[grey70 bold]Current Aggregate Usage[/]")
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
                .WithSmoothGradient("spectrum")
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
                .WithSmoothGradient("warm")
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
                .WithSmoothGradient(Color.Cyan1, Color.Yellow, Color.Red)
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
                .WithSmoothGradient(Color.Green, Color.Yellow, Color.Orange1, Color.Red)
                .Build()
        );

        AddSectionSeparator(panel);

        AddSectionHeader(panel, "Aggregate Historical Trends");

        panel.AddControl(
            new SparklineBuilder()
                .WithName("cpuUserSparkline")
                .WithTitle("User CPU %")
                .WithTitleColor(Color.Red)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient("warm")
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
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
                .WithTitleColor(Color.Yellow)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(Color.Yellow, Color.Orange1, Color.Red)
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
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
                .WithTitleColor(Color.Cyan1)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient("spectrum")
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
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
                        .WithGradient(Color.Blue, Color.Cyan1, Color.Yellow, Color.Red)
                        .WithBackgroundColor(UIConstants.SparklineBackground)
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

    #region Left Panel (CPU uses controls, not plain text in left column)

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

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine($"[cyan1 bold]System CPU ({coreCount} cores)[/]")
                .AddLine("")
                .AddLine("[grey70 bold]Aggregate Usage[/]")
                .AddLine($"  [grey70]User:[/]      [red]{cpu.User:F1}%[/]")
                .AddLine($"  [grey70]System:[/]    [yellow]{cpu.System:F1}%[/]")
                .AddLine($"  [grey70]IoWait:[/]    [blue]{cpu.IoWait:F1}%[/] [grey50](Linux only)[/]")
                .AddLine($"  [grey70]Total:[/]     [cyan1]{totalCpu:F1}%[/]")
                .AddLine($"  [grey70]Idle:[/]      [green]{idleCpu:F1}%[/]")
                .AddLine("")
                .AddLine("[grey70 bold]Per-Core Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );

        if (cpu.PerCoreSamples is { Count: > 0 })
        {
            foreach (var core in cpu.PerCoreSamples)
            {
                double coreTotal = core.User + core.System + core.IoWait;
                panel.AddControl(
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
                        .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                        .Build()
                );
            }
        }
        else
        {
            for (int i = 0; i < coreCount; i++)
            {
                panel.AddControl(
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
                        .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                        .Build()
                );
            }
        }

        var markup = Controls.Markup()
            .AddLine("")
            .AddLine("[grey70 bold]Top CPU Consumers[/]");
        foreach (var p in topCpuProcs)
            markup = markup.AddLine($"  [cyan1]{p.CpuPercent,5:F1}%[/]  [grey70]{p.Pid,6}[/]  {p.Command}");
        panel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());
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

        // Update left column per-core bars
        if (grid.Columns.Count > 0 && cpu.PerCoreSamples != null)
        {
            var leftCol = grid.Columns[0];
            var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
            if (leftPanel != null)
            {
                foreach (var core in cpu.PerCoreSamples)
                {
                    double coreTotal = core.User + core.System + core.IoWait;
                    var coreBar = leftPanel.Children.FirstOrDefault(c => c.Name == $"cpuCoreLeftBar{core.CoreIndex}") as BarGraphControl;
                    if (coreBar != null)
                        coreBar.Value = coreTotal;
                }
            }
        }
    }

    protected override void UpdateLeftColumnText(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        if (grid.Columns.Count == 0)
            return;

        var leftCol = grid.Columns[0];
        var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        if (leftPanel == null || leftPanel.Children.Count == 0)
            return;

        var cpu = snapshot.Cpu;
        int coreCount = cpu.PerCoreSamples is { Count: > 0 }
            ? cpu.PerCoreSamples.Count
            : Environment.ProcessorCount;
        double totalCpu = cpu.User + cpu.System + cpu.IoWait;
        double idleCpu = Math.Max(0, 100 - totalCpu);

        // Children[0] is the header markup with aggregate stats
        var headerMarkup = leftPanel.Children[0] as MarkupControl;
        if (headerMarkup != null)
        {
            headerMarkup.SetContent(new List<string>
            {
                "",
                $"[cyan1 bold]System CPU ({coreCount} cores)[/]",
                "",
                "[grey70 bold]Aggregate Usage[/]",
                $"  [grey70]User:[/]      [red]{cpu.User:F1}%[/]",
                $"  [grey70]System:[/]    [yellow]{cpu.System:F1}%[/]",
                $"  [grey70]IoWait:[/]    [blue]{cpu.IoWait:F1}%[/] [grey50](Linux only)[/]",
                $"  [grey70]Total:[/]     [cyan1]{totalCpu:F1}%[/]",
                $"  [grey70]Idle:[/]      [green]{idleCpu:F1}%[/]",
                "",
                "[grey70 bold]Per-Core Usage[/]",
            });
        }

        // Last child is the "Top CPU Consumers" markup
        var lastChild = leftPanel.Children[^1] as MarkupControl;
        if (lastChild != null && lastChild != headerMarkup)
        {
            var topCpuProcs = snapshot.Processes
                .OrderByDescending(p => p.CpuPercent)
                .Take(UIConstants.TopConsumerCount)
                .ToList();

            var lines = new List<string> { "", "[grey70 bold]Top CPU Consumers[/]" };
            foreach (var p in topCpuProcs)
                lines.Add($"  [cyan1]{p.CpuPercent,5:F1}%[/]  [grey70]{p.Pid,6}[/]  {p.Command}");

            lastChild.SetContent(lines);
        }
    }
}
