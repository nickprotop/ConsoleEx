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

## License

MIT License
