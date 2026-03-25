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
	public partial class TimePickerControl
	{
		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled || !WantsMouseEvents)
				return false;

			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				MouseLeave?.Invoke(this, args);
				return true;
			}

			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return true;
			}

			if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return true;
			}

			// Determine absolute click X position
			int clickAbsX = _lastLayoutBounds.X + args.Position.X;
			int clickAbsY = _lastLayoutBounds.Y + args.Position.Y;

			// Check within content row
			int contentY = _lastLayoutBounds.Y + Margin.Top;
			if (clickAbsY != contentY)
				return false;

			// Mouse wheel on focused segment — only respond if already focused.
			// Don't steal focus on wheel; let parent scroll instead.
			if (args.HasAnyFlag(MouseFlags.WheeledDown | MouseFlags.WheeledUp))
			{
				if (!HasFocus)
					return false;

				int delta = args.HasFlag(MouseFlags.WheeledUp) ? 1 : -1;
				_pendingDigit = -1;
				IncrementSegment(_focusedSegment, delta);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (!HasFocus)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				// Hit-test against segment positions
				int hitSegment = HitTestSegment(clickAbsX);
				if (hitSegment >= 0)
				{
					_pendingDigit = -1;
					_focusedSegment = hitSegment;
					Container?.Invalidate(true);
				}

				MouseClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			return false;
		}

		private int HitTestSegment(int absX)
		{
			for (int i = 0; i < _segmentXPositions.Length; i++)
			{
				int segStart = _segmentXPositions[i];
				int segEnd = segStart + _segmentWidths[i];
				if (absX >= segStart && absX < segEnd)
					return i;
			}
			return -1;
		}
	}
}
