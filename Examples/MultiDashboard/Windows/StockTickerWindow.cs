using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Spectre.Console;

namespace MultiDashboard.Windows;

public class StockTickerWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private readonly StockService _stockService;
    private bool _disposed = false;

    public StockTickerWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _stockService = new StockService();
        CreateWindow();
    }

    private void CreateWindow()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Stock Ticker [2s refresh]")
            .WithSize(52, 14)
            .AtPosition(2, 20)
            .WithColors(Color.Black, Color.White)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // Stock table
        _window.AddControl(
            SpectreRenderableControl
                .Create()
                .WithRenderable(new Panel("[grey]Loading stock data...[/]"))
                .WithName("stockTable")
                .Build()
        );
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var quotes = _stockService.GetLatestQuotes();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]Symbol[/]").Centered())
                    .AddColumn(new TableColumn("[bold]Price[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]Change[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]%[/]").RightAligned());

                foreach (var quote in quotes)
                {
                    var color = quote.Change > 0 ? "green" :
                               quote.Change < 0 ? "red" : "grey";

                    table.AddRow(
                        $"[bold]{quote.Symbol}[/]",
                        $"${quote.Price:F2}",
                        $"[{color}]{quote.Change:+0.00;-0.00}[/]",
                        $"[{color}]{quote.ChangePercent:+0.00;-0.00}%[/]"
                    );
                }

                window.FindControl<SpectreRenderableControl>("stockTable")
                      ?.SetRenderable(table);

                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Stock ticker update error", ex, "StockTicker");
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
        if (!_disposed)
        {
            _disposed = true;
            _window?.Close();
        }
    }
}
