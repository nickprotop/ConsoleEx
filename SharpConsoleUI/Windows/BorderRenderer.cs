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
using SharpConsoleUI.Layout;
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

		// Border cache: CharacterBuffers for top and bottom border lines
		internal CharacterBuffer? _cachedTopBorder;
		internal CharacterBuffer? _cachedBottomBorder;
		internal int _cachedBorderWidth = -1;
		internal bool _cachedBorderIsActive;

		/// <summary>
		/// Initializes a new instance of the BorderRenderer class.
		/// </summary>
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
		}

		/// <summary>
		/// Renders window borders for the specified visible regions.
		/// </summary>
		public void RenderBorders(List<Rectangle> visibleRegions)
		{
			if (_window.BorderStyle == BorderStyle.None)
			{
				DrawInvisibleBorders(visibleRegions);
				return;
			}

			DrawVisibleBorders(visibleRegions);
		}

		/// <summary>
		/// Draws scrollbar at the specified vertical position using direct cell writes.
		/// </summary>
		private void DrawScrollbar(int y, Color borderFg, Color bg, char verticalBorder)
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

			driver.SetCell(
				_window.Left + _window.Width - 1,
				_window.Top + desktopUpperLeft.Y + y,
				scrollbarChar, borderFg, bg);
		}

		/// <summary>
		/// Builds the top border CharacterBuffer with title and buttons.
		/// </summary>
		private CharacterBuffer BuildTopBorder(BoxChars chars, Color borderFg, Color titleFg, Color buttonFg, Color closeFg, Color bg)
		{
			var buffer = new CharacterBuffer(_window.Width, 1, bg);

			// Window control buttons
			var minimizeButtonWidth = _window.IsMinimizable ? 3 : 0;
			var maximizeButtonWidth = _window.IsMaximizable ? 3 : 0;
			var closeButtonWidth = (_window.IsClosable && _window.ShowCloseButton) ? 3 : 0;
			var totalButtonWidth = minimizeButtonWidth + maximizeButtonWidth + closeButtonWidth;

			// Build title section
			int titleLength;
			string truncatedTitle;
			int leftPadding;
			int rightPadding;

			if (_window.ShowTitle && !string.IsNullOrEmpty(_window.Title))
			{
				var maxTitleSpace = Math.Max(0, _window.Width - 8 - totalButtonWidth);
				truncatedTitle = StringHelper.TrimWithEllipsis(_window.Title, maxTitleSpace, maxTitleSpace / 2);
				titleLength = 4 + truncatedTitle.Length; // "| " + title + " |"
				var availableSpace = Math.Max(0, _window.Width - 2 - titleLength - totalButtonWidth);
				leftPadding = Math.Min(1, availableSpace);
				rightPadding = Math.Max(0, availableSpace - leftPadding);
			}
			else
			{
				truncatedTitle = "";
				titleLength = 0;
				var availableSpace = Math.Max(0, _window.Width - 2 - totalButtonWidth);
				leftPadding = availableSpace / 2;
				rightPadding = availableSpace - leftPadding;
			}

			int x = 0;

			// Top-left corner
			buffer.SetCell(x++, 0, chars.TopLeft, borderFg, bg);

			// Left horizontal padding
			for (int i = 0; i < leftPadding; i++)
				buffer.SetCell(x++, 0, chars.Horizontal, borderFg, bg);

			// Title
			if (titleLength > 0)
			{
				buffer.SetCell(x++, 0, '|', titleFg, bg);
				buffer.SetCell(x++, 0, ' ', titleFg, bg);
				foreach (var ch in truncatedTitle)
					buffer.SetCell(x++, 0, ch, titleFg, bg);
				buffer.SetCell(x++, 0, ' ', titleFg, bg);
				buffer.SetCell(x++, 0, '|', titleFg, bg);
			}

			// Right horizontal padding
			for (int i = 0; i < rightPadding; i++)
				buffer.SetCell(x++, 0, chars.Horizontal, borderFg, bg);

			// Buttons
			if (_window.IsMinimizable)
			{
				buffer.SetCell(x++, 0, '[', buttonFg, bg);
				buffer.SetCell(x++, 0, '_', buttonFg, bg);
				buffer.SetCell(x++, 0, ']', buttonFg, bg);
			}
			if (_window.IsMaximizable)
			{
				var sym = _window.State == WindowState.Maximized ? '-' : '+';
				buffer.SetCell(x++, 0, '[', buttonFg, bg);
				buffer.SetCell(x++, 0, sym, buttonFg, bg);
				buffer.SetCell(x++, 0, ']', buttonFg, bg);
			}
			if (_window.IsClosable && _window.ShowCloseButton)
			{
				buffer.SetCell(x++, 0, '[', closeFg, bg);
				buffer.SetCell(x++, 0, 'X', closeFg, bg);
				buffer.SetCell(x++, 0, ']', closeFg, bg);
			}

			// Top-right corner
			if (x < _window.Width)
				buffer.SetCell(x, 0, chars.TopRight, borderFg, bg);

			return buffer;
		}

		/// <summary>
		/// Builds the bottom border CharacterBuffer.
		/// </summary>
		private CharacterBuffer BuildBottomBorder(BoxChars chars, Color borderFg, Color bg)
		{
			var buffer = new CharacterBuffer(_window.Width, 1, bg);

			// Bottom-left corner
			buffer.SetCell(0, 0, chars.BottomLeft, borderFg, bg);

			// Horizontal line
			for (int x = 1; x < _window.Width - 1; x++)
				buffer.SetCell(x, 0, chars.Horizontal, borderFg, bg);

			// Resize grip or bottom-right corner
			var resizeDirs = _window.AllowedResizeDirections;
			bool showResizeGrip = _window.IsResizable &&
				(resizeDirs.HasFlag(ResizeBorderDirections.BottomExpand) || resizeDirs.HasFlag(ResizeBorderDirections.BottomContract)) &&
				(resizeDirs.HasFlag(ResizeBorderDirections.RightExpand) || resizeDirs.HasFlag(ResizeBorderDirections.RightContract));

			var bottomRightChar = showResizeGrip ? '◢' : chars.BottomRight;
			if (_window.Width > 1)
				buffer.SetCell(_window.Width - 1, 0, bottomRightChar, borderFg, bg);

			return buffer;
		}

		/// <summary>
		/// Draws visible borders using cached CharacterBuffers and direct cell writes.
		/// </summary>
		private void DrawVisibleBorders(List<Rectangle> visibleRegions)
		{
			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();
			var desktopBottomRight = _getDesktopBottomRight();

			// Get border characters
			var chars = BoxChars.FromBorderStyle(_window.BorderStyle, _window.GetIsActive());

			bool isActive = _window.GetIsActive();
			var borderFg = isActive ? _window.ActiveBorderForegroundColor : _window.InactiveBorderForegroundColor;
			var titleFg = isActive ? _window.ActiveTitleForegroundColor : _window.InactiveTitleForegroundColor;
			var buttonFg = isActive ? Color.Yellow : _window.InactiveBorderForegroundColor;
			var closeFg = isActive ? Color.Red : _window.InactiveBorderForegroundColor;
			var bg = _window.BackgroundColor;

			// Rebuild cached border buffers if necessary
			if (_cachedTopBorder == null ||
				_cachedBorderWidth != _window.Width ||
				_cachedBorderIsActive != isActive)
			{
				_cachedTopBorder = BuildTopBorder(chars, borderFg, titleFg, buttonFg, closeFg, bg);
				_cachedBottomBorder = BuildBottomBorder(chars, borderFg, bg);
				_cachedBorderWidth = _window.Width;
				_cachedBorderIsActive = isActive;
			}

			var contentHeight = _window.TotalLines;
			var visibleHeight = _window.Height - 2;
			var scrollbarVisible = _window.IsScrollable && contentHeight > visibleHeight;

			foreach (var region in visibleRegions ?? new List<Rectangle>())
			{
				// Top border
				if (region.Top == _window.Top)
				{
					int borderStartX = Math.Max(region.Left, _window.Left);
					int borderWidth = Math.Min(region.Width, _window.Left + _window.Width - borderStartX);
					if (borderWidth > 0)
					{
						int srcX = borderStartX - _window.Left;
						driver.WriteBufferRegion(borderStartX, region.Top + desktopUpperLeft.Y,
							_cachedTopBorder, srcX, 0, borderWidth, bg);
					}
				}

				// Bottom border
				if (region.Top + region.Height == _window.Top + _window.Height)
				{
					int borderStartX = Math.Max(region.Left, _window.Left);
					int borderWidth = Math.Min(region.Width, _window.Left + _window.Width - borderStartX);
					if (borderWidth > 0)
					{
						int srcX = borderStartX - _window.Left;
						driver.WriteBufferRegion(borderStartX, _window.Top + _window.Height - 1 + desktopUpperLeft.Y,
							_cachedBottomBorder!, srcX, 0, borderWidth, bg);
					}
				}
			}

			// Vertical borders
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
							driver.SetCell(_window.Left, _window.Top + desktopUpperLeft.Y + y,
								chars.Vertical, borderFg, bg);
						}

						if (isRightBorderVisible)
						{
							if (scrollbarVisible)
							{
								DrawScrollbar(y, borderFg, bg, chars.Vertical);
							}
							else
							{
								driver.SetCell(rightBorderPos, _window.Top + desktopUpperLeft.Y + y,
									chars.Vertical, borderFg, bg);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Renders invisible borders by filling border areas with spaces using direct cell writes.
		/// </summary>
		private void DrawInvisibleBorders(List<Rectangle> visibleRegions)
		{
			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();
			var desktopBottomRight = _getDesktopBottomRight();

			var bg = _window.BackgroundColor;
			var fg = _window.ForegroundColor;

			foreach (var region in visibleRegions ?? new List<Rectangle>())
			{
				// Top border row
				if (region.Top == _window.Top)
				{
					int startX = Math.Max(region.Left, _window.Left);
					int width = Math.Min(region.Width, _window.Left + _window.Width - startX);
					if (width > 0)
					{
						driver.FillCells(startX, region.Top + desktopUpperLeft.Y, width, ' ', fg, bg);
					}
				}

				// Bottom border row
				if (region.Top + region.Height == _window.Top + _window.Height)
				{
					int startX = Math.Max(region.Left, _window.Left);
					int width = Math.Min(region.Width, _window.Left + _window.Width - startX);
					if (width > 0)
					{
						driver.FillCells(startX, _window.Top + _window.Height - 1 + desktopUpperLeft.Y, width, ' ', fg, bg);
					}
				}
			}

			// Left and right border columns
			for (var y = 1; y < _window.Height - 1; y++)
			{
				if (_window.Top + desktopUpperLeft.Y + y >= desktopBottomRight.Y)
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
							driver.SetCell(_window.Left, _window.Top + desktopUpperLeft.Y + y, ' ', fg, bg);
						}

						// Right border
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
								var borderFg = _window.GetIsActive()
									? _window.ActiveBorderForegroundColor
									: _window.InactiveBorderForegroundColor;
								var verticalBorder = _window.GetIsActive() ? '║' : '│';

								DrawScrollbar(y, borderFg, bg, verticalBorder);
							}
							else
							{
								driver.SetCell(rightPos, _window.Top + desktopUpperLeft.Y + y, ' ', fg, bg);
							}
						}
					}
				}
			}
		}
	}
}
