// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using System.Drawing;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Handles event dispatching and routing for window mouse/keyboard events.
	/// Extracted from Window class as part of Phase 3.5 refactoring.
	/// Manages focus, hit testing, and event bubbling.
	/// </summary>
	public class WindowEventDispatcher
	{
		private readonly Window _window;

		// Mouse tracking for enter/leave events
		private Controls.IWindowControl? _lastMouseOverControl;

		// Click target tracking for double-click consistency
		private IWindowControl? _lastClickTarget;
		private DateTime _lastClickTime;
		private Point _lastClickPosition;

		// Mouse capture: routes drag/release events to the control that received Button1Pressed
		private IWindowControl? _mouseCaptureControl;

		// Escape key tracking: remembers control for Tab restore after Escape
		private IInteractiveControl? _escapedFromControl;

		/// <summary>
		/// Initializes a new instance of the WindowEventDispatcher class.
		/// </summary>
		/// <param name="window">The window this dispatcher belongs to</param>
		public WindowEventDispatcher(Window window)
		{
			_window = window;
		}

		/// <summary>
		/// Gets the target control for a click event, using cached target for double-click sequences.
		/// This prevents the same logical double-click from being dispatched to different controls
		/// when layout changes (e.g., scroll) occur between the two clicks.
		/// </summary>
		private IWindowControl? GetClickTarget(Events.MouseEventArgs args)
		{
			var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;
			var positionChanged = _lastClickPosition != args.WindowPosition;

			bool isSequentialClick =
				timeSinceLastClick < Configuration.ControlDefaults.DefaultDoubleClickThresholdMs &&
				!positionChanged;

			// Reuse cached target for double-click sequences
			// We trust the cached target without validation since it's only used for a short time (< 500ms)
			// and prevents double-click events from being dispatched to different controls when layout changes
			if (isSequentialClick && _lastClickTarget != null)
			{
				return _lastClickTarget;
			}

			// New click sequence - perform fresh hit test
			var targetControl = GetControlAtPosition(args.WindowPosition);

			_lastClickTarget = targetControl;
			_lastClickTime = DateTime.Now;
			_lastClickPosition = args.WindowPosition;

			return targetControl;
		}

		/// <summary>
		/// Handles mouse events for this window and propagates them to controls
		/// </summary>
		/// <param name="args">Mouse event arguments with window-relative coordinates</param>
		/// <returns>True if the event was handled</returns>
		public bool ProcessMouseEvent(Events.MouseEventArgs args)
		{
			lock (_window._lock)
			{
				// Ensure layout is current before processing mouse events
				UpdateControlLayout();

				// Route drag/release events to the captured control (mouse capture).
				// This must run before enter/leave tracking to prevent synthetic
				// enter/leave events from interfering with active drags.
				// Include Button1Pressed because SGR mouse format uses Button1Pressed+ReportMousePosition
				// for drag events (without Button1Dragged flag).
				if (_mouseCaptureControl != null && args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged, MouseFlags.Button1Released))
				{
					var capturedControl = _mouseCaptureControl;

					if (args.HasFlag(MouseFlags.Button1Released))
						_mouseCaptureControl = null;

					if (capturedControl is Controls.IMouseAwareControl mouseCapture && mouseCapture.WantsMouseEvents)
					{
						var controlPosition = GetControlRelativePosition(capturedControl, args.WindowPosition);
						var controlArgs = args.WithPosition(controlPosition);
						return mouseCapture.ProcessMouseEvent(controlArgs);
					}
				}

				// Find the control at the current mouse position
				Controls.IWindowControl? currentControl = null;
				if (IsClickInWindowContent(args.WindowPosition))
				{
					currentControl = GetControlAtPosition(args.WindowPosition);
				}

				// Generate enter/leave events when control under mouse changes
				if (currentControl != _lastMouseOverControl)
				{
					// Send leave event to previous control
					if (_lastMouseOverControl != null && _lastMouseOverControl is Controls.IMouseAwareControl leavingControl && leavingControl.WantsMouseEvents)
					{
						var leavePosition = GetControlRelativePosition(_lastMouseOverControl, args.WindowPosition);
						var leaveArgs = args.WithPosition(leavePosition).WithFlags(MouseFlags.MouseLeave);
						leavingControl.ProcessMouseEvent(leaveArgs);
					}

					// Send enter event to new control
					if (currentControl != null && currentControl is Controls.IMouseAwareControl enteringControl && enteringControl.WantsMouseEvents)
					{
						var enterPosition = GetControlRelativePosition(currentControl, args.WindowPosition);
						var enterArgs = args.WithPosition(enterPosition).WithFlags(MouseFlags.MouseEnter);
						enteringControl.ProcessMouseEvent(enterArgs);
					}

					_lastMouseOverControl = currentControl;
				}

				// Check if the click is within the window content area (not borders/title)
				if (IsClickInWindowContent(args.WindowPosition))
				{
					// Hit test - use cached target for click sequences to prevent double-click bugs
					IWindowControl? targetControl;

					// For click events, use cached target to maintain consistency across double-click sequences
					// This prevents the same logical double-click from dispatching to different controls
					// when layout changes (e.g., scroll) occur between clicks
					bool isClickEvent = args.HasAnyFlag(
						MouseFlags.Button1Pressed,
						MouseFlags.Button1Released,
						MouseFlags.Button1Clicked,
						MouseFlags.Button1DoubleClicked,
						MouseFlags.Button1TripleClicked,
						MouseFlags.Button2Pressed,
						MouseFlags.Button2Released,
						MouseFlags.Button2Clicked,
						MouseFlags.Button2DoubleClicked,
						MouseFlags.Button2TripleClicked,
						MouseFlags.Button3Pressed,
						MouseFlags.Button3Released,
						MouseFlags.Button3Clicked,
						MouseFlags.Button3DoubleClicked,
						MouseFlags.Button3TripleClicked,
						MouseFlags.Button4Pressed,
						MouseFlags.Button4Released,
						MouseFlags.Button4Clicked,
						MouseFlags.Button4DoubleClicked,
						MouseFlags.Button4TripleClicked);

					if (isClickEvent)
					{
						targetControl = GetClickTarget(args);
					}
					else
					{
						// For non-click events (scroll, move), always use fresh hit test
						targetControl = GetControlAtPosition(args.WindowPosition);
					}

					// === NEW: SCROLL EVENT BUBBLING ===
					// For scroll events, try bubbling up the parent chain
					if (args.HasAnyFlag(MouseFlags.WheeledUp, MouseFlags.WheeledDown))
					{
						// Build parent chain from deepest to shallowest
						var parentChain = new List<IWindowControl>();
						var current = targetControl;
						while (current != null)
						{
							parentChain.Add(current);
							current = current.Container as IWindowControl;
						}

						// Try each control in chain (deepest first)
						foreach (var control in parentChain)
						{
							if (control is Controls.IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
							{
								// Calculate relative position for this control
								var controlPosition = GetControlRelativePosition(control, args.WindowPosition);
								var controlArgs = args.WithPosition(controlPosition);

								if (mouseAware.ProcessMouseEvent(controlArgs))
								{
									return true; // Event consumed
								}
							}
						}
					}
					else
					{
						// === DISMISS PORTALS ON OUTSIDE CLICK ===
						DismissOutsideClickPortals(args);

						// === EXISTING: NON-SCROLL EVENTS (clicks, etc.) ===
						// Centralized focus handling on click (left-click and right-click)
						if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked,
						                    MouseFlags.Button3Pressed, MouseFlags.Button3Clicked))
						{
							HandleClickFocus(targetControl);
						}

						// Propagate mouse event to control if applicable
						if (targetControl != null && targetControl is Controls.IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
						{
							// Set mouse capture on Button1Pressed so drag/release events route here
							if (args.HasFlag(MouseFlags.Button1Pressed))
								_mouseCaptureControl = targetControl;

							var controlPosition = GetControlRelativePosition(targetControl, args.WindowPosition);
							var controlArgs = args.WithPosition(controlPosition);

							if (mouseAware.ProcessMouseEvent(controlArgs))
								return true;
							// Control didn't handle it — fall through to UnhandledMouseClick
						}

						// Click was on a non-mouse-aware control (focus already handled above)
						// Fire UnhandledMouseClick so listeners (e.g., MenuControl) can dismiss
						if (targetControl != null && targetControl is not Controls.IMouseAwareControl)
						{
							if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked,
							                    MouseFlags.Button3Pressed, MouseFlags.Button3Clicked))
							{
								_window.RaiseUnhandledMouseClick(args);
							}
							return true;
						}

						// Fire UnhandledMouseClick event for clicks on empty/unhandled space
						if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked,
						                    MouseFlags.Button3Pressed, MouseFlags.Button3Clicked))
						{
							_window.RaiseUnhandledMouseClick(args);
							return true; // Considered handled after event fires
						}
					}
				}

				// Handle mouse wheel scrolling in DOM mode
				if (_window._renderer != null)
				{
					if (args.HasFlag(MouseFlags.WheeledUp))
					{
						_window._renderer.ScrollBy(-3);
						_window._invalidated = true;
						_window.IsDirty = true;
						return true;
					}
					else if (args.HasFlag(MouseFlags.WheeledDown))
					{
						_window._renderer.ScrollBy(3);
						_window._invalidated = true;
						_window.IsDirty = true;
						return true;
					}
				}

				return false; // Event not handled
			}
		}

		/// <summary>
		/// Dismisses portals that have DismissOnOutsideClick enabled when a click lands outside their bounds.
		/// Collects targets first to avoid modifying collections during iteration.
		/// </summary>
		private void DismissOutsideClickPortals(MouseEventArgs args)
		{
			var root = _window.RootLayoutNode;
			if (root == null) return;

			if (!args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked,
			                     MouseFlags.Button3Pressed, MouseFlags.Button3Clicked))
				return;

			var contentPos = GetContentCoordinates(args.WindowPosition);
			var toDismiss = new List<LayoutNode>();

			root.Visit(node =>
			{
				foreach (var portal in node.PortalChildren)
				{
					if (portal.Control is IHasPortalBounds hasPortalBounds
						&& hasPortalBounds.DismissOnOutsideClick)
					{
						var bounds = hasPortalBounds.GetPortalBounds();
						if (!bounds.Contains(contentPos))
						{
							toDismiss.Add(portal);
						}
					}
				}
			});

			foreach (var portal in toDismiss)
			{
				if (portal.Control is PortalContentBase portalContent)
					portalContent.RaiseDismissRequested();

				// Window.RemovePortal always removes from root node regardless of ownerControl,
				// so we pass the portal's own control as a placeholder.
				if (portal.Control != null)
					_window.RemovePortal(portal.Control, portal);
			}
		}

		/// <summary>
		/// Checks if a window-relative position is within the content area (not title bar or borders)
		/// </summary>
		private bool IsClickInWindowContent(Point windowPosition)
		{
			// Window content starts at (1,1) to account for borders
			// Title bar is at Y=0, so content starts at Y=1
			return windowPosition.X >= 1 && windowPosition.X < _window.Width - 1 &&
				   windowPosition.Y >= 1 && windowPosition.Y < _window.Height - 1;
		}

		/// <summary>
		/// Converts window-relative coordinates to content coordinates (accounting for borders and scroll)
		/// </summary>
		private Point GetContentCoordinates(Point windowPosition)
		{
			// Subtract border offset only - DOM layout handles scroll offset in absolute bounds
			return new Point(windowPosition.X - 1, windowPosition.Y - 1);
		}

		/// <summary>
		/// Calculates the position relative to a specific control using the new layout system
		/// </summary>
		private Point GetControlRelativePosition(IWindowControl control, Point windowPosition)
		{
		// Use DOM bounds if available (for sticky and container controls)
		var node = _window._renderer?.GetLayoutNode(control);
		if (node != null)
		{
		  // Convert window position to content position
			var contentPos = GetContentCoordinates(windowPosition);

			// Make relative to control's AbsoluteBounds
			return new Point(
				contentPos.X - node.AbsoluteBounds.X,
				contentPos.Y - node.AbsoluteBounds.Y
			);
		}

		// Fallback to layout manager for controls not in DOM
		var bounds = _window._layoutManager.GetOrCreateControlBounds(control);
		return bounds.WindowToControl(windowPosition);
	}

		/// <summary>
		/// Centralized focus handling for mouse clicks.
		/// Delegates to FocusCoordinator for all focus decisions.
		/// </summary>
		/// <param name="clickedControl">The control at click position, or null if clicked on empty space</param>
		private void HandleClickFocus(IWindowControl? clickedControl)
		{
			_window.FocusCoord?.HandleClickFocus(clickedControl);
		}

		/// <summary>
		/// Updates the layout manager with current control positions and bounds
		/// using DOM-based layout information.
		/// </summary>
		private void UpdateControlLayout()
		{
			lock (_window._lock)
			{
				var availableWidth = _window.Width - 2; // Account for borders
				var availableHeight = _window.Height - 2; // Account for borders

				// Ensure DOM tree is built
				if (_window._renderer?.RootLayoutNode == null)
				{
					_window.RebuildDOMTree();
				}

				// Update layout bounds from DOM nodes
				foreach (var control in _window._controls)
				{
					var node = _window._renderer?.GetLayoutNode(control);
					if (node == null)
						continue;

					if (!control.Visible)
						continue;

					var bounds = _window._layoutManager.GetOrCreateControlBounds(control);
					var nodeBounds = node.AbsoluteBounds;

					bounds.ControlContentBounds = new Rectangle(
						nodeBounds.X,
						nodeBounds.Y,
						nodeBounds.Width,
						nodeBounds.Height
					);


					bounds.ViewportSize = new Size(availableWidth, availableHeight);
					bounds.HasInternalScrolling = control is MultilineEditControl;
					bounds.ScrollOffset = control.StickyPosition == StickyPosition.None
						? new Point(0, _window.ScrollOffset)
						: Point.Empty;
					bounds.IsVisible = true;
				}
			}
		}

		/// <summary>
		/// Gets the control at the specified window-relative coordinates.
		/// </summary>
		/// <param name="point">The window-relative coordinates to check.</param>
		/// <returns>The control at the specified position, or null if none found or outside content area.</returns>
		public IWindowControl? GetControlAtPosition(Point? point)
		{
			lock (_window._lock)
			{
				if (point == null) return null;

				var windowPoint = point.Value;

				// Convert window coords to content coords (subtract border offset)
				// Title bar is at window Y=0, content starts at Y=1
				var contentX = windowPoint.X - 1;
				var contentY = windowPoint.Y - 1;

				// Return null if outside content area (title bar, borders)
				if (contentX < 0 || contentY < 0 ||
				    contentX >= _window.Width - 2 || contentY >= _window.Height - 2)
					return null;

				// Use DOM tree hit-testing for correct nested control detection
				return HitTestDOM(contentX, contentY);
			}
		}

		/// <summary>
		/// Hit-tests the DOM tree to find the control at the specified content coordinates.
		/// </summary>
		private IWindowControl? HitTestDOM(int x, int y)
		{
			return _window._renderer?.HitTestDOM(x, y);
		}

		/// <summary>
		/// Processes keyboard input for the window and its controls.
		/// </summary>
		/// <param name="key">The key information to process.</param>
		/// <returns>True if the input was handled; otherwise false.</returns>
		public bool ProcessInput(ConsoleKeyInfo key)
		{
			lock (_window._lock)
			{
				bool contentKeyHandled = false;
				bool windowHandled = false;

				// PreviewKeyPressed fires before any control sees the key.
				// If a handler marks it handled, skip control processing entirely.
				bool previewHandled = _window.OnPreviewKeyPressed(key);
				if (previewHandled)
					return true;

				if (HasActiveInteractiveContent(out var activeInteractiveContent))
				{
					contentKeyHandled = activeInteractiveContent!.ProcessKey(key);
				}
				else
				{
				}

				// Continue with key handling only if not handled by the focused interactive content
				if (!contentKeyHandled)
				{
					if (key.Key == ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						SwitchFocus(true); // Pass true to indicate backward focus switch
						windowHandled = true;
					}
					else if (key.Key == ConsoleKey.Tab)
					{
						SwitchFocus(false); // Pass false to indicate forward focus switch
						windowHandled = true;
					}
					else if (key.Key == ConsoleKey.Escape && _window._lastFocusedControl != null)
					{
						// Escape unfocuses the current control (for regular controls, not inside ScrollablePanel
						// which handles Escape internally via ProcessKey returning contentKeyHandled=true)
						_window._windowSystem?.LogService?.LogTrace($"WindowEventDispatcher: Escape → unfocusing {_window._lastFocusedControl.GetType().Name}", "Focus");
						// Save before ClearFocus clears _lastFocusedControl
						_escapedFromControl = _window._lastFocusedControl;
						_window.FocusCoord?.ClearFocus(Controls.FocusReason.Programmatic);
						windowHandled = true;
					}
					else
					{
						switch (key.Key)
						{
							case ConsoleKey.UpArrow:
								if (key.Modifiers != ConsoleModifiers.None) break;
								if (_window._renderer != null)
								{
									_window._renderer.ScrollBy(-1);
									_window._invalidated = true;
								}
								else
								{
									_window._scrollOffset = Math.Max(0, _window._scrollOffset - 1);
								}
								_window.IsDirty = true;
								windowHandled = true;
								break;

							case ConsoleKey.DownArrow:
								if (key.Modifiers != ConsoleModifiers.None) break;
								if (_window._renderer != null)
								{
									_window._renderer.ScrollBy(1);
									_window._invalidated = true;
								}
								else
								{
									_window._scrollOffset = Math.Min(_window.ContentLineCount - (_window.Height - 2 - _window._topStickyHeight), _window._scrollOffset + 1);
								}
								_window.IsDirty = true;
								windowHandled = true;
								break;

							case ConsoleKey.PageUp:
								if (key.Modifiers != ConsoleModifiers.None) break;
								if (_window._renderer != null)
								{
									_window._renderer.PageUp();
									_window._invalidated = true;
									_window.IsDirty = true;
									windowHandled = true;
								}
								break;

							case ConsoleKey.PageDown:
								if (key.Modifiers != ConsoleModifiers.None) break;
								if (_window._renderer != null)
								{
									_window._renderer.PageDown();
									_window._invalidated = true;
									_window.IsDirty = true;
									windowHandled = true;
								}
								break;

							case ConsoleKey.Home:
								if ((key.Modifiers == ConsoleModifiers.None || key.Modifiers == ConsoleModifiers.Control) && _window._renderer != null)
								{
									_window._renderer.ScrollToTop();
									_window._invalidated = true;
									_window.IsDirty = true;
									windowHandled = true;
								}
								break;

							case ConsoleKey.End:
								if ((key.Modifiers == ConsoleModifiers.None || key.Modifiers == ConsoleModifiers.Control) && _window._renderer != null)
								{
									_window._renderer.ScrollToBottom();
									_window._invalidated = true;
									_window.IsDirty = true;
									windowHandled = true;
								}
								break;
						}
					}
				}

				var handled = _window.OnKeyPressed(key, contentKeyHandled || windowHandled);

				return (handled || contentKeyHandled || windowHandled);
			}
		}

		/// <summary>
		/// Determines whether there is an active interactive control with focus.
		/// </summary>
		/// <param name="interactiveContent">The active interactive control, if found.</param>
		/// <returns>True if there is an active interactive control; otherwise false.</returns>
		public bool HasActiveInteractiveContent(out IInteractiveControl? interactiveContent)
		{
			// First try to find focused control in _interactiveContents (direct children)
			interactiveContent = _window._interactiveContents.LastOrDefault(ic => ic.IsEnabled && ic.HasFocus);

			// Fallback to _lastFocusedControl if it has focus (for nested controls in containers)
			if (interactiveContent == null && _window._lastFocusedControl != null && _window._lastFocusedControl.HasFocus && _window._lastFocusedControl.IsEnabled)
			{
				interactiveContent = _window._lastFocusedControl;
			}

			// Fallback: container's HasFocus was cleared by FocusService.SetFocus(leaf) in NCGF,
			// but the leaf itself still has focus. Use _lastFocusedControl as the routing target.
			if (interactiveContent == null &&
				_window._lastFocusedControl != null && _window._lastFocusedControl.IsEnabled &&
				_window._lastDeepFocusedControl is Controls.IFocusableControl deepFc && deepFc.HasFocus)
			{
				interactiveContent = _window._lastFocusedControl;
			}


			return interactiveContent != null;
		}

		/// <summary>
		/// Determines whether there is interactive content that needs cursor display.
		/// </summary>
		/// <param name="cursorPosition">When returning true, contains the cursor position in window coordinates.</param>
		/// <returns>True if cursor should be displayed; otherwise false.</returns>
		public bool HasInteractiveContent(out Point cursorPosition)
		{
			if (HasActiveInteractiveContent(out var activeInteractiveContent))
			{
				if (activeInteractiveContent is IWindowControl control)
				{
					// Find the deepest focused control to walk from
					var deepestControl = _window.FindDeepestFocusedControl(activeInteractiveContent);
					if (deepestControl != null)
					{
						control = deepestControl;
					}

					// Use the new layout manager for coordinate translation
					var windowCursorPos = _window._layoutManager.TranslateLogicalCursorToWindow(control);
					if (windowCursorPos != null)
					{
						cursorPosition = windowCursorPos.Value;

						// Check if the cursor position is actually visible in the window
						if (_window.IsCursorPositionVisible(cursorPosition, control))
						{
							return true;
						}
					}
				}
			}

			cursorPosition = Point.Empty;
			return false;
		}

		/// <summary>
		/// Switches focus to the next or previous interactive control.
		/// Delegates to FocusCoordinator for all focus decisions.
		/// </summary>
		/// <param name="backward">True to switch backward; false to switch forward.</param>
		public void SwitchFocus(bool backward = false)
		{
			lock (_window._lock)
			{
				// Tab restore after Escape: re-focus the escaped control instead of advancing
				if (_escapedFromControl != null)
				{
					var restoreTarget = _escapedFromControl;
					_escapedFromControl = null;

					_window._windowSystem?.LogService?.LogTrace($"SwitchFocus: restoring escaped control {restoreTarget.GetType().Name}", "Focus");

					_window.FocusCoord?.RequestFocus(restoreTarget as IWindowControl, Controls.FocusReason.Keyboard);
					BringIntoFocus(restoreTarget as IWindowControl);
					return;
				}

				_window.FocusCoord?.MoveFocus(backward);
			}
		}

		/// <summary>
		/// Brings the focused control into view by adjusting scroll position.
		/// Also walks up the container chain to scroll any IScrollableContainer parents.
		/// </summary>
		internal void BringIntoFocus(IWindowControl? focusedContent)
		{
			// Ensure the focused content is within the visible window

			if (focusedContent != null)
			{
				var bounds = _window._layoutManager.GetOrCreateControlBounds(focusedContent);
				var controlBounds = bounds.ControlContentBounds;

				int contentTop = controlBounds.Y;
				int contentHeight = controlBounds.Height;
				int contentBottom = contentTop + contentHeight;

				if (focusedContent.StickyPosition == StickyPosition.None)
				{
					// Calculate the visible region boundaries
					int visibleTop = _window._scrollOffset + _window._topStickyHeight;
					int visibleBottom = _window._scrollOffset + (_window.Height - 2 - _window._bottomStickyHeight);

					if (contentTop < visibleTop)
					{
						// Ensure we never set a negative scroll offset
						_window._scrollOffset = Math.Max(0, contentTop - _window._topStickyHeight);
					}
					else if (contentBottom > visibleBottom)
					{
						// Calculate how much we need to scroll to show the bottom of the content
						int newOffset = contentBottom - (_window.Height - 2 - _window._bottomStickyHeight);

						// Ensure we don't scroll beyond the maximum available content
						int maxOffset = Math.Max(0, _window.ContentLineCount - (_window.Height - 2 - _window._topStickyHeight));
						_window._scrollOffset = Math.Min(newOffset, maxOffset);
					}
				}

				// Walk up container chain to scroll nested IScrollableContainer parents
				var current = focusedContent;
				while (current != null)
				{
					if (current.Container is Controls.IScrollableContainer scrollable)
					{
						scrollable.ScrollChildIntoView(current);
					}
					current = current.Container as IWindowControl;
				}
			}

			// Invalidate the window to update the display
			_window.Invalidate(true);
		}
	}
}
