// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region Child Control Management

		/// <summary>
		/// Adds a child control to the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// For multi-threaded scenarios, queue additions and process them during paint.
		/// </summary>
		public void AddControl(IWindowControl control)
		{
			lock (_childrenLock)
			{
				_children.Add(control);
			}
			control.Container = this;
			// If the panel has focus but no focused child yet, focus the new control
			// if it's focusable. Restores focus routing after ClearContents.
			if (_hasFocus && GetFocusedChildFromCoordinator() == null &&
				control.Visible &&
				control is IFocusableControl fc && fc.CanReceiveFocus)
			{
				if (control is IDirectionalFocusControl dfc)
					dfc.SetFocusWithDirection(true, false);
				else
					fc.SetFocus(true, FocusReason.Programmatic);
			}
			Invalidate(true);
		}

		/// <summary>
		/// Inserts a child control at the specified index in the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// </summary>
		/// <param name="index">The zero-based index at which to insert the control.</param>
		/// <param name="control">The control to insert.</param>
		public void InsertControl(int index, IWindowControl control)
		{
			lock (_childrenLock)
			{
				index = Math.Clamp(index, 0, _children.Count);
				_children.Insert(index, control);
			}
			control.Container = this;
			Invalidate(true);
		}

		/// <summary>
		/// Removes a child control from the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// </summary>
		public void RemoveControl(IWindowControl control)
		{
			// If removing the focused child, clear focus
			var focusedChild = GetFocusedChildFromCoordinator();
			if (focusedChild != null && focusedChild == control as IInteractiveControl)
			{
				if (focusedChild is IFocusableControl fc)
					fc.SetFocus(false, FocusReason.Programmatic);
			}

			// Clear remembered child if it's being removed
			if (_lastInternalFocusedChild == control as IInteractiveControl)
				_lastInternalFocusedChild = null;

			bool removed;
			lock (_childrenLock)
			{
				removed = _children.Remove(control);
			}

			if (removed)
			{
				control.Container = null;

				// If we're no longer focusable (lost all children and no scrolling), lose focus
				if (_hasFocus && !CanReceiveFocus)
				{
					SetFocus(false, FocusReason.Programmatic);
				}

				Invalidate(true);
			}
		}

		/// <summary>
		/// Removes all child controls from the panel.
		/// </summary>
		public void ClearContents()
		{
			var focusedChild = GetFocusedChildFromCoordinator();
			if (focusedChild is IFocusableControl fc)
				fc.SetFocus(false, FocusReason.Programmatic);
			_lastInternalFocusedChild = null;

			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				snapshot = new List<IWindowControl>(_children);
				_children.Clear();
			}

			foreach (var child in snapshot)
			{
				child.Container = null;
				child.Dispose();
			}

			Invalidate(true);
		}

		/// <summary>
		/// Gets the collection of child controls.
		/// </summary>
		public IReadOnlyList<IWindowControl> Children
		{
			get { lock (_childrenLock) { return new List<IWindowControl>(_children); } }
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				// The notification may come from a deeply nested control (e.g. a button
				// inside a ColumnContainer inside a HorizontalGrid). We need to find the
				// direct child of this panel that contains the notifying control, so that
				// the coordinator's path correctly points to a direct child for Tab navigation.
				var directChild = FindDirectChildContaining(child) ?? child;

				var currentFocused = GetFocusedChildFromCoordinator();
				if (currentFocused != null && currentFocused != directChild && currentFocused is IFocusableControl oldFc)
					oldFc.HasFocus = false;

				_lastInternalFocusedChild = directChild;

				// Note: We do NOT update the coordinator's focus path here.
				// The notification chain passes currentNotifyTarget (which may be
				// an intermediate container, not the actual leaf), so calling
				// UpdateFocusPath with it would produce a truncated path.
				// The path is updated by the coordinator's own entry points
				// (MoveFocus, RequestFocus, HandleClickFocus) or by SPC's own
				// ProcessKey/SetFocus methods after focus delegation completes.

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}
			else
			{
				// When a child loses focus, the coordinator path may already be updated
				// by the unfocusing child's notification chain. No explicit path update needed.
			}

			Container?.Invalidate(true);
		}

		/// <summary>
		/// Finds the direct child of this panel that contains the given control.
		/// Returns the control itself if it IS a direct child, or null if not found.
		/// </summary>
		private IInteractiveControl? FindDirectChildContaining(IInteractiveControl control)
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			// Check if it's a direct child
			if (control is IWindowControl wc && snapshot.Contains(wc))
				return control;

			// Check if any direct child contains this control
			foreach (var child in snapshot)
			{
				if (child is IContainerControl container && ContainsControl(container, control))
				{
					if (child is IInteractiveControl interactive)
						return interactive;
				}
			}

			return null;
		}

		/// <summary>
		/// Recursively checks if a container contains the given control.
		/// </summary>
		private static bool ContainsControl(IContainerControl container, IInteractiveControl control)
		{
			foreach (var child in container.GetChildren())
			{
				if (child == control as IWindowControl)
					return true;
				if (child is IContainerControl nested && ContainsControl(nested, control))
					return true;
			}
			return false;
		}

		#endregion
	}
}
