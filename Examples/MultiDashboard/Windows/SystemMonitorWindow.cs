using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Spectre.Console;

namespace MultiDashboard.Windows;

public class SystemMonitorWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private readonly SystemStatsService _statsService;
    private bool _disposed = false;

    public SystemMonitorWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _statsService = new SystemStatsService();
        CreateWindow();
    }

    private void CreateWindow()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("System Monitor [1s refresh]")
            .WithSize(60, 18)
            .AtPosition(54, 2)
            .WithColors(Color.Grey11, Color.Grey93)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // CPU chart
        _window.AddControl(
            SpectreRenderableControl
                .Create()
                .WithRenderable(new Panel("[grey]Initializing CPU monitor...[/]"))
                .WithName("cpuChart")
                .Build()
        );

        // Memory chart
        _window.AddControl(
            SpectreRenderableControl
                .Create()
                .WithRenderable(new Panel("[grey]Initializing memory monitor...[/]"))
                .WithName("memChart")
                .Build()
        );

        // Disk stats
        _window.AddControl(
            MarkupControl
                .Create()
                .AddLine("[dim]Disk I/O: Loading...[/]")
                .WithName("diskStats")
                .Build()
        );
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var stats = _statsService.GetCurrentStats();

                // Update CPU chart
                var cpuChart = new BarChart()
                    .Label("[bold]CPU Usage[/]")
                    .WithMaxValue(100)
                    .AddItem("User", stats.CpuUser, Color.Green)
                    .AddItem("System", stats.CpuSystem, Color.Blue)
                    .AddItem("IO Wait", stats.CpuIo, Color.Yellow);

                window.FindControl<SpectreRenderableControl>("cpuChart")
                      ?.SetRenderable(cpuChart);

                // Update memory chart
                var memChart = new BarChart()
                    .Label("[bold]Memory Usage[/]")
                    .WithMaxValue(100)
                    .AddItem("Used", stats.MemoryUsed, Color.Red)
                    .AddItem("Cached", stats.MemoryCached, Color.Aqua)
                    .AddItem("Free", 100 - stats.MemoryUsed - stats.MemoryCached, Color.Grey);

                window.FindControl<SpectreRenderableControl>("memChart")
                      ?.SetRenderable(memChart);

                // Update disk stats
                var diskControl = window.FindControl<MarkupControl>("diskStats");
                diskControl?.SetContent(new List<string>
                {
                    $"[dim]Disk I/O - Read: [green]{stats.DiskReadMbps}[/] MB/s | Write: [yellow]{stats.DiskWriteMbps}[/] MB/s[/]"
                });

                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("System monitor update error", ex, "SystemMonitor");
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
