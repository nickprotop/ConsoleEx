// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using System.IO;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Layout algorithm that arranges children horizontally.
	/// Similar to HorizontalGridControl behavior.
	/// </summary>
	public class HorizontalLayout : ILayoutContainer
	{
		/// <summary>
		/// Gets or sets the spacing between children.
		/// </summary>
		public int Spacing { get; set; } = 0;

		/// <summary>
		/// Measures all children and returns the total desired size.
		/// Width: sum of all children widths plus spacing.
		/// Height: maximum of all children heights.
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{

			if (node.Children.Count == 0)
				return LayoutSize.Zero;

			int totalWidth = 0;
			int maxHeight = 0;
			int flexCount = 0;
			double totalFlexFactor = 0;

			int minRemainingWidth = 1; // guarantee at least 1 column for any child

			// First pass: measure fixed-width children
			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				if (child.ExplicitWidth == null && child.FlexFactor > 0)
				{
					flexCount++;
					totalFlexFactor += child.FlexFactor;
					continue;
				}

				var childConstraints = constraints.SubtractWidth(totalWidth);
				if (childConstraints.MaxWidth <= 0)
				{
					childConstraints = childConstraints.WithMaxWidth(minRemainingWidth);
				}

				var childSize = child.Measure(childConstraints);

				totalWidth += childSize.Width;
				maxHeight = Math.Max(maxHeight, childSize.Height);
			}

			// Second pass: measure flex children with remaining space
			if (flexCount > 0)
			{
				int remainingWidth = Math.Max(minRemainingWidth, constraints.MaxWidth - totalWidth);

				foreach (var child in node.Children)
				{
					if (!child.IsVisible)
						continue;

					if (child.ExplicitWidth != null || child.FlexFactor <= 0)
						continue;

					int flexWidth = (int)(remainingWidth * (child.FlexFactor / totalFlexFactor));
					flexWidth = Math.Max(minRemainingWidth, flexWidth);
					// Use Loose constraints (MinHeight=0) instead of Fixed (MinHeight=MaxHeight)
					// to allow children to measure with unbounded height
					var childConstraints = LayoutConstraints.Loose(flexWidth, constraints.MaxHeight);

					var childSize = child.Measure(childConstraints);

					totalWidth += childSize.Width;
					maxHeight = Math.Max(maxHeight, childSize.Height);
				}
			}

			return new LayoutSize(
				Math.Min(totalWidth, constraints.MaxWidth),
				Math.Min(maxHeight, constraints.MaxHeight)
			);
		}

		/// <summary>
		/// Arranges children horizontally within the given bounds.
		/// Flex children share remaining width proportionally.
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
		{

			if (node.Children.Count == 0)
				return;

			// Calculate fixed widths and count flex children
			int fixedWidth = 0;
			int flexCount = 0;
			double totalFlexFactor = 0;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				if (child.ExplicitWidth == null && child.FlexFactor > 0)
				{
					flexCount++;
					totalFlexFactor += child.FlexFactor;
				}
				else
				{
					fixedWidth += child.DesiredSize.Width;
				}
			}

			// Add spacing
			int visibleCount = node.Children.Count(c => c.IsVisible);
			if (visibleCount > 1)
			{
				fixedWidth += Spacing * (visibleCount - 1);
			}

			// Calculate width for flex children
			int remainingWidth = Math.Max(0, finalRect.Width - fixedWidth);

			// Arrange children
			int currentX = 0;
			bool isFirst = true;


			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
					continue;

				// Add spacing (except before first)
				if (!isFirst && Spacing > 0)
				{
					currentX += Spacing;
				}
				isFirst = false;

				int childWidth;
				if (child.ExplicitWidth == null && child.FlexFactor > 0)
				{
					// Distribute remaining space by flex factor
					childWidth = (int)(remainingWidth * (child.FlexFactor / totalFlexFactor));
				}
				else
				{
					childWidth = child.DesiredSize.Width;
				}

				// Calculate height based on vertical alignment
				int childHeight;
				int childY;

				switch (child.VerticalAlignment)
				{
					case VerticalAlignment.Top:
						childHeight = child.DesiredSize.Height;
						childY = 0;
						break;
					case VerticalAlignment.Center:
						childHeight = child.DesiredSize.Height;
						childY = (finalRect.Height - childHeight) / 2;
						break;
					case VerticalAlignment.Bottom:
						childHeight = child.DesiredSize.Height;
						childY = finalRect.Height - childHeight;
						break;
					case VerticalAlignment.Fill:
					default:
						childHeight = finalRect.Height;
						childY = 0;
						break;
				}

				child.Arrange(new LayoutRect(currentX, childY, childWidth, childHeight));
				currentX += childWidth;
			}
		}
	}
}
