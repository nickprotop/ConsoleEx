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
            .WithColors(Color.White, Color.DarkBlue)
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

        // Forecast table placeholder (will be replaced in update loop)
        _window.AddControl(
            MarkupControl
                .Create()
                .AddLine("[grey]Forecast loading...[/]")
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

                // Update forecast table - replace control
                var oldForecast = window.FindControl<IWindowControl>("forecast");
                if (oldForecast != null)
                {
                    window.RemoveContent(oldForecast);
                }
                var table = BuildForecastTable(weather.Forecast);
                table.Name = "forecast";
                window.AddControl(table);

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

    private TableControl BuildForecastTable(List<DayForecast> forecast)
    {
        var builder = TableControl.Create()
            .AddColumn("[bold]Day[/]", Justify.Center, 12)
            .AddColumn("[bold]High[/]", Justify.Center, 8)
            .AddColumn("[bold]Low[/]", Justify.Center, 8)
            .AddColumn("[bold]Condition[/]", Justify.Left, 15);

        foreach (var day in forecast)
        {
            builder.AddRow(
                $"[cyan]{day.Day}[/]",
                $"[red]{day.High}°[/]",
                $"[blue]{day.Low}°[/]",
                $"[yellow]{day.Condition}[/]"
            );
        }

        return builder.Rounded().Build();
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
