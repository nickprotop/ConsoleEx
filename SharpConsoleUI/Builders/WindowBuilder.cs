// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Models;
using SharpConsoleUI.Themes;
using SpectreColor = Spectre.Console.Color;
using DrawingSize = System.Drawing.Size;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating and configuring windows
/// </summary>
public sealed class WindowBuilder
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly IServiceProvider? _services;

    private string? _title;
    private WindowBounds? _bounds;
    private SpectreColor? _backgroundColor;
    private SpectreColor? _foregroundColor;
    private WindowMode _mode = WindowMode.Normal;
    private WindowState _state = WindowState.Normal;
    private bool _isResizable = true;
    private bool _isMovable = true;
    private int? _minWidth;
    private int? _minHeight;
    private int? _maxWidth;
    private int? _maxHeight;
    private Window? _parentWindow;
    private readonly List<IWindowControl> _controls = new();
    private Window.WindowThreadDelegate? _windowThread;
    private Window.WindowThreadDelegateAsync? _asyncWindowThread;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowBuilder"/> class
    /// </summary>
    /// <param name="windowSystem">The console window system</param>
    /// <param name="services">Optional service provider</param>
    public WindowBuilder(ConsoleWindowSystem windowSystem, IServiceProvider? services = null)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _services = services;
    }

    /// <summary>
    /// Sets the window title
    /// </summary>
    /// <param name="title">The window title</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the window bounds
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithBounds(int x, int y, int width, int height)
    {
        _bounds = new WindowBounds(x, y, width, height);
        return this;
    }

    /// <summary>
    /// Sets the window bounds
    /// </summary>
    /// <param name="bounds">The window bounds</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithBounds(WindowBounds bounds)
    {
        _bounds = bounds;
        return this;
    }

    /// <summary>
    /// Sets the window position
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder AtPosition(int x, int y)
    {
        _bounds = _bounds?.WithPosition(x, y) ?? new WindowBounds(x, y, 80, 25);
        return this;
    }

    /// <summary>
    /// Sets the window size
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithSize(int width, int height)
    {
        _bounds = _bounds?.WithSize(width, height) ?? new WindowBounds(0, 0, width, height);
        return this;
    }

    /// <summary>
    /// Centers the window on the screen
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder Centered()
    {
        var screenWidth = _windowSystem.DesktopDimensions.Width;
        var screenHeight = _windowSystem.DesktopDimensions.Height;
        var windowWidth = _bounds?.Width ?? 80;
        var windowHeight = _bounds?.Height ?? 25;

        var x = (screenWidth - windowWidth) / 2;
        var y = (screenHeight - windowHeight) / 2;

        return AtPosition(x, y);
    }

    /// <summary>
    /// Sets the window background color
    /// </summary>
    /// <param name="color">The background color</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithBackgroundColor(SpectreColor color)
    {
        _backgroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the window foreground color
    /// </summary>
    /// <param name="color">The foreground color</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithForegroundColor(SpectreColor color)
    {
        _foregroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the window colors
    /// </summary>
    /// <param name="backgroundColor">The background color</param>
    /// <param name="foregroundColor">The foreground color</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithColors(SpectreColor backgroundColor, SpectreColor foregroundColor)
    {
        _backgroundColor = backgroundColor;
        _foregroundColor = foregroundColor;
        return this;
    }

    /// <summary>
    /// Sets the window mode
    /// </summary>
    /// <param name="mode">The window mode</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithMode(WindowMode mode)
    {
        _mode = mode;
        return this;
    }

    /// <summary>
    /// Makes the window modal
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder AsModal()
    {
        _mode = WindowMode.Modal;
        return this;
    }

    /// <summary>
    /// Sets the window state
    /// </summary>
    /// <param name="state">The window state</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithState(WindowState state)
    {
        _state = state;
        return this;
    }

    /// <summary>
    /// Makes the window maximized
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder Maximized()
    {
        _state = WindowState.Maximized;
        return this;
    }

    /// <summary>
    /// Sets whether the window is resizable
    /// </summary>
    /// <param name="resizable">Whether the window is resizable</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder Resizable(bool resizable = true)
    {
        _isResizable = resizable;
        return this;
    }

    /// <summary>
    /// Sets whether the window is movable
    /// </summary>
    /// <param name="movable">Whether the window is movable</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder Movable(bool movable = true)
    {
        _isMovable = movable;
        return this;
    }

    /// <summary>
    /// Sets the minimum window size
    /// </summary>
    /// <param name="minWidth">The minimum width</param>
    /// <param name="minHeight">The minimum height</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithMinimumSize(int minWidth, int minHeight)
    {
        _minWidth = minWidth;
        _minHeight = minHeight;
        return this;
    }

    /// <summary>
    /// Sets the maximum window size
    /// </summary>
    /// <param name="maxWidth">The maximum width</param>
    /// <param name="maxHeight">The maximum height</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithMaximumSize(int maxWidth, int maxHeight)
    {
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        return this;
    }

    /// <summary>
    /// Sets the parent window
    /// </summary>
    /// <param name="parent">The parent window</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithParent(Window parent)
    {
        _parentWindow = parent;
        return this;
    }

    /// <summary>
    /// Adds a control to the window
    /// </summary>
    /// <param name="control">The control to add</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder AddControl(IWindowControl control)
    {
        if (control != null)
        {
            _controls.Add(control);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple controls to the window
    /// </summary>
    /// <param name="controls">The controls to add</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder AddControls(params IWindowControl[] controls)
    {
        foreach (var control in controls)
        {
            AddControl(control);
        }
        return this;
    }

    /// <summary>
    /// Adds a control using a builder pattern
    /// </summary>
    /// <typeparam name="T">The control type</typeparam>
    /// <param name="configure">Configuration action for the control</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder AddControl<T>(Action<T> configure) where T : class, IWindowControl, new()
    {
        var control = _services?.GetService<T>() ?? new T();
        configure(control);
        return AddControl(control);
    }

    /// <summary>
    /// Sets the window thread method
    /// </summary>
    /// <param name="threadMethod">The window thread method</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithWindowThread(Window.WindowThreadDelegate threadMethod)
    {
        _windowThread = threadMethod;
        return this;
    }

    /// <summary>
    /// Sets the async window thread method
    /// </summary>
    /// <param name="asyncThreadMethod">The async window thread method</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithAsyncWindowThread(Window.WindowThreadDelegateAsync asyncThreadMethod)
    {
        _asyncWindowThread = asyncThreadMethod;
        return this;
    }

    /// <summary>
    /// Builds the window with the configured settings
    /// </summary>
    /// <returns>The created window</returns>
    public Window Build()
    {
        Window window;

        // Create window based on configured thread method
        if (_asyncWindowThread != null)
        {
            window = new Window(_windowSystem, _asyncWindowThread, _parentWindow);
        }
        else if (_windowThread != null)
        {
            window = new Window(_windowSystem, _windowThread, _parentWindow);
        }
        else
        {
            window = new Window(_windowSystem, _parentWindow);
        }

        // Apply configuration
        if (_title != null)
            window.Title = _title;

        if (_bounds != null)
        {
            window.Left = _bounds.X;
            window.Top = _bounds.Y;
            window.Width = _bounds.Width;
            window.Height = _bounds.Height;
        }

        if (_backgroundColor.HasValue)
            window.BackgroundColor = _backgroundColor.Value;

        if (_foregroundColor.HasValue)
            window.ForegroundColor = _foregroundColor.Value;

        window.Mode = _mode;
        window.State = _state;
        window.IsResizable = _isResizable;
        window.IsMovable = _isMovable;

        // Note: MinimumWidth, MinimumHeight, MaximumWidth, MaximumHeight are private fields in Window
        // and cannot be set from the builder. These properties would need to be exposed publicly
        // in the Window class if needed.

        // Add controls
        foreach (var control in _controls)
        {
            window.AddControl(control);
        }

        return window;
    }

    /// <summary>
    /// Builds and shows the window
    /// </summary>
    /// <param name="activate">Whether to activate the window</param>
    /// <returns>The created window</returns>
    public Window BuildAndShow(bool activate = true)
    {
        var window = Build();
        _windowSystem.AddWindow(window, activate);
        return window;
    }

    /// <summary>
    /// Applies a theme to the window builder
    /// </summary>
    /// <param name="theme">The theme to apply</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithTheme(ITheme theme)
    {
        if (theme == null)
            return this;

        return WithColors(theme.WindowBackgroundColor, theme.WindowForegroundColor);
    }

    /// <summary>
    /// Applies a preconfigured window template
    /// </summary>
    /// <param name="template">The window template</param>
    /// <returns>The builder for chaining</returns>
    public WindowBuilder WithTemplate(WindowTemplate template)
    {
        return template.Configure(this);
    }
}

/// <summary>
/// Abstract base class for window templates
/// </summary>
public abstract class WindowTemplate
{
    /// <summary>
    /// Configures the window builder with this template
    /// </summary>
    /// <param name="builder">The window builder</param>
    /// <returns>The configured builder</returns>
    public abstract WindowBuilder Configure(WindowBuilder builder);
}

/// <summary>
/// Template for dialog windows
/// </summary>
public sealed class DialogTemplate : WindowTemplate
{
    private readonly string _title;
    private readonly int _width;
    private readonly int _height;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogTemplate"/> class
    /// </summary>
    /// <param name="title">The dialog title</param>
    /// <param name="width">The dialog width</param>
    /// <param name="height">The dialog height</param>
    public DialogTemplate(string title, int width = 50, int height = 15)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        _width = width;
        _height = height;
    }

    /// <inheritdoc />
    public override WindowBuilder Configure(WindowBuilder builder)
    {
        return builder
            .WithTitle(_title)
            .WithSize(_width, _height)
            .Centered()
            .AsModal()
            .Resizable(false);
    }
}

/// <summary>
/// Template for tool windows
/// </summary>
public sealed class ToolWindowTemplate : WindowTemplate
{
    private readonly string _title;
    private readonly Point _position;
    private readonly DrawingSize _size;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolWindowTemplate"/> class
    /// </summary>
    /// <param name="title">The tool window title</param>
    /// <param name="position">The window position</param>
    /// <param name="size">The window size</param>
    public ToolWindowTemplate(string title, Point position, DrawingSize size)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        _position = position;
        _size = size;
    }

    /// <inheritdoc />
    public override WindowBuilder Configure(WindowBuilder builder)
    {
        return builder
            .WithTitle(_title)
            .AtPosition(_position.X, _position.Y)
            .WithSize(_size.Width, _size.Height)
            .Resizable(true)
            .Movable(true);
    }
}