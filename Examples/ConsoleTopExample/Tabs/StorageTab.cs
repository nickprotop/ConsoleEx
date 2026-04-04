using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ConsoleTopExample.Tabs;

internal sealed class StorageTab : BaseResponsiveTab
{
    private readonly KeyedHistoryTracker<string> _readHistory = new();
    private readonly KeyedHistoryTracker<string> _writeHistory = new();

    public StorageTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
        : base(windowSystem, stats) { }

    public override string Name => "Storage";
    public override string PanelControlName => "storagePanel";
    protected override int LayoutThresholdWidth => UIConstants.StorageLayoutThresholdWidth;

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
                    var rightPanel = BuildRightPanel();
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
        var storage = snapshot.Storage;

        // Total Storage aggregate card
        var totalCard = BuildCard("Total Storage");
        totalCard.AddControl(
            new BarGraphBuilder()
                .WithName("leftStorageTotalBar")
                .WithLabel("Used")
                .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                .WithValue(storage.TotalUsedPercent)
                .WithMaxValue(100)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F1")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientHealthy)
                .Build()
        );
        totalCard.AddControl(
            Controls.Markup()
                .WithName("storageAggStats")
                .AddLine(BuildAggStatsContent(storage))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );
        panel.AddControl(totalCard);

        // Per-disk cards
        foreach (var disk in storage.Disks)
        {
            var mountIcon = disk.IsRemovable ? "📀" : "💾";
            var diskCard = BuildCard($"{mountIcon} {disk.MountPoint}");

            diskCard.AddControl(
                Controls.Markup()
                    .WithName($"disk_{disk.DeviceName}_info")
                    .AddLine(BuildDiskInfoContent(disk))
                    .WithAlignment(HorizontalAlignment.Left)
                    .Build()
            );

            diskCard.AddControl(
                new BarGraphBuilder()
                    .WithName($"leftDisk_{disk.DeviceName}_usage")
                    .WithLabel("Used %")
                    .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                    .WithValue(disk.UsedPercent)
                    .WithMaxValue(100)
                    .WithUnfilledColor(UIConstants.BarUnfilledColor)
                    .ShowLabel().ShowValue()
                    .WithValueFormat("F1")
                    .WithMargin(0, 0, 0, 0)
                    .WithSmoothGradient(UIConstants.GradientHealthy)
                    .Build()
            );

            diskCard.AddControl(
                new BarGraphBuilder()
                    .WithName($"leftDisk_{disk.DeviceName}_read")
                    .WithLabel("Read MB/s")
                    .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                    .WithValue(disk.ReadMbps)
                    .WithMaxValue(Math.Max(10, _readHistory.GetMutable(disk.DeviceName).DefaultIfEmpty(0).Max()))
                    .WithUnfilledColor(UIConstants.BarUnfilledColor)
                    .ShowLabel().ShowValue()
                    .WithValueFormat("F1")
                    .WithMargin(0, 0, 0, 0)
                    .WithSmoothGradient(UIConstants.GradientIoRead)
                    .Build()
            );

            diskCard.AddControl(
                new BarGraphBuilder()
                    .WithName($"leftDisk_{disk.DeviceName}_write")
                    .WithLabel("Write MB/s")
                    .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                    .WithValue(disk.WriteMbps)
                    .WithMaxValue(Math.Max(10, _writeHistory.GetMutable(disk.DeviceName).DefaultIfEmpty(0).Max()))
                    .WithUnfilledColor(UIConstants.BarUnfilledColor)
                    .ShowLabel().ShowValue()
                    .WithValueFormat("F1")
                    .WithMargin(0, 0, 0, 0)
                    .WithSmoothGradient(UIConstants.GradientIoWrite)
                    .Build()
            );

            panel.AddControl(diskCard);
        }
    }

    private static string BuildAggStatsContent(StorageSample storage)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        return $"  [{muted}]Capacity:[/] [{accent}]{storage.TotalCapacityGb:F1} GB[/]\n" +
               $"  [{muted}]Used:[/]     [{accent}]{storage.TotalUsedGb:F1} GB[/] [{muted}]({storage.TotalUsedPercent:F1}%)[/]\n" +
               $"  [{muted}]Free:[/]     [{accent}]{storage.TotalFreeGb:F1} GB[/]";
    }

    private static string BuildDiskInfoContent(DiskSample disk)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        return $"  [{muted}]Device:[/] [{accent}]{System.IO.Path.GetFileName(disk.DeviceName)}[/]\n" +
               $"  [{muted}]Type:[/]   [{accent}]{disk.FileSystemType}[/]\n" +
               $"  [{muted}]Size:[/]   [{accent}]{disk.TotalGb:F1} GB[/]";
    }

    #endregion

    #region Graphs

    protected override void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var storage = snapshot.Storage;

        if (storage.Disks.Count == 0)
        {
            panel.AddControl(
                Controls.Markup()
                    .AddLine("[grey50]No storage devices to display[/]")
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );
            return;
        }

        foreach (var disk in storage.Disks)
        {
            var deviceKey = disk.DeviceName;

            var headerText = !string.IsNullOrEmpty(disk.Label)
                ? $"[{UIConstants.Accent.ToMarkup()} bold]{disk.MountPoint}[/] [{UIConstants.MutedText.ToMarkup()}]({System.IO.Path.GetFileName(disk.DeviceName)} - {disk.FileSystemType} - \"{disk.Label}\")[/]"
                : $"[{UIConstants.Accent.ToMarkup()} bold]{disk.MountPoint}[/] [{UIConstants.MutedText.ToMarkup()}]({System.IO.Path.GetFileName(disk.DeviceName)} - {disk.FileSystemType})[/]";

            panel.AddControl(
                Controls.Markup()
                    .AddLine(headerText)
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );

            panel.AddControl(
                new BarGraphBuilder()
                    .WithName($"disk_{deviceKey}_usage")
                    .WithLabel("Used %")
                    .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                    .WithValue(disk.UsedPercent)
                    .WithMaxValue(100)
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithSmoothGradient(UIConstants.GradientHealthy)
                    .WithMargin(2, 1, 1, 0)
                    .Build()
            );

            panel.AddControl(
                new BarGraphBuilder()
                    .WithName($"disk_{deviceKey}_read_current")
                    .WithLabel("Read MB/s")
                    .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                    .WithValue(disk.ReadMbps)
                    .WithMaxValue(100)
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithSmoothGradient(UIConstants.GradientIoRead)
                    .WithMargin(2, 0, 1, 0)
                    .Build()
            );

            panel.AddControl(
                new BarGraphBuilder()
                    .WithName($"disk_{deviceKey}_write_current")
                    .WithLabel("Write MB/s")
                    .WithLabelWidth(UIConstants.StorageBarLabelWidth)
                    .WithValue(disk.WriteMbps)
                    .WithMaxValue(100)
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithSmoothGradient(UIConstants.GradientIoWrite)
                    .WithMargin(2, 0, 1, 0)
                    .Build()
            );

            double maxRead = Math.Max(10, _readHistory.GetMutable(deviceKey).DefaultIfEmpty(0).Max());
            double maxWrite = Math.Max(10, _writeHistory.GetMutable(deviceKey).DefaultIfEmpty(0).Max());

            panel.AddControl(
                new SparklineBuilder()
                    .WithName($"disk_{deviceKey}_io")
                    .WithTitle("↑ Read  ↓ Write")
                    .WithTitleColor(UIConstants.MutedText)
                    .WithTitlePosition(TitlePosition.Bottom)
                    .WithHeight(UIConstants.StorageIoSparklineHeight)
                    .WithMaxValue(maxRead)
                    .WithSecondaryMaxValue(maxWrite)
                    .WithGradient(UIConstants.SparkStorageRead)
                    .WithSecondaryGradient(UIConstants.SparkStorageWrite)
                    .WithBackgroundColor(UIConstants.PanelBg)
                    .WithBorder(BorderStyle.None)
                    .WithMode(SparklineMode.BidirectionalBraille)
                    .WithBaseline(true, position: TitlePosition.Bottom)
                    .WithInlineTitleBaseline(true)
                    .WithMargin(2, 1, 1, 0)
                    .WithBidirectionalData(_readHistory.GetMutable(deviceKey), _writeHistory.GetMutable(deviceKey))
                    .Build()
            );

            AddSectionSeparator(panel);
        }
    }

    #endregion

    #region Update

    protected override void UpdateHistory(SystemSnapshot snapshot)
    {
        foreach (var disk in snapshot.Storage.Disks)
        {
            _readHistory.Add(disk.DeviceName, disk.ReadMbps);
            _writeHistory.Add(disk.DeviceName, disk.WriteMbps);
        }
    }

    protected override void UpdateGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var rightPanel = FindGraphPanel(grid);
        if (rightPanel == null)
            return;

        foreach (var disk in snapshot.Storage.Disks)
        {
            var deviceKey = disk.DeviceName;

            var usageBar = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_usage") as BarGraphControl;
            if (usageBar != null) usageBar.Value = disk.UsedPercent;

            double readMax  = Math.Max(10, _readHistory.Get(deviceKey).DefaultIfEmpty(0).Max());
            double writeMax = Math.Max(10, _writeHistory.Get(deviceKey).DefaultIfEmpty(0).Max());

            var readBar = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_read_current") as BarGraphControl;
            if (readBar != null) { readBar.Value = disk.ReadMbps; readBar.MaxValue = readMax; }

            var writeBar = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_write_current") as BarGraphControl;
            if (writeBar != null) { writeBar.Value = disk.WriteMbps; writeBar.MaxValue = writeMax; }

            var ioSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"disk_{deviceKey}_io") as SparklineControl;
            if (ioSparkline != null)
            {
                ioSparkline.SetBidirectionalData(
                    _readHistory.GetMutable(deviceKey),
                    _writeHistory.GetMutable(deviceKey));
            }
        }
    }

    protected override void UpdateLeftColumnText(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var storage = snapshot.Storage;

        var aggStats = FindMainWindow()?.FindControl<MarkupControl>("storageAggStats");
        aggStats?.SetContent(new List<string> { BuildAggStatsContent(storage) });

        var totalBar = FindMainWindow()?.FindControl<BarGraphControl>("leftStorageTotalBar");
        if (totalBar != null)
            totalBar.Value = storage.TotalUsedPercent;

        foreach (var disk in storage.Disks)
        {
            var usageBar = FindMainWindow()?.FindControl<BarGraphControl>($"leftDisk_{disk.DeviceName}_usage");
            if (usageBar != null)
                usageBar.Value = disk.UsedPercent;

            double readMax = Math.Max(10, _readHistory.Get(disk.DeviceName).DefaultIfEmpty(0).Max());
            double writeMax = Math.Max(10, _writeHistory.Get(disk.DeviceName).DefaultIfEmpty(0).Max());

            var readBar = FindMainWindow()?.FindControl<BarGraphControl>($"leftDisk_{disk.DeviceName}_read");
            if (readBar != null) { readBar.Value = disk.ReadMbps; readBar.MaxValue = readMax; }

            var writeBar = FindMainWindow()?.FindControl<BarGraphControl>($"leftDisk_{disk.DeviceName}_write");
            if (writeBar != null) { writeBar.Value = disk.WriteMbps; writeBar.MaxValue = writeMax; }
        }
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

            var rightPanel = BuildRightPanel();
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
