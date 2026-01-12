// -----------------------------------------------------------------------
// BorderStyleDemo - Demonstrates window border styles
// Shows both DoubleLine (default) and None (borderless) border styles
// Uses modern patterns: async/await, fluent builders
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Spectre.Console;

namespace BorderStyleDemo;

/// <summary>
/// Demo showing BorderStyle enum functionality
/// </summary>
internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _window1;
    private static Window? _window2;
    private static Window? _window3;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Initialize console window system
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "BorderStyle Demo - F1: DoubleLine | F2: Borderless | F3: Toggle | F10: Exit",
                BottomStatus = "ESC: Close Window | Tab: Switch Windows | Ctrl+Q: Quit"
            };

            // Setup graceful shutdown handler for Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                _windowSystem?.LogService.LogInfo("Received interrupt signal, shutting down gracefully...");
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            // Create demo windows
            CreateDoubleLineWindow();
            CreateBorderlessWindow();
            CreateToggleWindow();

            // Run the application
            _windowSystem.LogService.LogInfo("Starting BorderStyle Demo");
            await Task.Run(() => _windowSystem.Run());

            _windowSystem.LogService.LogInfo("Application shutting down");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void CreateDoubleLineWindow()
    {
        if (_windowSystem == null) return;

        // Create window with DoubleLine border (default)
        _window1 = new WindowBuilder(_windowSystem)
            .WithTitle("DoubleLine Border (Default)")
            .WithSize(60, 20)
            .AtPosition(5, 3)
            .WithBorderStyle(BorderStyle.DoubleLine)
            .Build();

        // Add header
        _window1.AddControl(new MarkupControl(new List<string>
        {
            "[bold cyan]BorderStyle.DoubleLine[/]",
            "[dim]The classic border with box-drawing characters[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Top
        });

        _window1.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Add main content
        _window1.AddControl(new MarkupControl(new List<string>
        {
            "",
            "[yellow]Features:[/]",
            "",
            "  [green]•[/] Active border:   ╔═╗║╚╝ (double-line)",
            "  [green]•[/] Inactive border: ┌─┐│└┘ (single-line)",
            "  [green]•[/] Title bar visible",
            "  [green]•[/] Window buttons visible",
            "  [green]•[/] Resize grip: ◢",
            "",
            "[dim]This is the default border style[/]",
            "[dim]Used by all windows unless changed[/]",
            "",
            "[bold]Press [yellow]F1[/] to show this window[/]"
        }));

        // Handle F1 to show window
        _window1.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.F10)
            {
                _windowSystem?.Shutdown();
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(_window1);
    }

    private static void CreateBorderlessWindow()
    {
        if (_windowSystem == null) return;

        // Create borderless window using Borderless() method
        _window2 = new WindowBuilder(_windowSystem)
            .WithTitle("This Title Won't Show")
            .WithSize(60, 20)
            .AtPosition(70, 3)
            .Borderless()  // Convenience method
            .Build();

        // Add header
        _window2.AddControl(new MarkupControl(new List<string>
        {
            "[bold magenta]BorderStyle.None[/]",
            "[dim]Borderless window with invisible borders[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Top
        });

        _window2.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Add main content
        _window2.AddControl(new MarkupControl(new List<string>
        {
            "",
            "[yellow]Features:[/]",
            "",
            "  [green]•[/] No visible borders",
            "  [green]•[/] Border space filled with window background",
            "  [green]•[/] No title bar",
            "  [green]•[/] No window buttons",
            "  [green]•[/] All dimension calculations preserved",
            "  [green]•[/] Scrollbar still works (at X=width-1)",
            "",
            "[dim]Created with:[/]",
            "[cyan].Borderless()[/]",
            "[dim]or[/]",
            "[cyan].WithBorderStyle(BorderStyle.None)[/]",
            "",
            "[bold]Press [yellow]F2[/] to show this window[/]"
        }));

        // Handle F10 to exit
        _window2.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.F10)
            {
                _windowSystem?.Shutdown();
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(_window2);
    }

    private static void CreateToggleWindow()
    {
        if (_windowSystem == null) return;

        // Create window that toggles between border styles
        _window3 = new WindowBuilder(_windowSystem)
            .WithTitle("Toggle Window")
            .WithSize(70, 25)
            .AtPosition(40, 12)
            .Build();

        var statusLabel = new MarkupControl(new List<string>
        {
            $"[green]Current BorderStyle: {_window3.BorderStyle}[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Add header
        _window3.AddControl(new MarkupControl(new List<string>
        {
            "[bold yellow]Interactive Border Toggle[/]",
            "[dim]Press the button or Space to toggle border style[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Top
        });

        _window3.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

        // Add status
        _window3.AddControl(statusLabel);
        _window3.AddControl(new MarkupControl(new List<string> { "" }));

        // Add toggle button
        var toggleButton = new ButtonControl
        {
            Text = "Toggle Border Style (Space)",
            Width = 40,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        int toggleCount = 0;
        toggleButton.Click += (s, e) =>
        {
            toggleCount++;
            // Toggle between DoubleLine and None
            _window3.BorderStyle = _window3.BorderStyle == BorderStyle.DoubleLine
                ? BorderStyle.None
                : BorderStyle.DoubleLine;

            statusLabel.SetContent(new List<string>
            {
                $"[green]Current BorderStyle: {_window3.BorderStyle}[/]",
                $"[dim]Toggled {toggleCount} time(s)[/]"
            });

            _windowSystem?.LogService.LogInfo(
                $"BorderStyle toggled to: {_window3.BorderStyle}",
                "Toggle"
            );
        };

        _window3.AddControl(toggleButton);

        // Add info
        _window3.AddControl(new MarkupControl(new List<string>
        {
            "",
            "",
            "[yellow]What happens when toggling:[/]",
            "",
            "  [green]DoubleLine → None:[/]",
            "    • Borders become invisible (rendered as spaces)",
            "    • Title bar disappears",
            "    • Window buttons disappear",
            "    • Content area dimensions stay the same",
            "",
            "  [green]None → DoubleLine:[/]",
            "    • Borders become visible again",
            "    • Title bar reappears",
            "    • Window buttons reappear",
            "    • Everything works normally",
            "",
            "[bold]Press [yellow]F3[/] to show this window[/]",
            "[bold]Press [yellow]Space[/] or click button to toggle[/]"
        }));

        // Handle Space and F10
        _window3.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Spacebar)
            {
                // Manually trigger button click logic
                toggleCount++;
                _window3.BorderStyle = _window3.BorderStyle == BorderStyle.DoubleLine
                    ? BorderStyle.None
                    : BorderStyle.DoubleLine;

                statusLabel.SetContent(new List<string>
                {
                    $"[green]Current BorderStyle: {_window3.BorderStyle}[/]",
                    $"[dim]Toggled {toggleCount} time(s)[/]"
                });

                _windowSystem?.LogService.LogInfo(
                    $"BorderStyle toggled to: {_window3.BorderStyle}",
                    "Toggle"
                );
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.F10)
            {
                _windowSystem?.Shutdown();
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(_window3);
    }
}
