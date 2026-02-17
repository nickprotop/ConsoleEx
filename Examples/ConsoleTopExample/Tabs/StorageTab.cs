using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

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

    protected override List<string> BuildTextContent(SystemSnapshot snapshot)
    {
        var lines = new List<string>();
        var storage = snapshot.Storage;

        lines.Add("[bold cyan1]Total Storage[/]");
        lines.Add($"  Capacity:  {storage.TotalCapacityGb,6:F1} GB");
        lines.Add($"  Used:      {storage.TotalUsedGb,6:F1} GB ([cyan1]{storage.TotalUsedPercent:F1}%[/])");
        lines.Add($"  Free:      {storage.TotalFreeGb,6:F1} GB");
        lines.Add("");

        lines.Add("[bold grey70]Mounted Filesystems[/]");
        lines.Add("");

        foreach (var disk in storage.Disks)
        {
            var mountIcon = disk.IsRemovable ? "📀" : "💾";
            lines.Add($"[cyan1]{mountIcon} {disk.MountPoint}[/] [grey50]({System.IO.Path.GetFileName(disk.DeviceName)})[/]");
            lines.Add($"  Type:    [grey70]{disk.FileSystemType}[/]");

            if (!string.IsNullOrEmpty(disk.Label))
                lines.Add($"  Label:   [yellow]{disk.Label}[/]");

            lines.Add($"  Size:    {disk.TotalGb,6:F1} GB");
            lines.Add($"  Used:    {disk.UsedGb,6:F1} GB ([cyan1]{disk.UsedPercent:F1}%[/])");
            lines.Add($"  Free:    {disk.FreeGb,6:F1} GB");

            if (!string.IsNullOrEmpty(disk.MountOptions))
                lines.Add($"  Options: [grey50]{disk.MountOptions}[/]");

            lines.Add("");
        }

        if (storage.Disks.Count == 0)
            lines.Add("[grey50]No storage devices found[/]");

        return lines;
    }

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
                ? $"[bold cyan1]{disk.MountPoint}[/] [grey50]({System.IO.Path.GetFileName(disk.DeviceName)} - {disk.FileSystemType} - \"{disk.Label}\")[/]"
                : $"[bold cyan1]{disk.MountPoint}[/] [grey50]({System.IO.Path.GetFileName(disk.DeviceName)} - {disk.FileSystemType})[/]";

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
                    .WithSmoothGradient(Color.Green, Color.Yellow, Color.Orange1, Color.Red)
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
                    .WithSmoothGradient(Color.Blue, Color.Cyan1)
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
                    .WithSmoothGradient(Color.Yellow, Color.Orange1, Color.Red)
                    .WithMargin(2, 0, 1, 0)
                    .Build()
            );

            double maxRead = Math.Max(10, _readHistory.GetMutable(deviceKey).DefaultIfEmpty(0).Max());
            double maxWrite = Math.Max(10, _writeHistory.GetMutable(deviceKey).DefaultIfEmpty(0).Max());

            panel.AddControl(
                new SparklineBuilder()
                    .WithName($"disk_{deviceKey}_io")
                    .WithTitle("↑ Read  ↓ Write")
                    .WithTitleColor(Color.Grey70)
                    .WithTitlePosition(TitlePosition.Bottom)
                    .WithHeight(UIConstants.StorageIoSparklineHeight)
                    .WithMaxValue(maxRead)
                    .WithSecondaryMaxValue(maxWrite)
                    .WithGradient("cool")
                    .WithSecondaryGradient("warm")
                    .WithBackgroundColor(UIConstants.SparklineBackground)
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
}
