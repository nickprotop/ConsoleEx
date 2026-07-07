using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace MultiDashboard.Windows;

/// <summary>
/// The live Log Stream dashboard. A normal, movable (non-maximized) window that renders a
/// continuously scrolling, virtualized feed via the framework-native <see cref="LogViewerControl"/>,
/// which mirrors the system <c>LogService</c> as a live, filterable table. The viewer is created once
/// and fed on this window's own async thread — the loop only pushes new entries into the system log
/// (via <c>LogService.Log</c>), it never rebuilds the control. Implements <see cref="IDashboardWindow"/>
/// so the Control Center can toggle it.
/// </summary>
public class LogStreamWindow : IDashboardWindow
{
    private const int UpdateIntervalMs = 500;

    private readonly ConsoleWindowSystem _windowSystem;
    private readonly LogStreamService _logService = new();
    private Window? _window;
    private bool _disposed = false;

    public LogStreamWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        CreateWindow();
    }

    /// <summary>The built window (a normal, movable window — never maximized).</summary>
    public Window? Window => _window;

    private void CreateWindow()
    {
        // Ensure the system log surfaces every level so the viewer shows Info/Debug too.
        _windowSystem.LogService.MinimumLevel = LogLevel.Trace;

        // The live, virtualized log table — created once, mirrors the system LogService.
        var logViewer = new LogViewerControl(_windowSystem.LogService)
        {
            VerticalAlignment = VerticalAlignment.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FilterLevel = LogLevel.Trace,
            AutoScroll = true
        };

        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Log Stream")
            .WithName("logstream")
            .WithBackgroundGradient(ColorGradient.FromColors(new Color(16, 42, 66), new Color(9, 24, 40)), GradientDirection.Vertical)
            .WithSize(48, 16)
            .AtPosition(2, 29)
            .AddControl(logViewer)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();
    }

    /// <summary>
    /// Async feed: every <see cref="UpdateIntervalMs"/> ms generates a random log line and pushes it
    /// into the system <c>LogService</c> at a rotating level. The <see cref="LogViewerControl"/> is
    /// subscribed to that service, so it updates itself — the loop touches no control state directly.
    /// </summary>
    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        int counter = 0;
        var levels = new[] { LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Debug };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var level = levels[counter % levels.Length];
                var message = _logService.GenerateRandomLog();

                _windowSystem.LogService.Log(level, message, "MultiDashboard");

                counter++;
                await Task.Delay(UpdateIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
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
