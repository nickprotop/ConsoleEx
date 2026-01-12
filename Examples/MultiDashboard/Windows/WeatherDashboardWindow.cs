using MultiDashboard.Models;
using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Spectre.Console;

namespace MultiDashboard.Windows;

public class WeatherDashboardWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private readonly WeatherService _weatherService;
    private bool _disposed = false;

    public WeatherDashboardWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _weatherService = new WeatherService();
        CreateWindow();
    }

    private void CreateWindow()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Weather Dashboard [5s refresh]")
            .WithSize(50, 16)
            .AtPosition(2, 2)
            .WithColors(Color.DarkBlue, Color.White)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // Current weather panel
        _window.AddControl(
            SpectreRenderableControl
                .Create()
                .WithRenderable(new Panel("[yellow]Loading weather data...[/]"))
                .WithName("currentWeather")
                .Build()
        );

        // Forecast table
        _window.AddControl(
            SpectreRenderableControl
                .Create()
                .WithRenderable(new Panel("[grey]Forecast loading...[/]"))
                .WithName("forecast")
                .Build()
        );

        // Location info
        _window.AddControl(
            MarkupControl
                .Create()
                .AddLine("[dim]San Francisco, CA[/]")
                .WithName("location")
                .Build()
        );
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Simulate API call with delay
                var weather = await _weatherService.GetWeatherAsync();

                // Update current weather panel
                var panel = new Panel(
                    $"[yellow]Temperature:[/] {weather.Temp}°F\n" +
                    $"[cyan]Conditions:[/] {weather.Condition}\n" +
                    $"[grey]Humidity:[/] {weather.Humidity}%"
                )
                {
                    Border = BoxBorder.Rounded,
                    Header = new PanelHeader(" Current Weather ")
                };

                window.FindControl<SpectreRenderableControl>("currentWeather")
                      ?.SetRenderable(panel);

                // Update forecast table
                var table = BuildForecastTable(weather.Forecast);
                window.FindControl<SpectreRenderableControl>("forecast")
                      ?.SetRenderable(table);

                // Update location with timestamp
                var locationControl = window.FindControl<MarkupControl>("location");
                locationControl?.SetContent(new List<string>
                {
                    $"[dim]San Francisco, CA • Updated: {DateTime.Now:HH:mm:ss}[/]"
                });

                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Weather update error", ex, "WeatherDashboard");
            }
        }
    }

    private Table BuildForecastTable(List<DayForecast> forecast)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Day[/]").Centered())
            .AddColumn(new TableColumn("[bold]High[/]").Centered())
            .AddColumn(new TableColumn("[bold]Low[/]").Centered())
            .AddColumn(new TableColumn("[bold]Condition[/]"));

        foreach (var day in forecast)
        {
            table.AddRow(
                $"[cyan]{day.Day}[/]",
                $"[red]{day.High}°[/]",
                $"[blue]{day.Low}°[/]",
                $"[yellow]{day.Condition}[/]"
            );
        }

        return table;
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
