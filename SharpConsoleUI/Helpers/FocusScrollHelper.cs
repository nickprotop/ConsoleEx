// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Shared helpers for the focus scroll-into-view walk. When the focused control is nested
	/// deeper than a scroller's direct child (e.g. a button inside a non-scrolling
	/// <c>CollapsiblePanel</c> inside a <c>ScrollablePanelControl</c>), revealing the whole
	/// direct child is wrong: a tall direct child that already intersects the viewport is skipped
	/// by the "don't yank a spanning container to its top" guard, so the off-fold leaf is never
	/// revealed. These helpers compute the focused leaf's true row WITHIN the direct child so the
	/// scroller can reveal that row specifically.
	/// </summary>
	public static class FocusScrollHelper
	{
		/// <summary>
		/// Computes the vertical offset (in rows, from the direct child's top edge) of
		/// <paramref name="focused"/> within <paramref name="directChild"/>, and the focused
		/// control's height. Works even when <paramref name="focused"/> is off-viewport and was
		/// therefore never painted (its <c>ActualY</c> is stale/zero): it arranges a transient copy
		/// of the direct child's own layout subtree at the direct child's actual on-screen size and
		/// reads the focused node's arranged bounds — the exact idiom used by
		/// <c>CollapsiblePanel.FocusedChildOffsetInPanel</c> for cursor placement, so the offset can
		/// never desync from where the body is actually laid out.
		/// </summary>
		/// <param name="directChild">The scroller's direct child that contains the focused control.</param>
		/// <param name="focused">The focused control nested somewhere inside <paramref name="directChild"/>.</param>
		/// <param name="rowInChild">On success, the focused control's top row relative to the direct child's top.</param>
		/// <param name="regionHeight">On success, the focused control's arranged height (at least 1).</param>
		/// <returns><c>true</c> if the offset was resolved from the layout subtree; otherwise <c>false</c>.</returns>
		public static bool TryGetFocusedRowInDirectChild(
			IWindowControl directChild,
			IWindowControl focused,
			out int rowInChild,
			out int regionHeight)
		{
			rowInChild = 0;
			regionHeight = 1;

			// A scrollable direct child manages its own viewport: reveal it as a unit and let its own
			// scroll system bring the focused leaf into view. Probing its transient subtree here would
			// also mutate the LIVE panel's child-node resolver / scroll offset during measure/arrange.
			if (directChild is IScrollableContainer)
				return false;

			int width = directChild.ActualWidth > 0 ? directChild.ActualWidth : (directChild.Width ?? 0);
			int height = directChild.ActualHeight;
			if (width <= 0 || height <= 0)
				return false;

			LayoutNode subtree;
			try
			{
				subtree = LayoutNodeFactory.CreateSubtree(directChild);
				subtree.Measure(new LayoutConstraints(1, width, 1, height));
				subtree.Arrange(new LayoutRect(0, 0, width, height));
			}
			catch
			{
				// A container that does not participate in DOM subtree layout can't be probed this
				// way; the caller falls back to revealing the whole direct child.
				return false;
			}

			// Locate the focused control in the transient subtree. Self-painting containers
			// (ToolbarControl, ScrollablePanelControl, …) don't expose their items as DOM children, so
			// a deeply-hosted focused control won't appear as its own node. In that case, walk up from
			// the focused control toward the direct child and reveal the nearest ancestor that IS a
			// node in the subtree — that ancestor (e.g. the short toolbar row hosting the button) is
			// bounded and brings the focused element into view without over-revealing the whole panel.
			var childNode = subtree.FindByControl(focused);
			if (childNode == null)
			{
				IWindowControl? ancestor = SharpConsoleUI.Core.FocusManager.ResolveParentWindowControl(focused);
				while (ancestor != null && !ReferenceEquals(ancestor, directChild))
				{
					childNode = subtree.FindByControl(ancestor);
					if (childNode != null)
						break;
					ancestor = SharpConsoleUI.Core.FocusManager.ResolveParentWindowControl(ancestor);
				}
			}

			if (childNode == null)
				return false;

			// Subtree root is arranged at the origin, so the located node's AbsoluteBounds is already
			// its offset within the direct child.
			var ab = childNode.AbsoluteBounds;
			rowInChild = ab.Y;
			regionHeight = System.Math.Max(1, ab.Height);
			return true;
		}
	}
}
