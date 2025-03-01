# ConsoleEx

A simple console window system for .NET Core.

## Overview

ConsoleEx is a lightweight console window system implementation for .NET Core applications. It provides a windowing system with support for multiple overlapping windows, keyboard and mouse input, and customizable themes.

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

### Key Components and Classes of the ConsoleEx Window System

The ConsoleEx window system is a sophisticated console-based windowing framework for .NET applications. It provides a rich terminal user interface (TUI) with windowing capabilities, input handling, and rendering features. Here's a breakdown of its key components:
Core Components

1. ConsoleWindowSystem
The central orchestrator that manages the entire window system:
•	Maintains a collection of windows and their z-order
•	Handles window activation and focus management
•	Coordinates input events and dispatches them to the appropriate windows
•	Manages screen updates and rendering
•	Provides desktop-like functionality (status bars, window operations)

2. Window
Represents an individual window in the system:
•	Contains and manages content elements
•	Handles input directed to the window
•	Supports window states (normal, minimized, maximized)
•	Provides scrolling capabilities
•	Manages focus between interactive content elements
•	Supports window events (closing, activation changes, state changes)

3. IConsoleDriver and NetConsoleDriver
The abstraction and implementation for console I/O operations:
•	Handles raw keyboard and mouse input
•	Manages console modes and settings
•	Processes ANSI escape sequences
•	Provides cross-platform compatibility layer
•	Supports different rendering modes (direct or buffered)

4. ConsoleBuffer
A double-buffering mechanism for rendering:
•	Maintains front and back buffers to reduce screen flicker
•	Optimizes rendering by only updating changed areas
•	Handles ANSI color sequences and character placement
•	Provides efficient content rendering
1. 
Rendering Subsystem
5. Renderer
Responsible for drawing windows and their content:
•	Renders window borders, titles, and content
•	Handles overlapping windows and clipping
•	Draws UI elements like scrollbars
•	Fills rectangular areas with specified characters/colors

6. VisibleRegions
Computes which parts of windows are visible:
•	Calculates visible regions of overlapping windows
•	Creates clipping rectangles for efficient rendering
•	Handles window intersection calculations

Content System
7. IWIndowContent
Interface for all content that can be placed in windows:
•	Provides rendering capability for content
•	Manages content invalidation
•	Supports positioning and sizing

8. Interactive Content Components
Various UI controls implementing both IWIndowContent and IInteractiveContent:
•	PromptContent: Text input fields
•	ButtonContent: Clickable buttons
•	MarkupContent: Rich text display with formatting
•	FigletContent: ASCII art text display
•	RuleContent: Horizontal rule/separator

9. Layout Components
Content organizers for more complex layouts:
•	HorizontalGridContent: Arranges content in columns
•	ColumnContainer: Contains content within a grid column

Services
10. Notifications
A service for showing popup notifications:
•	Displays temporary windows with messages
•	Supports different severity levels (Info, Warning, Error, etc.)
•	Provides timeout capabilities
•	Supports blocking and non-blocking notifications

Helpers and Utilities
11. Theme
Defines the visual appearance of the system:
•	Colors for window borders, backgrounds, and text
•	Color schemes for different UI states
•	Notification styling

12. Helper Classes
Various utility classes that support the system:
•	AnsiConsoleHelper: Handles ANSI escape sequence processing
•	SequenceHelper: Contains ANSI control sequences
•	ContentHelper: Assists with content layout
•	MouseFlags: Enumerations for mouse event types

Event System
13. Event Handling
Rich event system for interaction:
•	Keyboard events with modifier detection
•	Mouse events (click, double-click, movement)
•	Window state change events
•	Content interaction events

Architecture Overview
The system follows a layered architecture:
1.	Input Layer (IConsoleDriver/NetConsoleDriver): Captures raw input from the console
2.	Control Layer (ConsoleWindowSystem): Manages windows and routes events
3.	Presentation Layer (Window, Renderer): Displays content and handles window-specific behavior
4.	Content Layer (IWIndowContent implementations): Provides the actual UI elements
This modular design allows for:
•	Easy extension with new content types
•	Customization of appearance through themes
•	Platform independence through the driver abstraction
•	Efficient rendering through optimization techniques
 1. 
The system combines these components to create a powerful yet lightweight window system that runs entirely within a text-based console environment, providing GUI-like capabilities without requiring a graphical environment.


## License

MIT License
