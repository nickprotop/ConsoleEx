// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;
using Color = Spectre.Console.Color;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A hierarchical tree control that displays nodes in a collapsible tree structure with keyboard navigation.
	/// </summary>
	public class TreeControl : IWindowControl, IInteractiveControl, IFocusableControl, IDOMPaintable
	{
		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;
		private readonly List<TreeNode> _rootNodes = new();
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Color? _backgroundColorValue;
		private int? _calculatedMaxVisibleItems;
		private List<TreeNode> _flattenedNodes = new();
		private Color? _foregroundColorValue;
		private TreeGuide _guide = TreeGuide.Line;
		private bool _hasFocus = false;
		private int? _height;
		private string _indent = "  ";
		private bool _isEnabled = true;
		private Margin _margin = new(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Local selection state
		private int _selectedIndex = 0;
		private int _scrollOffset = 0;

		// Performance: Cache for expensive text measurement operations
		private readonly TextMeasurementCache _textMeasurementCache = new(AnsiConsoleHelper.StripAnsiStringLength);

		// Read-only helpers
		private int CurrentSelectedIndex => _selectedIndex;
		private int CurrentScrollOffset => _scrollOffset;

		/// <summary>
		/// Initializes a new instance of the TreeControl class.
		/// </summary>
		public TreeControl()
		{
		}

		// Helper to get cached text length (expensive operation)
		private int GetCachedTextLength(string text)
	{
		return _textMeasurementCache.GetCachedLength(text);
	}

		/// <summary>
		/// Gets the actual rendered width in characters.
		/// </summary>
		public int? ContentWidth
		{
			get
			{
				if (_flattenedNodes.Count == 0) return _margin.Left + _margin.Right;

				int maxLength = 0;
				var guideChars = GetGuideChars();
				foreach (var node in _flattenedNodes)
				{
					int depth = GetNodeDepth(node);
					string prefix = BuildTreePrefix(depth, IsLastChildInParent(node), guideChars);
					string displayText = node.Text ?? string.Empty;
					string expandIndicator = node.Children.Count > 0 ? " [-]" : "";
					int length = GetCachedTextLength(prefix + displayText + expandIndicator);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + _margin.Left + _margin.Right;
			}
		}

		public int ActualX => _actualX;
		public int ActualY => _actualY;
		public int ActualWidth => _actualWidth;
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set
			{
				if (_verticalAlignment == value) return;
				_verticalAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the tree control.
		/// </summary>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets the foreground color of the tree control.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the tree guide style used for drawing the tree structure.
		/// </summary>
		public TreeGuide Guide
		{
			get => _guide;
			set
			{
				_guide = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;
				Container?.Invalidate(true);

				// Fire focus events
				if (value && !hadFocus)
				{
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets the explicit height of the control.
		/// If null, control height is based on content until available height.
		/// </summary>
		public int? Height
		{
			get => _height;
			set => PropertySetterHelper.SetDimensionProperty(ref _height, value, Container);
		}		/// <summary>
		/// Gets or sets the background color for highlighted items
		/// </summary>
		public Color HighlightBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for highlighted items
		/// </summary>
		public Color HighlightForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the indent string for each level
		/// </summary>
		public string Indent
		{
			get => _indent;
			set
			{
				_indent = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the tree control is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum number of items to display at once.
		/// If null, shows as many as will fit in available height.
		/// </summary>
		public int? MaxVisibleItems { get; set; }

		/// <summary>
		/// Event that fires when a tree node is expanded or collapsed.
		/// </summary>
		public event EventHandler<TreeNodeEventArgs>? NodeExpandCollapse;

		/// <summary>
		/// Event that fires when the selected node changes.
		/// </summary>
		public event EventHandler<TreeNodeEventArgs>? SelectedNodeChanged;

		/// <summary>
		/// Gets the collection of root nodes in the tree.
		/// </summary>
		public ReadOnlyCollection<TreeNode> RootNodes => _rootNodes.AsReadOnly();

		/// <summary>
		/// Gets or sets the currently selected node index in the flattened nodes list.
		/// </summary>
		public int SelectedIndex
		{
			get => CurrentSelectedIndex;
			set
			{
				if (_flattenedNodes.Count > 0)
				{
					int newValue = Math.Max(0, Math.Min(value, _flattenedNodes.Count - 1));
					int currentSel = CurrentSelectedIndex;
					if (currentSel != newValue)
					{
						// Write to state service (single source of truth)
						int oldIndex = _selectedIndex;
					_selectedIndex = newValue;
					if (oldIndex != _selectedIndex)
					{
						var selectedNode = _selectedIndex >= 0 && _selectedIndex < _flattenedNodes.Count ? _flattenedNodes[_selectedIndex] : null;
						SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(selectedNode));
					}
						Container?.Invalidate(true);
					}
				}
			}
		}

		/// <summary>
		/// Gets the currently selected node.
		/// </summary>
		public TreeNode? SelectedNode
		{
			get
			{
				int idx = CurrentSelectedIndex;
				return _flattenedNodes.Count > 0 && idx >= 0 && idx < _flattenedNodes.Count
					? _flattenedNodes[idx]
					: null;
			}
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

		/// <summary>
		/// Adds a root node to the tree control.
		/// </summary>
		/// <param name="node">The node to add.</param>
		public void AddRootNode(TreeNode node)
		{
			if (node == null)
				return;

			_rootNodes.Add(node);
			UpdateFlattenedNodes();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a root node with the specified text.
		/// </summary>
		/// <param name="text">Text for the new node.</param>
		/// <returns>The newly created node.</returns>
		public TreeNode AddRootNode(string text)
		{
			var node = new TreeNode(text);
			_rootNodes.Add(node);
			UpdateFlattenedNodes();
			Container?.Invalidate(true);
			return node;
		}

		/// <summary>
		/// Clears all nodes from the tree.
		/// </summary>
		public void Clear()
		{
			_rootNodes.Clear();
			_flattenedNodes.Clear();
			_textMeasurementCache.InvalidateCache(); // Clear cache when tree cleared

			// Clear state via services (single source of truth)
			int oldIndex = _selectedIndex;
		_selectedIndex = -1;
		if (oldIndex != _selectedIndex)
		{
			SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(null));
		}
			_scrollOffset = 0;

			Container?.Invalidate(true);
		}

		/// <summary>
		/// Collapses all nodes in the tree.
		/// </summary>
		public void CollapseAll()
		{
			CollapseNodes(_rootNodes);
			UpdateFlattenedNodes();
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <summary>
		/// Ensures a node is visible by expanding all its parent nodes.
		/// </summary>
		/// <param name="targetNode">The node to make visible.</param>
		/// <returns>True if the node was found and made visible, false otherwise.</returns>
		public bool EnsureNodeVisible(TreeNode targetNode)
		{
			if (targetNode == null)
				return false;

			// Try to find the node and its path in the tree
			var path = new Stack<TreeNode>();
			if (FindNodePath(targetNode, _rootNodes, path))
			{
				// Expand all parent nodes
				while (path.Count > 0)
				{
					var node = path.Pop();
					node.IsExpanded = true;
				}

				UpdateFlattenedNodes();
				Container?.Invalidate(true);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Expands all nodes in the tree.
		/// </summary>
		public void ExpandAll()
		{
			ExpandNodes(_rootNodes);
			UpdateFlattenedNodes();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Finds a node by its associated tag object.
		/// </summary>
		/// <param name="tag">The tag object to search for.</param>
		/// <returns>The first node found with matching tag, or null if not found.</returns>
		public TreeNode? FindNodeByTag(object tag)
		{
			if (tag == null)
				return null;

			return FindNodeByTagRecursive(tag, _rootNodes);
		}

		/// <summary>
		/// Finds a node by its text content (exact match).
		/// </summary>
		/// <param name="text">The text to search for.</param>
		/// <param name="searchRoot">Optional root to search from; searches all nodes if null.</param>
		/// <returns>The first node found with matching text, or null if not found.</returns>
		public TreeNode? FindNodeByText(string text, TreeNode? searchRoot = null)
		{
			if (string.IsNullOrEmpty(text))
				return null;

			if (searchRoot != null)
			{
				// Search within a specific node and its children
				if (searchRoot.Text == text)
					return searchRoot;

				foreach (var child in searchRoot.Children)
				{
					var found = FindNodeByText(text, child);
					if (found != null)
						return found;
				}
				return null;
			}
			else
			{
				// Search all nodes
				foreach (var node in _rootNodes)
				{
					var found = FindNodeByText(text, node);
					if (found != null)
						return found;
				}
				return null;
			}
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			UpdateFlattenedNodes();
			int width = ContentWidth ?? 0;
			int height = _flattenedNodes.Count + _margin.Top + _margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled || _flattenedNodes.Count == 0)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			int selectedIndex = CurrentSelectedIndex;
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					if (SelectionStateHelper.UpdateSelectionWithScroll(
						ref _selectedIndex,
						selectedIndex - 1,
						_flattenedNodes.Count,
						ref _scrollOffset,
						effectiveMaxVisibleItems,
						OnSelectionChanged))
					{
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.DownArrow:
					if (SelectionStateHelper.UpdateSelectionWithScroll(
						ref _selectedIndex,
						selectedIndex + 1,
						_flattenedNodes.Count,
						ref _scrollOffset,
						effectiveMaxVisibleItems,
						OnSelectionChanged))
					{
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.PageUp:
				{
					int pageSize = effectiveMaxVisibleItems;
					int newIndex = Math.Max(0, selectedIndex - pageSize);
					if (SelectionStateHelper.UpdateSelectionWithScroll(
						ref _selectedIndex,
						newIndex,
						_flattenedNodes.Count,
						ref _scrollOffset,
						effectiveMaxVisibleItems,
						OnSelectionChanged))
					{
						Container?.Invalidate(true);
						return true;
					}
					break;
				}

				case ConsoleKey.PageDown:
				{
					int pageSize = effectiveMaxVisibleItems;
					int newIndex = Math.Min(_flattenedNodes.Count - 1, selectedIndex + pageSize);
					if (SelectionStateHelper.UpdateSelectionWithScroll(
						ref _selectedIndex,
						newIndex,
						_flattenedNodes.Count,
						ref _scrollOffset,
						effectiveMaxVisibleItems,
						OnSelectionChanged))
					{
						Container?.Invalidate(true);
						return true;
					}
					break;
				}

				case ConsoleKey.RightArrow:
					if (SelectedNode != null && !SelectedNode.IsExpanded)
					{
						SelectedNode.IsExpanded = true;
						NodeExpandCollapse?.Invoke(this, new TreeNodeEventArgs(SelectedNode));
						UpdateFlattenedNodes();
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.LeftArrow:
					if (SelectedNode != null)
					{
						if (SelectedNode.IsExpanded && SelectedNode.Children.Count > 0)
						{
							// Collapse the expanded node
							SelectedNode.IsExpanded = false;
							NodeExpandCollapse?.Invoke(this, new TreeNodeEventArgs(SelectedNode));
							UpdateFlattenedNodes();
							Container?.Invalidate(true);
							return true;
						}
					}
					break;

				case ConsoleKey.Spacebar:
				case ConsoleKey.Enter:
					if (SelectedNode != null)
					{
						// Toggle expand/collapse
						SelectedNode.IsExpanded = !SelectedNode.IsExpanded;
						NodeExpandCollapse?.Invoke(this, new TreeNodeEventArgs(SelectedNode));
						UpdateFlattenedNodes();
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.Home:
					if (SelectionStateHelper.UpdateSelectionWithScroll(
						ref _selectedIndex,
						0,
						_flattenedNodes.Count,
						ref _scrollOffset,
						effectiveMaxVisibleItems,
						OnSelectionChanged))
					{
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.End:
					if (SelectionStateHelper.UpdateSelectionWithScroll(
						ref _selectedIndex,
						_flattenedNodes.Count - 1,
						_flattenedNodes.Count,
						ref _scrollOffset,
						effectiveMaxVisibleItems,
						OnSelectionChanged))
					{
						Container?.Invalidate(true);
						return true;
					}
					break;
			}

			return false;
		}

		// Helper method to invoke selection changed event (called by SelectionStateHelper)
		private void OnSelectionChanged(int newIndex)
		{
			var selectedNode = newIndex >= 0 && newIndex < _flattenedNodes.Count ? _flattenedNodes[newIndex] : null;
			SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(selectedNode));
		}

		/// <summary>
		/// Removes a root node from the tree.
		/// </summary>
		/// <param name="node">The node to remove.</param>
		/// <returns>True if the node was found and removed, false otherwise.</returns>
		public bool RemoveRootNode(TreeNode node)
		{
			if (node == null || !_rootNodes.Contains(node))
				return false;

			bool result = _rootNodes.Remove(node);
			if (result)
			{
				UpdateFlattenedNodes();
				Container?.Invalidate(true);
			}
			return result;
		}

		/// <summary>
		/// Selects a specific node in the tree.
		/// </summary>
		/// <param name="node">The node to select.</param>
		/// <returns>True if the node was found and selected, false otherwise.</returns>
		public bool SelectNode(TreeNode node)
		{
			if (node == null)
				return false;

			// Make sure the node is in our flattened list (which means it's visible)
			int index = _flattenedNodes.IndexOf(node);
			if (index >= 0)
			{
				if (_selectedIndex != index)
				{
					_selectedIndex = index;
					SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(node));
				}
				Container?.Invalidate(true);
				return true;
			}

			// Node might be hidden (collapsed parent), try to expand parents
			if (EnsureNodeVisible(node))
			{
				// Update flattened nodes after expanding
				UpdateFlattenedNodes();

				// Try to find the node again
				index = _flattenedNodes.IndexOf(node);
				if (index >= 0)
				{
					int oldIndex = _selectedIndex;
					_selectedIndex = index;
				if (oldIndex != _selectedIndex)
				{
					var selectedNode = _selectedIndex >= 0 && _selectedIndex < _flattenedNodes.Count ? _flattenedNodes[_selectedIndex] : null;
					SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(selectedNode));
				}
					Container?.Invalidate(true);
					return true;
				}
			}

			return false;
		}

		// IFocusableControl implementation
		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = _hasFocus;
			_hasFocus = focus;

			// When gaining focus, make sure the selected node is visible
			if (focus && SelectedNode != null)
			{
				// Ensure the selected node is visible (expand parent nodes if needed)
				EnsureNodeVisible(SelectedNode);

				// Position the selected node in the middle of the visible area when gaining focus
				int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
				int selectedIndex = CurrentSelectedIndex;

				// Try to position the selected item in the middle of the viewport
				int desiredPosition = Math.Max(0, effectiveMaxVisibleItems / 2);
				int newScrollOffset = Math.Max(0, selectedIndex - desiredPosition);

				// Make sure we don't scroll past the end
				int maxScrollOffset = Math.Max(0, _flattenedNodes.Count - effectiveMaxVisibleItems);
				int validScrollOffset = Math.Min(newScrollOffset, maxScrollOffset);
				_scrollOffset = validScrollOffset;
			}

			Container?.Invalidate(true);

			// Fire focus events
			if (focus && !hadFocus)
			{
				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		/// <summary>
		/// Build the tree prefix for a node based on its depth and position
		/// </summary>
		private string BuildTreePrefix(int depth, bool isLast, (string cross, string corner, string tee, string vertical, string horizontal) guides)
		{
			if (depth == 0)
				return "";

			StringBuilder prefix = new StringBuilder();

			// Add indentation based on depth
			for (int i = 0; i < depth - 1; i++)
			{
				prefix.Append(_indent);
			}

			// Add appropriate connector for the current node
			string connector = isLast ? guides.corner : guides.tee;
			string horizontalLine = guides.horizontal;

			prefix.Append(connector);
			prefix.Append(horizontalLine);
			prefix.Append(" ");

			return prefix.ToString();
		}

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

		private (string cross, string corner, string tee, string vertical, string horizontal) GetGuideChars()
		{
			switch (_guide)
			{
				case var _ when _guide == TreeGuide.Ascii:
					return ("+", "\\", "+", "|", "-");

				case var _ when _guide == TreeGuide.DoubleLine:
					return ("╬", "╚", "╚", "║", "═");

				case var _ when _guide == TreeGuide.BoldLine:
					return ("┿", "┗", "┗", "┃", "━");

				case var _ when _guide == TreeGuide.Line:
				default:
					return ("┼", "└", "└", "│", "─");
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
		/// Recursively renders a collection of tree nodes into the Spectre.Console tree structure
		/// </summary>
		/// <param name="nodes">Collection of nodes to render</param>
		/// <param name="parent">Parent Spectre.Console tree node to add children to</param>
		/// <param name="depth">Current depth level in the tree</param>
		private void RenderNodes(List<TreeNode> nodes, IHasTreeNodes parent, int depth)
		{
			if (nodes == null || nodes.Count == 0)
				return;

			int selectedIndex = CurrentSelectedIndex;
			foreach (var node in nodes)
			{
				// Get display style for this node
				Style nodeStyle;

				if (_flattenedNodes.Contains(node) && selectedIndex == _flattenedNodes.IndexOf(node) && _hasFocus)
				{
					// Selected node style
					nodeStyle = new Style(foreground: HighlightForegroundColor, background: HighlightBackgroundColor);
				}
				else
				{
					// Normal node style based on node's color or default colors
					Color nodeFg = node.TextColor ?? ForegroundColor;
					nodeStyle = new Style(foreground: nodeFg, background: BackgroundColor);
				}

				// Add the current node to the tree
				string displayText = node.Text ?? string.Empty;

				// Add indicator if node has children
				if (node.Children.Count > 0)
				{
					displayText = $"{displayText} {(node.IsExpanded ? "[[-]]" : "[[+]]")}";
				}

				// Create the tree node with proper style
				var treeNode = parent.AddNode(new Markup(displayText, nodeStyle));

				// Only process children if the node is expanded
				if (node.IsExpanded && node.Children.Count > 0)
				{
					RenderNodes(node.Children.ToList(), treeNode, depth + 1);
				}
			}
		}

		private void RenderTreeNodes(List<TreeNode> nodes, List<string> output, int depth, int contentWidth)
		{
			if (nodes == null || nodes.Count == 0)
				return;

			// Define tree guide characters
			var guideChars = GetGuideChars();
			int selectedIndex = CurrentSelectedIndex;

			for (int i = 0; i < nodes.Count; i++)
			{
				var node = nodes[i];
				bool isLast = (i == nodes.Count - 1);

				// Build the tree prefix (the line graphics)
				string prefix = BuildTreePrefix(depth, isLast, guideChars);

				// Get node style and text
				string displayText = node.Text ?? string.Empty;
				Color textColor;
				Color backgroundColor;

				// Determine colors for this node
				if (_flattenedNodes.Contains(node) && selectedIndex == _flattenedNodes.IndexOf(node) && _hasFocus)
				{
					// Selected node
					textColor = HighlightForegroundColor;
					backgroundColor = HighlightBackgroundColor;
				}
				else
				{
					// Regular node
					textColor = node.TextColor ?? ForegroundColor;
					backgroundColor = BackgroundColor;
				}

				// Add expand/collapse indicator if the node has children
				string expandCollapseIndicator = "";
				if (node.Children.Count > 0)
				{
					expandCollapseIndicator = node.IsExpanded ? " [-]" : " [+]";
				}

				// Create the complete node display text
				string nodeText = prefix + displayText + expandCollapseIndicator;

				// Calculate the visible length of the text (without ANSI codes)
				int visibleLength = GetCachedTextLength(nodeText);

				// Truncate if necessary to fit in the available width
				if (visibleLength > contentWidth)
				{
					nodeText = TextTruncationHelper.TruncateWithFixedParts(
						prefix,
						displayText,
						expandCollapseIndicator,
						contentWidth,
						_textMeasurementCache);
					visibleLength = GetCachedTextLength(nodeText);
				}

				// Determine how much padding is needed to reach full width
				int paddingNeeded = Math.Max(0, contentWidth - visibleLength);

				// Add padding to reach full width
				if (paddingNeeded > 0)
				{
					nodeText += new string(' ', paddingNeeded);
				}

				// Add the formatted node with padding to the output
				string formattedNode = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					nodeText,
					contentWidth,
					1,
					false,
					backgroundColor,
					textColor
				).FirstOrDefault() ?? string.Empty;

				output.Add(formattedNode);

				// Recursively render children if the node is expanded
				if (node.IsExpanded && node.Children.Count > 0)
				{
					RenderTreeNodes(node.Children.ToList(), output, depth + 1, contentWidth);
				}
			}
		}

		/// <summary>
		/// Renders a subset of tree nodes, handling custom tree connector lines
		/// </summary>
		/// <param name="rootNode">The root node to start rendering from</param>
		/// <param name="output">Output buffer to add rendered lines to</param>
		/// <param name="depth">Depth of the root node</param>
		/// <param name="contentWidth">Available content width</param>
		/// <param name="visibleNodes">Set of nodes that should be visible</param>
		private void RenderTreeNodeSubset(TreeNode rootNode, List<string> output, int depth, int contentWidth, List<TreeNode> visibleNodes)
		{
			if (rootNode == null || !visibleNodes.Contains(rootNode))
				return;

			// Define tree guide characters
			var guideChars = GetGuideChars();
			int selectedIndex = CurrentSelectedIndex;

			// Determine if this is a "last" node at its level
			bool isLast = IsLastChildInParent(rootNode);

			// Build the tree prefix (the line graphics)
			string prefix = BuildTreePrefix(depth, isLast, guideChars);

			// Get node style and text
			string displayText = rootNode.Text ?? string.Empty;
			Color textColor;
			Color backgroundColor;

			// Determine colors for this node
			if (_flattenedNodes.Contains(rootNode) && selectedIndex == _flattenedNodes.IndexOf(rootNode) && _hasFocus)
			{
				// Selected node
				textColor = HighlightForegroundColor;
				backgroundColor = HighlightBackgroundColor;
			}
			else
			{
				// Regular node
				textColor = rootNode.TextColor ?? ForegroundColor;
				backgroundColor = BackgroundColor;
			}

			// Add expand/collapse indicator if the node has children
			string expandCollapseIndicator = "";
			if (rootNode.Children.Count > 0)
			{
				expandCollapseIndicator = rootNode.IsExpanded ? " [-]" : " [+]";
			}

			// Create the complete node display text
			string nodeText = prefix + displayText + expandCollapseIndicator;

			// Calculate the visible length of the text (without ANSI codes)
			int visibleLength = GetCachedTextLength(nodeText);

			// Truncate if necessary to fit in the available width
			if (visibleLength > contentWidth)
			{
				nodeText = TextTruncationHelper.TruncateWithFixedParts(
					prefix,
					displayText,
					expandCollapseIndicator,
					contentWidth,
					_textMeasurementCache);
				visibleLength = GetCachedTextLength(nodeText);
			}

			// Determine how much padding is needed to reach full width
			int paddingNeeded = Math.Max(0, contentWidth - visibleLength);

			// Add padding to reach full width
			if (paddingNeeded > 0)
			{
				nodeText += new string(' ', paddingNeeded);
			}

			// Add the formatted node with padding to the output
			string formattedNode = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
				nodeText,
				contentWidth,
				1,
				false,
				backgroundColor,
				textColor
			).FirstOrDefault() ?? string.Empty;

			output.Add(formattedNode);

			// Recursively render visible children if the node is expanded
			if (rootNode.IsExpanded && rootNode.Children.Count > 0)
			{
				// Only render children that are in the visible nodes list
				foreach (var child in rootNode.Children)
				{
					if (visibleNodes.Contains(child))
					{
						RenderTreeNodeSubset(child, output, depth + 1, contentWidth, visibleNodes);
					}
				}
			}
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

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			UpdateFlattenedNodes();

			int contentWidth = constraints.MaxWidth - _margin.Left - _margin.Right;

			// Calculate max item width
			int maxItemWidth = 0;
			var guideChars = GetGuideChars();
			foreach (var node in _flattenedNodes)
			{
				int depth = GetNodeDepth(node);
				string prefix = BuildTreePrefix(depth, IsLastChildInParent(node), guideChars);
				string displayText = node.Text ?? string.Empty;
				string expandIndicator = node.Children.Count > 0 ? " [-]" : "";
				int itemWidth = GetCachedTextLength(prefix + displayText + expandIndicator);
				if (itemWidth > maxItemWidth) maxItemWidth = itemWidth;
			}

			// Calculate width based on content or explicit width
			int contentBasedWidth = (_width ?? maxItemWidth) + _margin.Left + _margin.Right;

			// For Stretch alignment, request full available width
			// For other alignments, request only what content needs
			int width = _horizontalAlignment == HorizontalAlignment.Stretch
				? constraints.MaxWidth
				: contentBasedWidth;

			// Calculate height based on visible items
			int effectiveMaxVisibleItems;
			if (MaxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = MaxVisibleItems.Value;
			}
			else if (_height.HasValue)
			{
				effectiveMaxVisibleItems = _height.Value - _margin.Top - _margin.Bottom;
			}
			else
			{
				effectiveMaxVisibleItems = Math.Min(_flattenedNodes.Count, constraints.MaxHeight - _margin.Top - _margin.Bottom - 1);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			bool hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			int contentHeight = Math.Min(_flattenedNodes.Count, effectiveMaxVisibleItems);
			int height = contentHeight + _margin.Top + _margin.Bottom + (hasScrollIndicator ? 1 : 0);

			if (_height.HasValue)
			{
				height = _height.Value;
			}

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;
			int contentWidth = bounds.Width - _margin.Left - _margin.Right;
			int contentHeight = bounds.Height - _margin.Top - _margin.Bottom;

			if (contentWidth <= 0 || contentHeight <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			// Update flattened nodes
			UpdateFlattenedNodes();

			// Determine visible items
			// For VerticalAlignment.Fill controls, use the actual content height from bounds, not the cached measurement
			int effectiveMaxVisibleItems;
			if (_verticalAlignment == VerticalAlignment.Fill)
			{
				// VerticalAlignment.Fill: use all available space
				effectiveMaxVisibleItems = MaxVisibleItems ?? contentHeight;
			}
			else
			{
				// Normal: use cached measurement or content height
				effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? contentHeight;
			}
			bool hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			int visibleItemsHeight = hasScrollIndicator ? contentHeight - 1 : contentHeight;
			effectiveMaxVisibleItems = Math.Min(effectiveMaxVisibleItems, visibleItemsHeight);
			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			// Get and validate scroll offset
			int scrollOffset = CurrentScrollOffset;
			int maxScrollOffset = Math.Max(0, _flattenedNodes.Count - effectiveMaxVisibleItems);
			if (scrollOffset < 0 || scrollOffset > maxScrollOffset)
			{
				scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScrollOffset));
				_scrollOffset = scrollOffset;
			}

			// Ensure selected item is visible
			EnsureSelectedItemVisible();
			scrollOffset = CurrentScrollOffset;

			// Get guide characters
			var guideChars = GetGuideChars();
			int selectedIndex = CurrentSelectedIndex;

			// Render visible nodes
			int endIndex = Math.Min(scrollOffset + effectiveMaxVisibleItems, _flattenedNodes.Count);
			int paintRow = 0;
			for (int i = scrollOffset; i < endIndex && paintRow < visibleItemsHeight; i++, paintRow++)
			{
				var node = _flattenedNodes[i];
				int paintY = startY + paintRow;

				if (paintY < clipRect.Y || paintY >= clipRect.Bottom || paintY >= bounds.Bottom)
					continue;

				// Calculate node depth and build prefix
				int depth = GetNodeDepth(node);
				bool isLast = IsLastChildInParent(node);
				string prefix = BuildTreePrefix(depth, isLast, guideChars);

				// Get node text and colors
				string displayText = node.Text ?? string.Empty;
				Color textColor;
				Color nodeBgColor;

				if (i == selectedIndex && _hasFocus)
				{
					textColor = HighlightForegroundColor;
					nodeBgColor = HighlightBackgroundColor;
				}
				else
				{
					textColor = node.TextColor ?? fgColor;
					nodeBgColor = bgColor;
				}

				// Add expand/collapse indicator
				string expandIndicator = "";
				if (node.Children.Count > 0)
				{
					expandIndicator = node.IsExpanded ? " [-]" : " [+]";
				}

				// Build full node text
				string nodeText = prefix + displayText + expandIndicator;
				int visibleLength = GetCachedTextLength(nodeText);

				// Truncate if necessary
				if (visibleLength > contentWidth)
				{
					nodeText = TextTruncationHelper.TruncateWithFixedParts(
						prefix,
						displayText,
						expandIndicator,
						contentWidth,
						_textMeasurementCache);
					visibleLength = GetCachedTextLength(nodeText);
				}

				// Fill left margin
				if (_margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, bgColor);
				}

				// Calculate alignment offset
				int alignOffset = 0;
				if (visibleLength < contentWidth)
				{
					switch (_horizontalAlignment)
					{
						case HorizontalAlignment.Center:
							alignOffset = (contentWidth - visibleLength) / 2;
							break;
						case HorizontalAlignment.Right:
							alignOffset = contentWidth - visibleLength;
							break;
					}
				}

				// Fill left alignment padding
				if (alignOffset > 0)
				{
					buffer.FillRect(new LayoutRect(startX, paintY, alignOffset, 1), ' ', textColor, nodeBgColor);
				}

				// Render the node text
				string formattedNode = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					nodeText,
					visibleLength,
					1,
					false,
					nodeBgColor,
					textColor
				).FirstOrDefault() ?? string.Empty;

				var cells = AnsiParser.Parse(formattedNode, textColor, nodeBgColor);
				buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

				// Fill right padding
				int rightPadStart = startX + alignOffset + visibleLength;
				int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
				if (rightPadWidth > 0)
				{
					buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', textColor, nodeBgColor);
				}

				// Fill right margin
				if (_margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', fgColor, bgColor);
				}
			}

			// Fill remaining content rows if needed
			for (int row = paintRow; row < visibleItemsHeight; row++)
			{
				int paintY = startY + row;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, paintY, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			// Draw scroll indicator if needed
			if (hasScrollIndicator)
			{
				int scrollY = startY + visibleItemsHeight;
				if (scrollY >= clipRect.Y && scrollY < clipRect.Bottom && scrollY < bounds.Bottom)
				{
					// Fill the scroll indicator line
					buffer.FillRect(new LayoutRect(bounds.X, scrollY, bounds.Width, 1), ' ', fgColor, bgColor);

					// Up arrow
					char upArrow = scrollOffset > 0 ? '▲' : ' ';
					buffer.SetCell(startX, scrollY, upArrow, fgColor, bgColor);

					// Down arrow
					char downArrow = scrollOffset + effectiveMaxVisibleItems < _flattenedNodes.Count ? '▼' : ' ';
					buffer.SetCell(startX + contentWidth - 1, scrollY, downArrow, fgColor, bgColor);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - _margin.Bottom, fgColor, bgColor);
		}

		#endregion
	}

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