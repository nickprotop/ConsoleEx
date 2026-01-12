using MultiDashboard.Services;
using Microsoft.Extensions.Logging;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using Spectre.Console;

namespace MultiDashboard.Windows;

public class LogStreamWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private readonly LogStreamService _logService;
    private bool _disposed = false;

    public LogStreamWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _logService = new LogStreamService();
        CreateWindow();
    }

    private void CreateWindow()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Log Stream [500ms refresh]")
            .WithSize(100, 12)
            .AtPosition(20, 36)
            .WithColors(Color.Black, Color.Grey93)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        int counter = 0;
        var levels = new[] { LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Debug };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var level = levels[counter % 4];
                var message = _logService.GenerateRandomLog();

                _windowSystem.LogService.Log(level, message, "MultiDashboard");

                counter++;
                await Task.Delay(500, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Log stream error", ex, "LogStream");
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
