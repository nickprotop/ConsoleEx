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
using SharpConsoleUI.Logging;
using SharpConsoleUI.Plugins;
using static SharpConsoleUI.Window;
using SharpConsoleUI.Drivers;
using System.Drawing;
using Color = Spectre.Console.Color;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI
{
	/// <summary>
	/// Specifies the direction of movement or navigation.
	/// </summary>
	public enum Direction
	{
		/// <summary>Upward direction.</summary>
		Up,
		/// <summary>Downward direction.</summary>
		Down,
		/// <summary>Leftward direction.</summary>
		Left,
		/// <summary>Rightward direction.</summary>
		Right
	}

	/// <summary>
	/// Specifies the type of window topology operation.
	/// </summary>
	public enum WindowTopologyAction
	{
		/// <summary>Resize the window.</summary>
		Resize,
		/// <summary>Move the window.</summary>
		Move
	}

	/// <summary>
	/// Specifies the direction from which a window is being resized.
	/// </summary>
	public enum ResizeDirection
	{
		/// <summary>No resize operation.</summary>
		None,
		/// <summary>Resize from the top edge.</summary>
		Top,
		/// <summary>Resize from the bottom edge.</summary>
		Bottom,
		/// <summary>Resize from the left edge.</summary>
		Left,
		/// <summary>Resize from the right edge.</summary>
		Right,
		/// <summary>Resize from the top-left corner.</summary>
		TopLeft,
		/// <summary>Resize from the top-right corner.</summary>
		TopRight,
		/// <summary>Resize from the bottom-left corner.</summary>
		BottomLeft,
		/// <summary>Resize from the bottom-right corner.</summary>
		BottomRight
	}

	/// <summary>
	/// The main window system that manages console windows, input processing, and rendering.
	/// Provides window management, focus handling, theming, and event processing for console applications.
	/// </summary>
	public class ConsoleWindowSystem
	{
		private readonly Renderer _renderer;
		private readonly object _renderLock = new();
		private readonly VisibleRegions _visibleRegions;
		private string? _cachedBottomStatus;
		private string? _cachedTopStatus;
		private bool _showTopStatus = true;
		private bool _showBottomStatus = true;
		private IConsoleDriver _consoleDriver;
		private int _exitCode;
		private int _idleTime = 10;
		private bool _running;
		private bool _showTaskBar = true;

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

		// Track windows currently being flashed to prevent concurrent flashes
		private readonly HashSet<Window> _flashingWindows = new();

		// Plugin system
		private readonly List<IPlugin> _plugins = new();
		private readonly Dictionary<string, Func<IWindowControl>> _pluginControlFactories = new();
		private readonly Dictionary<string, Func<ConsoleWindowSystem, Window>> _pluginWindowFactories = new();
		private readonly Dictionary<Type, object> _pluginServices = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWindowSystem"/> class with the default theme.
		/// </summary>
		/// <param name="driver">Pre-configured console driver.</param>
		public ConsoleWindowSystem(IConsoleDriver driver)
			: this(driver, ThemeRegistry.GetDefaultTheme())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWindowSystem"/> class with a theme specified by name.
		/// </summary>
		/// <param name="driver">Pre-configured console driver.</param>
		/// <param name="themeName">The name of the theme to use.</param>
		public ConsoleWindowSystem(IConsoleDriver driver, string themeName)
			: this(driver, ThemeRegistry.GetThemeOrDefault(themeName, new ModernGrayTheme()))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWindowSystem"/> class with a specific theme instance.
		/// </summary>
		/// <param name="driver">Pre-configured console driver.</param>
		/// <param name="theme">The theme instance to use.</param>
		public ConsoleWindowSystem(IConsoleDriver driver, ITheme theme)
		{
			_consoleDriver = driver ?? throw new ArgumentNullException(nameof(driver));
			_theme = theme ?? new ModernGrayTheme();

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

			// Provide log service to InvalidationManager for error logging
			InvalidationManager.Instance.LogService = _logService;

			// Initialize the visible regions
			_visibleRegions = new VisibleRegions(this);

			// Initialize the renderer
			_renderer = new Renderer(this);

			// NOW initialize driver with 'this' reference (after services exist)
			_consoleDriver.Initialize(this);

			// Subscribe to theme changes for automatic window invalidation
			_themeStateService.ThemeChanged += OnThemeChanged;
			_themeStateService.ThemePropertyChanged += OnThemePropertyChanged;
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
		/// </summary>
		private int GetTopStatusHeight()
		{
			return _showTopStatus && !string.IsNullOrEmpty(TopStatus) ? 1 : 0;
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// </summary>
		private int GetBottomStatusHeight()
		{
			return _showBottomStatus && !string.IsNullOrEmpty(BottomStatus) ? 1 : 0;
		}

		/// <summary>
		/// Returns true if the top status bar should be rendered.
		/// </summary>
		private bool ShouldRenderTopStatus()
		{
			return _showTopStatus && !string.IsNullOrEmpty(TopStatus);
		}

		/// <summary>
		/// Returns true if the bottom status bar should be rendered.
		/// </summary>
		private bool ShouldRenderBottomStatus()
		{
			return _showBottomStatus && !string.IsNullOrEmpty(BottomStatus);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the task bar is visible at the bottom of the screen.
		/// </summary>
		public bool ShowTaskBar { get => _showTaskBar; set => _showTaskBar = value; }

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
			get => _showTopStatus;
			set
			{
				if (_showTopStatus != value)
				{
					_showTopStatus = value;
					_cachedTopStatus = null; // Force re-render
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
			get => _showBottomStatus;
			set
			{
				if (_showBottomStatus != value)
				{
					_showBottomStatus = value;
					_cachedBottomStatus = null; // Force re-render
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
		/// Gets the library-managed logging service.
		/// Subscribe to LogAdded event or call GetRecentLogs() to access internal logs.
		/// </summary>
		public ILogService LogService => _logService;

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
			_logService.LogDebug($"Adding window: {window.Title} (GUID: {window.Guid})", "Window");

			// Delegate to window state service for window registration
			// The service handles ZIndex assignment and adding to collection
			_windowStateService.RegisterWindow(window, activate: false);

			// Register modal windows with the modal state service
			if (window.Mode == WindowMode.Modal)
			{
				_modalStateService.PushModal(window, window.ParentWindow);
				_logService.LogDebug($"Modal window pushed: {window.Title}", "Modal");
			}

			// Activate the window if needed (through SetActiveWindow for modal logic)
			if (ActiveWindow == null || activateWindow) SetActiveWindow(window);

			window.WindowIsAdded();

			_logService.LogDebug($"Window added successfully: {window.Title}", "Window");
			return window;
		}

		/// <summary>
		/// Closes a modal window and optionally activates its parent window.
		/// </summary>
		/// <param name="modalWindow">The modal window to close. If null or not a modal window, the method returns without action.</param>
		public void CloseModalWindow(Window? modalWindow)
		{
			if (modalWindow == null || modalWindow.Mode != WindowMode.Modal)
				return;

			_logService.LogDebug($"Closing modal window: {modalWindow.Title}", "Modal");

			// Store the parent window before closing
			Window? parentWindow = modalWindow.ParentWindow;

			// Close the modal window
			if (CloseWindow(modalWindow))
			{
				// If we have a parent window, ensure it becomes active
				if (parentWindow != null && Windows.ContainsKey(parentWindow.Guid))
				{
					SetActiveWindow(parentWindow);
				}
			}
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
			if (window == null) return false;
			if (!Windows.ContainsKey(window.Guid)) return false;

			_logService.LogDebug($"Closing window: {window.Title} (GUID: {window.Guid}, Force: {force})", "Window");

			// STEP 1: Check if close is allowed BEFORE any state changes
			// This fires OnClosing and respects IsClosable (unless forced)
			if (!window.TryClose(force))
			{
				_logService.LogDebug($"Window close cancelled by OnClosing handler: {window.Title}", "Window");
				return false;
			}

			// STEP 2: Close is allowed - now safe to remove from system
			Window? parentWindow = window.ParentWindow;
			bool wasActive = (window == ActiveWindow);

			// Unregister modal window from modal state service
			if (window.Mode == WindowMode.Modal)
			{
				_modalStateService.PopModal(window);
			}

			// Clear focus state for this window
			_focusStateService.ClearFocus(window);

			// Remove from window collection via service
			// This prevents race condition where render thread tries to render disposed controls
			_windowStateService.UnregisterWindow(window);

			// Activate the next window (UnregisterWindow updates state but doesn't call SetIsActive)
			if (wasActive)
			{
				if (activateParent && parentWindow != null && Windows.ContainsKey(parentWindow.Guid))
				{
					// Activate the parent window
					SetActiveWindow(parentWindow);
				}
				else if (Windows.Count > 0)
				{
					// Activate window with highest Z-Index
					int maxZIndex = Windows.Values.Max(w => w.ZIndex);
					var nextWindow = Windows.Values.FirstOrDefault(w => w.ZIndex == maxZIndex);
					if (nextWindow != null)
					{
						SetActiveWindow(nextWindow);
					}
				}
			}

			// STEP 3: Complete the close (fire OnClosed, dispose controls)
			window.CompleteClose();

			// Redraw the screen
			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height,
							  Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
			foreach (var w in Windows.Values)
			{
				w.Invalidate(true);
			}

			return true;
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
				if (DoesRectangleIntersect(windowRect, clearRect))
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
			if (window == null) return;

			// Prevent multiple concurrent flashes on the same window
			lock (_flashingWindows)
			{
				if (_flashingWindows.Contains(window)) return;
				_flashingWindows.Add(window);
			}

			var originalBackgroundColor = window.BackgroundColor;
			var flashColor = flashBackgroundColor ?? Theme.ModalFlashColor;

			// Use ThreadPool with synchronous sleep for reliable timing
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					for (int i = 0; i < flashCount; i++)
					{
						window.BackgroundColor = flashColor;
						window.Invalidate(true);
						Thread.Sleep(flashDuration);

						window.BackgroundColor = originalBackgroundColor;
						window.Invalidate(true);

						// Only delay between flashes, not after the last one
						if (i < flashCount - 1)
						{
							Thread.Sleep(flashDuration);
						}
					}
				}
				finally
				{
					// Always remove from tracking to allow future flashes
					lock (_flashingWindows)
					{
						_flashingWindows.Remove(window);
					}
				}
			});
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
		/// Starts the main event loop of the window system. Blocks until <see cref="Shutdown"/> is called.
		/// </summary>
		/// <returns>The exit code set by <see cref="Shutdown"/> or 1 if an unhandled exception occurred.</returns>
		public int Run()
		{
			_logService.LogDebug("Console window system starting", "System");
			_running = true;

			// Subscribe to the console driver events
			// Store handlers in fields so they can be properly unsubscribed in Shutdown()
			_keyPressedHandler = (sender, key) =>
			{
				_inputStateService.EnqueueKey(key);
			};
			_consoleDriver.KeyPressed += _keyPressedHandler;

			_screenResizedHandler = (sender, size) =>
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

					_cachedBottomStatus = null;
					_cachedTopStatus = null;
				}
			};
			_consoleDriver.ScreenResized += _screenResizedHandler;

			_consoleDriver.MouseEvent += HandleMouseEvent;

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
					ProcessInput();
					UpdateDisplay();
					UpdateCursor();

					// Update idle state and get recommended sleep duration
					_inputStateService.UpdateIdleState();
					_idleTime = _inputStateService.GetRecommendedSleepDuration(10, 100);

					// Reduce idle time if windows need redrawing
					if (AnyWindowDirty())
					{
						_idleTime = 10;
					}

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
			_logService.LogDebug($"Console window system shutting down (exit code: {exitCode})", "System");
			_exitCode = exitCode;
			_running = false;

			// Unsubscribe from console driver events to prevent memory leaks
			if (_keyPressedHandler != null)
			{
				_consoleDriver.KeyPressed -= _keyPressedHandler;
				_keyPressedHandler = null;
			}

			if (_screenResizedHandler != null)
			{
				_consoleDriver.ScreenResized -= _screenResizedHandler;
				_screenResizedHandler = null;
			}

			_consoleDriver.MouseEvent -= HandleMouseEvent;
		}

		/// <summary>
		/// Processes one iteration of the main loop (input, display, cursor).
		/// This is useful for modal dialogs that need to block while still processing UI events.
		/// </summary>
		public void ProcessOnce()
		{
			ProcessInput();
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
			int absoluteLeft = window.Left + point.X;
			int absoluteTop = window.Top + DesktopUpperLeft.Y + point.Y;
			return (absoluteLeft, absoluteTop);
		}

		/// <summary>
		/// Translates an absolute screen point to window-relative coordinates.
		/// </summary>
		/// <param name="window">The window to translate coordinates relative to.</param>
		/// <param name="point">The point in absolute screen coordinates, or null.</param>
		/// <returns>The point in window-relative coordinates. Returns (0,0) if point is null.</returns>
		public Point TranslateToRelative(Window window, Point? point)
		{
			if (point == null) return new Point(0, 0);

			int relativeLeft = (point?.X ?? 0) - window.Left;
			int relativeTop = (point?.Y ?? 0) - window.Top - DesktopUpperLeft.Y;
			return new Point(relativeLeft, relativeTop);
		}

		/// <summary>
		/// Invalidates only the regions that are newly exposed after a window move/resize operation
		/// This happens AFTER the window is already in its new position
		/// </summary>
		private void InvalidateExposedRegions(Window movedWindow, Rectangle oldBounds)
		{
			// Calculate what regions were covered by the old window but are not covered by the new window
			var newBounds = new Rectangle(movedWindow.Left, movedWindow.Top, movedWindow.Width, movedWindow.Height);
			var exposedRegions = CalculateExposedRegions(oldBounds, newBounds);

			if (exposedRegions.Count == 0)
				return; // No exposed regions, nothing to do

			// Merge small adjacent regions to reduce flicker and improve performance
			var optimizedRegions = OptimizeExposedRegions(exposedRegions);

			// For each exposed region, redraw what should be there based on Z-order
			foreach (var exposedRegion in optimizedRegions)
			{
				RedrawExposedRegion(exposedRegion, movedWindow.ZIndex);
			}
		}

		/// <summary>
		/// Optimize exposed regions by merging small adjacent rectangles
		/// </summary>
		private List<Rectangle> OptimizeExposedRegions(List<Rectangle> regions)
		{
			// For now, just return the original regions
			// Could be enhanced later to merge adjacent rectangles
			return regions.Where(r => r.Width > 0 && r.Height > 0).ToList();
		}

		/// <summary>
		/// Calculate regions that were covered by oldBounds but are not covered by newBounds
		/// </summary>
		private List<Rectangle> CalculateExposedRegions(Rectangle oldBounds, Rectangle newBounds)
		{
			// If the old and new bounds are the same, no exposed regions
			if (oldBounds.Equals(newBounds))
				return new List<Rectangle>();
			
			// If there's no overlap between old and new, the entire old bounds is exposed
			if (!DoesRectangleIntersect(oldBounds, newBounds))
				return new List<Rectangle> { oldBounds };
			
			// Start with the old bounds
			var regions = new List<Rectangle> { oldBounds };
			
			// Subtract the new bounds from the old bounds to get exposed areas
			return SubtractRectangleFromRegions(regions, newBounds);
		}

		/// <summary>
		/// Redraw a specific exposed region by finding what should be visible there
		/// </summary>
		private void RedrawExposedRegion(Rectangle exposedRegion, int movedWindowZIndex)
		{
			// Find all windows that could be visible in this region (with lower Z-index than the moved window)
			var candidateWindows = Windows.Values
				.Where(w => w.ZIndex < movedWindowZIndex) // Only windows that were underneath
				.Where(w => DoesRectangleOverlapWindow(exposedRegion, w))
				.OrderBy(w => w.ZIndex) // Process in Z-order (bottom to top)
				.ToList();

			// Start by clearing the region with desktop background
			_renderer.FillRect(exposedRegion.X, exposedRegion.Y, exposedRegion.Width, exposedRegion.Height,
				Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			// Redraw each candidate window in the exposed region (in Z-order)
			foreach (var candidateWindow in candidateWindows)
			{
				var intersection = GetRectangleIntersection(exposedRegion,
					new Rectangle(candidateWindow.Left, candidateWindow.Top, candidateWindow.Width, candidateWindow.Height));
				
				if (!intersection.IsEmpty)
				{
					// Calculate what part of this window should be visible in the intersection
					// (considering other windows that might be on top of it)
					var windowsAbove = candidateWindows
						.Where(w => w.ZIndex > candidateWindow.ZIndex && w.ZIndex < movedWindowZIndex)
						.ToList();

					// Calculate which parts of the intersection are not covered by windows above
					var uncoveredRegions = CalculateUncoveredRegions(intersection, windowsAbove);

					// Render only the uncovered parts of this window
					foreach (var uncoveredRegion in uncoveredRegions)
					{
						_renderer.RenderRegion(candidateWindow, uncoveredRegion);
					}
				}
			}
			
		}

		/// <summary>
		/// Helper method to check if a rectangle overlaps with a window
		/// </summary>
		private bool DoesRectangleOverlapWindow(Rectangle rect, Window window)
		{
			return rect.X < window.Left + window.Width &&
				   rect.X + rect.Width > window.Left &&
				   rect.Y < window.Top + window.Height &&
				   rect.Y + rect.Height > window.Top;
		}

		/// <summary>
		/// Subtract a rectangle from a list of regions, similar to VisibleRegions.SubtractRectangle but simpler
		/// </summary>
		private List<Rectangle> SubtractRectangleFromRegions(List<Rectangle> regions, Rectangle subtract)
		{
			var result = new List<Rectangle>();

			foreach (var region in regions)
			{
				// If regions don't intersect, keep the original region
				if (!DoesRectangleIntersect(region, subtract))
				{
					result.Add(region);
					continue;
				}

				// Calculate the intersection
				var intersection = GetRectangleIntersection(region, subtract);

				// If region is completely covered, skip it
				if (intersection.Width == region.Width && intersection.Height == region.Height)
				{
					continue;
				}

				// Split the region into up to 4 sub-regions around the intersection

				// Region above the intersection
				if (intersection.Y > region.Y)
				{
					result.Add(new Rectangle(region.X, region.Y, region.Width, intersection.Y - region.Y));
				}

				// Region below the intersection
				if (intersection.Y + intersection.Height < region.Y + region.Height)
				{
					result.Add(new Rectangle(region.X, intersection.Y + intersection.Height, region.Width,
						region.Y + region.Height - (intersection.Y + intersection.Height)));
				}

				// Region to the left of the intersection
				if (intersection.X > region.X)
				{
					result.Add(new Rectangle(region.X, intersection.Y, intersection.X - region.X, intersection.Height));
				}

				// Region to the right of the intersection
				if (intersection.X + intersection.Width < region.X + region.Width)
				{
					result.Add(new Rectangle(intersection.X + intersection.Width, intersection.Y,
						region.X + region.Width - (intersection.X + intersection.Width), intersection.Height));
				}
			}

			return result;
		}

		/// <summary>
		/// Helper method to check if two rectangles intersect
		/// </summary>
		private bool DoesRectangleIntersect(Rectangle rect1, Rectangle rect2)
		{
			return rect1.X < rect2.X + rect2.Width &&
				   rect1.X + rect1.Width > rect2.X &&
				   rect1.Y < rect2.Y + rect2.Height &&
				   rect1.Y + rect1.Height > rect2.Y;
		}

		/// <summary>
		/// Calculate which parts of a region are not covered by a list of windows
		/// </summary>
		private List<Rectangle> CalculateUncoveredRegions(Rectangle region, List<Window> coveringWindows)
		{
			// Start with the entire region
			var regions = new List<Rectangle> { region };

			// Subtract each covering window's area
			foreach (var window in coveringWindows)
			{
				var windowRect = new Rectangle(window.Left, window.Top, window.Width, window.Height);
				regions = SubtractRectangleFromRegions(regions, windowRect);
			}

			return regions;
		}

		/// <summary>
		/// Handles window clicks - activates inactive windows or propagates to active windows
		/// </summary>
		private void HandleWindowClick(Window window, List<MouseFlags> flags, Point point)
		{
			if (window != ActiveWindow)
			{
				// Window is not active - activate it and stop propagation
				SetActiveWindow(window);
			}
			else
			{
				// Window is already active - propagate the click event
				PropagateMouseEventToWindow(window, flags, point);
			}
		}

		/// <summary>
		/// Propagates mouse events to the specified window
		/// </summary>
		private void PropagateMouseEventToWindow(Window window, List<MouseFlags> flags, Point point)
		{
			// Calculate window-relative coordinates
			var windowPosition = TranslateToRelative(window, point);
			
			// Create mouse event arguments
			var mouseArgs = new Events.MouseEventArgs(
				flags,
				windowPosition, // This will be recalculated for control-relative coordinates in the window
				point, // Absolute desktop coordinates
				windowPosition, // Window-relative coordinates
				window
			);

			// Propagate to the window
			window.ProcessWindowMouseEvent(mouseArgs);
		}

		/// <summary>
		/// Helper method to calculate intersection of two rectangles
		/// </summary>
		private Rectangle GetRectangleIntersection(Rectangle rect1, Rectangle rect2)
		{
			int left = Math.Max(rect1.X, rect2.X);
			int top = Math.Max(rect1.Y, rect2.Y);
			int right = Math.Min(rect1.X + rect1.Width, rect2.X + rect2.Width);
			int bottom = Math.Min(rect1.Y + rect1.Height, rect2.Y + rect2.Height);

			if (left < right && top < bottom)
				return new Rectangle(left, top, right - left, bottom - top);
			else
				return Rectangle.Empty;
		}


		private bool AnyWindowDirty()
		{
			return Windows.Values.Any(window => window.IsDirty);
		}

		private void CycleActiveWindow()
		{
			// Delegate to service - it handles window cycling and restoring minimized windows
			_windowStateService.ActivateNextWindow();
		}

		// Helper method to find the deepest modal child window with the highest Z-index
		private Window? FindDeepestModalChild(Window window)
		{
			// Get all direct modal children of the window, ordered by Z-index (highest first)
			var modalChildren = Windows.Values
				.Where(w => w.ParentWindow == window && w.Mode == WindowMode.Modal)
				.OrderByDescending(w => w.ZIndex)
				.ToList();

			// If no direct modal children, return null
			if (modalChildren.Count == 0)
			{
				return null;
			}

			// Take the highest Z-index modal child
			Window highestModalChild = modalChildren.First();

			// Check if this modal child itself has modal children
			Window? deeperModalChild = FindDeepestModalChild(highestModalChild);

			// If deeper modal child found, return it, otherwise return the highest modal child
			return deeperModalChild ?? highestModalChild;
		}

		// Helper method to find the appropriate window to activate based on modality rules
		private Window FindWindowToActivate(Window targetWindow)
		{
			// First, check if there's already an active modal child - prioritize it
			var activeModalChild = Windows.Values
				.Where(w => w.ParentWindow == targetWindow && w.Mode == WindowMode.Modal && w.GetIsActive())
				.FirstOrDefault();

			if (activeModalChild != null)
			{
				// Found an already active modal child, prioritize it
				FlashWindow(activeModalChild);
				return FindWindowToActivate(activeModalChild); // Recursively check if this active modal has active modal children
			}

			// No already active modal child, check for any modal children
			var modalChild = FindDeepestModalChild(targetWindow);
			if (modalChild != null)
			{
				// Found a modal child, activate it instead
				FlashWindow(modalChild);
				return modalChild;
			}

			// No modal children, return the target window itself
			return targetWindow;
		}

		private Window? GetWindowAtPoint(Point point)
		{
			List<Window> windows = Windows.Values
				.Where(window =>
					point.X >= window.Left &&
					point.X < window.Left + window.Width &&
					point.Y - DesktopUpperLeft.Y >= window.Top &&
					point.Y - DesktopUpperLeft.Y < window.Top + window.Height)
				.OrderBy(window => window.ZIndex).ToList();

			// Iterate from topmost (highest ZIndex) to bottom
			// Return the first window that doesn't have a child at this point
			for (int i = windows.Count - 1; i >= 0; i--)
			{
				var window = windows[i];

				// Check if any higher-ZIndex window in the list is a child of this window
				bool hasChildAtPoint = false;
				for (int j = i + 1; j < windows.Count; j++)
				{
					var higherWindow = windows[j];

					// Check if this higher window is a modal child of current window
					if (higherWindow.Mode == WindowMode.Modal && higherWindow.ParentWindow == window)
					{
						hasChildAtPoint = true;
						break;
					}
				}

				if (!hasChildAtPoint)
				{
					return window;
				}
			}

			return null;
		}

		private bool HandleAltInput(ConsoleKeyInfo key)
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

		private void HandleMouseEvent(object sender, List<MouseFlags> flags, Point point)
		{
			// Handle mouse button release first (end drag/resize) - highest priority
			if (flags.Contains(MouseFlags.Button1Released))
			{
				if (IsDragging || IsResizing)
				{
					// Invalidate the window that was moved/resized for a final clean redraw
					DragWindow?.Invalidate(true);

					// End interaction via service
					if (IsDragging)
					{
						_logService.LogDebug($"Drag ended: {DragWindow?.Title}", "Interaction");
						_windowStateService.EndDrag();
					}
					else if (IsResizing)
					{
						_logService.LogDebug($"Resize ended: {DragWindow?.Title}", "Interaction");
						_windowStateService.EndResize();
					}

					return;
				}
			}

			// Handle mouse movement during drag/resize operations - second priority
			if ((IsDragging || IsResizing) && DragWindow != null)
			{
				// Handle any mouse movement when in drag/resize mode
				if (IsDragging)
				{
					HandleWindowMove(point);
					return;
				}
				else if (IsResizing)
				{
					HandleWindowResize(point);
					return;
				}
			}

			// Handle mouse button press (start drag/resize) - third priority
			if (flags.Contains(MouseFlags.Button1Pressed) && !IsDragging && !IsResizing)
			{
				var window = GetWindowAtPoint(point);
				if (window != null)
				{
					// Activate the window if it's not already active
					if (window != ActiveWindow)
					{
						SetActiveWindow(window);
					}

					// IMPORTANT: Check if clicking on an interactive control first
					// Controls should have priority over resize/drag operations
					var contentControl = window.GetContentFromWindowCoordinates(TranslateToRelative(window, point));
					bool clickingOnControl = contentControl is Controls.IMouseAwareControl mouseAware
											  && mouseAware.WantsMouseEvents;

					// Only check resize/drag if NOT clicking on an interactive control
					if (!clickingOnControl)
					{
						// Check if we're starting a resize operation
						var resizeDirection = GetResizeDirection(window, point);
						if (resizeDirection != ResizeDirection.None && window.IsResizable)
						{
							// Start resize via service
							_logService.LogDebug($"Resize started: {window.Title} ({resizeDirection})", "Interaction");
							_windowStateService.StartResize(window, resizeDirection, point);
							return;
						}

						// Check if we're starting a move operation (title bar area)
						if (IsInTitleBar(window, point) && window.IsMovable)
						{
							// Start drag via service
							_logService.LogDebug($"Drag started: {window.Title}", "Interaction");
							_windowStateService.StartDrag(window, point);
							return;
						}
					}
				}
			}

			// Handle mouse clicks for window activation and event propagation (when not dragging) - lowest priority
			if (flags.Contains(MouseFlags.Button1Clicked) && !IsDragging && !IsResizing)
			{
				var window = GetWindowAtPoint(point);
				if (window != null)
				{
					// Check if close button was clicked
					if (IsOnCloseButton(window, point))
					{
						CloseWindow(window);
						return;
					}

					// Check if maximize button was clicked
					if (IsOnMaximizeButton(window, point))
					{
						if (window.State == WindowState.Maximized)
						{
							window.Restore();
						}
						else
						{
							window.Maximize();
						}
						return;
					}

					// Check if minimize button was clicked
					if (IsOnMinimizeButton(window, point))
					{
						window.Minimize();
						// If the minimized window was active, activate another window
						if (ActiveWindow == window)
						{
							// Find the next highest z-index window that isn't minimized
							var nextWindow = Windows.Values
								.Where(w => w != window && w.State != WindowState.Minimized)
								.OrderByDescending(w => w.ZIndex)
								.FirstOrDefault();
							if (nextWindow != null)
							{
								SetActiveWindow(nextWindow);
							}
							else
							{
								// No windows left - deactivate via service
								_windowStateService.ActivateWindow(null);
							}
						}
						return;
					}

					HandleWindowClick(window, flags, point);
					return; // Event handled, prevent double-propagation
				}
				else
				{
					// Clicked on empty desktop background - deactivate active window
					if (ActiveWindow != null)
					{
						ActiveWindow.SetIsActive(false);
						ActiveWindow.Invalidate(true);
						_windowStateService.ActivateWindow(null);
					}
				}
			}

			// Handle other mouse events for active window propagation
			if (ActiveWindow != null && !IsDragging && !IsResizing)
			{
				// Check if mouse event is over the active window
				var windowAtPoint = GetWindowAtPoint(point);

				if (windowAtPoint == ActiveWindow)
				{
					// Propagate mouse event to the active window
					PropagateMouseEventToWindow(ActiveWindow, flags, point);
				}
			}
		}

		private ResizeDirection GetResizeDirection(Window window, Point point)
		{
			// Borderless windows cannot be resized from borders
			if (window.BorderStyle == BorderStyle.None)
			{
				return ResizeDirection.None;
			}

			// Convert to window-relative coordinates
			var relativePoint = TranslateToRelative(window, point);
			
			// Define resize border thickness
			const int borderThickness = 1;  // Border is exactly 1 row/column thick
			const int cornerSize = 3; // Must match title bar corner exclusion
			
			// Check if point is within expanded window bounds for resize detection
			if (relativePoint.X < -borderThickness || relativePoint.X >= window.Width + borderThickness ||
				relativePoint.Y < -borderThickness || relativePoint.Y >= window.Height + borderThickness)
			{
				return ResizeDirection.None;
			}

			// IMPORTANT: Exclude title bar area (Y=0) from TOP resize detection to allow window moving
			// Only allow top resize when NOT in the title bar row
			bool onLeftBorder = relativePoint.X < borderThickness;
			bool onRightBorder = relativePoint.X >= window.Width - borderThickness;
			bool onTopBorder = relativePoint.Y < borderThickness && relativePoint.Y != 0; // Exclude title bar
			bool onBottomBorder = relativePoint.Y >= window.Height - borderThickness;
			
			// For corner detection, only allow top corners when NOT in title bar center area
			bool inTitleBarCorner = relativePoint.Y == 0 && 
									   (relativePoint.X < cornerSize || relativePoint.X >= window.Width - cornerSize);

			// Corner resize areas (only allow top corners in title bar corner zones)
			if (relativePoint.Y == 0)
			{
				// In title bar row - only allow corner resize in the corner zones
				if (inTitleBarCorner && onLeftBorder) return ResizeDirection.TopLeft;
				if (inTitleBarCorner && onRightBorder) return ResizeDirection.TopRight;
			}
			else
			{
				// Not in title bar - allow all corner combinations
				if (onTopBorder && onLeftBorder) return ResizeDirection.TopLeft;
				if (onTopBorder && onRightBorder) return ResizeDirection.TopRight;
			}
			
			// Bottom corners work normally
			if (onBottomBorder && onLeftBorder) return ResizeDirection.BottomLeft;
			if (onBottomBorder && onRightBorder) return ResizeDirection.BottomRight;

			// Resize grip at bottom-right (width-2, height-1) also triggers BottomRight resize
			if (IsOnResizeGrip(window, point)) return ResizeDirection.BottomRight;

			// Edge resize areas
			if (onTopBorder) return ResizeDirection.Top; // This won't trigger for Y=0 due to exclusion above
			if (onBottomBorder) return ResizeDirection.Bottom;
			if (onLeftBorder) return ResizeDirection.Left;
			if (onRightBorder) return ResizeDirection.Right;

			return ResizeDirection.None;
		}

		private bool IsInTitleBar(Window window, Point point)
		{
			// Borderless windows have no title bar
			if (window.BorderStyle == BorderStyle.None)
			{
				return false;
			}

			var relativePoint = TranslateToRelative(window, point);

			// Must be in the top row
			if (relativePoint.Y != 0)
				return false;

			// Must be within window bounds
			if (relativePoint.X < 0 || relativePoint.X >= window.Width)
				return false;

			// Exclude left corner for resize
			const int cornerSize = 3;
			if (relativePoint.X < cornerSize)
				return false;

			// Exclude button area on the right (close, maximize, minimize buttons + corner)
			int closeButtonWidth = window.IsClosable ? 3 : 0;
			int maximizeButtonWidth = window.IsMaximizable ? 3 : 0;
			int minimizeButtonWidth = window.IsMinimizable ? 3 : 0;
			int rightExcludeWidth = 1 + closeButtonWidth + maximizeButtonWidth + minimizeButtonWidth; // +1 for corner

			if (relativePoint.X >= window.Width - rightExcludeWidth)
				return false;

			return true;
		}

		private bool IsOnCloseButton(Window window, Point point)
		{
			if (!window.IsClosable)
				return false;

			var relativePoint = TranslateToRelative(window, point);

			// Top row only
			if (relativePoint.Y != 0)
				return false;

			// Close button [X] is 3 characters wide, positioned just before the corner at (width-1)
			// Button spans positions: (width-4), (width-3), (width-2)
			int closeButtonStart = window.Width - 4;
			int closeButtonEnd = window.Width - 2;

			return relativePoint.X >= closeButtonStart && relativePoint.X <= closeButtonEnd;
		}

		private bool IsOnMaximizeButton(Window window, Point point)
		{
			if (!window.IsMaximizable)
				return false;

			var relativePoint = TranslateToRelative(window, point);

			// Top row only
			if (relativePoint.Y != 0)
				return false;

			// Calculate maximize button position
			// Button order from right: corner(width-1), close[X](3 chars if present), maximize[+](3 chars), minimize[_]
			int closeButtonWidth = window.IsClosable ? 3 : 0;
			int maximizeButtonEnd = window.Width - 2 - closeButtonWidth;
			int maximizeButtonStart = maximizeButtonEnd - 2;

			return relativePoint.X >= maximizeButtonStart && relativePoint.X <= maximizeButtonEnd;
		}

		private bool IsOnMinimizeButton(Window window, Point point)
		{
			if (!window.IsMinimizable)
				return false;

			var relativePoint = TranslateToRelative(window, point);

			// Top row only
			if (relativePoint.Y != 0)
				return false;

			// Calculate minimize button position
			// Button order from right: corner(width-1), close[X](3 chars if present), maximize[+](3 chars if present), minimize[_](3 chars)
			int closeButtonWidth = window.IsClosable ? 3 : 0;
			int maximizeButtonWidth = window.IsMaximizable ? 3 : 0;
			int minimizeButtonEnd = window.Width - 2 - closeButtonWidth - maximizeButtonWidth;
			int minimizeButtonStart = minimizeButtonEnd - 2;

			return relativePoint.X >= minimizeButtonStart && relativePoint.X <= minimizeButtonEnd;
		}

		private bool IsOnResizeGrip(Window window, Point point)
		{
			if (!window.IsResizable)
				return false;

			var relativePoint = TranslateToRelative(window, point);

			// Resize grip is at the bottom-right corner, position (width-1, height-1)
			return relativePoint.Y == window.Height - 1 && relativePoint.X == window.Width - 1;
		}

		private void HandleWindowMove(Point currentMousePos)
		{
			var dragState = _windowStateService.CurrentDrag;
			if (dragState == null) return;

			var window = dragState.Window;

			// Store the current window bounds before moving
			var oldBounds = new Rectangle(window.Left, window.Top, window.Width, window.Height);

			// Calculate the offset from the start position
			int deltaX = currentMousePos.X - dragState.StartMousePos.X;
			int deltaY = currentMousePos.Y - dragState.StartMousePos.Y;

			// Calculate new position
			int newLeft = dragState.StartWindowPos.X + deltaX;
			int newTop = dragState.StartWindowPos.Y + deltaY;

			// Constrain to desktop bounds
			newLeft = Math.Max(DesktopUpperLeft.X, Math.Min(newLeft, DesktopBottomRight.X - window.Width + 1));
			newTop = Math.Max(DesktopUpperLeft.Y, Math.Min(newTop, DesktopBottomRight.Y - window.Height + 1));

			// Only update if position actually changed
			if (newLeft != window.Left || newTop != window.Top)
			{
				// FIRST: Clear the old window position completely
				_renderer.FillRect(window.Left, window.Top, window.Width, window.Height,
					Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

				// THEN: Apply the new position
				window.SetPosition(new Point(newLeft, newTop));

				// FINALLY: Force redraw of the window at its new position
				window.Invalidate(true);

				// And invalidate exposed regions to redraw any windows that were underneath
				InvalidateExposedRegions(window, oldBounds);
			}
		}

		private void HandleWindowResize(Point currentMousePos)
		{
			var resizeState = _windowStateService.CurrentResize;
			if (resizeState == null) return;

			var window = resizeState.Window;

			// Store the current window bounds before resizing
			var oldBounds = new Rectangle(window.Left, window.Top, window.Width, window.Height);

			// Calculate the offset from the start position
			int deltaX = currentMousePos.X - resizeState.StartMousePos.X;
			int deltaY = currentMousePos.Y - resizeState.StartMousePos.Y;

			// Calculate new position and size based on resize direction
			int newLeft = resizeState.StartWindowPos.X;
			int newTop = resizeState.StartWindowPos.Y;
			int newWidth = resizeState.StartWindowSize.Width;
			int newHeight = resizeState.StartWindowSize.Height;

			switch (resizeState.Direction)
			{
				case ResizeDirection.Top:
					newTop = resizeState.StartWindowPos.Y + deltaY;
					newHeight = resizeState.StartWindowSize.Height - deltaY;
					break;

				case ResizeDirection.Bottom:
					newHeight = resizeState.StartWindowSize.Height + deltaY;
					break;

				case ResizeDirection.Left:
					newLeft = resizeState.StartWindowPos.X + deltaX;
					newWidth = resizeState.StartWindowSize.Width - deltaX;
					break;

				case ResizeDirection.Right:
					newWidth = resizeState.StartWindowSize.Width + deltaX;
					break;

				case ResizeDirection.TopLeft:
					newLeft = resizeState.StartWindowPos.X + deltaX;
					newTop = resizeState.StartWindowPos.Y + deltaY;
					newWidth = resizeState.StartWindowSize.Width - deltaX;
					newHeight = resizeState.StartWindowSize.Height - deltaY;
					break;

				case ResizeDirection.TopRight:
					newTop = resizeState.StartWindowPos.Y + deltaY;
					newWidth = resizeState.StartWindowSize.Width + deltaX;
					newHeight = resizeState.StartWindowSize.Height - deltaY;
					break;

				case ResizeDirection.BottomLeft:
					newLeft = resizeState.StartWindowPos.X + deltaX;
					newWidth = resizeState.StartWindowSize.Width - deltaX;
					newHeight = resizeState.StartWindowSize.Height + deltaY;
					break;

				case ResizeDirection.BottomRight:
					newWidth = resizeState.StartWindowSize.Width + deltaX;
					newHeight = resizeState.StartWindowSize.Height + deltaY;
					break;
			}

			// Constrain to minimum/maximum sizes and desktop bounds
			newWidth = Math.Max(10, newWidth); // Minimum width
			newHeight = Math.Max(3, newHeight); // Minimum height

			// Constrain to desktop bounds
			newLeft = Math.Max(DesktopUpperLeft.X, Math.Min(newLeft, DesktopBottomRight.X - newWidth + 1));
			newTop = Math.Max(DesktopUpperLeft.Y, Math.Min(newTop, DesktopBottomRight.Y - newHeight + 1));

			// Ensure the window doesn't resize beyond desktop bounds
			newWidth = Math.Min(newWidth, DesktopBottomRight.X - newLeft + 1);
			newHeight = Math.Min(newHeight, DesktopBottomRight.Y - newTop + 1);

			// Only update if position or size actually changed
			if (newLeft != window.Left || newTop != window.Top ||
				newWidth != window.Width || newHeight != window.Height)
			{
				// FIRST: Clear the old window position/size completely
				_renderer.FillRect(window.Left, window.Top, window.Width, window.Height,
					Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

				// THEN: Apply the new position and size
				window.SetPosition(new Point(newLeft, newTop));
				window.SetSize(newWidth, newHeight);

				// FINALLY: Force redraw of the window at its new position and size
				window.Invalidate(true);

				// And invalidate exposed regions to redraw any windows that were underneath
				InvalidateExposedRegions(window, oldBounds);
			}
		}

		private bool HandleMoveInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Move, Direction.Up);
					ActiveWindow?.SetPosition(new Point(ActiveWindow?.Left ?? 0, Math.Max(0, (ActiveWindow?.Top ?? 0) - 1)));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Move, Direction.Down);
					ActiveWindow?.SetPosition(new Point(ActiveWindow?.Left ?? 0, Math.Min(DesktopBottomRight.Y - (ActiveWindow?.Height ?? 0) + 1, (ActiveWindow?.Top ?? 0) + 1)));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Move, Direction.Left);
					ActiveWindow?.SetPosition(new Point(Math.Max(DesktopUpperLeft.X, (ActiveWindow?.Left ?? 0) - 1), ActiveWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Move, Direction.Right);
					ActiveWindow?.SetPosition(new Point(Math.Min(DesktopBottomRight.X - (ActiveWindow?.Width ?? 0) + 1, (ActiveWindow?.Left ?? 0) + 1), ActiveWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.X:
					if (ActiveWindow?.IsClosable ?? false)
					{
						CloseWindow(ActiveWindow);
					}
					handled = true;
					break;
			}
			return handled;
		}

		private bool HandleResizeInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Resize, Direction.Up);
					ActiveWindow?.SetSize(ActiveWindow.Width, Math.Max(1, ActiveWindow.Height - 1));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Resize, Direction.Down);
					ActiveWindow?.SetSize(ActiveWindow.Width, Math.Min(DesktopDimensions.Height - ActiveWindow.Top, ActiveWindow.Height + 1));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Move, Direction.Left);
					ActiveWindow?.SetSize(Math.Max(1, ActiveWindow.Width - 1), ActiveWindow.Height);
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(ActiveWindow, WindowTopologyAction.Move, Direction.Right);
					ActiveWindow?.SetSize(Math.Min(DesktopBottomRight.X - ActiveWindow.Left + 1, ActiveWindow.Width + 1), ActiveWindow.Height);
					handled = true;
					break;
			}

			return handled;
		}

		// Helper method to check if a window is a child of another
		private bool IsChildWindow(Window potentialChild, Window potentialParent)
		{
			Window? current = potentialChild;
			while (current?.ParentWindow != null)
			{
				if (current.ParentWindow.Equals(potentialParent))
					return true;
				current = current.ParentWindow;
			}
			return false;
		}

		private bool IsCompletelyCovered(Window window)
		{
			foreach (var otherWindow in Windows.Values)
			{
				if (otherWindow != window && _renderer.IsOverlapping(window, otherWindow) && otherWindow.ZIndex > window.ZIndex)
				{
					if (otherWindow.Left <= window.Left && otherWindow.Top <= window.Top &&
						otherWindow.Left + otherWindow.Width >= window.Left + window.Width &&
						otherWindow.Top + otherWindow.Height >= window.Top + window.Height)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void MoveOrResizeOperation(Window? window, WindowTopologyAction windowTopologyAction, Direction direction)
		{
			if (window == null) return;

			// Store the current window bounds before any operation
			var oldBounds = new Rectangle(window.Left, window.Top, window.Width, window.Height);

			// FIRST: Clear the old window position completely (same as mouse operations)
			_renderer.FillRect(window.Left, window.Top, window.Width, window.Height,
				Theme.DesktopBackgroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			// No need for direction-specific clearing since we clear the entire window

			// Redraw the necessary regions that were underneath the window
			foreach (var w in Windows.Values.OrderBy(w => w.ZIndex))
			{
				// Skip minimized windows - they're invisible
				if (w.State == WindowState.Minimized)
					continue;

				if (w != window && DoesRectangleOverlapWindow(oldBounds, w))
				{
					// Redraw the parts of underlying windows that were covered
					var intersection = GetRectangleIntersection(oldBounds,
						new Rectangle(w.Left, w.Top, w.Width, w.Height));
					
					if (!intersection.IsEmpty)
					{
						_renderer.RenderRegion(w, intersection);
					}
				}
			}

			// FINALLY: Invalidate the window which will cause it to redraw at its new position
			// (The actual position/size change happens in the calling HandleMoveInput method)
			window.Invalidate(false);
		}

		private void ProcessInput()
		{
			ConsoleKeyInfo? key;
			while ((key = _inputStateService.DequeueKey()) != null)
			{
				var keyInfo = key.Value;
				if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 && keyInfo.Key == ConsoleKey.T)
				{
					CycleActiveWindow();
				}
				else if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 && keyInfo.Key == ConsoleKey.Q)
				{
					_exitCode = 0;
					_running = false;
				}
				else if (ActiveWindow != null)
				{
					bool handled = ActiveWindow.ProcessInput(keyInfo);

					if (!handled)
					{
						if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0 && ActiveWindow.IsResizable)
						{
							handled = HandleResizeInput(keyInfo);
						}
						else if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 && ActiveWindow.IsMovable)
						{
							handled = HandleMoveInput(keyInfo);
						}
						else if ((keyInfo.Modifiers & ConsoleModifiers.Alt) != 0)
						{
							handled = HandleAltInput(keyInfo);
						}
					}
				}
			}
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
			_cursorStateService.ApplyCursorToConsole(
				_consoleDriver.ScreenSize.Width,
				_consoleDriver.ScreenSize.Height);
		}

		private void UpdateDisplay()
		{
			lock (_renderLock)
			{
				if (ShouldRenderTopStatus())
				{
					if (TopStatus != _cachedTopStatus)
					{
						var topRow = TopStatus;

						var effectiveLength = AnsiConsoleHelper.StripSpectreLength(topRow);
						var paddedTopRow = topRow.PadRight(_consoleDriver.ScreenSize.Width + (topRow.Length - effectiveLength));
						_consoleDriver.WriteToConsole(0, 0, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.TopBarForegroundColor}]{paddedTopRow}[/]", _consoleDriver.ScreenSize.Width, 1, false, Theme.TopBarBackgroundColor, null)[0]);

						_cachedTopStatus = TopStatus;
					}
				}

				var windowsToRender = new HashSet<Window>();

				// Identify dirty windows and overlapping windows
				foreach (var window in Windows.Values)
				{
					// Skip minimized windows - they're invisible
					if (window.State == WindowState.Minimized)
						continue;

					if (window.IsDirty && !IsCompletelyCovered(window))
					{
						var overlappingWindows = _renderer.GetOverlappingWindows(window);
						foreach (var overlappingWindow in overlappingWindows)
						{
							if (overlappingWindow.IsDirty || _renderer.IsOverlapping(window, overlappingWindow))
							{
								if (overlappingWindow.IsDirty || overlappingWindow.ZIndex > window.ZIndex)
								{
									windowsToRender.Add(overlappingWindow);
								}
							}
						}
					}
				}

				// PASS 1: Render normal (non-AlwaysOnTop) windows based on their ZIndex
				foreach (var window in Windows.Values
					.Where(w => !w.AlwaysOnTop)
					.OrderBy(w => w.ZIndex))
				{
					if (window != ActiveWindow && windowsToRender.Contains(window))
					{
						_renderer.RenderWindow(window);
					}
				}

				// Check if any of the overlapping windows is overlapping the active window
				if (ActiveWindow != null && !ActiveWindow.AlwaysOnTop)
				{
					if (windowsToRender.Contains(ActiveWindow))
					{
						_renderer.RenderWindow(ActiveWindow);
					}
					else
					{
						var overlappingWindows = _renderer.GetOverlappingWindows(ActiveWindow);

						foreach (var overlappingWindow in overlappingWindows)
						{
							if (windowsToRender.Contains(overlappingWindow))
							{
								_renderer.RenderWindow(ActiveWindow);
							}
						}
					}
				}

				// PASS 2: Render AlwaysOnTop windows (always last, on top of everything)
				foreach (var window in Windows.Values
					.Where(w => w.AlwaysOnTop && w.State != WindowState.Minimized)
					.OrderBy(w => w.ZIndex))
				{
					// AlwaysOnTop windows always render if dirty or in windowsToRender
					if (window.IsDirty || windowsToRender.Contains(window))
					{
						_renderer.RenderWindow(window);
					}
				}
			}

			if (ShouldRenderBottomStatus())
			{
				// Filter out sub-windows from the bottom status bar
				var topLevelWindows = Windows.Values
					.Where(w => w.ParentWindow == null)
					.ToList();

				var taskBar = _showTaskBar ? $"{string.Join(" | ", topLevelWindows.Select((w, i) => {
					var minIndicator = w.State == WindowState.Minimized ? "[dim]" : "";
					var minEnd = w.State == WindowState.Minimized ? "[/]" : "";
					return $"[bold]Alt-{i + 1}[/] {minIndicator}{StringHelper.TrimWithEllipsis(w.Title, 15, 7)}{minEnd}";
				}))} | " : string.Empty;

				string bottomRow = $"{taskBar}{BottomStatus}";

				// Display the list of window titles in the bottom row
				if (AnsiConsoleHelper.StripSpectreLength(bottomRow) > _consoleDriver.ScreenSize.Width)
				{
					bottomRow = AnsiConsoleHelper.TruncateSpectre(bottomRow, _consoleDriver.ScreenSize.Width);
				}

				bottomRow += new string(' ', _consoleDriver.ScreenSize.Width - AnsiConsoleHelper.StripSpectreLength(bottomRow));

				if (_cachedBottomStatus != bottomRow)
				{   //add padding to the bottom row
					_consoleDriver.WriteToConsole(0, _consoleDriver.ScreenSize.Height - 1, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.BottomBarForegroundColor}]{bottomRow}[/]", _consoleDriver.ScreenSize.Width, 1, false, Theme.BottomBarBackgroundColor, null)[0]);

					_cachedBottomStatus = bottomRow;
				}
			}

			_consoleDriver.Flush();
		}

		#region Plugin System

		/// <summary>
		/// Gets the list of loaded plugins.
		/// </summary>
		public IReadOnlyList<IPlugin> Plugins => _plugins.AsReadOnly();

		/// <summary>
		/// Loads plugins from the specified directory.
		/// If no path is specified, uses the "plugins" subdirectory of the application's base directory.
		/// </summary>
		/// <param name="pluginsPath">Optional path to the plugins directory.</param>
		public void LoadPluginsFromDirectory(string? pluginsPath = null)
		{
			pluginsPath ??= Path.Combine(AppContext.BaseDirectory, "plugins");
			if (!Directory.Exists(pluginsPath))
			{
				_logService?.LogDebug($"Plugin directory not found: {pluginsPath}", "Plugins");
				return;
			}

			foreach (var dll in Directory.GetFiles(pluginsPath, "*.dll"))
			{
				try
				{
					var assembly = System.Reflection.Assembly.LoadFrom(dll);
					foreach (var type in assembly.GetTypes().Where(t =>
						typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
					{
						var plugin = (IPlugin?)Activator.CreateInstance(type);
						if (plugin != null)
						{
							LoadPlugin(plugin);
							_logService?.LogInfo($"Loaded plugin: {plugin.Info.Name} v{plugin.Info.Version}", "Plugins");
						}
					}
				}
				catch (Exception ex)
				{
					_logService?.LogError($"Failed to load plugin from {dll}: {ex.Message}", ex, "Plugins");
				}
			}
		}

		/// <summary>
		/// Loads a plugin instance and registers its contributions.
		/// </summary>
		/// <param name="plugin">The plugin to load.</param>
		public void LoadPlugin(IPlugin plugin)
		{
			if (plugin == null)
				throw new ArgumentNullException(nameof(plugin));

			// Initialize the plugin
			plugin.Initialize(this);

			// Register themes to ThemeRegistry
			foreach (var theme in plugin.GetThemes())
			{
				ThemeRegistry.RegisterTheme(theme.Name, theme.Description, () => theme.Theme);
				_logService?.LogDebug($"Registered theme: {theme.Name}", "Plugins");
			}

			// Register control factories
			foreach (var control in plugin.GetControls())
			{
				_pluginControlFactories[control.Name] = control.Factory;
				_logService?.LogDebug($"Registered control: {control.Name}", "Plugins");
			}

			// Register window factories
			foreach (var window in plugin.GetWindows())
			{
				_pluginWindowFactories[window.Name] = window.Factory;
				_logService?.LogDebug($"Registered window: {window.Name}", "Plugins");
			}

			// Register services
			foreach (var service in plugin.GetServices())
			{
				_pluginServices[service.ServiceType] = service.Instance;
				_logService?.LogDebug($"Registered service: {service.ServiceType.Name}", "Plugins");
			}

			_plugins.Add(plugin);
		}

		/// <summary>
		/// Loads a plugin of the specified type.
		/// </summary>
		/// <typeparam name="T">The plugin type to instantiate and load.</typeparam>
		public void LoadPlugin<T>() where T : IPlugin, new()
		{
			LoadPlugin(new T());
		}

		/// <summary>
		/// Creates a control instance from a plugin-registered factory.
		/// </summary>
		/// <param name="name">The name of the control to create.</param>
		/// <returns>The created control instance, or null if not found.</returns>
		public IWindowControl? CreatePluginControl(string name)
		{
			return _pluginControlFactories.TryGetValue(name, out var factory) ? factory() : null;
		}

		/// <summary>
		/// Creates a window instance from a plugin-registered factory.
		/// </summary>
		/// <param name="name">The name of the window to create.</param>
		/// <returns>The created window instance, or null if not found.</returns>
		public Window? CreatePluginWindow(string name)
		{
			return _pluginWindowFactories.TryGetValue(name, out var factory) ? factory(this) : null;
		}

		/// <summary>
		/// Gets a service instance registered by a plugin.
		/// </summary>
		/// <typeparam name="T">The service type to retrieve.</typeparam>
		/// <returns>The service instance, or null if not found.</returns>
		public T? GetService<T>() where T : class
		{
			return _pluginServices.TryGetValue(typeof(T), out var service) ? service as T : null;
		}

		/// <summary>
		/// Gets the names of all registered plugin controls.
		/// </summary>
		public IReadOnlyCollection<string> RegisteredPluginControls => _pluginControlFactories.Keys;

		/// <summary>
		/// Gets the names of all registered plugin windows.
		/// </summary>
		public IReadOnlyCollection<string> RegisteredPluginWindows => _pluginWindowFactories.Keys;

		/// <summary>
		/// Gets the types of all registered plugin services.
		/// </summary>
		public IReadOnlyCollection<Type> RegisteredPluginServices => _pluginServices.Keys;

		#endregion
	}
}