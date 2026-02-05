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
using SharpConsoleUI.Performance;
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
		private readonly StatusBarStateService _statusBarStateService;
		private readonly ILogService _logService;
		private readonly ConsoleWindowSystem _windowSystemContext;
		private readonly ConsoleWindowSystemOptions _options;
		private readonly PerformanceTracker _performanceTracker;

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
		/// <param name="statusBarStateService">Service managing status bar state and Start menu.</param>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="windowSystemContext">Context providing access to window system properties.</param>
		/// <param name="options">Configuration options for the window system.</param>
		/// <param name="performanceTracker">Performance metrics tracker.</param>
		public RenderCoordinator(
			IConsoleDriver consoleDriver,
			Renderer renderer,
			WindowStateService windowStateService,
			StatusBarStateService statusBarStateService,
			ILogService logService,
			ConsoleWindowSystem windowSystemContext,
			ConsoleWindowSystemOptions options,
			PerformanceTracker performanceTracker)
		{
			_consoleDriver = consoleDriver ?? throw new ArgumentNullException(nameof(consoleDriver));
			_renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
			_windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
			_statusBarStateService = statusBarStateService ?? throw new ArgumentNullException(nameof(statusBarStateService));
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_windowSystemContext = windowSystemContext ?? throw new ArgumentNullException(nameof(windowSystemContext));
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_performanceTracker = performanceTracker ?? throw new ArgumentNullException(nameof(performanceTracker));
		}

		#region Public Properties

		/// <summary>
		/// Gets the top status bar bounds for mouse hit testing.
		/// </summary>
		public Rectangle TopStatusBarBounds => _statusBarStateService.TopStatusBarBounds;

		/// <summary>
		/// Gets the bottom status bar bounds for mouse hit testing.
		/// </summary>
		public Rectangle BottomStatusBarBounds => _statusBarStateService.BottomStatusBarBounds;

		/// <summary>
		/// Gets the start button bounds for mouse hit testing.
		/// </summary>
		public Rectangle StartButtonBounds => _statusBarStateService.StartButtonBounds;

		#endregion

		#region Public Methods

		/// <summary>
		/// Gets the height occupied by the top status bar (0 or 1).
		/// Accounts for both status text and performance metrics.
		/// </summary>
		public int GetTopStatusHeight()
		{
			return _statusBarStateService.GetTopStatusHeight(_statusBarStateService.ShowTopStatus, _options.EnablePerformanceMetrics);
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// Accounts for both status text and Start button.
		/// </summary>
		public int GetBottomStatusHeight()
		{
			return _statusBarStateService.GetBottomStatusHeight(_statusBarStateService.ShowBottomStatus, _options.StatusBar.ShowTaskBar, _options.StatusBar.ShowStartButton, _options.StatusBar.StartButtonLocation);
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
		/// Invalidates all windows and status bars, forcing a complete redraw.
		/// Call this after theme changes, status bar visibility changes, or other global UI updates.
		/// </summary>
		public void InvalidateAllWindows()
		{
			InvalidateStatusCache();
			foreach (var w in _windowSystemContext.Windows.Values)
			{
				w.Invalidate(true);
			}
		}

		/// <summary>
		/// Updates the status bar bounds based on current screen size and configuration.
		/// Call this after screen resizes or configuration changes.
		/// </summary>
		public void UpdateStatusBarBounds()
		{
			_statusBarStateService.UpdateStatusBarBounds(
				_consoleDriver.ScreenSize.Width,
				_consoleDriver.ScreenSize.Height,
				_statusBarStateService.ShowTopStatus,
				_statusBarStateService.ShowBottomStatus,
				_options.StatusBar);
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
			// Begin new frame for diagnostics tracking
			_windowSystemContext.RenderingDiagnostics?.BeginFrame();

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
					_performanceTracker.SetDirtyChars(_consoleDriver.GetDirtyCharacterCount());
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
			return _statusBarStateService.ShowTopStatus && (!string.IsNullOrEmpty(_statusBarStateService.TopStatus) || _options.EnablePerformanceMetrics);
		}

		/// <summary>
		/// Returns true if the bottom status bar should be rendered.
		/// </summary>
		private bool ShouldRenderBottomStatus()
		{
			// Render if we have status text OR if task bar (window list) is enabled
			bool hasContent = !string.IsNullOrEmpty(_statusBarStateService.BottomStatus) || _options.StatusBar.ShowTaskBar;
			bool hasStartButton = _options.StatusBar.ShowStartButton &&
								  _options.StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Bottom;

			return _statusBarStateService.ShowBottomStatus && (hasContent || hasStartButton);
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

			// Clear coverage cache - Z-indices may have changed since last frame
			_coverageCache.Clear();

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

			// Safety: Always ensure the window being dragged is in the render list.
			// This prevents the dragging window from going invisible if an edge case
			// causes it to be skipped (e.g., marked clean prematurely or covered check race).
			var dragState = _windowStateService.CurrentDrag;
			if (dragState != null && dragState.Window != null &&
			    dragState.Window.State != WindowState.Minimized &&
			    dragState.Window.Width > 0 && dragState.Window.Height > 0 &&
			    !_windowsToRender.Contains(dragState.Window))
			{
				dragState.Window.Invalidate(true);
				_windowsToRender.Add(dragState.Window);
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
			var baseStatus = _statusBarStateService.TopStatus ?? string.Empty;
			var metricsString = _options.EnablePerformanceMetrics
				? _performanceTracker.FormatMetrics()
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
				bottomRow = $"{startButton}{taskBar}{_statusBarStateService.BottomStatus}";
			}
			else
			{
				// Right position - add start button at the end
				var content = $"{taskBar}{_statusBarStateService.BottomStatus}";
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
