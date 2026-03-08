using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

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

        var newsPanel = new ScrollablePanelControl
        {
            VerticalAlignment = VerticalAlignment.Fill,
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
                    var existingControls = panel.Children.ToList();
                    foreach (var control in existingControls)
                    {
                        panel.RemoveControl(control);
                    }

                    foreach (var item in news)
                    {
                        var newsPanel = PanelControl.Create()
                            .WithContent(
                                $"[bold yellow]{item.Headline}[/]\n" +
                                $"[grey]{item.Source} • {item.Time:HH:mm}[/]")
                            .WithBorderStyle(BorderStyle.Rounded)
                            .WithBorderColor(Color.Grey35)
                            .WithPadding(1, 0, 1, 0)
                            .Build();

                        panel.AddControl(newsPanel);
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
