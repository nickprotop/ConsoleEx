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

internal enum ResponsiveLayoutMode { Wide, Narrow }

internal abstract class BaseResponsiveTab : ITab
{
    protected readonly ConsoleWindowSystem WindowSystem;
    protected readonly ISystemStatsProvider Stats;
    protected ResponsiveLayoutMode _currentLayout = ResponsiveLayoutMode.Wide;

    protected BaseResponsiveTab(ConsoleWindowSystem windowSystem, ISystemStatsProvider stats)
    {
        WindowSystem = windowSystem;
        Stats = stats;
    }

    #region Abstract Members

    public abstract string Name { get; }
    public abstract string PanelControlName { get; }
    protected abstract int LayoutThresholdWidth { get; }
    protected abstract List<string> BuildTextContent(SystemSnapshot snapshot);
    protected abstract void BuildGraphsContent(ScrollablePanelControl panel, SystemSnapshot snapshot);
    protected abstract void UpdateHistory(SystemSnapshot snapshot);
    protected abstract void UpdateGraphControls(HorizontalGridControl grid, SystemSnapshot snapshot);

    #endregion

    #region ITab Implementation

    public virtual IWindowControl BuildPanel(SystemSnapshot initialSnapshot, int windowWidth)
    {
        _currentLayout = windowWidth >= LayoutThresholdWidth
            ? ResponsiveLayoutMode.Wide
            : ResponsiveLayoutMode.Narrow;

        return _currentLayout == ResponsiveLayoutMode.Wide
            ? BuildWideGrid(initialSnapshot)
            : BuildNarrowGrid(initialSnapshot);
    }

    public void UpdatePanel(SystemSnapshot snapshot)
    {
        var grid = FindPanel();
        if (grid == null)
            return;

        UpdateHistory(snapshot);
        UpdateGraphControls(grid, snapshot);

        UpdateLeftColumnText(grid, snapshot);
    }

    public void HandleResize(int newWidth, int newHeight)
    {
        var grid = FindPanel();
        if (grid == null || !grid.Visible)
            return;

        var desired = newWidth >= LayoutThresholdWidth
            ? ResponsiveLayoutMode.Wide
            : ResponsiveLayoutMode.Narrow;

        if (desired == _currentLayout)
            return;

        _currentLayout = desired;
        RebuildColumns(grid);
    }

    #endregion

    #region Grid Building

    private HorizontalGridControl BuildWideGrid(SystemSnapshot snapshot)
    {
        var lines = BuildTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName(PanelControlName)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false)
            .Column(col =>
            {
                col.Width(UIConstants.FixedTextColumnWidth);
                var leftPanel = BuildScrollablePanel();
                AddMarkupLines(leftPanel, lines);
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
                BuildGraphsContent(rightPanel, snapshot);
                col.Add(rightPanel);
            })
            .Build();

