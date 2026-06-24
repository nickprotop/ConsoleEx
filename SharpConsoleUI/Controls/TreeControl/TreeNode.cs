// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Represents a tree node in the TreeControl. Implements
	/// <see cref="INotifyPropertyChanged"/> so node properties (expansion, text, color, tag)
	/// can participate in data binding. Interactive expand/collapse in <c>TreeControl</c>
	/// assigns through <see cref="IsExpanded"/>, so those changes raise notifications too.
	/// </summary>
	public class TreeNode : INotifyPropertyChanged
	{
		private List<TreeNode> _children = new();
		private bool _isExpanded = true;
		private object? _tag;
		private string _text;
		private Color? _textColor;

		/// <inheritdoc/>
		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			// Route the change to the owning control so it can self-invalidate without the
			// consumer having to call tree.Invalidate() manually. Uses the Owner back-ref
			// (set when the node is attached) rather than per-node event subscriptions,
			// mirroring the existing CachedParent pattern.
			Owner?.OnNodePropertyChanged(propertyName);
		}

		/// <summary>
		/// Creates a new tree node with specified text.
		/// </summary>
		/// <param name="text">The text label for the node.</param>
		public TreeNode(string text)
		{
			_text = text;
		}

		/// <summary>
		/// Gets the children of this node.
		/// </summary>
		public ReadOnlyCollection<TreeNode> Children => _children.AsReadOnly();

		/// <summary>
		/// Gets or sets whether this node is expanded.
		/// </summary>
		public bool IsExpanded
		{
			get => _isExpanded;
			set
			{
				if (_isExpanded == value) return;
				_isExpanded = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets or sets a custom object associated with this node.
		/// </summary>
		public object? Tag
		{
			get => _tag;
			set
			{
				if (Equals(_tag, value)) return;
				_tag = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets or sets the text label of this node.
		/// </summary>
		public string Text
		{
			get => _text;
			set
			{
				if (_text == value) return;
				_text = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets or sets the optional color for this node's text.
		/// </summary>
		public Color? TextColor
		{
			get => _textColor;
			set
			{
				if (Nullable.Equals(_textColor, value)) return;
				_textColor = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Internal cache for node depth calculation. Invalidated when tree structure changes.
		/// </summary>
		internal int? CachedDepth { get; set; }

		/// <summary>
		/// Internal cache for parent node lookup. Invalidated when tree structure changes.
		/// </summary>
		internal TreeNode? CachedParent { get; set; }

		/// <summary>
		/// Back-reference to the <see cref="TreeControl"/> that owns this node's tree, or null
		/// if the node is detached. Propagated down a subtree when it is attached (via
		/// <see cref="TreeControl.AddRootNode(TreeNode)"/> or <see cref="AddChild(TreeNode)"/>)
		/// and cleared when detached (<see cref="RemoveChild(TreeNode)"/>/<see cref="ClearChildren()"/>).
		/// Lets the node notify its owner of mutations so the control can self-invalidate.
		/// </summary>
		internal TreeControl? Owner { get; set; }

		/// <summary>
		/// Sets <see cref="Owner"/> on this node and recursively on its entire subtree.
		/// Called when a subtree is attached to (or detached from, by passing null) a
		/// <see cref="TreeControl"/>.
		/// </summary>
		/// <param name="owner">The owning control, or null to detach the subtree.</param>
		internal void SetOwnerRecursive(TreeControl? owner)
		{
			Owner = owner;
			foreach (var child in _children)
			{
				child.SetOwnerRecursive(owner);
			}
		}

		/// <summary>
		/// Adds a child node to this node.
		/// </summary>
		/// <param name="node">The child node to add.</param>
		/// <returns>The added child node.</returns>
		public TreeNode AddChild(TreeNode node)
		{
			_children.Add(node);
			// Propagate this node's owner onto the newly attached subtree, then notify the
			// owner so it can rebuild its flattened cache and invalidate.
			node.SetOwnerRecursive(Owner);
			Owner?.OnNodeStructureChanged();
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
			// New leaf inherits this node's owner; notify the owner to self-invalidate.
			node.SetOwnerRecursive(Owner);
			Owner?.OnNodeStructureChanged();
			return node;
		}

		/// <summary>
		/// Clears all child nodes.
		/// </summary>
		public void ClearChildren()
		{
			if (_children.Count == 0)
				return;

			// Detach the removed subtrees (clear their owner), then notify the owner.
			var owner = Owner;
			foreach (var child in _children)
			{
				child.SetOwnerRecursive(null);
			}
			_children.Clear();
			owner?.OnNodeStructureChanged();
		}

		/// <summary>
		/// Removes a child node.
		/// </summary>
		/// <param name="node">The node to remove.</param>
		/// <returns>True if the node was found and removed, false otherwise.</returns>
		public bool RemoveChild(TreeNode node)
		{
			bool removed = _children.Remove(node);
			if (removed)
			{
				// Detach the removed subtree (clear its owner), then notify the owner.
				node.SetOwnerRecursive(null);
				Owner?.OnNodeStructureChanged();
			}
			return removed;
		}
	}
}
