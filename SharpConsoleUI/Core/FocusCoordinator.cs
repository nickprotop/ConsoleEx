// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Single authority for all focus changes in a window.
	/// All focus transitions — click, Tab, programmatic — go through this coordinator.
	/// Containers are notified but never initiate focus changes themselves.
	///
	/// Maintains a FocusPath — an ordered list of controls from root to leaf
	/// representing the current focus chain. Example:
	///   [NavigationView, ScrollablePanelControl, ButtonControl]
	/// This path is the authoritative source of truth for which controls are
	/// in the focus chain. In Phase 1, it coexists with the legacy tracking
	/// (_focusedChild, _lastFocusedControl, etc.) for backward compatibility.
	/// </summary>
	public class FocusCoordinator
	{
		private readonly Window _window;
		private ILogService? LogService => _window._windowSystem?.LogService;

		/// <summary>
		/// The current focus path — ordered from outermost container to innermost leaf.
		/// Empty when no control has focus.
		/// </summary>
		private readonly List<IWindowControl> _focusPath = new();

		/// <summary>
		/// Gets the current focus path as a read-only list.
		/// Ordered from outermost container to innermost leaf.
		/// </summary>
		public IReadOnlyList<IWindowControl> FocusPath => _focusPath.AsReadOnly();

		/// <summary>
		/// Gets the focused leaf control (deepest in the path), or null if no focus.
		/// </summary>
		public IWindowControl? FocusedLeaf => _focusPath.Count > 0 ? _focusPath[^1] : null;

		/// <summary>
		/// Returns true if the specified control is in the current focus path.
		/// A container is "in the focus path" if it's an ancestor of the focused leaf.
		/// </summary>
		public bool IsInFocusPath(IWindowControl control)
		{
			return _focusPath.Contains(control);
		}

		/// <summary>
		/// Returns the child of the specified container that is in the focus path,
		/// or null if the container is not in the path or is the leaf.
		/// This replaces per-container _focusedChild tracking.
		/// </summary>
		public IWindowControl? GetFocusedChild(IContainer container)
		{
			// Find the container in the path — the next entry is its focused child
			for (int i = 0; i < _focusPath.Count - 1; i++)
			{
				// Match by identity: the path entry IS the container (cast to IWindowControl)
				if (ReferenceEquals(_focusPath[i], container))
					return _focusPath[i + 1];

				// Also match when the path entry's Container property points to this container
				// (handles transparent containers like ColumnContainer that aren't in the path)
				if (ReferenceEquals(_focusPath[i + 1].Container, container))
					return _focusPath[i + 1];
			}
			return null;
		}

		/// <summary>
		/// Returns the child of the specified container control that is in the focus path.
		/// Use for containers that implement IContainerControl but not IContainer
		/// (e.g. HorizontalGridControl). For IContainer implementations, use the
		/// <see cref="GetFocusedChild(IContainer)"/> overload instead.
		/// </summary>
		public IWindowControl? GetFocusedChildOf(IWindowControl containerControl)
		{
			// Find the container control in the path by identity — the next entry is its focused child
			for (int i = 0; i < _focusPath.Count - 1; i++)
			{
				if (ReferenceEquals(_focusPath[i], containerControl))
					return _focusPath[i + 1];
			}
			return null;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FocusCoordinator"/> class.
		/// </summary>
		public FocusCoordinator(Window window)
		{
			_window = window ?? throw new ArgumentNullException(nameof(window));
		}

		#region Public API

		/// <summary>
		/// Requests focus on the specified control. This is the SINGLE entry point for all focus changes.
		/// Handles: walking from a deep leaf up to find the correct container chain,
		/// unfocusing the old chain, focusing the new chain, and syncing FocusStateService.
		/// </summary>
		/// <param name="target">The control to focus (can be a deep leaf or a container). Null to clear focus.</param>
		/// <param name="reason">The reason for the focus change.</param>
		public void RequestFocus(IWindowControl? target, FocusReason reason)
		{
			LogService?.LogTrace($"FocusCoordinator.RequestFocus: target={target?.GetType().Name ?? "null"} reason={reason}", "Focus");

			if (target == null)
			{
				ClearFocus(reason);
				return;
			}

			// Validate target is focusable
			if (target is IFocusableControl fc && !fc.CanReceiveFocus)
			{
				LogService?.LogTrace($"FocusCoordinator.RequestFocus: target {target.GetType().Name} CanReceiveFocus=false, clearing focus", "Focus");
				ClearFocus(reason);
				return;
			}

			// Build the chain from target up to the window
			var newChain = BuildContainerChain(target);
			var newTopLevel = FindTopLevelControl(target);
			var newLeaf = target;

			// Find the current focus chain
			var oldLeaf = _window._lastDeepFocusedControl;
			var oldTopLevel = _window._lastFocusedControl;

			// If the same leaf is already focused, nothing to do
			if (oldLeaf == newLeaf && oldLeaf is IFocusableControl oldFc && oldFc.HasFocus)
			{
				LogService?.LogTrace("FocusCoordinator.RequestFocus: same leaf already focused, no-op", "Focus");
				return;
			}

			// === Step 1: Unfocus the old chain ===
			UnfocusCurrentChain(reason);

			// === Step 2: Focus the new chain ===
			FocusNewChain(newChain, newTopLevel, newLeaf, reason);

			// === Step 3: Update focus path ===
			UpdateFocusPath(newLeaf);

			// === Step 4: Sync Window tracking (legacy — will be removed in Phase 5) ===
			if (newTopLevel is IInteractiveControl topInteractive)
				_window._lastFocusedControl = topInteractive;
			_window._lastDeepFocusedControl = newLeaf is IInteractiveControl leafInteractive ? leafInteractive : null;

			// === Step 5: Sync FocusStateService ===
			var focusTarget = _window._lastDeepFocusedControl ?? _window._lastFocusedControl;
			if (focusTarget != null)
			{
				var changeReason = reason switch
				{
					FocusReason.Mouse => FocusChangeReason.Mouse,
					FocusReason.Keyboard => FocusChangeReason.Keyboard,
					_ => FocusChangeReason.Programmatic
				};
				_window.FocusService?.SetFocus(_window, focusTarget, changeReason);
			}
		}

		/// <summary>
		/// Clears all focus in the window.
		/// </summary>
		public void ClearFocus(FocusReason reason)
		{
			LogService?.LogTrace("FocusCoordinator.ClearFocus", "Focus");

			UnfocusCurrentChain(reason);

			_focusPath.Clear();
			_window._lastFocusedControl = null;
			_window._lastDeepFocusedControl = null;

			var changeReason = reason switch
			{
				FocusReason.Mouse => FocusChangeReason.Mouse,
				FocusReason.Keyboard => FocusChangeReason.Keyboard,
				_ => FocusChangeReason.Programmatic
			};
			_window.FocusService?.ClearControlFocus(changeReason);
		}

		/// <summary>
		/// Moves focus to the next or previous control in the Tab order.
		/// </summary>
		/// <param name="backward">True to move backward (Shift+Tab).</param>
		public void MoveFocus(bool backward)
		{
			var focusableControls = _window.GetAllFocusableControlsFlattened();
			LogService?.LogTrace($"FocusCoordinator.MoveFocus(backward={backward}): count={focusableControls.Count}", "Focus");

			if (focusableControls.Count == 0) return;

			// Find current position
			var currentIndex = focusableControls.FindIndex(ic => ic.HasFocus);
			if (currentIndex == -1 && _window._lastFocusedControl != null)
				currentIndex = focusableControls.IndexOf(_window._lastFocusedControl);

			// Unfocus current
			if (currentIndex >= 0 && currentIndex < focusableControls.Count)
			{
				var current = focusableControls[currentIndex];
				if (current is IFocusableControl currentFc)
					currentFc.SetFocus(false, FocusReason.Keyboard);
				else
					current.HasFocus = false;
			}

			// Find next
			int nextIndex = currentIndex;
			int attempts = 0;
			do
			{
				nextIndex = backward
					? (nextIndex - 1 + focusableControls.Count) % focusableControls.Count
					: (nextIndex + 1) % focusableControls.Count;
				attempts++;

				var control = focusableControls[nextIndex];
				bool canFocus = control is IFocusableControl fc2 ? fc2.CanReceiveFocus : control.IsEnabled;

				if (canFocus)
				{
					// Use directional focus for containers that support it
					if (control is IDirectionalFocusControl directional)
						directional.SetFocusWithDirection(true, backward);
					else if (control is IFocusableControl focusable)
						focusable.SetFocus(true, FocusReason.Keyboard);
					else
						control.HasFocus = true;

					// Update focus path — find the actual focused leaf (may be inside a container)
					var actualLeaf = FindDeepestFocusedLeaf(control) ?? control;
					UpdateFocusPath(actualLeaf as IWindowControl ?? control as IWindowControl);

					// Sync legacy tracking (will be removed in Phase 5)
					_window._lastFocusedControl = control;
					var leaf = (_window._lastDeepFocusedControl is IFocusableControl leafFc && leafFc.HasFocus)
						? _window._lastDeepFocusedControl
						: control;
					_window.FocusService?.SetFocus(_window, leaf, FocusChangeReason.Keyboard);

					// Scroll into view
					_window.EventDispatcher?.BringIntoFocus(control as IWindowControl);
					break;
				}
			} while (attempts < focusableControls.Count);
		}

		/// <summary>
		/// Advances Tab focus through an ordered list of children within a container.
		/// Handles: index calculation, boundary detection, unfocus/focus with directional
		/// support, and focus path update. Returns the newly focused control, or null
		/// if Tab should exit the container (caller returns false to propagate).
		/// </summary>
		/// <param name="orderedChildren">The container's focusable children in Tab order.</param>
		/// <param name="currentFocused">The currently focused child, or null if none.</param>
		/// <param name="backward">True for Shift+Tab, false for Tab.</param>
		/// <returns>The newly focused control, or null to signal container exit.</returns>
		public IInteractiveControl? TabThroughChildren(
			IReadOnlyList<IInteractiveControl> orderedChildren,
			IInteractiveControl? currentFocused,
			bool backward)
		{
			var newControl = AdvanceTabFocus(orderedChildren, currentFocused, backward);

			// Update focus path with the deepest focused leaf
			if (newControl != null)
			{
				var actualLeaf = FindDeepestFocusedLeaf(newControl) ?? newControl;
				UpdateFocusPath(actualLeaf as IWindowControl ?? newControl as IWindowControl);
			}

			return newControl;
		}

		/// <summary>
		/// Core Tab traversal logic: finds next/previous control, unfocuses old, focuses new.
		/// Static so it can be used even without a coordinator (e.g., in tests without a parent window).
		/// Does NOT update the focus path — call <see cref="TabThroughChildren"/> for that.
		/// </summary>
		/// <param name="orderedChildren">The container's focusable children in Tab order.</param>
		/// <param name="currentFocused">The currently focused child, or null if none.</param>
		/// <param name="backward">True for Shift+Tab, false for Tab.</param>
		/// <returns>The newly focused control, or null to signal container exit.</returns>
		public static IInteractiveControl? AdvanceTabFocus(
			IReadOnlyList<IInteractiveControl> orderedChildren,
			IInteractiveControl? currentFocused,
			bool backward)
		{
			if (orderedChildren.Count == 0)
				return null;

			// Find current position (-1 if null or not in list)
			// Use ReferenceEquals loop — IReadOnlyList doesn't have IndexOf
			int currentIndex = -1;
			if (currentFocused != null)
			{
				for (int i = 0; i < orderedChildren.Count; i++)
				{
					if (ReferenceEquals(orderedChildren[i], currentFocused))
					{
						currentIndex = i;
						break;
					}
				}
			}

			// Compute next index
			int newIndex;
			if (currentIndex == -1)
			{
				// No current focus: forward → first, backward → last
				newIndex = backward ? orderedChildren.Count - 1 : 0;
			}
			else if (backward)
			{
				newIndex = currentIndex - 1;
				if (newIndex < 0)
					return null; // Exit container backward
			}
			else
			{
				newIndex = currentIndex + 1;
				if (newIndex >= orderedChildren.Count)
					return null; // Exit container forward
			}

			// Unfocus current
			if (currentFocused is IFocusableControl currentFc)
				currentFc.SetFocus(false, FocusReason.Keyboard);
			else if (currentFocused != null)
				currentFocused.HasFocus = false;

			// Focus new control with directional support
			var newControl = orderedChildren[newIndex];
			if (newControl is IDirectionalFocusControl directional)
				directional.SetFocusWithDirection(true, backward);
			else if (newControl is IFocusableControl newFc)
				newFc.SetFocus(true, FocusReason.Keyboard);
			else
				newControl.HasFocus = true;

			return newControl;
		}

		/// <summary>
		/// Handles focus for a mouse click. Determines the correct focus target
		/// from a hit-test result (which may be a deep leaf) and routes focus correctly.
		/// </summary>
		/// <param name="clickedControl">The deepest control at the click position (from hit-test), or null for empty space.</param>
		public void HandleClickFocus(IWindowControl? clickedControl)
		{
			var target = DetermineFocusTarget(clickedControl);
			LogService?.LogTrace($"FocusCoordinator.HandleClickFocus: clicked={clickedControl?.GetType().Name ?? "null"} resolved={target?.GetType().Name ?? "null"}", "Focus");

			if (target == null)
			{
				ClearFocus(FocusReason.Mouse);
				return;
			}

			// If target is already focused, no change needed
			if (target == _window._lastFocusedControl as IWindowControl ||
			    target == _window._lastDeepFocusedControl as IWindowControl)
			{
				// But still propagate the click to the container for internal child focus
				PropagateClickFocusToContainers(clickedControl, FocusReason.Mouse);
				return;
			}

			// Find the top-level entry for this target
			var topLevel = FindTopLevelControl(target);

			// Same top-level container, different leaf — just update internal focus
			if (topLevel == _window._lastFocusedControl as IWindowControl && topLevel != null)
			{
				PropagateClickFocusToContainers(clickedControl, FocusReason.Mouse);

				// Update focus path with the new leaf
				if (clickedControl != null)
					UpdateFocusPath(clickedControl);

				// Update legacy leaf tracking (will be removed in Phase 5)
				_window._lastDeepFocusedControl = clickedControl is IInteractiveControl leafInteractive ? leafInteractive : null;
				var focusTarget = _window._lastDeepFocusedControl ?? _window._lastFocusedControl;
				if (focusTarget != null)
					_window.FocusService?.SetFocus(_window, focusTarget, FocusChangeReason.Mouse);
				return;
			}

			// Different top-level container — full focus switch
			RequestFocus(target, FocusReason.Mouse);

			// After focusing the container, propagate click to internal children
			PropagateClickFocusToContainers(clickedControl, FocusReason.Mouse);
		}

		#endregion

		#region Private Helpers

		/// <summary>
		/// Determines the correct focus target from a hit-test result.
		/// Walks UP from the clicked leaf to find the nearest focusable control.
		/// </summary>
		private IWindowControl? DetermineFocusTarget(IWindowControl? clickedControl)
		{
			if (clickedControl == null)
				return null;

			// Portal/overlay content — don't change focus
			if (clickedControl is IMouseAwareControl mouseAware && !mouseAware.CanFocusWithMouse)
				return _window._lastFocusedControl as IWindowControl;

			// Walk up from clicked control to find the nearest focusable ancestor
			IWindowControl? current = clickedControl;
			while (current != null)
			{
				if (current is IFocusableControl fc && fc.CanReceiveFocus)
					return current;

				current = current.Container as IWindowControl;
				if (current is Window)
					break; // Don't go past the window
			}

			// No focusable ancestor found — clear focus
			return null;
		}

		/// <summary>
		/// Builds the container chain from a control up to (but not including) the Window.
		/// Returns list ordered from innermost to outermost.
		/// </summary>
		private List<IWindowControl> BuildContainerChain(IWindowControl control)
		{
			var chain = new List<IWindowControl> { control };
			var current = control.Container;
			while (current != null && current is not Window)
			{
				if (current is IWindowControl wc)
					chain.Add(wc);
				current = (current as IWindowControl)?.Container;
			}
			return chain;
		}

		/// <summary>
		/// Finds the top-level control (direct child of Window) for any control in the tree.
		/// </summary>
		private IWindowControl? FindTopLevelControl(IWindowControl control)
		{
			IWindowControl current = control;
			while (current.Container != null && current.Container is not Window)
			{
				if (current.Container is IWindowControl parent)
					current = parent;
				else
					break;
			}
			return current;
		}

		/// <summary>
		/// Unfocuses the current focus chain — clears HasFocus on focused controls.
		/// </summary>
		private void UnfocusCurrentChain(FocusReason reason)
		{
			// Unfocus all top-level controls that have focus
			foreach (var control in _window._interactiveContents)
			{
				if (control.HasFocus && control is IFocusableControl focusable)
				{
					focusable.SetFocus(false, reason);
				}
			}
		}

		/// <summary>
		/// Focuses the new chain — sets HasFocus on the appropriate control(s).
		/// For opaque containers (like ScrollablePanelControl), focuses the container which
		/// handles internal child focus via SetFocusWithDirection or SetFocus.
		/// </summary>
		private void FocusNewChain(List<IWindowControl> chain, IWindowControl? topLevel, IWindowControl leaf, FocusReason reason)
		{
			// Find the outermost focusable control in the chain — that's what we focus
			// (it will propagate internally to children)
			IWindowControl? focusTarget = null;
			for (int i = chain.Count - 1; i >= 0; i--)
			{
				if (chain[i] is IFocusableControl fc && fc.CanReceiveFocus)
				{
					focusTarget = chain[i];
					break;
				}
			}

			if (focusTarget == null)
				focusTarget = leaf;

			if (focusTarget is IFocusableControl focusable)
			{
				focusable.SetFocus(true, reason);
			}
			else if (focusTarget is IInteractiveControl interactive)
			{
				interactive.HasFocus = true;
			}
		}

		/// <summary>
		/// After a click, propagates focus DOWN through containers to the clicked child.
		/// This handles the case where the top-level container is already focused
		/// but the user clicked a different child inside it.
		/// </summary>
		private void PropagateClickFocusToContainers(IWindowControl? clickedControl, FocusReason reason)
		{
			if (clickedControl == null) return;

			// Walk up from clicked control, notifying each container
			var current = clickedControl;
			while (current != null && current.Container != null)
			{
				var container = current.Container;
				if (container is IFocusTrackingContainer tracker)
				{
					if (current is IInteractiveControl interactive)
						tracker.NotifyChildFocusChanged(interactive, true);
				}

				if (container is Window)
					break;

				current = container as IWindowControl;
			}
		}

		/// <summary>
		/// Computes the focus path from a leaf control up to (but not including) the Window.
		/// Path is stored outermost-first: [NavigationView, SPC, Button].
		/// </summary>
		public void UpdateFocusPath(IWindowControl? leaf)
		{
			_focusPath.Clear();

			if (leaf == null) return;

			// Build path from leaf to root, then reverse
			var current = leaf;
			while (current != null && current is not Window)
			{
				_focusPath.Add(current);
				current = ResolveParentWindowControl(current);
			}
			_focusPath.Reverse();

			LogService?.LogTrace($"FocusPath updated: [{string.Join(" → ", _focusPath.Select(c => c.GetType().Name))}]", "Focus");
		}

		/// <summary>
		/// Resolves the next IWindowControl ancestor of a control, walking through
		/// transparent containers (like ColumnContainer) whose Container property
		/// bypasses their parent HorizontalGridControl.
		/// </summary>
		private static IWindowControl? ResolveParentWindowControl(IWindowControl control)
		{
			// SplitterControl's Container is set to the HGrid's Container (skipping the HGrid).
			// For the focus path, we need to include the HGrid, so resolve through ParentGrid.
			if (control is SplitterControl splitter && splitter.ParentGrid != null)
				return splitter.ParentGrid;

			var container = control.Container;
			if (container == null) return null;

			// ColumnContainer is a transparent layout container whose Container property
			// returns the HGrid's parent (skipping the HGrid). For the focus path, we
			// need to include the HGrid as an ancestor, so we resolve through
			// ColumnContainer.HorizontalGridContent instead of ColumnContainer.Container.
			if (container is ColumnContainer cc)
			{
				var hgrid = cc.HorizontalGridContent;
				if (hgrid != null)
					return hgrid;
			}

			// Standard case: container is IWindowControl
			if (container is IWindowControl wc)
				return wc;

			return null;
		}

		/// <summary>
		/// Finds the deepest control with HasFocus=true inside a container hierarchy.
		/// Used after SetFocusWithDirection to discover what leaf was actually focused.
		/// </summary>
		private static IInteractiveControl? FindDeepestFocusedLeaf(IInteractiveControl control)
		{
			if (control is IContainerControl container)
			{
				foreach (var child in container.GetChildren())
				{
					if (child is IInteractiveControl interactive)
					{
						var deeper = FindDeepestFocusedLeaf(interactive);
						if (deeper != null)
							return deeper;
					}
				}
			}
			return control.HasFocus ? control : null;
		}

		#endregion
	}
}
