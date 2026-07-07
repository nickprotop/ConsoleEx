// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Sub-regions of a <see cref="ScrollablePanelControl"/> that own a mouse gesture: the vertical or
	/// horizontal scrollbar, or the scrollable content area (which forwards to children).
	/// </summary>
	internal enum SpcGestureRegion
	{
		/// <summary>The scrollable content area (forwarded to child controls).</summary>
		Content,

		/// <summary>The vertical scrollbar track.</summary>
		VScrollbar,

		/// <summary>The horizontal scrollbar track.</summary>
		HScrollbar
	}

	public partial class ScrollablePanelControl
	{
		#region IMouseAwareControl Implementation

		// Routes every Button1 press/drag/release through a single captured sub-region so a scrollbar
		// thumb-drag whose pointer leaves the track cannot re-hit-test into content (SGR re-sends
		// Button1Pressed on every motion-while-held).
		private readonly MouseGestureCapture<SpcGestureRegion> _gesture = new();

		// Thumb-drag latches: set only when a Down starts a THUMB drag (not an arrow/page click), so a
		// subsequent Move applies the drag delta rather than a stray track interaction.
		private bool _vThumbDragging;
		private bool _hThumbDragging;

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;  // Always want mouse events for child focus

		/// <inheritdoc/>
		public bool CanFocusWithMouse
		{
			get
			{
				var result = CanReceiveFocus;
				GetConsoleWindowSystem?.LogService?.LogTrace($"ScrollPanel.CanFocusWithMouse: {result}", "Focus");
				return result;
			}
		}

		/// <inheritdoc/>
		/// <summary>
		/// Gets the target child control for a click event, using cached target for double-click sequences.
		/// This prevents the same logical double-click from being dispatched to different children
		/// when scroll position changes between clicks.
		/// </summary>
		/// <summary>
		/// Finds the child control at the given mouse position, accounting for scroll offset and margins.
		/// Used for routing scroll events to the child under the cursor (not just the focused child).
		/// </summary>
		private IWindowControl? GetChildAtContentPosition(System.Drawing.Point mousePosition)
		{
			int contentWidth = VisibleContentWidth;

			int viewportX = mousePosition.X - Margin.Left - ContentInsetLeft;
			if (viewportX < 0 || viewportX >= contentWidth || mousePosition.Y < Margin.Top + ContentInsetTop)
				return null;

			int contentY = mousePosition.Y - Margin.Top - ContentInsetTop + _verticalScrollOffset;

			foreach (var slot in GetVisibleChildLayout(contentWidth))
			{
				if (contentY >= slot.Top && contentY < slot.Top + slot.Height)
					return slot.Control;
			}

			return null;
		}

		/// <summary>
		/// Creates a MouseEventArgs with coordinates translated to be relative to the given child control.
		/// </summary>
		private MouseEventArgs? CreateChildRelativeArgs(MouseEventArgs args, IWindowControl child)
		{
			int contentWidth = VisibleContentWidth;

			// Content-space X/Y: undo the panel's scroll offsets so coordinates are child-relative
			// regardless of how far the panel is scrolled (Bug C — horizontal term).
			int contentX = args.Position.X - Margin.Left - ContentInsetLeft + _horizontalScrollOffset;
			int contentY = args.Position.Y - Margin.Top - ContentInsetTop + _verticalScrollOffset;

			foreach (var slot in GetVisibleChildLayout(contentWidth))
			{
				if (slot.Control == child)
				{
					var childPosition = new System.Drawing.Point(contentX, contentY - slot.Top);
					return args.WithPosition(childPosition);
				}
			}

			return null;
		}

		private IWindowControl? GetClickTargetChild(MouseEventArgs args, int contentWidth)
		{
			var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;
			var positionChanged = _lastClickPosition != args.Position;

			bool isSequentialClick =
				timeSinceLastClick < Configuration.ControlDefaults.DefaultDoubleClickThresholdMs &&
				!positionChanged;

			// Reuse cached target for double-click sequences
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			if (isSequentialClick && _lastClickTarget != null)
			{
				// Verify child still exists
				if (snapshot.Contains(_lastClickTarget))
					return _lastClickTarget;
			}

			// New click sequence - perform fresh hit test using current scroll offset
			int contentY = args.Position.Y - Margin.Top - ContentInsetTop + _verticalScrollOffset;

			foreach (var slot in GetVisibleChildLayout(contentWidth))
			{
				if (contentY >= slot.Top && contentY < slot.Top + slot.Height)
				{
					// Found the clicked child
					_lastClickTarget = slot.Control;
					_lastClickTime = DateTime.Now;
					_lastClickPosition = args.Position;
					return slot.Control;
				}
			}

			// No child found at this position
			_lastClickTarget = null;
			_lastClickTime = DateTime.Now;
			_lastClickPosition = args.Position;
			return null;
		}

		/// <inheritdoc />
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			var log = GetConsoleWindowSystem?.LogService;

			if (args.Handled) return false;

			// Handle mouse wheel scrolling
			{
				bool isWheel = args.HasFlag(Drivers.MouseFlags.WheeledUp) || args.HasFlag(Drivers.MouseFlags.WheeledDown);
				if (isWheel)
				{
					// Re-sync viewport/content metrics from this panel's ARRANGED node bounds before the
					// scroll decision. A MEASURE pass resolves metrics against the unbounded full-content
					// box; when a later paint is culled (e.g. an ancestor clip is empty) the measure-time
					// values persist, leaving _viewportHeight == content height so the panel wrongly
					// believes it cannot scroll and bubbles the wheel to an outer container. Resolving from
					// the arranged bounds here makes the decision use the real on-screen viewport.
					SyncMetricsFromArrangedBounds();

					// Forward to child under mouse cursor first (e.g. ListControl with internal scroll).
					// This lets the child handle its own item scrolling before SPC attempts
					// to scroll the panel viewport. Uses hit-testing by position rather than
					// focus state, consistent with how HorizontalGridControl routes scroll events.
					var childUnderMouse = GetChildAtContentPosition(args.Position);
					if (childUnderMouse is IMouseAwareControl childMouse && childMouse.WantsMouseEvents)
					{
						var childArgs = CreateChildRelativeArgs(args, childUnderMouse);
						bool childResult = childArgs != null && childMouse.ProcessMouseEvent(childArgs);
						if (childResult)
							return true;
					}

					// Fall back to SPC viewport scroll (only if mouse wheel scrolling is enabled)
					if (_enableMouseWheel)
					{
						if (args.HasFlag(Drivers.MouseFlags.WheeledUp))
						{
							bool willScroll = _verticalScrollOffset > 0;
							if (willScroll)
							{
								ScrollVerticalBy(-ControlDefaults.DefaultScrollWheelLines);
								args.Handled = true;
								return true;
							}
							return false;
						}
						else if (args.HasFlag(Drivers.MouseFlags.WheeledDown))
						{
							int maxScroll = Math.Max(0, _contentHeight - VisibleContentHeight);
							bool willScroll = _verticalScrollOffset < maxScroll;
							if (willScroll)
							{
								ScrollVerticalBy(ControlDefaults.DefaultScrollWheelLines);
								args.Handled = true;
								return true;
							}
							return false;
						}
					}

					return false;
				}
			}

			// --- Button1 gesture routing (press / drag / release / click) ---
			// A fresh press hit-tests one of { VScrollbar, HScrollbar, Content } and captures it; every
			// subsequent resent press/drag routes to the captured region WITHOUT re-hit-testing. This is what
			// stops a scrollbar thumb-drag from re-hit-testing into content when the pointer leaves the track
			// (SGR re-sends Button1Pressed on every motion-while-held).
			if (args.HasAnyFlag(Drivers.MouseFlags.Button1Pressed, Drivers.MouseFlags.Button1Dragged,
				Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1Clicked))
			{
				var route = _gesture.Route(args, HitTestRegion);

				// A self-contained Button1Clicked with no preceding press (nothing captured) is a collapsed
				// press+release: hit-test and dispatch a Down to that region. Arrow/track clicks act on Down,
				// so this reproduces the pre-capture click behaviour without leaving a capture latched.
				if (route.Phase == GesturePhase.None && args.HasFlag(Drivers.MouseFlags.Button1Clicked))
					route = new GestureRoute<SpcGestureRegion>(GesturePhase.Down, HitTestRegion(args));

				if (route.Phase != GesturePhase.None)
				{
					return route.Region switch
					{
						SpcGestureRegion.VScrollbar => HandleVScrollbar(route.Phase, args),
						SpcGestureRegion.HScrollbar => HandleHScrollbar(route.Phase, args),
						_ => HandleContent(route.Phase, args),
					};
				}
			}

			// --- Non-gesture button clicks (Button2 / Button3, plus Button1 double/triple) ---
			// These are not part of the Button1 press/drag/release gesture the router recognises, so they
			// route straight to the child-focus/forward path (the content area) as before.
			if (args.HasAnyFlag(Drivers.MouseFlags.Button1DoubleClicked, Drivers.MouseFlags.Button1TripleClicked,
				Drivers.MouseFlags.Button2Clicked, Drivers.MouseFlags.Button2Pressed, Drivers.MouseFlags.Button2Released,
				Drivers.MouseFlags.Button2DoubleClicked, Drivers.MouseFlags.Button2TripleClicked,
				Drivers.MouseFlags.Button3Clicked, Drivers.MouseFlags.Button3Pressed, Drivers.MouseFlags.Button3Released,
				Drivers.MouseFlags.Button3DoubleClicked, Drivers.MouseFlags.Button3TripleClicked))
			{
				// A double/triple-click on a scrollbar track must be consumed (not forwarded to a child), as
				// the pre-capture handler did. Single-click gesture routing above already covers presses.
				if ((NeedsVerticalScrollbar || NeedsHorizontalScrollbar) &&
					HitTestRegion(args) != SpcGestureRegion.Content &&
					args.HasAnyFlag(Drivers.MouseFlags.Button1DoubleClicked, Drivers.MouseFlags.Button1TripleClicked))
				{
					args.Handled = true;
					return true;
				}

				if (ForwardClickToChildForFocus(args))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Maps a fresh Button1 press position to the sub-region that should own the gesture, in the same
		/// priority order the pre-capture handler used: vertical scrollbar, then horizontal scrollbar, else
		/// content. Called ONLY on a fresh press by <see cref="MouseGestureCapture{TRegion}"/>; never
		/// re-invoked mid-gesture — which is what stops a resent-press-on-motion from leaking a scrollbar
		/// thumb-drag into the content pass-through when the pointer leaves the track.
		/// </summary>
		private SpcGestureRegion HitTestRegion(MouseEventArgs args)
		{
			if (NeedsVerticalScrollbar)
			{
				var (_, _, sbHeight, _, _) = GetScrollbarGeometry();
				int viewportX = args.Position.X - Margin.Left - ContentInsetLeft;
				int relY = args.Position.Y - Margin.Top - ContentInsetTop;
				if (viewportX >= VisibleContentWidth && relY >= 0 && relY < sbHeight)
					return SpcGestureRegion.VScrollbar;
			}

			if (NeedsHorizontalScrollbar)
			{
				var (_, hRelY, hTrackWidth, _, _) = GetHScrollbarGeometry();
				int relY = args.Position.Y - Margin.Top - ContentInsetTop;
				int relX = args.Position.X - Margin.Left - ContentInsetLeft;
				if (relY == (hRelY - Margin.Top - ContentInsetTop) && relX >= 0 && relX < hTrackWidth)
					return SpcGestureRegion.HScrollbar;
			}

			return SpcGestureRegion.Content;
		}

		// --- Per-region gesture handlers (built from the pre-capture bodies) ---

		/// <summary>
		/// Vertical scrollbar gesture: Down = arrow / thumb-start / track-page; Move = apply the thumb-drag
		/// delta if a thumb drag was started on Down; Up = end. The capture keeps a thumb drag glued to the
		/// scrollbar even when the pointer leaves the track column.
		/// </summary>
		private bool HandleVScrollbar(GesturePhase phase, MouseEventArgs args)
		{
			switch (phase)
			{
				case GesturePhase.Down:
					_vThumbDragging = false;
					{
						var (_, _, sbHeight, sbThumbY, sbThumbHeight) = GetScrollbarGeometry();
						int relY = args.Position.Y - Margin.Top - ContentInsetTop;
						int maxScroll = Math.Max(0, _contentHeight - VisibleContentHeight);

						if (relY >= sbThumbY && relY < sbThumbY + sbThumbHeight)
						{
							// Thumb: start drag
							_vThumbDragging = true;
							_scrollbarDragStartY = args.Position.Y;
							_scrollbarDragStartThumbPos = sbThumbY;
						}
						else if (relY == 0 && _verticalScrollOffset > 0)
						{
							// Arrow up
							ScrollVerticalBy(-ControlDefaults.DefaultScrollWheelLines);
						}
						else if (relY == sbHeight - 1 && _verticalScrollOffset < maxScroll)
						{
							// Arrow down
							ScrollVerticalBy(ControlDefaults.DefaultScrollWheelLines);
						}
						else if (relY < sbThumbY)
						{
							// Track above thumb: page up
							ScrollVerticalBy(-VisibleContentHeight);
						}
						else if (relY >= sbThumbY + sbThumbHeight)
						{
							// Track below thumb: page down
							ScrollVerticalBy(VisibleContentHeight);
						}
					}
					args.Handled = true;
					return true;

				case GesturePhase.Move:
					// Only a thumb-drag (not an arrow/page Down) tracks subsequent motion. The captured region
					// keeps the drag glued to the scrollbar even when the pointer leaves the track column.
					if (_vThumbDragging)
					{
						var (_, _, sbHeight, _, _) = GetScrollbarGeometry();
						// Map the dragged-to track row back to an offset using the SAME geometry the thumb
						// was drawn with, so the thumb tracks the cursor and round-trips (Bug D).
						int trackRowFromStart = _scrollbarDragStartThumbPos + (args.Position.Y - _scrollbarDragStartY);
						int newOffset = OffsetForThumbPos(sbHeight, sbHeight, _contentHeight, trackRowFromStart);
						ScrollVerticalTo(newOffset);
					}
					args.Handled = true;
					return true;

				default: // Up
					_vThumbDragging = false;
					args.Handled = true;
					return true;
			}
		}

		/// <summary>
		/// Horizontal scrollbar gesture: Down = arrow / thumb-start / track-page; Move = apply the thumb-drag
		/// delta if a thumb drag was started on Down; Up = end (mirrors <see cref="HandleVScrollbar"/>).
		/// </summary>
		private bool HandleHScrollbar(GesturePhase phase, MouseEventArgs args)
		{
			switch (phase)
			{
				case GesturePhase.Down:
					_hThumbDragging = false;
					{
						var (_, _, hTrackWidth, hThumbX, hThumbWidth) = GetHScrollbarGeometry();
						int relX = args.Position.X - Margin.Left - ContentInsetLeft;

						if (relX >= hThumbX && relX < hThumbX + hThumbWidth)
						{
							// Thumb: start drag
							_hThumbDragging = true;
							_scrollbarDragStartX = args.Position.X;
							_hScrollbarDragStartThumbPos = hThumbX;
						}
						else if (relX == 0 && _horizontalScrollOffset > 0)
						{
							// Left arrow
							ScrollHorizontalBy(-ControlDefaults.DefaultScrollWheelLines);
						}
						else if (relX == hTrackWidth - 1 && CanScrollRight)
						{
							// Right arrow
							ScrollHorizontalBy(ControlDefaults.DefaultScrollWheelLines);
						}
						else if (relX < hThumbX)
						{
							// Track left of thumb: page left
							ScrollHorizontalBy(-VisibleContentWidth);
						}
						else if (relX >= hThumbX + hThumbWidth)
						{
							// Track right of thumb: page right
							ScrollHorizontalBy(VisibleContentWidth);
						}
					}
					args.Handled = true;
					return true;

				case GesturePhase.Move:
					if (_hThumbDragging)
					{
						var (_, _, trackWidth, _, _) = GetHScrollbarGeometry();
						int trackColFromStart = _hScrollbarDragStartThumbPos + (args.Position.X - _scrollbarDragStartX);
						int newOffset = OffsetForThumbPos(trackWidth, trackWidth, _contentWidth, trackColFromStart);
						ScrollHorizontalTo(newOffset);
					}
					args.Handled = true;
					return true;

				default: // Up
					_hThumbDragging = false;
					args.Handled = true;
					return true;
			}
		}

		/// <summary>
		/// Content gesture: forwards press/drag/release to the child under the ORIGINAL press. The child
		/// capture (<see cref="_mouseCaptureChild"/>, set on the child's Down and cleared on Up) guarantees
		/// every event of a captured content gesture reaches the same child, even if the pointer moves off it.
		/// </summary>
		private bool HandleContent(GesturePhase phase, MouseEventArgs args)
		{
			// Route drag/press/release to the child that started the gesture, preventing sibling controls
			// from stealing the drag via re-hit-testing.
			if (_mouseCaptureChild != null && phase != GesturePhase.Down)
			{
				if (phase == GesturePhase.Up)
				{
					var releasedChild = _mouseCaptureChild;
					_mouseCaptureChild = null;
					if (releasedChild is IMouseAwareControl releasedMouse && releasedMouse.WantsMouseEvents)
					{
						var childArgs = CreateChildRelativeArgs(args, releasedChild);
						if (childArgs != null)
						{
							releasedMouse.ProcessMouseEvent(childArgs);
							args.Handled = true;
						}
					}
					return true;
				}

				if (_mouseCaptureChild is IMouseAwareControl capturedMouse && capturedMouse.WantsMouseEvents)
				{
					var childArgs = CreateChildRelativeArgs(args, _mouseCaptureChild);
					if (childArgs != null)
					{
						bool result = capturedMouse.ProcessMouseEvent(childArgs);
						if (result) args.Handled = true;
						return result;
					}
				}
			}

			// Fresh press/click with no captured child (Down): hit-test and forward to the child under the
			// cursor. Returns the pre-capture result directly (unhandled presses fell through to `return
			// false`). A Move/Up with no captured child had nothing to forward — treat as unhandled.
			if (phase == GesturePhase.Down)
				return ForwardClickToChildForFocus(args);

			return false;
		}

		/// <summary>
		/// Hit-tests the content area for the child under the cursor, sets focus, and forwards the mouse event
		/// to it (setting child capture on a fresh press). Returns <c>true</c> if the event was handled.
		/// This is the pre-capture "click events for child focus" body, unchanged.
		/// </summary>
		private bool ForwardClickToChildForFocus(MouseEventArgs args)
		{
			var log = GetConsoleWindowSystem?.LogService;
			var currentFocused = GetFocusedChildFromCoordinator();
			log?.LogTrace($"ScrollPanel.ProcessMouseEvent: click pos={args.Position} HasFocus={HasFocus} focusedChild={currentFocused?.GetType().Name ?? "null"}", "Focus");
			// Visible content width (accounting for the vertical scrollbar columns)
			int contentWidth = VisibleContentWidth;

			// Translate mouse position to viewport coordinates
			int viewportX = args.Position.X - Margin.Left - ContentInsetLeft;

			// Check if click is within content area (not in scrollbar, border, or margins)
			if (viewportX >= 0 && viewportX < contentWidth && args.Position.Y >= Margin.Top + ContentInsetTop)
			{
				// Use click target caching to ensure double-clicks go to the same child
				// even if scroll position changes between clicks
				var child = GetClickTargetChild(args, contentWidth);
				log?.LogTrace($"ScrollPanel.ProcessMouseEvent: GetClickTargetChild={child?.GetType().Name ?? "null (empty space)"}", "Focus");

				if (child != null)
				{
					// Set focus on clicked child (if directly focusable or a container with focusable descendants)
					bool directlyFocusable = child is IFocusableControl focusable && focusable.CanReceiveFocus;
					bool containerWithFocusableChildren = !directlyFocusable && CanChildReceiveFocus(child);

					if (directlyFocusable || containerWithFocusableChildren)
					{
						// Clear focus from other children
						List<IWindowControl> mouseSnapshot;
						lock (_childrenLock) { mouseSnapshot = new List<IWindowControl>(_children); }
						foreach (var otherChild in mouseSnapshot)
						{
							if (otherChild != child && otherChild is IFocusableControl fc && (this.GetParentWindow()?.FocusManager.IsFocused(fc) ?? false))
							{
								fc.Container?.Invalidate(Invalidation.Repaint);
							}
						}

						if (directlyFocusable)
						{
							// Set focus on clicked child directly
							this.GetParentWindow()?.FocusManager.SetFocus((IFocusableControl)child, FocusReason.Mouse);
							// Correct the coordinator path to the actually clicked child.
							// RequestFocus(SPC) → SPC.SetFocus(Programmatic) delegates to the

						}
						// For containers (e.g. HorizontalGrid with CanReceiveFocus=false):
						// Don't call SetFocus — let the mouse forwarding + the container's own
						// mouse focus handling (Fix 1) set focus on the actual child control.

						_lastInternalFocusedChild = null;
						log?.LogTrace($"ScrollPanel.ProcessMouseEvent: focused child {child.GetType().Name}", "Focus");
					}

					// Forward mouse event to child (if it handles mouse events)
					if (child is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
					{
						// Translate coordinates to child-relative using the shared layout
						// so this agrees with paint and the hit-test that found the child.
						var childArgs = CreateChildRelativeArgs(args, child);
						if (childArgs != null && mouseAware.ProcessMouseEvent(childArgs))
						{
							// Set child capture on press to prevent drag stealing
							if (args.HasFlag(Drivers.MouseFlags.Button1Pressed))
								_mouseCaptureChild = child;
							args.Handled = true;
							return true;
						}
					}
				}
				else
				{
					// Clicked on empty space or non-focusable content:
					// Move focus to the panel itself (scroll mode) so children lose focus.
					log?.LogTrace("ScrollPanel.ProcessMouseEvent: click on empty space → entering scroll mode", "Focus");
					var emptyClickWindow = (this as IWindowControl).GetParentWindow();
					if (emptyClickWindow != null && CanReceiveFocus)
					{
						emptyClickWindow.FocusManager.SetFocus(this, FocusReason.Mouse);
					}
					_lastInternalFocusedChild = null;

					Invalidate(Invalidation.Repaint);
					return true;
				}
			}

			return false;
		}

		#endregion
	}
}
