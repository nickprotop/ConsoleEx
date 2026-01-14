# SharpConsoleUI

A simple console window system for .NET Core.

## Overview

SharpConsoleUI is a lightweight console window system implementation for .NET Core applications. It provides a windowing system with support for multiple overlapping windows, keyboard and mouse input, and customizable themes.

## Features

### ConsoleWindowSystem

ConsoleWindowSystem is the core component that manages the console window environment:

- **Window Management**
  - Create and manage multiple windows
  - Handle window activation and focus
  - Support for overlapping windows with proper Z-order
  - Window cycling (Alt+1-9, Ctrl+T)

- **Input Handling**
  - Keyboard input with modifier key support
  - Mouse input capture and processing
  - Input queuing system

- **Rendering**
  - Double-buffered rendering 
  - Efficient partial updates for dirty regions
  - Support for different render modes (Direct or Buffer)
  - Status bar rendering (top and bottom)

- **Layout**
  - Desktop environment simulation
  - Dynamic window resizing and positioning
  - Automatic window repositioning on console resize

- **UI**
  - Themeable interface
  - Window flashing for notifications

### Window

Window provides a container for content and handles rendering:

- **Appearance**
  - Customizable title
  - Border drawing with active/inactive states
  - Support for different background and foreground colors
  - Theme integration

- **Window States**
  - Normal, minimized, and maximized states
  - Window restoration

- **Content Management**
  - Content invalidation system for efficient updates
  - Support for interactive content elements
  - Sticky positioning for footer content
  - Scrollable content with automatic scrolling

- **Input Handling**
  - Focus management between interactive elements
  - Tab navigation between focusable elements
  - Keyboard event dispatching

- **Window Operations**
  - Resizable windows (minimum/maximum size constraints)
  - Movable windows
  - Window closing with confirmation

### NetConsoleDriver

NetConsoleDriver is the implementation of IConsoleDriver that interfaces with the .NET Console:

- **Input Management**
  - Mouse input handling
  - Keyboard input processing with support for:
    - Control characters
    - Alt key combinations
    - Arrow keys and function keys
    - ANSI escape sequence parsing
  
- **Output Features**
  - ANSI color and formatting sequence support
  - Virtual terminal processing
  - Cursor positioning
  
- **Rendering Modes**
  - Direct mode (immediate console updates)
  - Buffer mode (double buffering for flicker-free rendering)
  
- **Console Events**
  - Window resize detection
  - Mouse event generation
  - Keyboard event generation
  
- **Platform Support**
  - Windows console API integration
  - Cross-platform compatibility considerations

## Getting Started

### Basic Setup

Here's a minimal example showing how to initialize the ConsoleWindowSystem and create a simple window with markup:

```cs
using System; 
using System.Collections.Generic; 
using System.Threading.Tasks; 
using SharpConsoleUI; 
using SharpConsoleUI.Controls; 
using SharpConsoleUI.Themes; 
using Spectre.Console;

namespace YourNamespace 
{ 
    class Program 
    { 
        static async Task Main(string[] args) 
        { 
            // Initialize the console window system 
            var consoleWindowSystem = new ConsoleWindowSystem(RenderMode.Buffer) 
            { 
                TopStatus = "SharpConsoleUI Example", 
                BottomStatus = "Ctrl-Q to Quit", 
                Theme = new Theme 
                { 
                    DesktopBackroundChar = '.', 
                    DesktopBackgroundColor = Color.Black, 
                    DesktopForegroundColor = Color.Grey
                }
            };

            // Create a simple window with async thread for updates
            var window = new Window(consoleWindowSystem, WindowThreadAsync)
            {
                Title = "Hello World",
                Left = 10,
                Top = 5,
                Width = 50,
                Height = 15,
                IsResizable = true
            };

            // Add the window to the console window system
            consoleWindowSystem.AddWindow(window);

            // Make this window the active window
            consoleWindowSystem.SetActiveWindow(window);

            // Run the console window system (this blocks until the application exits)
            int exitCode = consoleWindowSystem.Run();

            Console.WriteLine($"Application exited with code: {exitCode}");
        }

        // Window thread function that adds content to the window
        static async Task WindowThreadAsync(Window window, CancellationToken ct)
        {
            // Create a markup control with formatted text
            var markupContent = new MarkupControl(new List<string>
            {
                "[bold yellow]Welcome to SharpConsoleUI![/]",
                "",
                "This is a [green]simple[/] demonstration of using [cyan]markup[/] in a window.",
                "",
                "[red]Colors[/], [underline]formatting[/], and [bold]styles[/] are supported."
            });
        
            // Add the markup content to the window
            window.AddContent(markupContent);
        
            // Add a horizontal rule
            window.AddContent(new RuleControl
            {
                Title = "Controls Demo",
                TitleAlignment = Justify.Center
            });
        
            // Add a button control
            var button = new ButtonControl
            {
                Text = "[blue]Click Me![/]",
                Margin = new Margin(1, 0, 0, 0)
            };
        
            // Add button click handler
            button.OnClick += (sender) => 
            {
                markupContent.SetContent(new List<string>
                {
                    "[bold green]Button was clicked![/]",
                    $"Current time: {DateTime.Now}"
                });
            };
        
            // Add the button to the window
            window.AddContent(button);
        }
    }
}
```

### Displaying Dynamic Content

You can update window content dynamically, for example to show system statistics:

```cs
public async void WindowThread(Window window) 
{ 
    // Create a markup control for content 
    var contentControl = new MarkupControl(new List<string> { "Loading data..." }); 
    window.AddContent(contentControl);

    // Update the content every few seconds
    while (true)
    {
        var systemInfo = new List<string>
        {
            $"[yellow]CPU Usage:[/] [green]{GetCpuUsage()}%[/]",
            $"[yellow]Memory Available:[/] [green]{GetAvailableMemory()} MB[/]",
            $"[yellow]Time:[/] [cyan]{DateTime.Now:HH:mm:ss}[/]"
        };
        
        // Update the control with new content
        contentControl.SetContent(systemInfo);
    
        // Wait before updating again
        await Task.Delay(1000);
    }
}
```

## License

MIT License
