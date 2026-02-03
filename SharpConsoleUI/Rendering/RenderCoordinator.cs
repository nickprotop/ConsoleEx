// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Themes;
using System.Drawing;

namespace SharpConsoleUI.Rendering
{
	/// <summary>
	/// Coordinates all rendering operations for the console window system.
	/// Handles window rendering, status bar display, caching, and performance metrics.
	/// Extracted from ConsoleWindowSystem as part of Phase 1.2 refactoring.
	/// </summary>
	public class RenderCoordinator
	{
		// Dependencies (injected via constructor)
		private readonly IConsoleDriver _consoleDriver;
		private readonly Renderer _renderer;
		private readonly WindowStateService _windowStateService;
		private readonly ILogService _logService;
		private readonly IWindowSystemContext _windowSystemContext;
		private readonly ConsoleWindowSystemOptions _options;

		// Performance optimization: cached collections to avoid allocations in hot paths
		private readonly HashSet<Window> _windowsToRender = new HashSet<Window>();
		private readonly List<Window> _sortedWindows = new List<Window>();
		private readonly Dictionary<string, bool> _coverageCache = new Dictionary<string, bool>();
		private readonly List<Rectangle> _pendingDesktopClears = new List<Rectangle>();

		// Status bar caching
		private string? _cachedBottomStatus;
		private string? _cachedTopStatus;
		private string? _cachedTaskBar;
		private int _taskBarWindowCount;
		private int _taskBarStateHash;
		private bool _showTopStatus = true;
		private bool _showBottomStatus = true;

		// Bounds
		private Rectangle _topStatusBarBounds = Rectangle.Empty;
		private Rectangle _bottomStatusBarBounds = Rectangle.Empty;
		private Rectangle _startButtonBounds = Rectangle.Empty;

		// Performance metrics
		private double _currentFrameTimeMs;
		private int _currentWindowCount;
		private int _currentDirtyCount;
		private int _currentDirtyChars;
		private int _displayedDirtyChars;
		private DateTime _lastDirtyCharsChange = DateTime.UtcNow;
		private const int DirtyCharsHoldTimeMs = 1000;

		// Render lock for thread safety
		private readonly object _renderLock = new object();

		// Track windows needing region update
		private readonly HashSet<Window> _windowsNeedingRegionUpdate = new();

		/// <summary>
		/// Initializes a new instance of the RenderCoordinator class.
		/// </summary>
		/// <param name="consoleDriver">Console driver for low-level I/O.</param>
		/// <param name="renderer">Renderer for window and content rendering.</param>
		/// <param name="windowStateService">Service managing window state and Z-order.</param>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="windowSystemContext">Context providing access to window system properties.</param>
		/// <param name="options">Configuration options for the window system.</param>
		public RenderCoordinator(
			IConsoleDriver consoleDriver,
			Renderer renderer,
			WindowStateService windowStateService,
			ILogService logService,
			IWindowSystemContext windowSystemContext,
			ConsoleWindowSystemOptions options)
		{
			_consoleDriver = consoleDriver ?? throw new ArgumentNullException(nameof(consoleDriver));
			_renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
			_windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_windowSystemContext = windowSystemContext ?? throw new ArgumentNullException(nameof(windowSystemContext));
			_options = options ?? throw new ArgumentNullException(nameof(options));
		}

		#region Public Properties

		/// <summary>
		/// Gets the most recent frame time in milliseconds.
		/// </summary>
		public double CurrentFrameTimeMs => _currentFrameTimeMs;

		/// <summary>
		/// Gets the current frames per second (FPS).
		/// </summary>
		public double CurrentFPS => _currentFrameTimeMs > 0 ? 1000.0 / _currentFrameTimeMs : 0;

		/// <summary>
		/// Gets the displayed dirty character count (held for visibility).
		/// </summary>
		public int CurrentDirtyChars => _displayedDirtyChars;

		/// <summary>
		/// Gets the current window count.
		/// </summary>
		public int CurrentWindowCount => _currentWindowCount;

		/// <summary>
		/// Gets the top status bar bounds for mouse hit testing.
		/// </summary>
		public Rectangle TopStatusBarBounds => _topStatusBarBounds;

		/// <summary>
		/// Gets the bottom status bar bounds for mouse hit testing.
		/// </summary>
		public Rectangle BottomStatusBarBounds => _bottomStatusBarBounds;

		/// <summary>
		/// Gets the start button bounds for mouse hit testing.
		/// </summary>
		public Rectangle StartButtonBounds => _startButtonBounds;

		#endregion

		#region Public Methods

