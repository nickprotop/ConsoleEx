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
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region IMouseAwareControl Implementation

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

			// Handle vertical scrollbar drag-in-progress
			// Button1Dragged = real mouse movement; Button1Pressed = synthetic continuous-press repeats
			if (_isScrollbarDragging && args.HasAnyFlag(Drivers.MouseFlags.Button1Dragged, Drivers.MouseFlags.Button1Pressed))
			{
				var (_, sbTop, sbHeight, _, _) = GetScrollbarGeometry();
				// Map the dragged-to track row back to an offset using the SAME geometry the thumb
				// was drawn with, so the thumb tracks the cursor and round-trips (Bug D).
				int trackRowFromStart = (_scrollbarDragStartThumbPos + (args.Position.Y - _scrollbarDragStartY));
				int newOffset = OffsetForThumbPos(sbHeight, sbHeight, _contentHeight, trackRowFromStart);
				ScrollVerticalTo(newOffset);
				args.Handled = true;
				return true;
			}

			// Handle vertical scrollbar drag end
			if (args.HasFlag(Drivers.MouseFlags.Button1Released) && _isScrollbarDragging)
			{
				_isScrollbarDragging = false;
				args.Handled = true;
				return true;
			}

			// Handle horizontal scrollbar drag-in-progress
			if (_isHScrollbarDragging && args.HasAnyFlag(Drivers.MouseFlags.Button1Dragged, Drivers.MouseFlags.Button1Pressed))
			{
				var (_, _, trackWidth, _, _) = GetHScrollbarGeometry();
				int trackColFromStart = (_hScrollbarDragStartThumbPos + (args.Position.X - _scrollbarDragStartX));
				int newOffset = OffsetForThumbPos(trackWidth, trackWidth, _contentWidth, trackColFromStart);
				ScrollHorizontalTo(newOffset);
				args.Handled = true;
				return true;
			}

			// Handle horizontal scrollbar drag end
			if (args.HasFlag(Drivers.MouseFlags.Button1Released) && _isHScrollbarDragging)
			{
				_isHScrollbarDragging = false;
				args.Handled = true;
				return true;
			}

			// Handle vertical scrollbar click/press interactions
			if (NeedsVerticalScrollbar)
			{
				var (sbRelX, sbTop, sbHeight, sbThumbY, sbThumbHeight) = GetScrollbarGeometry();
				int viewportX = args.Position.X - Margin.Left - ContentInsetLeft;
				bool isOnScrollbar = viewportX >= VisibleContentWidth;
				int relY = args.Position.Y - Margin.Top - ContentInsetTop;
				bool inTrackRows = relY >= 0 && relY < sbHeight;

				// Thumb drag initiation (needs Button1Pressed for responsive dragging)
				if (isOnScrollbar && inTrackRows && args.HasFlag(Drivers.MouseFlags.Button1Pressed))
				{
					if (relY >= sbThumbY && relY < sbThumbY + sbThumbHeight)
					{
						_isScrollbarDragging = true;
						_scrollbarDragStartY = args.Position.Y;
						_scrollbarDragStartThumbPos = sbThumbY;
						args.Handled = true;
						return true;
					}
				}

				// Arrow and track clicks (Button1Clicked only to avoid double-firing)
				if (isOnScrollbar && inTrackRows && args.HasFlag(Drivers.MouseFlags.Button1Clicked))
				{
					int maxScroll = Math.Max(0, _contentHeight - VisibleContentHeight);

					if (relY == 0 && _verticalScrollOffset > 0)
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
					args.Handled = true;
					return true;
				}

				if (isOnScrollbar && inTrackRows && args.HasAnyFlag(
					Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1DoubleClicked,
					Drivers.MouseFlags.Button1TripleClicked))
				{
					// Consume scrollbar events to prevent propagation to children
					args.Handled = true;
					return true;
				}
			}

			// Handle horizontal scrollbar click/press interactions (mirrors the vertical block).
			if (NeedsHorizontalScrollbar)
			{
				var (hRelX, hRelY, hTrackWidth, hThumbX, hThumbWidth) = GetHScrollbarGeometry();
				int relY = args.Position.Y - Margin.Top - ContentInsetTop;
				int relX = args.Position.X - Margin.Left - ContentInsetLeft;
				bool isOnHScrollbar = relY == (hRelY - Margin.Top - ContentInsetTop) && relX >= 0 && relX < hTrackWidth;

				// Thumb drag initiation
				if (isOnHScrollbar && args.HasFlag(Drivers.MouseFlags.Button1Pressed))
				{
					if (relX >= hThumbX && relX < hThumbX + hThumbWidth)
					{
						_isHScrollbarDragging = true;
						_scrollbarDragStartX = args.Position.X;
						_hScrollbarDragStartThumbPos = hThumbX;
						args.Handled = true;
						return true;
					}
				}

				// Arrow and track clicks
				if (isOnHScrollbar && args.HasFlag(Drivers.MouseFlags.Button1Clicked))
				{
					if (relX == 0 && _horizontalScrollOffset > 0)
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
					args.Handled = true;
					return true;
				}

				if (isOnHScrollbar && args.HasAnyFlag(
					Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1DoubleClicked,
					Drivers.MouseFlags.Button1TripleClicked))
				{
					args.Handled = true;
					return true;
				}
			}

			// Child mouse capture: route drag/press/release to the child that started the drag,
			// preventing sibling controls from stealing the drag via re-hit-testing.
			if (_mouseCaptureChild != null &&
				args.HasAnyFlag(Drivers.MouseFlags.Button1Pressed, Drivers.MouseFlags.Button1Dragged, Drivers.MouseFlags.Button1Released))
			{
				if (args.HasFlag(Drivers.MouseFlags.Button1Released))
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

			// Handle click events for child focus
			if (args.HasAnyFlag(Drivers.MouseFlags.Button1Clicked, Drivers.MouseFlags.Button1Pressed,
				Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1DoubleClicked, Drivers.MouseFlags.Button1TripleClicked,
				Drivers.MouseFlags.Button2Clicked, Drivers.MouseFlags.Button2Pressed, Drivers.MouseFlags.Button2Released,
				Drivers.MouseFlags.Button2DoubleClicked, Drivers.MouseFlags.Button2TripleClicked,
				Drivers.MouseFlags.Button3Clicked, Drivers.MouseFlags.Button3Pressed, Drivers.MouseFlags.Button3Released,
				Drivers.MouseFlags.Button3DoubleClicked, Drivers.MouseFlags.Button3TripleClicked))
			{
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
									fc.Container?.Invalidate(true);
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

						Container?.Invalidate(true);
						return true;
					}
				}
			}

			return false;
		}

		#endregion
	}
}
