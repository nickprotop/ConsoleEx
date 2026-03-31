// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class VideoControl
	{
		#region IMouseAwareControl

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
#pragma warning disable CS0067 // Event never raised (interface requirement)
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
#pragma warning restore CS0067

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
#pragma warning disable CS0067 // Event never raised (interface requirement)
		public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return true;
			}

			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				MouseLeave?.Invoke(this, args);
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				MouseClick?.Invoke(this, args);
				ShowOverlay();
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				ShowOverlay();
				return true;
			}

			return false;
		}

		#endregion
	}
}
