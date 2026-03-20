// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region ProcessKey

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			var log = GetConsoleWindowSystem?.LogService;
			var focusedChild = GetFocusedChildFromCoordinator();
			log?.LogTrace($"ScrollPanel.ProcessKey({key.Key}): _hasFocus={_hasFocus} _isEnabled={_isEnabled} focusedChild={focusedChild?.GetType().Name ?? "null"} focusedChild.HasFocus={(focusedChild as IFocusableControl)?.HasFocus}", "Focus");

			if (!_hasFocus || !_isEnabled) return false;

			// FIRST: Delegate to focused child if we have one
			if (focusedChild != null && focusedChild.ProcessKey(key))
			{
				return true; // Child handled it
			}

			// Save pre-delegation reference — the coordinator path may be cleared by
			// a child's Tab exit notification chain (e.g., HGrid unfocuses its last child).
			// Used as fallback for Tab index calculation below.
			var focusedChildBeforeDelegate = focusedChild;

			// Re-read after delegation — child's ProcessKey may have changed focus
			focusedChild = GetFocusedChildFromCoordinator();

			// Handle Escape: unfocus child, enter scroll mode (panel stays focused)
			if (key.Key == ConsoleKey.Escape && focusedChild != null)
			{
				log?.LogTrace($"ScrollPanel.ProcessKey: Escape → unfocusing child {focusedChild.GetType().Name}, entering scroll mode", "Focus");
				_lastInternalFocusedChild = focusedChild;
				if (focusedChild is IFocusableControl escapeFc)
					escapeFc.SetFocus(false, FocusReason.Programmatic);
				// Update path so SPC is the leaf (no focused child)
				var coordinator = (this as IWindowControl).GetParentWindow()?.FocusCoord;
				coordinator?.UpdateFocusPath(this);
				Container?.Invalidate(true);
				return true;
			}

			// Handle Escape in scroll mode (no child focused): let it propagate to unfocus panel
			if (key.Key == ConsoleKey.Escape && focusedChild == null)
			{
				log?.LogTrace("ScrollPanel.ProcessKey: Escape in scroll mode → propagating to parent", "Focus");
				_lastInternalFocusedChild = null;
				return false; // Let parent handle (will unfocus panel)
			}

			// SECOND: Handle Tab navigation through children
			if (key.Key == ConsoleKey.Tab)
			{
				bool shiftPressed = (key.Modifiers & ConsoleModifiers.Shift) != 0;

				// Tab in scroll mode: restore last focused child
				if (focusedChild == null && _lastInternalFocusedChild != null)
				{
					log?.LogTrace($"ScrollPanel.ProcessKey: Tab in scroll mode → restoring {_lastInternalFocusedChild.GetType().Name}", "Focus");
					var restoreChild = _lastInternalFocusedChild;
					_lastInternalFocusedChild = null;
					if (restoreChild is IFocusableControl restoreFc)
						restoreFc.SetFocus(true, FocusReason.Keyboard);
					// Update coordinator path with the actual focused leaf
					UpdateCoordinatorFocusPath(restoreChild);
					if (restoreChild is IWindowControl focusedWindow)
						ScrollChildIntoView(focusedWindow);
					Container?.Invalidate(true);
					return true;
				}

				List<IWindowControl> childrenSnapshot;
				lock (_childrenLock) { childrenSnapshot = new List<IWindowControl>(_children); }
				var focusableChildren = childrenSnapshot
					.Where(c => c.Visible && c is IInteractiveControl && CanChildReceiveFocus(c))
					.Cast<IInteractiveControl>()
					.ToList();

				if (focusableChildren.Count > 0)
				{
					// Use saved reference as fallback — focusedChild may be null if the
					// coordinator path was cleared by a child's Tab exit notification chain
					var effectiveFocused = focusedChild ?? focusedChildBeforeDelegate;

					var coordinator = (this as IWindowControl).GetParentWindow()?.FocusCoord;
					var newChild = coordinator != null
						? coordinator.TabThroughChildren(focusableChildren, effectiveFocused, shiftPressed)
						: Core.FocusCoordinator.AdvanceTabFocus(focusableChildren, effectiveFocused, shiftPressed);
					if (newChild == null)
						return false; // Exit container — let Tab propagate to parent

					// Ensure panel stays focused (notification chain may have cleared it)
					_hasFocus = true;

					// Scroll newly focused child into view
					if (newChild is IWindowControl scrollTarget)
						ScrollChildIntoView(scrollTarget);

					Container?.Invalidate(true);
					return true;
				}
			}

			// THIRD: Handle scrolling keys (only if panel needs scrolling)
			if (NeedsScrolling())
			{
				switch (key.Key)
				{
					case ConsoleKey.UpArrow:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(-1);
							return true;
						}
						break;

					case ConsoleKey.DownArrow:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(1);
							return true;
						}
						break;

					case ConsoleKey.PageUp:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(-_viewportHeight);
							return true;
						}
						break;

					case ConsoleKey.PageDown:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(_viewportHeight);
							return true;
						}
						break;

					case ConsoleKey.Home:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalTo(0);
							return true;
						}
						break;

					case ConsoleKey.End:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							_autoScroll = true;  // Explicitly re-attach
							ScrollVerticalTo(Math.Max(0, _contentHeight - _viewportHeight));
							return true;
						}
						break;

					case ConsoleKey.LeftArrow:
						if (_horizontalScrollMode == ScrollMode.Scroll)
						{
							ScrollHorizontalBy(-1);
							return true;
						}
						break;

					case ConsoleKey.RightArrow:
						if (_horizontalScrollMode == ScrollMode.Scroll)
						{
							ScrollHorizontalBy(1);
							return true;
						}
						break;
				}
			}

			return false;
		}

		/// <summary>
		/// Determines if a child control can receive focus, either directly
		/// or because it's a container with focusable descendants (e.g. HorizontalGrid).
		/// </summary>
		private static bool CanChildReceiveFocus(IWindowControl child)
		{
			// Direct focusable control
			if (child is IFocusableControl fc && fc.CanReceiveFocus)
				return true;

			// Container with focusable descendants (e.g. HorizontalGrid with CanReceiveFocus=false
			// but containing focusable controls in its columns)
			if (child is IContainerControl container)
			{
				foreach (var nested in container.GetChildren())
				{
					if (CanChildReceiveFocus(nested))
						return true;
				}
			}

			return false;
		}

		#endregion

		#region IFocusableControl Implementation

		/// <inheritdoc/>
		/// <summary>
		/// ScrollablePanel is focusable when it has anything to interact with:
		/// either scrollable content or focusable children. The panel acts as an
		/// opaque focus container — it owns its children's focus lifecycle entirely.
		/// </summary>
		public bool CanReceiveFocus
		{
			get
			{
				if (!Visible || !_isEnabled) return false;

				bool needsScrolling = NeedsScrolling();
				bool hasFocusableChildren = HasFocusableChildren();

				var result = needsScrolling || hasFocusableChildren;
				GetConsoleWindowSystem?.LogService?.LogTrace($"ScrollPanel.CanReceiveFocus: needsScrolling={needsScrolling} hasFocusableChildren={hasFocusableChildren} result={result}", "Focus");
				return result;
			}
		}

		/// <inheritdoc/>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_focusFromBackward = backward;
			SetFocus(focus, FocusReason.Keyboard);
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var log = GetConsoleWindowSystem?.LogService;
			var focusedChild = GetFocusedChildFromCoordinator();
			log?.LogTrace($"ScrollPanel.SetFocus({focus}, {reason}): _hasFocus={_hasFocus} focusedChild={focusedChild?.GetType().Name ?? "null"}", "Focus");

			if (_hasFocus == focus) return;

			var hadFocus = _hasFocus;
			_hasFocus = focus;

			if (focus)
			{
				// Getting focus - find first/last focusable child if we have any
				List<IWindowControl> childrenSnap;
				lock (_childrenLock) { childrenSnap = new List<IWindowControl>(_children); }
				var focusableChildren = childrenSnap
					.Where(c => c.Visible && c is IInteractiveControl && CanChildReceiveFocus(c))
					.ToList();

				if (focusableChildren.Any())
				{
					// If the viewport has been laid out and content overflows, enter scroll
					// mode first so the user can browse with arrow keys before pressing Tab
					// to focus the first child. Only for forward entry — backward entry should
					// focus the last child directly (user came from a control after this panel).
					if (_viewportHeight > 0 && NeedsScrolling() && !_focusFromBackward)
					{
						log?.LogTrace("ScrollPanel.SetFocus: needs scrolling, entering scroll mode (focusable children available via Tab)", "Focus");
					}
					else
					{
						// Content fits in viewport (or layout not yet computed) — delegate focus to child immediately.
						var initialChild = (_focusFromBackward
							? focusableChildren.Last()
							: focusableChildren.First()) as IInteractiveControl;

						log?.LogTrace($"ScrollPanel.SetFocus: delegating to child {initialChild?.GetType().Name} (backward={_focusFromBackward})", "Focus");

						if (initialChild is IFocusableControl fc)
						{
							if (initialChild is IDirectionalFocusControl dfc)
								dfc.SetFocusWithDirection(true, _focusFromBackward);
							else
								fc.SetFocus(true, reason);
						}

						// Ensure panel stays focused (notification chain may have cleared it)
						_hasFocus = true;

						// Update coordinator path with the actual focused leaf
						UpdateCoordinatorFocusPath(initialChild);

						// Scroll the focused child into view
						if (initialChild is IWindowControl focusedWc)
							ScrollChildIntoView(focusedWc);
					}
				}
				else
				{
					log?.LogTrace("ScrollPanel.SetFocus: no focusable children, panel focused for scrolling", "Focus");
				}

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				// Losing focus - unfocus any focused child
				focusedChild = GetFocusedChildFromCoordinator();
				if (focusedChild != null && focusedChild is IFocusableControl fc)
				{
					log?.LogTrace($"ScrollPanel.SetFocus(false): unfocusing child {focusedChild.GetType().Name}", "Focus");
					fc.SetFocus(false, reason);
				}
				_lastInternalFocusedChild = null;

				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			Container?.Invalidate(true);

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion
	}
}
