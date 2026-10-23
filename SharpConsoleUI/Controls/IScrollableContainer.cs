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
	/// Interface for containers that can scroll to bring children into view.
	/// Used by BringIntoFocus to notify parent containers when nested child receives focus.
	/// </summary>
	public interface IScrollableContainer
	{
		/// <summary>
		/// Scrolls the container to bring the specified child control into view.
		/// Should also show/highlight scrollbars if applicable.
		/// </summary>
		/// <param name="child">The child control to bring into view (may be deeply nested)</param>
		/// <remarks>
		/// Implementation should use child.AbsoluteBounds to calculate position,
		/// which works correctly for deeply nested children (grandchildren, etc).
		/// </remarks>
		void ScrollChildIntoView(IWindowControl child);

		/// <summary>
		/// Scrolls so that a sub-region of <paramref name="child"/> is visible. The region is given in the
		/// child's own content coordinates: <paramref name="childRelativeTop"/> rows from the child's top,
		/// spanning <paramref name="regionHeight"/> rows. Used to bring a focused element's row into view when
		/// the child itself does not scroll. Default implementation degrades to <see cref="ScrollChildIntoView(IWindowControl)"/>.
		/// </summary>
		/// <param name="child">The (direct) child whose sub-region should be made visible.</param>
		/// <param name="childRelativeTop">Row offset of the region from the child's top edge. A negative value is clamped to 0.</param>
		/// <param name="regionHeight">Height of the region in rows. A value less than 1 is treated as 1.</param>
		void ScrollChildRegionIntoView(IWindowControl child, int childRelativeTop, int regionHeight)
			=> ScrollChildIntoView(child);

		/// <summary>
		/// Scrolls so that <paramref name="nestedTarget"/> is visible, where the target sits somewhere
		/// inside <paramref name="child"/> rather than being the direct child itself.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Revealing the direct child is not enough when that child is tall: a container that already
		/// spans the viewport is deliberately left alone by the tall-child guard, so a target further
		/// down it never comes into view. This resolves the target's own row within the child and
		/// reveals that row instead.
		/// </para>
		/// <para>
		/// Use this to re-reveal a control that is already focused - <c>SetFocus</c> returns early when
		/// the focus does not change, so focusing it again scrolls nothing.
		/// </para>
		/// </remarks>
		/// <param name="child">The direct child of this container that contains the target.</param>
		/// <param name="nestedTarget">The control to reveal. Passing the direct child itself is allowed and degrades to <see cref="ScrollChildIntoView(IWindowControl)"/>.</param>
		void ScrollChildIntoView(IWindowControl child, IWindowControl nestedTarget)
		{
			if (ReferenceEquals(child, nestedTarget))
			{
				ScrollChildIntoView(child);
				return;
			}

			if (Helpers.FocusScrollHelper.TryGetFocusedRowInDirectChild(
					child, nestedTarget, out int rowInChild, out int regionHeight, this))
			{
				ScrollChildRegionIntoView(child, rowInChild, regionHeight);
			}
			else
			{
				ScrollChildIntoView(child);
			}
		}
	}
}
