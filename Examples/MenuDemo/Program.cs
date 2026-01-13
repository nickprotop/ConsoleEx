using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Spectre.Console;

namespace MenuDemo;

class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _mainWindow;
    private static MarkupControl? _statusControl;
    private static int _actionCounter = 0;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Initialize window system
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "MenuControl Demo - Full-Featured Menu with Keyboard & Mouse Support",
                BottomStatus = "Use Arrow Keys, Enter, Escape, Home/End, or Mouse | Tab to focus menu | ESC to close | Ctrl+C to Quit",
                ShowTaskBar = true
            };

            // Graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            // Create main window
            CreateMainWindow(_windowSystem);

            // Run application
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

    static void CreateMainWindow(ConsoleWindowSystem windowSystem)
    {
        // Calculate responsive window size based on terminal dimensions
        var screenWidth = windowSystem.DesktopDimensions.Width;
        var screenHeight = windowSystem.DesktopDimensions.Height;

        // Use 90% of screen width (max 140) and 90% of height (max 35)
        var windowWidth = Math.Min(140, (int)(screenWidth * 0.9));
        var windowHeight = Math.Min(35, (int)(screenHeight * 0.9));

        // Ensure minimum viable size
        windowWidth = Math.Max(60, windowWidth);
        windowHeight = Math.Max(20, windowHeight);

        _mainWindow = new WindowBuilder(windowSystem)
            .WithTitle("MenuControl Showcase")
            .WithSize(windowWidth, windowHeight)
            .Centered()
            .WithColors(Color.Grey11, Color.White)
            .Build();

        // Create horizontal menu bar
        var menu = Controls.Menu()
            .Horizontal()
            .WithName("mainMenu")
            .Sticky()
            .AddItem("File", m => m
                .AddItem("New", "Ctrl+N", () => LogAction("New file created"))
                .AddItem("Open", "Ctrl+O", () => LogAction("Open file dialog"))
                .AddItem("Open Recent", sub => sub
                    .AddItem("Document1.txt", () => LogAction("Opened Document1.txt"))
                    .AddItem("Document2.txt", () => LogAction("Opened Document2.txt"))
                    .AddItem("Document3.txt", () => LogAction("Opened Document3.txt"))
                    .AddSeparator()
                    .AddItem("Clear Recent Files", () => LogAction("Recent files cleared")))
                .AddSeparator()
                .AddItem("Save", "Ctrl+S", () => LogAction("File saved"))
                .AddItem("Save As...", "Ctrl+Shift+S", () => LogAction("Save As dialog"))
                .AddSeparator()
                .AddItem("Close", "Ctrl+W", () => LogAction("File closed"))
                .AddItem("Exit", "Alt+F4", () => { LogAction("Exiting application"); _windowSystem?.Shutdown(0); }))
            .AddItem("Edit", m => m
                .AddItem("Undo", "Ctrl+Z", () => LogAction("Undo last action"))
                .AddItem("Redo", "Ctrl+Y", () => LogAction("Redo last action"))
                .AddSeparator()
                .AddItem("Cut", "Ctrl+X", () => LogAction("Cut to clipboard"))
                .AddItem("Copy", "Ctrl+C", () => LogAction("Copy to clipboard"))
                .AddItem("Paste", "Ctrl+V", () => LogAction("Paste from clipboard"))
                .AddSeparator()
                .AddItem("Find", "Ctrl+F", () => LogAction("Find dialog opened"))
                .AddItem("Replace", "Ctrl+H", () => LogAction("Replace dialog opened"))
                .AddSeparator()
                .AddItem("Select All", "Ctrl+A", () => LogAction("All text selected")))
            .AddItem("View", m => m
                .AddItem("Zoom", sub => sub
                    .AddItem("Zoom In", "Ctrl++", () => LogAction("Zoomed in"))
                    .AddItem("Zoom Out", "Ctrl+-", () => LogAction("Zoomed out"))
                    .AddItem("Reset Zoom", "Ctrl+0", () => LogAction("Zoom reset to 100%"))
                    .AddSeparator()
                    .AddItem("Actual Size", () => LogAction("Zoom set to actual size")))
                .AddSeparator()
                .AddItem("Toggle Sidebar", "Ctrl+B", () => LogAction("Sidebar toggled"))
                .AddItem("Toggle Panel", "Ctrl+J", () => LogAction("Panel toggled"))
                .AddItem("Toggle Fullscreen", "F11", () => LogAction("Fullscreen toggled")))
            .AddItem("Format", m => m
                .AddItem("Font", sub => sub
                    .AddItem("Increase Font Size", "Ctrl+>", () => LogAction("Font size increased"))
                    .AddItem("Decrease Font Size", "Ctrl+<", () => LogAction("Font size decreased"))
                    .AddSeparator()
                    .AddItem("Font Family", subsub => subsub
                        .AddItem("Consolas", () => LogAction("Font changed to Consolas"))
                        .AddItem("Courier New", () => LogAction("Font changed to Courier New"))
                        .AddItem("Monaco", () => LogAction("Font changed to Monaco"))
                        .AddItem("Source Code Pro", () => LogAction("Font changed to Source Code Pro"))))
                .AddSeparator()
                .AddItem("Bold", "Ctrl+B", () => LogAction("Bold formatting applied"))
                .AddItem("Italic", "Ctrl+I", () => LogAction("Italic formatting applied"))
                .AddItem("Underline", "Ctrl+U", () => LogAction("Underline formatting applied")))
            .AddItem("Tools", m => m
                .AddItem("Options", () => LogAction("Options dialog opened"))
                .AddItem("Preferences", () => LogAction("Preferences dialog opened"))
                .AddSeparator()
                .AddItem("Extensions", () => LogAction("Extensions manager opened"))
                .AddItem("Command Palette", "Ctrl+Shift+P", () => LogAction("Command palette opened")))
            .AddItem("Help", m => m
                .AddItem("Documentation", "F1", () => LogAction("Documentation opened"))
                .AddItem("Keyboard Shortcuts", "Ctrl+K Ctrl+S", () => LogAction("Keyboard shortcuts opened"))
                .AddSeparator()
                .AddItem("Check for Updates", () => LogAction("Checking for updates..."))
                .AddItem("About", () => LogAction("About dialog opened")))
            .OnItemSelected((sender, item) =>
            {
                // This fires for every menu item selection
                LogAction($"Item path: {item.GetPath()}");
            })
            .Build();

        _mainWindow.AddControl(menu);

        // Add content panel with instructions
        var instructions = Controls.Markup()
            .AddLine("")
            .AddLine("[bold cyan]Welcome to the MenuControl Showcase![/]")
            .AddLine("")
            .AddLine("[yellow]Features demonstrated:[/]")
            .AddLine("• Horizontal menu bar with unlimited nesting depth")
            .AddLine("• Full keyboard navigation (Arrow keys, Enter, Escape, Home/End, Letter keys)")
            .AddLine("• Complete mouse support (Click, hover, delayed submenu opening)")
            .AddLine("• Separators between menu items")
            .AddLine("• Keyboard shortcut display (display only, not handled)")
            .AddLine("• Disabled items (none in this demo)")
            .AddLine("• Action callbacks with logging")
            .AddLine("")
            .AddLine("[yellow]Try these interactions:[/]")
            .AddLine("1. [cyan]Tab[/] to focus the menu bar")
            .AddLine("2. Use [cyan]Left/Right[/] arrows to navigate top-level items")
            .AddLine("3. Press [cyan]Down[/] to open a dropdown")
            .AddLine("4. Use [cyan]Up/Down[/] to navigate within dropdown")
            .AddLine("5. Press [cyan]Right[/] to open submenus, [cyan]Left[/] to close them")
            .AddLine("6. Press [cyan]Enter[/] to execute an item")
            .AddLine("7. Press [cyan]Escape[/] to close menus or unfocus")
            .AddLine("8. Try [cyan]Home/End[/] to jump to first/last item")
            .AddLine("9. Press letter keys to jump to items starting with that letter")
            .AddLine("10. [cyan]Click[/] menu items with the mouse")
            .AddLine("11. [cyan]Hover[/] over items with children to see delayed submenu opening")
            .AddLine("12. Explore the deep nesting in [cyan]Format > Font > Font Family[/]")
            .AddLine("")
            .AddLine("[dim]Action log:[/]")
            .Build();

        _mainWindow.AddControl(instructions);

        // Add status control for action logging
        _statusControl = Controls.Markup()
            .AddLine("[dim]No actions yet...[/]")
            .WithName("statusLog")
            .Build();

        _mainWindow.AddControl(_statusControl);

        windowSystem.AddWindow(_mainWindow);
    }

    static void LogAction(string action)
    {
        _actionCounter++;
        if (_statusControl != null)
        {
            _statusControl.SetContent(new List<string>
            {
                $"[green]Action #{_actionCounter}:[/] {action}"
            });
        }
    }
}
