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
	/// A layout node that supports scrolling.
	/// Manages a viewport into a larger content area.
	/// </summary>
	public class ScrollableLayoutNode : LayoutNode
	{
		/// <summary>
		/// Gets the total content size (may be larger than viewport).
		/// </summary>
		public LayoutSize ContentSize { get; private set; }

		/// <summary>
		/// Gets the viewport size (visible area).
		/// </summary>
		public LayoutSize ViewportSize { get; private set; }

		/// <summary>
		/// Gets or sets the scroll offset (top-left of visible area).
		/// </summary>
		public int ScrollX { get; set; }

		/// <summary>
		/// Gets or sets the scroll offset (top-left of visible area).
		/// </summary>
		public int ScrollY { get; set; }

		/// <summary>
		/// Gets the maximum horizontal scroll offset.
		/// </summary>
		public int MaxScrollX => Math.Max(0, ContentSize.Width - ViewportSize.Width);

		/// <summary>
		/// Gets the maximum vertical scroll offset.
		/// </summary>
		public int MaxScrollY => Math.Max(0, ContentSize.Height - ViewportSize.Height);

		/// <summary>
		/// Gets whether horizontal scrolling is needed.
		/// </summary>
		public bool CanScrollHorizontally => ContentSize.Width > ViewportSize.Width;

		/// <summary>
		/// Gets whether vertical scrolling is needed.
		/// </summary>
		public bool CanScrollVertically => ContentSize.Height > ViewportSize.Height;

		/// <summary>
		/// Creates a new scrollable layout node.
		/// </summary>
		public ScrollableLayoutNode(IWindowControl? control = null, ILayoutContainer? layout = null)
			: base(control, layout)
		{
		}

		/// <summary>
		/// Measures the scrollable content.
		/// The content can exceed the available space (will scroll).
		/// </summary>
		public new LayoutSize Measure(LayoutConstraints constraints)
		{
			// Measure children with unbounded height for scrolling
			var unboundedConstraints = constraints.WithMaxHeight(int.MaxValue);

			if (Layout != null && Children.Count > 0)
			{
				ContentSize = Layout.MeasureChildren(this, unboundedConstraints);
			}
			else
			{
				ContentSize = base.Measure(unboundedConstraints);
			}

			// Our desired size is the minimum of content and constraints
			var desiredSize = new LayoutSize(
				Math.Min(ContentSize.Width, constraints.MaxWidth),
				Math.Min(ContentSize.Height, constraints.MaxHeight)
			);

			// Store viewport size
			ViewportSize = desiredSize;

			return desiredSize;
		}

		/// <summary>
		/// Scrolls to ensure the specified Y position is visible.
		/// </summary>
		public void EnsureVisible(int y)
		{
			if (y < ScrollY)
			{
				ScrollY = y;
			}
			else if (y >= ScrollY + ViewportSize.Height)
			{
				ScrollY = y - ViewportSize.Height + 1;
			}
			ClampScroll();
		}

		/// <summary>
		/// Scrolls to ensure the specified rectangle is visible.
		/// </summary>
		public void EnsureVisible(LayoutRect rect)
		{
			// Vertical
			if (rect.Y < ScrollY)
			{
				ScrollY = rect.Y;
			}
			else if (rect.Bottom > ScrollY + ViewportSize.Height)
			{
				ScrollY = rect.Bottom - ViewportSize.Height;
			}

			// Horizontal
			if (rect.X < ScrollX)
			{
				ScrollX = rect.X;
			}
			else if (rect.Right > ScrollX + ViewportSize.Width)
			{
				ScrollX = rect.Right - ViewportSize.Width;
			}

			ClampScroll();
		}

		/// <summary>
		/// Scrolls by the specified amounts.
		/// </summary>
		public void ScrollBy(int deltaX, int deltaY)
		{
			ScrollX += deltaX;
			ScrollY += deltaY;
			ClampScroll();
		}

		/// <summary>
		/// Scrolls to the specified position.
		/// </summary>
		public void ScrollTo(int x, int y)
		{
			ScrollX = x;
			ScrollY = y;
			ClampScroll();
		}

		/// <summary>
		/// Scrolls to the top.
		/// </summary>
		public void ScrollToTop()
		{
			ScrollY = 0;
		}

		/// <summary>
		/// Scrolls to the bottom.
		/// </summary>
		public void ScrollToBottom()
		{
			ScrollY = MaxScrollY;
		}

		/// <summary>
		/// Scrolls up by one page.
		/// </summary>
		public void PageUp()
		{
			ScrollY = Math.Max(0, ScrollY - ViewportSize.Height);
		}

		/// <summary>
		/// Scrolls down by one page.
		/// </summary>
		public void PageDown()
		{
			ScrollY = Math.Min(MaxScrollY, ScrollY + ViewportSize.Height);
		}

		private void ClampScroll()
		{
			ScrollX = Math.Clamp(ScrollX, 0, MaxScrollX);
			ScrollY = Math.Clamp(ScrollY, 0, MaxScrollY);
		}

		/// <summary>
		/// Gets the visible range of children indices.
		/// Useful for virtual scrolling (only process visible items).
		/// </summary>
		public (int StartIndex, int Count) GetVisibleChildRange()
		{
			if (Children.Count == 0)
				return (0, 0);

			int startIndex = 0;
			int count = 0;
			int currentY = 0;

			for (int i = 0; i < Children.Count; i++)
			{
				var child = Children[i];
				int childBottom = currentY + child.DesiredSize.Height;

				// Check if child is before viewport
				if (childBottom <= ScrollY)
				{
					startIndex = i + 1;
					currentY = childBottom;
					continue;
				}

				// Check if child is after viewport
				if (currentY >= ScrollY + ViewportSize.Height)
				{
					break;
				}

				// Child is visible
				count++;
				currentY = childBottom;
			}

			return (startIndex, count);
		}

		/// <summary>
		/// Paints the scrollable content with scroll offset applied.
		/// </summary>
		public new void Paint(CharacterBuffer buffer, LayoutRect clipRect)
		{
			if (!IsVisible)
				return;

			// Calculate visible area
			var visibleBounds = AbsoluteBounds.Intersect(clipRect);
			if (visibleBounds.IsEmpty)
				return;

			// Paint children with scroll offset applied
			foreach (var child in Children)
			{
				if (!child.IsVisible)
					continue;

				// Translate child bounds by scroll offset
				var translatedBounds = new LayoutRect(
					child.AbsoluteBounds.X - ScrollX,
					child.AbsoluteBounds.Y - ScrollY,
					child.AbsoluteBounds.Width,
					child.AbsoluteBounds.Height);

				// Check if visible
				if (translatedBounds.IntersectsWith(visibleBounds))
				{
					// Paint child with translated bounds
					PaintChildScrolled(child, buffer, visibleBounds, ScrollX, ScrollY);
				}
			}

			NeedsPaint = false;
		}

		private void PaintChildScrolled(LayoutNode child, CharacterBuffer buffer, LayoutRect clipRect, int scrollX, int scrollY)
		{
			// This is a simplified implementation
			// In a full implementation, we'd need to adjust the child's Paint method
			// to account for scroll offset
			child.Paint(buffer, clipRect);
		}
	}
}
