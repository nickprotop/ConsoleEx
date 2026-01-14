// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using SpectreColor = Spectre.Console.Color;
using DrawingSize = System.Drawing.Size;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Provides a fluent builder pattern for creating and configuring windows in the console window system.
/// Use method chaining to configure window properties before calling <see cref="Build"/> or <see cref="BuildAndShow"/>.
/// </summary>
/// <example>
/// <code>
/// var window = new WindowBuilder(windowSystem)
///     .WithTitle("My Window")
///     .WithSize(80, 25)
///     .Centered()
///     .Build();
/// </code>
/// </example>
public sealed class WindowBuilder
{
    private readonly ConsoleWindowSystem _windowSystem;

    private string? _title;
    private string? _name;
    private WindowBounds? _bounds;
    private SpectreColor? _backgroundColor;
    private SpectreColor? _foregroundColor;
    private WindowMode _mode = WindowMode.Normal;
    private WindowState _state = WindowState.Normal;
    private bool _isResizable = true;
    private bool _isClosable = true;
    private bool _isMovable = true;
    private bool _isMinimizable = true;
    private bool _isMaximizable = true;
    private int? _minWidth;
    private int? _minHeight;
    private int? _maxWidth;
    private int? _maxHeight;
    private Window? _parentWindow;
    private readonly List<IWindowControl> _controls = new();
    private Window.WindowThreadDelegateAsync? _asyncWindowThread;
    private bool _alwaysOnTop = false;
    private EventHandler? _activatedHandler;
    private EventHandler? _deactivatedHandler;
    private EventHandler<KeyPressedEventArgs>? _keyPressedHandler;
    private EventHandler? _closedHandler;
    private EventHandler<ClosingEventArgs>? _closingHandler;
    private EventHandler? _resizeHandler;
    private EventHandler? _shownHandler;
    private EventHandler<Window.WindowStateChangedEventArgs>? _stateChangedHandler;
    private BorderStyle _borderStyle = BorderStyle.DoubleLine;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowBuilder"/> class.
    /// </summary>
    /// <param name="windowSystem">The console window system that will manage the created window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="windowSystem"/> is null.</exception>
    public WindowBuilder(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
    }

    /// <summary>
    /// Sets the window title displayed in the title bar.
    /// </summary>
    /// <param name="title">The title text to display.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the window name for singleton window patterns.
    /// Windows can be found and activated by name using <see cref="ConsoleWindowSystem.ActivateOrCreate"/>.
    /// </summary>
    /// <param name="name">The unique window name used for identification.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the window bounds including position and size.
    /// </summary>
    /// <param name="x">The X coordinate of the window's left edge.</param>
    /// <param name="y">The Y coordinate of the window's top edge.</param>
    /// <param name="width">The width of the window in characters.</param>
    /// <param name="height">The height of the window in characters.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithBounds(int x, int y, int width, int height)
    {
        _bounds = new WindowBounds(x, y, width, height);
        return this;
    }

    /// <summary>
    /// Sets the window bounds using a <see cref="WindowBounds"/> instance.
    /// </summary>
    /// <param name="bounds">The bounds defining position and size of the window.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithBounds(WindowBounds bounds)
    {
        _bounds = bounds;
        return this;
    }

    /// <summary>
    /// Sets the window position without changing its size.
    /// If no size has been set, defaults to 80x25 characters.
    /// </summary>
    /// <param name="x">The X coordinate of the window's left edge.</param>
    /// <param name="y">The Y coordinate of the window's top edge.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder AtPosition(int x, int y)
    {
        _bounds = _bounds?.WithPosition(x, y) ?? new WindowBounds(x, y, 80, 25);
        return this;
    }

    /// <summary>
    /// Sets the window size without changing its position.
    /// If no position has been set, defaults to position (0, 0).
    /// </summary>
    /// <param name="width">The width of the window in characters.</param>
    /// <param name="height">The height of the window in characters.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithSize(int width, int height)
    {
        _bounds = _bounds?.WithSize(width, height) ?? new WindowBounds(0, 0, width, height);
        return this;
    }

