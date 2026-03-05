// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI
{
	public partial class Window
	{
		/// <summary>
		/// Gets or sets the current state of the window (Normal, Minimized, Maximized).
		/// </summary>
		public WindowState State
		{
			get => _state;
			set
			{
				WindowState previous_state = _state;

				if (previous_state == value)
				{
					return;
				}

				_windowSystem?.LogService?.LogDebug($"Window state changed: {Title} ({previous_state} -> {value})", "Window");
				_state = value;

				switch (value)
				{
					case WindowState.Minimized:
						// Clear the window area before minimizing
						_windowSystem?.Renderer?.ClearArea(Left, Top, Width, Height, _windowSystem.Theme, _windowSystem.Windows);
						Invalidate(true);
						break;

					case WindowState.Maximized:
						OriginalWidth = Width;
						OriginalHeight = Height;
						OriginalLeft = Left;
						OriginalTop = Top;
						// Position window at desktop origin (0,0 in desktop coordinates)
						// Desktop coordinates are automatically offset by DesktopUpperLeft during rendering
						Left = 0;
						Top = 0;
						// Use centralized SetSize which handles invalidation order correctly
						SetSize(
							_windowSystem?.DesktopDimensions.Width ?? 80,
							_windowSystem?.DesktopDimensions.Height ?? 24
						);
						OnResize?.Invoke(this, EventArgs.Empty);
						break;

					case WindowState.Normal:
						if (previous_state == WindowState.Maximized)
						{
							// Clear the old maximized area before restoring
							_windowSystem?.Renderer?.ClearArea(Left, Top, Width, Height, _windowSystem.Theme, _windowSystem.Windows);

							Top = OriginalTop;
							Left = OriginalLeft;
							// Use centralized SetSize which handles invalidation order correctly
							SetSize(OriginalWidth, OriginalHeight);
							OnResize?.Invoke(this, EventArgs.Empty);
						}
						else if (previous_state == WindowState.Minimized)
						{
							// Just need to redraw - position hasn't changed
							Invalidate(true);
						}
						break;
				}

				OnStateChanged(value);
			}
		}

	/// <summary>
	/// Checks if the window can be closed by firing the OnClosing event.
	/// Does not modify any state - only queries whether close is allowed.
	/// </summary>
	/// <param name="force">If true, bypasses IsClosable check and ignores Allow from OnClosing.
		/// The OnClosing event is still fired so handlers can perform pre-close work.</param>
		/// <returns>True if the window can be closed; false if close was cancelled (only when force=false).</returns>
		public bool TryClose(bool force = false)
		{
			// Already closing - allow it to proceed
			if (_isClosing) return true;

			// Check IsClosable (unless forced)
			if (!force && !IsClosable) return false;

			// Fire OnClosing event
			if (OnClosing != null)
			{
				var args = new ClosingEventArgs(force);
				OnClosing(this, args);

				// Only respect Allow if not forced
				if (!force && !args.Allow)
				{
					return false;  // Close cancelled by handler
				}
			}

			return true;
		}

		/// <summary>
		/// Attempts to close the window.
		/// If the window is in a system, delegates to CloseWindow() for proper cleanup.
		/// </summary>
		/// <param name="force">If true, forces the window to close even if IsClosable is false or OnClosing cancels.</param>
		/// <returns>True if the window was closed or close was initiated; false if closing was cancelled.</returns>
		public bool Close(bool force = false)
		{
			// Prevent re-entrancy
			if (_isClosing) return true;

			// Handle async thread cleanup with timeout (must happen before CloseWindow removes from system)
			if (_windowThreadCts != null && _windowTask != null)
			{
				// Check if close is allowed first
				if (!TryClose(force))
				{
					return false;  // Close cancelled - nothing changed
				}

				// Commit to closing
				_isClosing = true;

				_windowThreadCts.Cancel();

				var cts = _windowThreadCts;
				var task = _windowTask;
				_windowThreadCts = null;
				_windowTask = null;

				// Start grace period with visual feedback
				// When done, it will call CloseWindow() to remove from system
				BeginGracePeriodClose(task, cts);
				return true; // Close initiated (not completed yet)
			}

			// No async thread - delegate to CloseWindow if window is in a system
			if (_windowSystem != null)
			{
				bool closedBySystem = _windowSystem.CloseWindow(this, force: force);
				if (closedBySystem)
					return true;
				// Window wasn't registered in system - fall through to orphan handling
			}

			// Orphan window (not in a system OR system couldn't close it) - handle locally
			if (!TryClose(force))
			{
				return false;  // Close cancelled - nothing changed
			}

			_isClosing = true;
			CompleteClose();
			return true;
		}

		/// <summary>
		/// Begins the grace period for window thread cleanup with visual feedback.
		/// </summary>
		private void BeginGracePeriodClose(Task windowTask, CancellationTokenSource cts)
		{
			Windows.WindowLifecycleHelper.BeginGracePeriodClose(this, windowTask, cts);
		}

		/// <summary>
		/// Completes the window close operation by disposing controls and firing OnClosed event.
		/// Called by ConsoleWindowSystem after removing the window from collections.
		/// </summary>
		internal void CompleteClose()
		{
			OnClosed?.Invoke(this, EventArgs.Empty);

			foreach (var content in _controls.ToList())
			{
				InvalidationManager.Instance.UnregisterControl(content as IWindowControl);
				(content as IWindowControl)?.Dispose();
			}
		}

		/// <summary>
		/// Transforms this window into an error boundary showing hung thread information.
		/// </summary>
		private void TransformToErrorWindow(IWindowControl? statusControl)
		{
			Windows.WindowLifecycleHelper.TransformToErrorWindow(this, statusControl);
		}

		/// <summary>
		/// Gets a value indicating whether this window is currently active.
		/// </summary>
		/// <returns>True if the window is active; otherwise false.</returns>
		public bool GetIsActive()
		{
			return _isActive;
		}

		/// <summary>
		/// Scrolls the window content to the bottom.
		/// </summary>
		public void GoToBottom()
		{
			_scrollOffset = Math.Max(0, (_cachedContent?.Count ?? 0) - (Height - 2));
			Invalidate(true);
		}

		/// <summary>
		/// Scrolls the window content to the top.
		/// </summary>
		public void GoToTop()
		{
			_scrollOffset = 0;
			Invalidate(true);
		}

		/// <summary>
		/// Scrolls the window to ensure the specified control is visible in the viewport.
		/// If the control is already fully visible, no scrolling occurs.
		/// Handles both partially visible and completely off-screen controls.
		/// </summary>
		/// <param name="control">The control to scroll into view</param>
		public void ScrollToControl(IWindowControl control)
		{
			if (_layoutManager == null || _renderer == null) return;

			try
			{
				// CRITICAL: Force layout update to get fresh widget positions
				// Without this, we get stale cached bounds from before the previous scroll
				Invalidate(true);

				var bounds = _layoutManager.GetOrCreateControlBounds(control);
				if (bounds == null) return;

				var controlBounds = bounds.ControlContentBounds;
				int contentTop = controlBounds.Y;
				int contentHeight = controlBounds.Height;
				int contentBottom = contentTop + contentHeight;

				int windowHeight = Height;
				int currentScrollOffset = ScrollOffset;

				// IMPORTANT: Control bounds Y values are RELATIVE to scrollOffset, not absolute!
				// When scrollOffset=0, widget at absolute Y=50 has relative Y=50
				// When scrollOffset=50, same widget has relative Y=0
				int visibleTop = 0;
				int visibleBottom = windowHeight - 2;  // -2 for window borders

				// Check if widget is not fully visible
				bool topCutOff = contentTop < visibleTop;
				bool bottomCutOff = contentBottom > visibleBottom;

				if (topCutOff)
				{
					// Widget top is cut off - scroll up to align top with viewport top
					int absoluteY = currentScrollOffset + contentTop;
					ScrollOffset = Math.Max(0, absoluteY);
					Invalidate(true);
				}
				else if (bottomCutOff)
				{
					// Widget bottom is cut off - scroll down to show widget
					int absoluteTopY = currentScrollOffset + contentTop;
					int newOffset = Math.Min(absoluteTopY, _renderer.MaxScrollOffset);
					ScrollOffset = Math.Max(0, newOffset);
					Invalidate(true);
				}

				// If neither topCutOff nor bottomCutOff, widget is already fully visible - no scroll needed
			}
			catch
			{
				// If layout access fails, widget may be off-screen but won't crash
			}
		}

		/// <summary>
		/// Checks if a cursor position is visible within the current window viewport
		/// </summary>
		/// <param name="cursorPosition">The cursor position in window coordinates</param>
		/// <param name="control">The control that owns the cursor</param>
		/// <returns>True if the cursor position is visible</returns>
		internal bool IsCursorPositionVisible(Point cursorPosition, IWindowControl control)
		{
			// Get the control's bounds to understand its positioning
			var bounds = _layoutManager.GetOrCreateControlBounds(control);
			if (bounds == null) return false;
			var controlBounds = bounds.ControlContentBounds;

			// For nested controls, ControlContentBounds is never populated (only top-level controls get it).
			// Fall back to the DOM node's AbsoluteBounds which tracks all controls including nested ones.
			if (controlBounds.Width == 0 && controlBounds.Height == 0)
			{
				var node = _renderer?.GetLayoutNode(control);

				// If this control has no LayoutNode (lives inside a self-painting container),
				// walk up through Container to find the nearest ancestor with a LayoutNode.
				if (node == null)
				{
					var current = control.Container as IWindowControl;
					while (current != null)
					{
						node = _renderer?.GetLayoutNode(current);
						if (node != null) break;
						current = current.Container as IWindowControl;
					}
					if (node == null) return false;
				}

				var ab = node.AbsoluteBounds;
				controlBounds = new Rectangle(ab.X, ab.Y, ab.Width, ab.Height);
			}

			// Convert cursor position from window coordinates to window content coordinates
			// Window coordinates have border at (0,0), content starts at (1,1)
			// Window content coordinates (used by ControlContentBounds) have content at (0,0)
			var cursorInContentCoords = new Point(cursorPosition.X - 1, cursorPosition.Y - 1);

			// Check if cursor is within the control's actual content bounds
			if (cursorInContentCoords.X < controlBounds.X ||
				cursorInContentCoords.X >= controlBounds.X + controlBounds.Width ||
				cursorInContentCoords.Y < controlBounds.Y ||
				cursorInContentCoords.Y >= controlBounds.Y + controlBounds.Height)
			{
				return false;
			}


			// For sticky controls, the cursor is visible if it's within the window bounds
			// (bounds check already passed above)
			if (control.StickyPosition == StickyPosition.Top || control.StickyPosition == StickyPosition.Bottom)
			{
				// Sticky controls are always visible if within window bounds
				var result = cursorPosition.X >= 1 && cursorPosition.X < Width - 1 &&
							 cursorPosition.Y >= 1 && cursorPosition.Y < Height - 1;
				return result;
			}
			else
			{
				// For scrollable (non-sticky) controls, check if cursor is within window viewport
				var scrollableAreaTop = 1;
				var scrollableAreaBottom = Height - 1;


				// Check if cursor Y is within the scrollable area bounds
				if (cursorPosition.Y < scrollableAreaTop || cursorPosition.Y >= scrollableAreaBottom)
				{
					return false;
				}

				// Check if the control itself is visible in the current scroll position
				// Control is visible if any part of it intersects with the visible scrollable area
				var visibleScrollTop = _scrollOffset;
				var visibleScrollBottom = _scrollOffset + (scrollableAreaBottom - scrollableAreaTop);

				var controlTop = controlBounds.Y; // controlBounds.Y is already in window coordinates
				var controlBottom = controlTop + controlBounds.Height;

				// Control is visible if it overlaps with the visible scroll area
				var result = controlBottom > visibleScrollTop && controlTop < visibleScrollBottom;
				return result;
			}
		}

		/// <summary>
		/// Marks the window as needing redraw and optionally invalidates all controls.
		/// </summary>
		/// <param name="redrawAll">True to invalidate all controls; false for partial invalidation.</param>
		/// <param name="callerControl">The control that initiated the invalidation, to prevent recursion.</param>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			_invalidated = true;

			// Use TryEnter to avoid blocking when the render thread holds the lock.
			// If we can't acquire it, _invalidated + IsDirty are still set, so the
			// render loop will do a full layout rebuild on the next frame.
			if (Monitor.TryEnter(_lock))
			{
				try
				{
					if (redrawAll)
					{
						// Invalidate measurements without rebuilding the tree
						// This preserves runtime state like splitter positions
						_renderer?.InvalidateDOMLayout();
					}
					else if (callerControl != null)
					{
						// Specific control invalidation
						var node = _renderer?.GetLayoutNode(callerControl);
						if (node != null)
						{
							node.InvalidateMeasure();
						}
						else
						{
							// Fallback: invalidate entire tree
							_renderer?.InvalidateDOMLayout();
						}
					}
				}
				finally
				{
					Monitor.Exit(_lock);
				}
			}

			IsDirty = true;
		}

		/// <summary>
		/// Invalidates cached border strings, forcing them to be regenerated on next render.
		/// Called when properties affecting border rendering change (width, height, title, border style, active state).
		/// </summary>
		internal void InvalidateBorderCache()
		{
			_borderRenderer?.InvalidateCache();
		}

		/// <summary>
		/// Maximizes the window to fill the entire desktop area.
		/// </summary>
		/// <param name="force">
		/// If true, bypasses the <see cref="IsMaximizable"/> check and forces maximization.
		/// Default is false, which respects the <see cref="IsMaximizable"/> property.
		/// Use force=true for programmatic maximization that should override user preferences.
		/// </param>
		/// <remarks>
		/// When force is false (default), the method will silently return if IsMaximizable is false.
		/// This maintains backward compatibility with existing code.
		/// </remarks>
		public void Maximize(bool force = false)
		{
			if (!force && !IsMaximizable)
				return;
			State = WindowState.Maximized;
		}

		/// <summary>
		/// Minimizes the window.
		/// </summary>
		/// <param name="force">
		/// If true, bypasses the <see cref="IsMinimizable"/> check and forces minimization.
		/// Default is false, which respects the <see cref="IsMinimizable"/> property.
		/// Use force=true for programmatic minimization (e.g., UAC-style dialogs) that
		/// should override user preferences.
		/// </param>
		/// <remarks>
		/// When force is false (default), the method will silently return if IsMinimizable is false.
		/// This maintains backward compatibility with existing code.
		/// </remarks>
		public void Minimize(bool force = false)
		{
			if (!force && !IsMinimizable)
				return;
			State = WindowState.Minimized;
		}

		/// <summary>
		/// Restores the window to its normal state.
		/// </summary>
		public void Restore()
		{
			State = WindowState.Normal;
		}

		/// <summary>
		/// Sets the active state of the window.
		/// </summary>
		/// <param name="value">True to activate the window; false to deactivate.</param>
		public void SetIsActive(bool value)
		{
			if (value)
			{
				Activated?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				DismissAutoClosePortals();
				Deactivated?.Invoke(this, EventArgs.Empty);
			}

			_isActive = value;
		InvalidateBorderCache();

			// Invalidate window to redraw border with new active/inactive colors
			Invalidate(false);  // Border-only invalidation (redrawAll=false)

		if (_lastFocusedControl != null)
			{
				_lastFocusedControl.HasFocus = value;
				// Sync with FocusStateService
				if (value)
				{
					FocusService?.SetFocus(this, _lastDeepFocusedControl ?? _lastFocusedControl, FocusChangeReason.WindowActivation);
				}
				else
				{
					FocusService?.ClearFocus(this, FocusChangeReason.WindowActivation);
				}
			}
		}

		/// <summary>
		/// Sets the position of the window.
		/// </summary>
		/// <param name="point">The new position with X as left and Y as top.</param>
		public void SetPosition(Point point)
		{
			if (point.X < 0 || point.Y < 0) return;

			// Use public setters which go through WindowPositioningManager for proper invalidation
			Left = point.X;
			Top = point.Y;
		}

		/// <summary>
		/// Internal method to set position directly without triggering invalidation logic.
		/// Used by WindowPositioningManager to avoid recursion.
		/// Note: Negative coordinates are allowed - rendering pipeline handles them safely.
		/// </summary>
		internal void SetPositionDirect(Point point)
		{
			_left = point.X;
			_top = point.Y;
		}

		/// <summary>
		/// Sets the window size with proper invalidation and constraint handling.
		/// </summary>
		/// <param name="width">The new width in character columns.</param>
		/// <param name="height">The new height in character rows.</param>
		public void SetSize(int width, int height)
		{
			if (_width == width && _height == height)
			{
				return;
			}

			// Apply constraints
			if (_minimumWidth != null && width < _minimumWidth)
				width = (int)_minimumWidth;
			if (_minimumHeight != null && height < _minimumHeight)
				height = (int)_minimumHeight;

			// Set backing fields directly to avoid property setters calling
			// UpdateControlLayout() before both dimensions are set
			_width = width;
			_height = height;

			// IMPORTANT: Invalidate controls FIRST so they clear their caches
			Invalidate(true);

			// Layout will be updated lazily on next event

			if (_scrollOffset > (_cachedContent?.Count ?? Height) - (Height - 2))
			{
				GoToBottom();
			}

			OnResize?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Called when the window has been added to the window system.
		/// </summary>
		public void WindowIsAdded()
		{
			OnShown?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Raises the PreviewKeyPressed event before the key reaches any control.
		/// </summary>
		/// <returns>True if a handler set Handled=true (prevents control processing).</returns>
		protected internal virtual bool OnPreviewKeyPressed(ConsoleKeyInfo key)
		{
			var handler = PreviewKeyPressed;
			if (handler != null)
			{
				var args = new KeyPressedEventArgs(key, false);
				handler(this, args);
				return args.Handled;
			}
			return false;
		}

		/// <summary>
		/// Raises the KeyPressed event.
		/// </summary>
		/// <param name="key">The key information.</param>
		/// <param name="alreadyHandled">Indicates whether the key was already handled.</param>
		/// <returns>True if the event was handled; otherwise false.</returns>
		protected internal virtual bool OnKeyPressed(ConsoleKeyInfo key, bool alreadyHandled)
		{
			var handler = KeyPressed;
			if (handler != null)
			{
				var args = new KeyPressedEventArgs(key, alreadyHandled);
				handler(this, args);
				return args.Handled;
			}
			return false;
		}

		/// <summary>
		/// Raises the StateChanged event.
		/// </summary>
		/// <param name="newState">The new window state.</param>
		protected virtual void OnStateChanged(WindowState newState)
		{
			StateChanged?.Invoke(this, new WindowStateChangedEventArgs(newState));
		}

		// Helper method to set up initial position for subwindows
		private void SetupInitialPosition()
		{
			// Only set position if this is a subwindow and both Left and Top are at their default values (0)
			if (_parentWindow != null && Left == 0 && Top == 0)
			{
				// Position the subwindow in the center of the parent window
				int parentCenterX = _parentWindow.Left + (_parentWindow.Width / 2);
				int parentCenterY = _parentWindow.Top + (_parentWindow.Height / 2);

				// Center this window on the parent's center
				Left = Math.Max(0, parentCenterX - (Width / 2));
				Top = Math.Max(0, parentCenterY - (Height / 2));

				// If we're a modal window, ensure we're visible and properly centered
				if (IsModal)
				{
					// Use a smaller offset for modal windows to make them look more like dialogs
					// Ensure the window fits within the parent window bounds
					Left = Math.Max(0, _parentWindow.Left + Configuration.ControlDefaults.ModalWindowLeftOffset);
					Top = Math.Max(0, _parentWindow.Top + Configuration.ControlDefaults.ModalWindowTopOffset);

					// Make sure the window isn't too large for the parent
					if (_windowSystem != null)
					{
						// Make sure the window fits on the screen
						if (Left + Width > _windowSystem.DesktopBottomRight.X)
							Left = Math.Max(0, _windowSystem.DesktopBottomRight.X - Width);

						if (Top + Height > _windowSystem.DesktopBottomRight.Y)
							Top = Math.Max(0, _windowSystem.DesktopBottomRight.Y - Height);
					}
				}
			}
		}

		/// <summary>
		/// Provides data for the window state changed event.
		/// </summary>
		public class WindowStateChangedEventArgs : EventArgs
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="WindowStateChangedEventArgs"/> class.
			/// </summary>
			/// <param name="newState">The new window state.</param>
			public WindowStateChangedEventArgs(WindowState newState)
			{
				NewState = newState;
			}

			/// <summary>
			/// Gets the new window state.
			/// </summary>
			public WindowState NewState { get; }
		}
	}
}
