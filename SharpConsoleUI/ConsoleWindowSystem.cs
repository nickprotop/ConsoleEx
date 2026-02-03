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
using SharpConsoleUI.StartMenu;
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
	public class ConsoleWindowSystem : IWindowSystemContext
	{
		private readonly Renderer _renderer;
		private readonly object _renderLock = new();
		private readonly object _consoleLock = new(); // Shared lock for ALL Console I/O operations
		private readonly VisibleRegions _visibleRegions;

		private IConsoleDriver _consoleDriver;
		private int _exitCode;
		private int _idleTime = 10;
		private bool _running;

		// Frame rate limiting (configured via ConsoleWindowSystemOptions)
		private DateTime _lastRenderTime = DateTime.UtcNow;

		// Performance metrics tracking - delegated to PerformanceTracker
		private ConsoleWindowSystemOptions _options;
		private readonly PerformanceTracker _performanceTracker;

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

		// Plugin system
		private readonly PluginStateService _pluginStateService;

		// Input coordination
		private readonly InputCoordinator _inputCoordinator;

		// Render coordination
		private RenderCoordinator _renderCoordinator = null!; // Initialized in constructor after renderer

		// Start menu coordination
		private readonly StartMenuCoordinator _startMenuCoordinator;

		// Window lifecycle coordination
		private WindowLifecycleManager _windowLifecycleManager = null!; // Initialized in constructor after renderer

		// Window positioning coordination
		private WindowPositioningManager _windowPositioningManager = null!; // Initialized in constructor after renderer

	// Region invalidation helper

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
			_windowStateService = new WindowStateService(_logService);
			_focusStateService = new FocusStateService(_logService);
			_modalStateService = new ModalStateService(_logService);
			_themeStateService = new ThemeStateService(_theme);
			_themeStateService.ShowThemeSelectorCallback = ShowThemeSelectorDialog;
			_inputStateService = new InputStateService();

			// Initialize notification service (needs 'this' reference)
			_notificationStateService = new NotificationStateService(this, _logService);

			// Initialize plugin state service
			_pluginStateService = new PluginStateService(this, _logService, pluginConfiguration);

			// Initialize input coordinator (handles all mouse and keyboard input)
			_inputCoordinator = new InputCoordinator(
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

			// Initialize performance tracker (tracks frame timing and metrics)
			_performanceTracker = new PerformanceTracker();

			// Initialize start menu coordinator (manages start menu actions)
			_startMenuCoordinator = new StartMenuCoordinator(() => this);

			// Initialize render coordinator (needs renderer, performance tracker, and other services)
			_renderCoordinator = new RenderCoordinator(
				_consoleDriver,
				_renderer,
				_windowStateService,
				_logService,
				this,
				_options,
				_performanceTracker);

			// Initialize window lifecycle manager (manages window add, close, flash, etc.)
			_windowLifecycleManager = new WindowLifecycleManager(
				_logService,
				_windowStateService,
				_modalStateService,
				_focusStateService,
				_renderer,
				_consoleDriver,
				() => this);

			// Initialize window positioning manager (manages window move, resize, bounds)
			_windowPositioningManager = new WindowPositioningManager(
				_renderer,
				_renderCoordinator,
				() => this);

			// NOW initialize driver with 'this' reference (after services exist)
			_consoleDriver.Initialize(this);

			// Subscribe to theme changes for automatic window invalidation
			_themeStateService.ThemeChanged += OnThemeChanged;
			_themeStateService.ThemePropertyChanged += OnThemePropertyChanged;

			// Auto-load plugins if configured
			if (pluginConfiguration?.AutoLoad == true)
			{
				_pluginStateService.LoadPluginsFromDirectory(pluginConfiguration.GetEffectivePluginsDirectory());
			}
		}

		/// <summary>
		/// Handles theme change events and automatically invalidates all windows.
		/// </summary>
		private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
		{
			_logService?.Log(LogLevel.Information, "Theme",
				$"Theme changed from '{e.PreviousTheme?.Name}' to '{e.NewTheme.Name}'");
			InvalidateAllWindows();
		}

		/// <summary>
		/// Handles theme property change events and automatically invalidates all windows.
		/// </summary>
		private void OnThemePropertyChanged(object? sender, EventArgs e)
		{
			_logService?.Log(LogLevel.Debug, "Theme",
				"Theme property changed");
			InvalidateAllWindows();
		}

		/// <summary>
		/// Switches to a theme by name and automatically invalidates all windows.
		/// </summary>
		/// <param name="themeName">Name of the theme to switch to.</param>
		/// <returns>True if theme was found and applied, false otherwise.</returns>
		public bool SwitchTheme(string themeName)
		{
			var newTheme = ThemeRegistry.GetTheme(themeName);
			if (newTheme == null)
			{
				_logService?.Log(LogLevel.Warning, "Theme",
					$"Theme '{themeName}' not found in registry");
				return false;
			}

			Theme = newTheme;
			return true;
		}

		/// <summary>
		/// Invalidates all windows to force complete redraw.
		/// Called automatically after theme changes.
		/// </summary>
		private void InvalidateAllWindows()
		{
			// Get all windows and invalidate them
			var windows = _windowStateService.GetWindowsByZOrder();
			foreach (var window in windows)
			{
				window.Invalidate(true); // Deep invalidate (controls too)
			}
		}

		/// <summary>
		/// Shows the theme selector dialog for interactive theme selection.
		/// </summary>
		public void ShowThemeSelectorDialog() => Dialogs.ThemeSelectorDialog.Show(this);

	#region File/Folder Picker Dialogs

	/// <summary>
	/// Shows a folder picker dialog for selecting a directory.
	/// </summary>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="parentWindow">Optional parent window for the dialog</param>
	/// <returns>A task that completes with the selected folder path, or null if cancelled.</returns>
	public Task<string?> ShowFolderPickerDialogAsync(string? startPath = null, Window? parentWindow = null)
		=> Dialogs.FileDialogs.ShowFolderPickerAsync(this, startPath, parentWindow);

	/// <summary>
	/// Shows a file picker dialog for selecting a file.
	/// </summary>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="filter">Optional file filter (e.g., "*.txt", "*.cs;*.txt"). Null shows all files.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	/// <returns>A task that completes with the selected file path, or null if cancelled.</returns>
	public Task<string?> ShowFilePickerDialogAsync(string? startPath = null, string? filter = null, Window? parentWindow = null)
		=> Dialogs.FileDialogs.ShowFilePickerAsync(this, startPath, filter, parentWindow);

	/// <summary>
	/// Shows a save file dialog for specifying a file to save.
	/// </summary>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="filter">Optional file filter (e.g., "*.txt", "*.cs;*.txt"). Null shows all files.</param>
	/// <param name="defaultFileName">Default filename to pre-populate in the input field.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	/// <returns>A task that completes with the specified file path, or null if cancelled.</returns>
	public Task<string?> ShowSaveFileDialogAsync(string? startPath = null, string? filter = null, string? defaultFileName = null, Window? parentWindow = null)
		=> Dialogs.FileDialogs.ShowSaveFileAsync(this, startPath, filter, defaultFileName, parentWindow);

	#endregion


		/// <summary>
		/// Gets or sets the text displayed in the bottom status bar.
		/// </summary>
		public string BottomStatus { get; set; } = "";

		/// <summary>
		/// Gets or sets the console driver used for low-level console operations.
		/// </summary>
		public IConsoleDriver ConsoleDriver
		{ get { return _consoleDriver; } set { _consoleDriver = value; } }

		/// <summary>
		/// Gets the bottom-right coordinate of the usable desktop area (excluding status bars).
		/// </summary>
		public Point DesktopBottomRight => new Point(_consoleDriver.ScreenSize.Width - 1, _consoleDriver.ScreenSize.Height - 1 - GetTopStatusHeight() - GetBottomStatusHeight());

		/// <summary>
		/// Gets the dimensions of the usable desktop area (excluding status bars).
		/// </summary>
		public Helpers.Size DesktopDimensions => new Helpers.Size(_consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height - GetTopStatusHeight() - GetBottomStatusHeight());

		/// <summary>
		/// Gets the upper-left coordinate of the usable desktop area (excluding status bars).
		/// </summary>
		public Point DesktopUpperLeft => new Point(0, GetTopStatusHeight());

		/// <summary>
		/// Gets the height occupied by the top status bar (0 or 1).
		/// Accounts for both status text and performance metrics.
		/// </summary>
		private int GetTopStatusHeight()
		{
			return _renderCoordinator.GetTopStatusHeight();
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// Accounts for both status text and Start button.
		/// </summary>
		private int GetBottomStatusHeight()
		{
			return _renderCoordinator.GetBottomStatusHeight();
		}


		/// <summary>
		/// Gets the current console window system options.
		/// </summary>
		public ConsoleWindowSystemOptions Options => _options;

		/// <summary>
		/// Gets a value indicating whether the window system is currently running.
		/// </summary>
		public bool IsRunning => _running;

		private ITheme _theme = null!; // Initialized in constructor

		/// <summary>
		/// Gets or sets the theme used for styling windows and controls.
		/// </summary>
		public ITheme Theme
		{
			get => _theme;
			set
			{
				_theme = value ?? throw new ArgumentNullException(nameof(value));
				_themeStateService.SetTheme(_theme);
			}
		}

		/// <summary>
		/// Gets or sets the text displayed in the top status bar.
		/// </summary>
		public string TopStatus { get; set; } = "";

		/// <summary>
		/// Gets or sets whether the top status bar is shown.
		/// Changing this affects desktop dimensions and all window coordinates.
		/// </summary>
		public bool ShowTopStatus
		{
			get => _renderCoordinator.GetShowTopStatus();
			set
			{
				if (_renderCoordinator.GetShowTopStatus() != value)
				{
					_renderCoordinator.SetShowTopStatus(value);
					_renderCoordinator.InvalidateStatusCache();
					// Invalidate all windows to recalculate bounds
					foreach (var w in Windows.Values)
					{
						w.Invalidate(true);
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets whether the bottom status bar is shown.
		/// Changing this affects desktop dimensions and all window coordinates.
		/// </summary>
		public bool ShowBottomStatus
		{
			get => _renderCoordinator.GetShowBottomStatus();
			set
			{
				if (_renderCoordinator.GetShowBottomStatus() != value)
				{
					_renderCoordinator.SetShowBottomStatus(value);
					_renderCoordinator.InvalidateStatusCache();
					// Invalidate all windows to recalculate bounds
					foreach (var w in Windows.Values)
					{
						w.Invalidate(true);
					}
				}
			}
		}

		/// <summary>
		/// Gets the visible regions manager for calculating window visibility.
		/// </summary>
		public VisibleRegions VisibleRegions => _visibleRegions;

		/// <summary>
		/// Gets a read-only dictionary of all windows in the system, keyed by their GUID.
		/// </summary>
		public IReadOnlyDictionary<string, Window> Windows => _windowStateService.Windows;

		/// <summary>
		/// Gets the currently active window, or null if no window is active.
		/// </summary>
		public Window? ActiveWindow => _windowStateService.ActiveWindow;

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
		/// Gets the library-managed logging service.
		/// Subscribe to LogAdded event or call GetRecentLogs() to access internal logs.
		/// </summary>
		public ILogService LogService => _logService;

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

		// Convenience properties for drag/resize state (delegated to service)
		private bool IsDragging => _windowStateService.IsDragging;
		private bool IsResizing => _windowStateService.IsResizing;
		private Window? DragWindow => IsDragging ? _windowStateService.CurrentDrag?.Window :
									   (IsResizing ? _windowStateService.CurrentResize?.Window : null);

		/// <summary>
		/// Adds a window to the window system.
		/// </summary>
		/// <param name="window">The window to add.</param>
		/// <param name="activateWindow">Whether to activate the window after adding. Defaults to true.</param>
		/// <returns>The added window.</returns>
		public Window AddWindow(Window window, bool activateWindow = true)
		{
			return _windowLifecycleManager.AddWindow(window, activateWindow);
		}

		/// <summary>
		/// Closes a modal window and optionally activates its parent window.
		/// </summary>
		/// <param name="modalWindow">The modal window to close. If null or not a modal window, the method returns without action.</param>
		public void CloseModalWindow(Window? modalWindow)
		{
			_windowLifecycleManager.CloseModalWindow(modalWindow);
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
			return _windowLifecycleManager.CloseWindow(window, activateParent, force);
		}

		/// <summary>
		/// Explicit interface implementation for IWindowSystemContext.CloseWindow.
		/// Calls the full CloseWindow method with default parameters.
		/// </summary>
		bool IWindowSystemContext.CloseWindow(Window window)
		{
			return CloseWindow(window, activateParent: true, force: false);
		}

		/// <summary>
		/// Clears a rectangular area with the desktop background
		/// </summary>
		public void ClearArea(int left, int top, int width, int height)
		{
			_renderer.FillRect(left, top, width, height,
				Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			// Invalidate any windows that overlap with this area to redraw them
			foreach (var window in Windows.Values)
			{
				var windowRect = new Rectangle(window.Left, window.Top, window.Width, window.Height);
				var clearRect = new Rectangle(left, top, width, height);
				if (GeometryHelpers.DoesRectangleIntersect(windowRect, clearRect))
				{
					window.Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Flashes a window to draw user attention by briefly changing its background color.
		/// </summary>
		/// <param name="window">The window to flash. If null, the method returns without action.</param>
		/// <param name="flashCount">The number of times to flash. Defaults to 1.</param>
		/// <param name="flashDuration">The duration of each flash in milliseconds. Defaults to 150.</param>
		/// <param name="flashBackgroundColor">The background color to use for flashing. If null, uses the theme's ModalFlashColor.</param>
		public void FlashWindow(Window? window, int flashCount = 1, int flashDuration = 150, Color? flashBackgroundColor = null)
		{
			_windowLifecycleManager.FlashWindow(window, flashCount, flashDuration, flashBackgroundColor);
		}

		/// <summary>
		/// Gets a window by its GUID.
		/// </summary>
		/// <param name="guid">The GUID of the window to find.</param>
		/// <returns>The window with the specified GUID, or null if not found.</returns>
		public Window? GetWindow(string guid)
		{
			return _windowStateService.GetWindow(guid);
		}

		/// <summary>
		/// Sets the target frames per second for rendering.
		/// </summary>
		/// <param name="fps">Target FPS (must be greater than 0). Common values: 15, 30, 60, 120, 144.</param>
		public void SetTargetFPS(int fps)
		{
			if (fps <= 0)
				throw new ArgumentException("FPS must be greater than 0", nameof(fps));

			_options = _options with { TargetFPS = fps };
			_logService?.Log(LogLevel.Information, "System", $"Target FPS changed to {fps}");
		}

		/// <summary>
		/// Gets the current target frames per second.
		/// </summary>
		public int GetTargetFPS() => _options.TargetFPS;

		/// <summary>
		/// Enables or disables frame rate limiting.
		/// </summary>
		/// <param name="enabled">True to enable frame rate limiting (cap at TargetFPS), false to render as fast as possible.</param>
		public void SetFrameRateLimiting(bool enabled)
		{
			_options = _options with { EnableFrameRateLimiting = enabled };
			_logService?.Log(LogLevel.Information, "System", $"Frame rate limiting {(enabled ? "enabled" : "disabled")}");
		}

		/// <summary>
		/// Gets whether frame rate limiting is currently enabled.
		/// </summary>
		public bool IsFrameRateLimitingEnabled() => _options.EnableFrameRateLimiting;

		/// <summary>
		/// Enables or disables performance metrics display in the top status bar.
		/// </summary>
		/// <param name="enabled">True to show performance metrics, false to hide them.</param>
		public void SetPerformanceMetrics(bool enabled)
		{
			_options = _options with { EnablePerformanceMetrics = enabled };
			_renderCoordinator.InvalidateStatusCache();
			_logService?.Log(LogLevel.Information, "System", $"Performance metrics {(enabled ? "enabled" : "disabled")}");
		}

		/// <summary>
		/// Gets whether performance metrics are currently enabled.
		/// </summary>
		public bool IsPerformanceMetricsEnabled() => _options.EnablePerformanceMetrics;

		/// <summary>
		/// Gets the current actual frame time in milliseconds.
		/// </summary>
		public double GetCurrentFrameTime() => _performanceTracker.CurrentFrameTimeMs;

		/// <summary>
		/// Gets the current actual FPS based on frame time.
		/// </summary>
		public double GetCurrentFPS() => _performanceTracker.CurrentFPS;

		/// <summary>
		/// Gets the number of dirty characters (changed cells) in the last frame.
		/// </summary>
		public int GetDirtyCharCount() => _performanceTracker.CurrentDirtyChars;

		/// <summary>
		/// Starts the main event loop of the window system. Blocks until <see cref="Shutdown"/> is called.
		/// </summary>
		/// <returns>The exit code set by <see cref="Shutdown"/> or 1 if an unhandled exception occurred.</returns>
	/// <summary>
	/// Handles screen resize events by adjusting window positions and sizes.
	/// </summary>
	private void HandleScreenResize(object? sender, Size size)
	{
		lock (_renderLock)
		{
			Helpers.Size desktopSize = DesktopDimensions;

			_consoleDriver.Clear();

			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

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

			_renderCoordinator.InvalidateStatusCache();
		}
	}
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
			// Handler registered later via InputCoordinator.RegisterEventHandlers()

		_screenResizedHandler = HandleScreenResize;
			_consoleDriver.ScreenResized += _screenResizedHandler;

			// Register input coordinator event handlers
			_inputCoordinator.RegisterEventHandlers(_keyPressedHandler);

			// Start the console driver
			_consoleDriver.Start();

			// Initialize the console window system with background color and character
			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			Exception? fatalException = null;

			try
			{
				// Main loop
				while (_running)
				{
					_inputCoordinator.ProcessInput();

					var now = DateTime.UtcNow;
					var elapsed = (now - _lastRenderTime).TotalMilliseconds;

					// Track performance metrics on EVERY iteration (independent of rendering)
					bool metricsNeedUpdate = false;
					if (_options.EnablePerformanceMetrics)
					{
						metricsNeedUpdate = _performanceTracker.BeginFrame();
						if (metricsNeedUpdate)
						{
							_renderCoordinator.InvalidateStatusCache();
						}

						_performanceTracker.UpdateMetrics(
							Windows.Count,
							Windows.Values.Count(w => w.IsDirty)
						);
					}

					// Frame pacing: render if windows are dirty OR metrics need update
					bool shouldRender = AnyWindowDirty() || metricsNeedUpdate;

					if (_options.EnableFrameRateLimiting)
					{
						// Frame rate limiting enabled: only render if enough time elapsed
						if (shouldRender && elapsed >= _options.MinFrameTime)
						{
							UpdateDisplay();
							_lastRenderTime = now;
							_idleTime = _options.MinFrameTime;
						}
						else
						{
							_inputStateService.UpdateIdleState();
							_idleTime = _inputStateService.GetRecommendedSleepDuration(10, 100);
						}
					}
					else
					{
						// Frame rate limiting disabled: render immediately when dirty
						if (shouldRender)
						{
							UpdateDisplay();
							_lastRenderTime = now;
							_idleTime = 10; // Fast loop when dirty, no frame rate cap
						}
						else
						{
							_inputStateService.UpdateIdleState();
							_idleTime = _inputStateService.GetRecommendedSleepDuration(10, 100);
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
				_inputCoordinator.UnregisterEventHandlers(_keyPressedHandler);
				_keyPressedHandler = null;
			}

			if (_screenResizedHandler != null)
			{
				_consoleDriver.ScreenResized -= _screenResizedHandler;
				_screenResizedHandler = null;
			}
		}

		#region Start Menu API

		/// <summary>
		/// Registers a user-defined action in the Start menu.
		/// </summary>
		/// <param name="name">Display name for the action (supports Spectre markup).</param>
		/// <param name="callback">Action to execute when selected.</param>
		/// <param name="category">Optional category (defaults to "User Actions").</param>
		/// <param name="order">Sort order within category (lower = earlier).</param>
		public void RegisterStartMenuAction(string name, Action callback, string? category = null, int order = 0)
		{
			_startMenuCoordinator.RegisterAction(name, callback, category, order);
		}

		/// <summary>
		/// Unregisters a Start menu action by name.
		/// </summary>
		/// <param name="name">Name of the action to remove.</param>
		public void UnregisterStartMenuAction(string name)
		{
			_startMenuCoordinator.UnregisterAction(name);
		}

		/// <summary>
		/// Gets all registered Start menu actions.
		/// </summary>
		public IReadOnlyList<StartMenuAction> GetStartMenuActions() => _startMenuCoordinator.GetActions();

		/// <summary>
		/// Shows the Start menu dialog.
		/// </summary>
		public void ShowStartMenu()
		{
			_startMenuCoordinator.ShowMenu();
		}

		/// <summary>
		/// Handles start menu keyboard shortcut (Alt+key or configured shortcut).
		/// </summary>
		public bool HandleStartMenuShortcut(ConsoleKeyInfo key)
		{
			var options = _options.StatusBar;

			// Only handle shortcut if Start button is enabled
			if (!options.ShowStartButton)
				return false;

			if (key.Key == options.StartMenuShortcutKey &&
				key.Modifiers == options.StartMenuShortcutModifiers)
			{
				ShowStartMenu();
				return true;
			}
			return false;
		}

		private void UpdateStatusBarBounds()
		{
			_renderCoordinator.UpdateStatusBarBounds();
		}

		/// <summary>
		/// Handles status bar mouse click (e.g., start button).
		/// </summary>
		public bool HandleStatusBarMouseClick(int x, int y)
		{
			// Check if click is on Start button
			if (_renderCoordinator.StartButtonBounds.Contains(x, y))
			{
				ShowStartMenu();
				return true;
			}

			return false;
		}

		#endregion

		/// <summary>
		/// Processes one iteration of the main loop (input, display, cursor).
		/// This is useful for modal dialogs that need to block while still processing UI events.
		/// </summary>
		public void ProcessOnce()
		{
			_inputCoordinator.ProcessInput();
			UpdateDisplay();
			UpdateCursor();
		}

		/// <summary>
		/// Sets the specified window as the active window, handling modal window logic and focus.
		/// </summary>
		/// <param name="window">The window to activate. If null, the method returns without action.</param>
		public void SetActiveWindow(Window window)
		{
			if (window == null)
			{
				return;
			}

			// Check if activation is blocked by modal service
			if (_modalStateService.IsActivationBlocked(window))
			{
				_logService.LogTrace($"Window activation blocked by modal: {window.Title}", "Modal");
				var blockingModal = _modalStateService.GetBlockingModal(window);
				if (blockingModal != null && blockingModal != ActiveWindow)
				{
					FlashWindow(blockingModal);
				}
				else if (ActiveWindow != null)
				{
					FlashWindow(ActiveWindow);
				}
				return;
			}

			// Get the effective activation target (handles modal children)
			Window windowToActivate = _modalStateService.GetEffectiveActivationTarget(window) ?? window;

			// If a different modal should be activated, flash it
			if (windowToActivate != window && windowToActivate.Mode == WindowMode.Modal)
			{
				FlashWindow(windowToActivate);
			}

			var previousActiveWindow = ActiveWindow;

			// Invalidate previous active window
			previousActiveWindow?.Invalidate(true);

			// Delegate activation to the service
			// The service handles: SetIsActive, ZIndex update, and state tracking
			_windowStateService.ActivateWindow(windowToActivate);

			// Update focus state via FocusStateService
			_focusStateService.SetWindowFocus(windowToActivate);

			// Invalidate new active window
			windowToActivate.Invalidate(true);

			// Unfocus the currently focused control of other windows
			foreach (var w in Windows.Values)
			{
				if (w != ActiveWindow)
				{
					w.UnfocusCurrentControl();
					_focusStateService.ClearFocus(w);
				}
			}

			_logService.LogTrace($"Window activated: {windowToActivate.Title}", "Window");
		}

		/// <summary>
		/// Activates an existing window by name, or creates it using the factory if not found.
		/// </summary>
		/// <param name="name">The window name to find/create</param>
		/// <param name="factory">Factory function to create the window if it doesn't exist</param>
		/// <returns>The activated or newly created window</returns>
		public Window ActivateOrCreate(string name, Func<Window> factory)
		{
			if (string.IsNullOrEmpty(name))
				throw new ArgumentException("Window name cannot be null or empty", nameof(name));
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));

			var existing = _windowStateService.FindWindowByName(name);
			if (existing != null)
			{
				SetActiveWindow(existing);
				return existing;
			}

			var window = factory();
			window.Name = name;
			AddWindow(window);
			return window;
		}

		/// <summary>
		/// Activates an existing window by GUID, or creates it using the factory if not found.
		/// </summary>
		/// <param name="guid">The window GUID to find/create</param>
		/// <param name="factory">Factory function to create the window if it doesn't exist</param>
		/// <returns>The activated or newly created window</returns>
		public Window ActivateOrCreateByGuid(string guid, Func<Window> factory)
		{
			if (string.IsNullOrEmpty(guid))
				throw new ArgumentException("Window GUID cannot be null or empty", nameof(guid));
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));

			var existing = _windowStateService.GetWindow(guid);
			if (existing != null)
			{
				SetActiveWindow(existing);
				return existing;
			}

			var window = factory();
			AddWindow(window);
			return window;
		}

		/// <summary>
		/// Activates an existing window by name if found, otherwise returns null.
		/// </summary>
		/// <param name="name">The window name to find</param>
		/// <returns>The activated window, or null if not found</returns>
		public Window? TryActivate(string name)
		{
			var existing = _windowStateService.FindWindowByName(name);
			if (existing != null)
			{
				SetActiveWindow(existing);
			}
			return existing;
		}

		/// <summary>
		/// Activates an existing window by GUID if found, otherwise returns null.
		/// </summary>
		/// <param name="guid">The window GUID to find</param>
		/// <returns>The activated window, or null if not found</returns>
		public Window? TryActivateByGuid(string guid)
		{
			var existing = _windowStateService.GetWindow(guid);
			if (existing != null)
			{
				SetActiveWindow(existing);
			}
			return existing;
		}

		/// <summary>
		/// Translates a window-relative point to absolute screen coordinates.
		/// </summary>
		/// <param name="window">The window containing the point.</param>
		/// <param name="point">The point in window-relative coordinates.</param>
		/// <returns>A tuple containing the absolute screen coordinates (absoluteLeft, absoluteTop).</returns>
		public (int absoluteLeft, int absoluteTop) TranslateToAbsolute(Window window, Point point)
		{
	{
		return GeometryHelpers.TranslateToAbsolute(window, point, DesktopUpperLeft.Y);
	}
		}

		/// <summary>
		/// Translates an absolute screen point to window-relative coordinates.
		/// </summary>
		/// <param name="window">The window to translate coordinates relative to.</param>
		/// <param name="point">The point in absolute screen coordinates, or null.</param>
		/// <returns>The point in window-relative coordinates. Returns (0,0) if point is null.</returns>
		public Point TranslateToRelative(Window window, Point? point)
		{
	{
		return GeometryHelpers.TranslateToRelative(window, point, DesktopUpperLeft.Y);
	}
		}


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
		/// Cycles to the next active window (Ctrl+T handler).
		/// </summary>
		public void CycleActiveWindow()
		{
			// Delegate to service - it handles window cycling and restoring minimized windows
			_windowStateService.ActivateNextWindow();
		}

	/// <summary>
	/// Finds the topmost window at the specified point.
	/// </summary>
	/// <param name="point">The point in absolute screen coordinates.</param>
	/// <returns>The topmost window at the point, or null if none found.</returns>
	public Window? GetWindowAtPoint(Point point)
	{
		return WindowQueryHelper.GetWindowAtPoint(point, this);
	}

	/// <summary>
	/// Handles Alt+Number keyboard shortcuts for quick window switching.
	/// </summary>
		public bool HandleAltInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			if (key.KeyChar >= (char)ConsoleKey.D1 && key.KeyChar <= (char)ConsoleKey.D9)
			{
				// Get only top-level windows to match what's displayed in the bottom status bar
				var topLevelWindows = Windows.Values
					.Where(w => w.ParentWindow == null)
					.ToList();

				int index = key.KeyChar - (char)ConsoleKey.D1;
				if (index < topLevelWindows.Count)
				{
					var newActiveWindow = topLevelWindows[index];

					SetActiveWindow(newActiveWindow);
					if (newActiveWindow.State == WindowState.Minimized)
						newActiveWindow.State = WindowState.Normal;
					handled = true;
				}
			}

			return handled;
		}

		/// <summary>
		/// Moves a window to a new position with proper invalidation (used by both mouse and keyboard moves)
		/// </summary>
		public void MoveWindowTo(Window window, int newLeft, int newTop)
		{
			_windowPositioningManager.MoveWindowTo(window, newLeft, newTop);
		}
		/// <summary>
		/// Computes a hash representing the current state of windows for task bar caching.
		/// Includes window titles, states, and count to detect changes.
		/// </summary>
		private void MoveOrResizeOperation(Window? window, WindowTopologyAction windowTopologyAction, Direction direction)
		{
			_windowPositioningManager.MoveOrResizeOperation(window, windowTopologyAction, direction);

		}


		private void UpdateCursor()
		{
			if (ActiveWindow != null && ActiveWindow.HasInteractiveContent(out var cursorPosition))
			{
				var (absoluteLeft, absoluteTop) = TranslateToAbsolute(ActiveWindow, new Point(cursorPosition.X, cursorPosition.Y));

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

					if (ActiveWindow.HasActiveInteractiveContent(out var interactiveContent) &&
						interactiveContent is IWindowControl windowControl)
					{
						ownerControl = windowControl;

						// Check if control provides a preferred cursor shape
						if (windowControl is ICursorShapeProvider shapeProvider &&
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

		private void UpdateDisplay()
		{
			_renderCoordinator.UpdateDisplay();
		}


	#region IWindowSystemContext Implementation - Additional Methods

	/// <summary>
	/// Moves the specified window by a relative delta.
	/// </summary>
	public void MoveWindowBy(Window window, int deltaX, int deltaY)
	{
		_windowPositioningManager.MoveWindowBy(window, deltaX, deltaY);
	}

	/// <summary>
	/// Resizes the specified window to a new size and position.
	/// </summary>
	public void ResizeWindowTo(Window window, int newLeft, int newTop, int newWidth, int newHeight)
	{
		_windowPositioningManager.ResizeWindowTo(window, newLeft, newTop, newWidth, newHeight);
	}

	/// <summary>
	/// Resizes the specified window by a relative delta.
	/// </summary>
	public void ResizeWindowBy(Window window, int deltaWidth, int deltaHeight)
	{
		_windowPositioningManager.ResizeWindowBy(window, deltaWidth, deltaHeight);
	}

	/// <summary>
	/// Requests the window system to exit with the specified exit code.
	/// </summary>
	public void RequestExit(int exitCode)
	{
		_exitCode = exitCode;
		_running = false;
	}

	/// <summary>
	/// Activates the next non-minimized window after the specified window is minimized.
	/// </summary>
	public void ActivateNextNonMinimizedWindow(Window minimizedWindow)
	{
		_windowLifecycleManager.ActivateNextNonMinimizedWindow(minimizedWindow);
	}

	/// <summary>
	/// Deactivates the current active window (e.g., when clicking on empty desktop).
	/// </summary>
	public void DeactivateCurrentWindow()
	{
		_windowLifecycleManager.DeactivateCurrentWindow();
	}

	#endregion

	}
}
