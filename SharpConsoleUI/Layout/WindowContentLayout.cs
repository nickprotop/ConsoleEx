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
	public class WindowContentLayout : ILayoutContainer, IRegionClippingLayout
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
		/// Gets the height of sticky top controls (set during arrange).
		/// </summary>
		public int StickyTopHeight { get; private set; }

		/// <summary>
		/// Gets the height of sticky bottom controls (set during arrange).
		/// </summary>
		public int StickyBottomHeight { get; private set; }

		/// <summary>
		/// Gets the Y position where scrollable content starts (set during arrange).
		/// </summary>
		public int ScrollableTop { get; private set; }

		/// <summary>
		/// Gets the Y position where scrollable content ends (set during arrange).
		/// </summary>
		public int ScrollableBottom { get; private set; }

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

			// Store sticky boundaries for paint clipping
			StickyTopHeight = stickyTopHeight;
			StickyBottomHeight = stickyBottomHeight;
			ScrollableTop = scrollableTop;
			ScrollableBottom = scrollableBottom;

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
							var (childX, childWidth) = CalculateHorizontalPosition(child, finalRect.Width);
							child.Arrange(new LayoutRect(
								childX, currentStickyTopY,
								childWidth, child.DesiredSize.Height));
							currentStickyTopY += child.DesiredSize.Height;
							break;
						}

					case StickyPosition.Bottom:
						{
							var (childX, childWidth) = CalculateHorizontalPosition(child, finalRect.Width);
							child.Arrange(new LayoutRect(
								childX, currentStickyBottomY,
								childWidth, child.DesiredSize.Height));
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

							var (childX, childWidth) = CalculateHorizontalPosition(child, finalRect.Width);
							var childRect = new LayoutRect(childX, visualY, childWidth, childHeight);
							child.Arrange(childRect);

							currentScrollableY += childHeight;
							break;
						}
				}
			}
		}

		/// <summary>
		/// Calculates the X position and width for a child based on its HorizontalAlignment.
		/// </summary>
		private (int X, int Width) CalculateHorizontalPosition(LayoutNode child, int availableWidth)
		{
			var alignment = child.Control?.HorizontalAlignment ?? HorizontalAlignment.Stretch;

			switch (alignment)
			{
				case HorizontalAlignment.Left:
					return (0, child.DesiredSize.Width);

				case HorizontalAlignment.Center:
					int centeredX = (availableWidth - child.DesiredSize.Width) / 2;
					return (Math.Max(0, centeredX), child.DesiredSize.Width);

				case HorizontalAlignment.Right:
					int rightX = availableWidth - child.DesiredSize.Width;
					return (Math.Max(0, rightX), child.DesiredSize.Width);

				case HorizontalAlignment.Stretch:
				default:
					return (0, availableWidth);
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

		/// <summary>
		/// Gets the paint clip rectangle for a child node based on its sticky position.
		/// This prevents scrollable content from painting over sticky regions.
		/// </summary>
		/// <param name="child">The child node to get the clip rectangle for.</param>
		/// <param name="parentClipRect">The parent's clip rectangle.</param>
		/// <returns>A restricted clip rectangle based on the child's sticky position.</returns>
		public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
		{
			var stickyPosition = child.Control?.StickyPosition ?? StickyPosition.None;

			switch (stickyPosition)
			{
				case StickyPosition.Top:
					// Sticky top controls can only paint in the top sticky region
					return new LayoutRect(
						parentClipRect.X,
						parentClipRect.Y,
						parentClipRect.Width,
						Math.Min(StickyTopHeight, parentClipRect.Height));

				case StickyPosition.Bottom:
					// Sticky bottom controls can only paint in the bottom sticky region
					int bottomStartY = Math.Max(parentClipRect.Y, ScrollableBottom);
					return new LayoutRect(
						parentClipRect.X,
						bottomStartY,
						parentClipRect.Width,
						Math.Max(0, parentClipRect.Bottom - bottomStartY));

				default: // None - scrollable
					// Scrollable controls can only paint in the scrollable region (between sticky areas)
					int scrollableStartY = Math.Max(parentClipRect.Y, ScrollableTop);
					int scrollableEndY = Math.Min(parentClipRect.Bottom, ScrollableBottom);
					return new LayoutRect(
						parentClipRect.X,
						scrollableStartY,
						parentClipRect.Width,
						Math.Max(0, scrollableEndY - scrollableStartY));
			}
		}
	}
}
