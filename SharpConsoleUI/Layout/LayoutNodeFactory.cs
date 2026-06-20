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
	/// Shared utility for building layout subtrees from controls.
	/// Extracts the control-type to layout-algorithm mapping so both
	/// WindowRenderer and ScrollablePanelControl can build proper layout trees.
	/// </summary>
	public static class LayoutNodeFactory
	{
		/// <summary>
		/// Resolves the layout algorithm and children for a control.
		/// Returns (null, null) for leaf controls or self-painting containers.
		/// </summary>
		public static (ILayoutContainer? Layout, IEnumerable<IWindowControl>? Children)
			ResolveLayout(IWindowControl control)
		{
			if (control is ColumnContainer columnContainer)
			{
				return (new VerticalStackLayout(), columnContainer.Contents);
			}
			else if (control is HorizontalGridControl horizontalGrid)
			{
				var orderedChildren = new List<IWindowControl>();
				for (int i = 0; i < horizontalGrid.Columns.Count; i++)
				{
					orderedChildren.Add(horizontalGrid.Columns[i]);
					var splitter = horizontalGrid.Splitters.FirstOrDefault(s => horizontalGrid.GetSplitterLeftColumnIndex(s) == i);
					if (splitter != null)
					{
						orderedChildren.Add(splitter);
					}
				}
				return (new HorizontalLayout(), orderedChildren);
			}
			else if (control is TabControl tabControl)
			{
				// Build only the active tab's content into the layout tree (mirrors
				// TabControl.GetChildren()). The active page is selected by the control's
				// own ActiveTabIndex state, NOT by clobbering each page's caller-owned
				// Visible flag. See issue #53.
				return (new TabLayout(), tabControl.GetChildren());
			}
			else if (control is CollapsiblePanel collapsible)
			{
				return (new CollapsibleLayout(), collapsible.ContentsSnapshot());
			}
			else if (control is NavigationView navView)
			{
				// NavigationView wraps a single HorizontalGrid
				return (new VerticalStackLayout(), new IWindowControl[] { navView.InternalGrid });
			}
			else if (control is ScrollablePanelControl spc)
			{
				// Tree-participating container: the engine builds the panel's children into the
				// tree and paints them via ScrollLayout (scroll offset flows into AbsoluteBounds).
				// The panel paints only its own chrome (border + scrollbars) in PaintDOM.
				return (new ScrollLayout(), spc.Children);
			}
			else if (control is GridControl gridControl)
			{
				// Tree-participating container: the engine builds the grid's content-bearing cells into the
				// tree (in OrderedCells order, which is content-only) and GridLayout correlates
				// node.Children[i] to OrderedCells[i] by index, so the children enumerable must be in
				// OrderedCells order. The grid and the tree share the grid's GridLayout instance so the
				// grid's PaintDOM can read the per-cell rectangles the arrange pass recorded.
				return (gridControl.LayoutAlgorithm, gridControl.OrderedCells.Select(c => c.Control).ToList());
			}
			else if (control is PortalContentContainer)
			{
				// Self-painting container - owns its layout
				return (null, null);
			}

			// Leaf control
			return (null, null);
		}

		/// <summary>
		/// Builds a complete LayoutNode subtree for a control, recursively
		/// handling container children with proper layout algorithms.
		/// </summary>
		public static LayoutNode CreateSubtree(IWindowControl control)
		{
			var (layout, children) = ResolveLayout(control);
			var node = new LayoutNode(control, layout);

			if (children != null)
			{
				foreach (var child in children)
				{
					var childNode = CreateSubtree(child);
					childNode.IsVisible = child.Visible;

					// Propagate FlexFactor from ColumnContainer to LayoutNode
					if (child is ColumnContainer column)
					{
						childNode.FlexFactor = column.FlexFactor;
					}

					node.AddChild(childNode);
				}
			}

			return node;
		}
	}
}
