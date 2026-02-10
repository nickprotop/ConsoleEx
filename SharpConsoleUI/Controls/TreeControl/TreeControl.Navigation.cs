// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;

namespace SharpConsoleUI.Controls
{
	public partial class TreeControl
	{
		/// <summary>
		/// Helper method to recursively collapse all nodes
		/// </summary>
		private void CollapseNodes(List<TreeNode> nodes)
		{
			foreach (var node in nodes)
			{
				node.IsExpanded = false;
				if (node.Children.Count > 0)
				{
					CollapseNodes(node.Children.ToList());
				}
			}
		}

		// Add method to ensure selected item is visible
		/// <summary>
		/// Ensures the selected node is visible in the current view by adjusting scroll offset
		/// </summary>
		private void EnsureSelectedItemVisible()
		{
			int selectedIndex = CurrentSelectedIndex;
			if (selectedIndex < 0 || _flattenedNodes.Count == 0)
				return;

			// Calculate effective max visible items considering available space
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
			int scrollOffset = CurrentScrollOffset;
			int newScrollOffset = scrollOffset;

			// Adjust scroll offset if selected item is outside visible range
			if (selectedIndex < scrollOffset)
			{
				newScrollOffset = selectedIndex;
			}
			else if (selectedIndex >= scrollOffset + effectiveMaxVisibleItems)
			{
				newScrollOffset = selectedIndex - effectiveMaxVisibleItems + 1;
			}

			// Ensure scroll offset is valid
			newScrollOffset = Math.Max(0, Math.Min(newScrollOffset, _flattenedNodes.Count - effectiveMaxVisibleItems));

			if (newScrollOffset != scrollOffset)
			{
				_scrollOffset = newScrollOffset;
			}
		}

		/// <summary>
		/// Helper method to recursively expand all nodes
		/// </summary>
		private void ExpandNodes(List<TreeNode> nodes)
		{
			foreach (var node in nodes)
			{
				node.IsExpanded = true;
				if (node.Children.Count > 0)
				{
					ExpandNodes(node.Children.ToList());
				}
			}
		}

		/// <summary>
		/// Helper method for recursive tag search
		/// </summary>
		private TreeNode? FindNodeByTagRecursive(object tag, List<TreeNode> nodes)
		{
			foreach (var node in nodes)
			{
				if (tag.Equals(node.Tag))
					return node;

				if (node.Children.Count > 0)
				{
					var found = FindNodeByTagRecursive(tag, node.Children.ToList());
					if (found != null)
						return found;
				}
			}
			return null;
		}

		/// <summary>
		/// Helper method to find a node's path in the tree
		/// </summary>
		/// <param name="targetNode">The node to find</param>
		/// <param name="currentLevel">The current level of nodes to search</param>
		/// <param name="path">Stack tracking the path to the node</param>
		/// <returns>True if node was found, false otherwise</returns>
		private bool FindNodePath(TreeNode targetNode, List<TreeNode> currentLevel, Stack<TreeNode> path)
		{
			foreach (var node in currentLevel)
			{
				if (node == targetNode)
					return true;

				path.Push(node);
				if (FindNodePath(targetNode, node.Children.ToList(), path))
					return true;

				path.Pop();
			}

			return false;
		}

		/// <summary>
		/// Find the parent node of a given node in the tree with caching.
		/// Uses cached parent when available to avoid tree traversal.
		/// </summary>
		/// <param name="node">The node to find parent for</param>
		/// <returns>Parent node or null if node is a root node</returns>
		private TreeNode? FindParentNode(TreeNode node)
		{
			if (node == null || _rootNodes.Contains(node))
				return null;

			// Use cache if available
			if (node.CachedParent != null)
				return node.CachedParent;

			// Fall back to search and cache result
			var parent = FindParentNodeRecursive(node, _rootNodes);
			node.CachedParent = parent;
			return parent;
		}

		/// <summary>
		/// Recursively searches for the parent of a node
		/// </summary>
		/// <param name="targetNode">The node to find parent for</param>
		/// <param name="currentNodes">Current level of nodes to search</param>
		/// <returns>Parent node or null if not found</returns>
		private TreeNode? FindParentNodeRecursive(TreeNode targetNode, List<TreeNode> currentNodes)
		{
			foreach (var node in currentNodes)
			{
				// Check if this node is the parent
				if (node.Children.Contains(targetNode))
					return node;

				// Recursively check children
				if (node.Children.Count > 0)
				{
					var found = FindParentNodeRecursive(targetNode, node.Children.ToList());
					if (found != null)
						return found;
				}
			}

			return null;
		}

