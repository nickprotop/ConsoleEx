using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

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

    protected override List<string> BuildTextContent(SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;
        var topMemProcs = snapshot.Processes
            .OrderByDescending(p => p.MemPercent)
            .Take(UIConstants.TopConsumerCount)
            .ToList();

        var lines = new List<string>
        {
            "",
            "[cyan1 bold]System Memory[/]",
            "",
            "[grey70 bold]Statistics[/]",
            $"  [grey70]Total:[/]     [cyan1]{mem.TotalMb:F0} MB[/]",
            $"  [grey70]Used:[/]      [cyan1]{mem.UsedMb:F0} MB[/] [grey50]({mem.UsedPercent:F1}%)[/]",
            $"  [grey70]Available:[/] [cyan1]{mem.AvailableMb:F0} MB[/]",
            $"  [grey70]Cached:[/]    [cyan1]{mem.CachedMb:F0} MB[/] [grey50]({mem.CachedPercent:F1}%)[/]",
            $"  [grey70]Buffers:[/]   [cyan1]{mem.BuffersMb:F0} MB[/]",
            "",
            "[grey70 bold]Swap[/]",
            $"  [grey70]Total:[/] [cyan1]{mem.SwapTotalMb:F0} MB[/]",
            GetSwapUsedLine(mem),
            GetSwapFreeLine(mem),
            "",
            "[grey70 bold]Top Memory Consumers[/]",
        };

        foreach (var p in topMemProcs)
            lines.Add($"  [cyan1]{p.MemPercent,5:F1}%[/]  [grey70]{p.Pid,6}[/]  {p.Command}");

        return lines;
    }

    private static string GetSwapUsedLine(MemorySample mem)
    {
        if (mem.SwapTotalMb <= 0)
            return "  [grey70]Used:[/]  [grey50]N/A[/]";
        var pct = mem.SwapUsedMb / mem.SwapTotalMb * 100;
        var color = pct > 0 ? UIConstants.ThresholdColor(pct) : "cyan1";
        return $"  [grey70]Used:[/]  [{color}]{mem.SwapUsedMb:F0} MB ({pct:F0}%)[/]";
    }

    private static string GetSwapFreeLine(MemorySample mem) =>
        $"  [grey70]Free:[/]  [cyan1]{mem.SwapFreeMb:F0} MB[/]";

    protected override void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var mem = snapshot.Memory;

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine("[cyan1 bold]═══ Memory Visualization ═══[/]")
                .AddLine("")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 0)
                .Build()
        );

        panel.AddControl(
            Controls.Markup()
                .AddLine("[grey70 bold]Current Usage[/]")
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
                .WithSmoothGradient(Color.Green, Color.Yellow, Color.Orange1, Color.Red)
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
                .WithSmoothGradient(Color.Red, Color.Orange1, Color.Yellow, Color.Green)
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
                .WithSmoothGradient("warm")
                .Build()
        );

        AddSectionSeparator(panel);

        panel.AddControl(
            Controls.Markup()
                .AddLine("[grey70 bold]Historical Trends[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("memoryUsedSparkline")
                .WithTitle("Memory Used %")
                .WithTitleColor(Color.Cyan1)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient("cool")
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
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
                .WithTitleColor(Color.Yellow)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(Color.Yellow, Color.Orange1)
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
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
                .WithTitleColor(Color.Green)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.SparklineHeight)
                .WithMaxValue(100)
                .WithGradient(Color.Blue, Color.Green)
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.Block)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithMargin(2, 0, 1, 0)
                .WithData(_availableHistory.DataMutable)
                .Build()
        );
    }

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
}
