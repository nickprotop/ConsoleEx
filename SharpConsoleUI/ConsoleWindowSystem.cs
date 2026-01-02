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
using static SharpConsoleUI.Window;
using SharpConsoleUI.Drivers;
using System.Drawing;
using Color = Spectre.Console.Color;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI
{
	public enum Direction
	{
		Up,
		Down,
		Left,
		Right
	}

	public enum WindowTopologyAction
	{
		Resize,
		Move
	}

	public enum ResizeDirection
	{
		None,
		Top,
		Bottom,
		Left,
		Right,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	public class ConsoleWindowSystem
	{
		private readonly ConcurrentQueue<ConsoleKeyInfo> _inputQueue = new();
		private readonly Renderer _renderer;
		private readonly object _renderLock = new();
		private readonly VisibleRegions _visibleRegions;
		private readonly ConcurrentDictionary<string, Window> _windows = new();
		private Window? _activeWindow;
		private string? _cachedBottomStatus;
		private string? _cachedTopStatus;
		private IConsoleDriver _consoleDriver;
		private int _exitCode;
		private int _idleTime = 10;
		private bool _running;
		private bool _showTaskBar = true;

		// Mouse drag state
		private bool _isDragging = false;
		private bool _isResizing = false;
		private Window? _dragWindow = null;
		private Point _dragStartPos = Point.Empty;
		private Point _dragStartWindowPos = Point.Empty;
		private Size _dragStartWindowSize = new Size(0, 0);
		private ResizeDirection _resizeDirection = ResizeDirection.None;

		// Cursor state management
		private readonly CursorStateService _cursorStateService = new();

		public ConsoleWindowSystem(RenderMode renderMode)
		{
			RenderMode = renderMode;

			// Initialize the console driver
			_consoleDriver = new NetConsoleDriver(this)
			{
				RenderMode = RenderMode
			};

			// Initialize the visible regions
			_visibleRegions = new VisibleRegions(this);

			// Initialize the renderer
			_renderer = new Renderer(this);
		}

		public string BottomStatus { get; set; } = "";

		public IConsoleDriver ConsoleDriver
		{ get { return _consoleDriver; } set { _consoleDriver = value; } }

		public Point DesktopBottomRight => new Point(_consoleDriver.ScreenSize.Width - 1, _consoleDriver.ScreenSize.Height - 1 - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Helpers.Size DesktopDimensions => new Helpers.Size(_consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Point DesktopUpperLeft => new Point(0, string.IsNullOrEmpty(TopStatus) ? 0 : 1);
		public RenderMode RenderMode { get; set; }
		public bool ShowTaskBar { get => _showTaskBar; set => _showTaskBar = value; }
		public Theme Theme { get; set; } = new Theme();
		public string TopStatus { get; set; } = "";
		public VisibleRegions VisibleRegions => _visibleRegions;
		public ConcurrentDictionary<string, Window> Windows => _windows;
		public CursorStateService CursorStateService => _cursorStateService;

		public Window AddWindow(Window window, bool activateWindow = true)
		{
			window.ZIndex = _windows.Count > 0 ? _windows.Values.Max(w => w.ZIndex) + 1 : 0;
			_windows.TryAdd(window.Guid, window);

			if (_activeWindow == null || activateWindow) SetActiveWindow(window);

			window.WindowIsAdded();

			return window;
		}

		public void CloseModalWindow(Window? modalWindow)
		{
			if (modalWindow == null || modalWindow.Mode != WindowMode.Modal)
				return;

			// Store the parent window before closing
			Window? parentWindow = modalWindow.ParentWindow;

			// Close the modal window
			if (CloseWindow(modalWindow))
			{
				// If we have a parent window, ensure it becomes active
				if (parentWindow != null && _windows.ContainsKey(parentWindow.Guid))
				{
					SetActiveWindow(parentWindow);
				}
			}
		}

		public bool CloseWindow(Window? window, bool activateParent = true)
		{
			if (window == null) return false;
			if (!_windows.ContainsKey(window.Guid)) return false;

			if (window.Close(systemCall: true) == false) return false;

			// Store references before removal
			Window? parentWindow = window.ParentWindow;
			bool wasActive = (window == _activeWindow);

			// Remove from window collection
			_windows.TryRemove(window.Guid, out _);

			// Handle active window change if needed
			if (wasActive)
			{
				if (activateParent && parentWindow != null && _windows.ContainsKey(parentWindow.Guid))
				{
					// Activate the parent
					_activeWindow = parentWindow;
					SetActiveWindow(_activeWindow);
				}
				else
				{
					// Default behavior - activate window with highest Z-Index
					_activeWindow = _windows.Values.LastOrDefault(w => w.ZIndex == _windows.Values.Max(w => w.ZIndex));
					if (_activeWindow != null)
					{
						SetActiveWindow(_activeWindow);
					}
				}
			}

			// Redraw the screen
			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height,
							  Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
			foreach (var w in _windows.Values)
			{
				w.Invalidate(true);
			}

			return true;
		}

		public void FlashWindow(Window? window, int flashCount = 3, int flashDuration = 200, Color? flashBackgroundColor = null)
		{
			if (window == null) return;

			var originalBackgroundColor = window.BackgroundColor;
			var flashColor = flashBackgroundColor ?? (window.BackgroundColor == Theme.ButtonBackgroundColor ? window.ForegroundColor : Theme.ButtonBackgroundColor);

			var flashTask = new Task(async () =>
			{
				for (int i = 0; i < flashCount; i++)
				{
					if (window == null) return;

					window.BackgroundColor = flashColor;
					window.Invalidate(true);
					await Task.Delay(flashDuration);

					window.BackgroundColor = originalBackgroundColor;
					window.Invalidate(true);
					await Task.Delay(flashDuration);
				}
			});

			flashTask.Start();
		}

		public Window? GetWindow(string guid)
		{
			if (_windows.TryGetValue(guid, out var window))
			{
				return window;
			}
			return null;
		}

		public int Run()
		{
			_running = true;

			// Subscribe to the console driver events
			_consoleDriver.KeyPressed += (sender, key) =>
			{
				_inputQueue.Enqueue(key);
			};

			_consoleDriver.ScreenResized += (sender, size) =>
			{
				lock (_renderLock)
				{
					Helpers.Size desktopSize = DesktopDimensions;

					_consoleDriver.Clear();

					_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

					foreach (var window in _windows.Values)
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

			_consoleDriver.MouseEvent += HandleMouseEvent;

			// Start the console driver
			_consoleDriver.Start();

			// Initialize the console window system with background color and character
			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			// Main loop
			while (_running)
			{
				ProcessInput();
				UpdateDisplay();
				UpdateCursor();

				// Adjust idle time based on workload
				if (_inputQueue.IsEmpty && !AnyWindowDirty())
				{
					_idleTime = Math.Min(_idleTime + 10, 100); // Increase idle time up to 100ms
				}
				else
				{
					_idleTime = 10; // Reset idle time when there is work to do
				}

				Thread.Sleep(_idleTime);
			}

			_consoleDriver.Stop();

			return _exitCode;
		}

		/// <summary>
		/// Gracefully shuts down the console window system with the specified exit code
		/// </summary>
		/// <param name="exitCode">The exit code to return</param>
		public void Shutdown(int exitCode = 0)
		{
			_exitCode = exitCode;
			_running = false;
		}

		public void SetActiveWindow(Window window)
		{
			if (window == null)
			{
				return;
			}

			if (_windows.Values.Count(w => w.ParentWindow == null && w.Mode == WindowMode.Modal) > 0)
			{
				if (window != _activeWindow)
				{
					FlashWindow(_activeWindow);
				}

				return;
			}

			// Find the appropriate window to activate based on modality rules
			Window windowToActivate = FindWindowToActivate(window);

			var previousActiveWindow = _activeWindow;

			_windows.Values.FirstOrDefault(w => w.GetIsActive())?.SetIsActive(false);
			_activeWindow?.Invalidate(true);

			_activeWindow = windowToActivate;
			_activeWindow.SetIsActive(true);
			_activeWindow.ZIndex = _windows.Values.Max(w => w.ZIndex) + 1;

			_activeWindow.Invalidate(true);

			// Unfocus the currently focused control of other windows
			foreach (var w in _windows.Values)
			{
				if (w != _activeWindow)
				{
					w.UnfocusCurrentControl();
				}
			}
		}

		public (int absoluteLeft, int absoluteTop) TranslateToAbsolute(Window window, Point point)
		{
			int absoluteLeft = window.Left + point.X;
			int absoluteTop = window.Top + DesktopUpperLeft.Y + point.Y;
			return (absoluteLeft, absoluteTop);
		}

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
			var candidateWindows = _windows.Values
				.Where(w => w.ZIndex < movedWindowZIndex) // Only windows that were underneath
				.Where(w => DoesRectangleOverlapWindow(exposedRegion, w))
				.OrderBy(w => w.ZIndex) // Process in Z-order (bottom to top)
				.ToList();

			// Start by clearing the region with desktop background
			_renderer.FillRect(exposedRegion.X, exposedRegion.Y, exposedRegion.Width, exposedRegion.Height,
				Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

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
			if (window != _activeWindow)
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
			return _windows.Values.Any(window => window.IsDirty);
		}

		private void CycleActiveWindow()
		{
			if (_windows.Count == 0) return;

			var index = _windows.Values.ToList().IndexOf(_activeWindow ?? _windows.Values.First());
			Window? window = _windows.Values.ElementAt((index + 1) % _windows.Count);

			if (window != null)
			{
				SetActiveWindow(window);
				if (window.State == WindowState.Minimized) window.State = WindowState.Normal;
			}
		}

		// Helper method to find the deepest modal child window with the highest Z-index
		private Window? FindDeepestModalChild(Window window)
		{
			// Get all direct modal children of the window, ordered by Z-index (highest first)
			var modalChildren = _windows.Values
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
			var activeModalChild = _windows.Values
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
			List<Window> windows = _windows.Values
				.Where(window =>
					point.X >= window.Left &&
					point.X < window.Left + window.Width &&
					point.Y - DesktopUpperLeft.Y >= window.Top &&
					point.Y - DesktopUpperLeft.Y < window.Top + window.Height)
				.OrderBy(window => window.ZIndex).ToList();

			return windows.LastOrDefault();
		}

		private bool HandleAltInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			if (key.KeyChar >= (char)ConsoleKey.D1 && key.KeyChar <= (char)ConsoleKey.D9)
			{
				// Get only top-level windows to match what's displayed in the bottom status bar
				var topLevelWindows = _windows.Values
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
				if (_isDragging || _isResizing)
				{
					// Force a final cleanup after drag/resize operations
					if (_isResizing || _isDragging)
					{
						// Invalidate the window that was moved/resized for a final clean redraw
						if (_dragWindow != null)
						{
							_dragWindow.Invalidate(true);
						}
					}
					
					_isDragging = false;
					_isResizing = false;
					_dragWindow = null;
					_resizeDirection = ResizeDirection.None;
					return;
				}
			}

			// Handle mouse movement during drag/resize operations - second priority
			if ((_isDragging || _isResizing) && _dragWindow != null)
			{
				// Handle any mouse movement when in drag/resize mode
				if (_isDragging)
				{
					HandleWindowMove(point);
					return;
				}
				else if (_isResizing)
				{
					HandleWindowResize(point);
					return;
				}
			}

			// Handle mouse button press (start drag/resize) - third priority
			if (flags.Contains(MouseFlags.Button1Pressed) && !_isDragging && !_isResizing)
			{
				var window = GetWindowAtPoint(point);
				if (window != null)
				{
					// Activate the window if it's not already active
					if (window != _activeWindow)
					{
						SetActiveWindow(window);
					}

					// Check if we're starting a resize operation
					var resizeDirection = GetResizeDirection(window, point);
					if (resizeDirection != ResizeDirection.None && window.IsResizable)
					{
						_isResizing = true;
						_isDragging = false;
						_dragWindow = window;
						_dragStartPos = point;
						_dragStartWindowPos = new Point(window.Left, window.Top);
						_dragStartWindowSize = new Size(window.Width, window.Height);
						_resizeDirection = resizeDirection;
						return;
					}

					// Check if we're starting a move operation (title bar area)
					if (IsInTitleBar(window, point) && window.IsMovable)
					{
						_isDragging = true;
						_isResizing = false;
						_dragWindow = window;
						_dragStartPos = point;
						_dragStartWindowPos = new Point(window.Left, window.Top);
						_dragStartWindowSize = new Size(0, 0);
						_resizeDirection = ResizeDirection.None;
						return;
					}
				}
			}

			// Handle mouse clicks for window activation and event propagation (when not dragging) - lowest priority
			if (flags.Contains(MouseFlags.Button1Clicked) && !_isDragging && !_isResizing)
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

					HandleWindowClick(window, flags, point);
				}
			}
			
			// Handle other mouse events for active window propagation
			if (_activeWindow != null && !_isDragging && !_isResizing)
			{
				// Check if mouse event is over the active window
				var windowAtPoint = GetWindowAtPoint(point);
				if (windowAtPoint == _activeWindow)
				{
					// Propagate mouse event to the active window
					PropagateMouseEventToWindow(_activeWindow, flags, point);
				}
			}
		}

		private ResizeDirection GetResizeDirection(Window window, Point point)
		{
			// Convert to window-relative coordinates
			var relativePoint = TranslateToRelative(window, point);
			
			// Define resize border thickness
			const int borderThickness = 2;
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
			var relativePoint = TranslateToRelative(window, point);
			
			// Title bar is the top row of the window, but exclude the resize corner areas
			// This prevents conflicts between title bar dragging and corner resizing
			const int cornerSize = 3; // Slightly larger corner exclusion for better UX
			
			// Must be in the top row
			if (relativePoint.Y != 0)
				return false;
			
			// Must be within window bounds
			if (relativePoint.X < 0 || relativePoint.X >= window.Width)
				return false;
			
			// Exclude corner areas where resize handles take precedence
			if (relativePoint.X < cornerSize || relativePoint.X >= window.Width - cornerSize)
				return false;
			
			return true;
		}

		private bool IsOnCloseButton(Window window, Point point)
		{
			if (!window.IsClosable)
				return false;

			var relativePoint = TranslateToRelative(window, point);

			// Close button is at positions [X] which is at width-4, width-3, width-2 (before the corner)
			// Top row only
			if (relativePoint.Y != 0)
				return false;

			// Close button occupies 3 characters: [X] at positions (width-4), (width-3), (width-2)
			// The corner is at (width-1)
			int closeButtonStart = window.Width - 4;
			int closeButtonEnd = window.Width - 2;

			return relativePoint.X >= closeButtonStart && relativePoint.X <= closeButtonEnd;
		}

		private bool IsOnResizeGrip(Window window, Point point)
		{
			if (!window.IsResizable)
				return false;

			var relativePoint = TranslateToRelative(window, point);

			// Resize grip is at bottom-right corner, position (width-2, height-1)
			// The corner is at (width-1, height-1)
			return relativePoint.Y == window.Height - 1 && relativePoint.X == window.Width - 2;
		}

		private void HandleWindowMove(Point currentMousePos)
		{
			if (_dragWindow == null) return;

			// Store the current window bounds before moving
			var oldBounds = new Rectangle(_dragWindow.Left, _dragWindow.Top, _dragWindow.Width, _dragWindow.Height);

			// Calculate the offset from the start position
			int deltaX = currentMousePos.X - _dragStartPos.X;
			int deltaY = currentMousePos.Y - _dragStartPos.Y;

			// Calculate new position
			int newLeft = _dragStartWindowPos.X + deltaX;
			int newTop = _dragStartWindowPos.Y + deltaY;

			// Constrain to desktop bounds
			newLeft = Math.Max(DesktopUpperLeft.X, Math.Min(newLeft, DesktopBottomRight.X - _dragWindow.Width + 1));
			newTop = Math.Max(DesktopUpperLeft.Y, Math.Min(newTop, DesktopBottomRight.Y - _dragWindow.Height + 1));

			// Only update if position actually changed
			if (newLeft != _dragWindow.Left || newTop != _dragWindow.Top)
			{
				// FIRST: Clear the old window position completely
				_renderer.FillRect(_dragWindow.Left, _dragWindow.Top, _dragWindow.Width, _dragWindow.Height,
					Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
				
				// THEN: Apply the new position
				_dragWindow.SetPosition(new Point(newLeft, newTop));
				
				// FINALLY: Force redraw of the window at its new position
				_dragWindow.Invalidate(true);

				// And invalidate exposed regions to redraw any windows that were underneath
				InvalidateExposedRegions(_dragWindow, oldBounds);
			}
		}

		private void HandleWindowResize(Point currentMousePos)
		{
			if (_dragWindow == null) return;

			// Store the current window bounds before resizing
			var oldBounds = new Rectangle(_dragWindow.Left, _dragWindow.Top, _dragWindow.Width, _dragWindow.Height);

			// Calculate the offset from the start position
			int deltaX = currentMousePos.X - _dragStartPos.X;
			int deltaY = currentMousePos.Y - _dragStartPos.Y;

			// Calculate new position and size based on resize direction
			int newLeft = _dragStartWindowPos.X;
			int newTop = _dragStartWindowPos.Y;
			int newWidth = _dragStartWindowSize.Width;
			int newHeight = _dragStartWindowSize.Height;

			switch (_resizeDirection)
			{
				case ResizeDirection.Top:
					newTop = _dragStartWindowPos.Y + deltaY;
					newHeight = _dragStartWindowSize.Height - deltaY;
					break;
				
				case ResizeDirection.Bottom:
					newHeight = _dragStartWindowSize.Height + deltaY;
					break;
				
				case ResizeDirection.Left:
					newLeft = _dragStartWindowPos.X + deltaX;
					newWidth = _dragStartWindowSize.Width - deltaX;
					break;
				
				case ResizeDirection.Right:
					newWidth = _dragStartWindowSize.Width + deltaX;
					break;
				
				case ResizeDirection.TopLeft:
					newLeft = _dragStartWindowPos.X + deltaX;
					newTop = _dragStartWindowPos.Y + deltaY;
					newWidth = _dragStartWindowSize.Width - deltaX;
					newHeight = _dragStartWindowSize.Height - deltaY;
					break;
				
				case ResizeDirection.TopRight:
					newTop = _dragStartWindowPos.Y + deltaY;
					newWidth = _dragStartWindowSize.Width + deltaX;
					newHeight = _dragStartWindowSize.Height - deltaY;
					break;
				
				case ResizeDirection.BottomLeft:
					newLeft = _dragStartWindowPos.X + deltaX;
					newWidth = _dragStartWindowSize.Width - deltaX;
					newHeight = _dragStartWindowSize.Height + deltaY;
					break;
				
				case ResizeDirection.BottomRight:
					newWidth = _dragStartWindowSize.Width + deltaX;
					newHeight = _dragStartWindowSize.Height + deltaY;
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
			if (newLeft != _dragWindow.Left || newTop != _dragWindow.Top || 
				newWidth != _dragWindow.Width || newHeight != _dragWindow.Height)
			{
				// FIRST: Clear the old window position/size completely
				_renderer.FillRect(_dragWindow.Left, _dragWindow.Top, _dragWindow.Width, _dragWindow.Height,
					Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
					
				// THEN: Apply the new position and size
				_dragWindow.SetPosition(new Point(newLeft, newTop));
				_dragWindow.SetSize(newWidth, newHeight);
				
				// FINALLY: Force redraw of the window at its new position and size
				_dragWindow.Invalidate(true);

				// And invalidate exposed regions to redraw any windows that were underneath
				InvalidateExposedRegions(_dragWindow, oldBounds);
			}
		}

		private bool HandleMoveInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Up);
					_activeWindow?.SetPosition(new Point(_activeWindow?.Left ?? 0, Math.Max(0, (_activeWindow?.Top ?? 0) - 1)));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Down);
					_activeWindow?.SetPosition(new Point(_activeWindow?.Left ?? 0, Math.Min(DesktopBottomRight.Y - (_activeWindow?.Height ?? 0) + 1, (_activeWindow?.Top ?? 0) + 1)));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Left);
					_activeWindow?.SetPosition(new Point(Math.Max(DesktopUpperLeft.X, (_activeWindow?.Left ?? 0) - 1), _activeWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Right);
					_activeWindow?.SetPosition(new Point(Math.Min(DesktopBottomRight.X - (_activeWindow?.Width ?? 0) + 1, (_activeWindow?.Left ?? 0) + 1), _activeWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.X:
					CloseWindow(_activeWindow);
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
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Resize, Direction.Up);
					_activeWindow?.SetSize(_activeWindow.Width, Math.Max(1, _activeWindow.Height - 1));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Resize, Direction.Down);
					_activeWindow?.SetSize(_activeWindow.Width, Math.Min(DesktopDimensions.Height - _activeWindow.Top, _activeWindow.Height + 1));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Left);
					_activeWindow?.SetSize(Math.Max(1, _activeWindow.Width - 1), _activeWindow.Height);
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Right);
					_activeWindow?.SetSize(Math.Min(DesktopBottomRight.X - _activeWindow.Left + 1, _activeWindow.Width + 1), _activeWindow.Height);
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
			foreach (var otherWindow in _windows.Values)
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
				Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			// No need for direction-specific clearing since we clear the entire window

			// Redraw the necessary regions that were underneath the window
			foreach (var w in _windows.Values.OrderBy(w => w.ZIndex))
			{
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
			while (_inputQueue.TryDequeue(out var key))
			{
				if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.T)
				{
					CycleActiveWindow();
				}
				else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.Q)
				{
					_exitCode = 0;
					_running = false;
				}
				else if (_activeWindow != null)
				{
					bool handled = _activeWindow.ProcessInput(key);

					if (!handled)
					{
						if ((key.Modifiers & ConsoleModifiers.Shift) != 0 && _activeWindow.IsResizable)
						{
							handled = HandleResizeInput(key);
						}
						else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && _activeWindow.IsMovable)
						{
							handled = HandleMoveInput(key);
						}
						else if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
						{
							handled = HandleAltInput(key);
						}
					}
				}
			}
		}

		private void UpdateCursor()
		{
			if (_activeWindow != null && _activeWindow.HasInteractiveContent(out var cursorPosition))
			{
				var (absoluteLeft, absoluteTop) = TranslateToAbsolute(_activeWindow, new Point(cursorPosition.X, cursorPosition.Y));

				// Check if the cursor position is within the console window boundaries
				// and within the active window's boundaries
				bool isWithinBounds =
					absoluteLeft >= 0 && absoluteLeft < _consoleDriver.ScreenSize.Width &&
					absoluteTop >= 0 && absoluteTop < _consoleDriver.ScreenSize.Height &&
					absoluteLeft >= _activeWindow.Left && absoluteLeft < _activeWindow.Left + _activeWindow.Width &&
					absoluteTop >= _activeWindow.Top && absoluteTop < _activeWindow.Top + _activeWindow.Height;

				if (isWithinBounds)
				{
					// Get the owner control if available
					IWindowControl? ownerControl = null;
					CursorShape cursorShape = CursorShape.Block;

					if (_activeWindow.HasActiveInteractiveContent(out var interactiveContent) &&
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
						ownerWindow: _activeWindow,
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
				if (TopStatus != _cachedTopStatus)
				{
					var topRow = TopStatus;

					var effectiveLength = AnsiConsoleHelper.StripSpectreLength(topRow);
					var paddedTopRow = topRow.PadRight(_consoleDriver.ScreenSize.Width + (topRow.Length - effectiveLength));
					_consoleDriver.WriteToConsole(0, 0, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.TopBarForegroundColor}]{paddedTopRow}[/]", _consoleDriver.ScreenSize.Width, 1, false, Theme.TopBarBackgroundColor, null)[0]);

					_cachedTopStatus = TopStatus;
				}

				var windowsToRender = new HashSet<Window>();

				// Identify dirty windows and overlapping windows
				foreach (var window in _windows.Values)
				{
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

				// Render non-active windows based on their ZIndex
				foreach (var window in _windows.Values.OrderBy(w => w.ZIndex))
				{
					if (window != _activeWindow && windowsToRender.Contains(window))
					{
						_renderer.RenderWindow(window);
					}
				}

				// Check if any of the overlapping windows is overlapping the active window
				if (_activeWindow != null)
				{
					if (windowsToRender.Contains(_activeWindow))
					{
						_renderer.RenderWindow(_activeWindow);
					}
					else
					{
						var overlappingWindows = _renderer.GetOverlappingWindows(_activeWindow);

						foreach (var overlappingWindow in overlappingWindows)
						{
							if (windowsToRender.Contains(overlappingWindow))
							{
								_renderer.RenderWindow(_activeWindow);
							}
						}
					}
				}
			}

			// Filter out sub-windows from the bottom status bar
			var topLevelWindows = _windows.Values
				.Where(w => w.ParentWindow == null)
				.ToList();

			var taskBar = _showTaskBar ? $"{string.Join(" | ", topLevelWindows.Select((w, i) => $"[bold]Alt-{i + 1}[/] {StringHelper.TrimWithEllipsis(w.Title, 15, 7)}"))} | " : string.Empty;

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

			_consoleDriver.Flush();
		}
	}
}