		/// <summary>
		/// Helper method to flatten the tree structure into a list for keyboard navigation
		/// </summary>
		/// <param name="nodes">Collection of nodes to flatten</param>
		private void FlattenNodes(List<TreeNode> nodes)
		{
			if (nodes == null || nodes.Count == 0)
				return;

			foreach (var node in nodes)
			{
				_flattenedNodes.Add(node);

				if (node.IsExpanded && node.Children.Count > 0)
				{
					FlattenNodes(node.Children.ToList());
				}
			}
		}

		/// <summary>
		/// Calculates the depth of a node in the tree with memoization.
		/// Uses cached depth when available to avoid repeated traversals.
		/// </summary>
		/// <param name="node">The node to calculate depth for</param>
		/// <returns>The depth of the node (0 for root nodes)</returns>
		private int GetNodeDepth(TreeNode node)
		{
			if (node == null)
				return -1;

			// Return cached depth if available
			if (node.CachedDepth.HasValue)
				return node.CachedDepth.Value;

			if (_rootNodes.Contains(node))
			{
				node.CachedDepth = 0;
				return 0;
			}

			TreeNode? parent = FindParentNode(node);
			if (parent == null)
			{
				node.CachedDepth = 0;
				return 0;
			}

			// Recursive calculation with memoization
			int depth = GetNodeDepth(parent) + 1;
			node.CachedDepth = depth;
			node.CachedParent = parent;
			return depth;
		}

		/// <summary>
		/// Determines if a node is the last child within its parent
		/// </summary>
		/// <param name="node">The node to check</param>
		/// <returns>True if the node is the last child in its parent's children</returns>
		private bool IsLastChildInParent(TreeNode node)
		{
			if (node == null)
				return false;

			TreeNode? parent = FindParentNode(node);

			// If it's a root node, check if it's the last root node
			if (parent == null)
			{
				int index = _rootNodes.IndexOf(node);
				return index == _rootNodes.Count - 1;
			}

			// If it has a parent, check if it's the last child
			var children = parent.Children;
			int lastIndex = children.Count - 1;

			for (int i = 0; i < children.Count; i++)
			{
				if (children[i] == node)
					return i == lastIndex;
			}

			return false;
		}

		/// <summary>
		/// Invalidates the depth cache for all nodes in the tree.
		/// Should be called when the tree structure changes.
		/// </summary>
		private void InvalidateDepthCache()
		{
			foreach (var node in _flattenedNodes)
			{
				node.CachedDepth = null;
				node.CachedParent = null;
			}
		}

		/// <summary>
		/// Updates the flattened nodes list for easier navigation
		/// </summary>
		private void UpdateFlattenedNodes()
		{
			_flattenedNodes.Clear();
			_textMeasurementCache.InvalidateCache(); // Clear cache when tree structure changes
			InvalidateDepthCache(); // Clear depth cache when tree structure changes
			FlattenNodes(_rootNodes);

			// Ensure selected index is valid
			int selectedIndex = CurrentSelectedIndex;
			if (_flattenedNodes.Count > 0)
			{
				int validIndex = Math.Max(0, Math.Min(selectedIndex, _flattenedNodes.Count - 1));
				if (validIndex != selectedIndex)
				{
					int oldIndex = _selectedIndex;
					_selectedIndex = validIndex;
					if (oldIndex != _selectedIndex)
					{
						var selectedNode = _selectedIndex >= 0 && _selectedIndex < _flattenedNodes.Count ? _flattenedNodes[_selectedIndex] : null;
						SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(selectedNode));
					}
				}
			}
			else if (selectedIndex != -1)
			{
				int oldIndex = _selectedIndex;
					_selectedIndex = -1;
				if (oldIndex != _selectedIndex)
				{
					var selectedNode = _selectedIndex >= 0 && _selectedIndex < _flattenedNodes.Count ? _flattenedNodes[_selectedIndex] : null;
					SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(selectedNode));
				}
			}
		}
	}
}
