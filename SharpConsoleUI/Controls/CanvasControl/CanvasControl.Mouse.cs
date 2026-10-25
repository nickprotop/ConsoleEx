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
	public partial class CanvasControl
	{
		#region IMouseAwareControl

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

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

			// Convert to canvas-local coordinates
			int canvasX = args.Position.X - Margin.Left;
			int canvasY = args.Position.Y - Margin.Top;
			bool inBounds = canvasX >= 0 && canvasX < _canvasWidth
				&& canvasY >= 0 && canvasY < _canvasHeight;

			// Press and release come before the click check. They are separate flags
			// arriving on separate frames, not components of a click, so none of the
			// three swallows another.
			//
			// Each is consumed ONLY when something is listening. An unsubscribed
			// press must fall through untouched or it would silently break dragging
			// a window by a canvas that never asked for press events - the common
			// case, since these two events are new and most canvases use clicks.
			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (!inBounds || CanvasMouseDown == null) return false;

				CanvasMouseDown.Invoke(this, new CanvasMouseEventArgs(canvasX, canvasY, args));

				// Focus on the press as well as the click: a control that acts on the
				// press should take the keyboard with it rather than waiting for a
				// release that may land somewhere else.
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1Released))
			{
				if (!inBounds || CanvasMouseUp == null) return false;

				CanvasMouseUp.Invoke(this, new CanvasMouseEventArgs(canvasX, canvasY, args));
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				MouseClick?.Invoke(this, args);
				if (inBounds)
				{
					CanvasMouseClick?.Invoke(this, new CanvasMouseEventArgs(canvasX, canvasY, args));
				}
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				if (inBounds)
				{
					CanvasMouseRightClick?.Invoke(this, new CanvasMouseEventArgs(canvasX, canvasY, args));
				}
				return true;
			}

			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				if (inBounds)
				{
					CanvasMouseMove?.Invoke(this, new CanvasMouseEventArgs(canvasX, canvasY, args));
				}
				return true;
			}

			return false;
		}

		#endregion
	}
}
