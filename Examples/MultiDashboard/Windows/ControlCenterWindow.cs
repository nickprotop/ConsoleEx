using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace MultiDashboard.Windows;

/// <summary>
/// The desktop's Control Center hub. Hosts a NavigationView pane whose items list the available
/// dashboards. The <b>Overview</b> item shows a live compact panel (a CPU sparkline plus a ticking
/// clock) fed from this window's own async thread; selecting <b>Metrics</b>, <b>Markets</b> or
/// <b>Log Stream</b> toggles that dashboard window on the desktop. This is a normal, movable
/// (non-maximized) window pinned near the top-left corner.
/// </summary>
public class ControlCenterWindow : IDashboardWindow
{
    private const int UpdateIntervalMs = 500;

    // Nav-item tags used to identify which dashboard toggle to fire on invoke.
    private const string TagMetrics = "metrics";
    private const string TagMarkets = "markets";
    private const string TagLogStream = "logstream";

    private readonly ConsoleWindowSystem _windowSystem;
    private readonly SystemStatsService _stats = new();
    private Window? _window;
    private bool _disposed = false;

    public ControlCenterWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        CreateWindow();
    }

    /// <summary>The built window (a normal, movable window — never maximized).</summary>
    public Window? Window => _window;

    private void CreateWindow()
    {
        var metricsItem = new NavigationItem("Metrics", subtitle: "CPU · memory · disk · network") { Tag = TagMetrics };
        var marketsItem = new NavigationItem("Markets", subtitle: "Live tickers & movers") { Tag = TagMarkets };
        var logItem = new NavigationItem("Log Stream", subtitle: "Streaming activity log") { Tag = TagLogStream };

        var nav = Controls.NavigationView()
            .WithPaneHeader("[gradient=cool]  Control Center[/]")
            // Lowered so the pane shows full labels (expanded) at this window's content width instead of
            // collapsing to icon-only. Default is 80; the Control Center is a compact hub window.
            .WithExpandedThreshold(46)
            .AddHeader("Dashboards", Color.Cyan1, header => header
                .AddItem("Overview", subtitle: "Live at-a-glance", content: BuildOverview)
                // Each dashboard item carries a content page (so the content pane switches when it is
                // selected in the nav) AND toggles its floating window on activation (ItemInvoked, below).
                .AddItem(metricsItem, panel => BuildDashboardPage(panel,
                    "Metrics", "Real-time system metrics — CPU, memory, network sparklines, per-core load bars, and a latency line graph.",
                    "Open / focus Metrics", Program.ToggleMetrics))
                .AddItem(marketsItem, panel => BuildDashboardPage(panel,
                    "Markets", "Live tickers in an ITableDataSource data grid (sortable) beside a multi-series price line graph.",
                    "Open / focus Markets", Program.ToggleMarkets))
                .AddItem(logItem, panel => BuildDashboardPage(panel,
                    "Log Stream", "A live, auto-scrolling activity log streamed on the window's own async thread.",
                    "Open / focus Log Stream", Program.ToggleLogStream)))
            .WithAlignment(HorizontalAlignment.Stretch)
            .Fill()
            .Build();

        // ItemInvoked fires on Enter/click (not on mere selection navigation). Toggle the matching
        // dashboard window based on the invoked item's tag.
        nav.ItemInvoked += (sender, args) =>
        {
            switch (args.NewItem?.Tag as string)
            {
                case TagMetrics:
                    Program.ToggleMetrics();
                    break;
                case TagMarkets:
                    Program.ToggleMarkets();
                    break;
                case TagLogStream:
                    Program.ToggleLogStream();
                    break;
            }
        };

        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Control Center")
            .WithName("controlcenter")
            .WithBackgroundGradient(ColorGradient.FromColors(new Color(16, 42, 66), new Color(9, 24, 40)), GradientDirection.Vertical)
            .WithSize(60, 26)
            .AtPosition(2, 2)
            .AddControl(nav)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();
    }

    /// <summary>
    /// Builds the live Overview page: a named CPU sparkline plus a ticking clock label, both fed
    /// by <see cref="UpdateLoopAsync"/>.
    /// </summary>
    private void BuildOverview(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup("[bold]System at a glance[/]")
            .WithMargin(1, 1, 1, 0)
            .Build());

        panel.AddControl(Controls.Markup("[dim]CPU load[/]")
            .WithMargin(1, 1, 1, 0)
            .Build());

        panel.AddControl(Controls.Sparkline()
            .WithMode(SparklineMode.Block)
            .WithHeight(3)
            .WithName("ovCpu")
            .WithMargin(1, 0, 1, 0)
            .Build());

        panel.AddControl(Controls.Markup("[dim]Memory used[/]")
            .WithMargin(1, 1, 1, 0)
            .Build());

        panel.AddControl(Controls.Sparkline()
            .WithMode(SparklineMode.Block)
            .WithHeight(3)
            .WithName("ovMem")
            .WithMargin(1, 0, 1, 0)
            .Build());

        panel.AddControl(Controls.Markup("[bold]--:--:--[/]")
            .WithName("ovClock")
            .WithMargin(1, 1, 1, 1)
            .Build());
    }

    /// <summary>
    /// Content page for a dashboard nav item — shown in the content pane when the item is selected, so the
    /// pane switches on navigation. The page describes the dashboard and carries a real button that
    /// opens / focuses its floating window on the desktop, so the Control Center acts as a launcher.
    /// </summary>
    private void BuildDashboardPage(ScrollablePanelControl panel, string title, string description,
        string buttonText, Action openAction)
    {
        panel.AddControl(Controls.Markup($"[gradient=cool]{title}[/]")
            .WithMargin(1, 1, 1, 1)
            .Build());

        panel.AddControl(Controls.Markup(description)
            .WithMargin(1, 0, 1, 1)
            .Build());

        panel.AddControl(Controls.Button($"  {buttonText}  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(1, 1, 1, 0)
            .OnClick((_, _, _) => openAction())
            .Build());

        panel.AddControl(Controls.Markup("[dim]Or press [/][bold]Enter[/][dim] on the nav item to toggle it.[/]")
            .WithMargin(1, 1, 1, 0)
            .Build());
    }

    /// <summary>
    /// Async feed for the Overview page: pushes a live sample into each sparkline and ticks the
    /// clock every <see cref="UpdateIntervalMs"/> ms while the window is open.
    /// </summary>
    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(UpdateIntervalMs, ct);

            var sample = _stats.GetCurrentStats();
            var cpu = sample.CpuUser + sample.CpuSystem + sample.CpuIo;

            window.FindControl<SparklineControl>("ovCpu")?.AddDataPoint(cpu);
            window.FindControl<SparklineControl>("ovMem")?.AddDataPoint(sample.MemoryUsed);
            window.FindControl<MarkupControl>("ovClock")?
                .SetContent(new List<string> { $"[bold]{DateTime.Now:HH:mm:ss}[/]" });
        }
    }

    public void Show()
    {
        if (_window != null)
            _windowSystem.AddWindow(_window);
    }

    public void Hide()
    {
        if (_window != null)
            _windowSystem.CloseWindow(_window);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _window?.Close();
        _window = null;
        _disposed = true;
    }
}
