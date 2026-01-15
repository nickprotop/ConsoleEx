using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

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

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // Set minimum log level to show all logs including Info and Debug
        _windowSystem.LogService.MinimumLevel = LogLevel.Trace;

        // Add LogViewerControl to display log entries
        var logViewer = new LogViewerControl(_windowSystem.LogService)
        {
            VerticalAlignment = VerticalAlignment.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FilterLevel = LogLevel.Trace,
            AutoScroll = true
        };
        _window.AddControl(logViewer);
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