    /// <summary>
    /// Centers the window on the screen based on the current desktop dimensions.
    /// Should be called after <see cref="WithSize"/> to ensure correct centering.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
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
    /// Sets the window background color.
    /// </summary>
    /// <param name="color">The background color for the window content area.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithBackgroundColor(SpectreColor color)
    {
        _backgroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the window foreground color used for text rendering.
    /// </summary>
    /// <param name="color">The foreground color for the window content area.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithForegroundColor(SpectreColor color)
    {
        _foregroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets both the background and foreground colors for the window.
    /// </summary>
    /// <param name="backgroundColor">The background color for the window content area.</param>
    /// <param name="foregroundColor">The foreground color for text in the window content area.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithColors(SpectreColor backgroundColor, SpectreColor foregroundColor)
    {
        _backgroundColor = backgroundColor;
        _foregroundColor = foregroundColor;
        return this;
    }

    /// <summary>
    /// Sets the window mode (normal or modal).
    /// </summary>
    /// <param name="mode">The window mode to apply.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithMode(WindowMode mode)
    {
        _mode = mode;
        return this;
    }

    /// <summary>
    /// Makes the window modal, blocking input to other windows until closed.
    /// Equivalent to calling <see cref="WithMode"/> with <see cref="WindowMode.Modal"/>.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder AsModal()
    {
        _mode = WindowMode.Modal;
        return this;
    }

    /// <summary>
    /// Sets the initial window state (normal, minimized, or maximized).
    /// </summary>
    /// <param name="state">The initial window state.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithState(WindowState state)
    {
        _state = state;
        return this;
    }

    /// <summary>
    /// Makes the window start in maximized state, filling the available desktop area.
    /// Equivalent to calling <see cref="WithState"/> with <see cref="WindowState.Maximized"/>.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Maximized()
    {
        _state = WindowState.Maximized;
        return this;
    }

    /// <summary>
    /// Sets whether the window can be resized by the user.
    /// </summary>
    /// <param name="resizable">True to allow resizing; false to prevent it. Defaults to true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Resizable(bool resizable = true)
    {
        _isResizable = resizable;
        return this;
    }

    /// <summary>
    /// Sets whether the window shows a close button and can be closed by the user.
    /// </summary>
    /// <param name="closable">True to show the close button; false to hide it. Defaults to true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Closable(bool closable = true)
    {
        _isClosable = closable;
        return this;
    }

    /// <summary>
    /// Sets whether the window can be moved by dragging the title bar.
    /// </summary>
    /// <param name="movable">True to allow moving; false to prevent it. Defaults to true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Movable(bool movable = true)
    {
        _isMovable = movable;
        return this;
    }

    /// <summary>
    /// Sets whether the window shows a minimize button and can be minimized.
    /// </summary>
    /// <param name="minimizable">True to show the minimize button; false to hide it. Defaults to true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Minimizable(bool minimizable = true)
    {
        _isMinimizable = minimizable;
        return this;
    }

    /// <summary>
    /// Sets whether the window shows a maximize button and can be maximized.
    /// </summary>
    /// <param name="maximizable">True to show the maximize button; false to hide it. Defaults to true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Maximizable(bool maximizable = true)
    {
        _isMaximizable = maximizable;
        return this;
    }

    /// <summary>
    /// Sets the border style for the window.
    /// </summary>
    /// <param name="borderStyle">The border style to use.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithBorderStyle(BorderStyle borderStyle)
    {
        _borderStyle = borderStyle;
        return this;
    }

    /// <summary>
    /// Creates a borderless window (renders borders as invisible spaces).
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder Borderless()
    {
        _borderStyle = BorderStyle.None;
        return this;
    }

    /// <summary>
    /// Sets the minimum size constraints for the window when resizing.
    /// </summary>
    /// <param name="minWidth">The minimum width in characters.</param>
    /// <param name="minHeight">The minimum height in characters.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithMinimumSize(int minWidth, int minHeight)
    {
        _minWidth = minWidth;
        _minHeight = minHeight;
        return this;
    }

    /// <summary>
    /// Sets the maximum size constraints for the window when resizing.
    /// </summary>
    /// <param name="maxWidth">The maximum width in characters.</param>
    /// <param name="maxHeight">The maximum height in characters.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithMaximumSize(int maxWidth, int maxHeight)
    {
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        return this;
    }

    /// <summary>
    /// Sets the parent window for establishing a parent-child relationship.
    /// Child windows are typically positioned relative to their parent.
    /// </summary>
    /// <param name="parent">The parent window instance.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithParent(Window parent)
    {
        _parentWindow = parent;
        return this;
    }

    /// <summary>
    /// Adds a control to the window's control collection.
    /// </summary>
    /// <param name="control">The control to add. Null values are ignored.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder AddControl(IWindowControl control)
    {
        if (control != null)
        {
            _controls.Add(control);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple controls to the window's control collection.
    /// </summary>
    /// <param name="controls">The controls to add. Null values in the array are ignored.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder AddControls(params IWindowControl[] controls)
    {
        foreach (var control in controls)
        {
            AddControl(control);
        }
        return this;
    }

    /// <summary>
    /// Adds a control to the window using a configuration action for inline setup.
    /// </summary>
    /// <typeparam name="T">The type of control to create. Must have a parameterless constructor.</typeparam>
    /// <param name="configure">An action to configure the newly created control.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder AddControl<T>(Action<T> configure) where T : class, IWindowControl, new()
    {
        var control = new T();
        configure(control);
        return AddControl(control);
    }

    /// <summary>
    /// Sets an asynchronous window thread method that runs in the background while the window is open.
    /// </summary>
    /// <param name="asyncThreadMethod">The async delegate to execute as the window's background thread.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithAsyncWindowThread(Window.WindowThreadDelegateAsync asyncThreadMethod)
    {
        _asyncWindowThread = asyncThreadMethod;
        return this;
    }

    /// <summary>
    /// Configures the window to always render on top of normal windows.
    /// AlwaysOnTop windows are rendered after all normal windows regardless of ZIndex.
    /// </summary>
    /// <param name="alwaysOnTop">True to make the window always on top; false otherwise. Default is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithAlwaysOnTop(bool alwaysOnTop = true)
    {
        _alwaysOnTop = alwaysOnTop;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's Activated event, which is raised when the window becomes the active window.
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window is activated.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnActivated(EventHandler handler)
    {
        _activatedHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's Deactivated event, which is raised when the window loses focus to another window.
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window is deactivated.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnDeactivated(EventHandler handler)
    {
        _deactivatedHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's KeyPressed event, which is raised when a key is pressed while the window has focus.
    /// </summary>
    /// <param name="handler">The event handler to invoke when a key is pressed.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnKeyPressed(EventHandler<KeyPressedEventArgs> handler)
    {
        _keyPressedHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's OnClosed event, which is raised after the window has been closed.
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window is closed.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnClosed(EventHandler handler)
    {
        _closedHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's OnCLosing event, which is raised when the window is about to close and can be cancelled.
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window is closing.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnClosing(EventHandler<ClosingEventArgs> handler)
    {
        _closingHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's OnResize event, which is raised when the window size changes.
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window is resized.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnResize(EventHandler handler)
    {
        _resizeHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's OnShown event, which is raised when the window is first displayed.
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window is shown.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnShown(EventHandler handler)
    {
        _shownHandler = handler;
        return this;
    }

    /// <summary>
    /// Subscribes a handler to the window's StateChanged event, which is raised when the window state changes (normal, minimized, maximized).
    /// </summary>
    /// <param name="handler">The event handler to invoke when the window state changes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder OnStateChanged(EventHandler<Window.WindowStateChangedEventArgs> handler)
    {
        _stateChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Enables DOM-based layout for this window.
    /// DOM layout is now always enabled and is the only rendering path.
    /// </summary>
    /// <param name="enabled">This parameter is ignored. DOM layout is always enabled.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    [Obsolete("DOM layout is now always enabled. This method will be removed.")]
    public WindowBuilder WithDOMLayout(bool enabled = true)
    {
        // DOM layout is always enabled - this method is kept for backward compatibility
        return this;
    }

    /// <summary>
    /// Builds and returns the window with all configured settings applied.
    /// The window is created but not added to the window system.
    /// </summary>
    /// <returns>A new <see cref="Window"/> instance with the configured settings.</returns>
    public Window Build()
    {
        Window window;

        // Create window based on configured thread method
        if (_asyncWindowThread != null)
        {
            window = new Window(_windowSystem, _asyncWindowThread, _parentWindow);
        }
        else
        {
            window = new Window(_windowSystem, _parentWindow);
        }

        // Apply configuration
        if (_title != null)
            window.Title = _title;

        if (_name != null)
            window.Name = _name;

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
        window.IsClosable = _isClosable;
        window.IsMovable = _isMovable;
        window.IsMinimizable = _isMinimizable;
        window.IsMaximizable = _isMaximizable;
        window.AlwaysOnTop = _alwaysOnTop;
        window.BorderStyle = _borderStyle;
        // DOM layout is now always enabled - no need to set

        // Note: MinimumWidth, MinimumHeight, MaximumWidth, MaximumHeight are private fields in Window
        // and cannot be set from the builder. These properties would need to be exposed publicly
        // in the Window class if needed.

        // Add controls
        foreach (var control in _controls)
        {
            window.AddControl(control);
        }

        // Subscribe event handlers
        if (_activatedHandler != null)
            window.Activated += _activatedHandler;

        if (_deactivatedHandler != null)
            window.Deactivated += _deactivatedHandler;

        if (_keyPressedHandler != null)
            window.KeyPressed += _keyPressedHandler;

        if (_closedHandler != null)
            window.OnClosed += _closedHandler;

        if (_closingHandler != null)
            window.OnCLosing += _closingHandler;

        if (_resizeHandler != null)
            window.OnResize += _resizeHandler;

        if (_shownHandler != null)
            window.OnShown += _shownHandler;

        if (_stateChangedHandler != null)
            window.StateChanged += _stateChangedHandler;

        return window;
    }

    /// <summary>
    /// Builds the window with all configured settings and immediately adds it to the window system.
    /// </summary>
    /// <param name="activate">True to activate (bring to front and focus) the window after creation; false to add it inactive. Defaults to true.</param>
    /// <returns>The created and displayed <see cref="Window"/> instance.</returns>
    public Window BuildAndShow(bool activate = true)
    {
        var window = Build();
        _windowSystem.AddWindow(window, activate);
        return window;
    }

    /// <summary>
    /// Applies a theme to set the window's background and foreground colors.
    /// </summary>
    /// <param name="theme">The theme to apply. If null, no changes are made.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithTheme(ITheme theme)
    {
        if (theme == null)
            return this;

        return WithColors(theme.WindowBackgroundColor, theme.WindowForegroundColor);
    }

    /// <summary>
    /// Applies a preconfigured window template to set multiple properties at once.
    /// Templates encapsulate common window configurations for reuse.
    /// </summary>
    /// <param name="template">The window template to apply.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WindowBuilder WithTemplate(WindowTemplate template)
    {
        return template.Configure(this);
    }
}

/// <summary>
/// Abstract base class for window templates that encapsulate reusable window configurations.
/// Inherit from this class to create custom templates for common window types.
/// </summary>
public abstract class WindowTemplate
{
    /// <summary>
    /// Configures the window builder with the settings defined by this template.
    /// </summary>
    /// <param name="builder">The window builder to configure.</param>
    /// <returns>The configured builder instance for continued method chaining.</returns>
    public abstract WindowBuilder Configure(WindowBuilder builder);
}

/// <summary>
/// A window template for creating modal dialog windows with standard dialog behavior.
/// Dialogs are centered, modal, and non-resizable by default.
/// </summary>
public sealed class DialogTemplate : WindowTemplate
{
    private readonly string _title;
    private readonly int _width;
    private readonly int _height;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogTemplate"/> class.
    /// </summary>
    /// <param name="title">The title text to display in the dialog's title bar.</param>
    /// <param name="width">The width of the dialog in characters. Defaults to 50.</param>
    /// <param name="height">The height of the dialog in characters. Defaults to 15.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
    public DialogTemplate(string title, int width = 50, int height = 15)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Configures the window builder with dialog-specific settings including
    /// modal mode, centered position, and disabled resizing.
    /// </summary>
    /// <param name="builder">The window builder to configure.</param>
    /// <returns>The configured builder instance for continued method chaining.</returns>
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
/// A window template for creating tool windows with standard tool panel behavior.
/// Tool windows are typically smaller, positioned to the side, and can be resized and moved.
/// </summary>
public sealed class ToolWindowTemplate : WindowTemplate
{
    private readonly string _title;
    private readonly Point _position;
    private readonly DrawingSize _size;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolWindowTemplate"/> class.
    /// </summary>
    /// <param name="title">The title text to display in the tool window's title bar.</param>
    /// <param name="position">The initial position of the tool window.</param>
    /// <param name="size">The initial size of the tool window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
    public ToolWindowTemplate(string title, Point position, DrawingSize size)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        _position = position;
        _size = size;
    }

    /// <summary>
    /// Configures the window builder with tool window-specific settings including
    /// the specified position and size, with resizing and moving enabled.
    /// </summary>
    /// <param name="builder">The window builder to configure.</param>
    /// <returns>The configured builder instance for continued method chaining.</returns>
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
