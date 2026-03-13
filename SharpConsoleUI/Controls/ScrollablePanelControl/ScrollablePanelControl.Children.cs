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
			if (_hasFocus && _focusedChild == null &&
				control.Visible &&
				control is IFocusableControl fc && fc.CanReceiveFocus)
			{
				_focusedChild = control as IInteractiveControl;
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
			if (_focusedChild == control)
			{
				if (_focusedChild is IFocusableControl fc)
					fc.SetFocus(false, FocusReason.Programmatic);
				_focusedChild = null;
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
			if (_focusedChild is IFocusableControl fc)
				fc.SetFocus(false, FocusReason.Programmatic);
			_focusedChild = null;
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
				if (_focusedChild != null && _focusedChild != child && _focusedChild is IFocusableControl oldFc)
					oldFc.HasFocus = false;

				_focusedChild = child;
				_lastInternalFocusedChild = child;

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}
			else if (_focusedChild == child)
			{
				_focusedChild = null;
			}

			Container?.Invalidate(true);
		}

		#endregion
	}
}
