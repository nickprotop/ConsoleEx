// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace ConsoleEx
{
	public class Renderer
	{
		private ConsoleWindowSystem _consoleWindowSystem;

		public Renderer(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;
		}

		public void FillRect(int left, int top, int width, int height, char character, Color? backgroundColor, Color? foregroundColor)
		{
			for (var y = 0; y < height; y++)
			{
				if (top + y > _consoleWindowSystem.DesktopDimensions.Height) break;

				_consoleWindowSystem.ConsoleDriver.WriteToConsole(left, top + _consoleWindowSystem.DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(character, Math.Min(width, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width - left))}", Math.Min(width, _consoleWindowSystem.ConsoleDriver.ScreenSize.Width - left), 1, false, backgroundColor, foregroundColor)[0]);
			}
		}

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
				if (window != otherWindow && IsOverlapping(window, otherWindow))
				{
					GetOverlappingWindows(otherWindow, visited);
				}
			}

			return visited;
		}

		public bool IsOverlapping(Window window1, Window window2)
		{
			return window1.Left < window2.Left + window2.Width &&
				   window1.Left + window1.Width > window2.Left &&
				   window1.Top < window2.Top + window2.Height &&
				   window1.Top + window1.Height > window2.Top;
		}

		public void RenderRegion(Window window, Rectangle region)
		{
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

		public void RenderWindow(Window window)
		{
			Point desktopTopLeftCorner = _consoleWindowSystem.DesktopUpperLeft;
			Point desktopBottomRightCorner = _consoleWindowSystem.DesktopBottomRight;

			if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
			{
				return;
			}

			// Get all windows that potentially overlap with this window
			var overlappingWindows = _consoleWindowSystem.Windows.Values
				.Where(w => w != window && w.ZIndex > window.ZIndex && IsOverlapping(window, w))
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
			var horizontalBorder = window.GetIsActive() ? '═' : '─';
			var verticalBorder = window.GetIsActive() ? '║' : '│';
			var topLeftCorner = window.GetIsActive() ? '╔' : '┌';
			var topRightCorner = window.GetIsActive() ? '╗' : '┐';
			var bottomLeftCorner = window.GetIsActive() ? '╚' : '└';
			var bottomRightCorner = window.GetIsActive() ? '╝' : '┘';

			var borderColor = window.GetIsActive() ? $"[{window.ActiveBorderForegroundColor}]" : $"[{window.InactiveBorderForegroundColor}]";
			var titleColor = window.GetIsActive() ? $"[{window.ActiveTitleForegroundColor}]" : $"[{window.InactiveTitleForegroundColor}]";

			var resetColor = "[/]";

			var title = $"{titleColor}| {StringHelper.TrimWithEllipsis(window.Title, window.Width - 8, (window.Width - 8) / 2)} |{resetColor}";
			var titleLength = AnsiConsoleHelper.StripSpectreLength(title);
			var availableSpace = window.Width - 2 - titleLength;
			var leftPadding = 1;
			var rightPadding = availableSpace - leftPadding;

			var topBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{topLeftCorner}{new string(horizontalBorder, leftPadding)}{title}{new string(horizontalBorder, rightPadding)}{topRightCorner}{resetColor}", Math.Min(window.Width, _consoleWindowSystem.DesktopBottomRight.X - window.Left + 1), 1, false, window.BackgroundColor, window.ForegroundColor)[0];
			var bottomBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{bottomLeftCorner}{new string(horizontalBorder, window.Width - 2)}{bottomRightCorner}{resetColor}", window.Width, 1, false, window.BackgroundColor, window.ForegroundColor)[0];

			var contentHeight = window.TotalLines;
			var visibleHeight = window.Height - 2;

			var scrollbarVisible = window.IsScrollable && contentHeight > visibleHeight;
			var verticalBorderAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0];

			foreach (var region in visibleRegions ?? [])
			{
				if (region.Top == window.Top)
				{
					_consoleWindowSystem.ConsoleDriver.WriteToConsole(region.Left, region.Top + _consoleWindowSystem.DesktopUpperLeft.Y, AnsiConsoleHelper.SubstringAnsi(topBorder, region.Left - window.Left, region.Width));
				}

				if (region.Top + region.Height == window.Top + window.Height)
				{
					_consoleWindowSystem.ConsoleDriver.WriteToConsole(region.Left, window.Top + window.Height, AnsiConsoleHelper.SubstringAnsi(bottomBorder, region.Left - window.Left, region.Width));
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
						bool isRightBorderVisible = window.Left + window.Width > region.Left && window.Left + window.Width < region.Left + region.Width + 1;

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
								_consoleWindowSystem.ConsoleDriver.WriteToConsole(window.Left + window.Width - 1, window.Top + _consoleWindowSystem.DesktopUpperLeft.Y + y, verticalBorderAnsi);
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
						int contentLeft = Math.Max(windowLeft + 1, region.Left);
						int contentRight = Math.Min(windowLeft + windowWidth - 1, region.Left + region.Width);
						int contentWidth = contentRight - contentLeft;

						if (contentWidth <= 0) continue;

						// Calculate the portion of the line to render
						int startOffset = contentLeft - (windowLeft + 1);
						startOffset = Math.Max(0, startOffset);

						// Get the substring of the line to render
						string visiblePortion = AnsiConsoleHelper.SubstringAnsi(line, startOffset, contentWidth);

						// Write the visible portion to the console
						_consoleWindowSystem.ConsoleDriver.WriteToConsole(contentLeft, windowTop + desktopUpperLeftY + y + 1, visiblePortion);
					}
				}
			}
		}
	}
}