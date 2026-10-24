// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

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
			if (_window.BorderStyle == BorderStyle.Frameless)
			{
				DrawFramelessScrollbar();
				return;
			}

			if (_window.BorderStyle == BorderStyle.None)
			{
				DrawInvisibleBorders(visibleRegions);
				return;
			}

			DrawVisibleBorders(visibleRegions);
		}

		/// <summary>
		/// Draws the vertical scrollbar for a frameless window over the reserved last content column.
		/// No-op unless the window is scrollable and content overflows (decision is width-stable; see
		/// <see cref="Window.FramelessScrollbarReserved"/>).
		/// </summary>
		private void DrawFramelessScrollbar()
		{
			var totalLines = _window.TotalLines;
			if (!_window.FramelessScrollbarReserved(totalLines))
				return;

			int visibleHeight = _window.ContentHeight;
			if (visibleHeight <= 0)
				return;

			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();
			var bg = _window.BackgroundColor;
			var fg = _window.ForegroundColor;

			int scrollbarX = _window.Left + _window.InsetLeft + _window.ContentWidth - 1;
			int topY = _window.Top + _window.InsetTop;

			int thumbRow = -1;
			if (visibleHeight > 1)
			{
				var scrollPosition = (float)_window.ScrollOffset / Math.Max(1, totalLines - visibleHeight);
				thumbRow = (int)(scrollPosition * (visibleHeight - 1));
			}

			for (int i = 0; i < visibleHeight; i++)
			{
				char glyph = (i == thumbRow) ? '█' : '░';
				driver.SetNarrowCell(scrollbarX, topY + desktopUpperLeft.Y + i, glyph, fg, bg);
			}
		}

		/// <summary>
		/// Draws scrollbar at the specified vertical position using direct cell writes.
		/// </summary>
		private void DrawScrollbar(int y, Color borderFg, Color bg, char verticalBorder)
		{
			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();

			var scrollbarChar = '░';

			// A region may have taken this border over; otherwise the window's own body drives it,
			// which is the arrangement the frame has always had.
			var delegated = _window.BorderScrollSource(Layout.ScrollAxis.Vertical);
			var contentHeight = delegated?.Extent ?? _window.TotalLines;
			var visibleHeight = delegated?.Viewport ?? _window.ContentHeight;
			var offset = delegated?.Offset ?? _window.ScrollOffset;
			bool scrollable = delegated != null || _window.IsScrollable;

			// The TRACK is the border between the corners; the CONTENT is whatever the region
			// scrolls. These are different lengths and must not be conflated: the thumb position
			// has to be scaled into the track, not into the content's own viewport. They used to
			// be equal (the window body filled the frame), so the old code could use the viewport
			// for both - until docked bands made the body shorter than the border it draws in.
			// Scaling by the viewport left the thumb short of the end, and off the track entirely
			// once the bands took enough rows.
			int trackLength = _window.Height - 2;

			if (scrollable && contentHeight > visibleHeight && trackLength > 0)
			{
				int maxOffset = Math.Max(1, contentHeight - visibleHeight);
				int clamped = Math.Clamp(offset, 0, maxOffset);

				// Arrow caps at both ends, matching the scrollbar control, so the frame reads the
				// same way as an interior bar. They need a track with room for them plus a thumb.
				bool caps = trackLength >= Configuration.ControlDefaults.MinArrowTrackLength;
				int thumbTrack = caps ? trackLength - 2 : trackLength;
				int thumbStart = caps ? 1 : 0;

				int thumbRow = thumbStart + (int)Math.Round(
					(double)clamped / maxOffset * (thumbTrack - 1));
				thumbRow = Math.Clamp(thumbRow, thumbStart, thumbStart + thumbTrack - 1);

				int row = y - 1;
				if (caps && row == 0)
					scrollbarChar = Configuration.ControlDefaults.BorderScrollUpArrow;
				else if (caps && row == trackLength - 1)
					scrollbarChar = Configuration.ControlDefaults.BorderScrollDownArrow;
				else if (row == thumbRow)
					scrollbarChar = '█';
			}
			else scrollbarChar = verticalBorder;

			driver.SetNarrowCell(
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
				titleLength = 4 + UnicodeWidth.GetStringWidth(truncatedTitle); // "| " + title + " |"
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
			buffer.SetNarrowCell(x++, 0, chars.TopLeft, borderFg, bg);

			// Left horizontal padding
			for (int i = 0; i < leftPadding; i++)
				buffer.SetNarrowCell(x++, 0, chars.Horizontal, borderFg, bg);

			// Title
			if (titleLength > 0)
			{
				buffer.SetNarrowCell(x++, 0, '|', titleFg, bg);
				buffer.SetNarrowCell(x++, 0, ' ', titleFg, bg);
				foreach (var rune in truncatedTitle.EnumerateRunes())
				{
					int rw = UnicodeWidth.GetRuneWidth(rune);
					if (rw == 0) continue;
					buffer.SetNarrowCell(x, 0, rune, titleFg, bg);
					if (rw == 2)
					{
						buffer.SetCell(x + 1, 0, new Cell(' ', titleFg, bg) { IsWideContinuation = true });
						x++;
					}
					x++;
				}
				buffer.SetNarrowCell(x++, 0, ' ', titleFg, bg);
				buffer.SetNarrowCell(x++, 0, '|', titleFg, bg);
			}

			// Right horizontal padding
			for (int i = 0; i < rightPadding; i++)
				buffer.SetNarrowCell(x++, 0, chars.Horizontal, borderFg, bg);

			// Buttons
			if (_window.IsMinimizable)
			{
				buffer.SetNarrowCell(x++, 0, '[', buttonFg, bg);
				buffer.SetNarrowCell(x++, 0, '_', buttonFg, bg);
				buffer.SetNarrowCell(x++, 0, ']', buttonFg, bg);
			}
			if (_window.IsMaximizable)
			{
				var sym = _window.State == WindowState.Maximized ? '-' : '+';
				buffer.SetNarrowCell(x++, 0, '[', buttonFg, bg);
				buffer.SetNarrowCell(x++, 0, sym, buttonFg, bg);
				buffer.SetNarrowCell(x++, 0, ']', buttonFg, bg);
			}
			if (_window.IsClosable && _window.ShowCloseButton)
			{
				buffer.SetNarrowCell(x++, 0, '[', closeFg, bg);
				buffer.SetNarrowCell(x++, 0, 'X', closeFg, bg);
				buffer.SetNarrowCell(x++, 0, ']', closeFg, bg);
			}

			// Top-right corner
			if (x < _window.Width)
				buffer.SetNarrowCell(x, 0, chars.TopRight, borderFg, bg);

			return buffer;
		}

		/// <summary>
		/// Builds the bottom border CharacterBuffer.
		/// </summary>
		private CharacterBuffer BuildBottomBorder(BoxChars chars, Color borderFg, Color bg)
		{
			var buffer = new CharacterBuffer(_window.Width, 1, bg);

			// Bottom-left corner
			buffer.SetNarrowCell(0, 0, chars.BottomLeft, borderFg, bg);

			// Horizontal line
			for (int x = 1; x < _window.Width - 1; x++)
				buffer.SetNarrowCell(x, 0, chars.Horizontal, borderFg, bg);

			// Resize grip or bottom-right corner
			var resizeDirs = _window.AllowedResizeDirections;
			bool showResizeGrip = _window.IsResizable &&
				(resizeDirs.HasFlag(ResizeBorderDirections.BottomExpand) || resizeDirs.HasFlag(ResizeBorderDirections.BottomContract)) &&
				(resizeDirs.HasFlag(ResizeBorderDirections.RightExpand) || resizeDirs.HasFlag(ResizeBorderDirections.RightContract));

			var bottomRightChar = showResizeGrip ? '◢' : chars.BottomRight;
			if (_window.Width > 1)
				buffer.SetNarrowCell(_window.Width - 1, 0, bottomRightChar, borderFg, bg);

			return buffer;
		}

		/// <summary>
		/// Whether the right border should render a scroll track rather than plain frame.
		/// </summary>
		/// <remarks>
		/// A delegated region governs on its own terms: it took the border precisely because it
		/// has content to scroll, so the window's <c>IsScrollable</c> (which describes the
		/// window body) does not gate it.
		/// </remarks>
		private bool VerticalScrollbarVisible(int windowContentHeight, int windowVisibleHeight)
		{
			var delegated = _window.BorderScrollSource(Layout.ScrollAxis.Vertical);
			if (delegated != null)
				return delegated.Viewport > 0 && delegated.Extent > delegated.Viewport;

			return _window.IsScrollable && windowContentHeight > windowVisibleHeight;
		}

		/// <summary>
		/// Draws a horizontal scroll thumb into the bottom border for a region that delegated
		/// that axis. No-op when nothing has, which is every window unless asked.
		/// </summary>
		/// <remarks>
		/// Painted after the bottom border is blitted rather than built into it: the cached
		/// border buffer is keyed on width and active state, so a scroll that changed only the
		/// offset would redraw a stale thumb. This mirrors how the vertical thumb is written
		/// cell-by-cell over the right border for the same reason.
		/// </remarks>
		private void DrawHorizontalBorderScrollbar(List<Rectangle>? visibleRegions, Color borderFg, Color bg)
		{
			var source = _window.BorderScrollSource(Layout.ScrollAxis.Horizontal);
			if (source == null)
				return;

			int extent = source.Extent;
			int viewport = source.Viewport;
			if (viewport <= 0 || extent <= viewport)
				return;

			// The track spans the frame between the corners.
			int trackWidth = _window.Width - 2;
			if (trackWidth <= 0)
				return;

			var driver = _getDriver();
			var desktopUpperLeft = _getDesktopUpperLeft();
			int rowY = _window.Top + _window.Height - 1;

			int thumbCol = -1;
			if (trackWidth > 1)
			{
				float position = (float)source.Offset / Math.Max(1, extent - viewport);
				thumbCol = (int)(position * (trackWidth - 1));
			}

			for (int i = 0; i < trackWidth; i++)
			{
				int x = _window.Left + 1 + i;

				if (visibleRegions != null && visibleRegions.Count > 0)
				{
					bool visible = false;
					foreach (var region in visibleRegions)
					{
						if (x >= region.Left && x < region.Left + region.Width &&
							rowY >= region.Top && rowY < region.Top + region.Height)
						{
							visible = true;
							break;
						}
					}

					if (!visible) continue;
				}

				driver.SetNarrowCell(x, rowY + desktopUpperLeft.Y,
					i == thumbCol ? '█' : '░', borderFg, bg);
			}
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

			/* In PreserveTerminalTransparency (no-colour) mode the active-window title buttons
			   must not paint their hardcoded Yellow / Red: pass Color.Transparent (A=0) so
			   FormatCellAnsi emits terminal-default (;39) and a colour-free frame stays
			   colour-free (for the no-colour vendoring consumer). Inactive already
			   uses the border colour, so it is unaffected. */
			bool preserveTransparency = _window.GetConsoleWindowSystem?.Options.TerminalTransparencyMode
				== Configuration.TerminalTransparencyMode.PreserveTerminalTransparency;
			var buttonFg = isActive ? (preserveTransparency ? Color.Transparent : Color.Yellow) : _window.InactiveBorderForegroundColor;
			var closeFg = isActive ? (preserveTransparency ? Color.Transparent : Color.Red) : _window.InactiveBorderForegroundColor;
			var bg = _window.BackgroundColor;

			// Rebuild cached border buffers if necessary
			// Snapshot width to avoid race with resize changing _window.Width between check and use
			var windowWidth = _window.Width;
			if (_cachedTopBorder == null ||
				_cachedBorderWidth != windowWidth ||
				_cachedBorderIsActive != isActive)
			{
				_cachedTopBorder = BuildTopBorder(chars, borderFg, titleFg, buttonFg, closeFg, bg);
				_cachedBottomBorder = BuildBottomBorder(chars, borderFg, bg);
				_cachedBorderWidth = windowWidth;
				_cachedBorderIsActive = isActive;
			}

			var contentHeight = _window.TotalLines;
			var visibleHeight = _window.ContentHeight;
			var scrollbarVisible = VerticalScrollbarVisible(contentHeight, visibleHeight);
			var cachedTop = _cachedTopBorder;
			var cachedBottom = _cachedBottomBorder;

			foreach (var region in visibleRegions ?? new List<Rectangle>())
			{
				// Top border
				if (region.Top == _window.Top && cachedTop != null)
				{
					int borderStartX = Math.Max(region.Left, _window.Left);
					int borderWidth = Math.Min(region.Width, _window.Left + windowWidth - borderStartX);
					// Clamp to cached buffer width in case of resize race
					borderWidth = Math.Min(borderWidth, cachedTop.Width - Math.Max(0, borderStartX - _window.Left));
					if (borderWidth > 0)
					{
						int srcX = borderStartX - _window.Left;
						driver.WriteBufferRegion(borderStartX, region.Top + desktopUpperLeft.Y,
							cachedTop, srcX, 0, borderWidth, bg);
					}
				}

				// Bottom border
				if (region.Top + region.Height == _window.Top + _window.Height && cachedBottom != null)
				{
					int borderStartX = Math.Max(region.Left, _window.Left);
					int borderWidth = Math.Min(region.Width, _window.Left + windowWidth - borderStartX);
					borderWidth = Math.Min(borderWidth, cachedBottom.Width - Math.Max(0, borderStartX - _window.Left));
					if (borderWidth > 0)
					{
						int srcX = borderStartX - _window.Left;
						driver.WriteBufferRegion(borderStartX, _window.Top + _window.Height - 1 + desktopUpperLeft.Y,
							cachedBottom, srcX, 0, borderWidth, bg);
					}
				}
			}

			// The horizontal thumb goes over the bottom border just blitted above.
			DrawHorizontalBorderScrollbar(visibleRegions, borderFg, bg);

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
							driver.SetNarrowCell(_window.Left, _window.Top + desktopUpperLeft.Y + y,
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
								driver.SetNarrowCell(rightBorderPos, _window.Top + desktopUpperLeft.Y + y,
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
							driver.SetNarrowCell(_window.Left, _window.Top + desktopUpperLeft.Y + y, ' ', fg, bg);
						}

						// Right border
						int rightPos = _window.Left + _window.Width - 1;
						if (rightPos >= region.Left &&
							rightPos < region.Left + region.Width)
						{
							// Check if scrollbar should be visible
							var contentHeight = _window.TotalLines;
							var visibleHeight = _window.ContentHeight;
							var scrollbarVisible = VerticalScrollbarVisible(contentHeight, visibleHeight);

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
								driver.SetNarrowCell(rightPos, _window.Top + desktopUpperLeft.Y + y, ' ', fg, bg);
							}
						}
					}
				}
			}
		}
	}
}
