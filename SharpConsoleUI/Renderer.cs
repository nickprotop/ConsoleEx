// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI
{
	/// <summary>
	/// Handles rendering of windows and their content to the console display.
	/// Manages window borders, scrollbars, and content rendering with support for overlapping windows.
	/// </summary>
	public class Renderer
	{
		private ConsoleWindowSystem _consoleWindowSystem;

		// Pooled collections to avoid per-frame allocations on hot paths
		private readonly List<Window> _overlappingWindowsPool = new List<Window>();

		/// <summary>
		/// Initializes a new instance of the <see cref="Renderer"/> class.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system that owns this renderer.</param>
		public Renderer(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;
		}

		/// <summary>
		/// Fills a rectangular area with a specified character and colors.
		/// </summary>
		/// <param name="left">The left coordinate of the rectangle.</param>
		/// <param name="top">The top coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="character">The character to fill the rectangle with.</param>
		/// <param name="backgroundColor">The background color, or null to use the default.</param>
		/// <param name="foregroundColor">The foreground color, or null to use the default.</param>
		public void FillRect(int left, int top, int width, int height, char character, Color? backgroundColor, Color? foregroundColor)
		{
			// Guard against invalid dimensions during rapid console resize
			if (width <= 0 || height <= 0)
				return;

			// Early exit if completely off-screen
			if (left >= _consoleWindowSystem.ConsoleDriver.ScreenSize.Width)
				return;

			if (top >= _consoleWindowSystem.DesktopDimensions.Height)
				return;

			var fg = foregroundColor ?? Color.White;
			var bg = backgroundColor ?? Color.Black;

			for (var y = 0; y < height; y++)
			{
				if (top + y >= _consoleWindowSystem.DesktopDimensions.Height) break;

				// Calculate effective width, ensuring it's non-negative
				int effectiveWidth = Math.Min(width, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width - left);
				if (effectiveWidth <= 0)
					continue;

				_consoleWindowSystem.ConsoleDriver.FillCells(left, top + _consoleWindowSystem.DesktopUpperLeft.Y + y, effectiveWidth, character, fg, bg);
			}
		}

	/// <summary>
	/// Clears a rectangular area with the desktop background.
	/// </summary>
	/// <param name="left">The left coordinate.</param>
	/// <param name="top">The top coordinate.</param>
	/// <param name="width">The width of the area.</param>
	/// <param name="height">The height of the area.</param>
	/// <param name="theme">The theme to use for background colors.</param>
	/// <param name="windows">The collection of windows to invalidate if they overlap.</param>
	public void ClearArea(int left, int top, int width, int height, Themes.ITheme theme, IReadOnlyDictionary<string, Window> windows)
	{
		FillRect(left, top, width, height,
			theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);

		// Invalidate any windows that overlap with this area to redraw them
		foreach (var window in windows.Values)
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
	/// Fills the entire desktop area with the theme's background character and colors.
	/// Used for initializing the desktop background.
	/// </summary>
	/// <param name="theme">The theme to use for desktop colors.</param>
	/// <param name="screenWidth">The width of the screen.</param>
	/// <param name="screenHeight">The height of the screen.</param>
	public void FillDesktopBackground(Themes.ITheme theme, int screenWidth, int screenHeight)
	{
		FillRect(
			0, 0,
			screenWidth,
			screenHeight,
			theme.DesktopBackgroundChar,
			theme.DesktopBackgroundColor,
			theme.DesktopForegroundColor);
	}

		/// <summary>
		/// Gets the rectangular regions where two windows overlap.
		/// </summary>
		/// <param name="window1">The first window.</param>
		/// <param name="window2">The second window.</param>
		/// <returns>A list of rectangles representing the overlapping areas. Empty if windows do not overlap.</returns>
		public List<Rectangle> GetOverlappingRegions(Window window1, Window window2)
		{
			var overlappingRegions = new List<Rectangle>();

			int left = Math.Max(window1.Left, window2.Left);
			int top = Math.Max(window1.Top, window2.Top);
			int right = Math.Min(window1.Left + window1.Width, window2.Left + window2.Width);
			int bottom = Math.Min(window1.Top + window1.Height, window2.Top + window2.Height);

			if (left < right && top < bottom)
			{
				overlappingRegions.Add(new Rectangle(left, top, right - left, bottom - top));
			}

			return overlappingRegions;
		}

		/// <summary>
		/// Gets all windows that overlap with the specified window using iterative BFS.
		/// This avoids unbounded recursion and is more efficient than the recursive approach.
		/// </summary>
		/// <param name="window">The window to check for overlaps.</param>
		/// <param name="visited">Optional set of already visited windows (ignored, kept for compatibility).</param>
		/// <returns>A set of all windows that form an overlapping chain with the specified window.</returns>
		public HashSet<Window> GetOverlappingWindows(Window window, HashSet<Window>? visited = null)
		{
			var result = new HashSet<Window>();
			var queue = new Queue<Window>();
			queue.Enqueue(window);

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				if (!result.Add(current))
					continue;

				foreach (var other in _consoleWindowSystem.Windows.Values)
				{
					// Skip minimized windows - they're invisible and don't overlap
					if (other.State == WindowState.Minimized)
						continue;

					if (result.Contains(other))
						continue;

					if (IsOverlapping(current, other))
					{
						queue.Enqueue(other);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Determines whether two windows overlap each other.
		/// </summary>
		/// <param name="window1">The first window.</param>
		/// <param name="window2">The second window.</param>
		/// <returns>True if the windows overlap; otherwise, false.</returns>
		public bool IsOverlapping(Window window1, Window window2)
		{
			return window1.Left < window2.Left + window2.Width &&
				   window1.Left + window1.Width > window2.Left &&
				   window1.Top < window2.Top + window2.Height &&
				   window1.Top + window1.Height > window2.Top;
		}

		/// <summary>
		/// Renders a specific region of a window. Used for partial window updates.
		/// </summary>
		/// <param name="window">The window to render.</param>
		/// <param name="region">The rectangular region to render.</param>
		public void RenderRegion(Window window, Rectangle region)
		{
			// Skip rendering entirely for minimized windows
			if (window.State == WindowState.Minimized)
			{
				return;
			}
		
		// Skip screen output if render lock is enabled, but still do internal work
		if (window.RenderLock)
		{
			// Trigger internal work if dirty (keeps CharacterBuffer up-to-date)
			if (window.IsDirty)
			{
				// Create dummy region covering entire window for internal rendering
				var fullRegion = new List<Rectangle> 
				{ 
					new Rectangle(window.Left, window.Top, window.Width, window.Height) 
				};
				
				// Trigger internal measure/layout/paint but don't output to screen
				window.EnsureContentReady(fullRegion);
				window.IsDirty = false;
			}
			
			return;  // Don't output to screen
		}

			var visibleRegions = new List<Rectangle> { region };

			// Fill the background only for the visible regions
			foreach (var visibleRegion in visibleRegions)
			{
				FillRect(visibleRegion.Left, visibleRegion.Top, visibleRegion.Width, visibleRegion.Height, ' ', window.BackgroundColor, null);
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			window.BorderRenderer?.RenderBorders(visibleRegions);

			// Optimized path: rebuild buffer only and render cells directly
			var buffer = window.EnsureContentReady(visibleRegions);
			window.IsDirty = false;

			if (buffer != null)
			{
				RenderVisibleWindowContentFromBuffer(window, buffer, visibleRegions);
			}
		}

		/// <summary>
		/// Renders a complete window including borders, content, and scrollbars.
		/// Handles visibility calculations for overlapping windows.
		/// </summary>
		/// <param name="window">The window to render.</param>
		public void RenderWindow(Window window)
		{
			// Skip rendering entirely for minimized windows
			if (window.State == WindowState.Minimized)
			{
				return;
			}
		
		// Skip screen output if render lock is enabled, but still do internal work
		if (window.RenderLock)
		{
			// Trigger internal work if dirty (keeps CharacterBuffer up-to-date)
			if (window.IsDirty)
			{
				// Create dummy region covering entire window for internal rendering
				var fullRegion = new List<Rectangle> 
				{ 
					new Rectangle(window.Left, window.Top, window.Width, window.Height) 
				};
				
				// Trigger internal measure/layout/paint but don't output to screen
				window.EnsureContentReady(fullRegion);
				window.IsDirty = false;
			}
			
			return;  // Don't output to screen
		}



			Point desktopTopLeftCorner = _consoleWindowSystem.DesktopUpperLeft;
			Point desktopBottomRightCorner = _consoleWindowSystem.DesktopBottomRight;

			if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
			{
				return;
			}

			// Get all windows that potentially overlap with this window
			// Exclude minimized windows - they're invisible and shouldn't block rendering
			_overlappingWindowsPool.Clear();
			foreach (var w in _consoleWindowSystem.Windows.Values)
			{
				if (w != window &&
				    w.ZIndex > window.ZIndex &&
				    w.State != WindowState.Minimized &&
				    IsOverlapping(window, w))
				{
					_overlappingWindowsPool.Add(w);
				}
			}
			_overlappingWindowsPool.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

			// Calculate visible regions
			var visibleRegions = _consoleWindowSystem.VisibleRegions.CalculateVisibleRegions(window, _overlappingWindowsPool);

			if (visibleRegions.Count == 0)
			{
				// Window is completely covered - skip rendering but keep dirty.
				// When the covering window moves away and exposes this window,
				// InvalidateExposedRegions will trigger a re-render where visible regions will exist.
				return;
			}

			// Fill the background only for the visible regions
			// Can be disabled via ClearDestinationOnWindowMove option to reduce flicker during moves
			if (_consoleWindowSystem.Options.ClearDestinationOnWindowMove)
			{
				foreach (var region in visibleRegions)
				{
					FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
				}
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			window.BorderRenderer?.RenderBorders(visibleRegions);

			// Optimized path: rebuild buffer only (no ANSI serialization) and render cells directly
			var buffer = window.EnsureContentReady(visibleRegions);
			window.IsDirty = false;

			if (buffer != null)
			{
				RenderVisibleWindowContentFromBuffer(window, buffer, visibleRegions);
			}
		}


		private bool IsWindowOutOfBounds(Window window, Point desktopTopLeftCorner, Point desktopBottomRightCorner)
		{
			return window.Left < desktopTopLeftCorner.X || (window.Top + _consoleWindowSystem.DesktopUpperLeft.Y) < desktopTopLeftCorner.Y ||
				   window.Left >= desktopBottomRightCorner.X || window.Top + _consoleWindowSystem.DesktopUpperLeft.Y >= desktopBottomRightCorner.Y;
		}

		/// <summary>
		/// Renders visible window content by copying cells directly from the CharacterBuffer
		/// to the console driver, bypassing ANSI string serialization and parsing.
		/// </summary>
		private void RenderVisibleWindowContentFromBuffer(Window window, CharacterBuffer buffer, List<Rectangle> visibleRegions)
		{
			var windowLeft = window.Left;
			var windowTop = window.Top;
			var windowWidth = window.Width;
			var desktopUpperLeftY = _consoleWindowSystem.DesktopUpperLeft.Y;
			var bufferHeight = buffer.Height;

			for (var y = 0; y < bufferHeight; y++)
			{
				// Skip if this line is outside the desktop area
				if (windowTop + y >= _consoleWindowSystem.DesktopBottomRight.Y) break;

				// Check if this line is in any visible region
				foreach (var region in visibleRegions)
				{
					// Check if this line falls within the current region's vertical bounds
					if (window.Top + y + 1 >= region.Top && window.Top + y + 1 < region.Top + region.Height)
					{
						// Calculate content boundaries within the window
						int contentLeft = Math.Max(windowLeft + 1, region.Left);
						int contentRight = Math.Min(windowLeft + windowWidth - 1, region.Left + region.Width);
						int contentWidth = contentRight - contentLeft;

						if (contentWidth <= 0) continue;

						// Calculate the portion of the buffer to render
						int startOffset = Math.Max(0, contentLeft - (windowLeft + 1));

						// Write cells directly from buffer to console driver
						_consoleWindowSystem.ConsoleDriver.WriteBufferRegion(
							contentLeft,
							windowTop + desktopUpperLeftY + y + 1,
							buffer,
							startOffset,
							y,
							contentWidth,
							window.BackgroundColor);
					}
				}
			}
		}

	}
}