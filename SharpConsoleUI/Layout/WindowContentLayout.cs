// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Root layout for window content.
	/// Handles sticky top, scrollable middle, and sticky bottom sections.
	/// </summary>
	public class WindowContentLayout : ILayoutContainer
	{
		/// <summary>
		/// Gets or sets the current scroll offset for the scrollable section.
		/// </summary>
		public int ScrollOffset { get; set; }

		/// <summary>
		/// Gets the total height of scrollable content.
		/// </summary>
		public int ScrollableContentHeight { get; private set; }

		/// <summary>
		/// Gets the height of the scrollable viewport.
		/// </summary>
		public int ViewportHeight { get; private set; }

		/// <summary>
		/// Gets whether scrolling is needed.
		/// </summary>
		public bool CanScroll => ScrollableContentHeight > ViewportHeight;

		/// <summary>
		/// Gets the maximum scroll offset.
		/// </summary>
		public int MaxScrollOffset => Math.Max(0, ScrollableContentHeight - ViewportHeight);

		/// <summary>
		/// Measures all children and returns the total desired size.
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{
			if (node.Children.Count == 0)
				return LayoutSize.Zero;

			int stickyTopHeight = 0;
			int stickyBottomHeight = 0;
			int scrollableHeight = 0;
			int maxWidth = 0;

			// First pass: Measure sticky and non-Fill scrollable children
			int fillChildCount = 0;
			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				var stickyPosition = child.Control?.StickyPosition ?? StickyPosition.None;

				switch (stickyPosition)
				{
					case StickyPosition.Top:
						{
							var childConstraints = constraints.SubtractHeight(stickyTopHeight);
							var childSize = child.Measure(childConstraints);
							stickyTopHeight += childSize.Height;
							maxWidth = Math.Max(maxWidth, childSize.Width);
							break;
						}

					case StickyPosition.Bottom:
						{
							var childConstraints = constraints.SubtractHeight(stickyBottomHeight);
							var childSize = child.Measure(childConstraints);
							stickyBottomHeight += childSize.Height;
							maxWidth = Math.Max(maxWidth, childSize.Width);
							break;
						}

					default: // None - scrollable
						{
							if (child.VerticalAlignment == VerticalAlignment.Fill)
							{
								// Count Fill children for second pass
								fillChildCount++;
							}
							else
							{
								// Measure non-Fill scrollable children with unlimited height
								var childConstraints = constraints.WithMaxHeight(int.MaxValue);
								var childSize = child.Measure(childConstraints);
								scrollableHeight += childSize.Height;
								maxWidth = Math.Max(maxWidth, childSize.Width);
							}
							break;
						}
				}
			}

			// Second pass: Measure Fill scrollable children with remaining space divided among them
			int availableViewportHeight = Math.Max(0, constraints.MaxHeight - stickyTopHeight - stickyBottomHeight - scrollableHeight);
			int fillChildHeight = fillChildCount > 0 ? availableViewportHeight / fillChildCount : 0;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				var stickyPosition = child.Control?.StickyPosition ?? StickyPosition.None;

				if (stickyPosition == StickyPosition.None && child.VerticalAlignment == VerticalAlignment.Fill)
				{
					// Measure Fill children with divided remaining space
					var childConstraints = constraints.WithMaxHeight(fillChildHeight);
					var childSize = child.Measure(childConstraints);
					scrollableHeight += childSize.Height;
					maxWidth = Math.Max(maxWidth, childSize.Width);
				}
			}

			// Store scrollable metrics
			ScrollableContentHeight = scrollableHeight;
			ViewportHeight = Math.Max(0, constraints.MaxHeight - stickyTopHeight - stickyBottomHeight);

			// Total desired height includes all content
			int totalHeight = stickyTopHeight + scrollableHeight + stickyBottomHeight;

			return new LayoutSize(
				Math.Min(maxWidth, constraints.MaxWidth),
				Math.Min(totalHeight, constraints.MaxHeight)
			);
		}

		/// <summary>
		/// Arranges children with sticky positioning.
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
		{
			if (node.Children.Count == 0)
				return;

			// First pass: calculate sticky heights and count fill children
			int stickyTopHeight = 0;
			int stickyBottomHeight = 0;
			int fixedScrollableHeight = 0;
			int fillChildCount = 0;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				var stickyPosition = child.Control?.StickyPosition ?? StickyPosition.None;

				switch (stickyPosition)
				{
					case StickyPosition.Top:
						stickyTopHeight += child.DesiredSize.Height;
						break;
					case StickyPosition.Bottom:
						stickyBottomHeight += child.DesiredSize.Height;
						break;
					default: // None - scrollable
						if (child.VerticalAlignment == VerticalAlignment.Fill)
						{
							fillChildCount++;
						}
						else
						{
							fixedScrollableHeight += child.DesiredSize.Height;
						}
						break;
				}
			}

			// Calculate scrollable area
			int scrollableTop = stickyTopHeight;
			int scrollableBottom = finalRect.Height - stickyBottomHeight;
			ViewportHeight = scrollableBottom - scrollableTop;

			// Calculate height for fill children
			int remainingHeight = Math.Max(0, ViewportHeight - fixedScrollableHeight);
			int fillChildHeight = fillChildCount > 0 ? remainingHeight / fillChildCount : 0;
			int extraHeight = fillChildCount > 0 ? remainingHeight % fillChildCount : 0;

			// Clamp scroll offset
			ScrollOffset = Math.Clamp(ScrollOffset, 0, MaxScrollOffset);

			// Arrange children
			int currentStickyTopY = 0;
			int currentScrollableY = -ScrollOffset; // Start offset by scroll position
			int currentStickyBottomY = scrollableBottom;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				var stickyPosition = child.Control?.StickyPosition ?? StickyPosition.None;

				switch (stickyPosition)
				{
					case StickyPosition.Top:
						{
							child.Arrange(new LayoutRect(
								0, currentStickyTopY,
								finalRect.Width, child.DesiredSize.Height));
							currentStickyTopY += child.DesiredSize.Height;
							break;
						}

					case StickyPosition.Bottom:
						{
							child.Arrange(new LayoutRect(
								0, currentStickyBottomY,
								finalRect.Width, child.DesiredSize.Height));
							currentStickyBottomY += child.DesiredSize.Height;
							break;
						}

					default: // None - scrollable
						{
							// Position relative to scrollable area with scroll offset applied
							int visualY = scrollableTop + currentScrollableY;

							// Determine child height - fill children get remaining space
							int childHeight;
							if (child.VerticalAlignment == VerticalAlignment.Fill)
							{
								childHeight = fillChildHeight + (extraHeight > 0 ? 1 : 0);
								extraHeight--;
							}
							else
							{
								childHeight = child.DesiredSize.Height;
							}

							var childRect = new LayoutRect(0, visualY, finalRect.Width, childHeight);
							child.Arrange(childRect);

							currentScrollableY += childHeight;
							break;
						}
				}
			}
		}

		/// <summary>
		/// Scrolls to ensure the specified Y position is visible in the scrollable area.
		/// </summary>
		public void EnsureVisible(int y)
		{
			if (y < ScrollOffset)
			{
				ScrollOffset = y;
			}
			else if (y >= ScrollOffset + ViewportHeight)
			{
				ScrollOffset = y - ViewportHeight + 1;
			}
			ScrollOffset = Math.Clamp(ScrollOffset, 0, MaxScrollOffset);
		}

		/// <summary>
		/// Scrolls by the specified amount.
		/// </summary>
		public void ScrollBy(int delta)
		{
			ScrollOffset = Math.Clamp(ScrollOffset + delta, 0, MaxScrollOffset);
		}

		/// <summary>
		/// Scrolls to the top.
		/// </summary>
		public void ScrollToTop()
		{
			ScrollOffset = 0;
		}

		/// <summary>
		/// Scrolls to the bottom.
		/// </summary>
		public void ScrollToBottom()
		{
			ScrollOffset = MaxScrollOffset;
		}

		/// <summary>
		/// Scrolls up by one page.
		/// </summary>
		public void PageUp()
		{
			ScrollBy(-ViewportHeight);
		}

		/// <summary>
		/// Scrolls down by one page.
		/// </summary>
		public void PageDown()
		{
			ScrollBy(ViewportHeight);
		}
	}
}
