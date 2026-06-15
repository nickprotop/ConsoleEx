// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Implemented by selectable controls that participate in drag-select autoscroll. When a text
	/// drag-select is active, the control registers itself with the window system; the main loop then
	/// drives continuous scrolling each frame while the cursor is held past the viewport edge.
	/// All coordinates are CONTROL-RELATIVE rows (matching what the drag handler receives).
	/// </summary>
	public interface IDragAutoScrollTarget
	{
		/// <summary>True while a Button1 text-drag-select is in progress.</summary>
		bool IsDragSelecting { get; }

		/// <summary>
		/// True when the control has painted at least once and its viewport metrics are known.
		/// The autoscroll tick no-ops until this is true.
		/// </summary>
		bool IsViewportReady { get; }

		/// <summary>
		/// The last drag Y in control-relative rows. May be negative (above the viewport top) or
		/// ≥ <see cref="ViewportHeightRows"/> (below the bottom) — that overshoot drives the scroll.
		/// </summary>
		int LastDragRelativeY { get; }

		/// <summary>The control's effective visible viewport height, in rows.</summary>
		int ViewportHeightRows { get; }

		/// <summary>
		/// Scrolls by <paramref name="rows"/> (sign = direction). The control chooses how (host
		/// <c>IScrollableContainer</c>, <c>Window.ScrollBy</c>, or its own offset) and clamps at the
		/// scroll extent. A no-op when no scrollable host exists.
		/// </summary>
		void AutoScrollStep(int rows);

		/// <summary>
		/// After a scroll step, extends the selection's end to the edge row now revealed:
		/// <paramref name="direction"/> -1 = the new top row, +1 = the new bottom row. Computes the
		/// row from the post-scroll position arithmetically (never from the stale pre-paint cache).
		/// </summary>
		void ExtendSelectionToRevealedEdge(int direction);
	}
}
