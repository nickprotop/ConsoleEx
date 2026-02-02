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

        // Stock table placeholder (will be replaced in update loop)
        _window.AddControl(
            MarkupControl
                .Create()
                .AddLine("[grey]Loading stock data...[/]")
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

                var builder = TableControl.Create()
                    .AddColumn("[bold]Symbol[/]", Justify.Center, 10)
                    .AddColumn("[bold]Price[/]", Justify.Right, 10)
                    .AddColumn("[bold]Change[/]", Justify.Right, 10)
                    .AddColumn("[bold]%[/]", Justify.Right, 10);

                foreach (var quote in quotes)
                {
                    var color = quote.Change > 0 ? "green" :
                               quote.Change < 0 ? "red" : "grey";

                    builder.AddRow(
                        $"[bold]{quote.Symbol}[/]",
                        $"${quote.Price:F2}",
                        $"[{color}]{quote.Change:+0.00;-0.00}[/]",
                        $"[{color}]{quote.ChangePercent:+0.00;-0.00}%[/]"
                    );
                }

                // Replace the stock table control
                var oldTable = window.FindControl<IWindowControl>("stockTable");
                if (oldTable != null)
                {
                    window.RemoveContent(oldTable);
                }
                var table = builder.Rounded().Build();
                table.Name = "stockTable";
                window.AddControl(table);

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
