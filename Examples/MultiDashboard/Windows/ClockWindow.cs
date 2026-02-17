using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Spectre.Console;

namespace MultiDashboard.Windows;

public class ClockWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private bool _disposed = false;

    public ClockWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        CreateWindow();
    }

    private void CreateWindow()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Digital Clock [1s refresh]")
            .WithSize(40, 12)
            .AtPosition(120, 2)
            .WithColors(Color.Cyan1, Color.Black)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // Time display
        _window.AddControl(
            MarkupControl
                .Create()
                .AddLine("[bold cyan]00:00:00[/]")
                .AddLine("[yellow]Loading...[/]")
                .AddLine("[white][/]")
                .WithName("timeDisplay")
                .Build()
        );
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;

                var timeControl = window.FindControl<MarkupControl>("timeDisplay");
                timeControl?.SetContent(new List<string>
                {
                    "",
                    $"[bold cyan on black]{now:HH:mm:ss}[/]",
                    "",
                    $"       [yellow]{now:dddd}[/]",
                    $"   [white]{now:MMMM dd, yyyy}[/]",
                    ""
                });

                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Clock update error", ex, "Clock");
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
