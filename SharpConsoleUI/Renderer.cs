// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
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
			for (var y = 0; y < height; y++)
			{
				if (top + y > _consoleWindowSystem.DesktopDimensions.Height) break;

				_consoleWindowSystem.ConsoleDriver.WriteToConsole(left, top + _consoleWindowSystem.DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(character, Math.Min(width, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width - left))}", Math.Min(width, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width - left), 1, false, backgroundColor, foregroundColor)[0]);
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
		/// Gets all windows that overlap with the specified window, recursively including windows that overlap with those windows.
		/// </summary>
		/// <param name="window">The window to check for overlaps.</param>
		/// <param name="visited">Optional set of already visited windows to prevent infinite recursion.</param>
		/// <returns>A set of all windows that form an overlapping chain with the specified window.</returns>
		public HashSet<Window> GetOverlappingWindows(Window window, HashSet<Window>? visited = null)
		{
			visited ??= new HashSet<Window>();
			if (visited.Contains(window))
			{
				return visited;
			}

			visited.Add(window);

			foreach (var otherWindow in _consoleWindowSystem.Windows.Values)
			{
				// Skip minimized windows - they're invisible and don't overlap
				if (otherWindow.State == WindowState.Minimized)
					continue;

				if (window != otherWindow && IsOverlapping(window, otherWindow))
				{
					GetOverlappingWindows(otherWindow, visited);
				}
			}

			return visited;
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

			var visibleRegions = new List<Rectangle> { region };

			// Fill the background only for the visible regions
			foreach (var visibleRegion in visibleRegions)
			{
				FillRect(visibleRegion.Left, visibleRegion.Top, visibleRegion.Width, visibleRegion.Height, ' ', window.BackgroundColor, null);
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			DrawWindowBorders(window, visibleRegions);

			var lines = window.RenderAndGetVisibleContent();
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
				// Window is completely covered - no need to render
				window.IsDirty = false;
				return;
			}

			// Fill the background only for the visible regions
			foreach (var region in visibleRegions)
			{
				FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
			}

			// Draw window borders - these might be partially hidden but the drawing functions
			// will handle clipping against screen boundaries
			DrawWindowBorders(window, visibleRegions);

			var lines = window.RenderAndGetVisibleContent();
			window.IsDirty = false;

			// Render content only for visible parts
			RenderVisibleWindowContent(window, lines, visibleRegions);
		}

		private void DrawScrollbar(Window window, int y, string borderColor, char verticalBorder, string resetColor)
		{
			var scrollbarChar = '░';
			var contentHeight = window.TotalLines;
			var visibleHeight = window.Height - 2;

			if (window.IsScrollable && contentHeight > visibleHeight)
			{
				if (window.Height > 2)
				{
					var scrollPosition = (float)window.ScrollOffset / Math.Max(1, contentHeight - visibleHeight);
					var scrollbarPosition = (int)(scrollPosition * (visibleHeight - 1));
					if (y - 1 == scrollbarPosition)
					{
						scrollbarChar = '█';
					}
				}
			}
			else scrollbarChar = verticalBorder;

			_consoleWindowSystem.ConsoleDriver.WriteToConsole(window.Left + window.Width - 1, window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{scrollbarChar}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);
		}

		private void DrawWindowBorders(Window window, List<Rectangle> visibleRegions)
		{
			// Handle borderless windows
			if (window.BorderStyle == BorderStyle.None)
			{
				DrawInvisibleBorders(window, visibleRegions);
				return;
			}

			// Get border characters based on BorderStyle (not active state)
			char horizontalBorder, verticalBorder, topLeftCorner, topRightCorner, bottomLeftCorner, bottomRightCorner;
			switch (window.BorderStyle)
			{
				case BorderStyle.Single:
					horizontalBorder = '─';
					verticalBorder = '│';
					topLeftCorner = '┌';
					topRightCorner = '┐';
					bottomLeftCorner = '└';
					bottomRightCorner = '┘';
					break;
				case BorderStyle.Rounded:
					horizontalBorder = '─';
					verticalBorder = '│';
					topLeftCorner = '╭';
					topRightCorner = '╮';
					bottomLeftCorner = '╰';
					bottomRightCorner = '╯';
					break;
				case BorderStyle.DoubleLine:
				default:
					// DoubleLine: use double when active, single when inactive (legacy behavior)
					horizontalBorder = window.GetIsActive() ? '═' : '─';
					verticalBorder = window.GetIsActive() ? '║' : '│';
					topLeftCorner = window.GetIsActive() ? '╔' : '┌';
					topRightCorner = window.GetIsActive() ? '╗' : '┐';
					bottomLeftCorner = window.GetIsActive() ? '╚' : '└';
					bottomRightCorner = window.GetIsActive() ? '╝' : '┘';
					break;
			}

			var borderColor = window.GetIsActive() ? $"[{window.ActiveBorderForegroundColor}]" : $"[{window.InactiveBorderForegroundColor}]";
			var titleColor = window.GetIsActive() ? $"[{window.ActiveTitleForegroundColor}]" : $"[{window.InactiveTitleForegroundColor}]";
			var buttonColor = window.GetIsActive() ? "[yellow]" : $"[{window.InactiveBorderForegroundColor}]";
			var closeButtonColor = window.GetIsActive() ? "[red]" : $"[{window.InactiveBorderForegroundColor}]";

			var resetColor = "[/]";

			// Window control buttons: [_] minimize, [+]/[-] maximize/restore, [X] close
			// Each button takes 3 characters
			var minimizeButtonWidth = window.IsMinimizable ? 3 : 0;
			var maximizeButtonWidth = window.IsMaximizable ? 3 : 0;
			var closeButtonWidth = (window.IsClosable && window.ShowCloseButton) ? 3 : 0;
			var totalButtonWidth = minimizeButtonWidth + maximizeButtonWidth + closeButtonWidth;

			var minimizeButton = window.IsMinimizable ? $"{buttonColor}[_]" : "";
			// + for maximize, - for restore (when already maximized)
			var maximizeSymbol = window.State == WindowState.Maximized ? "-" : "+";
			var maximizeButton = window.IsMaximizable ? $"{buttonColor}[{maximizeSymbol}]" : "";
			var closeButton = (window.IsClosable && window.ShowCloseButton) ? $"{closeButtonColor}[X]" : "";
			var windowButtons = $"{minimizeButton}{maximizeButton}{closeButton}{resetColor}";

			// Resize grip replaces bottom-right corner when window is resizable: ◢
			var bottomRightChar = window.IsResizable ? "◢" : bottomRightCorner.ToString();

			// Build title section (only if ShowTitle is true and title is not empty)
			string title;
			int titleLength;
			int leftPadding;
			int rightPadding;

			if (window.ShowTitle && !string.IsNullOrEmpty(window.Title))
			{
				// Ensure we have enough space for the title, with safety margins
				var maxTitleSpace = Math.Max(0, window.Width - 8 - totalButtonWidth); // Reserve space for corners, padding, buttons, and safety
				var truncatedTitle = StringHelper.TrimWithEllipsis(window.Title, maxTitleSpace, maxTitleSpace / 2);
				// Don't escape - title is plain text, not parsed as markup when concatenated
				// Calculate visible length directly from plain text
				titleLength = 4 + truncatedTitle.Length; // "| " + title + " |" = 4 extra chars
				title = $"{titleColor}| {truncatedTitle} |{resetColor}";
				var availableSpace = Math.Max(0, window.Width - 2 - titleLength - totalButtonWidth);
				leftPadding = Math.Min(1, availableSpace);
				rightPadding = Math.Max(0, availableSpace - leftPadding);
			}
			else
			{
				// No title - just border and buttons
				title = "";
				titleLength = 0;
				var availableSpace = Math.Max(0, window.Width - 2 - totalButtonWidth);
				leftPadding = availableSpace / 2;
				rightPadding = availableSpace - leftPadding;
			}

			var topBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{topLeftCorner}{new string(horizontalBorder, leftPadding)}{title}{new string(horizontalBorder, rightPadding)}{windowButtons}{borderColor}{topRightCorner}{resetColor}", Math.Min(window.Width, _consoleWindowSystem.DesktopBottomRight.X - window.Left + 1), 1, false, window.BackgroundColor, window.ForegroundColor)[0];
			var bottomBorderWidth = window.Width - 2;
			var bottomBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{bottomLeftCorner}{new string(horizontalBorder, bottomBorderWidth)}{bottomRightChar}{resetColor}", window.Width, 1, false, window.BackgroundColor, window.ForegroundColor)[0];

			var contentHeight = window.TotalLines;
			var visibleHeight = window.Height - 2;

			var scrollbarVisible = window.IsScrollable && contentHeight > visibleHeight;
			var verticalBorderAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0];

			foreach (var region in visibleRegions ?? [])
			{
				if (region.Top == window.Top)
				{
					// Ensure we don't write beyond the region boundaries
					int borderStartX = Math.Max(region.Left, window.Left);
					int borderWidth = Math.Min(region.Width, window.Left + window.Width - borderStartX);
					if (borderWidth > 0)
					{
						string borderSegment = AnsiConsoleHelper.SubstringAnsi(topBorder, borderStartX - window.Left, borderWidth);
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(borderStartX, region.Top + _consoleWindowSystem.DesktopUpperLeft.Y, borderSegment);
					}
				}

				if (region.Top + region.Height == window.Top + window.Height)
				{
					// Ensure we don't write beyond the region boundaries for bottom border
					int borderStartX = Math.Max(region.Left, window.Left);
					int borderWidth = Math.Min(region.Width, window.Left + window.Width - borderStartX);
					if (borderWidth > 0)
					{
						string borderSegment = AnsiConsoleHelper.SubstringAnsi(bottomBorder, borderStartX - window.Left, borderWidth);
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(borderStartX, window.Top + window.Height - 1 + _consoleWindowSystem.DesktopUpperLeft.Y, borderSegment);
					}
				}
			}

			for (var y = 1; y < window.Height - 1; y++)
			{
				if (window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y - 1 >= _consoleWindowSystem.DesktopBottomRight.Y) break;

				foreach (var region in visibleRegions ?? [])
				{
					if (window.Top + y >= region.Top && window.Top + y < region.Top + region.Height)
					{
						bool isLeftBorderVisible = window.Left >= region.Left && window.Left < region.Left + region.Width;
						int rightBorderPos = window.Left + window.Width - 1;
						bool isRightBorderVisible = rightBorderPos >= region.Left && rightBorderPos < region.Left + region.Width;

						if (isLeftBorderVisible)
						{
							_consoleWindowSystem.ConsoleDriver.WriteToConsole(window.Left, window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y, verticalBorderAnsi);
						}

						if (isRightBorderVisible)
						{
							if (scrollbarVisible)
							{
								DrawScrollbar(window, y, borderColor, verticalBorder, resetColor);
							}
							else
							{
								_consoleWindowSystem.ConsoleDriver.WriteToConsole(rightBorderPos, window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y, verticalBorderAnsi);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Renders invisible borders by filling border areas with spaces.
		/// Preserves layout (border space exists) while making borders visually disappear.
		/// </summary>
		private void DrawInvisibleBorders(Window window, List<Rectangle> visibleRegions)
		{
			var backgroundColor = window.BackgroundColor;
			var foregroundColor = window.ForegroundColor;

			// Create space character with window background color
			var spaceAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
				" ", 1, 1, false, backgroundColor, foregroundColor)[0];

			foreach (var region in visibleRegions ?? new List<Rectangle>())
			{
				// Top border row (Y=0)
				if (region.Top == window.Top)
				{
					int startX = Math.Max(region.Left, window.Left);
					int width = Math.Min(region.Width, window.Left + window.Width - startX);

					for (int x = 0; x < width; x++)
					{
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(
							startX + x,
							region.Top + _consoleWindowSystem.DesktopUpperLeft.Y,
							spaceAnsi);
					}
				}

				// Bottom border row (Y=height-1)
				if (region.Top + region.Height == window.Top + window.Height)
				{
					int startX = Math.Max(region.Left, window.Left);
					int width = Math.Min(region.Width, window.Left + window.Width - startX);

					for (int x = 0; x < width; x++)
					{
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(
							startX + x,
							window.Top + window.Height - 1 + _consoleWindowSystem.DesktopUpperLeft.Y,
							spaceAnsi);
					}
				}
			}

			// Left and right border columns
			for (var y = 1; y < window.Height - 1; y++)
			{
				if (window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y >=
					_consoleWindowSystem.DesktopBottomRight.Y)
					break;

				foreach (var region in visibleRegions ?? new List<Rectangle>())
				{
					if (window.Top + y >= region.Top &&
						window.Top + y < region.Top + region.Height)
					{
						// Left border
						if (window.Left >= region.Left &&
							window.Left < region.Left + region.Width)
						{
							_consoleWindowSystem.ConsoleDriver.WriteToConsole(
								window.Left,
								window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y,
								spaceAnsi);
						}

						// Right border (includes scrollbar position)
						int rightPos = window.Left + window.Width - 1;
						if (rightPos >= region.Left &&
							rightPos < region.Left + region.Width)
						{
							// Check if scrollbar should be visible
							var contentHeight = window.TotalLines;
							var visibleHeight = window.Height - 2;
							var scrollbarVisible = window.IsScrollable && contentHeight > visibleHeight;

							if (scrollbarVisible)
							{
								// Render scrollbar (same as DrawWindowBorders does)
								var borderColor = window.GetIsActive()
									? $"[{window.ActiveBorderForegroundColor}]"
									: $"[{window.InactiveBorderForegroundColor}]";
								var verticalBorder = window.GetIsActive() ? '║' : '│';
								var resetColor = "[/]";

								DrawScrollbar(window, y, borderColor, verticalBorder, resetColor);
							}
							else
							{
								// No scrollbar - render as space
								_consoleWindowSystem.ConsoleDriver.WriteToConsole(
									rightPos,
									window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y,
									spaceAnsi);
							}
						}
					}
				}
			}
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