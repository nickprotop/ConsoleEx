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
		/// Reserves space for tab headers and the control's margins.
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{
			// Get the TabControl's margin
			var margin = (node.Control as SharpConsoleUI.Controls.TabControl)?.Margin
				?? new Controls.Margin(0, 0, 0, 0);

			int marginH = margin.Top + margin.Bottom;
			int marginW = margin.Left + margin.Right;

			// Reserve space for tab headers and margins
			int verticalOverhead = TAB_HEADER_HEIGHT + marginH;
			int availableHeight = Math.Max(0, constraints.MaxHeight - verticalOverhead);
			int availableWidth = Math.Max(0, constraints.MaxWidth - marginW);

			// Create child constraints (reduced for header + margins)
			var childConstraints = new LayoutConstraints(
				Math.Max(0, constraints.MinWidth - marginW),
				availableWidth,
				Math.Max(0, constraints.MinHeight - verticalOverhead),
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

			// Return total size (margins + header + content)
			return new LayoutSize(
				Math.Max(tabControlWidth + marginW, constraints.MinWidth),
				verticalOverhead + maxHeight
			);
		}

		/// <summary>
		/// Arranges all children within the container's final bounds.
		/// Active tab is positioned below header, inactive tabs are collapsed.
		/// Accounts for the TabControl's margins.
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect bounds)
		{
			var margin = (node.Control as SharpConsoleUI.Controls.TabControl)?.Margin
				?? new Controls.Margin(0, 0, 0, 0);

			int contentTop = margin.Top + TAB_HEADER_HEIGHT;
			int contentHeight = Math.Max(0, bounds.Height - contentTop - margin.Bottom);
			int contentWidth = Math.Max(0, bounds.Width - margin.Left - margin.Right);

			foreach (var child in node.Children)
			{
				if (child.IsVisible)
				{
					// Active tab: arrange below header, inset by margins
					var childBounds = new LayoutRect(
						bounds.X + margin.Left,
						bounds.Y + contentTop,
						contentWidth,
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
