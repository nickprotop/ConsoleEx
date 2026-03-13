// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
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
			bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
			int contentWidth = _viewportWidth;
			if (needsScrollbar)
				contentWidth -= 2;

			int viewportX = mousePosition.X - Margin.Left - ContentInsetLeft;
			if (viewportX < 0 || viewportX >= contentWidth || mousePosition.Y < Margin.Top + ContentInsetTop)
				return null;

			int contentY = mousePosition.Y - Margin.Top - ContentInsetTop + _verticalScrollOffset;
			int currentY = 0;

			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			foreach (var child in snapshot.Where(c => c.Visible))
			{
				int childHeight = MeasureChildHeight(child, contentWidth);
				if (contentY >= currentY && contentY < currentY + childHeight)
					return child;
				currentY += childHeight;
			}

			return null;
		}

		/// <summary>
		/// Creates a MouseEventArgs with coordinates translated to be relative to the given child control.
		/// </summary>
		private MouseEventArgs? CreateChildRelativeArgs(MouseEventArgs args, IWindowControl child)
		{
			bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
			int contentWidth = _viewportWidth;
			if (needsScrollbar)
				contentWidth -= 2;

			int viewportX = args.Position.X - Margin.Left - ContentInsetLeft;
			int contentY = args.Position.Y - Margin.Top - ContentInsetTop + _verticalScrollOffset;
			int currentY = 0;

			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			foreach (var c in snapshot.Where(c => c.Visible))
			{
				if (c == child)
				{
					var childPosition = new System.Drawing.Point(viewportX, contentY - currentY);
					return args.WithPosition(childPosition);
				}
				currentY += MeasureChildHeight(c, contentWidth);
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
			int currentY = 0;

			foreach (var child in snapshot.Where(c => c.Visible))
			{
				int childHeight = MeasureChildHeight(child, contentWidth);

				if (contentY >= currentY && contentY < currentY + childHeight)
				{
					// Found the clicked child
					_lastClickTarget = child;
					_lastClickTime = DateTime.Now;
					_lastClickPosition = args.Position;
					return child;
				}

				currentY += childHeight;
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
					// Forward to child under mouse cursor first (e.g. ListControl with internal scroll).
					// This lets the child handle its own item scrolling before SPC attempts
					// to scroll the panel viewport. Uses hit-testing by position rather than
					// focus state, consistent with how HorizontalGridControl routes scroll events.
					var childUnderMouse = GetChildAtContentPosition(args.Position);
					if (childUnderMouse is IMouseAwareControl childMouse && childMouse.WantsMouseEvents)
					{
						var childArgs = CreateChildRelativeArgs(args, childUnderMouse);
						if (childArgs != null && childMouse.ProcessMouseEvent(childArgs))
							return true;
					}

					// Fall back to SPC viewport scroll (only if mouse wheel scrolling is enabled)
					if (_enableMouseWheel)
					{
						if (args.HasFlag(Drivers.MouseFlags.WheeledUp))
						{
							if (_verticalScrollOffset > 0)
							{
								ScrollVerticalBy(-3);
								args.Handled = true;
								return true;
							}
							return false;
						}
						else if (args.HasFlag(Drivers.MouseFlags.WheeledDown))
						{
							int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);
							if (_verticalScrollOffset < maxScroll)
							{
								ScrollVerticalBy(3);
								args.Handled = true;
								return true;
							}
							return false;
						}
					}

					return false;
				}
			}

			// Handle scrollbar drag-in-progress
			// Button1Dragged = real mouse movement; Button1Pressed = synthetic continuous-press repeats
			if (_isScrollbarDragging && args.HasAnyFlag(Drivers.MouseFlags.Button1Dragged, Drivers.MouseFlags.Button1Pressed))
			{
				var (_, sbTop, sbHeight, _, sbThumbHeight) = GetScrollbarGeometry();
				int deltaY = args.Position.Y - _scrollbarDragStartY;
				int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);
				int trackRange = Math.Max(1, sbHeight - sbThumbHeight);
				int newOffset = _scrollbarDragStartOffset + (int)(deltaY * (double)maxScroll / trackRange);
				ScrollVerticalTo(newOffset);
				args.Handled = true;
				return true;
			}

			// Handle scrollbar drag end
			if (args.HasFlag(Drivers.MouseFlags.Button1Released) && _isScrollbarDragging)
			{
				_isScrollbarDragging = false;
				args.Handled = true;
				return true;
			}

			// Handle scrollbar click/press interactions
			{
				bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
				if (needsScrollbar)
				{
					var (sbRelX, sbTop, sbHeight, sbThumbY, sbThumbHeight) = GetScrollbarGeometry();
					int viewportX = args.Position.X - Margin.Left - ContentInsetLeft;
					int contentWidth = _viewportWidth - 2;
					bool isOnScrollbar = viewportX >= contentWidth;

					if (isOnScrollbar && args.HasFlag(Drivers.MouseFlags.Button1Pressed))
					{
						int relY = args.Position.Y - Margin.Top - ContentInsetTop;
						int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);

						if (relY == 0 && _verticalScrollOffset > 0)
						{
							// Arrow up
							ScrollVerticalBy(-3);
						}
						else if (relY == sbHeight - 1 && _verticalScrollOffset < maxScroll)
						{
							// Arrow down
							ScrollVerticalBy(3);
						}
						else if (relY >= sbThumbY && relY < sbThumbY + sbThumbHeight)
						{
							// Thumb: start drag
							_isScrollbarDragging = true;
							_scrollbarDragStartY = args.Position.Y;
							_scrollbarDragStartOffset = _verticalScrollOffset;
						}
						else if (relY < sbThumbY)
						{
							// Track above thumb: page up
							ScrollVerticalBy(-_viewportHeight);
						}
						else
						{
							// Track below thumb: page down
							ScrollVerticalBy(_viewportHeight);
						}
						args.Handled = true;
						return true;
					}

					if (isOnScrollbar && args.HasAnyFlag(Drivers.MouseFlags.Button1Clicked,
						Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1DoubleClicked,
						Drivers.MouseFlags.Button1TripleClicked))
					{
						// Consume scrollbar click events to prevent propagation to children
						args.Handled = true;
						return true;
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
				log?.LogTrace($"ScrollPanel.ProcessMouseEvent: click pos={args.Position} _hasFocus={_hasFocus} _focusedChild={_focusedChild?.GetType().Name ?? "null"}", "Focus");
				// Calculate content width (accounting for scrollbar)
				int contentWidth = _viewportWidth;
				bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
				if (needsScrollbar)
					contentWidth -= 2;

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
						// Set focus on clicked child (if focusable)
						if (child is IFocusableControl focusable && focusable.CanReceiveFocus)
						{
							// Clear focus from other children
							List<IWindowControl> mouseSnapshot;
							lock (_childrenLock) { mouseSnapshot = new List<IWindowControl>(_children); }
							foreach (var otherChild in mouseSnapshot)
							{
								if (otherChild != child && otherChild is IFocusableControl fc && fc.HasFocus)
								{
									fc.SetFocus(false, FocusReason.Mouse);
								}
							}

							// Set focus on clicked child
							focusable.SetFocus(true, FocusReason.Mouse);
							if (child is IInteractiveControl interactive)
								_focusedChild = interactive;
							// Mark panel as focused so ProcessKey routes keys to _focusedChild.
							// Tab focus calls SetFocus(true) which sets _hasFocus; mouse click
							// skips that path, leaving _hasFocus=false without this line.
							_hasFocus = true;
							_lastInternalFocusedChild = null;
							log?.LogTrace($"ScrollPanel.ProcessMouseEvent: focused child {child.GetType().Name}, _focusedChild={_focusedChild?.GetType().Name}", "Focus");
						}

						// Forward mouse event to child (if it handles mouse events)
						if (child is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
						{
							// Calculate child-relative Y position
							// We need to recalculate the child's position in the current scroll state
							int contentY = args.Position.Y - Margin.Top - ContentInsetTop + _verticalScrollOffset;
							int currentY = 0;
							List<IWindowControl> childSnap;
							lock (_childrenLock) { childSnap = new List<IWindowControl>(_children); }
							foreach (var c in childSnap.Where(c => c.Visible))
							{
								if (c == child)
								{
									// Translate coordinates to child-relative
									var childPosition = new System.Drawing.Point(viewportX, contentY - currentY);
									var childArgs = args.WithPosition(childPosition);

									// Forward event to child
									if (mouseAware.ProcessMouseEvent(childArgs))
									{
										args.Handled = true;
										return true;
									}
									break;
								}
								currentY += MeasureChildHeight(c, contentWidth);
							}
						}
					}
					else
					{
						// Clicked on empty space or non-focusable content:
						// Unfocus all children, clear _focusedChild so arrow keys scroll
						log?.LogTrace("ScrollPanel.ProcessMouseEvent: click on empty space → unfocusing children", "Focus");
						List<IWindowControl> emptySpaceSnap;
						lock (_childrenLock) { emptySpaceSnap = new List<IWindowControl>(_children); }
						foreach (var otherChild in emptySpaceSnap)
						{
							if (otherChild is IFocusableControl fc && fc.HasFocus)
							{
								fc.SetFocus(false, FocusReason.Mouse);
							}
						}
						_focusedChild = null;
						_lastInternalFocusedChild = null;
						Container?.Invalidate(true);
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Measures the height of a child control using the layout pipeline.
		/// </summary>
		private int MeasureChildHeight(IWindowControl child, int availableWidth)
		{
			var childNode = LayoutNodeFactory.CreateSubtree(child);
			childNode.IsVisible = true;
			int maxH = _viewportHeight > 0 ? _viewportHeight : int.MaxValue;
			var constraints = new LayoutConstraints(1, availableWidth, 1, maxH);
			childNode.Measure(constraints);
			return childNode.DesiredSize.Height;
		}

		#endregion
	}
}
