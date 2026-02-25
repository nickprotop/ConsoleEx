// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		/// <summary>
		/// Tests whether a mouse position falls on the vertical scrollbar column.
		/// </summary>
		private bool IsOnVerticalScrollbar(int mouseX)
		{
			if (!_needsVerticalScrollbar) return false;
			int scrollbarX = Margin.Left + GetGutterWidth() + _effectiveWidth;
			return mouseX == scrollbarX;
		}

		/// <summary>
		/// Tests whether a mouse position falls on the horizontal scrollbar row.
		/// </summary>
		private bool IsOnHorizontalScrollbar(int mouseY)
		{
			if (!_needsHorizontalScrollbar) return false;
			int scrollbarY = Margin.Top + _effectiveViewportHeight;
			return mouseY == scrollbarY;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// --- Scrollbar drag-in-progress (must be checked before text drag) ---

			if (_isVerticalScrollbarDragging &&
				args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				var (trackHeight, _, sbThumbHeight) = GetVerticalScrollbarGeometry();
				int deltaY = args.Position.Y - _verticalScrollbarDragStartY;
				int totalLines = GetTotalWrappedLineCount();
				int effectiveViewport = GetEffectiveViewportHeight();
				int maxScroll = Math.Max(0, totalLines - effectiveViewport);
				int trackRange = Math.Max(1, trackHeight - sbThumbHeight);
				int newOffset = _verticalScrollbarDragStartOffset +
					(int)(deltaY * (double)maxScroll / trackRange);
				newOffset = Math.Clamp(newOffset, 0, maxScroll);
				_skipUpdateScrollPositionsInRender = true;
				_verticalScrollOffset = newOffset;
				Container?.Invalidate(true);
				return true;
			}

			if (_isHorizontalScrollbarDragging &&
				args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				var (trackWidth, _, sbThumbWidth) = GetHorizontalScrollbarGeometry();
				int deltaX = args.Position.X - _horizontalScrollbarDragStartX;
				int maxLineLength = GetMaxLineLength();
				int maxScroll = Math.Max(0, maxLineLength - _effectiveWidth);
				int trackRange = Math.Max(1, trackWidth - sbThumbWidth);
				int newOffset = _horizontalScrollbarDragStartOffset +
					(int)(deltaX * (double)maxScroll / trackRange);
				newOffset = Math.Clamp(newOffset, 0, maxScroll);
				_horizontalScrollOffset = newOffset;
				Container?.Invalidate(true);
				return true;
			}

			// --- Scrollbar drag end ---

			if (args.HasFlag(MouseFlags.Button1Released))
			{
				if (_isVerticalScrollbarDragging || _isHorizontalScrollbarDragging)
				{
					_isVerticalScrollbarDragging = false;
					_isHorizontalScrollbarDragging = false;
					return true;
				}
			}

			// --- Scrollbar click detection (before text handling) ---

			if (args.HasFlag(MouseFlags.Button1Pressed) && _hasFocus)
			{
				// Vertical scrollbar interaction
				if (IsOnVerticalScrollbar(args.Position.X))
				{
					_scrollbarInteracted = true;
					int relY = args.Position.Y - Margin.Top;
					var (trackHeight, sbThumbY, sbThumbHeight) = GetVerticalScrollbarGeometry();
					int effectiveViewport = trackHeight;
					int totalLines = GetTotalWrappedLineCount();
					int maxScroll = Math.Max(0, totalLines - effectiveViewport);

					if (relY >= 0 && relY < trackHeight)
					{
						if (relY == 0 && _verticalScrollOffset > 0)
						{
							// Arrow up
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = Math.Max(0, _verticalScrollOffset - ControlDefaults.DefaultScrollWheelLines);
							Container?.Invalidate(true);
						}
						else if (relY == trackHeight - 1 && _verticalScrollOffset < maxScroll)
						{
							// Arrow down
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = Math.Min(maxScroll, _verticalScrollOffset + ControlDefaults.DefaultScrollWheelLines);
							Container?.Invalidate(true);
						}
						else if (relY >= sbThumbY && relY < sbThumbY + sbThumbHeight)
						{
							// Thumb: start drag
							_isVerticalScrollbarDragging = true;
							_verticalScrollbarDragStartY = args.Position.Y;
							_verticalScrollbarDragStartOffset = _verticalScrollOffset;
						}
						else if (relY < sbThumbY)
						{
							// Track above thumb: page up
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = Math.Max(0, _verticalScrollOffset - effectiveViewport);
							Container?.Invalidate(true);
						}
						else
						{
							// Track below thumb: page down
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = Math.Min(maxScroll, _verticalScrollOffset + effectiveViewport);
							Container?.Invalidate(true);
						}
						return true;
					}
				}

				// Horizontal scrollbar interaction
				if (IsOnHorizontalScrollbar(args.Position.Y))
				{
					_scrollbarInteracted = true;
					int relX = args.Position.X - Margin.Left - GetGutterWidth();
					var (trackWidth, sbThumbX, sbThumbWidth) = GetHorizontalScrollbarGeometry();
					int maxLineLength = GetMaxLineLength();
					int maxScroll = Math.Max(0, maxLineLength - _effectiveWidth);

					if (relX >= 0 && relX < trackWidth)
					{
						if (relX == 0 && _horizontalScrollOffset > 0)
						{
							// Arrow left
							_horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - ControlDefaults.DefaultScrollWheelLines);
							Container?.Invalidate(true);
						}
						else if (relX == trackWidth - 1 && _horizontalScrollOffset < maxScroll)
						{
							// Arrow right
							_horizontalScrollOffset = Math.Min(maxScroll, _horizontalScrollOffset + ControlDefaults.DefaultScrollWheelLines);
							Container?.Invalidate(true);
						}
						else if (relX >= sbThumbX && relX < sbThumbX + sbThumbWidth)
						{
							// Thumb: start drag
							_isHorizontalScrollbarDragging = true;
							_horizontalScrollbarDragStartX = args.Position.X;
							_horizontalScrollbarDragStartOffset = _horizontalScrollOffset;
						}
						else if (relX < sbThumbX)
						{
							// Track left of thumb: page left
							_horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - _effectiveWidth);
							Container?.Invalidate(true);
						}
						else
						{
							// Track right of thumb: page right
							_horizontalScrollOffset = Math.Min(maxScroll, _horizontalScrollOffset + _effectiveWidth);
							Container?.Invalidate(true);
						}
						return true;
					}
				}
			}

			// Consume other mouse events on scrollbar areas to prevent text cursor positioning
			if (args.HasAnyFlag(MouseFlags.Button1Clicked, MouseFlags.Button1Released,
				MouseFlags.Button1DoubleClicked, MouseFlags.Button1TripleClicked,
				MouseFlags.Button1Dragged))
			{
				if (IsOnVerticalScrollbar(args.Position.X) || IsOnHorizontalScrollbar(args.Position.Y))
					return true;
			}

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Triple-click: select entire line
			if (args.HasFlag(MouseFlags.Button1TripleClicked))
			{
				if (_hasFocus)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					_hasSelection = true;
					_selectionStartX = 0;
					_selectionStartY = _cursorY;
					_selectionEndX = _lines[_cursorY].Length;
					_selectionEndY = _cursorY;
					_cursorX = _lines[_cursorY].Length;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Double-click: select word
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				if (_hasFocus)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					var (wordStart, wordEnd) = WordBoundaryHelper.FindWordAt(_lines[_cursorY], _cursorX);
					_hasSelection = wordStart != wordEnd;
					_selectionStartX = wordStart;
					_selectionStartY = _cursorY;
					_selectionEndX = wordEnd;
					_selectionEndY = _cursorY;
					_cursorX = wordEnd;
					Container?.Invalidate(true);
				}
				MouseDoubleClick?.Invoke(this, args);
				return true;
			}

			// Mouse drag: extend selection.
			// Checked before Button1Pressed because SGR mouse format emits Button1Pressed|Button1Dragged
			// together for every motion-while-button-held event.
			if (args.HasFlag(MouseFlags.Button1Dragged) && _isDragging)
			{
				PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
				_selectionEndX = _cursorX;
				_selectionEndY = _cursorY;
				_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
				EnsureCursorVisible();
				Container?.Invalidate(true);
				return true;
			}

			// Mouse button pressed: start new selection or extend as SGR drag.
			// SGR mouse format sends Button1Pressed|ReportMousePosition (no Button1Dragged)
			// for every motion-while-held event. When _isDragging is already true we are in
			// a drag continuation, so extend the selection instead of resetting the anchor.
			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (_hasFocus && !_readOnly)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					if (_isDragging)
					{
						// SGR drag continuation — extend selection end
						_selectionEndX = _cursorX;
						_selectionEndY = _cursorY;
						_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
					}
					else
					{
						// Fresh press — anchor the selection start
						_scrollbarInteracted = false;
						_hasSelection = true;
						_selectionStartX = _cursorX;
						_selectionStartY = _cursorY;
						_selectionEndX = _cursorX;
						_selectionEndY = _cursorY;
						_isDragging = true;
					}
					EnsureCursorVisible();
					Container?.Invalidate(true);
				}
				return true;
			}

			// Single click / end of drag
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (_hasFocus && !_readOnly)
				{
					IsEditing = true;
					if (_isDragging && (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY))
					{
						// Release after drag: finalise selection end at release point
						PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
						_selectionEndX = _cursorX;
						_selectionEndY = _cursorY;
						_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
						EnsureCursorVisible();
						Container?.Invalidate(true);
					}
					else
					{
						// Simple click: place cursor and clear any stale selection
						PositionCursorFromMouse(args.Position.X, args.Position.Y);
					}
				}
				_isDragging = false;
				MouseClick?.Invoke(this, args);
				return true;
			}

			// Mouse button released: end drag
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				_isDragging = false;
				return true;
			}

			// Mouse wheel up
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, _verticalScrollOffset);
				if (scrollAmount > 0)
				{
					_scrollbarInteracted = true;
					_skipUpdateScrollPositionsInRender = true;
					_verticalScrollOffset -= scrollAmount;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Mouse wheel down
			if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int totalLines = GetTotalWrappedLineCount();
				int maxScroll = Math.Max(0, totalLines - GetEffectiveViewportHeight());
				int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, maxScroll - _verticalScrollOffset);
				if (scrollAmount > 0)
				{
					_scrollbarInteracted = true;
					_skipUpdateScrollPositionsInRender = true;
					_verticalScrollOffset += scrollAmount;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Regular mouse move (no drag)
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return false;
			}

			return false;
		}

		/// <summary>
		/// Core positioning: maps control-relative mouse coordinates to cursor position.
		/// Does NOT clear selection or invalidate — caller decides.
		/// </summary>
		private void PositionCursorFromMouseCore(int mouseX, int mouseY)
		{
			int relX = mouseX - Margin.Left - GetGutterWidth();
			int relY = mouseY - Margin.Top;

			if (relX < 0) relX = 0;
			if (relY < 0) relY = 0;

			if (_wrapMode == WrapMode.NoWrap)
			{
				_cursorY = Math.Min(_lines.Count - 1, relY + _verticalScrollOffset);
				_cursorX = Math.Min(_lines[_cursorY].Length, relX + _horizontalScrollOffset);
			}
			else
			{
				var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
				int wrappedIndex = Math.Clamp(relY + _verticalScrollOffset, 0, wrappedLines.Count - 1);

				var wl = wrappedLines[wrappedIndex];
				_cursorY = wl.SourceLineIndex;
				_cursorX = Math.Min(wl.SourceCharOffset + relX, wl.SourceCharOffset + wl.Length);
				_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
			}
		}

		/// <summary>
		/// Maps control-relative mouse coordinates to cursor position, clears selection.
		/// Used for simple click positioning.
		/// </summary>
		private void PositionCursorFromMouse(int mouseX, int mouseY)
		{
			_scrollbarInteracted = false;
			PositionCursorFromMouseCore(mouseX, mouseY);
			ClearSelection();
			EnsureCursorVisible();
			Container?.Invalidate(true);
		}
	}
}
