// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using System.Collections.ObjectModel;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Represents a tree node in the TreeControl.
	/// </summary>
	public class TreeNode
	{
		private List<TreeNode> _children = new();

		/// <summary>
		/// Creates a new tree node with specified text.
		/// </summary>
		/// <param name="text">The text label for the node.</param>
		public TreeNode(string text)
		{
			Text = text;
		}

		/// <summary>
		/// Gets the children of this node.
		/// </summary>
		public ReadOnlyCollection<TreeNode> Children => _children.AsReadOnly();

		/// <summary>
		/// Gets or sets whether this node is expanded.
		/// </summary>
		public bool IsExpanded { get; set; } = true;

		/// <summary>
		/// Gets or sets a custom object associated with this node.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text label of this node.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Gets or sets the optional color for this node's text.
		/// </summary>
		public Color? TextColor { get; set; }

		/// <summary>
		/// Internal cache for node depth calculation. Invalidated when tree structure changes.
		/// </summary>
		internal int? CachedDepth { get; set; }

		/// <summary>
		/// Internal cache for parent node lookup. Invalidated when tree structure changes.
		/// </summary>
		internal TreeNode? CachedParent { get; set; }

		/// <summary>
		/// Adds a child node to this node.
		/// </summary>
		/// <param name="node">The child node to add.</param>
		/// <returns>The added child node.</returns>
		public TreeNode AddChild(TreeNode node)
		{
			_children.Add(node);
			return node;
		}

		/// <summary>
		/// Adds a child node with specified text.
		/// </summary>
		/// <param name="text">The text label for the child node.</param>
		/// <returns>The newly created and added child node.</returns>
		public TreeNode AddChild(string text)
		{
			var node = new TreeNode(text);
			_children.Add(node);
			return node;
		}

		/// <summary>
		/// Clears all child nodes.
		/// </summary>
		public void ClearChildren()
		{
			_children.Clear();
		}

		/// <summary>
		/// Removes a child node.
		/// </summary>
		/// <param name="node">The node to remove.</param>
		/// <returns>True if the node was found and removed, false otherwise.</returns>
		public bool RemoveChild(TreeNode node)
		{
			return _children.Remove(node);
		}
	}
}
