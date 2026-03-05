// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace SharpConsoleUI
{
	public partial class Window
	{
	/// <summary>
	/// Gets a flattened list of all focusable controls by recursively traversing IContainerControl children.
		/// Focusable containers are "opaque" — they are added to the list but their children are NOT
		/// included (the container handles internal Tab navigation for its children).
		/// Non-focusable containers are "transparent" — they are skipped but their children are included.
		/// </summary>
		internal List<IInteractiveControl> GetAllFocusableControlsFlattened()
		{
			var result = new List<IInteractiveControl>();

			void RecursiveAdd(IWindowControl control)
			{
				// If this is a container, check if it's focusable
				if (control is Controls.IContainerControl container)
				{
					bool isFocusable = false;
					if (control is Controls.IFocusableControl fc)
					{
						isFocusable = fc.CanReceiveFocus;
					}
					else if (control is IInteractiveControl ic)
					{
						isFocusable = ic.IsEnabled;
					}

					// Focusable container: add it; then also recurse for header-style containers
					if (isFocusable && control is IInteractiveControl interactiveContainer)
					{
						result.Add(interactiveContainer);
						// IFocusableContainerWithHeader: header is a focus stop AND visible children
						// also appear in Tab order immediately after it (e.g. TabControl).
						if (container is Controls.IFocusableContainerWithHeader)
						{
							foreach (var child in container.GetChildren())
								if (child.Visible) RecursiveAdd(child);
						}
						return; // Don't recurse for regular opaque containers
					}

					// Non-focusable container: transparent — recurse into visible children only
					foreach (var child in container.GetChildren())
					{
						if (child.Visible)
							RecursiveAdd(child);
					}
				}
				// Leaf control - check if it's interactive/focusable
				else if (control is IInteractiveControl interactive)
				{
					if (control is Controls.IFocusableControl fc)
					{
						if (fc.CanReceiveFocus)
						{
							result.Add(interactive);
						}
					}
					else
					{
						if (interactive.IsEnabled)
						{
							result.Add(interactive);
						}
					}
				}
			}

			// Start from top-level controls
			foreach (var control in _controls.Where(c => c.Visible))
			{
				RecursiveAdd(control);
			}

			return result;
		}

		/// <summary>
		/// Finds the deepest focused control by recursively checking containers
		/// </summary>
		internal IWindowControl? FindDeepestFocusedControl(IInteractiveControl control)
		{
			// Search all container children recursively — HasFocus does NOT propagate
			// upward through containers, so we must traverse into every container to find
			// the deepest leaf control that actually has focus.
			if (control is Controls.IContainerControl container)
			{
				foreach (var child in container.GetChildren())
				{
					if (child is IInteractiveControl interactive)
					{
						var result = FindDeepestFocusedControl(interactive);
						if (result != null)
							return result;
					}
				}
			}

			return control.HasFocus ? control as IWindowControl : null;
		}

		/// <summary>
		/// Notifies the window that a control has lost focus.
		/// </summary>
		/// <param name="control">The control that lost focus.</param>
		public void NotifyControlFocusLost(IInteractiveControl control)
		{
			if (control != null && _interactiveContents.Contains(control))
			{
				_lastFocusedControl = control;
			}
		}

		/// <summary>
		/// Called by controls when they gain focus (via SetFocus).
		/// Updates Window's focus tracking to keep _lastFocusedControl in sync.
		/// </summary>
		/// <param name="control">The control that gained focus.</param>
		/// <param name="actualFocusedControl">The actual leaf control that gained focus (may differ from control when nested).</param>
		public void NotifyControlGainedFocus(IInteractiveControl control,
			IInteractiveControl? actualFocusedControl = null)
		{
			bool isTopLevel = control != null && _interactiveContents.Contains(control);
			_windowSystem?.LogService?.LogTrace($"NotifyControlGainedFocus: {control?.GetType().Name} isTopLevel={isTopLevel} (only top-level updates _lastFocusedControl)", "Focus");
			// _lastFocusedControl: only update for top-level Tab-cycle entries (direct _interactiveContents members)
			if (isTopLevel)
				_lastFocusedControl = control;

			// _lastDeepFocusedControl and FocusStateService must always be updated — the outermost
			// container reached by the walk (e.g. ScrollablePanelControl inside HorizontalGrid)
			// is NOT in _interactiveContents, so isTopLevel would be false, but the leaf still needs tracking.
			bool isIntermediateContainer = actualFocusedControl is Controls.IFocusTrackingContainer;
			bool hasValidLeaf = _lastDeepFocusedControl is Controls.IFocusableControl leafFc && leafFc.HasFocus;
			var prevDeep = _lastDeepFocusedControl;
			if (!isIntermediateContainer)
			{
				// True leaf (ListControl, ButtonControl, etc.) — always prefer it
				_lastDeepFocusedControl = actualFocusedControl ?? control;
			}
			else if (!hasValidLeaf)
			{
				// No valid leaf tracked — use the intermediate container as fallback
				_lastDeepFocusedControl = actualFocusedControl ?? control;
			}
			// else: intermediate container notified, valid leaf already tracked — preserve it
			FocusService?.SetFocus(this, _lastDeepFocusedControl ?? control, FocusChangeReason.Programmatic);
		}

		/// <summary>
		/// Called by controls when they lose focus (via SetFocus).
		/// Updates Window's focus tracking to keep _lastFocusedControl in sync.
		/// </summary>
		/// <param name="control">The control that lost focus.</param>
		public void NotifyControlLostFocus(IInteractiveControl control)
		{
			bool isTracked = control != null && _lastFocusedControl == control;
			_windowSystem?.LogService?.LogTrace($"NotifyControlLostFocus: {control?.GetType().Name} isTracked={isTracked} _lastFocused={_lastFocusedControl?.GetType().Name ?? "null"}", "Focus");

			// Clear tracking if this was the last focused control AND it actually lost focus
			// (not just a child inside it losing focus while the container maintains focus)
			if (isTracked && control is Controls.IFocusableControl focusable && !focusable.HasFocus)
			{
				_lastFocusedControl = null;
				_lastDeepFocusedControl = null;
				FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
			}
		}


		/// <summary>
		/// Sets focus to the specified control in this window.
		/// This is the recommended way to programmatically change focus, as it properly
		/// updates Window's internal focus tracking and unfocuses the previously focused control.
		/// </summary>
		/// <param name="control">The control to focus, or null to clear focus entirely.</param>
		public void FocusControl(IInteractiveControl? control)
		{
			// Unfocus currently focused control
			if (_lastFocusedControl != null && _lastFocusedControl is Controls.IFocusableControl currentFocusable)
			{
				currentFocusable.SetFocus(false, Controls.FocusReason.Programmatic);
			}

			// Focus new control
			if (control != null && control is Controls.IFocusableControl newFocusable && newFocusable.CanReceiveFocus)
			{
				// If the control has stale focus (e.g. window deactivation only cleared the container's
				// HasFocus, not the deep leaf), reset it first so GotFocus fires and IsEditing is restored.
				if (newFocusable.HasFocus)
					newFocusable.SetFocus(false, Controls.FocusReason.Programmatic);

				newFocusable.SetFocus(true, Controls.FocusReason.Programmatic);

				// Explicitly update _lastFocusedControl so keyboard routing (HasActiveInteractiveContent
				// fallback 2) reaches this control directly. Controls nested inside containers (e.g.
				// MultilineEditControl inside TabControl) are not in _interactiveContents, so
				// NotifyControlGainedFocus won't update _lastFocusedControl for them.
				_lastFocusedControl = control;
			}
			else
			{
				_lastFocusedControl = null;
				FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
			}
		}

		/// <summary>
		/// Switches focus to the next or previous interactive control in the window.
		/// </summary>
		/// <param name="backward">True to move focus backward; false to move forward.</param>
		public void SwitchFocus(bool backward = false)
		{
			_eventDispatcher?.SwitchFocus(backward);
		}

		/// <summary>
		/// Removes focus from the currently focused control.
		/// </summary>
		public void UnfocusCurrentControl()
		{
			if (_lastFocusedControl != null && _lastFocusedControl is Controls.IFocusableControl focusable)
			{
				focusable.SetFocus(false, Controls.FocusReason.Programmatic);
				// Guard: only clear FSS if this window is currently the focused window.
				// Without this, deactivating a background window wipes the active window's focus.
				if (FocusService?.FocusedWindow == this)
				{
					FocusService.ClearControlFocus(FocusChangeReason.Programmatic);
				}
			}
		}
	}
}
