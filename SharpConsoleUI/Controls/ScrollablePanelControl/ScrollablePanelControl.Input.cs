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
			log?.LogTrace($"ScrollPanel.ProcessKey({key.Key}): HasFocus={HasFocus} _isEnabled={_isEnabled} focusedChild={focusedChild?.GetType().Name ?? "null"}", "Focus");

			if (!HasFocus || !_isEnabled) return false;

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

			// Handle Escape: save focused child and transfer focus to panel itself (scroll mode)
			if (key.Key == ConsoleKey.Escape)
			{
				var parentWindow = (this as IWindowControl).GetParentWindow();
				var managed = parentWindow?.FocusManager.FocusedControl;
				// A child is focused if FocusManager tracks a non-self child, or (legacy path) focusedChild from coordinator is non-null
				var childIsFocused = (managed != null && !ReferenceEquals(managed, this)) || focusedChild != null;
				if (childIsFocused)
				{
					log?.LogTrace($"ScrollPanel.ProcessKey: Escape → saving child focus, moving focus to panel", "Focus");
					// Save the child for restoration (SavedFocus for FocusScope protocol, _lastInternalFocusedChild for Tab restore)
					var childToSave = managed ?? (focusedChild as IFocusableControl);
					SavedFocus = childToSave;
					_lastInternalFocusedChild = focusedChild ?? (managed as IInteractiveControl);

					if (parentWindow != null && managed != null)
					{
						// Set scroll mode flag so GetInitialFocus returns 'this' (self-sentinel),
						// causing FocusManager.SetFocus(panel) to focus panel directly.
						_enterScrollModeOnNextInitialFocus = true;
						parentWindow.FocusManager.SetFocus(this, FocusReason.Keyboard);
					}
					else
					{
						// Legacy path: manually unfocus child and keep panel focused
						Container?.Invalidate(true);
					}
					return true;
				}
				log?.LogTrace("ScrollPanel.ProcessKey: Escape in scroll mode → propagating to parent", "Focus");
				_lastInternalFocusedChild = null;
				return false;
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
						(this as IWindowControl).GetParentWindow()?.FocusManager.SetFocus(restoreFc, FocusReason.Keyboard);
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
					// In sentinel scroll mode (panel itself is FocusManager's focused control and
					// no child is internally focused), Shift+Tab should exit the panel so the
					// caller can switch to the previous sibling (e.g. NavigationView nav pane).
					if (shiftPressed && focusedChild == null && _lastInternalFocusedChild == null)
					{
						var parentWindow2 = (this as IWindowControl).GetParentWindow();
						if (parentWindow2?.FocusManager.IsFocused(this) == true)
							return false; // Exit panel backward
					}

					// Use saved reference as fallback — focusedChild may be null if the
					// coordinator path was cleared by a child's Tab exit notification chain
					var effectiveFocused = focusedChild ?? focusedChildBeforeDelegate;

					// Advance Tab focus within this container's children via FocusManager
					var newChild = AdvanceFocusInChildren(focusableChildren, effectiveFocused, shiftPressed);
					if (newChild == null)
					{
						// Tab exited the container: clear focus from children so the parent
						// can correctly determine that focus has left this container.
						var parentWindow3 = (this as IWindowControl).GetParentWindow();
						parentWindow3?.FocusManager.SetFocus(null, FocusReason.Keyboard);
						return false; // Exit container — let Tab propagate to parent
					}

					// Ensure panel stays focused (notification chain may have cleared it)

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
		/// Advances Tab focus within a list of the panel's focusable children.
		/// Finds the next or previous child after <paramref name="currentFocused"/>,
		/// sets focus via FocusManager, and returns the new child.
		/// Returns null when the Tab key should exit this container.
		/// </summary>
		private IInteractiveControl? AdvanceFocusInChildren(
			IReadOnlyList<IInteractiveControl> children,
			IInteractiveControl? currentFocused,
			bool backward)
		{
			if (children.Count == 0) return null;

			// Find current index
			int currentIndex = -1;
			if (currentFocused != null)
			{
				for (int i = 0; i < children.Count; i++)
				{
					if (ReferenceEquals(children[i], currentFocused))
					{
						currentIndex = i;
						break;
					}
				}
			}

			// Compute next index; null signals container exit
			int newIndex;
			if (currentIndex == -1)
			{
				newIndex = backward ? children.Count - 1 : 0;
			}
			else if (backward)
			{
				newIndex = currentIndex - 1;
				if (newIndex < 0) return null; // Exit container backward
			}
			else
			{
				newIndex = currentIndex + 1;
				if (newIndex >= children.Count) return null; // Exit container forward
			}

			var newChild = children[newIndex];

			// Set focus via FocusManager — this fires FocusChanged and updates all subscribers.
			// For transparent IFocusScope containers (CanReceiveFocus=false, e.g. HGrid),
			// SetFocus silently rejects the control; enter the scope manually via GetInitialFocus.
			var window = (this as IWindowControl).GetParentWindow();
			if (newChild is IFocusableControl newFc)
			{
				if (newFc.CanReceiveFocus)
				{
					window?.FocusManager.SetFocus(newFc, FocusReason.Keyboard);
				}
				else if (newChild is IFocusScope transparentScope)
				{
					// Transparent scope: enter it directly
					var firstChild = transparentScope.GetInitialFocus(backward);
					if (firstChild != null)
						window?.FocusManager.SetFocus(firstChild, FocusReason.Keyboard);
				}
			}

			return newChild;
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
		/// When true, forces <see cref="CanReceiveFocus"/> to return true regardless of
		/// whether the panel has scrollable content or focusable children.
		/// Use this when the panel must be focusable as a scroll/container target even
		/// when its children are non-interactive (e.g., the NavigationView nav pane).
		/// </summary>
		public bool ForceReceiveFocus { get; set; }

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
				if (ForceReceiveFocus) return true;

				bool needsScrolling = NeedsScrolling();
				bool hasFocusableChildren = HasFocusableChildren();

				var result = needsScrolling || hasFocusableChildren;
				GetConsoleWindowSystem?.LogService?.LogTrace($"ScrollPanel.CanReceiveFocus: needsScrolling={needsScrolling} hasFocusableChildren={hasFocusableChildren} result={result}", "Focus");
				return result;
			}
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			Container?.Invalidate(true);
		}

		#endregion
	}
}
