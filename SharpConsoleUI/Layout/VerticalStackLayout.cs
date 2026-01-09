// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Layout algorithm that stacks children vertically.
	/// Similar to ColumnContainer behavior.
	/// </summary>
	public class VerticalStackLayout : ILayoutContainer
	{
		/// <summary>
		/// Gets or sets the spacing between children.
		/// </summary>
		public int Spacing { get; set; } = 0;

		/// <summary>
		/// Measures all children and returns the total desired size.
		/// Width: maximum of all children widths.
		/// Height: sum of all children heights plus spacing.
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{

			if (node.Children.Count == 0)
				return LayoutSize.Zero;

			int totalHeight = 0;
			int maxWidth = 0;
			int fillCount = 0;

			// First pass: measure non-fill children
			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				if (child.VerticalAlignment == VerticalAlignment.Fill)
				{
					fillCount++;
					continue;
				}

				var childConstraints = constraints.WithMaxHeight(constraints.MaxHeight - totalHeight);
				var childSize = child.Measure(childConstraints);

				totalHeight += childSize.Height;
				maxWidth = Math.Max(maxWidth, childSize.Width);
			}

			// Second pass: measure fill children
			// When measuring with unbounded height (for scrollable areas), fill children
			// should report their actual content size, not try to fill infinite space.
			if (fillCount > 0)
			{
				// If measuring with unbounded height, just measure fill children with unbounded constraints
				// They will return their actual content height
				bool isUnbounded = constraints.MaxHeight >= int.MaxValue / 2;

				foreach (var child in node.Children)
				{
					if (!child.IsVisible || child.VerticalAlignment != VerticalAlignment.Fill)
						continue;

					LayoutConstraints childConstraints;
					if (isUnbounded)
					{
						// For unbounded measurement, let fill children report their natural size
						childConstraints = constraints.WithMaxHeight(constraints.MaxHeight - totalHeight);
					}
					else
					{
						// For bounded measurement, distribute remaining space
						int remainingHeight = Math.Max(0, constraints.MaxHeight - totalHeight);
						int fillHeight = remainingHeight / fillCount;
						// Use Loose constraints (MinHeight=0) to allow flexible sizing
						childConstraints = LayoutConstraints.Loose(constraints.MaxWidth, fillHeight);
					}

					var childSize = child.Measure(childConstraints);
					totalHeight += childSize.Height;
					maxWidth = Math.Max(maxWidth, childSize.Width);
				}
			}

			return new LayoutSize(
				Math.Min(maxWidth, constraints.MaxWidth),
				Math.Min(totalHeight, constraints.MaxHeight)
			);
		}

		/// <summary>
		/// Arranges children vertically within the given bounds.
		/// Fill children share remaining space proportionally.
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
		{

			if (node.Children.Count == 0)
				return;

			// Calculate fixed heights and count fill children
			int fixedHeight = 0;
			int fillCount = 0;
			double totalFlexFactor = 0;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				if (child.VerticalAlignment == VerticalAlignment.Fill)
				{
					fillCount++;
					totalFlexFactor += child.FlexFactor;
				}
				else
				{
					fixedHeight += child.DesiredSize.Height;
				}
			}

			// Add spacing
			int visibleCount = node.Children.Count(c => c.IsVisible);
			if (visibleCount > 1)
			{
				fixedHeight += Spacing * (visibleCount - 1);
			}

			// Calculate height for fill children
			int remainingHeight = Math.Max(0, finalRect.Height - fixedHeight);

			// Arrange children
			int currentY = 0;
			bool isFirst = true;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				// Add spacing (except before first)
				if (!isFirst && Spacing > 0)
				{
					currentY += Spacing;
				}
				isFirst = false;

				int childHeight;
				if (child.VerticalAlignment == VerticalAlignment.Fill)
				{
					// Distribute remaining space by flex factor
					childHeight = (int)(remainingHeight * (child.FlexFactor / totalFlexFactor));
				}
				else
				{
					childHeight = child.DesiredSize.Height;
				}

				// Calculate width based on horizontal alignment
				int childWidth;
				int childX;

				switch (child.Control?.HorizontalAlignment ?? HorizontalAlignment.Stretch)
				{
					case HorizontalAlignment.Left:
						childWidth = child.DesiredSize.Width;
						childX = 0;
						break;
					case HorizontalAlignment.Center:
						childWidth = child.DesiredSize.Width;
						childX = (finalRect.Width - childWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						childWidth = child.DesiredSize.Width;
						childX = finalRect.Width - childWidth;
						break;
					case HorizontalAlignment.Stretch:
					default:
						childWidth = finalRect.Width;
						childX = 0;
						break;
				}

				child.Arrange(new LayoutRect(childX, currentY, childWidth, childHeight));
				currentY += childHeight;
			}
		}
	}
}
