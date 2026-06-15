// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Pure step computation for drag-select autoscroll. Given the cursor's control-relative Y, the
	/// viewport height in rows, and the frame's elapsed time, returns the signed number of rows to
	/// scroll this frame. Distance-accelerated and time-normalized, with a fractional carry so speed
	/// stays smooth at any frame rate. No clock, no threads — fully unit-testable.
	/// </summary>
	public static class DragAutoScroll
	{
		/// <summary>
		/// Computes the rows to scroll this frame (signed; negative = up, positive = down, 0 = none).
		/// </summary>
		/// <param name="dragRelativeY">Last drag Y in control-relative rows (may be &lt;0 or ≥ viewport height).</param>
		/// <param name="viewportHeightRows">The control's visible viewport height, in rows.</param>
		/// <param name="elapsedMs">Milliseconds elapsed since the previous frame.</param>
		/// <param name="carry">Sub-row remainder carried across frames. Reset to 0 when in-bounds.</param>
		public static int ComputeStep(int dragRelativeY, int viewportHeightRows, double elapsedMs, ref double carry)
		{
			if (viewportHeightRows <= 0)
			{
				carry = 0;
				return 0;
			}

			int dead = ControlDefaults.DragAutoScrollDeadZoneRows;
			int sign;
			int overshoot;
			if (dragRelativeY < -dead)
			{
				sign = -1;
				overshoot = -dragRelativeY - dead;
			}
			else if (dragRelativeY > viewportHeightRows - 1 + dead)
			{
				sign = 1;
				overshoot = dragRelativeY - (viewportHeightRows - 1) - dead;
			}
			else
			{
				carry = 0;
				return 0;
			}

			double rowsPerSec = ControlDefaults.DragAutoScrollBaseRowsPerSec
				+ overshoot * ControlDefaults.DragAutoScrollAccelRowsPerSecPerRow;
			if (rowsPerSec > ControlDefaults.DragAutoScrollMaxRowsPerSec)
				rowsPerSec = ControlDefaults.DragAutoScrollMaxRowsPerSec;

			double exact = rowsPerSec * elapsedMs / 1000.0 + carry;
			int whole = (int)Math.Floor(exact);
			carry = exact - whole;

			return sign * whole;
		}
	}
}
