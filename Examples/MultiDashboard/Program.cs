using MultiDashboard.Windows;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Spectre.Console;

namespace MultiDashboard;

class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _helpWindow;
    private static WeatherDashboardWindow? _weatherWindow;
    private static SystemMonitorWindow? _systemWindow;
    private static StockTickerWindow? _stockWindow;
    private static NewsFeedWindow? _newsWindow;
    private static ClockWindow? _clockWindow;
    private static LogStreamWindow? _logWindow;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // 1. Initialize window system
            _windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    StatusBarOptions: new StatusBarOptions(
                        ShowTaskBar: true,
                        ShowStartButton: true
                    )
                ));
            _windowSystem.StatusBarStateService.TopStatus = "Multi-Dashboard Showcase - ConsoleEx Unique Capabilities Demo";
            _windowSystem.StatusBarStateService.BottomStatus = "F1-F6: Toggle Windows | ESC: Close Window | F10: Close All | Ctrl+C: Quit";

            // 2. Graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            // 3. Create and show help window (small, centered)
            CreateHelpWindow(_windowSystem);

            // 4. Setup global key handlers on help window
            if (_helpWindow != null)
            {
                _helpWindow.KeyPressed += (sender, e) =>
                {
                    HandleGlobalKeys(e);
                };
            }

            // 5. Auto-open all dashboard windows (immediate showcase)
            _weatherWindow = new WeatherDashboardWindow(_windowSystem);
            _weatherWindow.Show();

            _systemWindow = new SystemMonitorWindow(_windowSystem);
            _systemWindow.Show();

            _stockWindow = new StockTickerWindow(_windowSystem);
            _stockWindow.Show();

            _newsWindow = new NewsFeedWindow(_windowSystem);
            _newsWindow.Show();

            _clockWindow = new ClockWindow(_windowSystem);
            _clockWindow.Show();

            _logWindow = new LogStreamWindow(_windowSystem);
            _logWindow.Show();

            // 6. Run application
            await Task.Run(() => _windowSystem.Run());

            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            // Cleanup
            _weatherWindow?.Dispose();
            _systemWindow?.Dispose();
            _stockWindow?.Dispose();
            _newsWindow?.Dispose();
            _clockWindow?.Dispose();
            _logWindow?.Dispose();
        }
    }

    static void HandleGlobalKeys(KeyPressedEventArgs e)
    {
        switch (e.KeyInfo.Key)
        {
            case ConsoleKey.F1:
                ToggleWindow(ref _weatherWindow, () => new WeatherDashboardWindow(_windowSystem!));
                e.Handled = true;
                break;
            case ConsoleKey.F2:
                ToggleWindow(ref _systemWindow, () => new SystemMonitorWindow(_windowSystem!));
                e.Handled = true;
                break;
            case ConsoleKey.F3:
                ToggleWindow(ref _stockWindow, () => new StockTickerWindow(_windowSystem!));
                e.Handled = true;
                break;
            case ConsoleKey.F4:
                ToggleWindow(ref _newsWindow, () => new NewsFeedWindow(_windowSystem!));
                e.Handled = true;
                break;
            case ConsoleKey.F5:
                ToggleWindow(ref _clockWindow, () => new ClockWindow(_windowSystem!));
                e.Handled = true;
                break;
            case ConsoleKey.F6:
                ToggleWindow(ref _logWindow, () => new LogStreamWindow(_windowSystem!));
                e.Handled = true;
                break;
            case ConsoleKey.F10:
                CloseAllWindows();
                e.Handled = true;
                break;
        }
    }

    static void ToggleWindow<T>(ref T? window, Func<T> factory) where T : class, IDisposable
    {
        if (window != null)
        {
            // Close and dispose
            try
            {
                var hideMethod = window.GetType().GetMethod("Hide");
                hideMethod?.Invoke(window, null);
                window.Dispose();
                window = null;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Error closing window", ex, "Program");
            }
        }
        else
        {
            // Create and show
            try
            {
                window = factory();
                var showMethod = window.GetType().GetMethod("Show");
                showMethod?.Invoke(window, null);
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Error opening window", ex, "Program");
            }
        }
    }

    static void CloseAllWindows()
    {
        if (_weatherWindow != null)
        {
            _weatherWindow.Hide();
            _weatherWindow.Dispose();
            _weatherWindow = null;
        }

        if (_systemWindow != null)
        {
            _systemWindow.Hide();
            _systemWindow.Dispose();
            _systemWindow = null;
        }

        if (_stockWindow != null)
        {
            _stockWindow.Hide();
            _stockWindow.Dispose();
            _stockWindow = null;
        }

        if (_newsWindow != null)
        {
            _newsWindow.Hide();
            _newsWindow.Dispose();
            _newsWindow = null;
        }

        if (_clockWindow != null)
        {
            _clockWindow.Hide();
            _clockWindow.Dispose();
            _clockWindow = null;
        }

        if (_logWindow != null)
        {
            _logWindow.Hide();
            _logWindow.Dispose();
            _logWindow = null;
        }
    }

    static void CreateHelpWindow(ConsoleWindowSystem windowSystem)
    {
        _helpWindow = new WindowBuilder(windowSystem)
            .WithTitle("Multi-Dashboard Showcase")
            .WithSize(70, 30)
            .Centered()
            .WithColors(Spectre.Console.Color.Grey11, Spectre.Console.Color.White)
            .Build();

        _helpWindow.AddControl(
            MarkupControl
                .Create()
                .AddLine("")
                .AddLine("        [bold cyan]Welcome to the Multi-Dashboard Showcase![/]")
                .AddLine("")
                .AddLine("[yellow]What makes this unique:[/]")
                .AddLine("Each window has its own [bold]async update thread[/] running")
                .AddLine("independently. They update at [bold]different rates[/] without")
                .AddLine("blocking each other - something no other .NET console")
                .AddLine("framework can do while integrating Spectre.Console!")
                .AddLine("")
                .AddLine("[yellow]Dashboard Windows & Refresh Rates:[/]")
                .AddLine("[cyan]F1[/] - Weather Dashboard    ([green]5s[/])  - Async I/O simulation")
                .AddLine("[cyan]F2[/] - System Monitor       ([green]1s[/])  - Fast metrics update")
                .AddLine("[cyan]F3[/] - Stock Ticker         ([green]2s[/])  - Medium refresh")
                .AddLine("[cyan]F4[/] - News Feed           ([green]10s[/])  - Slow independence test")
                .AddLine("[cyan]F5[/] - Digital Clock        ([green]1s[/])  - Continuous time")
                .AddLine("[cyan]F6[/] - Log Stream        ([green]500ms[/]) - Stress test")
                .AddLine("")
                .AddLine("[yellow]Try this:[/]")
                .AddLine("1. Watch all windows update at their own pace")
                .AddLine("2. Close Weather (F1) - others keep updating!")
                .AddLine("3. Reopen Weather (F1) - it resumes fresh")
                .AddLine("4. Focus any window - all continue in background")
                .AddLine("")
                .AddLine("[yellow]Keyboard Shortcuts:[/]")
                .AddLine("[cyan]F1-F6[/]   - Toggle individual windows on/off")
                .AddLine("[cyan]ESC[/]     - Close active window")
                .AddLine("[cyan]F10[/]     - Close all dashboard windows")
                .AddLine("[cyan]Ctrl+C[/]  - Quit application")
                .AddLine("")
                .AddLine("[dim]All windows are now open. Try toggling them with F1-F6![/]")
                .Build()
        );

        windowSystem.AddWindow(_helpWindow);
    }
}
