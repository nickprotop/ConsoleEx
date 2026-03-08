using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

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
            .WithColors(Color.Grey93, Color.Grey11)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // CPU label
        _window.AddControl(MarkupControl.Create()
            .AddLine("[bold]CPU Usage[/]")
            .WithName("cpuLabel")
            .Build());

        // CPU bars
        _window.AddControl(new BarGraphBuilder()
            .WithLabel("User").WithFilledColor(Color.Green).WithMaxValue(100).WithName("cpuUser").Build());
        _window.AddControl(new BarGraphBuilder()
            .WithLabel("System").WithFilledColor(Color.Blue).WithMaxValue(100).WithName("cpuSystem").Build());
        _window.AddControl(new BarGraphBuilder()
            .WithLabel("IO Wait").WithFilledColor(Color.Yellow).WithMaxValue(100).WithName("cpuIo").Build());

        // Memory label
        _window.AddControl(MarkupControl.Create()
            .AddLine("[bold]Memory Usage[/]")
            .WithName("memLabel")
            .Build());

        // Memory bars
        _window.AddControl(new BarGraphBuilder()
            .WithLabel("Used").WithFilledColor(Color.Red).WithMaxValue(100).WithName("memUsed").Build());
        _window.AddControl(new BarGraphBuilder()
            .WithLabel("Cached").WithFilledColor(Color.Aqua).WithMaxValue(100).WithName("memCached").Build());
        _window.AddControl(new BarGraphBuilder()
            .WithLabel("Free").WithFilledColor(Color.Grey).WithMaxValue(100).WithName("memFree").Build());

        // Disk stats
        _window.AddControl(
            MarkupControl.Create()
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

                // Update CPU bars
                window.FindControl<BarGraphControl>("cpuUser")!.Value = stats.CpuUser;
                window.FindControl<BarGraphControl>("cpuSystem")!.Value = stats.CpuSystem;
                window.FindControl<BarGraphControl>("cpuIo")!.Value = stats.CpuIo;

                // Update memory bars
                window.FindControl<BarGraphControl>("memUsed")!.Value = stats.MemoryUsed;
                window.FindControl<BarGraphControl>("memCached")!.Value = stats.MemoryCached;
                window.FindControl<BarGraphControl>("memFree")!.Value = 100 - stats.MemoryUsed - stats.MemoryCached;

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
