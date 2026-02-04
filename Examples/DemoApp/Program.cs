// -----------------------------------------------------------------------
// Modern SharpConsoleUI Example - Comprehensive Demo
// Demonstrates all features with modern patterns adapted from the original examples
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.IO;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using SpectreColor = Spectre.Console.Color;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace ModernExample;

/// <summary>
/// Modern comprehensive example demonstrating SharpConsoleUI's enhanced features
/// with multiple windows adapted from the original examples
/// </summary>
internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;

    // Window references for management
    private static Window? _mainWindow;
    private static Window? _logWindow;
    private static Window? _clockWindow;
    private static Window? _sysInfoWindow;
    private static Window? _welcomeWindow;

    /// <summary>
    /// Application entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Initialize console window system with modern patterns
            _windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
            {
                TopStatus = "Modern SharpConsoleUI Demo - Press F1-F10 for windows",
                BottomStatus = "ESC: Close Window | F2-F6,F7-F10: Demo Windows | Ctrl+Q: Quit",
            };

            // Setup graceful shutdown handler for Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                _windowSystem?.LogService.LogInfo(
                    "Received interrupt signal, shutting down gracefully..."
                );
                e.Cancel = true; // Prevent immediate termination
                _windowSystem?.Shutdown(0);
            };

            // Create welcome window
            CreateWelcomeWindow();

            // Create main menu window using fluent builder
            CreateMainMenuWindow();

            // Set up key handlers for the main window
            SetupMainWindowKeyHandlers();

            // Activate wellcome window
            _windowSystem.TryActivate("WelcomeWindow");

            // Run the application
            _windowSystem.LogService.LogInfo("Starting Modern SharpConsoleUI Demo");
            await Task.Run(() => _windowSystem.Run());

            _windowSystem.LogService.LogInfo("Application shutting down");
            return 0;
        }
        catch (Exception ex)
        {
            // If console system is corrupted, use Spectre.Console to output error
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    /// <summary>
    /// Create a welcome window using the Fingle control
    /// </summary>
    private static void CreateWelcomeWindow()
    {
        if (_windowSystem == null)
            return;

        _welcomeWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Welcome")
            .WithSize(70, 20)
            .Centered()
            .Closable(true)
            .AddControl(new FigleControl() { Text = "ConsoleEx Demo App" })
            .AddControl(new RuleControl())
            .AddControl(
                new MarkupBuilder()
                    .AddLine("[magenta]Copyright (c) 2025 by Nikolaos Protopapas[/]")
                    .Centered()
                    .Build()
            )
            .WithName("WelcomeWindow")
            .Build();

        _welcomeWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _welcomeWindow.Close(false);
                e.Handled = true;
            }
        };

        _windowSystem.LogService?.LogInfo("Welcome window created");

        _windowSystem.AddWindow(_welcomeWindow);
    }

    /// <summary>
    /// Create the main menu window using fluent builder pattern
    /// </summary>
    private static void CreateMainMenuWindow()
    {
        if (_windowSystem == null)
            return;

        _mainWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Modern SharpConsoleUI Demo - Main Menu")
            .WithSize(65, 24)
            .Centered()
            .Closable(false)
            .Build();

        // Add welcome content with markup
        _mainWindow.AddControl(
            new MarkupControl(
                new List<string> { "[bold yellow]Welcome to Modern SharpConsoleUI![/]" }
            )
        );

        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

        // Theme info and selector button
        var themeInfoGrid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var themeLabel = new MarkupControl(
            new List<string> { $"[dim]Current Theme:[/] [cyan]{_windowSystem.ThemeStateService.CurrentTheme.Name}[/]" }
        )
        {
            Margin = new Margin(0, 0, 2, 0),
        };

        var themeLabelCol = new ColumnContainer(themeInfoGrid);
        themeLabelCol.AddContent(themeLabel);
        themeInfoGrid.AddColumn(themeLabelCol);

        var themeButton = new ButtonControl { Text = "Change Theme", Width = 16 };
        themeButton.Click += (sender, btn) =>
        {
            _windowSystem?.ThemeStateService.ShowThemeSelector();
            // Update theme label after dialog closes
            themeLabel.SetContent(
                new List<string> { $"[dim]Current Theme:[/] [cyan]{_windowSystem?.ThemeStateService.CurrentTheme.Name}[/]" }
            );
        };

        var themeButtonCol = new ColumnContainer(themeInfoGrid);
        themeButtonCol.AddContent(themeButton);
        themeInfoGrid.AddColumn(themeButtonCol);

        _mainWindow.AddControl(themeInfoGrid);
        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

        // Feature showcase
        _mainWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[green]Enhanced Features Demonstrated:[/]",
                    "• [cyan]Fluent Builder Pattern[/] - Chainable window creation",
                    "• [magenta]Async Patterns[/] - Background tasks and real-time updates",
                    "• [yellow]Modern C# Features[/] - Records, nullable refs, top-level",
                    "• [blue]Service Integration[/] - Dependency injection ready",
                    "• [red]Enhanced Controls[/] - Rich markup and styling",
                    "• [white]Comprehensive Examples[/] - Multiple window types",
                }
            )
        );

        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

        // Window showcase adapted from original examples
        _mainWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[bold]Available Demo Windows (adapted from originals):[/]",
                    "",
                    "[green]F1[/] - [bold]Comprehensive Layout Demo[/]",
                    "       Complete application UI with menus, splitters, status",
                    "",
                    "[blue]F2[/] - [bold]Real-time Log Window[/]",
                    "       Demonstrates async logging with live updates",
                    "",
                    "[blue]F3[/] - [bold]System Information Window[/]",
                    "       Shows system stats with modern data gathering",
                    "",
                    "[red]F4[/] - [bold]File Explorer[/]",
                    "        Tree control with file system navigation",
                    "",
                    "[magenta]F5[/] - [bold]Digital Clock Window[/]",
                    "       Real-time clock with async time updates",
                    "",
                    "[yellow]F6[/] - [bold]Interactive Demo[/]",
                    "       Shows modern control interactions",
                    "",
                    "[cyan]F7[/] - [bold]Command Window[/]",
                    "       Interactive command prompt with async I/O",
                    "",
                    "[white]F8[/] - [bold]Dropdown Demo[/]",
                    "       Country selection with styled dropdowns",
                    "",
                    "[green]F9[/] - [bold]ListView Demo[/]",
                    "       List control with selection handling",
                    "",
                }
            )
        );

        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));
        _mainWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[dim]Navigation:[/]",
                    "[dim]• Press function keys (F2-F9, F11) to open demo windows[/]",
                    "[dim]• Press ESC in any window to close it[/]",
                    "[dim]• Press Ctrl+Q in main window to exit[/]",
                }
            )
        );

        _windowSystem.AddWindow(_mainWindow);
        _windowSystem?.LogService.LogInfo("Main menu window created with fluent builder");
    }

    /// <summary>
    /// Set up key handlers for the main window
    /// </summary>
    private static void SetupMainWindowKeyHandlers()
    {
        if (_mainWindow == null)
            return;

        _mainWindow.KeyPressed += (sender, e) =>
        {
            try
            {
                switch (e.KeyInfo.Key)
                {
                    case ConsoleKey.F2:
                        CreateLogWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F3:
                        CreateSystemInfoWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F5:
                        _ = CreateClockWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F6:
                        CreateInteractiveDemo();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F7:
                        _ = CreateCommandWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F8:
                        CreateDropdownDemo();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F9:
                        CreateListViewDemo();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F4:
                        CreateFileExplorerWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F1:
                        CreateComprehensiveLayoutDemo();
                        e.Handled = true;
                        break;
                    case ConsoleKey.Q when e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control):
                        _windowSystem?.CloseWindow(_mainWindow);
                        e.Handled = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                _windowSystem?.LogService.LogError("Error handling key press in main window", ex);
            }
        };

        _windowSystem?.LogService.LogInfo("Main window key handlers configured");
    }

    /// <summary>
    /// Create log window with real-time updates using LogViewerControl
    /// </summary>
    private static void CreateLogWindow()
    {
        if (_windowSystem == null)
            return;

        // Use the new LogViewerControl
        var logViewer = new LogViewerControl(_windowSystem.LogService)
        {
            Title = "Application Logs",
            FilterLevel = LogLevel.Trace, // Show all levels for demo
        };

        _logWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Log Viewer (F2)")
            .WithSize(80, 18)
            .AtPosition(5, 3)
            .AddControl(logViewer)
            .WithAsyncWindowThread(SimulateLoggingAsync)
            .Build();

        // Setup ESC key handler
        _logWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(_logWindow);
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(_logWindow);
        _windowSystem.LogService.LogInfo("Log window created with LogViewerControl", "UI");
    }

    /// <summary>
    /// Simulate real-time logging updates using window thread delegate pattern
    /// </summary>
    private static async Task SimulateLoggingAsync(
        Window window,
        CancellationToken cancellationToken
    )
    {
        if (_windowSystem == null)
            return;

        // Enable all log levels for demo
        _windowSystem.LogService.MinimumLevel = LogLevel.Trace;

        var levels = new[]
        {
            LogLevel.Trace,
            LogLevel.Debug,
            LogLevel.Information,
            LogLevel.Warning,
        };
        var categories = new[] { "System", "Network", "Database", "UI", "Auth" };
        var messages = new[]
        {
            "Processing request",
            "Connection established",
            "Query executed",
            "Cache hit",
            "User action",
        };

        // Thread runs until cancelled or loop completes - window system manages lifecycle
        for (int i = 1; i <= 30; i++)
        {
            // Check for cancellation before delay and logging
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(600, cancellationToken);

            var level = levels[i % levels.Length];
            var category = categories[i % categories.Length];
            var message = $"{messages[i % messages.Length]} #{i}";

            _windowSystem.LogService.Log(level, message, category);
        }

        _windowSystem.LogService.LogInfo("Log simulation completed", "Demo");
    }

    /// <summary>
    /// Create system information window (adapted from SystemInfoWindow.cs)
    /// </summary>
    private static void CreateSystemInfoWindow()
    {
        if (_windowSystem == null)
            return;

        _sysInfoWindow = new WindowBuilder(_windowSystem)
            .WithTitle("System Information")
            .WithSize(75, 18)
            .AtPosition(8, 3)
            .Build();

        // System information using modern patterns
        var sysInfo = GetSystemInformation();

        _sysInfoWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[bold cyan]System Information Dashboard[/]",
                    "[dim](Adapted from original with modern data gathering)[/]",
                    "",
                }
            )
        );

        foreach (var info in sysInfo)
        {
            _sysInfoWindow.AddControl(new MarkupControl(new List<string> { info }));
        }

        // Setup ESC key handler
        _sysInfoWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(_sysInfoWindow);
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(_sysInfoWindow);
        _windowSystem?.LogService.LogInfo("System information window created");
    }

    /// <summary>
    /// Create clock window with real-time updates (adapted from ClockWindow.cs)
    /// </summary>
    private static Task CreateClockWindow()
    {
        if (_windowSystem == null)
            return Task.CompletedTask;

        _clockWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Digital Clock")
            .WithSize(35, 10)
            .AtPosition(15, 8)
            .Build();

        // Setup ESC key handler
        _clockWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(_clockWindow);
                e.Handled = true;
            }
        };

        // Start clock updates (modern async pattern)
        _ = Task.Run(async () => await UpdateClockAsync());

        _windowSystem.AddWindow(_clockWindow);
        _windowSystem?.LogService.LogInfo("Clock window created with async updates");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Update clock display continuously using modern async patterns
    /// </summary>
    private static async Task UpdateClockAsync()
    {
        while (_clockWindow != null && _windowSystem?.Windows.Values.Contains(_clockWindow) == true)
        {
            _clockWindow.ClearControls();

            var now = DateTime.Now;
            _clockWindow.AddControl(
                new MarkupControl(
                    new List<string>
                    {
                        "[bold yellow]Digital Clock[/]",
                        "[dim](Adapted with async updates)[/]",
                        "",
                        $"[bold green]{now:HH:mm:ss}[/]",
                        $"[cyan]{now:dddd}[/]",
                        $"[white]{now:MMMM dd, yyyy}[/]",
                        "",
                        "[dim]Updates every second • ESC to close[/]",
                    }
                )
            );

            await Task.Delay(1000);
        }
    }

    /// <summary>
    /// Create interactive demo window
    /// </summary>
    private static void CreateInteractiveDemo()
    {
        if (_windowSystem == null)
            return;

        var demoWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Interactive Demo")
            .WithSize(60, 16)
            .AtPosition(12, 6)
            .Build();

        demoWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[bold purple]Interactive Demo[/]",
                    "[dim](Shows modern control patterns)[/]",
                    "",
                    "[yellow]This window demonstrates:[/]",
                    "• Modern fluent builder patterns",
                    "• Enhanced markup controls with colors",
                    "• Proper event handling and cleanup",
                    "• Service provider integration",
                    "• Structured logging integration",
                    "",
                    "[green]Try pressing different keys:[/]",
                    "• ESC - Close window",
                    "• Any other key - Shows key info",
                }
            )
        );

        // Enhanced key handler with logging
        demoWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(demoWindow);
                e.Handled = true;
                return;
            }

            // Show key press info
            demoWindow.AddControl(
                new MarkupControl(
                    new List<string>
                    {
                        $"[cyan]Key pressed: {e.KeyInfo.Key} (Char: '{e.KeyInfo.KeyChar}')[/]",
                    }
                )
            );

            _windowSystem?.LogService.LogDebug($"Key pressed in demo window: {e.KeyInfo.Key}");
            demoWindow.GoToBottom();
            e.Handled = true;
        };

        _windowSystem.AddWindow(demoWindow);
        _windowSystem?.LogService.LogInfo("Interactive demo window created");
    }

    /// <summary>
    /// Get system information using modern patterns
    /// </summary>
    private static List<string> GetSystemInformation()
    {
        var info = new List<string>();

        try
        {
            info.Add($"[green]Operating System:[/] {Environment.OSVersion}");
            info.Add($"[green]Machine Name:[/] {Environment.MachineName}");
            info.Add($"[green]User Name:[/] {Environment.UserName}");
            info.Add($"[green]Processor Count:[/] {Environment.ProcessorCount}");
            info.Add($"[green]Working Set:[/] {Environment.WorkingSet / (1024 * 1024):N0} MB");
            info.Add($"[green].NET Version:[/] {Environment.Version}");
            info.Add(
                $"[green]Current Directory:[/] {(Environment.CurrentDirectory.Length > 50 ? "..." + Environment.CurrentDirectory.Substring(Environment.CurrentDirectory.Length - 47) : Environment.CurrentDirectory)}"
            );

            // Modern C# features demo
            var memoryInfo = GC.GetTotalMemory(false);
            info.Add($"[green]GC Memory:[/] {memoryInfo / (1024 * 1024):N2} MB");

            var uptime = Environment.TickCount64;
            var uptimeSpan = TimeSpan.FromMilliseconds(uptime);
            info.Add(
                $"[green]System Uptime:[/] {uptimeSpan.Days}d {uptimeSpan.Hours}h {uptimeSpan.Minutes}m"
            );

            info.Add("");
            info.Add("[dim]Press ESC to close this window[/]");
        }
        catch (Exception ex)
        {
            _windowSystem?.LogService.LogError("Error gathering system information", ex);
            info.Add($"[red]Error gathering system info: {ex.Message}[/]");
        }

        return info;
    }

    /// <summary>
    /// Create command window with interactive command prompt (adapted from CommandWindow.cs)
    /// </summary>
    private static Task CreateCommandWindow()
    {
        if (_windowSystem == null)
            return Task.CompletedTask;

        var commandWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Interactive Command Window")
            .WithSize(80, 25)
            .AtPosition(2, 2)
            .WithColors(SpectreColor.Grey15, SpectreColor.Grey93)
            .Build();

        // Create prompt control for command input
        var promptControl = new PromptControl
        {
            Prompt = "CMD> ",
            UnfocusOnEnter = false,
            StickyPosition = StickyPosition.Top,
            HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment.Stretch
        };

        // Create multiline output control
        var outputControl = new MultilineEditControl
        {
            ViewportHeight = commandWindow.Height - 4, // Leave space for prompt and borders
            WrapMode = WrapMode.Wrap,
            ReadOnly = true,
        };

        // Add controls to window
        commandWindow.AddControl(promptControl);
        commandWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });
        commandWindow.AddControl(outputControl);

        // Setup window resize handler
        commandWindow.OnResize += (sender, args) =>
        {
            outputControl.ViewportHeight = commandWindow.Height - 4;
        };

        // Add initial welcome message
        outputControl.AppendContent(
            "Interactive command prompt started. Modern async implementation.\n"
        );
        outputControl.AppendContent("Type 'help' for available commands, 'exit' to close.\n");

        // Setup command execution with modern async patterns
        promptControl.Entered += async (sender, command) =>
        {
            try
            {
                // Display the command in output
                outputControl.AppendContent($"\n> {command}\n");

                // Handle built-in commands first
                if (await HandleBuiltInCommand(command.Trim(), outputControl, commandWindow))
                {
                    promptControl.SetInput(string.Empty);
                    return;
                }

                // Execute external command with proper async handling
                await ExecuteExternalCommand(command, outputControl);

                promptControl.SetInput(string.Empty);
            }
            catch (Exception ex)
            {
                outputControl.AppendContent($"Error: {ex.Message}\n");
                _windowSystem?.LogService.LogError($"Error executing command: {command}", ex);
            }
            finally
            {
                outputControl.GoToEnd();
            }
        };

        // Setup ESC key handler
        commandWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(commandWindow);
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(commandWindow);
        _windowSystem?.LogService.LogInfo("Command window created with modern async patterns");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle built-in commands that don't need external process execution
    /// </summary>
    private static async Task<bool> HandleBuiltInCommand(
        string command,
        MultilineEditControl output,
        Window window
    )
    {
        await Task.Delay(10); // Simulate async operation

        switch (command.ToLowerInvariant())
        {
            case "help":
                output.AppendContent("Built-in commands:\n");
                output.AppendContent("  help     - Show this help\n");
                output.AppendContent("  clear    - Clear the output\n");
                output.AppendContent("  date     - Show current date and time\n");
                output.AppendContent("  version  - Show application version\n");
                output.AppendContent("  exit     - Close this window\n");
                output.AppendContent("  sysinfo  - Show system information\n");
                output.AppendContent(
                    "\nAll other commands will be executed as external processes.\n"
                );
                return true;

            case "clear":
                output.SetContent("");
                output.AppendContent(
                    "Interactive command prompt started. Modern async implementation.\n"
                );
                return true;

            case "date":
                output.AppendContent($"{DateTime.Now:F}\n");
                return true;

            case "version":
                output.AppendContent("Modern SharpConsoleUI Demo v1.0\n");
                output.AppendContent($".NET Version: {Environment.Version}\n");
                return true;

            case "exit":
                _windowSystem?.CloseWindow(window);
                return true;

            case "sysinfo":
                output.AppendContent($"OS: {Environment.OSVersion}\n");
                output.AppendContent($"Machine: {Environment.MachineName}\n");
                output.AppendContent($"User: {Environment.UserName}\n");
                output.AppendContent($"Processors: {Environment.ProcessorCount}\n");
                output.AppendContent(
                    $"Working Set: {Environment.WorkingSet / (1024 * 1024):N0} MB\n"
                );
                return true;

            default:
                return false; // Not a built-in command
        }
    }

    /// <summary>
    /// Execute external command using modern async patterns
    /// </summary>
    private static async Task ExecuteExternalCommand(string command, MultilineEditControl output)
    {
        try
        {
            using var process = new System.Diagnostics.Process();

            // Setup process for cross-platform compatibility
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c {command}";
            }
            else
            {
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"{command}\"";
            }

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Start process and read output asynchronously
            process.Start();

            // Read both output and error streams concurrently
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait for process to complete with timeout
            var processTask = process.WaitForExitAsync();
            var timeoutTask = Task.Delay(30000); // 30 second timeout

            var completedTask = await Task.WhenAny(processTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill();
                output.AppendContent("Command timed out after 30 seconds.\n");
                return;
            }

            // Get the results
            var outputText = await outputTask;
            var errorText = await errorTask;

            // Display results
            if (!string.IsNullOrEmpty(outputText))
            {
                output.AppendContent(outputText);
                if (!outputText.EndsWith('\n'))
                    output.AppendContent("\n");
            }

            if (!string.IsNullOrEmpty(errorText))
            {
                output.AppendContent($"Error: {errorText}");
                if (!errorText.EndsWith('\n'))
                    output.AppendContent("\n");
            }

            if (string.IsNullOrEmpty(outputText) && string.IsNullOrEmpty(errorText))
            {
                output.AppendContent($"Command completed with exit code: {process.ExitCode}\n");
            }
        }
        catch (Exception ex)
        {
            output.AppendContent($"Failed to execute command: {ex.Message}\n");
            _windowSystem?.LogService.LogError(
                $"Failed to execute external command: {command}",
                ex
            );
        }
    }

    /// <summary>
    /// Create dropdown demo window (adapted from DropDownWindow.cs)
    /// </summary>
    private static void CreateDropdownDemo()
    {
        if (_windowSystem == null)
            return;

        var dropdownWindow = new WindowBuilder(_windowSystem)
            .WithTitle("Country Selection Demo")
            .WithSize(50, 20)
            .AtPosition(4, 4)
            .Build();

        // Add title
        dropdownWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[bold]Country Selection Form[/]",
                    "[dim](Adapted with modern patterns)[/]",
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                StickyPosition = StickyPosition.Top,
            }
        );

        dropdownWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });
        dropdownWindow.AddControl(new MarkupControl(new List<string> { " " }));

        // Create dropdown control
        var countryDropdown = new DropdownControl("Select a country:");
        countryDropdown.AddItem("USA", "★", SpectreColor.Cyan1);
        countryDropdown.AddItem("Canada", "♦", SpectreColor.Red);
        countryDropdown.AddItem("UK", "♠", SpectreColor.Cyan1);
        countryDropdown.AddItem("France", "♣", SpectreColor.Red);
        countryDropdown.AddItem("Germany", "■", SpectreColor.Yellow);
        countryDropdown.AddItem("Japan", "●", SpectreColor.Red);
        countryDropdown.AddItem("Australia", "◆", SpectreColor.Green);
        countryDropdown.SelectedIndex = 0;

        // Add dropdown to window
        dropdownWindow.AddControl(countryDropdown);
        dropdownWindow.AddControl(new MarkupControl(new List<string> { " " }));

        // Create status display
        var statusControl = new MarkupControl(new List<string> { "Selected: USA" })
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        dropdownWindow.AddControl(statusControl);

        // Handle selection changes with modern patterns
        countryDropdown.SelectedItemChanged += (sender, item) =>
        {
            if (item != null)
            {
                statusControl.SetContent(new List<string> { $"Selected: [green]{item.Text}[/]" });
                dropdownWindow.Title = $"Country Selection - {item.Text}";
                _windowSystem?.LogService.LogDebug(
                    "Country selection changed to: {Country}",
                    item.Text
                );
            }
        };

        // Add action buttons
        var buttonsGrid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Bottom,
        };

        var okButton = new ButtonControl { Text = "OK" };
        okButton.Click += (sender, button) =>
        {
            var selected = countryDropdown.SelectedValue;
            _windowSystem?.LogService.LogInfo("User selected country: {Country}", selected);
            _windowSystem?.CloseWindow(dropdownWindow);
        };

        var cancelButton = new ButtonControl { Text = "Cancel" };
        cancelButton.Click += (sender, button) =>
        {
            _windowSystem?.CloseWindow(dropdownWindow);
        };

        var okColumn = new ColumnContainer(buttonsGrid);
        okColumn.AddContent(okButton);
        buttonsGrid.AddColumn(okColumn);

        var cancelColumn = new ColumnContainer(buttonsGrid);
        cancelColumn.AddContent(cancelButton);
        buttonsGrid.AddColumn(cancelColumn);

        dropdownWindow.AddControl(buttonsGrid);

        // Setup ESC key handler
        dropdownWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(dropdownWindow);
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(dropdownWindow);
        _windowSystem?.LogService.LogInfo("Dropdown demo window created");
    }

    /// <summary>
    /// Create ListView demo window (adapted from ListViewWindow.cs)
    /// </summary>
    private static void CreateListViewDemo()
    {
        if (_windowSystem == null)
            return;

        var listWindow = new WindowBuilder(_windowSystem)
            .WithTitle("ListView Demo")
            .WithSize(65, 22)
            .AtPosition(6, 6)
            .WithColors(SpectreColor.Grey19, SpectreColor.Grey93)
            .Build();

        // Add title
        listWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[bold]List Control Demonstration[/]",
                    "[dim](Adapted with modern selection handling)[/]",
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                StickyPosition = StickyPosition.Top,
            }
        );

        listWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Create selection info display
        var selectionInfo = new MarkupControl(new List<string> { "No item selected" })
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        listWindow.AddControl(selectionInfo);
        listWindow.AddControl(new MarkupControl(new List<string> { " " }));

        // Create list control
        var listControl = new ListControl("Available Items")
        {
            Width = 55,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxVisibleItems = 10,
            AutoAdjustWidth = true,
        };

        // Add diverse items to demonstrate features
        listControl.AddItem(new ListItem("Text Document", "●", SpectreColor.Green));
        listControl.AddItem(new ListItem("Image File\nJPEG format", "■", SpectreColor.Yellow));
        listControl.AddItem(new ListItem("Spreadsheet", "★", SpectreColor.Blue));
        listControl.AddItem("Folder Item");
        listControl.AddItem(new ListItem("Music File", "♦", SpectreColor.Red));
        listControl.AddItem("Plain Text Item");
        listControl.AddItem(new ListItem("Video File", "♥", SpectreColor.Magenta1));
        listControl.AddItem(new ListItem("Archive\nZipped content", "◆", SpectreColor.Cyan1));
        listControl.AddItem("Tool Item");
        listControl.AddItem("Contact Entry");

        // Handle selection changes with modern logging
        listControl.SelectedIndexChanged += (sender, selectedIndex) =>
        {
            if (selectedIndex >= 0)
            {
                var item = listControl.SelectedItem;
                var displayText = item?.Text.Split('\n')[0] ?? "Unknown";
                selectionInfo.SetContent(
                    new List<string>
                    {
                        $"Selected: [green]{displayText}[/] (Index: {selectedIndex})",
                    }
                );
                _windowSystem?.LogService.LogDebug(
                    $"List item selected: {displayText} at index {selectedIndex}"
                );
            }
            else
            {
                selectionInfo.SetContent(new List<string> { "No item selected" });
            }
        };

        // Select first item by default
        listControl.SelectedIndex = 0;
        listWindow.AddControl(listControl);

        // Add action buttons
        var buttonsGrid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Bottom,
        };

        var selectButton = new ButtonControl { Text = "Select" };
        selectButton.Click += (sender, button) =>
        {
            var selectedItem = listControl.SelectedItem;
            if (selectedItem != null)
            {
                var displayText = selectedItem.Text.Split('\n')[0];
                _windowSystem?.LogService.LogInfo("User selected item: {Item}", displayText);
            }
        };

        var closeButton = new ButtonControl { Text = "Close" };
        closeButton.Click += (sender, button) =>
        {
            _windowSystem?.CloseWindow(listWindow);
        };

        var selectColumn = new ColumnContainer(buttonsGrid);
        selectColumn.AddContent(selectButton);
        buttonsGrid.AddColumn(selectColumn);

        var closeColumn = new ColumnContainer(buttonsGrid);
        closeColumn.AddContent(closeButton);
        buttonsGrid.AddColumn(closeColumn);

        listWindow.AddControl(buttonsGrid);

        // Setup ESC key handler
        listWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(listWindow);
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(listWindow);
        _windowSystem?.LogService.LogInfo("ListView demo window created");
    }

    /// <summary>
    /// Create File Explorer window (adapted from FileExplorerWindow.cs)
    /// </summary>
    private static void CreateFileExplorerWindow()
    {
        if (_windowSystem == null)
            return;

        var explorerWindow = new WindowBuilder(_windowSystem)
            .WithTitle("File Explorer Demo")
            .WithSize(75, 26)
            .AtPosition(3, 3)
            .Build();

        // Add title and instructions
        explorerWindow.AddControl(
            new MarkupControl(
                new List<string>
                {
                    "[bold]File System Explorer[/] [dim](Adapted with modern patterns)[/]",
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                StickyPosition = StickyPosition.Top,
            }
        );

        explorerWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Create button container
        var buttonContainer = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            StickyPosition = StickyPosition.Top,
        };

        // Create main panel with splitter
        var mainPanel = new HorizontalGridControl { VerticalAlignment = VerticalAlignment.Fill };

        // Left panel - Tree control for folders
        var treeColumn = new ColumnContainer(mainPanel) { Width = 20 }; // Moved splitter left

        var fileTree = new TreeControl
        {
            Margin = new Margin(1, 1, 1, 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            HighlightBackgroundColor = SpectreColor.Blue,
            HighlightForegroundColor = SpectreColor.White,
            Guide = TreeGuide.Line,
            VerticalAlignment = VerticalAlignment.Fill,
        };

        treeColumn.AddContent(fileTree);
        mainPanel.AddColumn(treeColumn);

        // Right panel - File list
        var fileColumn = new ColumnContainer(mainPanel);
        fileColumn.AddContent(
            new MarkupControl(new List<string> { "[bold]Files in Selected Folder[/]" })
            {
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        );

        var fileList = new ListControl
        {
            Margin = new Margin(1, 1, 1, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxVisibleItems = null,
            VerticalAlignment = VerticalAlignment.Fill,
            IsSelectable = true,
        };

        fileColumn.AddContent(fileList);
        mainPanel.AddColumn(fileColumn);

        // Add splitter between panels
        mainPanel.AddSplitter(0, new SplitterControl());

        // Status bar
        var statusControl = new MarkupControl(
            new List<string> { "Select a folder to view its contents" }
        )
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            StickyPosition = StickyPosition.Bottom,
        };

        // Add controls to window
        explorerWindow.AddControl(buttonContainer);
        explorerWindow.AddControl(mainPanel);
        explorerWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom });
        explorerWindow.AddControl(statusControl);

        // Initialize with current directory
        try
        {
            var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
            var rootNode = fileTree.AddRootNode($"[{currentDir.Name}]");
            rootNode.TextColor = SpectreColor.Yellow;
            rootNode.Tag = currentDir;

            // Add placeholder for expansion
            if (HasSubdirectories(currentDir))
            {
                rootNode.AddChild("Loading...");
                rootNode.IsExpanded = false;
            }

            // Load initial file list
            UpdateFileList(currentDir, fileList, statusControl);
        }
        catch (Exception ex)
        {
            statusControl.SetContent(
                new List<string> { $"[red]Error initializing: {ex.Message}[/]" }
            );
            _windowSystem?.LogService.LogError("Error initializing file explorer", ex);
        }

        // Tree selection handler
        fileTree.SelectedNodeChanged += (tree, args) =>
        {
            if (args.Node?.Tag is DirectoryInfo dirInfo)
            {
                statusControl.SetContent(
                    new List<string> { $"Selected: [yellow]{dirInfo.FullName}[/]" }
                );
                UpdateFileList(dirInfo, fileList, statusControl);
            }
        };

        // Tree expand/collapse handler
        fileTree.NodeExpandCollapse += (tree, args) =>
        {
            if (args.Node != null && args.Node.IsExpanded && args.Node.Tag is DirectoryInfo dirInfo)
            {
                // Clear placeholder
                args.Node.ClearChildren();

                try
                {
                    // Load subdirectories
                    var subdirs = dirInfo
                        .GetDirectories()
                        .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                        .OrderBy(d => d.Name)
                        .Take(50); // Limit for performance

                    foreach (var subdir in subdirs)
                    {
                        var childNode = args.Node.AddChild($"[{subdir.Name}]");
                        childNode.TextColor = SpectreColor.Yellow;
                        childNode.Tag = subdir;

                        if (HasSubdirectories(subdir))
                        {
                            childNode.AddChild("Loading...");
                            childNode.IsExpanded = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    statusControl.SetContent(
                        new List<string> { $"[red]Error loading: {ex.Message}[/]" }
                    );
                }
            }
        };

        // Add control buttons
        var expandButton = new ButtonControl { Width = 12, Text = "Expand All" };
        expandButton.Click += (sender, button) => fileTree.ExpandAll();

        var collapseButton = new ButtonControl { Width = 12, Text = "Collapse All" };
        collapseButton.Click += (sender, button) => fileTree.CollapseAll();

        var refreshButton = new ButtonControl { Width = 12, Text = "Refresh" };
        refreshButton.Click += (sender, button) =>
        {
            fileTree.Clear();
            fileList.ClearItems();
            // Reinitialize...
            try
            {
                var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
                var rootNode = fileTree.AddRootNode($"[{currentDir.Name}]");
                rootNode.TextColor = SpectreColor.Yellow;
                rootNode.Tag = currentDir;
                UpdateFileList(currentDir, fileList, statusControl);
            }
            catch (Exception ex)
            {
                statusControl.SetContent(
                    new List<string> { $"[red]Refresh failed: {ex.Message}[/]" }
                );
            }
        };

        // Add buttons to container
        var expandButtonColumn = new ColumnContainer(buttonContainer);
        expandButtonColumn.AddContent(expandButton);
        buttonContainer.AddColumn(expandButtonColumn);

        var collapseButtonColumn = new ColumnContainer(buttonContainer);
        collapseButtonColumn.AddContent(collapseButton);
        buttonContainer.AddColumn(collapseButtonColumn);

        var refreshButtonColumn = new ColumnContainer(buttonContainer);
        refreshButtonColumn.AddContent(refreshButton);
        buttonContainer.AddColumn(refreshButtonColumn);

        // Setup ESC key handler
        explorerWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(explorerWindow);
                e.Handled = true;
            }
        };

        _windowSystem?.AddWindow(explorerWindow);
        _windowSystem?.LogService.LogInfo("File explorer window created");
    }

    /// <summary>
    /// Check if directory has subdirectories (helper for file explorer)
    /// </summary>
    private static bool HasSubdirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.GetDirectories().Any(d => (d.Attributes & FileAttributes.Hidden) == 0);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Update file list for selected directory (helper for file explorer)
    /// </summary>
    private static void UpdateFileList(
        DirectoryInfo directory,
        ListControl fileList,
        MarkupControl statusControl
    )
    {
        try
        {
            fileList.ClearItems();

            var files = directory
                .GetFiles()
                .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                .OrderBy(f => f.Name)
                .Take(100); // Limit for performance

            if (!files.Any())
            {
                fileList.AddItem("No files in this folder", "i", SpectreColor.Grey);
                return;
            }

            foreach (var file in files)
            {
                var icon = GetFileIcon(file.Extension);
                var color = GetFileColor(file.Extension);
                var sizeText = FormatFileSize(file.Length);
                var displayText =
                    $"{file.Name}\n{sizeText} • {file.LastWriteTime:yyyy-MM-dd HH:mm}";

                var listItem = new ListItem(displayText, icon, color);
                listItem.Tag = file;
                fileList.AddItem(listItem);
            }

            statusControl.SetContent(
                new List<string>
                {
                    $"Loaded {files.Count()} files from [yellow]{directory.FullName}[/]",
                }
            );
        }
        catch (Exception ex)
        {
            fileList.ClearItems();
            fileList.AddItem("Error: " + ex.Message, "!", SpectreColor.Red);
            statusControl.SetContent(
                new List<string> { $"[red]Error loading files: {ex.Message}[/]" }
            );
        }
    }

    /// <summary>
    /// Get appropriate icon for file extension
    /// </summary>
    private static string GetFileIcon(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".exe" or ".bat" or ".cmd" => ">",
            ".dll" or ".lib" => "#",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "*",
            ".txt" or ".log" or ".md" => "=",
            ".doc" or ".docx" or ".pdf" => "=",
            ".zip" or ".rar" or ".7z" => "[",
            ".mp3" or ".wav" or ".flac" => "~",
            ".mp4" or ".avi" or ".mkv" => ">",
            _ => ".",
        };
    }

    /// <summary>
    /// Get appropriate color for file extension
    /// </summary>
    private static SpectreColor GetFileColor(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".exe" or ".bat" or ".cmd" => SpectreColor.Green,
            ".dll" or ".lib" => SpectreColor.Blue,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => SpectreColor.Magenta1,
            ".txt" or ".log" or ".md" => SpectreColor.Yellow,
            ".doc" or ".docx" or ".pdf" => SpectreColor.Cyan1,
            ".zip" or ".rar" or ".7z" => SpectreColor.Red,
            _ => SpectreColor.White,
        };
    }

    /// <summary>
    /// Format file size in human readable format
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0 ? $"{size:0} {suffixes[order]}" : $"{size:0.##} {suffixes[order]}";
    }

    /// <summary>
    /// Create comprehensive layout demo window showcasing complete application UI patterns
    /// </summary>
    private static void CreateComprehensiveLayoutDemo()
    {
        if (_windowSystem == null)
            return;

        try
        {
            var comprehensiveWindow = new ComprehensiveLayoutWindow(_windowSystem);
            comprehensiveWindow.Show();
            _windowSystem?.LogService.LogInfo(
                "Comprehensive layout demo window created using separate class"
            );
        }
        catch (Exception ex)
        {
            _windowSystem?.LogService.LogError(
                "Error creating comprehensive layout demo window",
                ex
            );
        }
    }
}

/// <summary>
/// Event data for window operations using records (modern C# feature)
/// Demonstrates immutable data structures
/// </summary>
public record WindowCreatedEvent(string WindowId, string WindowType, DateTime Timestamp);

public record WindowClosedEvent(string WindowId, DateTime Timestamp);

public record LogEntryEvent(string Message, string Level, DateTime Timestamp);
