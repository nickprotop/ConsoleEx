// -----------------------------------------------------------------------
// Modern SharpConsoleUI Example - Comprehensive Demo
// Demonstrates all features with modern patterns adapted from the original examples
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Spectre.Console;
using System.Diagnostics;
using System.IO;
using SpectreColor = Spectre.Console.Color;

namespace ModernExample;

/// <summary>
/// Modern comprehensive example demonstrating SharpConsoleUI's enhanced features
/// with multiple windows adapted from the original examples
/// </summary>
internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static IServiceProvider? _serviceProvider;
    private static ILogger<Program>? _logger;

    // Window references for management
    private static Window? _mainWindow;
    private static Window? _logWindow;
    private static Window? _clockWindow;
    private static Window? _sysInfoWindow;

    /// <summary>
    /// Application entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Setup services with dependency injection
            SetupServices();

            // Initialize console window system with modern patterns
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "Modern SharpConsoleUI Demo - Press F1-F10 for windows",
                BottomStatus = "ESC: Close Window | F2-F6,F7-F10: Demo Windows | Ctrl+Q: Quit"
            };

            // Setup graceful shutdown handler for Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                _logger?.LogInformation("Received interrupt signal, shutting down gracefully...");
                e.Cancel = true; // Prevent immediate termination
                _windowSystem?.Shutdown(0);
            };

            // Create main menu window using fluent builder
            CreateMainMenuWindow();

            // Set up key handlers for the main window
            SetupMainWindowKeyHandlers();

            // Run the application
            _logger?.LogInformation("Starting Modern SharpConsoleUI Demo");
            await Task.Run(() => _windowSystem.Run());

            _logger?.LogInformation("Application shutting down");
            return 0;
        }
        catch (Exception ex)
        {
            // Use logger if available for critical startup errors
            _logger?.LogCritical(ex, "Fatal application error during startup");

            // If logger is not available, we can't safely output anything
            // as the console system may be in a corrupted state

            return 1;
        }
    }

    /// <summary>
    /// Setup dependency injection and services
    /// </summary>
    private static void SetupServices()
    {
        var services = new ServiceCollection();

        // Add logging - NEVER use AddConsole() in UI apps as it corrupts the display!
        // For UI applications, we either disable logging or use non-console providers
        services.AddLogging(builder =>
        {
            // For demo purposes, we'll use minimal logging to avoid console output
            // In production apps, use file logging, database logging, etc.

#if DEBUG
            // In debug mode, you might want to see logs - use EventLog or file logging
            // builder.AddEventLog(); // Windows Event Log (Windows only)
#endif

            // Set minimum level but no console output
            builder.SetMinimumLevel(LogLevel.Warning); // Only show warnings/errors
        });

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetService<ILogger<Program>>();

        _logger?.LogInformation("Services configured successfully");
    }

    /// <summary>
    /// Create the main menu window using fluent builder pattern
    /// </summary>
    private static void CreateMainMenuWindow()
    {
        if (_windowSystem == null) return;

        _mainWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Modern SharpConsoleUI Demo - Main Menu")
            .WithSize(65, 22)
            .Centered()
            .WithColors(SpectreColor.DarkBlue, SpectreColor.White)
            .Build();

        // Add welcome content with markup
        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold yellow]Welcome to Modern SharpConsoleUI![/]"
        }));

        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

        // Feature showcase
        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[green]Enhanced Features Demonstrated:[/]",
            "• [cyan]Fluent Builder Pattern[/] - Chainable window creation",
            "• [magenta]Async Patterns[/] - Background tasks and real-time updates",
            "• [yellow]Modern C# Features[/] - Records, nullable refs, top-level",
            "• [blue]Service Integration[/] - Dependency injection ready",
            "• [red]Enhanced Controls[/] - Rich markup and styling",
            "• [white]Comprehensive Examples[/] - Multiple window types"
        }));

        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

        // Window showcase adapted from original examples
        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold]Available Demo Windows (adapted from originals):[/]",
            "",
            "[green]F2[/] - [bold]Real-time Log Window[/]",
            "       Demonstrates async logging with live updates",
            "",
            "[blue]F3[/] - [bold]System Information Window[/]",
            "       Shows system stats with modern data gathering",
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
            "[red]F10[/] - [bold]File Explorer[/]",
            "        Tree control with file system navigation"
        }));

        _mainWindow.AddControl(new MarkupControl(new List<string> { "" }));
        _mainWindow.AddControl(new MarkupControl(new List<string>
        {
            "[dim]Navigation:[/]",
            "[dim]• Press function keys (F2-F6) to open demo windows[/]",
            "[dim]• Press ESC in any window to close it[/]",
            "[dim]• Press Ctrl+Q in main window to exit[/]"
        }));

        _windowSystem.AddWindow(_mainWindow);
        _logger?.LogInformation("Main menu window created with fluent builder");
    }

    /// <summary>
    /// Set up key handlers for the main window
    /// </summary>
    private static void SetupMainWindowKeyHandlers()
    {
        if (_mainWindow == null) return;

        _mainWindow.KeyPressed += async (sender, e) =>
        {
            try
            {
                switch (e.KeyInfo.Key)
                {
                    case ConsoleKey.F2:
                        await CreateLogWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F3:
                        CreateSystemInfoWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F5:
                        await CreateClockWindow();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F6:
                        CreateInteractiveDemo();
                        e.Handled = true;
                        break;
                    case ConsoleKey.F7:
                        await CreateCommandWindow();
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
                    case ConsoleKey.F10:
                        CreateFileExplorerWindow();
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
                _logger?.LogError(ex, "Error handling key press in main window");
            }
        };

        _logger?.LogInformation("Main window key handlers configured");
    }

    /// <summary>
    /// Create log window with real-time updates (adapted from LogWindow.cs)
    /// </summary>
    private static async Task CreateLogWindow()
    {
        if (_windowSystem == null) return;

        _logWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Real-time Log Viewer")
            .WithSize(70, 15)
            .AtPosition(5, 5)
            .WithColors(SpectreColor.Black, SpectreColor.Green)
            .Build();

        // Add initial content
        _logWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold green]Real-time Log Viewer[/]",
            "[dim](Adapted from original LogWindow with async patterns)[/]",
            ""
        }));

        // Setup ESC key handler
        _logWindow.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(_logWindow);
                e.Handled = true;
            }
        };

        // Start background logging task (modern async pattern)
        _ = Task.Run(async () => await SimulateLoggingAsync());

        _windowSystem.AddWindow(_logWindow);
        _logger?.LogInformation("Log window created with real-time updates");
    }

    /// <summary>
    /// Simulate real-time logging updates using modern async patterns
    /// </summary>
    private static async Task SimulateLoggingAsync()
    {
        for (int i = 1; i <= 30; i++)
        {
            if (_logWindow == null) break;

            await Task.Delay(800); // Faster updates for demo

            var logMessage = $"[{DateTime.Now:HH:mm:ss}] [yellow]Log #{i:D2}[/] - Processing task [green]OK[/]";
            _logWindow.AddControl(new MarkupControl(new List<string> { logMessage }));

            _logger?.LogDebug("Added log entry {LogNumber}", i);

            // Auto-scroll to show latest entries
            _logWindow.GoToBottom();
        }

        if (_logWindow != null)
        {
            _logWindow.AddControl(new MarkupControl(new List<string>
            {
                "",
                "[bold red]Logging simulation completed![/]",
                "[dim]Press ESC to close this window[/]"
            }));
        }
    }

    /// <summary>
    /// Create system information window (adapted from SystemInfoWindow.cs)
    /// </summary>
    private static void CreateSystemInfoWindow()
    {
        if (_windowSystem == null) return;

        _sysInfoWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("System Information")
            .WithSize(75, 18)
            .AtPosition(8, 3)
            .WithColors(SpectreColor.DarkCyan, SpectreColor.White)
            .Build();

        // System information using modern patterns
        var sysInfo = GetSystemInformation();

        _sysInfoWindow.AddControl(new MarkupControl(new List<string>
        {
            "[bold cyan]System Information Dashboard[/]",
            "[dim](Adapted from original with modern data gathering)[/]",
            ""
        }));

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
        _logger?.LogInformation("System information window created");
    }

    /// <summary>
    /// Create clock window with real-time updates (adapted from ClockWindow.cs)
    /// </summary>
    private static async Task CreateClockWindow()
    {
        if (_windowSystem == null) return;

        _clockWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Digital Clock")
            .WithSize(35, 10)
            .AtPosition(15, 8)
            .WithColors(SpectreColor.DarkGreen, SpectreColor.Yellow)
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
        _logger?.LogInformation("Clock window created with async updates");
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
            _clockWindow.AddControl(new MarkupControl(new List<string>
            {
                "[bold yellow]Digital Clock[/]",
                "[dim](Adapted with async updates)[/]",
                "",
                $"[bold green]{now:HH:mm:ss}[/]",
                $"[cyan]{now:dddd}[/]",
                $"[white]{now:MMMM dd, yyyy}[/]",
                "",
                "[dim]Updates every second • ESC to close[/]"
            }));

            await Task.Delay(1000);
        }
    }

    /// <summary>
    /// Create interactive demo window
    /// </summary>
    private static void CreateInteractiveDemo()
    {
        if (_windowSystem == null) return;

        var demoWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Interactive Demo")
            .WithSize(60, 16)
            .AtPosition(12, 6)
            .WithColors(SpectreColor.Purple, SpectreColor.White)
            .Build();

        demoWindow.AddControl(new MarkupControl(new List<string>
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
            "• Any other key - Shows key info"
        }));

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
            demoWindow.AddControl(new MarkupControl(new List<string>
            {
                $"[cyan]Key pressed: {e.KeyInfo.Key} (Char: '{e.KeyInfo.KeyChar}')[/]"
            }));

            _logger?.LogDebug("Key pressed in demo window: {Key}", e.KeyInfo.Key);
            demoWindow.GoToBottom();
            e.Handled = true;
        };

        _windowSystem.AddWindow(demoWindow);
        _logger?.LogInformation("Interactive demo window created");
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
            info.Add($"[green]Current Directory:[/] {(Environment.CurrentDirectory.Length > 50 ? "..." + Environment.CurrentDirectory.Substring(Environment.CurrentDirectory.Length - 47) : Environment.CurrentDirectory)}");

            // Modern C# features demo
            var memoryInfo = GC.GetTotalMemory(false);
            info.Add($"[green]GC Memory:[/] {memoryInfo / (1024 * 1024):N2} MB");

            var uptime = Environment.TickCount64;
            var uptimeSpan = TimeSpan.FromMilliseconds(uptime);
            info.Add($"[green]System Uptime:[/] {uptimeSpan.Days}d {uptimeSpan.Hours}h {uptimeSpan.Minutes}m");

            info.Add("");
            info.Add("[dim]Press ESC to close this window[/]");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error gathering system information");
            info.Add($"[red]Error gathering system info: {ex.Message}[/]");
        }

        return info;
    }

    /// <summary>
    /// Create command window with interactive command prompt (adapted from CommandWindow.cs)
    /// </summary>
    private static async Task CreateCommandWindow()
    {
        if (_windowSystem == null) return;

        var commandWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Interactive Command Window")
            .WithSize(80, 25)
            .AtPosition(2, 2)
            .WithColors(SpectreColor.Black, SpectreColor.White)
            .Build();

        // Create prompt control for command input
        var promptControl = new PromptControl
        {
            Prompt = "CMD> ",
            UnfocusOnEnter = false,
            StickyPosition = StickyPosition.Top
        };

        // Create multiline output control
        var outputControl = new MultilineEditControl
        {
            ViewportHeight = commandWindow.Height - 4, // Leave space for prompt and borders
            WrapMode = WrapMode.Wrap,
            ReadOnly = true
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
        outputControl.AppendContent("Interactive command prompt started. Modern async implementation.\n");
        outputControl.AppendContent("Type 'help' for available commands, 'exit' to close.\n");

        // Setup command execution with modern async patterns
        promptControl.OnEnter = async (sender, command) =>
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
                _logger?.LogError(ex, "Error executing command: {Command}", command);
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
        _logger?.LogInformation("Command window created with modern async patterns");
    }

    /// <summary>
    /// Handle built-in commands that don't need external process execution
    /// </summary>
    private static async Task<bool> HandleBuiltInCommand(string command, MultilineEditControl output, Window window)
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
                output.AppendContent("\nAll other commands will be executed as external processes.\n");
                return true;

            case "clear":
                output.SetContent("");
                output.AppendContent("Interactive command prompt started. Modern async implementation.\n");
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
                output.AppendContent($"Working Set: {Environment.WorkingSet / (1024 * 1024):N0} MB\n");
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
            _logger?.LogError(ex, "Failed to execute external command: {Command}", command);
        }
    }

    /// <summary>
    /// Create dropdown demo window (adapted from DropDownWindow.cs)
    /// </summary>
    private static void CreateDropdownDemo()
    {
        if (_windowSystem == null) return;

        var dropdownWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("Country Selection Demo")
            .WithSize(50, 20)
            .AtPosition(4, 4)
            .WithColors(SpectreColor.DarkBlue, SpectreColor.White)
            .Build();

        // Add title
        dropdownWindow.AddControl(new MarkupControl(new List<string> 
        { 
            "[bold]Country Selection Form[/]",
            "[dim](Adapted with modern patterns)[/]" 
        })
        {
            Alignment = Alignment.Center,
            StickyPosition = StickyPosition.Top
        });

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
            Alignment = Alignment.Center
        };
        dropdownWindow.AddControl(statusControl);

        // Handle selection changes with modern patterns
        countryDropdown.SelectedItemChanged += (sender, item) =>
        {
            if (item != null)
            {
                statusControl.SetContent(new List<string> { $"Selected: [green]{item.Text}[/]" });
                dropdownWindow.Title = $"Country Selection - {item.Text}";
                _logger?.LogDebug("Country selection changed to: {Country}", item.Text);
            }
        };

        // Add action buttons
        var buttonsGrid = new HorizontalGridControl 
        { 
            Alignment = Alignment.Center, 
            StickyPosition = StickyPosition.Bottom 
        };

        var okButton = new ButtonControl { Text = "OK" };
        okButton.Click += (sender, button) =>
        {
            var selected = countryDropdown.SelectedValue;
            _logger?.LogInformation("User selected country: {Country}", selected);
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
        _logger?.LogInformation("Dropdown demo window created");
    }

    /// <summary>
    /// Create ListView demo window (adapted from ListViewWindow.cs)
    /// </summary>
    private static void CreateListViewDemo()
    {
        if (_windowSystem == null) return;

        var listWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("ListView Demo")
            .WithSize(65, 22)
            .AtPosition(6, 6)
            .WithColors(SpectreColor.DarkGreen, SpectreColor.White)
            .Build();

        // Add title
        listWindow.AddControl(new MarkupControl(new List<string> 
        { 
            "[bold]List Control Demonstration[/]",
            "[dim](Adapted with modern selection handling)[/]" 
        })
        {
            Alignment = Alignment.Center,
            StickyPosition = StickyPosition.Top
        });

        listWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Create selection info display
        var selectionInfo = new MarkupControl(new List<string> { "No item selected" })
        {
            Alignment = Alignment.Left
        };
        listWindow.AddControl(selectionInfo);
        listWindow.AddControl(new MarkupControl(new List<string> { " " }));

        // Create list control
        var listControl = new ListControl("Available Items")
        {
            Width = 55,
            Alignment = Alignment.Center,
            MaxVisibleItems = 10,
            AutoAdjustWidth = true
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
                selectionInfo.SetContent(new List<string> { $"Selected: [green]{displayText}[/] (Index: {selectedIndex})" });
                _logger?.LogDebug("List item selected: {Item} at index {Index}", displayText, selectedIndex);
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
            Alignment = Alignment.Center, 
            StickyPosition = StickyPosition.Bottom 
        };

        var selectButton = new ButtonControl { Text = "Select" };
        selectButton.Click += (sender, button) =>
        {
            var selectedItem = listControl.SelectedItem;
            if (selectedItem != null)
            {
                var displayText = selectedItem.Text.Split('\n')[0];
                _logger?.LogInformation("User selected item: {Item}", displayText);
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
        _logger?.LogInformation("ListView demo window created");
    }

    /// <summary>
    /// Create File Explorer window (adapted from FileExplorerWindow.cs)
    /// </summary>
    private static void CreateFileExplorerWindow()
    {
        if (_windowSystem == null) return;

        var explorerWindow = new WindowBuilder(_windowSystem, _serviceProvider)
            .WithTitle("File Explorer Demo")
            .WithSize(75, 26)
            .AtPosition(3, 3)
            .WithColors(SpectreColor.DarkCyan, SpectreColor.White)
            .Build();

        // Add title and instructions
        explorerWindow.AddControl(new MarkupControl(new List<string> 
        { 
            "[bold]File System Explorer[/] [dim](Adapted with modern patterns)[/]" 
        })
        {
            Alignment = Alignment.Center,
            StickyPosition = StickyPosition.Top
        });

        explorerWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Create button container
        var buttonContainer = new HorizontalGridControl
        {
            Alignment = Alignment.Left,
            StickyPosition = StickyPosition.Top
        };

        // Create main panel with splitter
        var mainPanel = new HorizontalGridControl();

        // Left panel - Tree control for folders
        var treeColumn = new ColumnContainer(mainPanel) { Width = 35 };
        
        var fileTree = new TreeControl
        {
            Margin = new Margin(1, 1, 1, 1),
            Alignment = Alignment.Left,
            HighlightBackgroundColor = SpectreColor.Blue,
            HighlightForegroundColor = SpectreColor.White,
            Guide = TreeGuide.Line
        };

        treeColumn.AddContent(fileTree);
        mainPanel.AddColumn(treeColumn);

        // Right panel - File list
        var fileColumn = new ColumnContainer(mainPanel);
        fileColumn.AddContent(new MarkupControl(new List<string> { "[bold]Files in Selected Folder[/]" })
        {
            Alignment = Alignment.Center
        });

        var fileList = new ListControl
        {
            Margin = new Margin(1, 1, 1, 1),
            Alignment = Alignment.Strecth,
            MaxVisibleItems = null,
            FillHeight = true,
            IsSelectable = true
        };

        fileColumn.AddContent(fileList);
        mainPanel.AddColumn(fileColumn);

        // Add splitter between panels
        mainPanel.AddSplitter(0, new SplitterControl());

        // Status bar
        var statusControl = new MarkupControl(new List<string> { "Select a folder to view its contents" })
        {
            Alignment = Alignment.Left,
            StickyPosition = StickyPosition.Bottom
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
            statusControl.SetContent(new List<string> { $"[red]Error initializing: {ex.Message}[/]" });
            _logger?.LogError(ex, "Error initializing file explorer");
        }

        // Tree selection handler
        fileTree.OnSelectedNodeChanged = (tree, node) =>
        {
            if (node?.Tag is DirectoryInfo dirInfo)
            {
                statusControl.SetContent(new List<string> { $"Selected: [yellow]{dirInfo.FullName}[/]" });
                UpdateFileList(dirInfo, fileList, statusControl);
            }
        };

        // Tree expand/collapse handler
        fileTree.OnNodeExpandCollapse = (tree, node) =>
        {
            if (node.IsExpanded && node.Tag is DirectoryInfo dirInfo)
            {
                // Clear placeholder
                node.ClearChildren();
                
                try
                {
                    // Load subdirectories
                    var subdirs = dirInfo.GetDirectories()
                        .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                        .OrderBy(d => d.Name)
                        .Take(50); // Limit for performance

                    foreach (var subdir in subdirs)
                    {
                        var childNode = node.AddChild($"[{subdir.Name}]");
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
                    statusControl.SetContent(new List<string> { $"[red]Error loading: {ex.Message}[/]" });
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
                statusControl.SetContent(new List<string> { $"[red]Refresh failed: {ex.Message}[/]" });
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

        _windowSystem.AddWindow(explorerWindow);
        _logger?.LogInformation("File explorer window created");
    }

    /// <summary>
    /// Check if directory has subdirectories (helper for file explorer)
    /// </summary>
    private static bool HasSubdirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.GetDirectories()
                .Any(d => (d.Attributes & FileAttributes.Hidden) == 0);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Update file list for selected directory (helper for file explorer)
    /// </summary>
    private static void UpdateFileList(DirectoryInfo directory, ListControl fileList, MarkupControl statusControl)
    {
        try
        {
            fileList.ClearItems();
            
            var files = directory.GetFiles()
                .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                .OrderBy(f => f.Name)
                .Take(100); // Limit for performance

            if (!files.Any())
            {
                fileList.AddItem("📂 No files in this folder", "ℹ", SpectreColor.Grey);
                return;
            }

            foreach (var file in files)
            {
                var icon = GetFileIcon(file.Extension);
                var color = GetFileColor(file.Extension);
                var sizeText = FormatFileSize(file.Length);
                var displayText = $"{file.Name}\n{sizeText} • {file.LastWriteTime:yyyy-MM-dd HH:mm}";
                
                var listItem = new ListItem(displayText, icon, color);
                listItem.Tag = file;
                fileList.AddItem(listItem);
            }

            statusControl.SetContent(new List<string> { $"Loaded {files.Count()} files from [yellow]{directory.FullName}[/]" });
        }
        catch (Exception ex)
        {
            fileList.ClearItems();
            fileList.AddItem($"❌ Error: {ex.Message}", "⚠", SpectreColor.Red);
            statusControl.SetContent(new List<string> { $"[red]Error loading files: {ex.Message}[/]" });
        }
    }

    /// <summary>
    /// Get appropriate icon for file extension
    /// </summary>
    private static string GetFileIcon(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".exe" or ".bat" or ".cmd" => "▶",
            ".dll" or ".lib" => "◆",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "◆",
            ".txt" or ".log" or ".md" => "■",
            ".doc" or ".docx" or ".pdf" => "■",
            ".zip" or ".rar" or ".7z" => "□",
            ".mp3" or ".wav" or ".flac" => "♪",
            ".mp4" or ".avi" or ".mkv" => "►",
            _ => "·"
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
            _ => SpectreColor.White
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
}

/// <summary>
/// Event data for window operations using records (modern C# feature)
/// Demonstrates immutable data structures
/// </summary>
public record WindowCreatedEvent(string WindowId, string WindowType, DateTime Timestamp);
public record WindowClosedEvent(string WindowId, DateTime Timestamp);
public record LogEntryEvent(string Message, string Level, DateTime Timestamp);