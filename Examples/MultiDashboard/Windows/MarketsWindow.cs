using MultiDashboard.Data;
using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace MultiDashboard.Windows;

/// <summary>
/// The real Markets dashboard. A normal, movable (non-maximized) window that pairs a multi-series
/// Braille <see cref="LineGraphControl"/> of price trends (left) with a virtualized
/// <see cref="TableControl"/> bound to a live <see cref="MarketDataSource"/> (right). Both are created
/// once; the async feed loop ticks <see cref="MarketDataSource.Refresh"/> (which raises
/// <c>CollectionChanged</c> so the grid re-renders and re-sorts) and pushes each tracked symbol's price
/// into the line graph. Implements <see cref="IDashboardWindow"/> so the Control Center can toggle it.
/// </summary>
public class MarketsWindow : IDashboardWindow
{
    private const int UpdateIntervalMs = 500;

    // The symbols plotted on the line graph (a subset of StockService's roster).
    private static readonly string[] TrackedSymbols = { "AAPL", "MSFT", "GOOGL", "NVDA" };

    private readonly ConsoleWindowSystem _windowSystem;
    private readonly StockService _stockService = new();
    private readonly MarketDataSource _marketSource;
    private Window? _window;
    private bool _disposed = false;

    public MarketsWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _marketSource = new MarketDataSource(_stockService);
        CreateWindow();
    }

    /// <summary>The built window (a normal, movable window — never maximized).</summary>
    public Window? Window => _window;

    private void CreateWindow()
    {
        // ── Header (row 0) — spans both columns; [gradient=cool] accent over the ocean theme. ─────
        var header = Controls.Markup(
                "[gradient=cool]  Markets[/]   [dim]price trends · live tickers · click a header to sort[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        // ── Price trends LineGraph (row 1, col 0) — multi-series, Braille, fed live. ───────────────
        var prices = Controls.LineGraph()
            .WithTitle("Price trends", Color.Cyan1)
            .WithMode(LineGraphMode.Braille)
            .AddSeries("AAPL", Color.Cyan1, "cool")
            .AddSeries("MSFT", Color.Green)
            .AddSeries("GOOGL", Color.Yellow)
            .AddSeries("NVDA", Color.Fuchsia)
            .WithYAxisLabels()
            .WithBaseline()
            .WithName("prices")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithMargin(1, 0, 1, 0)
            .Build();

        // ── Ticker datagrid (row 1, col 1) — virtualized, sortable, live-updating rows. ────────────
        var grid = Controls.Table()
            .WithDataSource(_marketSource)
            .WithName("grid")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var layout = Controls.Grid()
            .Columns(GridLength.Star(1), GridLength.Star(1))
            .Rows(GridLength.Auto(), GridLength.Star(1))
            .RowGap(1)
            .ColumnGap(2)
            .WithColorRole(ColorRole.Primary)
            .WithPadding(1, 0, 1, 0)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Place(header, 0, 0, colSpan: 2)
            .Place(prices, 1, 0)
            .Place(grid, 1, 1)
            .Build();

        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Markets")
            .WithName("markets")
            .WithBackgroundGradient(ColorGradient.FromColors(new Color(16, 42, 66), new Color(9, 24, 40)), GradientDirection.Vertical)
            .WithSize(72, 20)
            .AtPosition(64, 23)
            .AddControl(layout)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();
    }

    /// <summary>
    /// Async feed: every <see cref="UpdateIntervalMs"/> ms refreshes the data source (which raises
    /// <c>CollectionChanged</c> so the bound grid re-renders and honours the active sort) and pushes
    /// each tracked symbol's fresh price into the named line graph. <c>Refresh</c>, <c>AddDataPoint</c>
    /// and the CollectionChanged path are self-invalidating, so no <c>EnqueueOnUIThread</c> is needed.
    /// </summary>
    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(UpdateIntervalMs, ct);

            _marketSource.Refresh();

            var graph = window.FindControl<LineGraphControl>("prices");
            if (graph == null)
                continue;

            foreach (var quote in _marketSource.Quotes)
            {
                if (Array.IndexOf(TrackedSymbols, quote.Symbol) >= 0)
                    graph.AddDataPoint(quote.Symbol, quote.Price);
            }
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
