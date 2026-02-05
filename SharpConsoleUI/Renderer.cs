// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Windows;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI
{
	/// <summary>
	/// Handles rendering of windows and their content to the console display.
	/// Manages window borders, scrollbars, and content rendering with support for overlapping windows.
	/// </summary>
	public class Renderer
	{
		private ConsoleWindowSystem _consoleWindowSystem;

		// Performance optimization: cache fill strings to avoid repeated string allocations
		private readonly Dictionary<(char, int), string> _fillStringCache = new Dictionary<(char, int), string>();


		// ===== FIX TOGGLES =====
		// FIX11: Prevent ANSI doubling by not passing foregroundColor when markup already has color tags
		private const bool FIX11_NO_FOREGROUND_IN_MARKUP = true;
		/// <summary>
		/// Initializes a new instance of the <see cref="Renderer"/> class.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system that owns this renderer.</param>
		public Renderer(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;
		}

		/// <summary>
		/// Gets a cached fill string or creates one if not in cache.
		/// Cache is limited to 100 entries to prevent memory leak.
		/// </summary>
		private string GetFillString(char character, int width)
		{
			var key = (character, width);
			if (_fillStringCache.TryGetValue(key, out string? cached))
				return cached;

			// Limit cache size to prevent memory leak
			if (_fillStringCache.Count > 100)
				_fillStringCache.Clear();

			string result = new string(character, width);
			_fillStringCache[key] = result;
			return result;
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

			for (var y = 0; y < height; y++)
			{
				if (top + y >= _consoleWindowSystem.DesktopDimensions.Height) break;

				// Calculate effective width, ensuring it's non-negative
				int effectiveWidth = Math.Min(width, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width - left);
				if (effectiveWidth <= 0)
					continue;

				_consoleWindowSystem.ConsoleDriver.WriteToConsole(left, top + _consoleWindowSystem.DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(GetFillString(character, effectiveWidth), effectiveWidth, 1, false, backgroundColor, foregroundColor)[0]);
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
				
				// This calls window's internal measure/layout/paint but we discard the output
				window.RenderAndGetVisibleContent(fullRegion);
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

			// Pass visible regions to window rendering so it only paints visible areas (optimization)
			// OPTION C FIX: Force rebuild for exposed regions to ensure cache contains correct content
			// Exposed regions may request areas that weren't in the previous cache (stale regional cache bug)
			window.Invalidate(true);

			var lines = window.RenderAndGetVisibleContent(visibleRegions);
			window.IsDirty = false;

			// Render content only for visible parts
			RenderVisibleWindowContent(window, lines, visibleRegions);
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
				
				// This calls window's internal measure/layout/paint but we discard the output
				window.RenderAndGetVisibleContent(fullRegion);
				window.IsDirty = false;
			}
			
			return;  // Don't output to screen
		}
			// Special rendering for OverlayWindow
			if (window is OverlayWindow overlay)
			{
				RenderOverlayWindow(overlay);
				return;
			}


			Point desktopTopLeftCorner = _consoleWindowSystem.DesktopUpperLeft;
			Point desktopBottomRightCorner = _consoleWindowSystem.DesktopBottomRight;

			if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
			{
				return;
			}

			// Get all windows that potentially overlap with this window
			// Exclude minimized windows - they're invisible and shouldn't block rendering
			var overlappingWindows = _consoleWindowSystem.Windows.Values
				.Where(w => w != window &&
				            w.ZIndex > window.ZIndex &&
				            w.State != WindowState.Minimized &&
				            IsOverlapping(window, w))
				.OrderBy(w => w.ZIndex)
				.ToList();

			// Calculate visible regions
			var visibleRegions = _consoleWindowSystem.VisibleRegions.CalculateVisibleRegions(window, overlappingWindows);

			if (!visibleRegions.Any())
			{
				// Window is completely covered - skip rendering but keep dirty.
				// When the covering window moves away and exposes this window,
				// InvalidateExposedRegions will trigger a re-render where visible regions will exist.
				return;
			}

			// Fill the background only for the visible regions
			foreach (var region in visibleRegions)
			{
				FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			window.BorderRenderer?.RenderBorders(visibleRegions);

			// Pass visible regions to window rendering so it only paints visible areas (optimization)
			var lines = window.RenderAndGetVisibleContent(visibleRegions);
			window.IsDirty = false;

			// Render content only for visible parts
			RenderVisibleWindowContent(window, lines, visibleRegions);
		}

		/// <summary>
		/// Special rendering for OverlayWindow that allows underlying windows to show through.
		/// Renders only the regions where controls exist, letting underlying content show through
		/// in areas without controls (true transparency effect).
		/// NOTE: This method does NOT call DrawWindowBorders() - no border space is rendered.
		/// OverlayWindow compensates by offsetting position and dimensions to account for missing borders.
		/// </summary>
		private void RenderOverlayWindow(OverlayWindow overlay)
		{
			// Get all windows BELOW the overlay (lower Z-index), ordered by z-index
			var underlyingWindows = _consoleWindowSystem.Windows.Values
				.Where(w => w != overlay &&
				            !(w is OverlayWindow) &&  // Skip other overlays
				            w.ZIndex < overlay.ZIndex &&
				            w.State != WindowState.Minimized)
				.OrderBy(w => w.ZIndex)
				.ToList();

			// First, fill the entire desktop area with the desktop background to clear any previous overlay content.
			// This ensures areas not covered by windows are properly cleared.
			var desktopDims = _consoleWindowSystem.DesktopDimensions;
			var desktopUpperLeftY = _consoleWindowSystem.DesktopUpperLeft.Y;
			var theme = _consoleWindowSystem.Theme;
			FillRect(0, desktopUpperLeftY, desktopDims.Width, desktopDims.Height,
				theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);

			// Render all underlying windows in z-order.
			// Each window renders fully within its bounds (ignoring the overlay).
			foreach (var underlyingWindow in underlyingWindows)
			{
				RenderWindowForOverlay(underlyingWindow, underlyingWindows);
			}

			// Trigger overlay content rebuild (populates buffer and performs layout)
			var lines = overlay.RenderAndGetVisibleContent();
			overlay.IsDirty = false;

			// Get control bounds - these are the only regions we need to render
			var controlBounds = overlay.GetControlBounds();

			if (controlBounds.Count == 0)
			{
				return;
			}

			// Calculate visible regions (areas not covered by higher z-index windows)
			var overlappingWindows = _consoleWindowSystem.Windows.Values
				.Where(w => w != overlay &&
				            w.ZIndex > overlay.ZIndex &&
				            w.State != WindowState.Minimized &&
				            IsOverlapping(overlay, w))
				.OrderBy(w => w.ZIndex)
				.ToList();

			var visibleRegions = _consoleWindowSystem.VisibleRegions.CalculateVisibleRegions(overlay, overlappingWindows);

			// Render only the control regions from the buffer
			RenderOverlayControlRegions(overlay, lines, controlBounds, visibleRegions);
		}

		/// <summary>
		/// Renders a window for overlay transparency purposes.
		/// Calculates visible regions considering only other underlying windows (not overlays).
		/// </summary>
		private void RenderWindowForOverlay(Window window, List<Window> allUnderlyingWindows)
		{
			if (window.State == WindowState.Minimized)
				return;

			Point desktopTopLeftCorner = _consoleWindowSystem.DesktopUpperLeft;
			Point desktopBottomRightCorner = _consoleWindowSystem.DesktopBottomRight;

			if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
				return;

			// Get overlapping windows from the underlying windows list only (higher z-index among underlying)
			var overlappingWindows = allUnderlyingWindows
				.Where(w => w != window &&
				            w.ZIndex > window.ZIndex &&
				            IsOverlapping(window, w))
				.OrderBy(w => w.ZIndex)
				.ToList();

			var visibleRegions = _consoleWindowSystem.VisibleRegions.CalculateVisibleRegions(window, overlappingWindows);

			if (!visibleRegions.Any())
			{
				window.IsDirty = false;
				return;
			}

			// Fill the background only for the visible regions
			foreach (var region in visibleRegions)
			{
				FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
			}

			window.BorderRenderer?.RenderBorders(visibleRegions);

			var windowLines = window.RenderAndGetVisibleContent(visibleRegions);
			window.IsDirty = false;

			RenderVisibleWindowContent(window, windowLines, visibleRegions);
		}


		/// <summary>
		/// Renders only the specified control regions from the overlay buffer.
		/// Areas without controls are not rendered, allowing underlying content to show through.
		/// </summary>
		private void RenderOverlayControlRegions(
			OverlayWindow overlay,
			List<string> lines,
			List<LayoutRect> controlBounds,
			List<Rectangle> visibleRegions)
		{
			var windowLeft = overlay.Left;
			var windowTop = overlay.Top;
			var desktopUpperLeftY = _consoleWindowSystem.DesktopUpperLeft.Y;

			// For each control bounds, render that portion of the buffer
			foreach (var bounds in controlBounds)
			{
				// Render each line within the control bounds
				for (int localY = 0; localY < bounds.Height; localY++)
				{
					int bufferY = bounds.Y + localY;

					// Skip if outside buffer
					if (bufferY < 0 || bufferY >= lines.Count)
					{
						continue;
					}

					// Check if outside desktop area (use same calculation as RenderVisibleWindowContent)
					// windowTop + bufferY is the window-relative Y, compare to DesktopBottomRight.Y
					if (windowTop + bufferY >= _consoleWindowSystem.DesktopBottomRight.Y)
					{
						break;
					}

					// Screen Y for rendering: window content starts at windowTop + 1 (border offset)
					// Plus desktopUpperLeftY for status bar
					int screenY = windowTop + 1 + bufferY;

					var line = lines[bufferY];

					// Check each visible region to see if this line intersects
					foreach (var region in visibleRegions)
					{
						// Visible regions are in screen coordinates (include desktopUpperLeftY)
						// Check vertical intersection
						int screenYWithOffset = screenY + desktopUpperLeftY;
						if (screenYWithOffset < region.Top || screenYWithOffset >= region.Top + region.Height)
						{
							continue;
						}

						// Calculate horizontal bounds for this control
						// Control X in buffer space, convert to screen space
						int controlScreenLeft = windowLeft + 1 + bounds.X;
						int controlScreenRight = controlScreenLeft + bounds.Width;

						// Intersect with visible region horizontally
						int renderLeft = Math.Max(controlScreenLeft, region.Left);
						int renderRight = Math.Min(controlScreenRight, region.Left + region.Width);
						int renderWidth = renderRight - renderLeft;

						if (renderWidth <= 0)
						{
							continue;
						}

						// Calculate offset within the line to extract
						int lineOffset = renderLeft - (windowLeft + 1);

						// Get the substring to render
						string portion = AnsiConsoleHelper.SubstringAnsiWithPadding(
							line, lineOffset, renderWidth, overlay.BackgroundColor);

						// Write to console
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(
							renderLeft, screenYWithOffset, portion);
					}
				}
			}
		}

		/// <summary>
		/// Normal window rendering (extracted from RenderWindow for reuse).
		/// </summary>
		private void RenderNormalWindow(Window window)
		{
			if (window.State == WindowState.Minimized)
			{
				return;
			}

			// Skip OverlayWindow (handled separately)
			if (window is OverlayWindow)
			{
				return;
			}

			Point desktopTopLeftCorner = _consoleWindowSystem.DesktopUpperLeft;
			Point desktopBottomRightCorner = _consoleWindowSystem.DesktopBottomRight;

			if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
			{
				return;
			}

			// Get all windows that potentially overlap with this window
			// Exclude minimized windows - they're invisible and shouldn't block rendering
			var overlappingWindows = _consoleWindowSystem.Windows.Values
				.Where(w => w != window &&
				            w.ZIndex > window.ZIndex &&
				            w.State != WindowState.Minimized &&
				            IsOverlapping(window, w))
				.OrderBy(w => w.ZIndex)
				.ToList();

			// Calculate visible regions
			var visibleRegions = _consoleWindowSystem.VisibleRegions.CalculateVisibleRegions(window, overlappingWindows);

			if (!visibleRegions.Any())
			{
				// Window is completely covered - skip rendering but keep dirty.
				// When the covering window moves away and exposes this window,
				// InvalidateExposedRegions will trigger a re-render where visible regions will exist.
				return;
			}

			// Fill the background only for the visible regions
			foreach (var region in visibleRegions)
			{
				FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			window.BorderRenderer?.RenderBorders(visibleRegions);

			// Pass visible regions to window rendering so it only paints visible areas (optimization)
			var lines = window.RenderAndGetVisibleContent(visibleRegions);
			window.IsDirty = false;

			// Render content only for visible parts
			RenderVisibleWindowContent(window, lines, visibleRegions);
		}

		private bool IsWindowOutOfBounds(Window window, Point desktopTopLeftCorner, Point desktopBottomRightCorner)
		{
			return window.Left < desktopTopLeftCorner.X || (window.Top + _consoleWindowSystem.DesktopUpperLeft.Y) < desktopTopLeftCorner.Y ||
				   window.Left >= desktopBottomRightCorner.X || window.Top + _consoleWindowSystem.DesktopUpperLeft.Y >= desktopBottomRightCorner.Y;
		}

		private void RenderVisibleWindowContent(Window window, List<string> lines, List<Rectangle> visibleRegions)
		{
			var screenWidth = _consoleWindowSystem.ConsoleDriver.ScreenSize.Width;
			var screenHeight = _consoleWindowSystem.ConsoleDriver.ScreenSize.Height;
			var windowLeft = window.Left;
			var windowTop = window.Top;
			var windowWidth = window.Width;
			var desktopUpperLeftY = _consoleWindowSystem.DesktopUpperLeft.Y;

			for (var y = 0; y < lines.Count; y++)
			{
				// Skip if this line is outside the desktop area
				if (windowTop + y >= _consoleWindowSystem.DesktopBottomRight.Y) break;

				// Get the current line
				var line = lines[y];

				// Check if this line is in any visible region
				foreach (var region in visibleRegions)
				{
					// Check if this line falls within the current region's vertical bounds
					if (window.Top + y + 1 >= region.Top && window.Top + y + 1 < region.Top + region.Height)
					{
						// Calculate content boundaries within the window
						// Content area is between left border (windowLeft + 1) and right border (windowLeft + windowWidth - 1)
						// Content right boundary (exclusive) should be windowLeft + windowWidth - 1
						int contentLeft = Math.Max(windowLeft + 1, region.Left);
						int contentRight = Math.Min(windowLeft + windowWidth - 1, region.Left + region.Width);
						int contentWidth = contentRight - contentLeft;

						if (contentWidth <= 0) continue;

						// Calculate the portion of the line to render
						int startOffset = contentLeft - (windowLeft + 1);
						startOffset = Math.Max(0, startOffset);

						// Get the substring of the line to render, padding to full width if shorter
						string visiblePortion = AnsiConsoleHelper.SubstringAnsiWithPadding(line, startOffset, contentWidth, window.BackgroundColor);

						// Write the visible portion to the console
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(contentLeft, windowTop + desktopUpperLeftY + y + 1, visiblePortion);
					}
				}
			}
		}
	}
}