		/// <summary>
		/// Gets the height occupied by the top status bar (0 or 1).
		/// Accounts for both status text and performance metrics.
		/// </summary>
		public int GetTopStatusHeight()
		{
			return ShouldRenderTopStatus() ? 1 : 0;
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// Accounts for both status text and Start button.
		/// </summary>
		public int GetBottomStatusHeight()
		{
			return ShouldRenderBottomStatus() ? 1 : 0;
		}

		/// <summary>
		/// Invalidates all status bar caches, forcing them to be rebuilt on next render.
		/// Call this when window state, titles, or themes change.
		/// </summary>
		public void InvalidateStatusCache()
		{
			_cachedBottomStatus = null;
			_cachedTopStatus = null;
			_cachedTaskBar = null;
		}

		/// <summary>
		/// Sets whether the top status bar should be shown.
		/// </summary>
		public void SetShowTopStatus(bool show)
		{
			_showTopStatus = show;
		}

		/// <summary>
		/// Sets whether the bottom status bar should be shown.
		/// </summary>
		public void SetShowBottomStatus(bool show)
		{
			_showBottomStatus = show;
		}

		/// <summary>
		/// Tracks performance metrics for the current frame.
		/// </summary>
		/// <param name="frameTimeMs">The frame time in milliseconds.</param>
		public void TrackPerformanceFrame(double frameTimeMs)
		{
			// Update current frame metrics - real-time, no aggregation
			_currentFrameTimeMs = frameTimeMs;
			_currentWindowCount = _windowSystemContext.Windows.Count;
			_currentDirtyCount = _windowSystemContext.Windows.Values.Count(w => w.IsDirty);
			// NOTE: _currentDirtyChars is captured in UpdateDisplay() before Flush()

			// Handle DirtyChars hold logic: preserve last non-zero value for visibility
			if (_currentDirtyChars != _displayedDirtyChars)
			{
				// Value changed - update immediately
				_displayedDirtyChars = _currentDirtyChars;
				_lastDirtyCharsChange = DateTime.UtcNow;
			}
			else if (_currentDirtyChars == 0)
			{
				// Value is 0 - check if hold time expired
				var elapsed = (DateTime.UtcNow - _lastDirtyCharsChange).TotalMilliseconds;
				if (elapsed >= DirtyCharsHoldTimeMs)
				{
					_displayedDirtyChars = 0; // Reset to 0 after hold period
				}
				// else: preserve last non-zero value
			}
		}

		/// <summary>
		/// Updates the status bar bounds based on current screen size and configuration.
		/// Call this after screen resizes or configuration changes.
		/// </summary>
		public void UpdateStatusBarBounds()
		{
			if (_showTopStatus)
				_topStatusBarBounds = new Rectangle(0, 0, _consoleDriver.ScreenSize.Width, 1);

			if (_showBottomStatus)
				_bottomStatusBarBounds = new Rectangle(0, _consoleDriver.ScreenSize.Height - 1,
					_consoleDriver.ScreenSize.Width, 1);

			if (_options.StatusBar.ShowStartButton)
			{
				int y = _options.StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Top
					? 0
					: (_consoleDriver.ScreenSize.Height - 1);

				int x;
				int width = AnsiConsoleHelper.StripSpectreLength(_options.StatusBar.StartButtonText) + 1;

				if (_options.StatusBar.StartButtonPosition == Configuration.StartButtonPosition.Left)
				{
					x = 0;
				}
				else
				{
					x = _consoleDriver.ScreenSize.Width - width;
				}

				_startButtonBounds = new Rectangle(x, y, width, 1);
			}
		}

		/// <summary>
		/// Adds a desktop area to be cleared on the next render.
		/// Used for atomic clearing of old window positions.
		/// </summary>
		/// <param name="rect">The rectangle to clear.</param>
		public void AddPendingDesktopClear(Rectangle rect)
		{
			_pendingDesktopClears.Add(rect);
		}

		/// <summary>
		/// Adds a window to the set of windows needing region update.
		/// </summary>
		/// <param name="window">The window needing a region update.</param>
		public void AddWindowNeedingRegionUpdate(Window window)
		{
			_windowsNeedingRegionUpdate.Add(window);
		}

		/// <summary>
		/// Main rendering orchestrator.
		/// Renders all dirty windows, status bars, and flushes to console.
		/// </summary>
		public void UpdateDisplay()
		{
			lock (_renderLock)
			{
				// ATOMIC DESKTOP CLEARING: Clear old window positions before rendering
				// This prevents traces from rapid moves between frames
				if (_pendingDesktopClears.Count > 0)
				{
					// Copy list to avoid race condition (mouse events can add during iteration)
					var clearsCopy = _pendingDesktopClears.ToList();
					_pendingDesktopClears.Clear();

					foreach (var rect in clearsCopy)
					{
						_renderer.FillRect(rect.Left, rect.Top, rect.Width, rect.Height,
							_windowSystemContext.Theme.DesktopBackgroundChar,
							_windowSystemContext.Theme.DesktopBackgroundColor,
							_windowSystemContext.Theme.DesktopForegroundColor);
					}
				}

				// RENDERING ORDER:
				// 1. Windows first (so we can measure their dirty chars)
				// 2. Capture dirty chars (after windows, before TopStatus)
				// 3. TopStatus (with captured metrics, doesn't pollute measurement)
				// 4. BottomStatus
				// 5. Flush

				RenderWindows();

				// CRITICAL: Capture dirty chars AFTER windows rendered, BEFORE TopStatus
				// This measures window rendering work without including TopStatus itself
				if (_options.EnablePerformanceMetrics)
				{
					_currentDirtyChars = _consoleDriver.GetDirtyCharacterCount();
				}

				RenderTopStatus();
				RenderBottomStatus();

				// Update status bar bounds for mouse click detection
				UpdateStatusBarBounds();

				// Clear the region update set for next frame
				_windowsNeedingRegionUpdate.Clear();
				_consoleDriver.Flush();
			}
		}

		#endregion

		#region Private Helper Methods

		/// <summary>
		/// Returns true if the top status bar should be rendered.
		/// </summary>
		private bool ShouldRenderTopStatus()
		{
			return _showTopStatus && (!string.IsNullOrEmpty(_windowSystemContext.TopStatus) || _options.EnablePerformanceMetrics);
		}

		/// <summary>
		/// Returns true if the bottom status bar should be rendered.
		/// </summary>
		private bool ShouldRenderBottomStatus()
		{
			// Render if we have status text OR if task bar (window list) is enabled
			bool hasContent = !string.IsNullOrEmpty(_windowSystemContext.BottomStatus) || _options.StatusBar.ShowTaskBar;
			bool hasStartButton = _options.StatusBar.ShowStartButton &&
								  _options.StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Bottom;

			return _showBottomStatus && (hasContent || hasStartButton);
		}

		/// <summary>
		/// Builds the start button markup string.
		/// </summary>
		private string BuildStartButton()
		{
			if (!_options.StatusBar.ShowStartButton)
				return string.Empty;

			var text = _options.StatusBar.StartButtonText;
			return $"[bold cyan]{text}[/] ";
		}

		/// <summary>
		/// Formats the performance metrics string for display.
		/// </summary>
		private string FormatPerformanceMetrics()
		{
			if (_currentFrameTimeMs <= 0)
				return string.Empty;

			// Format: " | Frame:16ms Win:3 Dirty:1 DirtyChars:234"
			return $" [dim]|[/] " +
				   $"[dim]Frame:{_currentFrameTimeMs:F0}ms[/] " +
				   $"[dim]Win:{_currentWindowCount}[/] " +
				   $"[dim]Dirty:{_currentDirtyCount}[/] " +
				   $"[dim]DirtyChars:{_displayedDirtyChars}[/]";
		}

		/// <summary>
		/// Computes a hash of the task bar state to detect changes.
		/// </summary>
		private int ComputeTaskBarStateHash(List<Window> windows)
		{
			int hash = 0;
			foreach (var w in windows)
			{
				hash ^= w.Title.GetHashCode();
				hash ^= w.State.GetHashCode();
				hash ^= w.GetIsActive().GetHashCode();
			}
			return hash;
		}

		/// <summary>
		/// Checks if a window is completely covered by other windows (with caching).
		/// </summary>
		private bool IsCompletelyCovered(Window window)
		{
			// Check cache first
			if (_coverageCache.TryGetValue(window.Guid, out bool cached))
				return cached;

			// Calculate coverage
			bool result = CalculateIsCompletelyCovered(window);
			_coverageCache[window.Guid] = result;
			return result;
		}

		/// <summary>
		/// Calculates if a window is completely covered by other windows.
		/// </summary>
		private bool CalculateIsCompletelyCovered(Window window)
		{
			foreach (var otherWindow in _windowSystemContext.Windows.Values)
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

		/// <summary>
		/// Renders all dirty windows in proper Z-order.
		/// </summary>
		private void RenderWindows()
		{
			// Reuse cached HashSet to avoid allocation
			_windowsToRender.Clear();

			// Build sorted window list for rendering (avoid LINQ allocations)
			_sortedWindows.Clear();
			_sortedWindows.AddRange(_windowSystemContext.Windows.Values);
			_sortedWindows.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

			// Identify dirty windows and only overlapping windows with higher Z-index
			// This prevents unnecessary redraws of windows below the dirty window
			foreach (var window in _windowSystemContext.Windows.Values)
			{
				// Skip minimized windows - they're invisible
				if (window.State == WindowState.Minimized)
					continue;

				// Skip windows with invalid dimensions (can happen during rapid resize)
				if (window.Width <= 0 || window.Height <= 0)
					continue;

				if (!window.IsDirty)
					continue;

				if (IsCompletelyCovered(window))
					continue;

				_windowsToRender.Add(window);

				// OPTIMIZATION: Don't add overlapping windows to render list
				// VisibleRegions.CalculateVisibleRegions() already clips each window's rendering
				// to exclude areas covered by higher Z-index windows, so overlapping windows
				// don't need re-rendering when a window beneath them changes.
			}

			// PASS 1: Render normal (non-AlwaysOnTop) windows based on their ZIndex (no LINQ)
			for (int i = 0; i < _sortedWindows.Count; i++)
			{
				var window = _sortedWindows[i];
				if (window.AlwaysOnTop) continue;

				if (window != _windowSystemContext.ActiveWindow && _windowsToRender.Contains(window))
				{
					// Skip windows with invalid dimensions
					if (window.Width > 0 && window.Height > 0)
					{
						_renderer.RenderWindow(window);
					}
				}
			}

			// Check if any of the overlapping windows is overlapping the active window
			if (_windowSystemContext.ActiveWindow != null && !_windowSystemContext.ActiveWindow.AlwaysOnTop)
			{
				if (_windowsToRender.Contains(_windowSystemContext.ActiveWindow))
				{
					// Skip windows with invalid dimensions
					if (_windowSystemContext.ActiveWindow.Width > 0 && _windowSystemContext.ActiveWindow.Height > 0)
					{
						_renderer.RenderWindow(_windowSystemContext.ActiveWindow);
					}
				}
				else
				{
					var overlappingWindows = _renderer.GetOverlappingWindows(_windowSystemContext.ActiveWindow);

					foreach (var overlappingWindow in overlappingWindows)
					{
						// Only render active window if overlapping window is ABOVE it (higher Z-index)
						// Windows below the active window can't affect its visible pixels
						if (overlappingWindow.ZIndex > _windowSystemContext.ActiveWindow.ZIndex &&
							_windowsToRender.Contains(overlappingWindow))
						{
							// Skip windows with invalid dimensions
							if (_windowSystemContext.ActiveWindow.Width > 0 && _windowSystemContext.ActiveWindow.Height > 0)
							{
								_renderer.RenderWindow(_windowSystemContext.ActiveWindow);
								break;  // Only need to render once
							}
						}
					}
				}
			}

			// PASS 2: Render AlwaysOnTop windows (always last, on top of everything) (no LINQ)
			for (int i = 0; i < _sortedWindows.Count; i++)
			{
				var window = _sortedWindows[i];
				if (!window.AlwaysOnTop) continue;
				if (window.State == WindowState.Minimized) continue;

				// AlwaysOnTop windows always render if dirty or in windowsToRender
				if (window.IsDirty || _windowsToRender.Contains(window))
				{
					// Skip windows with invalid dimensions
					if (window.Width > 0 && window.Height > 0)
					{
						_renderer.RenderWindow(window);
					}
				}
			}
		}

		/// <summary>
		/// Renders the top status bar with optional performance metrics.
		/// </summary>
		private void RenderTopStatus()
		{
			if (!ShouldRenderTopStatus())
				return;

			// Build complete TopStatus with metrics appended
			var baseStatus = _windowSystemContext.TopStatus ?? string.Empty;
			var metricsString = _options.EnablePerformanceMetrics
				? FormatPerformanceMetrics()
				: string.Empty;
			var completeTopStatus = baseStatus + metricsString;

			// Build start button if configured for top
			var startButton = string.Empty;
			if (_options.StatusBar.ShowStartButton &&
				_options.StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Top)
			{
				startButton = BuildStartButton();
			}

			string topRow;
			if (_options.StatusBar.StartButtonPosition == Configuration.StartButtonPosition.Left)
			{
				topRow = $"{startButton}{completeTopStatus}";
			}
			else
			{
				// Right position - add start button at the end
				var contentLength = AnsiConsoleHelper.StripSpectreLength(completeTopStatus);
				var startButtonLength = AnsiConsoleHelper.StripSpectreLength(startButton);
				var availableSpace = _consoleDriver.ScreenSize.Width - startButtonLength;

				var content = completeTopStatus;
				if (contentLength > availableSpace)
				{
					content = AnsiConsoleHelper.TruncateSpectre(content, availableSpace);
				}

				content += new string(' ', availableSpace - AnsiConsoleHelper.StripSpectreLength(content));
				topRow = $"{content}{startButton}";
			}

			// Cache includes start button for proper invalidation
			if (topRow != _cachedTopStatus)
			{
				var effectiveLength = AnsiConsoleHelper.StripSpectreLength(topRow);
				var paddedTopRow = topRow.PadRight(_consoleDriver.ScreenSize.Width + (topRow.Length - effectiveLength));
				_consoleDriver.WriteToConsole(0, 0, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"[{_windowSystemContext.Theme.TopBarForegroundColor}]{paddedTopRow}[/]",
					_consoleDriver.ScreenSize.Width, 1, false,
					_windowSystemContext.Theme.TopBarBackgroundColor, null)[0]);

				_cachedTopStatus = topRow;
			}
		}

		/// <summary>
		/// Renders the bottom status bar with task bar and optional start button.
		/// </summary>
		private void RenderBottomStatus()
		{
			if (!ShouldRenderBottomStatus())
				return;

			// Filter out sub-windows and overlay windows from the bottom status bar
			var topLevelWindows = _windowSystemContext.Windows.Values
				.Where(w => w.ParentWindow == null && !(w is SharpConsoleUI.Windows.OverlayWindow))
				.ToList();

			// Check if task bar cache is valid
			string taskBar;
			if (_options.StatusBar.ShowTaskBar)
			{
				int stateHash = ComputeTaskBarStateHash(topLevelWindows);
				if (_cachedTaskBar != null &&
					_taskBarWindowCount == topLevelWindows.Count &&
					_taskBarStateHash == stateHash)
				{
					// Use cached task bar
					taskBar = _cachedTaskBar;
				}
				else
				{
					// Rebuild task bar
					taskBar = $"{string.Join(" | ", topLevelWindows.Select((w, i) => {
						var minIndicator = w.State == WindowState.Minimized ? "[dim]" : "";
						var minEnd = w.State == WindowState.Minimized ? "[/]" : "";
						return $"[bold]Alt-{i + 1}[/] {minIndicator}{StringHelper.TrimWithEllipsis(w.Title, 15, 7)}{minEnd}";
					}))} | ";

