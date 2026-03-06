// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;

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

			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				MouseClick?.Invoke(this, args);
				if (inBounds)
				{
					CanvasMouseClick?.Invoke(this, new CanvasMouseEventArgs(canvasX, canvasY, args));
				}
				SetFocus(true, FocusReason.Mouse);
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
