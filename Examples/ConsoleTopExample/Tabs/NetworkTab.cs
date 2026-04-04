using ConsoleTopExample.Helpers;
using ConsoleTopExample.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ConsoleTopExample.Tabs;

internal sealed class NetworkTab : BaseResponsiveTab
{
    private readonly HistoryTracker _upHistory = new();
    private readonly HistoryTracker _downHistory = new();
    private readonly KeyedHistoryTracker<string> _perInterfaceUpHistory = new();
    private readonly KeyedHistoryTracker<string> _perInterfaceDownHistory = new();
    private double _peakUpMbps;
    private double _peakDownMbps;
    private HashSet<string> _knownInterfaces = new();

    public NetworkTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
        : base(windowSystem, stats) { }

    public override string Name => "Network";
    public override string PanelControlName => "networkPanel";
    protected override int LayoutThresholdWidth => UIConstants.NetworkLayoutThresholdWidth;

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
        var net = snapshot.Network;
        int interfaceCount = net.PerInterfaceSamples?.Count ?? 0;

        // Network summary card
        var summaryCard = BuildCard($"Network ({interfaceCount} interface{(interfaceCount != 1 ? "s" : "")})");

        double maxRate = Math.Max(Math.Max(_peakUpMbps, _peakDownMbps), 1.0);
        double barMax = Math.Ceiling(maxRate / 10) * 10;

        summaryCard.AddControl(
            new BarGraphBuilder()
                .WithName("leftNetUpBar")
                .WithLabel("Upload")
                .WithLabelWidth(UIConstants.NetworkBarLabelWidth)
                .WithValue(net.UpMbps)
                .WithMaxValue(barMax)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F2")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientNetUpload)
                .Build()
        );

        summaryCard.AddControl(
            new BarGraphBuilder()
                .WithName("leftNetDownBar")
                .WithLabel("Download")
                .WithLabelWidth(UIConstants.NetworkBarLabelWidth)
                .WithValue(net.DownMbps)
                .WithMaxValue(barMax)
                .WithBarWidth(UIConstants.TabBarWidth - 15)
                .WithUnfilledColor(UIConstants.BarUnfilledColor)
                .ShowLabel().ShowValue()
                .WithValueFormat("F2")
                .WithMargin(0, 0, 0, 0)
                .WithSmoothGradient(UIConstants.GradientNetDownload)
                .Build()
        );

        summaryCard.AddControl(
            Controls.Markup()
                .WithName("netSummaryStats")
                .AddLine(BuildNetSummaryStatsContent(net))
                .WithAlignment(HorizontalAlignment.Left)
                .Build()
        );

        panel.AddControl(summaryCard);

        // Per-interface cards
        if (net.PerInterfaceSamples is { Count: > 0 })
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                string ifaceNameDisplay = iface.InterfaceName.Length > UIConstants.InterfaceNameMaxLength
                    ? iface.InterfaceName.Substring(0, UIConstants.InterfaceNameTruncLength) + "..."
                    : iface.InterfaceName;