					// Update cache
					_cachedTaskBar = taskBar;
					_taskBarWindowCount = topLevelWindows.Count;
					_taskBarStateHash = stateHash;
				}
			}
			else
			{
				taskBar = string.Empty;
			}

			// Build start button if configured for bottom
			var startButton = string.Empty;
			if (_options.StatusBar.ShowStartButton &&
				_options.StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Bottom)
			{
				startButton = BuildStartButton();
			}

			string bottomRow;
			if (_options.StatusBar.StartButtonPosition == Configuration.StartButtonPosition.Left)
			{
				bottomRow = $"{startButton}{taskBar}{_windowSystemContext.BottomStatus}";
			}
			else
			{
				// Right position - add start button at the end
				var content = $"{taskBar}{_windowSystemContext.BottomStatus}";
				var contentLength = AnsiConsoleHelper.StripSpectreLength(content);
				var startButtonLength = AnsiConsoleHelper.StripSpectreLength(startButton);
				var availableSpace = _consoleDriver.ScreenSize.Width - startButtonLength;

				if (contentLength > availableSpace)
				{
					content = AnsiConsoleHelper.TruncateSpectre(content, availableSpace);
				}

				content += new string(' ', availableSpace - AnsiConsoleHelper.StripSpectreLength(content));
				bottomRow = $"{content}{startButton}";
			}

			// Display the list of window titles in the bottom row
			if (AnsiConsoleHelper.StripSpectreLength(bottomRow) > _consoleDriver.ScreenSize.Width)
			{
				bottomRow = AnsiConsoleHelper.TruncateSpectre(bottomRow, _consoleDriver.ScreenSize.Width);
			}

			bottomRow += new string(' ', _consoleDriver.ScreenSize.Width - AnsiConsoleHelper.StripSpectreLength(bottomRow));

			if (_cachedBottomStatus != bottomRow)
			{
				//add padding to the bottom row
				_consoleDriver.WriteToConsole(0, _consoleDriver.ScreenSize.Height - 1,
					AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						$"[{_windowSystemContext.Theme.BottomBarForegroundColor}]{bottomRow}[/]",
						_consoleDriver.ScreenSize.Width, 1, false,
						_windowSystemContext.Theme.BottomBarBackgroundColor, null)[0]);

				_cachedBottomStatus = bottomRow;
			}
		}

		#endregion
	}
}