        grid.BackgroundColor = UIConstants.WindowBackground;
        grid.ForegroundColor = UIConstants.WindowForeground;
        return grid;
    }

    private HorizontalGridControl BuildNarrowGrid(SystemSnapshot snapshot)
    {
        var lines = BuildTextContent(snapshot);

        var grid = Controls
            .HorizontalGrid()
            .WithName(PanelControlName)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Visible(false)
            .Column(col =>
            {
                var scrollPanel = BuildScrollablePanel();
                AddMarkupLines(scrollPanel, lines);
                AddNarrowSeparator(scrollPanel);
                BuildGraphsContent(scrollPanel, snapshot);
                col.Add(scrollPanel);
            })
            .Build();

        grid.BackgroundColor = UIConstants.WindowBackground;
        grid.ForegroundColor = UIConstants.WindowForeground;
        return grid;
    }

    #endregion

    #region Column Rebuild on Resize

    private void RebuildColumns(HorizontalGridControl grid)
    {
        var snapshot = GetLatestSnapshot();

        for (int i = grid.Columns.Count - 1; i >= 0; i--)
            grid.RemoveColumn(grid.Columns[i]);

        if (_currentLayout == ResponsiveLayoutMode.Wide)
            BuildWideColumns(grid, snapshot);
        else
            BuildNarrowColumns(grid, snapshot);

        FindMainWindow()?.ForceRebuildLayout();
        FindMainWindow()?.Invalidate(true);
    }

    private void BuildWideColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var lines = BuildTextContent(snapshot);

        var leftPanel = BuildScrollablePanel();
        AddMarkupLines(leftPanel, lines);

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

    private void BuildNarrowColumns(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        var lines = BuildTextContent(snapshot);

        var scrollPanel = BuildScrollablePanel();
        AddMarkupLines(scrollPanel, lines);
        AddNarrowSeparator(scrollPanel);
        BuildGraphsContent(scrollPanel, snapshot);

        var col = new ColumnContainer(grid);
        col.AddContent(scrollPanel);
        grid.AddColumn(col);
    }

    #endregion

    #region Update Helpers

    protected virtual void UpdateLeftColumnText(HorizontalGridControl grid, SystemSnapshot snapshot)
    {
        if (grid.Columns.Count == 0)
            return;

        var leftCol = grid.Columns[0];
        var leftPanel = leftCol.Contents.FirstOrDefault() as ScrollablePanelControl;
        if (leftPanel == null || leftPanel.Children.Count == 0)
            return;

        var markup = leftPanel.Children[0] as MarkupControl;
        if (markup != null)
        {
            var lines = BuildTextContent(snapshot);
            markup.SetContent(lines);
        }
    }

    protected ScrollablePanelControl? FindGraphPanel(HorizontalGridControl grid)
    {
        if (grid.Columns.Count >= 3)
        {
            return grid.Columns[2].Contents.FirstOrDefault() as ScrollablePanelControl;
        }
        if (grid.Columns.Count >= 1)
        {
            return grid.Columns[0].Contents.FirstOrDefault() as ScrollablePanelControl;
        }
        return null;
    }

    #endregion

    #region Shared Utilities

    internal static ScrollablePanelControl BuildScrollablePanel()
    {
        var panel = Controls
            .ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        panel.BackgroundColor = UIConstants.WindowBackground;
        panel.ForegroundColor = UIConstants.WindowForeground;
        return panel;
    }

    internal static void AddMarkupLines(ScrollablePanelControl panel, List<string> lines)
    {
        var markup = Controls.Markup();
        foreach (var line in lines)
            markup = markup.AddLine(line);
        panel.AddControl(markup.WithAlignment(HorizontalAlignment.Left).Build());
    }

    internal static void AddNarrowSeparator(ScrollablePanelControl panel)
    {
        panel.AddControl(
            Controls.RuleBuilder()
                .WithColor(UIConstants.RuleColor)
                .Build()
        );
    }

    protected static void AddSectionSeparator(ScrollablePanelControl panel)
    {
        panel.AddControl(
            Controls.RuleBuilder()
                .WithColor(UIConstants.RuleColor)
                .Build()
        );
    }

    protected static void AddSectionHeader(ScrollablePanelControl panel, string title)
    {
        panel.AddControl(
            Controls.Markup()
                .AddLine($"[grey70 bold]{title}[/]")
                .WithAlignment(HorizontalAlignment.Left)
                .WithMargin(2, 0, 2, 1)
                .Build()
        );
    }

    #endregion

    #region Window Access

    private HorizontalGridControl? FindPanel()
    {
        return FindMainWindow()?.FindControl<HorizontalGridControl>(PanelControlName);
    }

    protected Window? FindMainWindow()
    {
        return WindowSystem.Windows.Values.FirstOrDefault();
    }

    private SystemSnapshot GetLatestSnapshot()
    {
        return Stats.ReadSnapshot();
    }

    #endregion
}