                var ifaceCard = BuildCard(ifaceNameDisplay);
                ifaceCard.AddControl(
                    Controls.Markup()
                        .WithName($"netIface_{iface.InterfaceName}")
                        .AddLine(BuildIfaceCardContent(iface))
                        .WithAlignment(HorizontalAlignment.Left)
                        .Build()
                );
                panel.AddControl(ifaceCard);
            }
        }
    }

    private string BuildNetSummaryStatsContent(NetworkSample net)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        return $"  [{muted}]Peak Upload:[/]   [{accent}]{_peakUpMbps:F2} MB/s[/]\n" +
               $"  [{muted}]Peak Download:[/] [{accent}]{_peakDownMbps:F2} MB/s[/]";
    }

    private static string BuildIfaceCardContent(NetworkInterfaceSample iface)
    {
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();
        return $"  [{muted}]Upload:[/]   [{accent}]{iface.UpMbps:F2} MB/s[/]\n" +
               $"  [{muted}]Download:[/] [{accent}]{iface.DownMbps:F2} MB/s[/]";
    }

    #endregion

    #region Graphs

    protected override void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot)
    {
        var net = snapshot.Network;
        var accent = UIConstants.Accent.ToMarkup();
        var muted = UIConstants.MutedText.ToMarkup();

        panel.AddControl(
            Controls.Markup()
                .AddLine("")
                .AddLine($"[{accent} bold]═══ Network Visualization ═══[/]")
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
                .WithSmoothGradient(UIConstants.GradientNetUpload)
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
                .WithSmoothGradient(UIConstants.GradientNetDownload)
                .Build()
        );

        AddSectionSeparator(panel);

        panel.AddControl(
            Controls.Markup()
                .AddLine($"[{muted} bold]Network History[/] [grey50](↑ Upload  ↓ Download)[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );

        panel.AddControl(
            new SparklineBuilder()
                .WithName("netCombinedSparkline")
                .WithTitle("↓ Download  ↑ Upload")
                .WithTitleColor(UIConstants.MutedText)
                .WithTitlePosition(TitlePosition.Bottom)
                .WithHeight(UIConstants.NetworkCombinedSparklineHeight)
                .WithMaxValue(Math.Max(_peakDownMbps, 1.0))
                .WithSecondaryMaxValue(Math.Max(_peakUpMbps, 1.0))
                .WithGradient(UIConstants.SparkNetUpload)
                .WithSecondaryGradient(UIConstants.SparkNetDownload)
                .WithBackgroundColor(UIConstants.PanelBg)
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

                string ifaceNameDisplay = iface.InterfaceName.Length > UIConstants.InterfaceNameMaxLength
                    ? iface.InterfaceName.Substring(0, UIConstants.InterfaceNameTruncLength) + "..."
                    : iface.InterfaceName;

                panel.AddControl(
                    new SparklineBuilder()
                        .WithName($"net{iface.InterfaceName}Sparkline")
                        .WithTitle(ifaceNameDisplay)
                        .WithTitleColor(UIConstants.MutedText)
                        .WithTitlePosition(TitlePosition.Bottom)
                        .WithHeight(UIConstants.SparklineHeight)
                        .WithMaxValue(Math.Max(_peakDownMbps, 0.1))
                        .WithSecondaryMaxValue(Math.Max(_peakUpMbps, 0.1))
                        .WithGradient(UIConstants.SparkNetDownload)
                        .WithSecondaryGradient(UIConstants.SparkNetUpload)
                        .WithBackgroundColor(UIConstants.PanelBg)
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

    #endregion

    #region Update

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

            // Seed _knownInterfaces from the first history update so the initial
            // UpdateGraphControls call doesn't trigger a spurious rebuild.
            if (_knownInterfaces.Count == 0)
                _knownInterfaces = new HashSet<string>(net.PerInterfaceSamples.Select(i => i.InterfaceName));
        }
    }

    private static double RollingMax(HistoryTracker history, double floor = 1.0) =>
        Math.Max(floor, history.Data.Count > 0 ? history.Data.Max() : 0.0);

    protected override void UpdateGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var net = snapshot.Network;

        // Detect interface set changes and trigger a full panel rebuild when they occur
        var currentInterfaces = net.PerInterfaceSamples != null
            ? new HashSet<string>(net.PerInterfaceSamples.Select(i => i.InterfaceName))
            : new HashSet<string>();
        if (!currentInterfaces.SetEquals(_knownInterfaces))
        {
            _knownInterfaces = currentInterfaces;
            TriggerRebuild();
            return;
        }

        var rightPanel = FindGraphPanel(grid);
        if (rightPanel == null)
            return;

        double rollingUpMax   = RollingMax(_upHistory);
        double rollingDownMax = RollingMax(_downHistory);
        double barMax = Math.Ceiling(Math.Max(rollingUpMax, rollingDownMax) / 10.0) * 10;
        barMax = Math.Max(barMax, 1.0);

        var uploadBar = rightPanel.Children.FirstOrDefault(c => c.Name == "netUploadBar") as BarGraphControl;
        if (uploadBar != null) { uploadBar.Value = net.UpMbps; uploadBar.MaxValue = barMax; }

        var downloadBar = rightPanel.Children.FirstOrDefault(c => c.Name == "netDownloadBar") as BarGraphControl;
        if (downloadBar != null) { downloadBar.Value = net.DownMbps; downloadBar.MaxValue = barMax; }

        var combinedSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == "netCombinedSparkline") as SparklineControl;
        if (combinedSparkline != null)
        {
            combinedSparkline.SetBidirectionalData(_upHistory.DataMutable, _downHistory.DataMutable);
            combinedSparkline.MaxValue = rollingUpMax;
            combinedSparkline.SecondaryMaxValue = rollingDownMax;
        }

        if (net.PerInterfaceSamples != null)
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                var upData   = _perInterfaceUpHistory.GetMutable(iface.InterfaceName);
                var downData = _perInterfaceDownHistory.GetMutable(iface.InterfaceName);
                double ifaceUpMax   = Math.Max(0.1, upData.Count > 0 ? upData.Max() : 0.1);
                double ifaceDownMax = Math.Max(0.1, downData.Count > 0 ? downData.Max() : 0.1);

                var ifaceSparkline = rightPanel.Children.FirstOrDefault(c => c.Name == $"net{iface.InterfaceName}Sparkline") as SparklineControl;
                if (ifaceSparkline != null)
                {
                    ifaceSparkline.SetBidirectionalData(downData, upData);
                    ifaceSparkline.MaxValue = ifaceDownMax;
                    ifaceSparkline.SecondaryMaxValue = ifaceUpMax;
                }
            }
        }
    }

    protected override void UpdateLeftColumnText(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var net = snapshot.Network;

        double maxRate = Math.Max(Math.Max(_peakUpMbps, _peakDownMbps), 1.0);
        double barMax = Math.Ceiling(maxRate / 10) * 10;

        var upBar = FindMainWindow()?.FindControl<BarGraphControl>("leftNetUpBar");
        if (upBar != null) { upBar.Value = net.UpMbps; upBar.MaxValue = barMax; }

        var downBar = FindMainWindow()?.FindControl<BarGraphControl>("leftNetDownBar");
        if (downBar != null) { downBar.Value = net.DownMbps; downBar.MaxValue = barMax; }

        var summaryStats = FindMainWindow()?.FindControl<MarkupControl>("netSummaryStats");
        summaryStats?.SetContent(new List<string> { BuildNetSummaryStatsContent(net) });

        if (net.PerInterfaceSamples != null)
        {
            foreach (var iface in net.PerInterfaceSamples)
            {
                var ifaceMarkup = FindMainWindow()?.FindControl<MarkupControl>($"netIface_{iface.InterfaceName}");
                ifaceMarkup?.SetContent(new List<string> { BuildIfaceCardContent(iface) });
            }
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
