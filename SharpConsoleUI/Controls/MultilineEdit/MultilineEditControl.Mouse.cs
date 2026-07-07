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

		/// <summary>
		/// Maps a fresh Button1 press position to the sub-region that should own the resulting gesture, in the
		/// same priority order the handler used before capture: vertical scrollbar, then horizontal scrollbar,
		/// then gutter (only when a gutter handler is attached), else the text area. Called ONLY on a fresh
		/// press by <see cref="Helpers.MouseGestureCapture{TRegion}"/>; never re-invoked mid-gesture, which is
		/// what stops a resent-press-on-motion from leaking a text drag into the scrollbar/gutter handlers.
		/// </summary>
		private MleGestureRegion HitTestRegion(MouseEventArgs args)
		{
			if (IsOnVerticalScrollbar(args.Position.X))
				return MleGestureRegion.VScrollbar;

			if (IsOnHorizontalScrollbar(args.Position.Y))
				return MleGestureRegion.HScrollbar;

			if (GutterClick != null || GutterClickAsync != null)
			{
				int gutterWidth = GetGutterWidth();
				int gutterX = args.Position.X - Margin.Left;
				if (gutterX >= 0 && gutterX < gutterWidth)
					return MleGestureRegion.Gutter;
			}

			return MleGestureRegion.Text;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// --- Mouse wheel (not a gesture; scroll regardless of capture state) ---

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

			// --- Right-click: not a Button1 gesture; move cursor then fire event ---

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

			// --- Double/triple-click: word/line select in the text area ---
			// These arrive as their own flags (not Button1Pressed), so they are handled before the gesture
			// router (which only recognises press/drag/release). They act on the text area only; a captured
			// scrollbar/gutter gesture never produces these.
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

			// --- Button1 gesture routing (press / drag / release / click) ---
			// A fresh press hit-tests one of the four regions and captures it; every subsequent resent
			// press/drag routes to the captured region WITHOUT re-hit-testing. This is what stops a text
			// drag-select from leaking into the scrollbar/gutter handlers when the pointer crosses them
			// (which the former !_isDragging / !_gutterPressed guards patched piecemeal).
			if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged,
				MouseFlags.Button1Released, MouseFlags.Button1Clicked))
			{
				var route = _gesture.Route(args, HitTestRegion);

				// A bare Button1Clicked with no captured press (some drivers/tests deliver a click without a
				// separate Button1Pressed). MouseGestureCapture only captures on a press, so Route yields None;
				// synthesize a collapsed press+release by hit-testing fresh and dispatching Down then Up to that
				// region, so a plain click still places the caret / does the region's discrete action.
				if (route.Phase == Helpers.GesturePhase.None && args.HasFlag(MouseFlags.Button1Clicked))
				{
					var region = HitTestRegion(args);
					DispatchGesture(Helpers.GesturePhase.Down, region, args);
					return DispatchGesture(Helpers.GesturePhase.Up, region, args);
				}

				if (route.Phase != Helpers.GesturePhase.None)
				{
					return DispatchGesture(route.Phase, route.Region, args);
				}
			}

			// Regular mouse move (no drag)
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return false;
			}

			return false;
		}

		/// <summary>Routes a resolved gesture (region + phase) to its region handler.</summary>
		private bool DispatchGesture(Helpers.GesturePhase phase, MleGestureRegion region, MouseEventArgs args) =>
			region switch
			{
				MleGestureRegion.VScrollbar => HandleVScrollbar(phase, args),
				MleGestureRegion.HScrollbar => HandleHScrollbar(phase, args),
				MleGestureRegion.Gutter => HandleGutter(phase, args),
				_ => HandleTextGesture(phase, args),
			};

		// --- Per-region gesture handlers (built from the pre-capture bodies) ---

		/// <summary>
		/// Vertical scrollbar gesture: Down = arrow / thumb-start / track-page; Move = apply the thumb-drag
		/// delta if a thumb drag was started on Down; Up = end.
		/// </summary>
		private bool HandleVScrollbar(Helpers.GesturePhase phase, MouseEventArgs args)
		{
			switch (phase)
			{
				case Helpers.GesturePhase.Down:
					_scrollbarInteracted = true;
					_vThumbDragging = false;
					{
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
								_vThumbDragging = true;
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
						}
					}
					return true;

				case Helpers.GesturePhase.Move:
					// Only a thumb-drag (not an arrow/page Down) tracks subsequent motion. The captured region
					// keeps the drag glued to the scrollbar even when the pointer leaves the track column.
					if (_vThumbDragging)
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
					}
					return true;

				default: // Up
					_vThumbDragging = false;
					return true;
			}
		}

		/// <summary>
		/// Horizontal scrollbar gesture: Down = arrow / thumb-start / track-page; Move = apply the thumb-drag
		/// delta if a thumb drag was started on Down; Up = end.
		/// </summary>
		private bool HandleHScrollbar(Helpers.GesturePhase phase, MouseEventArgs args)
		{
			switch (phase)
			{
				case Helpers.GesturePhase.Down:
					_scrollbarInteracted = true;
					_hThumbDragging = false;
					{
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
								_hThumbDragging = true;
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
						}
					}
					return true;

				case Helpers.GesturePhase.Move:
					if (_hThumbDragging)
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
					}
					return true;

				default: // Up
					_hThumbDragging = false;
					return true;
			}
		}

		/// <summary>
		/// Gutter gesture: Down fires GutterClick (only a real fresh press reaches here — the capture model
		/// guarantees a text drag crossing the gutter never routes here); Move/Up are consumed.
		/// </summary>
		private bool HandleGutter(Helpers.GesturePhase phase, MouseEventArgs args)
		{
			if (phase == Helpers.GesturePhase.Down)
			{
				Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);

				int gutterX = args.Position.X - Margin.Left;
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
			}

			// Move/Up (and the Down above) all consume — the gutter owns this gesture until release.
			return true;
		}

		/// <summary>
		/// Text-area gesture: Down anchors the selection (registers drag-autoscroll); Move extends it (stores
		/// LastDragRelativeY for autoscroll and keeps the cursor visible); Up finalises and unregisters
		/// autoscroll.
		/// </summary>
		private bool HandleTextGesture(Helpers.GesturePhase phase, MouseEventArgs args)
		{
			switch (phase)
			{
				case Helpers.GesturePhase.Down:
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

						// Fresh press — anchor the selection start
						_scrollbarInteracted = false;
						_hasSelection = true;
						_selectionStartX = _cursorX;
						_selectionStartY = _cursorY;
						_selectionEndX = _cursorX;
						_selectionEndY = _cursorY;
						Container?.GetConsoleWindowSystem?.RegisterDragAutoScroll(this);

						NotifySelectionActive();
						EnsureCursorVisible();
						Invalidate(Invalidation.Relayout);
					}
					return true;

				case Helpers.GesturePhase.Move:
					if (HasFocus)
					{
						if (!_readOnly)
							IsEditing = true;
						PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
						_lastDragRelativeY = args.Position.Y;
						_selectionEndX = _cursorX;
						_selectionEndY = _cursorY;
						_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
						NotifySelectionActive();
						EnsureCursorVisible();
						Invalidate(Invalidation.Relayout);
					}
					return true;

				default: // Up
					if (HasFocus && !_readOnly)
					{
						IsEditing = true;
						if (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY)
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
					Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
					MouseClick?.Invoke(this, args);
					return true;
			}
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
