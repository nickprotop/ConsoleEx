using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace MultiDashboard.Windows;

public class NewsFeedWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private readonly NewsService _newsService;
    private bool _disposed = false;

    public NewsFeedWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _newsService = new NewsService();
        CreateWindow();
    }

    private void CreateWindow()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("News Feed [10s refresh]")
            .WithSize(62, 20)
            .AtPosition(56, 20)
            .WithColors(Color.White, Color.Grey15)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();

        SetupControls();
    }

    private void SetupControls()
    {
        if (_window == null) return;

        // Create scrollable panel for news items
        var newsPanel = new ScrollablePanelControl
        {
            VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment.Fill,
            Name = "newsPanel"
        };
        _window.AddControl(newsPanel);
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var news = _newsService.GetLatestNews();
                var panel = window.FindControl<ScrollablePanelControl>("newsPanel");

                if (panel != null)
                {
                    // Remove existing controls one by one
                    var existingControls = panel.Children.ToList();
                    foreach (var control in existingControls)
                    {
                        panel.RemoveControl(control);
                    }

                    // Add new news items
                    foreach (var item in news)
                    {
                        var newsPanel = new Panel(
                            $"[bold yellow]{item.Headline}[/]\n" +
                            $"[grey]{item.Source} • {item.Time:HH:mm}[/]"
                        )
                        {
                            Border = BoxBorder.Rounded,
                            BorderStyle = new Style(Color.Grey35),
                            Padding = new Padding(1, 0, 1, 0)
                        };

                        var newsControl = SpectreRenderableControl
                            .Create()
                            .WithRenderable(newsPanel)
                            .Build();

                        panel.AddControl(newsControl);
                    }

                    panel.ScrollToBottom();
                    panel.Invalidate(true);
                }

                await Task.Delay(10000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("News feed update error", ex, "NewsFeed");
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
