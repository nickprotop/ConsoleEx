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
				return (new TabLayout(), tabControl.TabPages.Select(tp => tp.Content));
			}
			else if (control is ScrollablePanelControl)
			{
				// Self-painting container - do not recurse into children
				return (null, null);
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
					node.AddChild(childNode);
				}
			}

			return node;
		}
	}
}
