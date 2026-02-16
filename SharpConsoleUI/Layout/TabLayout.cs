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
	/// Layout algorithm for TabControl that positions tab content below a header row.
	/// Similar to VerticalStackLayout but reserves space for tab headers.
	/// </summary>
	public class TabLayout : ILayoutContainer
	{
		private const int TAB_HEADER_HEIGHT = 1;

		/// <summary>
		/// Measures all children within the container and returns the desired size.
		/// Reserves space for tab headers at the top.
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{
			// Reserve space for tab headers
			int availableHeight = Math.Max(0, constraints.MaxHeight - TAB_HEADER_HEIGHT);

			// Create child constraints (reduced height for header)
			var childConstraints = new LayoutConstraints(
				constraints.MinWidth,
				constraints.MaxWidth,
				Math.Max(0, constraints.MinHeight - TAB_HEADER_HEIGHT),
				availableHeight
			);

			int maxWidth = 0;
			int maxHeight = 0;

			// Measure all children
			// Note: IsVisible filtering automatic via LayoutNode.Measure
			// Invisible children return LayoutSize.Zero (LayoutNode.cs line 218-223)
			foreach (var child in node.Children)
			{
				var childSize = child.Measure(childConstraints);
				maxWidth = Math.Max(maxWidth, childSize.Width);
				maxHeight = Math.Max(maxHeight, childSize.Height);
			}

			// Get the TabControl's content width (which includes header width)
			int tabControlWidth = maxWidth;
			if (node.Control is SharpConsoleUI.Controls.TabControl tabControl)
			{
				tabControlWidth = Math.Max(maxWidth, tabControl.ContentWidth ?? maxWidth);
			}

			// Return total size (header + content)
			return new LayoutSize(
				Math.Max(tabControlWidth, constraints.MinWidth),
				TAB_HEADER_HEIGHT + maxHeight
			);
		}

		/// <summary>
		/// Arranges all children within the container's final bounds.
		/// Active tab is positioned below header, inactive tabs are collapsed.
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect bounds)
		{
			int contentHeight = Math.Max(0, bounds.Height - TAB_HEADER_HEIGHT);

			foreach (var child in node.Children)
			{
				if (child.IsVisible)
				{
					// Active tab: arrange below header
					var childBounds = new LayoutRect(
						bounds.X,
						bounds.Y + TAB_HEADER_HEIGHT,
						bounds.Width,
						contentHeight
					);
					child.Arrange(childBounds);
				}
				else
				{
					// Inactive tabs: collapsed (zero size)
					child.Arrange(new LayoutRect(0, 0, 0, 0));
				}
			}
		}
	}
}
