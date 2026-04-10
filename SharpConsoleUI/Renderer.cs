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
using System.Text;

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

		// Pooled list for alpha compositing — sorted descending by ZIndex
		private readonly List<Window> _compositingWindowsPool = new List<Window>();

		// Scratch buffer for compositing transparent window rows (reused across frames)
		private CharacterBuffer? _scratchBuffer;

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
		BlitDesktopRegion(left, top, width, height, theme);

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
		var service = _consoleWindowSystem.DesktopBackgroundService;
		var desktopDims = _consoleWindowSystem.DesktopDimensions;
		service.Render(screenWidth, desktopDims.Height);

		if (service.HasBuffer)
		{
			var desktopY = _consoleWindowSystem.DesktopUpperLeft.Y;
			var blitHeight = Math.Min(desktopDims.Height, screenHeight);
			for (int y = 0; y < blitHeight; y++)
			{
				int effectiveWidth = Math.Min(screenWidth, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width);
				if (effectiveWidth <= 0) continue;
				_consoleWindowSystem.ConsoleDriver.WriteBufferRegion(
					0, y + desktopY, service.Buffer!, 0, y, effectiveWidth,
					theme.DesktopBackgroundColor);
			}
		}
		else
		{
			FillRect(0, 0, screenWidth, screenHeight,
				theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);
		}
	}

	/// <summary>
	/// Blits a rectangular desktop region from the cached background buffer to the console driver.
	/// Falls back to flat fill if the cached buffer is not available.
	/// Coordinates are in desktop-relative space (same as FillRect).
	/// </summary>
	/// <param name="left">Left coordinate in desktop space.</param>
	/// <param name="top">Top coordinate in desktop space.</param>
	/// <param name="width">Width of the region.</param>
	/// <param name="height">Height of the region.</param>
	/// <param name="theme">Theme for fallback colors.</param>
	public void BlitDesktopRegion(int left, int top, int width, int height, Themes.ITheme theme)
	{
		if (width <= 0 || height <= 0)
			return;

		var service = _consoleWindowSystem.DesktopBackgroundService;

		if (service.HasBuffer)
		{
			var desktopY = _consoleWindowSystem.DesktopUpperLeft.Y;
			var desktopHeight = _consoleWindowSystem.DesktopDimensions.Height;
			var screenWidth = _consoleWindowSystem.ConsoleDriver.ScreenSize.Width;

			for (int y = 0; y < height; y++)
			{
				if (top + y >= desktopHeight) break;

				int effectiveWidth = Math.Min(width, screenWidth - left);
				if (effectiveWidth <= 0) continue;

				// Clamp source coordinates to buffer bounds
				if (left < 0 || top + y < 0) continue;
				if (left >= service.Buffer!.Width || top + y >= service.Buffer.Height) continue;
				effectiveWidth = Math.Min(effectiveWidth, service.Buffer.Width - left);

				_consoleWindowSystem.ConsoleDriver.WriteBufferRegion(
					left, top + y + desktopY, service.Buffer, left, top + y, effectiveWidth,
					theme.DesktopBackgroundColor);
			}
		}
		else
		{
			FillRect(left, top, width, height,
				theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);
		}
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
			// Skip for transparent windows — compositing path handles each cell
			if (window.BackgroundColor.A == 255)
			{
				foreach (var visibleRegion in visibleRegions)
				{
					FillRect(visibleRegion.Left, visibleRegion.Top, visibleRegion.Width, visibleRegion.Height, ' ', window.BackgroundColor, null);
				}
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			window.BorderRenderer?.RenderBorders(visibleRegions);

			// Composite border cells for transparent windows
			if (window.BackgroundColor.A < 255)
				CompositeBorders(window, visibleRegions);

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
			// Skip for transparent windows — compositing path handles each cell
			if (_consoleWindowSystem.Options.ClearDestinationOnWindowMove && window.BackgroundColor.A == 255)
			{
				foreach (var region in visibleRegions)
				{
					FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
				}
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			window.BorderRenderer?.RenderBorders(visibleRegions);

			// Composite border cells for transparent windows
			if (window.BackgroundColor.A < 255)
				CompositeBorders(window, visibleRegions);

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

			// Fast path: fully opaque window — no compositing needed
			if (window.BackgroundColor.A == 255)
			{
				RenderVisibleWindowContentOpaque(window, buffer, visibleRegions, windowLeft, windowTop, windowWidth, desktopUpperLeftY, bufferHeight);
				return;
			}

			// Compositing path: window has semi-transparent background (Mica-style)
			RenderVisibleWindowContentComposited(window, buffer, visibleRegions, windowLeft, windowTop, windowWidth, desktopUpperLeftY, bufferHeight);
		}

		/// <summary>
		/// Fast path for fully opaque windows — direct buffer-to-driver copy with no compositing.
		/// </summary>
		private void RenderVisibleWindowContentOpaque(Window window, CharacterBuffer buffer, List<Rectangle> visibleRegions,
			int windowLeft, int windowTop, int windowWidth, int desktopUpperLeftY, int bufferHeight)
		{
			for (var y = 0; y < bufferHeight; y++)
			{
				if (windowTop + y >= _consoleWindowSystem.DesktopBottomRight.Y) break;

				foreach (var region in visibleRegions)
				{
					if (window.Top + y + 1 >= region.Top && window.Top + y + 1 < region.Top + region.Height)
					{
						int contentLeft = Math.Max(windowLeft + 1, region.Left);
						int contentRight = Math.Min(windowLeft + windowWidth - 1, region.Left + region.Width);
						int contentWidth = contentRight - contentLeft;

						if (contentWidth <= 0) continue;

						int startOffset = Math.Max(0, contentLeft - (windowLeft + 1));

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

		/// <summary>
		/// Compositing path for semi-transparent windows. Resolves each cell against
		/// the windows below and desktop background for Mica-style transparency.
		/// </summary>
		private void RenderVisibleWindowContentComposited(Window window, CharacterBuffer buffer, List<Rectangle> visibleRegions,
			int windowLeft, int windowTop, int windowWidth, int desktopUpperLeftY, int bufferHeight)
		{
			// Build sorted windows list (descending Z-order) once for this window render
			_compositingWindowsPool.Clear();
			foreach (var w in _consoleWindowSystem.Windows.Values)
			{
				if (w != window && w.State != WindowState.Minimized)
					_compositingWindowsPool.Add(w);
			}
			_compositingWindowsPool.Sort((a, b) => b.ZIndex.CompareTo(a.ZIndex)); // descending

			// Ensure scratch buffer is large enough for the widest row
			int scratchWidth = windowWidth;
			if (_scratchBuffer == null || _scratchBuffer.Width < scratchWidth)
			{
				_scratchBuffer = new CharacterBuffer(scratchWidth, 1);
			}

			for (var y = 0; y < bufferHeight; y++)
			{
				if (windowTop + y >= _consoleWindowSystem.DesktopBottomRight.Y) break;

				foreach (var region in visibleRegions)
				{
					if (window.Top + y + 1 >= region.Top && window.Top + y + 1 < region.Top + region.Height)
					{
						int contentLeft = Math.Max(windowLeft + 1, region.Left);
						int contentRight = Math.Min(windowLeft + windowWidth - 1, region.Left + region.Width);
						int contentWidth = contentRight - contentLeft;

						if (contentWidth <= 0) continue;

						int startOffset = Math.Max(0, contentLeft - (windowLeft + 1));

						// Resize scratch buffer if needed (width might have changed)
						if (_scratchBuffer.Width < contentWidth)
						{
							_scratchBuffer = new CharacterBuffer(contentWidth, 1);
						}

						// Composite each cell in this row segment
						for (int i = 0; i < contentWidth; i++)
						{
							int bufX = startOffset + i;
							var cell = buffer.GetCell(bufX, y);
							int screenX = contentLeft + i;
							int screenY = window.Top + y + 1;

							if (cell.Background.A == 255)
							{
								// Cell is fully opaque — no compositing needed
								_scratchBuffer.SetCellDirect(i, 0, cell);
							}
							else
							{
								// Resolve the cell below at this screen position
								var cellBelow = ResolveCellBelow(screenX, screenY, window.ZIndex, _compositingWindowsPool, 0);
								var brush = window.TransparencyBrush;

								// Compute effective background from below — block chars use fg as visual bg
								var effectiveBgBelow = IsBlockCharacter(cellBelow.Character)
									? cellBelow.Foreground : cellBelow.Background;

								Cell composited;
								if (brush != null)
								{
									composited = CompositeWithBrush(cell, cellBelow, effectiveBgBelow, brush);
								}
								else
								{
									// Default: true transparency — bg at raw alpha, character bubble-up
									// with parabolic fg fade
									composited = CompositeDefault(cell, cellBelow, effectiveBgBelow);
								}

								_scratchBuffer.SetCellDirect(i, 0, composited);
							}
						}

						// Write the composited row to the driver
						_consoleWindowSystem.ConsoleDriver.WriteBufferRegion(
							contentLeft,
							windowTop + desktopUpperLeftY + y + 1,
							_scratchBuffer,
							0,
							0,
							contentWidth,
							window.BackgroundColor);
					}
				}
			}
		}

		/// <summary>
		/// Re-renders border cells of a transparent window with composited backgrounds.
		/// Called after BorderRenderer.RenderBorders has written un-composited borders.
		/// Reads characters/foreground from the cached border buffers, computes fresh
		/// composited backgrounds, and overwrites the driver cells.
		/// </summary>
		private void CompositeBorders(Window window, List<Rectangle> visibleRegions)
		{
			var borderRenderer = window.BorderRenderer;
			if (borderRenderer == null)
				return;

			var driver = _consoleWindowSystem.ConsoleDriver;
			var desktopUpperLeftY = _consoleWindowSystem.DesktopUpperLeft.Y;
			var windowBg = window.BackgroundColor;

			// Build compositing windows pool for this window
			_compositingWindowsPool.Clear();
			foreach (var w in _consoleWindowSystem.Windows.Values)
			{
				if (w != window && w.State != WindowState.Minimized)
					_compositingWindowsPool.Add(w);
			}
			_compositingWindowsPool.Sort((a, b) => b.ZIndex.CompareTo(a.ZIndex));

			var cachedTop = borderRenderer._cachedTopBorder;
			var cachedBottom = borderRenderer._cachedBottomBorder;

			foreach (var region in visibleRegions)
			{
				// Top border
				if (region.Top == window.Top && cachedTop != null)
				{
					int borderStartX = Math.Max(region.Left, window.Left);
					int borderEndX = Math.Min(region.Left + region.Width, window.Left + window.Width);
					for (int x = borderStartX; x < borderEndX; x++)
					{
						int srcX = x - window.Left;
						if (srcX < 0 || srcX >= cachedTop.Width) continue;

						var cachedCell = cachedTop.GetCell(srcX, 0);
						var cellBelow = ResolveCellBelow(x, window.Top, window.ZIndex, _compositingWindowsPool, 0);
						var resolvedBg = Color.Blend(windowBg, cellBelow.Background);
						var resolvedFg = Color.Blend(cachedCell.Foreground, resolvedBg);

						driver.SetNarrowCell(x, window.Top + desktopUpperLeftY,
							(char)cachedCell.Character.Value, resolvedFg, resolvedBg);
					}
				}

				// Bottom border
				int bottomY = window.Top + window.Height - 1;
				if (region.Top + region.Height == window.Top + window.Height && cachedBottom != null)
				{
					int borderStartX = Math.Max(region.Left, window.Left);
					int borderEndX = Math.Min(region.Left + region.Width, window.Left + window.Width);
					for (int x = borderStartX; x < borderEndX; x++)
					{
						int srcX = x - window.Left;
						if (srcX < 0 || srcX >= cachedBottom.Width) continue;

						var cachedCell = cachedBottom.GetCell(srcX, 0);
						var cellBelow = ResolveCellBelow(x, bottomY, window.ZIndex, _compositingWindowsPool, 0);
						var resolvedBg = Color.Blend(windowBg, cellBelow.Background);
						var resolvedFg = Color.Blend(cachedCell.Foreground, resolvedBg);

						driver.SetNarrowCell(x, bottomY + desktopUpperLeftY,
							(char)cachedCell.Character.Value, resolvedFg, resolvedBg);
					}
				}
			}

			// Vertical borders (left and right columns)
			bool isActive = window.GetIsActive();
			var borderFg = isActive ? window.ActiveBorderForegroundColor : window.InactiveBorderForegroundColor;

			for (int y = 1; y < window.Height - 1; y++)
			{
				int screenY = window.Top + y;
				if (screenY + desktopUpperLeftY >= _consoleWindowSystem.DesktopBottomRight.Y) break;

				foreach (var region in visibleRegions)
				{
					if (screenY >= region.Top && screenY < region.Top + region.Height)
					{
						// Left border
						if (window.Left >= region.Left && window.Left < region.Left + region.Width)
						{
							var cellBelow = ResolveCellBelow(window.Left, screenY, window.ZIndex, _compositingWindowsPool, 0);
							var resolvedBg = Color.Blend(windowBg, cellBelow.Background);
							var resolvedFg = Color.Blend(borderFg, resolvedBg);

							// Read back the character that was written (scrollbar, border char, etc.)
							// by re-reading from what BorderRenderer wrote
							// For left border, it's always the vertical border char
							var chars = SharpConsoleUI.Drawing.BoxChars.FromBorderStyle(window.BorderStyle, isActive);
							driver.SetNarrowCell(window.Left, screenY + desktopUpperLeftY,
								window.BorderStyle == BorderStyle.None ? ' ' : chars.Vertical,
								resolvedFg, resolvedBg);
						}

						// Right border
						int rightX = window.Left + window.Width - 1;
						if (rightX >= region.Left && rightX < region.Left + region.Width)
						{
							var cellBelow = ResolveCellBelow(rightX, screenY, window.ZIndex, _compositingWindowsPool, 0);
							var resolvedBg = Color.Blend(windowBg, cellBelow.Background);

							// Right border may have scrollbar — read the character from what was already rendered.
							// We can't easily determine scrollbar state here, so we read the border char.
							// BorderRenderer already wrote the correct character; we just need to re-write with composited colors.
							// Unfortunately we can't read back from the driver easily, so we replicate the char logic.
							var contentHeight = window.TotalLines;
							var visibleHeight = window.Height - 2;
							var scrollbarVisible = window.IsScrollable && contentHeight > visibleHeight;

							char borderChar;
							Color fg;
							if (scrollbarVisible && window.Height > 2)
							{
								var scrollPosition = (float)window.ScrollOffset / Math.Max(1, contentHeight - visibleHeight);
								var scrollbarPosition = (int)(scrollPosition * (visibleHeight - 1));
								borderChar = (y - 1) == scrollbarPosition ? '█' : '░';
								fg = Color.Blend(borderFg, resolvedBg);
							}
							else
							{
								var chars = SharpConsoleUI.Drawing.BoxChars.FromBorderStyle(window.BorderStyle, isActive);
								borderChar = window.BorderStyle == BorderStyle.None ? ' ' : chars.Vertical;
								fg = Color.Blend(
									window.BorderStyle == BorderStyle.None ? window.ForegroundColor : borderFg,
									resolvedBg);
							}

							driver.SetNarrowCell(rightX, screenY + desktopUpperLeftY, borderChar, fg, resolvedBg);
						}
					}
				}
			}
		}

		/// <summary>
		/// Default transparency compositing: bg at raw alpha, character bubble-up with parabolic fg fade.
		/// Block characters use their foreground as effective background.
		/// </summary>
		private static Cell CompositeDefault(Cell topCell, Cell cellBelow, Color effectiveBgBelow)
		{
			var resolvedBg = Color.Blend(topCell.Background, effectiveBgBelow);

			var spaceRune = new Rune(' ');
			if (topCell.Character == spaceRune && topCell.Combiners == null &&
			    cellBelow.Character != spaceRune &&
			    !IsBlockCharacter(cellBelow.Character))
			{
				// Character bubble-up with parabolic fg fade:
				// fadeAlpha = 1 - (1 - α/255)² — fg fades faster than bg
				byte overlayAlpha = topCell.Background.A;
				double t = 1.0 - overlayAlpha / 255.0;
				byte fadeAlpha = (byte)(255 * (1.0 - t * t));
				var fadeMask = new Color(resolvedBg.R, resolvedBg.G, resolvedBg.B, fadeAlpha);
				var fadedFg = Color.Blend(fadeMask, cellBelow.Foreground);
				var result = new Cell(cellBelow.Character, fadedFg, resolvedBg, cellBelow.Decorations);
				result.IsWideContinuation = cellBelow.IsWideContinuation;
				result.Combiners = cellBelow.Combiners;
				return result;
			}
			else
			{
				var resolvedFg = Color.Blend(topCell.Foreground, resolvedBg);
				var result = new Cell(topCell.Character, resolvedFg, resolvedBg, topCell.Decorations);
				result.IsWideContinuation = topCell.IsWideContinuation;
				result.Combiners = topCell.Combiners;
				return result;
			}
		}

		/// <summary>
		/// Brush-based compositing: dispatches to Acrylic, Mica, Tinted, or Custom style.
		/// </summary>
		private static Cell CompositeWithBrush(Cell topCell, Cell cellBelow, Color effectiveBgBelow, Rendering.TransparencyBrush brush)
		{
			switch (brush.Style)
			{
				case Rendering.TransparencyStyle.Acrylic:
				{
					// Gaussian bg blend (PerceivedCellColor) + character bubble-up + power fade
					var resolvedBg = Color.Blend(topCell.Background, PerceivedCellColor(cellBelow));
					var spaceRune = new Rune(' ');
					if (topCell.Character == spaceRune && topCell.Combiners == null &&
					    cellBelow.Character != spaceRune &&
					    !IsBlockCharacter(cellBelow.Character))
					{
						byte overlayAlpha = topCell.Background.A;
						byte fadeAlpha = (byte)(255.0 * Math.Pow(overlayAlpha / 255.0, brush.FadeExponent));
						var fadeMask = new Color(resolvedBg.R, resolvedBg.G, resolvedBg.B, fadeAlpha);
						var fadedFg = Color.Blend(fadeMask, cellBelow.Foreground);
						var result = new Cell(cellBelow.Character, fadedFg, resolvedBg, cellBelow.Decorations);
						result.IsWideContinuation = cellBelow.IsWideContinuation;
						result.Combiners = cellBelow.Combiners;
						return result;
					}
					var resolvedFg = Color.Blend(topCell.Foreground, resolvedBg);
					var r = new Cell(topCell.Character, resolvedFg, resolvedBg, topCell.Decorations);
					r.IsWideContinuation = topCell.IsWideContinuation;
					r.Combiners = topCell.Combiners;
					return r;
				}

				case Rendering.TransparencyStyle.Mica:
				{
					// Gaussian bg blend, NO character bubble-up
					var resolvedBg = Color.Blend(topCell.Background, PerceivedCellColor(cellBelow));
					var resolvedFg = Color.Blend(topCell.Foreground, resolvedBg);
					var result = new Cell(topCell.Character, resolvedFg, resolvedBg, topCell.Decorations);
					result.IsWideContinuation = topCell.IsWideContinuation;
					result.Combiners = topCell.Combiners;
					return result;
				}

				case Rendering.TransparencyStyle.Tinted:
				{
					// Simple bg-only overlay — no fg influence, no bubble-up, no block guard
					var resolvedBg = Color.Blend(topCell.Background, cellBelow.Background);
					var resolvedFg = Color.Blend(topCell.Foreground, resolvedBg);
					var result = new Cell(topCell.Character, resolvedFg, resolvedBg, topCell.Decorations);
					result.IsWideContinuation = topCell.IsWideContinuation;
					result.Combiners = topCell.Combiners;
					return result;
				}

				case Rendering.TransparencyStyle.Custom when brush.CompositeFunc != null:
					return brush.CompositeFunc(topCell, cellBelow, topCell.Background.A);

				default:
					return CompositeDefault(topCell, cellBelow, effectiveBgBelow);
			}
		}

		private Cell ResolveCellBelow(int screenX, int screenY, int aboveZIndex, List<Window> sortedWindowsDesc, int depth)
		{
			if (depth > 20)
				return Cell.BlankWithBackground(_consoleWindowSystem.Theme.DesktopBackgroundColor);

			for (int i = 0; i < sortedWindowsDesc.Count; i++)
			{
				var w = sortedWindowsDesc[i];
				if (w.ZIndex >= aboveZIndex)
					continue;

				// Check if screen position falls within this window's full rectangle (including borders)
				if (screenX < w.Left || screenX >= w.Left + w.Width ||
				    screenY < w.Top || screenY >= w.Top + w.Height)
					continue;

				// Determine if this is a border cell or content cell
				bool isBorderCell = screenX == w.Left || screenX == w.Left + w.Width - 1 ||
				                    screenY == w.Top || screenY == w.Top + w.Height - 1;

				Cell cell;
				if (isBorderCell)
				{
					// Sample border cell from cached border buffers
					cell = SampleBorderCell(w, screenX, screenY);
				}
				else
				{
					// Content area — sample from ContentBuffer
					var contentBuffer = w.ContentBuffer;
					if (contentBuffer == null)
					{
						// Window exists here but buffer not ready — treat as opaque with its bg
						return Cell.BlankWithBackground(w.BackgroundColor.A == 255
							? w.BackgroundColor
							: Color.Blend(w.BackgroundColor, Cell.BlankWithBackground(
								_consoleWindowSystem.Theme.DesktopBackgroundColor).Background));
					}

					int bufX = screenX - (w.Left + 1);
					int bufY = screenY - (w.Top + 1);

					if (bufX < 0 || bufX >= contentBuffer.Width || bufY < 0 || bufY >= contentBuffer.Height)
					{
						// Position within window rect but outside buffer — use window bg
						cell = Cell.BlankWithBackground(w.BackgroundColor);
					}
					else
					{
						cell = contentBuffer.GetCell(bufX, bufY);
					}
				}

				if (cell.Background.A == 255)
					return cell;

				// This window is also semi-transparent — recurse to composite against what's below
				var cellBelow = ResolveCellBelow(screenX, screenY, w.ZIndex, sortedWindowsDesc, depth + 1);
				var effectiveBgBelow = IsBlockCharacter(cellBelow.Character)
					? cellBelow.Foreground : cellBelow.Background;
				return CompositeDefault(cell, cellBelow, effectiveBgBelow);
			}

			// No window covers this position — sample desktop background
			var desktopService = _consoleWindowSystem.DesktopBackgroundService;
			if (desktopService.HasBuffer)
			{
				var desktopBuffer = desktopService.Buffer!;
				if (screenX >= 0 && screenX < desktopBuffer.Width &&
				    screenY >= 0 && screenY < desktopBuffer.Height)
				{
					return desktopBuffer.GetCell(screenX, screenY);
				}
			}

			return Cell.BlankWithBackground(_consoleWindowSystem.Theme.DesktopBackgroundColor);
		}

		/// <summary>
		/// Returns true for block/fill characters whose foreground visually fills the entire cell,
		/// making them behave like background fills rather than text content.
		/// These should not bubble up through transparent overlays.
		/// </summary>
		private static bool IsBlockCharacter(Rune r)
		{
			int v = r.Value;
			return (v >= 0x2580 && v <= 0x259F) || v == 0x25A0;
		}

		/// <summary>
		/// Estimates what fraction of a cell a character visually covers (0–255 scale).
		/// Used to compute a "perceived cell color" by blending fg and bg, approximating
		/// a Gaussian blur of the cell's visual appearance for compositing.
		/// </summary>
		private static byte EstimateGlyphCoverage(Rune r)
		{
			int v = r.Value;

			if (v == ' ' || v == 0)
				return 0;

			if (v >= 0x2580 && v <= 0x259F)
			{
				return v switch
				{
					0x2588 => 255,
					0x2580 or 0x2584 or 0x258C or 0x2590 => 128,
					0x2591 => 64,
					0x2592 => 128,
					0x2593 => 192,
					_ => 128,
				};
			}

			if (v == 0x25A0) return 255;

			return 90; // typical text ~35% coverage
		}

		/// <summary>
		/// Computes the perceived visual color of a cell by blending its foreground into
		/// its background weighted by estimated glyph coverage. Approximates a Gaussian
		/// blur of the cell's appearance for use in transparency compositing.
		/// </summary>
		private static Color PerceivedCellColor(Cell cell)
		{
			byte coverage = EstimateGlyphCoverage(cell.Character);
			if (coverage == 0)
				return cell.Background;
			if (coverage == 255)
				return cell.Foreground;

			return Color.Blend(
				new Color(cell.Foreground.R, cell.Foreground.G, cell.Foreground.B, coverage),
				cell.Background);
		}

		/// <summary>
		/// Samples a border cell from a window's cached border buffers or reconstructs it
		/// from the window's border style and colors. Used by ResolveCellBelow to resolve
		/// border cells of windows underneath a transparent window.
		/// </summary>
		private Cell SampleBorderCell(Window w, int screenX, int screenY)
		{
			var bg = w.BackgroundColor;
			bool isActive = w.GetIsActive();
			var borderFg = isActive ? w.ActiveBorderForegroundColor : w.InactiveBorderForegroundColor;

			// Top border row
			if (screenY == w.Top)
			{
				var cachedTop = w.BorderRenderer?._cachedTopBorder;
				if (cachedTop != null)
				{
					int srcX = screenX - w.Left;
					if (srcX >= 0 && srcX < cachedTop.Width)
					{
						var cached = cachedTop.GetCell(srcX, 0);
						// Cached buffer has pre-blended colors — use raw border fg instead.
						// The character and decorations are correct, only colors need reconstruction.
						return new Cell(cached.Character, borderFg, bg, cached.Decorations);
					}
				}
				return Cell.BlankWithBackground(bg);
			}

			// Bottom border row
			if (screenY == w.Top + w.Height - 1)
			{
				var cachedBottom = w.BorderRenderer?._cachedBottomBorder;
				if (cachedBottom != null)
				{
					int srcX = screenX - w.Left;
					if (srcX >= 0 && srcX < cachedBottom.Width)
					{
						var cached = cachedBottom.GetCell(srcX, 0);
						return new Cell(cached.Character, borderFg, bg, cached.Decorations);
					}
				}
				return Cell.BlankWithBackground(bg);
			}

			// Vertical borders (left or right column)
			if (w.BorderStyle == BorderStyle.None)
				return new Cell(' ', w.ForegroundColor, bg);

			var chars = Drawing.BoxChars.FromBorderStyle(w.BorderStyle, isActive);
			return new Cell(chars.Vertical, borderFg, bg);
		}

	}
}