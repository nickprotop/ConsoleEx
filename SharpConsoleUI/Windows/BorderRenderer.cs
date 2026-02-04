// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Handles border rendering for windows including borders, scrollbars, and invisible borders.
	/// Extracted from Renderer class as part of Phase 3.3 refactoring.
	///
	/// Responsibilities:
	/// - Border drawing with caching
	/// - Scrollbar rendering
	/// - Invisible border (BorderStyle.None) rendering
	/// - Border cache management
	/// </summary>
	public class BorderRenderer
	{
		private readonly Window _window;
		private readonly Func<IConsoleDriver> _getDriver;
		private readonly Func<Point> _getDesktopUpperLeft;
		private readonly Func<Point> _getDesktopBottomRight;

		// Border cache (moved from Window.cs)
		internal string? _cachedTopBorder;
		internal string? _cachedBottomBorder;
		internal string? _cachedVerticalBorder;
		internal int _cachedBorderWidth = -1;
		internal bool _cachedBorderIsActive;

		// FIX11: Prevent ANSI doubling by not passing foregroundColor when markup already has color tags
		// DEPRECATED: This flag caused color bleeding bugs where borders inherited background colors from
		// previous ANSI output. We now ALWAYS pass explicit backgroundColor and foregroundColor to prevent
		// ANSI state bleeding. The markup's color tags control foreground, explicit params ensure background.
		private const bool FIX11_NO_FOREGROUND_IN_MARKUP = true;

		/// <summary>
		/// Initializes a new instance of the BorderRenderer class.
		/// </summary>
		/// <param name="window">The window this renderer serves</param>
		/// <param name="getDriver">Delegate to get the console driver</param>
		/// <param name="getDesktopUpperLeft">Delegate to get desktop upper left point</param>
		/// <param name="getDesktopBottomRight">Delegate to get desktop bottom right point</param>
		public BorderRenderer(
			Window window,
			Func<IConsoleDriver> getDriver,
			Func<Point> getDesktopUpperLeft,
			Func<Point> getDesktopBottomRight)
		{
			_window = window;
			_getDriver = getDriver;
			_getDesktopUpperLeft = getDesktopUpperLeft;
			_getDesktopBottomRight = getDesktopBottomRight;
		}

		/// <summary>
		/// Invalidates the border cache, forcing borders to be rebuilt on next render.
		/// </summary>
		public void InvalidateCache()
		{
			_cachedTopBorder = null;
			_cachedBottomBorder = null;
			_cachedVerticalBorder = null;
		}

		/// <summary>
		/// Renders window borders for the specified visible regions.
		/// </summary>
		/// <param name="visibleRegions">List of visible screen regions where borders should be drawn</param>
		public void RenderBorders(List<Rectangle> visibleRegions)
		{
			// Handle borderless windows
			if (_window.BorderStyle == BorderStyle.None)
			{
				DrawInvisibleBorders(visibleRegions);
				return;
			}

			DrawVisibleBorders(visibleRegions);
		}

		/// <summary>
		/// Draws scrollbar at the specified vertical position.
		/// </summary>
		private void DrawScrollbar(int y, string borderColor, char verticalBorder, string resetColor)
		{
			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();

			var scrollbarChar = '░';
			var contentHeight = _window.TotalLines;
			var visibleHeight = _window.Height - 2;

			if (_window.IsScrollable && contentHeight > visibleHeight)
			{
				if (_window.Height > 2)
				{
					var scrollPosition = (float)_window.ScrollOffset / Math.Max(1, contentHeight - visibleHeight);
					var scrollbarPosition = (int)(scrollPosition * (visibleHeight - 1));
					if (y - 1 == scrollbarPosition)
					{
						scrollbarChar = '█';
					}
				}
			}
			else scrollbarChar = verticalBorder;

			driver.WriteToConsole(
				_window.Left + _window.Width - 1,
				_window.Top + desktopUpperLeft.Y + y,
				AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"{borderColor}{scrollbarChar}{resetColor}",
					1, 1, false,
					_window.BackgroundColor,  // Explicit background prevents color bleeding
					_window.ForegroundColor)[0]);  // Explicit foreground as safety net
		}

		/// <summary>
		/// Draws visible borders (non-None BorderStyle).
		/// </summary>
		private void DrawVisibleBorders(List<Rectangle> visibleRegions)
		{
			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();
			var desktopBottomRight = _getDesktopBottomRight();

			// Get border characters using BoxChars abstraction
			// DoubleLine style uses active state to switch between double (active) and single (inactive)
			var chars = BoxChars.FromBorderStyle(_window.BorderStyle, _window.GetIsActive());
			char horizontalBorder = chars.Horizontal;
			char verticalBorder = chars.Vertical;
			char topLeftCorner = chars.TopLeft;
			char topRightCorner = chars.TopRight;
			char bottomLeftCorner = chars.BottomLeft;
			char bottomRightCorner = chars.BottomRight;

			var borderColor = _window.GetIsActive() ? $"[{_window.ActiveBorderForegroundColor}]" : $"[{_window.InactiveBorderForegroundColor}]";
			var titleColor = _window.GetIsActive() ? $"[{_window.ActiveTitleForegroundColor}]" : $"[{_window.InactiveTitleForegroundColor}]";
			var buttonColor = _window.GetIsActive() ? "[yellow]" : $"[{_window.InactiveBorderForegroundColor}]";
			var closeButtonColor = _window.GetIsActive() ? "[red]" : $"[{_window.InactiveBorderForegroundColor}]";

			var resetColor = "[/]";

			// Window control buttons: [_] minimize, [+]/[-] maximize/restore, [X] close
			// Each button takes 3 characters
			var minimizeButtonWidth = _window.IsMinimizable ? 3 : 0;
			var maximizeButtonWidth = _window.IsMaximizable ? 3 : 0;
			var closeButtonWidth = (_window.IsClosable && _window.ShowCloseButton) ? 3 : 0;
			var totalButtonWidth = minimizeButtonWidth + maximizeButtonWidth + closeButtonWidth;

			var minimizeButton = _window.IsMinimizable ? $"{buttonColor}[_]" : "";
			// + for maximize, - for restore (when already maximized)
			var maximizeSymbol = _window.State == WindowState.Maximized ? "-" : "+";
			var maximizeButton = _window.IsMaximizable ? $"{buttonColor}[{maximizeSymbol}]" : "";
			var closeButton = (_window.IsClosable && _window.ShowCloseButton) ? $"{closeButtonColor}[X]" : "";
			var windowButtons = $"{minimizeButton}{maximizeButton}{closeButton}{resetColor}";

			// Resize grip replaces bottom-right corner when window is resizable: ◢
			var bottomRightChar = _window.IsResizable ? "◢" : bottomRightCorner.ToString();

			// Build title section (only if ShowTitle is true and title is not empty)
			string title;
			int titleLength;
			int leftPadding;
			int rightPadding;

			if (_window.ShowTitle && !string.IsNullOrEmpty(_window.Title))
			{
				// Ensure we have enough space for the title, with safety margins
				var maxTitleSpace = Math.Max(0, _window.Width - 8 - totalButtonWidth); // Reserve space for corners, padding, buttons, and safety
				var truncatedTitle = StringHelper.TrimWithEllipsis(_window.Title, maxTitleSpace, maxTitleSpace / 2);
				// Don't escape - title is plain text, not parsed as markup when concatenated
				// Calculate visible length directly from plain text
				titleLength = 4 + truncatedTitle.Length; // "| " + title + " |" = 4 extra chars
				title = $"{titleColor}| {truncatedTitle} |{resetColor}";
				var availableSpace = Math.Max(0, _window.Width - 2 - titleLength - totalButtonWidth);
				leftPadding = Math.Min(1, availableSpace);
				rightPadding = Math.Max(0, availableSpace - leftPadding);
			}
			else
			{
				// No title - just border and buttons
				title = "";
				titleLength = 0;
				var availableSpace = Math.Max(0, _window.Width - 2 - totalButtonWidth);
				leftPadding = availableSpace / 2;
				rightPadding = availableSpace - leftPadding;
			}

			// Check if border cache is valid, rebuild if necessary
			bool isActive = _window.GetIsActive();
			if (_cachedTopBorder == null ||
				_cachedBorderWidth != _window.Width ||
				_cachedBorderIsActive != isActive)
			{
				// Rebuild cached border strings
				// IMPORTANT: Always pass explicit backgroundColor and foregroundColor to prevent ANSI color bleeding
				// from previous console output. Without these, borders inherit stale colors from overlapping windows.
				var topBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"{borderColor}{topLeftCorner}{new string(horizontalBorder, leftPadding)}{title}{new string(horizontalBorder, rightPadding)}{windowButtons}{borderColor}{topRightCorner}{resetColor}",
					Math.Min(_window.Width, desktopBottomRight.X - _window.Left + 1), 1, false,
					_window.BackgroundColor,  // Explicit background prevents color bleeding
					_window.ForegroundColor)[0];  // Explicit foreground as safety net

				var bottomBorderWidth = _window.Width - 2;
				var bottomBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"{borderColor}{bottomLeftCorner}{new string(horizontalBorder, bottomBorderWidth)}{bottomRightChar}{resetColor}",
					_window.Width, 1, false,
					_window.BackgroundColor,  // Explicit background prevents color bleeding
					_window.ForegroundColor)[0];  // Explicit foreground as safety net

				var vertBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"{borderColor}{verticalBorder}{resetColor}",
					1, 1, false,
					_window.BackgroundColor,  // Explicit background prevents color bleeding
					_window.ForegroundColor)[0];  // Explicit foreground as safety net

				// Update cache
				_cachedTopBorder = topBorder;
				_cachedBottomBorder = bottomBorder;
				_cachedVerticalBorder = vertBorder;
				_cachedBorderWidth = _window.Width;
				_cachedBorderIsActive = isActive;
			}

			// Use cached borders for rendering
			var cachedTopBorder = _cachedTopBorder!;
			var cachedBottomBorder = _cachedBottomBorder!;
			var cachedVerticalBorderAnsi = _cachedVerticalBorder!;

			var contentHeight = _window.TotalLines;
			var visibleHeight = _window.Height - 2;

			var scrollbarVisible = _window.IsScrollable && contentHeight > visibleHeight;

			foreach (var region in visibleRegions ?? new List<Rectangle>())
			{
				if (region.Top == _window.Top)
				{
					// Ensure we don't write beyond the region boundaries
					int borderStartX = Math.Max(region.Left, _window.Left);
					int borderWidth = Math.Min(region.Width, _window.Left + _window.Width - borderStartX);
					if (borderWidth > 0)
					{
						string borderSegment = AnsiConsoleHelper.SubstringAnsi(cachedTopBorder, borderStartX - _window.Left, borderWidth);
						driver.WriteToConsole(borderStartX, region.Top + desktopUpperLeft.Y, borderSegment);
					}
				}

				if (region.Top + region.Height == _window.Top + _window.Height)
				{
					// Ensure we don't write beyond the region boundaries for bottom border
					int borderStartX = Math.Max(region.Left, _window.Left);
					int borderWidth = Math.Min(region.Width, _window.Left + _window.Width - borderStartX);
					if (borderWidth > 0)
					{
						string borderSegment = AnsiConsoleHelper.SubstringAnsi(cachedBottomBorder, borderStartX - _window.Left, borderWidth);
						driver.WriteToConsole(borderStartX, _window.Top + _window.Height - 1 + desktopUpperLeft.Y, borderSegment);
					}
				}
			}

			for (var y = 1; y < _window.Height - 1; y++)
			{
				if (_window.Top + desktopUpperLeft.Y + y - 1 >= desktopBottomRight.Y) break;

				foreach (var region in visibleRegions ?? new List<Rectangle>())
				{
					if (_window.Top + y >= region.Top && _window.Top + y < region.Top + region.Height)
					{
						bool isLeftBorderVisible = _window.Left >= region.Left && _window.Left < region.Left + region.Width;
						int rightBorderPos = _window.Left + _window.Width - 1;
						bool isRightBorderVisible = rightBorderPos >= region.Left && rightBorderPos < region.Left + region.Width;

						if (isLeftBorderVisible)
						{
							driver.WriteToConsole(_window.Left, _window.Top + desktopUpperLeft.Y + y, cachedVerticalBorderAnsi);
						}

						if (isRightBorderVisible)
						{
							if (scrollbarVisible)
							{
								DrawScrollbar(y, borderColor, verticalBorder, resetColor);
							}
							else
							{
								driver.WriteToConsole(rightBorderPos, _window.Top + desktopUpperLeft.Y + y, cachedVerticalBorderAnsi);
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
		private void DrawInvisibleBorders(List<Rectangle> visibleRegions)
		{
			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();
			var desktopBottomRight = _getDesktopBottomRight();

			var backgroundColor = _window.BackgroundColor;
			var foregroundColor = _window.ForegroundColor;

			// Create space character with window background color
			var spaceAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
				" ", 1, 1, false, backgroundColor, foregroundColor)[0];

			foreach (var region in visibleRegions ?? new List<Rectangle>())
			{
				// Top border row (Y=0)
				if (region.Top == _window.Top)
				{
					int startX = Math.Max(region.Left, _window.Left);
					int width = Math.Min(region.Width, _window.Left + _window.Width - startX);

					for (int x = 0; x < width; x++)
					{
						driver.WriteToConsole(
							startX + x,
							region.Top + desktopUpperLeft.Y,
							spaceAnsi);
					}
				}

				// Bottom border row (Y=height-1)
				if (region.Top + region.Height == _window.Top + _window.Height)
				{
					int startX = Math.Max(region.Left, _window.Left);
					int width = Math.Min(region.Width, _window.Left + _window.Width - startX);

					for (int x = 0; x < width; x++)
					{
						driver.WriteToConsole(
							startX + x,
							_window.Top + _window.Height - 1 + desktopUpperLeft.Y,
							spaceAnsi);
					}
				}
			}

			// Left and right border columns
			for (var y = 1; y < _window.Height - 1; y++)
			{
				if (_window.Top + desktopUpperLeft.Y + y >=
					desktopBottomRight.Y)
					break;

				foreach (var region in visibleRegions ?? new List<Rectangle>())
				{
					if (_window.Top + y >= region.Top &&
						_window.Top + y < region.Top + region.Height)
					{
						// Left border
						if (_window.Left >= region.Left &&
							_window.Left < region.Left + region.Width)
						{
							driver.WriteToConsole(
								_window.Left,
								_window.Top + desktopUpperLeft.Y + y,
								spaceAnsi);
						}

						// Right border (includes scrollbar position)
						int rightPos = _window.Left + _window.Width - 1;
						if (rightPos >= region.Left &&
							rightPos < region.Left + region.Width)
						{
							// Check if scrollbar should be visible
							var contentHeight = _window.TotalLines;
							var visibleHeight = _window.Height - 2;
							var scrollbarVisible = _window.IsScrollable && contentHeight > visibleHeight;

							if (scrollbarVisible)
							{
								// Render scrollbar (same as DrawVisibleBorders does)
								var borderColor = _window.GetIsActive()
									? $"[{_window.ActiveBorderForegroundColor}]"
									: $"[{_window.InactiveBorderForegroundColor}]";
								var verticalBorder = _window.GetIsActive() ? '║' : '│';
								var resetColor = "[/]";

								DrawScrollbar(y, borderColor, verticalBorder, resetColor);
							}
							else
							{
								// No scrollbar - render as space
								driver.WriteToConsole(
									rightPos,
									_window.Top + desktopUpperLeft.Y + y,
									spaceAnsi);
							}
						}
					}
				}
			}
		}
	}
}
