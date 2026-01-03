// -----------------------------------------------------------------------
// FullScreenExample - Demonstrates full-screen window mode
// A maximized window with no resize, move, close, minimize, or maximize buttons
// Uses modern patterns: DI, async/await, fluent builders
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Spectre.Console;

namespace FullScreenExample;

/// <summary>
/// Full-screen example demonstrating kiosk-style application with modern patterns
/// </summary>
internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static IServiceProvider? _serviceProvider;
    private static ILogger<Program>? _logger;
    private static Window? _mainWindow;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Setup services with dependency injection
            SetupServices();

            // Initialize console window system
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "Full Screen Example - Press F10 to Exit",
                ShowTaskBar = false  // Hide taskbar for true full-screen experience
            };

            // Setup graceful shutdown handler for Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                _logger?.LogInformation("Received interrupt signal, shutting down gracefully...");
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            // Create full-screen window using fluent builder
            CreateFullScreenWindow();

            // Run the application
            _logger?.LogInformation("Starting Full Screen Example");
            await Task.Run(() => _windowSystem.Run());

            _logger?.LogInformation("Application shutting down");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void SetupServices()
    {
        var services = new ServiceCollection();

        // Configure logging (file-based only - never console for UI apps!)
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            // In production: builder.AddFile("logs/fullscreen.txt");
        });

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetService<ILogger<Program>>();
    }

    private static void CreateFullScreenWindow()
    {
        if (_windowSystem == null || _serviceProvider == null) return;

        // Create window using fluent builder
        _mainWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Full Screen Application")
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .Build();

        // Add header
        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold cyan]Welcome to Full Screen Mode[/]",
            "[dim]This window fills the entire console and resizes with it[/]"
        })
        {
            Alignment = Alignment.Center,
            StickyPosition = StickyPosition.Top
        });

        _mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Add main content
        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "",
            "[yellow]Features demonstrated:[/]",
            "",
            "  [green]•[/] IsResizable = false     - Window cannot be resized manually",
            "  [green]•[/] IsMovable = false       - Window cannot be dragged",
            "  [green]•[/] IsClosable = false      - Window cannot be closed with X button",
            "  [green]•[/] IsMinimizable = false   - Window cannot be minimized",
            "  [green]•[/] IsMaximizable = false   - Window cannot be maximized/restored",
            "  [green]•[/] ShowTaskBar = false     - Taskbar hidden for full-screen",
            "  [green]•[/] State = Maximized       - Fills console, resizes with it",
            "",
            "[dim]This is ideal for:[/]",
            "  - Kiosk applications",
            "  - Game interfaces",
            "  - Terminal-based dashboards",
            "  - Embedded system UIs",
            "",
            "[bold]Press [yellow]F10[/] to exit the application[/]"
        }));

        // Add interactive elements
        _mainWindow.AddControl(new RuleControl());

        var statusLabel = new MarkupControl(new List<string> { "[dim]Status: Running...[/]" })
        {
            Alignment = Alignment.Center
        };
        _mainWindow.AddControl(statusLabel);

        // Create button grid at the bottom
        var buttonGrid = new HorizontalGridControl
        {
            Alignment = Alignment.Center,
            StickyPosition = StickyPosition.Bottom
        };

        var actionButton = new ButtonControl { Text = "Perform Action", Width = 20 };
        var infoButton = new ButtonControl { Text = "Show Info", Width = 20 };
        var exitButton = new ButtonControl { Text = "Exit (F10)", Width = 20 };

        int actionCount = 0;
        actionButton.Click += (s, e) =>
        {
            actionCount++;
            statusLabel.SetContent(new List<string> { $"[green]Action performed {actionCount} time(s)![/]" });
            _logger?.LogInformation("Action performed: {Count}", actionCount);
        };

        infoButton.Click += (s, e) =>
        {
            statusLabel.SetContent(new List<string>
            {
                $"[cyan]Window: {_mainWindow?.Width}x{_mainWindow?.Height} | Console: {Console.WindowWidth}x{Console.WindowHeight}[/]"
            });
        };

        exitButton.Click += (s, e) =>
        {
            _logger?.LogInformation("Exit button clicked");
            _windowSystem?.Shutdown();
        };

        var col1 = new ColumnContainer(buttonGrid);
        col1.AddContent(actionButton);
        buttonGrid.AddColumn(col1);

        var col2 = new ColumnContainer(buttonGrid);
        col2.AddContent(infoButton);
        buttonGrid.AddColumn(col2);

        var col3 = new ColumnContainer(buttonGrid);
        col3.AddContent(exitButton);
        buttonGrid.AddColumn(col3);

        _mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom });
        _mainWindow.AddControl(buttonGrid);

        // Handle F10 to exit
        _mainWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.F10)
            {
                _logger?.LogInformation("F10 pressed, shutting down");
                _windowSystem?.Shutdown();
                e.Handled = true;
            }
        };

        // Add window and set to maximized state
        _windowSystem.AddWindow(_mainWindow);
        // Directly set State to bypass IsMaximizable check (which is false to hide the button)
        _mainWindow.State = WindowState.Maximized;
    }
}
