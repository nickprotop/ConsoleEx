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
using SharpConsoleUI.Extensions;
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
				Invalidate(Invalidation.Relayout);
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
				Invalidate(Invalidation.Relayout);
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

			// Skip scrollbar interaction while a text drag-selection is already in progress. SGR mouse format
			// re-sends Button1Pressed for every motion-while-held event, so a slow downward drag-select that
			// steps onto the horizontal-scrollbar row (which sits at the viewport bottom) would otherwise be
			// mistaken for a scrollbar thumb-press — flipping _isHorizontalScrollbarDragging on so every
			// subsequent event is hijacked into horizontal scrolling instead of extending the selection. That
			// froze the drag Y at the last in-viewport row, so drag-autoscroll never fired. A FAST drag that
			// jumps over the scrollbar row never lands on it and works, hence "down-autoscroll needs high
			// velocity". Once a drag-select has started, the pointer crossing the scrollbar area must fall
			// through to the drag-extend/autoscroll path below.
			if (args.HasFlag(MouseFlags.Button1Pressed) && HasFocus && !_isDragging)
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
							Invalidate(Invalidation.Relayout);
						}
						else if (relY == trackHeight - 1 && _verticalScrollOffset < maxScroll)
						{
							// Arrow down
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = Math.Min(maxScroll, _verticalScrollOffset + ControlDefaults.DefaultScrollWheelLines);
							Invalidate(Invalidation.Relayout);
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
							Invalidate(Invalidation.Relayout);
						}
						else
						{
							// Track below thumb: page down
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = Math.Min(maxScroll, _verticalScrollOffset + effectiveViewport);
							Invalidate(Invalidation.Relayout);
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
							Invalidate(Invalidation.Relayout);
						}
						else if (relX == trackWidth - 1 && _horizontalScrollOffset < maxScroll)
						{
							// Arrow right
							_horizontalScrollOffset = Math.Min(maxScroll, _horizontalScrollOffset + ControlDefaults.DefaultScrollWheelLines);
							Invalidate(Invalidation.Relayout);
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
							Invalidate(Invalidation.Relayout);
						}
						else
						{
							// Track right of thumb: page right
							_horizontalScrollOffset = Math.Min(maxScroll, _horizontalScrollOffset + _effectiveWidth);
							Invalidate(Invalidation.Relayout);
						}
						return true;
					}
				}
			}

			// Consume other mouse events on scrollbar areas to prevent text cursor positioning — but NOT while a
			// text drag-selection is in progress, so a drag crossing the scrollbar row falls through to the
			// drag-extend/autoscroll path (see the _isDragging note on the scrollbar-interaction block above).
			if (!_isDragging && args.HasAnyFlag(MouseFlags.Button1Clicked, MouseFlags.Button1Released,
				MouseFlags.Button1DoubleClicked, MouseFlags.Button1TripleClicked,
				MouseFlags.Button1Dragged))
			{
				if (IsOnVerticalScrollbar(args.Position.X) || IsOnHorizontalScrollbar(args.Position.Y))
					return true;
			}

			// Handle gutter clicks: fire GutterClick on the first Button1Pressed only,
			// use _gutterPressed guard to prevent re-fire during SGR drag continuation,
			// and consume all Button1 events in gutter area to prevent text selection.
			if (GutterClick != null || GutterClickAsync != null)
			{
				int gutterWidth = GetGutterWidth();
				int gutterX = args.Position.X - Margin.Left;
				bool inGutter = gutterX >= 0 && gutterX < gutterWidth;

				// Fire GutterClick only on a FRESH press in the gutter, never when a text drag-selection is
				// already in progress. SGR mouse format re-sends Button1Pressed for every motion-while-held
				// event, so a drag-select that crosses into the gutter would otherwise be mistaken for a fresh
				// gutter click and (e.g.) toggle a breakpoint mid-drag. A fresh gutter click starts with
				// _isDragging == false; a drag crossing the gutter has _isDragging == true.
				if (inGutter && args.HasFlag(MouseFlags.Button1Pressed) && !_gutterPressed && !_isDragging)
				{
					// First press in gutter — fire GutterClick immediately
					_gutterPressed = true;
					_isDragging = false;
					Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);

					int sourceLineIndex = -1;
					int clickRow = args.Position.Y - Margin.Top + _verticalScrollOffset;
					var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
					if (clickRow >= 0 && clickRow < wrappedLines.Count)
						sourceLineIndex = wrappedLines[clickRow].SourceLineIndex;

					Core.AsyncEvent.Raise(GutterClick, GutterClickAsync, this, new GutterClickEventArgs
					{
						SourceLineIndex = sourceLineIndex,
						GutterX = gutterX,
						MouseEventArgs = args
					}, Container?.GetConsoleWindowSystem?.LogService);
					return true;
				}

				// Consume all other Button1 events in gutter while a GUTTER interaction is pressed (_gutterPressed).
				// An active TEXT drag-selection that merely crosses into the gutter (_isDragging but not
				// _gutterPressed) must fall through to the drag-extend path instead — otherwise crossing the
				// gutter would silently end the selection.
				if (inGutter && _gutterPressed && args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged,
					MouseFlags.Button1Released, MouseFlags.Button1Clicked))
				{
					if (args.HasAnyFlag(MouseFlags.Button1Released, MouseFlags.Button1Clicked))
						_gutterPressed = false;
					_isDragging = false;
					Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
					return true;
				}
			}

			// Handle right-click: move cursor to click position first, then fire event
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				if (HasFocus)
				{
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					ClearSelection();
					EnsureCursorVisible();
					Invalidate(Invalidation.Relayout);
				}
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Triple-click: select entire line
			if (args.HasFlag(MouseFlags.Button1TripleClicked))
			{
				if (HasFocus)
				{
					if (!_readOnly)
						IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					lock (_contentLock)
					{
						_hasSelection = true;
						_selectionStartX = 0;
						_selectionStartY = _cursorY;
						_selectionEndX = _lines[_cursorY].Length;
						_selectionEndY = _cursorY;
						_cursorX = _lines[_cursorY].Length;
					}
					NotifySelectionActive();
					Invalidate(Invalidation.Relayout);
				}
				return true;
			}

			// Double-click: select word
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				if (HasFocus)
				{
					if (!_readOnly)
						IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					lock (_contentLock)
					{
						var (wordStart, wordEnd) = WordBoundaryHelper.FindWordAt(_lines[_cursorY], _cursorX);
						_hasSelection = wordStart != wordEnd;
						_selectionStartX = wordStart;
						_selectionStartY = _cursorY;
						_selectionEndX = wordEnd;
						_selectionEndY = _cursorY;
						_cursorX = wordEnd;
					}
					NotifySelectionActive();
					Invalidate(Invalidation.Relayout);
				}
				MouseDoubleClick?.Invoke(this, args);
				return true;
			}

			// Mouse drag: extend selection.
			// Checked before Button1Pressed because SGR mouse format emits Button1Pressed|Button1Dragged
			// together for every motion-while-button-held event.
			if (args.HasFlag(MouseFlags.Button1Dragged) && _isDragging)
			{
				_lastDragRelativeY = args.Position.Y;
				PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
				_selectionEndX = _cursorX;
				_selectionEndY = _cursorY;
				_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
				NotifySelectionActive();
				EnsureCursorVisible();
				Invalidate(Invalidation.Relayout);
				return true;
			}

			// Mouse button pressed: start new selection or extend as SGR drag.
			// SGR mouse format sends Button1Pressed|ReportMousePosition (no Button1Dragged)
			// for every motion-while-held event. When _isDragging is already true we are in
			// a drag continuation, so extend the selection instead of resetting the anchor.
			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (!HasFocus && CanFocusWithMouse)
				{
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				}

				// Allow mouse selection even when read-only — selecting/copying text must work for
				// read-only editors (issue #36). Editing mode is only entered when not read-only.
				if (HasFocus)
				{
					if (!_readOnly)
						IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					_lastDragRelativeY = args.Position.Y;
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
						Container?.GetConsoleWindowSystem?.RegisterDragAutoScroll(this);
					}
					NotifySelectionActive();
					EnsureCursorVisible();
					Invalidate(Invalidation.Relayout);
				}
				return true;
			}

			// Single click / end of drag
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (HasFocus && !_readOnly)
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
						Invalidate(Invalidation.Relayout);
					}
					else
					{
						// Simple click: place cursor and clear any stale selection
						PositionCursorFromMouse(args.Position.X, args.Position.Y);
					}
				}
				_isDragging = false;
				Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
				MouseClick?.Invoke(this, args);
				return true;
			}

			// Mouse button released: end drag and reset gutter press guard
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				_isDragging = false;
				_gutterPressed = false;
				Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
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
					Invalidate(Invalidation.Relayout);
					return true;
				}
				return false; // at top, bubble to parent
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
					Invalidate(Invalidation.Relayout);
					return true;
				}
				return false; // at bottom, bubble to parent
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

			lock (_contentLock)
			{
				if (_wrapMode == WrapMode.NoWrap)
				{
					_cursorY = Math.Min(_lines.Count - 1, relY + _verticalScrollOffset);
					// relX is a display column; convert to a logical char index so wide
					// (e.g. CJK) characters map correctly. See GitHub issue #23.
					int targetColumn = GetDisplayColumn(_cursorY, _horizontalScrollOffset) + relX;
					_cursorX = GetCharOffsetFromColumn(_cursorY, targetColumn);
				}
				else
				{
					var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
					int wrappedIndex = Math.Clamp(relY + _verticalScrollOffset, 0, wrappedLines.Count - 1);

					var wl = wrappedLines[wrappedIndex];
					_cursorY = wl.SourceLineIndex;
					// relX is a display column within the wrapped segment; convert it to a
					// logical char index, then clamp to the segment end.
					int targetColumn = GetDisplayColumn(_cursorY, wl.SourceCharOffset) + relX;
					_cursorX = Math.Min(GetCharOffsetFromColumn(_cursorY, targetColumn), wl.SourceCharOffset + wl.Length);
					_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
				}
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
			Invalidate(Invalidation.Relayout);
		}
	}
}
