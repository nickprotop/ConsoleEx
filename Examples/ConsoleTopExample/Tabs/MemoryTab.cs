using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ConsoleTopExample.Tabs;

internal sealed class MemoryTab : BaseResponsiveTab
{
    private readonly HistoryTracker _usedHistory = new();
    private readonly HistoryTracker _availableHistory = new();
    private readonly HistoryTracker _cachedHistory = new();
    private readonly HistoryTracker _swapHistory = new();

    public MemoryTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
        : base(windowSystem, stats) { }

    public override string Name => "Memory";
    public override string PanelControlName => "memoryPanel";
    protected override int LayoutThresholdWidth => UIConstants.MemoryLayoutThresholdWidth;

    protected override List<string> BuildTextContent(SystemSnapshot snapshot) => new();

    #region Panel Building

    public override IWindowControl BuildPanel(SystemSnapshot initialSnapshot, int windowWidth)
    {
        _currentLayout = windowWidth >= LayoutThresholdWidth
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
                    BuildLeftPanelCards(leftPanel, initialSnapshot);
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
                    BuildGraphsContent(rightPanel, initialSnapshot);
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
                    BuildLeftPanelCards(scrollPanel, initialSnapshot);
                    AddNarrowSeparator(scrollPanel);
                    BuildGraphsContent(scrollPanel, initialSnapshot);
                    col.Add(scrollPanel);
                })
                .Build();

            grid.BackgroundColor = UIConstants.BaseBg;
            grid.ForegroundColor = UIConstants.PrimaryText;
            return grid;
        }
    }

    #endregion

    #region Left Panel Cards

    private void BuildLeftPanelCards(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;
        double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        var topMemProcs = snapshot.Processes
            .OrderByDescending(p => p.MemPercent)
            .Take(UIConstants.TopConsumerCount)
            .ToList();

        // Physical Memory card
        var memCard = BuildCard("Physical Memory");
        memCard.AddControl(
            new BarGraphBuilder()
                .WithName("leftMemUsedBar")
                .WithLabel("Used")
                .WithLabelWidth(UIConstants.MemoryBarLabelWidth)
                .WithValue(mem.UsedPercent)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 10)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );
        memCard.AddControl(
            Controls.Markup()
                .WithName("memCardStats")
                .AddLine(BuildMemCardStatsContent(mem))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );
        panel.AddControl(memCard);

        // Swap card
        var swapCard = BuildCard("Swap");
        swapCard.AddControl(
            new BarGraphBuilder()
                .WithName("leftSwapBar")
                .WithLabel("Used")
                .WithLabelWidth(UIConstants.MemoryBarLabelWidth)
                .WithValue(swapPercent)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 10)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientIoWrite)
                .Build()
        );
        swapCard.AddControl(
            Controls.Markup()
                .WithName("swapCardStats")
                .AddLine(BuildSwapCardStatsContent(mem))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );
        panel.AddControl(swapCard);

        // Top Memory Consumers card
        var consumersCard = BuildCard("Top Memory Consumers");
        consumersCard.AddControl(
            Controls.Markup()
                .WithName("memTopConsumers")
                .AddLine(BuildTopConsumersContent(topMemProcs))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );
        panel.AddControl(consumersCard);
    }

    private static string BuildMemCardStatsContent(MemorySample mem)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        return $"  [{muted}]Total:[/]     [{accent}]{mem.TotalMb:F0} MB[/]\n" +
               $"  [{muted}]Used:[/]      [{accent}]{mem.UsedMb:F0} MB[/] [{muted}]({mem.UsedPercent:F1}%)[/]\n" +
               $"  [{muted}]Available:[/] [{accent}]{mem.AvailableMb:F0} MB[/]\n" +
               $"  [{muted}]Cached:[/]    [{accent}]{mem.CachedMb:F0} MB[/] [{muted}]({mem.CachedPercent:F1}%)[/]\n" +
               $"  [{muted}]Buffers:[/]   [{accent}]{mem.BuffersMb:F0} MB[/]";
    }

    private static string BuildSwapCardStatsContent(MemorySample mem)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        if (mem.SwapTotalMb <= 0)
            return $"  [{muted}]No swap configured[/]";

        var pct = mem.SwapUsedMb / mem.SwapTotalMb * 100;
        return $"  [{muted}]Total:[/] [{accent}]{mem.SwapTotalMb:F0} MB[/]\n" +
               $"  [{muted}]Used:[/]  [{accent}]{mem.SwapUsedMb:F0} MB[/] [{muted}]({pct:F0}%)[/]\n" +
               $"  [{muted}]Free:[/]  [{accent}]{mem.SwapFreeMb:F0} MB[/]";
    }

    private static string BuildTopConsumersContent(List<ProcessSample> topMemProcs)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        var lines = new List<string>();
        foreach (var p in topMemProcs)
            lines.Add($"  [{accent}]{p.MemPercent,5:F1}%[/]  [{muted}]{p.Pid,6}[/]  {p.Command}");
        return string.Join("\n", lines);
    }

    #endregion

    #region Graphs

    protected override void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine($"[{accent} bold]=== Memory Visualization ===[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            Controls.Markup()
                .AddLine($"[{muted} bold]Current Usage[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("ramUsedBar")
                .WithLabel("RAM Used")
                .WithLabelWidth(UIConstants.MemoryBarLabelWidth)
                .WithValue(mem.UsedPercent)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );

        var freePercent = (mem.AvailableMb / mem.TotalMb) * 100;
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("ramFreeBar")
                .WithLabel("RAM Free")
                .WithLabelWidth(UIConstants.MemoryBarLabelWidth)
                .WithValue(freePercent)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient(UIConstants.GradientBaseHealthy)
                .Build()
        );

        double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        panel.AddControl(
            new BarGraphBuilder()
                .WithName("swapUsedBar")
                .WithLabel("Swap Used")
                .WithLabelWidth(UIConstants.MemoryBarLabelWidth)
                .WithValue(swapPercent)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel()
                .ShowValue()
                .WithValueFormat("F1")
                .WithMargin(2, 0, 2, 2)
                .WithSmoothGradient(UIConstants.GradientIoWrite)
                .Build()
        );

        AddSectionSeparator(panel);

        panel.AddControl(
            Controls.Markup()
                .AddLine($"[{muted} bold]Historical Trends[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryUsedSparkline")
                .WithTitle("Memory Used %")
                .WithTitleColor(UIConstants.Accent)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkMemUsed)
                .WithBackgroundColor(UIConstants.PanelBg)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_usedHistory.DataMutable)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryCachedSparkline")
                .WithTitle("Memory Cached %")
                .WithTitleColor(UIConstants.AccentSecondary)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkMemCached)
                .WithBackgroundColor(UIConstants.PanelBg)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_cachedHistory.DataMutable)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryFreeSparkline")
                .WithTitle("Memory Available %")
                .WithTitleColor(UIConstants.Normal)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(UIConstants.SparkMemAvailable)
                .WithBackgroundColor(UIConstants.PanelBg)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Braille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_availableHistory.DataMutable)
                .Build()
        );
    }

    #endregion

    #region Update

    protected override void UpdateHistory(SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;
        _usedHistory.Add(mem.UsedPercent);
        _availableHistory.Add((mem.AvailableMb / mem.TotalMb) * 100);
        _cachedHistory.Add(mem.CachedPercent);

        double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        _swapHistory.Add(swapPercent);
    }

    protected override void UpdateGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var rightPanel = FindGraphPanel(grid);
        if (rightPanel == null)
            return;

        var mem = snapshot.Memory;

        var ramUsedBar = rightPanel.Children.FirstOrDefault(c => c.Name == "ramUsedBar") as BarGraphControl;
        if (ramUsedBar != null)
            ramUsedBar.Value = mem.UsedPercent;

        var ramFreeBar = rightPanel.Children.FirstOrDefault(c => c.Name == "ramFreeBar") as BarGraphControl;
        if (ramFreeBar != null)
            ramFreeBar.Value = (mem.AvailableMb / mem.TotalMb) * 100;

        var swapBar = rightPanel.Children.FirstOrDefault(c => c.Name == "swapUsedBar") as BarGraphControl;
        if (swapBar != null)
        {
            double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
            swapBar.Value = swapPercent;
        }

        var usedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "memoryUsedSparkline") as SparklineControl;
        usedSparkline?.SetDataPoints(_usedHistory.DataMutable);

        var cachedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "memoryCachedSparkline") as SparklineControl;
        cachedSparkline?.SetDataPoints(_cachedHistory.DataMutable);

        var freeSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "memoryFreeSparkline") as SparklineControl;
        freeSparkline?.SetDataPoints(_availableHistory.DataMutable);
    }

    protected override void UpdateLeftColumnText(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;
        double swapPercent = mem.SwapTotalMb > 0 ? (mem.SwapUsedMb / mem.SwapTotalMb * 100) : 0;
        var topMemProcs = snapshot.Processes
            .OrderByDescending(p => p.MemPercent)
            .Take(UIConstants.TopConsumerCount)
            .ToList();

        var memStats = FindMainWindow()?.FindControl<MarkupControl>("memCardStats");
        memStats?.SetContent(new List<string> { BuildMemCardStatsContent(mem) });

        var memBar = FindMainWindow()?.FindControl<BarGraphControl>("leftMemUsedBar");
        if (memBar != null)
            memBar.Value = mem.UsedPercent;

        var swapStats = FindMainWindow()?.FindControl<MarkupControl>("swapCardStats");
        swapStats?.SetContent(new List<string> { BuildSwapCardStatsContent(mem) });

        var swapBar = FindMainWindow()?.FindControl<BarGraphControl>("leftSwapBar");
        if (swapBar != null)
            swapBar.Value = swapPercent;

        var consumers = FindMainWindow()?.FindControl<MarkupControl>("memTopConsumers");
        consumers?.SetContent(new List<string> { BuildTopConsumersContent(topMemProcs) });
    }

    public new void HandleResize(int newWidth, int newHeight)
    {
        var grid = FindMainWindow()?.FindControl<HorizontalGridControl>(PanelControlName);
        if (grid == null || !grid.Visible)
            return;

        var desired = newWidth >= LayoutThresholdWidth
            ? ResponsiveLayoutMode.Wide
            : ResponsiveLayoutMode.Narrow;

        if (desired == _currentLayout)
            return;

        _currentLayout = desired;
        RebuildCardColumns(grid);
    }

    private void RebuildCardColumns(HorizontalGridControl grid)
    {
        var snapshot = Stats.ReadSnapshot();

        for (int i = grid.Columns.Count - 1; i >= 0; i--)
            grid.RemoveColumn(grid.Columns[i]);

        if (_currentLayout == ResponsiveLayoutMode.Wide)
        {
            var leftPanel = BuildScrollablePanel();
            BuildLeftPanelCards(leftPanel, snapshot);

            var leftCol = new ColumnContainer(grid) { Width = UIConstants.FixedTextColumnWidth };
            leftCol.AddContent(leftPanel);
            grid.AddColumn(leftCol);

            var sepCol = new ColumnContainer(grid) { Width = UIConstants.SeparatorColumnWidth };
            sepCol.AddContent(new SeparatorControl
            {
                ForegroundColor = UIConstants.SeparatorColor,
                VerticalAlignment = VerticalAlignment.Fill
            });
            grid.AddColumn(sepCol);

            var rightPanel = BuildScrollablePanel();
            BuildGraphsContent(rightPanel, snapshot);

            var rightCol = new ColumnContainer(grid);
            rightCol.AddContent(rightPanel);
            grid.AddColumn(rightCol);
        }
        else
        {
            var scrollPanel = BuildScrollablePanel();
            BuildLeftPanelCards(scrollPanel, snapshot);
            AddNarrowSeparator(scrollPanel);
            BuildGraphsContent(scrollPanel, snapshot);

            var col = new ColumnContainer(grid);
            col.AddContent(scrollPanel);
            grid.AddColumn(col);
        }

        FindMainWindow()?.ForceRebuildLayout();
        FindMainWindow()?.Invalidate(true);
    }

    #endregion
}
