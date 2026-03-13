// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Controls
{
	public partial class NavigationView
	{
		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => true;

#pragma warning disable CS0067 // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			// Delegate to the grid — it handles dispatching to columns and their children
			return _grid.ProcessMouseEvent(args);
		}

		#endregion

		#region IInteractiveControl / IFocusableControl Implementation

		private bool _hasFocus;

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;
				OnPropertyChanged();
				_grid.HasFocus = value;
				Container?.Invalidate(true);

				if (value && !hadFocus)
					GotFocus?.Invoke(this, EventArgs.Empty);
				else if (!value && hadFocus)
					LostFocus?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		public bool CanReceiveFocus => false; // Grid manages focus internally

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		/// <inheritdoc/>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_grid.SetFocusWithDirection(focus, backward);
		}

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// Delegate to the grid for Tab navigation between columns
			return _grid.ProcessKey(key);
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			// The grid is our only child — propagate focus tracking upward
			if (child == _grid || child is HorizontalGridControl)
			{
				if (hasFocus && !_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!hasFocus && _hasFocus)
				{
					_hasFocus = false;
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}

			Container?.Invalidate(true);
		}

		#endregion
	}
}
