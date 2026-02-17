// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using System.Collections.Concurrent;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Core;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Input;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Plugins;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Windows;
using SharpConsoleUI.Models;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Performance;
using static SharpConsoleUI.Window;
using SharpConsoleUI.Drivers;
using System.Drawing;
using Color = Spectre.Console.Color;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI
{

	/// <summary>
	/// The main window system that manages console windows, input processing, and rendering.
	/// Provides window management, focus handling, theming, and event processing for console applications.
	/// </summary>
	public class ConsoleWindowSystem
	{
		#region Fields

		private readonly Renderer _renderer;
		private readonly object _renderLock = new();
		private readonly object _consoleLock = new(); // Shared lock for ALL Console I/O operations
		private readonly VisibleRegions _visibleRegions;

		private IConsoleDriver _consoleDriver;
		private volatile int _exitCode;
		private volatile int _idleTime = Configuration.SystemDefaults.DefaultIdleTimeMs;
		private volatile bool _running;

		// Frame rate limiting (configured via ConsoleWindowSystemOptions)
		private DateTime _lastRenderTime = DateTime.UtcNow;

		// Performance metrics tracking - delegated to PerformanceTracker
		private ConsoleWindowSystemOptions _options;

		/// <summary>
		/// Gets the performance tracker for monitoring frame rates and render times.
		/// </summary>
		public PerformanceTracker Performance;

		// Event handlers stored for proper cleanup
		private EventHandler<ConsoleKeyInfo>? _keyPressedHandler;
		private EventHandler<Size>? _screenResizedHandler;

		// Logging service - created first so other services can use it
		private readonly LogService _logService = new();

		// State services
		private readonly CursorStateService _cursorStateService;
		private readonly WindowStateService _windowStateService;
		private readonly FocusStateService _focusStateService;
		private readonly ModalStateService _modalStateService;
		private readonly ThemeStateService _themeStateService;
		private readonly InputStateService _inputStateService;
		private readonly NotificationStateService _notificationStateService;
		private readonly StatusBarStateService _statusBarStateService;

		// Plugin system
		private readonly PluginStateService _pluginStateService;

		// Diagnostics system (optional, for testing and debugging)
		private Diagnostics.RenderingDiagnostics? _renderingDiagnostics;

		// Input coordination
		/// <summary>
		/// Gets the input coordinator for managing keyboard and mouse input across windows.
		/// </summary>
		public InputCoordinator Input;

		// Render coordination
		/// <summary>
		/// Gets the render coordinator for managing window rendering and invalidation.
		/// </summary>
		public RenderCoordinator Render { get; private set; } = null!; // Initialized in constructor after renderer

		// Window lifecycle coordination

		// Window positioning coordination
		/// <summary>
		/// Gets the window positioning manager for handling window movement and resizing.
		/// </summary>
		public WindowPositioningManager Positioning { get; private set; } = null!; // Initialized in constructor after renderer

		// Region invalidation helper

		private ITheme _theme = null!; // Initialized in constructor - kept for backward compatibility in internal code

		// Convenience properties for drag/resize state (delegated to service)
		private bool IsDragging => _windowStateService.IsDragging;
		private bool IsResizing => _windowStateService.IsResizing;
		private Window? DragWindow => IsDragging ? _windowStateService.CurrentDrag?.Window :
									   (IsResizing ? _windowStateService.CurrentResize?.Window : null);

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWindowSystem"/> class with the default theme.
		/// </summary>
		/// <param name="driver">Pre-configured console driver.</param>
		/// <param name="pluginConfiguration">Optional plugin configuration for auto-loading plugins.</param>
		/// <param name="options">Optional configuration options for system behavior.</param>
		public ConsoleWindowSystem(IConsoleDriver driver, PluginConfiguration? pluginConfiguration = null, ConsoleWindowSystemOptions? options = null)
			: this(driver, ThemeRegistry.GetDefaultTheme(), pluginConfiguration, options)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWindowSystem"/> class with a theme specified by name.
		/// </summary>
		/// <param name="driver">Pre-configured console driver.</param>
		/// <param name="themeName">The name of the theme to use.</param>
		/// <param name="pluginConfiguration">Optional plugin configuration for auto-loading plugins.</param>
		/// <param name="options">Optional configuration options for system behavior.</param>
		public ConsoleWindowSystem(IConsoleDriver driver, string themeName, PluginConfiguration? pluginConfiguration = null, ConsoleWindowSystemOptions? options = null)
			: this(driver, ThemeRegistry.GetThemeOrDefault(themeName, new ModernGrayTheme()), pluginConfiguration, options)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWindowSystem"/> class with a specific theme instance.
		/// </summary>
		/// <param name="driver">Pre-configured console driver.</param>
		/// <param name="theme">The theme instance to use.</param>
		/// <param name="pluginConfiguration">Optional plugin configuration for auto-loading plugins.</param>
		/// <param name="options">Optional configuration options for system behavior.</param>
		public ConsoleWindowSystem(IConsoleDriver driver, ITheme theme, PluginConfiguration? pluginConfiguration = null, ConsoleWindowSystemOptions? options = null)
		{
			_consoleDriver = driver ?? throw new ArgumentNullException(nameof(driver));
			_theme = theme ?? new ModernGrayTheme();

			// Initialize options with environment variable fallback
			_options = options ?? ConsoleWindowSystemOptions.Create();

			// Initialize state services BEFORE driver.Initialize() call
			_cursorStateService = new CursorStateService(_consoleDriver);
			_focusStateService = new FocusStateService(_logService);
			_modalStateService = new ModalStateService(_logService);
			_themeStateService = new ThemeStateService(_theme, _logService);
			_inputStateService = new InputStateService();
			_statusBarStateService = new StatusBarStateService(_logService, () => this);

			// Initialize notification service (needs 'this' reference)
			_notificationStateService = new NotificationStateService(this, _logService);

			// Initialize plugin state service
			_pluginStateService = new PluginStateService(this, _logService, pluginConfiguration);


			// Initialize WindowStateService (merged from WindowLifecycleManager)
			// Note: Renderer is set later via property after it is created
			_windowStateService = new WindowStateService(
				_logService,
				() => this,
				_modalStateService,
				_focusStateService,
				null, // Renderer not yet created
				_consoleDriver);
			// Initialize input coordinator (handles all mouse and keyboard input)
			Input = new InputCoordinator(
				_consoleDriver,
				_inputStateService,
				_windowStateService,
				_logService,
				this);

			// Provide log service to InvalidationManager for error logging
			InvalidationManager.Instance.LogService = _logService;

			// Initialize the visible regions
			_visibleRegions = new VisibleRegions(this);

			// Initialize the renderer
			_renderer = new Renderer(this);

			// Set renderer on WindowStateService for screen redraws during window close
			_windowStateService.SetRenderer(_renderer);


			// Initialize performance tracker (tracks frame timing and metrics)
			Performance = new PerformanceTracker(
				() => _options,
				(opts) => _options = opts,
				_logService,
				() => Render?.InvalidateStatusCache());

			// Initialize render coordinator (needs renderer, performance tracker, and other services)
			Render = new RenderCoordinator(
				_consoleDriver,
				_renderer,
				_windowStateService,
				_statusBarStateService,
				_logService,
				this,
				_options,
				Performance);

			// Initialize window lifecycle manager (manages window add, close, flash, etc.)

			// Initialize window positioning manager (manages window move, resize, bounds)
			Positioning = new WindowPositioningManager(
				_renderer,
				Render,
				() => this);

			// Initialize diagnostics BEFORE driver.Initialize() so driver can connect them
			if (_options.EnableDiagnostics)
			{
				_renderingDiagnostics = new Diagnostics.RenderingDiagnostics(_options);
			}

			// NOW initialize driver with 'this' reference (after services and diagnostics exist)
			_consoleDriver.Initialize(this);

			// Set window system context on ThemeStateService for window invalidation
			_themeStateService.SetWindowSystemContext(() => this);

			// Auto-load plugins if configured
			if (pluginConfiguration?.AutoLoad == true)
			{
				_pluginStateService.LoadPluginsFromDirectory(pluginConfiguration.GetEffectivePluginsDirectory());
			}
		}

		#endregion

		#region Properties

		#region Core Properties

		/// <summary>
		/// Gets a value indicating whether the window system is currently running.
		/// </summary>
		public bool IsRunning => _running;

		/// <summary>
		/// Gets or sets the current console window system options.
		/// </summary>
		public ConsoleWindowSystemOptions Options
		{
			get => _options;
			set => _options = value;
		}

		/// <summary>
		/// Gets or sets the console driver used for low-level console operations.
		/// </summary>
		public IConsoleDriver ConsoleDriver
		{ get { return _consoleDriver; } set { _consoleDriver = value; } }

		/// <summary>
		/// Gets the shared console lock used to synchronize ALL Console I/O operations.
		/// This lock MUST be used by all code that accesses System.Console to prevent
		/// thread-safety violations that corrupt Console internal state.
		/// </summary>
		/// <remarks>
		/// InputLoop thread and rendering thread must coordinate through this lock to prevent
		/// Console.KeyAvailable from returning incorrect values during concurrent Console.Write operations.
		/// </remarks>
		internal object ConsoleLock => _consoleLock;

		/// <summary>
		/// Gets the renderer for drawing operations.
		/// </summary>
		public Renderer Renderer => _renderer;

		#endregion

		#region State Services

		/// <summary>
		/// Gets the cursor state service for managing cursor visibility and position.
		/// </summary>
		public CursorStateService CursorStateService => _cursorStateService;

		/// <summary>
		/// Gets the window state service for managing window lifecycle and state.
		/// </summary>
		public WindowStateService WindowStateService => _windowStateService;

		/// <summary>
		/// Gets the focus state service for managing control focus within windows.
		/// </summary>
		public FocusStateService FocusStateService => _focusStateService;

		/// <summary>
		/// Gets the modal state service for managing modal window behavior.
		/// </summary>
		public ModalStateService ModalStateService => _modalStateService;

		/// <summary>
		/// Gets the theme state service for managing theme application.
		/// </summary>
		public ThemeStateService ThemeStateService => _themeStateService;

		/// <summary>
		/// Gets the input state service for managing input queue and idle state.
		/// </summary>
		public InputStateService InputStateService => _inputStateService;

		/// <summary>
		/// Gets the notification state service for managing notifications and toasts.
		/// </summary>
		public NotificationStateService NotificationStateService => _notificationStateService;

		/// <summary>
		/// Gets the plugin state service for managing plugins and their contributions.
		/// </summary>
		public PluginStateService PluginStateService => _pluginStateService;

		/// <summary>
		/// Gets the status bar state service for managing status bars and Start menu.
		/// </summary>
		public StatusBarStateService StatusBarStateService => _statusBarStateService;

		/// <summary>
		/// Gets the library-managed logging service.
		/// Subscribe to LogAdded event or call GetRecentLogs() to access internal logs.
		/// </summary>
		public ILogService LogService => _logService;

		/// <summary>
		/// Gets the rendering diagnostics system for testing and debugging.
		/// Only available when EnableDiagnostics is true in options.
		/// </summary>
		public Diagnostics.RenderingDiagnostics? RenderingDiagnostics => _renderingDiagnostics;

		#endregion

		#region Desktop Properties

		/// <summary>
		/// Gets the upper-left coordinate of the usable desktop area (excluding status bars).
		/// </summary>
		public Point DesktopUpperLeft => new Point(0, _statusBarStateService.GetTopStatusHeight(_statusBarStateService.ShowTopStatus, _options.EnablePerformanceMetrics));

		/// <summary>
		/// Gets the bottom-right coordinate of the usable desktop area (excluding status bars).
		/// </summary>
		public Point DesktopBottomRight => new Point(_consoleDriver.ScreenSize.Width - 1, _consoleDriver.ScreenSize.Height - 1 - _statusBarStateService.GetTopStatusHeight(_statusBarStateService.ShowTopStatus, _options.EnablePerformanceMetrics) - _statusBarStateService.GetBottomStatusHeight(_statusBarStateService.ShowBottomStatus, _options.StatusBar.ShowTaskBar, _options.StatusBar.ShowStartButton, _options.StatusBar.StartButtonLocation));

		/// <summary>
		/// Gets the dimensions of the usable desktop area (excluding status bars).
		/// </summary>
		public Helpers.Size DesktopDimensions => new Helpers.Size(_consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height - _statusBarStateService.GetTopStatusHeight(_statusBarStateService.ShowTopStatus, _options.EnablePerformanceMetrics) - _statusBarStateService.GetBottomStatusHeight(_statusBarStateService.ShowBottomStatus, _options.StatusBar.ShowTaskBar, _options.StatusBar.ShowStartButton, _options.StatusBar.StartButtonLocation));

		/// <summary>
		/// Gets the visible regions manager for calculating window visibility.
		/// </summary>
		public VisibleRegions VisibleRegions => _visibleRegions;

		#endregion


		#region Window Properties

		/// <summary>
		/// Gets a read-only dictionary of all windows in the system, keyed by their GUID.
		/// </summary>
		public IReadOnlyDictionary<string, Window> Windows => _windowStateService.Windows;

		/// <summary>
		/// Gets the currently active window, or null if no window is active.
		/// </summary>
		public Window? ActiveWindow => _windowStateService.ActiveWindow;

		#endregion

		#region Theme Property

		/// <summary>
		/// Gets the theme used for styling windows and controls.
		/// Use ThemeStateService.CurrentTheme or ThemeStateService.SetTheme() for theme management.
		/// </summary>
		public ITheme Theme => _themeStateService.CurrentTheme;

		#endregion

		#endregion

		#region Public Methods

		#region Window Management

		/// <summary>
		/// Adds a window to the window system.
		/// </summary>
		/// <param name="window">The window to add.</param>
		/// <param name="activateWindow">Whether to activate the window after adding. Defaults to true.</param>
		/// <returns>The added window.</returns>
		public Window AddWindow(Window window, bool activateWindow = true)
		{
			return _windowStateService.AddWindow(window, activateWindow);
		}

		/// <summary>
		/// Closes a window and removes it from the window system.
		/// </summary>
		/// <param name="window">The window to close. If null or not in the system, returns false.</param>
		/// <param name="activateParent">Whether to activate the parent window after closing. Defaults to true.</param>
		/// <param name="force">If true, forces the window to close even if IsClosable is false or OnClosing cancels.</param>
		/// <returns>True if the window was closed successfully; false otherwise.</returns>
		public bool CloseWindow(Window? window, bool activateParent = true, bool force = false)
		{
			return _windowStateService.CloseWindow(window, activateParent, force);
		}

		/// <summary>
		/// Closes a modal window and optionally activates its parent window.
		/// </summary>
		/// <param name="modalWindow">The modal window to close. If null or not a modal window, the method returns without action.</param>
		public void CloseModalWindow(Window? modalWindow)
		{
			_windowStateService.CloseModalWindow(modalWindow);
		}


		#endregion

		#region Window Activation

		/// <summary>
		/// Sets the specified window as the active window, handling modal window logic and focus.
		/// </summary>
		/// <param name="window">The window to activate. If null, the method returns without action.</param>
		public void SetActiveWindow(Window window)
		{
			_windowStateService.SetActiveWindow(window);
		}

		/// <summary>
		/// Cycles to the next active window (Ctrl+T handler).
		/// </summary>
		public void CycleActiveWindow()
		{
			// Delegate to service - it handles window cycling and restoring minimized windows
			_windowStateService.ActivateNextWindow();
		}


		#endregion

		#region Window Operations


		/// <summary>
		/// Finds the topmost window at the specified point.
		/// </summary>
		/// <param name="point">The point in absolute screen coordinates.</param>
		/// <returns>The topmost window at the point, or null if none found.</returns>
		public Window? GetWindowAtPoint(Point point)
		{
			return WindowQueryHelper.GetWindowAtPoint(point, this);
		}

		#endregion

		#region System Control

		/// <summary>
		/// Starts the main event loop of the window system. Blocks until <see cref="Shutdown"/> is called.
		/// </summary>
		/// <returns>The exit code set by <see cref="Shutdown"/> or 1 if an unhandled exception occurred.</returns>
		public int Run()
		{
			_logService.LogDebug("Console window system starting");
			_running = true;

			// Subscribe to the console driver events
			// Store handlers in fields so they can be properly unsubscribed in Shutdown()
			_keyPressedHandler = (sender, key) =>
			{
				_inputStateService.EnqueueKey(key);
			};

			_screenResizedHandler = HandleScreenResize;
			_consoleDriver.ScreenResized += _screenResizedHandler;

			// Register input coordinator event handlers
			Input.RegisterEventHandlers(_keyPressedHandler);

			// Start the console driver
			_consoleDriver.Start();

			// Initialize the console window system with background color and character
			_renderer.FillDesktopBackground(Theme, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height);

			Exception? fatalException = null;

			try
			{
				// Main loop
				while (_running)
				{
					Input.ProcessInput();

					var now = DateTime.UtcNow;
					var elapsed = (now - _lastRenderTime).TotalMilliseconds;

					// Track performance metrics on EVERY iteration (independent of rendering)
					bool metricsNeedUpdate = false;
					if (Performance.IsPerformanceMetricsEnabled)
					{
						metricsNeedUpdate = Performance.BeginFrame();
						if (metricsNeedUpdate)
						{
							Render.InvalidateStatusCache();
						}

						Performance.UpdateMetrics(
							Windows.Count,
							Windows.Values.Count(w => w.IsDirty)
						);
					}

					// Frame pacing: render if windows are dirty OR metrics need update OR desktop needs render
					bool shouldRender = AnyWindowDirty() || metricsNeedUpdate || Render.DesktopNeedsRender;

					// Calculate recommended sleep duration once (used in both branches)
					var recommendedSleep = _inputStateService.GetRecommendedSleepDuration(
						Configuration.SystemDefaults.MinSleepDurationMs,
						Configuration.SystemDefaults.MaxSleepDurationMs);

					if (Performance.IsFrameRateLimitingEnabled)
					{
						// Frame rate limiting enabled: only render if enough time elapsed
						if (shouldRender && elapsed >= Performance.MinFrameTime)
						{
							Render.UpdateDisplay();
							_lastRenderTime = now;
							_idleTime = (int)Performance.MinFrameTime;
						}
						else
						{
							_inputStateService.UpdateIdleState();
							_idleTime = recommendedSleep;
						}
					}
					else
					{
						// Frame rate limiting disabled: render immediately when dirty
						if (shouldRender)
						{
							Render.UpdateDisplay();
							_lastRenderTime = now;
							_idleTime = Configuration.SystemDefaults.FastLoopIdleMs; // Fast loop when dirty, no frame rate cap
						}
						else
						{
							_inputStateService.UpdateIdleState();
							_idleTime = recommendedSleep;
						}
					}

					UpdateCursor();
					Thread.Sleep(_idleTime);
				}
			}
			catch (Exception ex)
			{
				// Log the exception to file (if file logging is enabled) before cleanup
				LogService?.LogCritical($"Unhandled exception in main loop: {ex.Message}", ex, "System");
				fatalException = ex;
				_exitCode = 1;  // Error exit code
			}
			finally
			{
				// ALWAYS restore console state (mouse mode, cursor, etc.)
				// This ensures the terminal is usable even if the app crashes
				try
				{
					_consoleDriver.Stop();
				}
				catch
				{
					// Ignore cleanup errors - we're already exiting
				}
			}

			// After console is restored, show error message to user
			if (fatalException != null)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Fatal error: {fatalException.Message}");
				Console.ResetColor();
				Console.WriteLine(fatalException.StackTrace);
			}

			return _exitCode;
		}

		/// <summary>
		/// Gracefully shuts down the console window system with the specified exit code
		/// </summary>
		/// <param name="exitCode">The exit code to return</param>
		public void Shutdown(int exitCode = 0)
		{
			_logService.LogDebug($"Console window system shutting down (exit code: {exitCode})");
			_exitCode = exitCode;
			_running = false;

			// Unsubscribe from console driver events to prevent memory leaks
			if (_keyPressedHandler != null)
			{
				Input.UnregisterEventHandlers(_keyPressedHandler);
				_keyPressedHandler = null;
			}

			if (_screenResizedHandler != null)
			{
				_consoleDriver.ScreenResized -= _screenResizedHandler;
				_screenResizedHandler = null;
			}
		}

		/// <summary>
		/// Processes one iteration of the main loop (input, display, cursor).
		/// This is useful for modal dialogs that need to block while still processing UI events.
		/// </summary>
		public void ProcessOnce()
		{
			Input.ProcessInput();
			Render.UpdateDisplay();
			UpdateCursor();
		}

		/// <summary>
		/// Requests the window system to exit with the specified exit code.
		/// </summary>
		public void RequestExit(int exitCode)
		{
			_exitCode = exitCode;
			_running = false;
		}


		#endregion

		#endregion

		#region Private Methods

		#region Event Handlers

		/// <summary>
		/// Handles screen resize events by adjusting window positions and sizes.
		/// </summary>
		private void HandleScreenResize(object? sender, Size size)
		{
			lock (_renderLock)
			{
				Helpers.Size desktopSize = DesktopDimensions;

				_consoleDriver.Clear();

				_renderer.FillDesktopBackground(Theme, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height);

				foreach (var window in Windows.Values)
				{
					if (window.State == WindowState.Maximized)
					{
						window.SetSize(desktopSize.Width, desktopSize.Height);
						window.SetPosition(new Point(0, 0));
					}
					else
					{
						if (window.Left + window.Width > desktopSize.Width)
						{
							window.Left = Math.Max(0, desktopSize.Width - window.Width);
						}
						if (window.Top + window.Height > desktopSize.Height)
						{
							window.Top = Math.Max(1, desktopSize.Height - window.Height);
						}
					}

					window.Invalidate(true);
				}

				Render.InvalidateStatusCache();
			}
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Checks if any window has the dirty flag set.
		/// </summary>
		private bool AnyWindowDirty()
		{
			foreach (var window in Windows.Values)
			{
				if (window.IsDirty) return true;
			}
			return false;
		}

		/// <summary>
		/// Computes a hash representing the current state of windows for task bar caching.
		/// Includes window titles, states, and count to detect changes.
		/// </summary>
		private void MoveOrResizeOperation(Window? window, WindowTopologyAction windowTopologyAction, Direction direction)
		{
			Positioning.MoveOrResizeOperation(window, windowTopologyAction, direction);

		}

		private void UpdateCursor()
		{
			if (ActiveWindow != null && ActiveWindow.EventDispatcher != null && ActiveWindow.EventDispatcher.HasInteractiveContent(out var cursorPosition))
			{
				var (absoluteLeft, absoluteTop) = GeometryHelpers.TranslateToAbsolute(ActiveWindow, new Point(cursorPosition.X, cursorPosition.Y), DesktopUpperLeft.Y);

				// Check if the cursor position is within the console window boundaries
				// and within the active window's boundaries
				bool isWithinBounds =
					absoluteLeft >= 0 && absoluteLeft < _consoleDriver.ScreenSize.Width &&
					absoluteTop >= 0 && absoluteTop < _consoleDriver.ScreenSize.Height &&
					absoluteLeft >= ActiveWindow.Left && absoluteLeft < ActiveWindow.Left + ActiveWindow.Width &&
					absoluteTop >= ActiveWindow.Top && absoluteTop < ActiveWindow.Top + ActiveWindow.Height;


				if (isWithinBounds)
				{
					// Get the owner control if available
					IWindowControl? ownerControl = null;
					CursorShape cursorShape = CursorShape.Block;

					if (ActiveWindow.EventDispatcher != null && ActiveWindow.EventDispatcher.HasActiveInteractiveContent(out var interactiveContent) &&
						interactiveContent is IWindowControl windowControl)
					{
						ownerControl = windowControl;

							// Find the deepest focused control for cursor shape (e.g., PromptControl inside ScrollablePanel)
						var deepestControl = ActiveWindow.FindDeepestFocusedControl(interactiveContent);

						// Check deepest control first, then fall back to top-level control
						if (deepestControl is ICursorShapeProvider deepShapeProvider &&
							deepShapeProvider.PreferredCursorShape.HasValue)
						{
							cursorShape = deepShapeProvider.PreferredCursorShape.Value;
						}
						else if (windowControl is ICursorShapeProvider shapeProvider &&
							shapeProvider.PreferredCursorShape.HasValue)
						{
							cursorShape = shapeProvider.PreferredCursorShape.Value;
						}
					}

					// Update cursor state service with new state
					_cursorStateService.UpdateFromWindowSystem(
						ownerWindow: ActiveWindow,
						logicalPosition: cursorPosition,
						absolutePosition: new Point(absoluteLeft, absoluteTop),
						ownerControl: ownerControl,
						shape: cursorShape);
				}
				else
				{
					_cursorStateService.HideCursor();
				}
			}
			else
			{
				_cursorStateService.HideCursor();
			}

			// Apply cursor state to the actual console
			// CRITICAL: Protect Console I/O with lock to prevent concurrent writes
			// from corrupting ANSI sequences during InputLoop's mouse parsing
			lock (_consoleLock)
			{
				_cursorStateService.ApplyCursorToConsole(
					_consoleDriver.ScreenSize.Width,
					_consoleDriver.ScreenSize.Height);
			}
		}

		private void UpdateStatusBarBounds()
		{
			Render.UpdateStatusBarBounds();
		}

		#endregion

		#endregion

	}
}
