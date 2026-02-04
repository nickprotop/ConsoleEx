// -----------------------------------------------------------------------
// HighFreqDemo - Multi-frequency update showcase
// Demonstrates controls updating at different rates with timers
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace HighFreqDemo;

class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _mainWindow;
    private static readonly Random _random = new();

    // Data history for sparklines
    private static readonly List<double> _cpuHistory = new();
    private static readonly List<double> _memoryHistory = new();
    private static readonly List<double> _networkUpHistory = new();
    private static readonly List<double> _networkDownHistory = new();
    private static readonly List<double> _diskReadHistory = new();
    private static readonly List<double> _diskWriteHistory = new();
    private const int MaxHistoryPoints = 50;

    // Simulated values with smooth transitions
    private static double _cpuValue = 25;
    private static double _memoryValue = 45;
    private static double _diskRead = 20;
    private static double _diskWrite = 15;
    private static double _networkUp = 5;
    private static double _networkDown = 15;
    private static int _eventCounter = 0;
    private static int _alertCounter = 0;

    static async Task<int> Main(string[] args)
    {
        try
        {
            var driver = new NetConsoleDriver(RenderMode.Buffer);

            var options = new ConsoleWindowSystemOptions(
                EnablePerformanceMetrics: true,
                EnableFrameRateLimiting: true,
                TargetFPS: 60,
                StatusBarOptions: new StatusBarOptions(
                    ShowStartButton: true,
                    StartButtonLocation: StatusBarLocation.Bottom,
                    StartButtonPosition: StartButtonPosition.Left,
                    ShowWindowListInMenu: true,
                    ShowTaskBar: true
                )
            );

            _windowSystem = new ConsoleWindowSystem(driver, options: options)
            {
                TopStatus = "[bold cyan]HighFreqDemo[/] - Multi-frequency Update Showcase",
                BottomStatus = "[grey]Ctrl+C: Exit | Ctrl+Space: Start Menu[/]"
            };

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            // Register start menu actions
            RegisterStartMenuActions();

            // Create main window
            CreateMainWindow();

            await Task.Run(() => _windowSystem.Run());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void RegisterStartMenuActions()
    {
        if (_windowSystem == null) return;

        _windowSystem.RegisterStartMenuAction("Reset Counters", () =>
        {
            _eventCounter = 0;
            _alertCounter = 0;
            _windowSystem.NotificationStateService.ShowNotification(
                "Reset", "Counters reset to zero", NotificationSeverity.Info);
        }, category: "Tools", order: 10);

        _windowSystem.RegisterStartMenuAction("Spike CPU", () =>
        {
            _cpuValue = 95;
            _windowSystem.NotificationStateService.ShowNotification(
                "CPU Spike", "Simulated CPU spike triggered", NotificationSeverity.Warning);
        }, category: "Tools", order: 20);

        _windowSystem.RegisterStartMenuAction("Clear History", () =>
        {
            _cpuHistory.Clear();
            _memoryHistory.Clear();
            _networkUpHistory.Clear();
            _networkDownHistory.Clear();
            _diskReadHistory.Clear();
            _diskWriteHistory.Clear();
            _windowSystem.NotificationStateService.ShowNotification(
                "Cleared", "All history data cleared", NotificationSeverity.Success);
        }, category: "Tools", order: 30);
    }

    private static void CreateMainWindow()
    {
        if (_windowSystem == null) return;

        _mainWindow = new WindowBuilder(_windowSystem)
            .WithTitle("HighFreqDemo - Controls Showcase")
            .WithColors(Color.Grey11, Color.Grey93)
            .Maximized()
            .Resizable(true)
            .Movable(true)
            .Closable(true)
            .Minimizable(true)
            .Maximizable(true)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    _windowSystem?.Shutdown(0);
                    e.Handled = true;
                }
            })
            .Build();

        // === MENU BAR ===
        var menu = Controls.Menu()
            .Horizontal()
            .WithName("mainMenu")
            .Sticky()
            .AddItem("File", m => m
                .AddItem("Reset All", () =>
                {
                    _eventCounter = 0;
                    _alertCounter = 0;
                    _cpuHistory.Clear();
                    _memoryHistory.Clear();
                })
                .AddSeparator()
                .AddItem("Exit", "Esc", () => _windowSystem?.Shutdown(0))
            )
            .AddItem("Performance", m => m
                .AddItem("Toggle Metrics", () =>
                {
                    var current = _windowSystem!.Performance.IsPerformanceMetricsEnabled;
                    _windowSystem.Performance.SetPerformanceMetrics(!current);
                })
                .AddItem("Frame Rate", sub => sub
                    .AddItem("15 FPS", () => _windowSystem?.Performance.SetTargetFPS(15))
                    .AddItem("30 FPS", () => _windowSystem?.Performance.SetTargetFPS(30))
                    .AddItem("60 FPS (Default)", () => _windowSystem?.Performance.SetTargetFPS(60))
                    .AddItem("120 FPS", () => _windowSystem?.Performance.SetTargetFPS(120))
                    .AddItem("144 FPS", () => _windowSystem?.Performance.SetTargetFPS(144))
                )
                .AddItem("Frame Limiting", sub => sub
                    .AddItem("Enable", () => _windowSystem?.Performance.SetFrameRateLimiting(true))
                    .AddItem("Disable", () => _windowSystem?.Performance.SetFrameRateLimiting(false))
                )
                .AddSeparator()
                .AddItem("Performance Dialog...", () => PerformanceDialog.Show(_windowSystem!, _mainWindow))
            )
            .AddItem("View", m => m
                .AddItem("Spike CPU", () => _cpuValue = 95)
                .AddItem("Spike Memory", () => _memoryValue = 90)
                .AddItem("Spike Network", () => { _networkUp = 80; _networkDown = 90; })
                .AddSeparator()
                .AddItem("Normalize All", () =>
                {
                    _cpuValue = 25;
                    _memoryValue = 45;
                    _networkUp = 5;
                    _networkDown = 15;
                    _diskRead = 20;
                    _diskWrite = 15;
                })
            )
            .AddItem("Help", m => m
                .AddItem("About", () => AboutDialog.Show(_windowSystem!))
            )
            .Build();

        menu.StickyPosition = StickyPosition.Top;
        _mainWindow.AddControl(menu);
        _mainWindow.AddControl(Controls.RuleBuilder().StickyTop().WithColor(Color.Grey23).Build());

        // === MAIN HORIZONTAL GRID ===
        var mainGrid = Controls.HorizontalGrid()
            .WithName("mainGrid")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Top)
            .WithMargin(1, 1, 1, 1)
            // LEFT COLUMN - Two list controls
            .Column(col => col
                .Width(35)
                .Add(Controls.Markup("[bold cyan]Events (Fast: 100ms)[/]")
                    .WithMargin(0, 0, 0, 0).Build())
                .Add(BuildEventsList())
                .Add(Controls.Markup("[bold yellow]Alerts (Slow: 2s)[/]")
                    .WithMargin(0, 1, 0, 0).Build())
                .Add(BuildAlertsList())
            )
            // RIGHT COLUMN - Graphs, sparklines, markup
            .Column(col => col
                .Add(BuildGraphsPanel())
            )
            .Build();

        _mainWindow.AddControl(mainGrid);

        // === BOTTOM STATUS BAR ===
        _mainWindow.AddControl(Controls.RuleBuilder().StickyBottom().WithColor(Color.Grey23).Build());

        var bottomBar = Controls.HorizontalGrid()
            .StickyBottom()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Add(
                Controls.Markup("[grey70]ESC: Exit | Menu: File, Performance, View, Help[/]")
                    .WithAlignment(HorizontalAlignment.Left)
                    .WithMargin(1, 0, 0, 0)
                    .Build()))
            .Column(col => col.Add(
                Controls.Markup("[grey70]--[/]")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .WithName("statusRight")
                    .Build()))
            .Build();
        bottomBar.BackgroundColor = Color.Grey15;

        _mainWindow.AddControl(bottomBar);

        _windowSystem.AddWindow(_mainWindow);
    }

    private static ListControl BuildEventsList()
    {
        var list = Controls.List()
            .WithName("eventsList")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .MaxVisibleItems(12)
            .SimpleMode()
            .Build();

        // Seed with initial items
        for (int i = 0; i < 5; i++)
        {
            list.AddItem(new ListItem($"[grey]Event {i + 1}: System initialized[/]"));
        }

        return list;
    }

    private static ListControl BuildAlertsList()
    {
        var list = Controls.List()
            .WithName("alertsList")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .MaxVisibleItems(8)
            .SimpleMode()
            .Build();

        list.AddItem(new ListItem("[green]System nominal[/]"));
        list.AddItem(new ListItem("[grey]Monitoring active[/]"));

        return list;
    }

    private static HorizontalGridControl BuildGraphsPanel()
    {
        return Controls.HorizontalGrid()
            .WithName("graphsPanel")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Top)
            .Column(col => col
                // Bar graphs column
                .Add(Controls.Markup("[bold green]Bar Graphs (Medium: 500ms)[/]")
                    .WithMargin(1, 0, 0, 0).Build())
                .Add(new BarGraphBuilder()
                    .WithName("cpuBar")
                    .WithLabel("CPU")
                    .WithLabelWidth(8)
                    .WithValue(25)
                    .WithMaxValue(100)
                    .WithBarWidth(20)
                    .WithUnfilledColor(Color.Grey35)
                    .ShowLabel()
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithMargin(1, 0, 0, 0)
                    .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
                    .Build())
                .Add(new BarGraphBuilder()
                    .WithName("memoryBar")
                    .WithLabel("Memory")
                    .WithLabelWidth(8)
                    .WithValue(45)
                    .WithMaxValue(100)
                    .WithBarWidth(20)
                    .WithUnfilledColor(Color.Grey35)
                    .ShowLabel()
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithMargin(1, 0, 0, 0)
                    .WithSmoothGradient(Color.Cyan1, Color.Yellow, Color.Orange1)
                    .Build())
                .Add(new BarGraphBuilder()
                    .WithName("diskBar")
                    .WithLabel("Disk")
                    .WithLabelWidth(8)
                    .WithValue(30)
                    .WithMaxValue(100)
                    .WithBarWidth(20)
                    .WithUnfilledColor(Color.Grey35)
                    .ShowLabel()
                    .ShowValue()
                    .WithValueFormat("F1")
                    .WithMargin(1, 0, 0, 0)
                    .WithSmoothGradient(Color.Blue, Color.Cyan1, Color.Green)
                    .Build())
                .Add(Controls.Markup("[bold magenta]Sparklines - Block Mode[/]")
                    .WithMargin(1, 1, 0, 0).Build())
                .Add(new SparklineBuilder()
                    .WithName("cpuSparkline")
                    .WithTitle("CPU % (100ms)")
                    .WithTitleColor(Color.Cyan1)
                    .WithTitlePosition(TitlePosition.Top)
                    .WithHeight(4)
                    .WithMaxDataPoints(MaxHistoryPoints)
                    .WithMode(SparklineMode.Block)
                    .WithBarColor(Color.Cyan1)
                    .WithGradient("cool")
                    .WithBaseline(true, '─', Color.Grey50, TitlePosition.Bottom)
                    .WithMargin(1, 0, 0, 0)
                    .Build())
                .Add(new SparklineBuilder()
                    .WithName("networkSparkline")
                    .WithTitle("Net ↑↓ (150ms)")
                    .WithTitleColor(Color.Yellow)
                    .WithTitlePosition(TitlePosition.Bottom)
                    .WithHeight(4)
                    .WithMaxDataPoints(MaxHistoryPoints)
                    .WithMode(SparklineMode.Bidirectional)
                    .WithBarColor(Color.Green)
                    .WithSecondaryBarColor(Color.Red)
                    .WithBaseline(true, '┄', Color.Grey42, TitlePosition.Bottom)
                    .WithInlineTitleBaseline(true)
                    .WithMargin(1, 0, 0, 0)
                    .Build())
                .Add(Controls.Markup("[bold magenta]Sparklines - Braille Mode[/]")
                    .WithMargin(1, 1, 0, 0).Build())
                .Add(new SparklineBuilder()
                    .WithName("memorySparkline")
                    .WithTitle("Memory % (200ms)")
                    .WithTitleColor(Color.Green)
                    .WithTitlePosition(TitlePosition.Top)
                    .WithHeight(6)
                    .WithMaxDataPoints(MaxHistoryPoints)
                    .WithMode(SparklineMode.Braille)
                    .WithBarColor(Color.Green)
                    .WithGradient("warm")
                    .WithBaseline(true, '╌', Color.Grey50, TitlePosition.Bottom)
                    .WithMargin(1, 0, 0, 0)
                    .Build())
                .Add(new SparklineBuilder()
                    .WithName("diskSparkline")
                    .WithTitle("Disk R/W (300ms)")
                    .WithTitleColor(Color.Magenta1)
                    .WithTitlePosition(TitlePosition.Bottom)
                    .WithHeight(6)
                    .WithMaxDataPoints(MaxHistoryPoints)
                    .WithMode(SparklineMode.BidirectionalBraille)
                    .WithBarColor(Color.Blue)
                    .WithSecondaryBarColor(Color.Magenta1)
                    .WithBaseline(true, '┈', Color.Grey42, TitlePosition.Bottom)
                    .WithInlineTitleBaseline(true)
                    .WithMargin(1, 0, 0, 0)
                    .Build())
            )
            .Column(col => col
                .Width(1)
            )
            .Column(col => col
                // Markup/status column
                .Add(Controls.Markup("[bold blue]Status (Medium: 500ms)[/]")
                    .WithMargin(1, 0, 0, 0).Build())
                .Add(PanelControl.Create()
                    .WithName("statusPanel")
                    .WithContent("[grey]Initializing...[/]")
                    .WithHeader("System Status")
                    .HeaderCenter()
                    .Rounded()
                    .WithBorderColor(Color.Cyan1)
                    .WithPadding(1, 0)
                    .WithMargin(1, 0, 0, 0)
                    .Build())
                .Add(Controls.Markup("[bold red]Metrics (Fast: 100ms)[/]")
                    .WithMargin(1, 1, 0, 0).Build())
                .Add(Controls.Markup("[grey]Loading metrics...[/]")
                    .WithName("metricsMarkup")
                    .WithMargin(1, 0, 0, 0)
                    .Build())
                .Add(Controls.Markup("[bold white]Update Frequencies[/]")
                    .WithMargin(1, 1, 0, 0).Build())
                .Add(Controls.Markup(
                    "[bold]Sparklines:[/]\n" +
                    " [cyan]CPU:[/] 100ms  [yellow]Net:[/] 150ms\n" +
                    " [green]Mem:[/] 200ms [magenta]Disk:[/] 300ms\n" +
                    "[bold]Bar Graphs:[/]\n" +
                    " [cyan]CPU:[/] 300ms  [green]Mem:[/] 500ms\n" +
                    " [blue]Disk:[/] 700ms\n" +
                    "[bold]Lists:[/]\n" +
                    " [grey]Events:[/] 1s   [red]Alerts:[/] 2s")
                    .WithMargin(1, 0, 0, 0)
                    .Build())
            )
            .Build();
    }

    private static async Task UpdateLoopAsync(Window window, CancellationToken cancellationToken)
    {
        int tick = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                tick++;

                // === FAST UPDATES (every tick = 100ms) ===
                UpdateFast(window, tick);

                // === MEDIUM UPDATES (bars at different rates) ===
                UpdateMedium(window, tick);

                // === SLOW UPDATES (every 20 ticks = 2000ms) ===
                if (tick % 20 == 0)
                {
                    UpdateSlow(window);
                }

                // Update bottom status
                UpdateBottomStatus(window);
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Update error", ex, "HighFreqDemo");
            }

            try
            {
                await Task.Delay(100, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private static void UpdateFast(Window window, int tick)
    {
        // Smoothly transition values with some randomness
        _cpuValue = Clamp(_cpuValue + (_random.NextDouble() - 0.5) * 8, 5, 95);
        _memoryValue = Clamp(_memoryValue + (_random.NextDouble() - 0.5) * 3, 20, 85);
        _networkUp = Clamp(_networkUp + (_random.NextDouble() - 0.5) * 10, 0, 100);
        _networkDown = Clamp(_networkDown + (_random.NextDouble() - 0.5) * 15, 0, 100);
        _diskRead = Clamp(_diskRead + (_random.NextDouble() - 0.5) * 12, 5, 80);
        _diskWrite = Clamp(_diskWrite + (_random.NextDouble() - 0.5) * 8, 5, 60);

        // Update history at different rates
        AddToHistory(_cpuHistory, _cpuValue);
        AddToHistory(_networkUpHistory, _networkUp);
        AddToHistory(_networkDownHistory, _networkDown);

        if (tick % 2 == 0) // Memory history updates every 200ms
        {
            AddToHistory(_memoryHistory, _memoryValue);
        }

        if (tick % 3 == 0) // Disk history updates every 300ms
        {
            AddToHistory(_diskReadHistory, _diskRead);
            AddToHistory(_diskWriteHistory, _diskWrite);
        }

        // === SPARKLINE UPDATE FREQUENCIES ===

        // CPU Sparkline: 100ms (every tick) - fastest, real-time feel
        var cpuSparkline = window.FindControl<SparklineControl>("cpuSparkline");
        cpuSparkline?.SetDataPoints(_cpuHistory);

        // Network Sparkline: 150ms (every 1-2 ticks) - near real-time
        if (tick % 2 != 0 || tick % 3 == 0) // Alternating pattern ~150ms avg
        {
            var networkSparkline = window.FindControl<SparklineControl>("networkSparkline");
            if (networkSparkline != null)
            {
                networkSparkline.SetDataPoints(_networkUpHistory);
                networkSparkline.SetSecondaryDataPoints(_networkDownHistory);
            }
        }

        // Memory Sparkline (Braille): 200ms (every 2 ticks) - smooth updates
        if (tick % 2 == 0)
        {
            var memorySparkline = window.FindControl<SparklineControl>("memorySparkline");
            memorySparkline?.SetDataPoints(_memoryHistory);
        }

        // Disk Sparkline (Braille Bidirectional): 300ms (every 3 ticks) - slower, I/O style
        if (tick % 3 == 0)
        {
            var diskSparkline = window.FindControl<SparklineControl>("diskSparkline");
            if (diskSparkline != null)
            {
                diskSparkline.SetDataPoints(_diskReadHistory);
                diskSparkline.SetSecondaryDataPoints(_diskWriteHistory);
            }
        }

        // Update events list (add new event every 10 ticks = 1 second)
        if (tick % 10 == 0)
        {
            _eventCounter++;
            var eventsList = window.FindControl<ListControl>("eventsList");
            if (eventsList != null)
            {
                var eventTypes = new[] { "CPU sample", "Memory check", "I/O poll", "Network ping", "Heartbeat" };
                var eventType = eventTypes[_random.Next(eventTypes.Length)];
                var color = _cpuValue > 70 ? "yellow" : "grey";

                eventsList.AddItem(new ListItem($"[{color}]#{_eventCounter}: {eventType} @ {DateTime.Now:HH:mm:ss}[/]"));

                // Keep list manageable
                while (eventsList.Items.Count > 20)
                {
                    eventsList.Items.RemoveAt(0);
                }

                // Auto-scroll to bottom
                if (eventsList.Items.Count > 0)
                {
                    eventsList.SelectedIndex = eventsList.Items.Count - 1;
                }
            }
        }

        // Update metrics markup
        var metricsMarkup = window.FindControl<MarkupControl>("metricsMarkup");
        metricsMarkup?.SetContent(new List<string>
        {
            $"[white]Tick:[/] [cyan]{tick}[/]",
            $"[white]Events:[/] [green]{_eventCounter}[/]",
            $"[white]Alerts:[/] [yellow]{_alertCounter}[/]",
            $"[white]Time:[/] [grey]{DateTime.Now:HH:mm:ss.fff}[/]"
        });
    }

    private static void UpdateMedium(Window window, int tick)
    {
        // Update bar graphs at DIFFERENT frequencies

        // CPU Bar: 300ms (every 3 ticks from base 100ms)
        if (tick % 3 == 0)
        {
            var cpuBar = window.FindControl<BarGraphControl>("cpuBar");
            if (cpuBar != null) cpuBar.Value = _cpuValue;
        }

        // Memory Bar: 500ms (every 5 ticks)
        if (tick % 5 == 0)
        {
            var memoryBar = window.FindControl<BarGraphControl>("memoryBar");
            if (memoryBar != null) memoryBar.Value = _memoryValue;
        }

        // Disk Bar: 700ms (every 7 ticks)
        if (tick % 7 == 0)
        {
            var diskBar = window.FindControl<BarGraphControl>("diskBar");
            if (diskBar != null) diskBar.Value = (_diskRead + _diskWrite) / 2;
        }

        // Update status panel
        var statusPanel = window.FindControl<PanelControl>("statusPanel");
        if (statusPanel != null)
        {
            var status = _cpuValue > 80 ? "[red bold]HIGH LOAD[/]" :
                         _cpuValue > 60 ? "[yellow]Elevated[/]" :
                         "[green]Normal[/]";

            var content = $"CPU: {_cpuValue:F1}% - {status}\n" +
                          $"Memory: {_memoryValue:F1}%\n" +
                          $"Disk: R[cyan]{_diskRead:F0}[/] W[magenta]{_diskWrite:F0}[/] MB/s\n" +
                          $"Net: [green]^{_networkUp:F1}[/] [red]v{_networkDown:F1}[/] MB/s";

            statusPanel.SetContent(content);
        }
    }

    private static void UpdateSlow(Window window)
    {
        // Update alerts list
        var alertsList = window.FindControl<ListControl>("alertsList");
        if (alertsList != null)
        {
            // Generate alert based on conditions
            string? alert = null;
            string color = "grey";

            if (_cpuValue > 80)
            {
                alert = $"CPU critical: {_cpuValue:F1}%";
                color = "red";
            }
            else if (_memoryValue > 75)
            {
                alert = $"Memory high: {_memoryValue:F1}%";
                color = "yellow";
            }
            else if (_networkDown > 70)
            {
                alert = $"Network saturated: {_networkDown:F1} MB/s";
                color = "orange1";
            }
            else if (_random.NextDouble() > 0.7)
            {
                alert = "System check passed";
                color = "green";
            }

            if (alert != null)
            {
                _alertCounter++;
                alertsList.AddItem(new ListItem($"[{color}][{DateTime.Now:HH:mm:ss}] {alert}[/]"));

                // Keep list manageable
                while (alertsList.Items.Count > 15)
                {
                    alertsList.Items.RemoveAt(0);
                }
            }
        }
    }

    private static void UpdateBottomStatus(Window window)
    {
        var statusRight = window.FindControl<MarkupControl>("statusRight");
        if (statusRight != null && _windowSystem != null)
        {
            var fps = _windowSystem.Performance.CurrentFPS;
            var targetFps = _windowSystem.Performance.TargetFPS;
            var frameTime = _windowSystem.Performance.CurrentFrameTimeMs;

            statusRight.SetContent(new List<string>
            {
                $"[grey70]FPS: [cyan]{fps:F0}[/]/{targetFps} | Frame: [cyan]{frameTime:F1}ms[/] | CPU: [cyan]{_cpuValue:F0}%[/][/]"
            });
        }
    }

    private static void AddToHistory(List<double> history, double value)
    {
        history.Add(value);
        while (history.Count > MaxHistoryPoints)
        {
            history.RemoveAt(0);
        }
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
