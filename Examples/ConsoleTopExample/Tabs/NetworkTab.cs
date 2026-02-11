using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace ConsoleTopExample.Tabs;

internal sealed class NetworkTab : BaseResponsiveTab
{
    private readonly HistoryTracker _upHistory = new();
    private readonly HistoryTracker _downHistory = new();
    private readonly KeyedHistoryTracker<string> _perInterfaceUpHistory = new();
    private readonly KeyedHistoryTracker<string> _perInterfaceDownHistory = new();
    private double _peakUpMbps;
    private double _peakDownMbps;

    public NetworkTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
        : base(windowSystem, stats) { }

    public override string Name => "Network";
    public override string PanelControlName => "networkPanel";
    protected override int LayoutThresholdWidth => UIConstants.NetworkLayoutThresholdWidth;

    protected override List<string> BuildTextContent(SystemSnapshot snapshot)
    {
        var net = snapshot.Network;
        int interfaceCount = net.PerInterfaceSamples?.Count ?? 0;

        var lines = new List<string>
        {
            "",
            $"[cyan1 bold]Network ({interfaceCount} interface{(interfaceCount != 1 ? "s" : "")})[/]",
            "",
            "[grey70 bold]Current Rates[/]",
            $"  [grey70]Upload:[/]   [cyan1]{net.UpMbps:F2} MB/s[/]",
            $"  [grey70]Download:[/] [green]{net.DownMbps:F2} MB/s[/]",
            "",
            "[grey70 bold]Peak Rates (session)[/]",
            $"  [grey70]Upload:[/]   [cyan1]{_peakUpMbps:F2} MB/s[/]",
            $"  [grey70]Download:[/] [green]{_peakDownMbps:F2} MB/s[/]",
            "",
            "[grey70 bold]Active Interfaces[/]",
        };

        if (net.PerInterfaceSamples is { Count: > 0 })
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                string ifaceName = iface.InterfaceName.Length > UIConstants.InterfaceNameMaxLength
                    ? iface.InterfaceName.Substring(0, UIConstants.InterfaceNameTruncLength) + "..."
                    : iface.InterfaceName;

                lines.Add($"  [cyan1]{ifaceName,-15}[/] ↑[grey70]{iface.UpMbps:F2}[/] ↓[grey70]{iface.DownMbps:F2}[/]");
            }
        }
        else
        {
            lines.Add("  [grey50]No active interfaces[/]");
        }

        return lines;
    }

    protected override void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var net = snapshot.Network;

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine("[cyan1 bold]═══ Network Visualization ═══[/]")
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

        double maxRate = Math.Max(Math.Max(_peakUpMbps, _peakDownMbps), 1.0);
        double barMax = Math.Ceiling(maxRate / 10) * 10;

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("netUploadBar")
                .WithLabel("Upload")
                .WithLabelWidth(UIConstants.NetworkBarLabelWidth)
                .WithValue(net.UpMbps)
                .WithMaxValue(barMax)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F2")
                .WithMargin(2, 0, 2, 0)
                .WithSmoothGradient("cool")
                .Build()
        );

        panel.AddControl(
            new BarGraphBuilder()
                .WithName("netDownloadBar")
                .WithLabel("Download")
                .WithLabelWidth(UIConstants.NetworkBarLabelWidth)
                .WithValue(net.DownMbps)
                .WithMaxValue(barMax)
                .WithBarWidth(UIConstants.TabBarWidth)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F2")
                .WithMargin(2, 0, 2, 2)
                .WithSmoothGradient(Color.Blue, Color.Green, Color.Yellow)
                .Build()
        );

        AddSectionSeparator(panel);

        panel.AddControl(
            Controls.Markup()
                .AddLine("[grey70 bold]Network History[/] [grey50](↑ Upload  ↓ Download)[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("netCombinedSparkline")
                .WithTitle("↓ Download  ↑ Upload")
                .WithTitleColor(Color.Grey70)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.NetworkCombinedSparklineHeight)
                .WithMaxValue(Math.Max(_peakDownMbps, 1.0))
                .WithSecondaryMaxValue(Math.Max(_peakUpMbps, 1.0))
                .WithGradient("warm")
                .WithSecondaryGradient("cool")
                .WithBackgroundColor(UIConstants.SparklineBackground)
                .WithBorder(BorderStyle.None)
                .WithMode(SparklineMode.BidirectionalBraille)
                .WithBaseline(true, position: TitlePosition.Bottom)
                .WithInlineTitleBaseline(true)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithMargin(2, 0, 1, 0)
                .WithBidirectionalData(_downHistory.DataMutable, _upHistory.DataMutable)
                .Build()
        );

        if (net.PerInterfaceSamples is { Count: > 0 })
        {
            AddSectionSeparator(panel);
            AddSectionHeader(panel, "Per-Interface History");

            int ifaceIndex = 0;
            foreach (var iface in net.PerInterfaceSamples)
            {
                int ifaceCount = net.PerInterfaceSamples.Count;
                double ratio = ifaceCount > 1 ? (double)ifaceIndex / (ifaceCount - 1) : 0;

                string ifaceNameDisplay = iface.InterfaceName.Length > UIConstants.InterfaceNameMaxLength
                    ? iface.InterfaceName.Substring(0, UIConstants.InterfaceNameTruncLength) + "..."
                    : iface.InterfaceName;

                panel.AddControl(
                    new SparklineBuilder()
                        .WithName($"net{iface.InterfaceName}Sparkline")
                        .WithTitle(ifaceNameDisplay)
                        .WithTitleColor(Color.Grey70)
                        .WithTitlePosition(TitlePosition.Bottom)
                        .WithHeight(UIConstants.SparklineHeight)
                        .WithMaxValue(Math.Max(_peakDownMbps, 0.1))
                        .WithSecondaryMaxValue(Math.Max(_peakUpMbps, 0.1))
                        .WithGradient(Color.Green, Color.Yellow)
                        .WithSecondaryGradient(Color.Blue, Color.Cyan1)
                        .WithBackgroundColor(UIConstants.SparklineBackground)
                        .WithBorder(BorderStyle.None)
                        .WithMode(SparklineMode.BidirectionalBraille)
                        .WithBaseline(true, position: TitlePosition.Bottom)
                        .WithInlineTitleBaseline(true)
                        .WithAlignment(HorizontalAlignment.Stretch)
                        .WithMargin(2, 0, 1, 0)
                        .WithBidirectionalData(
                            _perInterfaceDownHistory.GetMutable(iface.InterfaceName),
                            _perInterfaceUpHistory.GetMutable(iface.InterfaceName))
                        .Build()
                );

                ifaceIndex++;
            }
        }
    }

    protected override void UpdateHistory(SystemSnapshot snapshot)
    {
        var net = snapshot.Network;
        _upHistory.Add(net.UpMbps);
        _downHistory.Add(net.DownMbps);

        _peakUpMbps = Math.Max(_peakUpMbps, net.UpMbps);
        _peakDownMbps = Math.Max(_peakDownMbps, net.DownMbps);

        if (net.PerInterfaceSamples != null)
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                _perInterfaceUpHistory.Add(iface.InterfaceName, iface.UpMbps);
                _perInterfaceDownHistory.Add(iface.InterfaceName, iface.DownMbps);
            }
        }
    }

    protected override void UpdateGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var rightPanel = FindGraphPanel(grid);
        if (rightPanel == null)
            return;

        var net = snapshot.Network;

        var uploadBar = rightPanel.Children.FirstOrDefault(c => c.Name == "netUploadBar") as BarGraphControl;
        if (uploadBar != null) uploadBar.Value = net.UpMbps;

        var downloadBar = rightPanel.Children.FirstOrDefault(c => c.Name == "netDownloadBar") as BarGraphControl;
        if (downloadBar != null) downloadBar.Value = net.DownMbps;

        var combinedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "netCombinedSparkline") as SparklineControl;
        if (combinedSparkline != null)
        {
            combinedSparkline.SetBidirectionalData(_upHistory.DataMutable, _downHistory.DataMutable);
            combinedSparkline.MaxValue = Math.Max(_peakUpMbps, 1.0);
            combinedSparkline.SecondaryMaxValue = Math.Max(_peakDownMbps, 1.0);
        }

        if (net.PerInterfaceSamples != null)
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                var ifaceSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"net{iface.InterfaceName}Sparkline") as SparklineControl;
                if (ifaceSparkline != null)
                {
                    ifaceSparkline.SetBidirectionalData(
                        _perInterfaceUpHistory.GetMutable(iface.InterfaceName),
                        _perInterfaceDownHistory.GetMutable(iface.InterfaceName));
                    ifaceSparkline.MaxValue = Math.Max(_peakUpMbps, 0.1);
                    ifaceSparkline.SecondaryMaxValue = Math.Max(_peakDownMbps, 0.1);
                }
            }
        }
    }
}
