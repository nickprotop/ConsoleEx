// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;
using System.Collections.ObjectModel;

namespace ConsoleEx.Controls
{
	public class TreeControl : IWIndowControl, IInteractiveControl
	{
		private readonly List<TreeNode> _rootNodes = new();
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColor;
		private List<string>? _cachedContent;
		private List<TreeNode> _flattenedNodes = new();
		private Color? _foregroundColor;
		private TreeGuide _guide = TreeGuide.Line;
		private bool _hasFocus = false;
		private string _indent = "  ";
		private bool _isEnabled = true;
		private Margin _margin = new(0, 0, 0, 0);
		private int _selectedIndex = 0;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		public int? ActualWidth
		{
			get
			{
				if (_cachedContent == null) return null;

				int maxLength = 0;
				foreach (var line in _cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color BackgroundColor
		{ get { return _backgroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black; } set { _backgroundColor = value; Invalidate(); } }

		public IContainer? Container { get; set; }

		public Color ForegroundColor
		{ get { return _foregroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White; } set { _foregroundColor = value; Invalidate(); } }

		/// <summary>
		/// Gets or sets the tree guide style
		/// </summary>
		public TreeGuide Guide
		{
			get => _guide;
			set
			{
				_guide = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				_hasFocus = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
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
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Event that fires when a tree node is expanded or collapsed
		/// </summary>
		public Action<TreeControl, TreeNode>? OnNodeExpandCollapse { get; set; }

		/// <summary>
		/// Event that fires when the selected node changes
		/// </summary>
		public Action<TreeControl, TreeNode?>? OnSelectedNodeChanged { get; set; }

		/// <summary>
		/// Gets the collection of root nodes in the tree
		/// </summary>
		public ReadOnlyCollection<TreeNode> RootNodes => _rootNodes.AsReadOnly();

		/// <summary>
		/// Gets or sets the currently selected node index (flattenedNodes)
		/// </summary>
		public int SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				if (_flattenedNodes.Count > 0)
				{
					_selectedIndex = Math.Max(0, Math.Min(value, _flattenedNodes.Count - 1));
					_cachedContent = null;
					Container?.Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets the currently selected node
		/// </summary>
		public TreeNode? SelectedNode => _flattenedNodes.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _flattenedNodes.Count
			? _flattenedNodes[_selectedIndex]
			: null;

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public bool Visible
		{
			get => _visible;
			set
			{
				_visible = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public int? Width
		{
			get => _width;
			set
			{
				_width = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Adds a root node to the tree control
		/// </summary>
		/// <param name="node">The node to add</param>
		public void AddRootNode(TreeNode node)
		{
			if (node == null)
				return;

			_rootNodes.Add(node);
			UpdateFlattenedNodes();
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a root node with the specified text
		/// </summary>
		/// <param name="text">Text for the new node</param>
		/// <returns>The newly created node</returns>
		public TreeNode AddRootNode(string text)
		{
			var node = new TreeNode(text);
			_rootNodes.Add(node);
			UpdateFlattenedNodes();
			_cachedContent = null;
			Container?.Invalidate(true);
			return node;
		}

		/// <summary>
		/// Clears all nodes from the tree
		/// </summary>
		public void Clear()
		{
			_rootNodes.Clear();
			_flattenedNodes.Clear();
			_selectedIndex = -1;
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Collapses all nodes in the tree
		/// </summary>
		public void CollapseAll()
		{
			CollapseNodes(_rootNodes);
			UpdateFlattenedNodes();
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		public void Dispose()
		{
			Container = null;
		}

		/// <summary>
		/// Ensures a node is visible by expanding all its parent nodes
		/// </summary>
		/// <param name="targetNode">The node to make visible</param>
		/// <returns>True if the node was found and made visible, false otherwise</returns>
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
				_cachedContent = null;
				Container?.Invalidate(true);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Expands all nodes in the tree
		/// </summary>
		public void ExpandAll()
		{
			ExpandNodes(_rootNodes);
			UpdateFlattenedNodes();
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Finds a node by its associated tag object
		/// </summary>
		/// <param name="tag">The tag object to search for</param>
		/// <returns>The first node found with matching tag, or null if not found</returns>
		public TreeNode? FindNodeByTag(object tag)
		{
			if (tag == null)
				return null;

			return FindNodeByTagRecursive(tag, _rootNodes);
		}

		/// <summary>
		/// Finds a node by its text content (exact match)
		/// </summary>
		/// <param name="text">The text to search for</param>
		/// <param name="searchRoot">Optional root to search from, searches all nodes if null</param>
		/// <returns>The first node found with matching text, or null if not found</returns>
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

		public (int Left, int Top)? GetCursorPosition()
		{
			return null; // Tree doesn't need a cursor position
		}

		public void Invalidate()
		{
			_cachedContent = null;
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled || _flattenedNodes.Count == 0)
				return false;

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					if (_selectedIndex > 0)
					{
						_selectedIndex--;
						OnSelectedNodeChanged?.Invoke(this, SelectedNode);
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.DownArrow:
					if (_selectedIndex < _flattenedNodes.Count - 1)
					{
						_selectedIndex++;
						OnSelectedNodeChanged?.Invoke(this, SelectedNode);
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.RightArrow:
					if (SelectedNode != null && !SelectedNode.IsExpanded)
					{
						SelectedNode.IsExpanded = true;
						OnNodeExpandCollapse?.Invoke(this, SelectedNode);
						UpdateFlattenedNodes();
						_cachedContent = null;
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
							OnNodeExpandCollapse?.Invoke(this, SelectedNode);
							UpdateFlattenedNodes();
							_cachedContent = null;
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
						OnNodeExpandCollapse?.Invoke(this, SelectedNode);
						UpdateFlattenedNodes();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.Home:
					if (_selectedIndex != 0)
					{
						_selectedIndex = 0;
						OnSelectedNodeChanged?.Invoke(this, SelectedNode);
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.End:
					if (_selectedIndex != _flattenedNodes.Count - 1)
					{
						_selectedIndex = _flattenedNodes.Count - 1;
						OnSelectedNodeChanged?.Invoke(this, SelectedNode);
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;
			}

			return false;
		}

		/// <summary>
		/// Removes a root node from the tree
		/// </summary>
		/// <param name="node">The node to remove</param>
		/// <returns>True if the node was found and removed, false otherwise</returns>
		public bool RemoveRootNode(TreeNode node)
		{
			if (node == null || !_rootNodes.Contains(node))
				return false;

			bool result = _rootNodes.Remove(node);
			if (result)
			{
				UpdateFlattenedNodes();
				_cachedContent = null;
				Container?.Invalidate(true);
			}
			return result;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (_cachedContent != null) return _cachedContent;
			if (!Visible) return new List<string>();

			// Create the internal tree structure
			var tree = new Tree("").Style(new Style(foreground: ForegroundColor, background: BackgroundColor));

			tree.Guide = Guide;

			// Update the flattened nodes list
			UpdateFlattenedNodes();

			// Create a Spectre tree renderable
			var spectreTree = new Tree("")
			{
				Guide = Guide,
				Style = new Style(foreground: ForegroundColor, background: BackgroundColor)
			};

			// Render the tree nodes
			RenderNodes(_rootNodes, spectreTree, 0);

			// Convert tree to ANSI string
			var renderedAnsi = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(
				spectreTree,
				_width ?? availableWidth,
				null,
				BackgroundColor
			);

			// Apply margins and alignment
			var finalContent = new List<string>();
			int effectiveWidth = _width ?? availableWidth ?? 80;

			for (int i = 0; i < renderedAnsi.Count; i++)
			{
				string line = renderedAnsi[i];

				// Apply horizontal alignment
				int paddingLeft = 0;
				if (_alignment == Alignment.Center)
				{
					int contentWidth = AnsiConsoleHelper.StripAnsiStringLength(line);
					paddingLeft = Math.Max(0, (effectiveWidth - contentWidth) / 2);
				}
				else if (_alignment == Alignment.Right)
				{
					int contentWidth = AnsiConsoleHelper.StripAnsiStringLength(line);
					paddingLeft = Math.Max(0, effectiveWidth - contentWidth);
				}

				// Apply left margin
				line = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"{new string(' ', _margin.Left + paddingLeft)}",
					_margin.Left + paddingLeft,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault() + line;

				// Apply right margin
				line += AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					$"{new string(' ', _margin.Right)}",
					_margin.Right,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault();

				finalContent.Add(line);
			}

			// Apply top margin
			if (_margin.Top > 0)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', effectiveWidth),
					effectiveWidth,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				finalContent.InsertRange(0, Enumerable.Repeat(emptyLine, _margin.Top));
			}

			// Apply bottom margin
			if (_margin.Bottom > 0)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', effectiveWidth),
					effectiveWidth,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				finalContent.AddRange(Enumerable.Repeat(emptyLine, _margin.Bottom));
			}

			_cachedContent = finalContent;
			return finalContent;
		}

		/// <summary>
		/// Selects a specific node in the tree
		/// </summary>
		/// <param name="node">The node to select</param>
		/// <returns>True if the node was found and selected, false otherwise</returns>
		public bool SelectNode(TreeNode node)
		{
			if (node == null)
				return false;

			// Make sure the node is in our flattened list (which means it's visible)
			int index = _flattenedNodes.IndexOf(node);
			if (index >= 0)
			{
				_selectedIndex = index;
				_cachedContent = null;
				Container?.Invalidate(true);
				OnSelectedNodeChanged?.Invoke(this, node);
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
					_selectedIndex = index;
					_cachedContent = null;
					Container?.Invalidate(true);
					OnSelectedNodeChanged?.Invoke(this, node);
					return true;
				}
			}

			return false;
		}

		public void SetFocus(bool focus, bool backward)
		{
			_hasFocus = focus;

			// When gaining focus, make sure the selected node is visible
			if (focus && SelectedNode != null)
			{
				// Ensure the selected node is visible (expand parent nodes if needed)
				EnsureNodeVisible(SelectedNode);
			}

			_cachedContent = null;
			Container?.Invalidate(true);
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
		/// Recursively renders a collection of tree nodes into the Spectre.Console tree structure
		/// </summary>
		/// <param name="nodes">Collection of nodes to render</param>
		/// <param name="parent">Parent Spectre.Console tree node to add children to</param>
		/// <param name="depth">Current depth level in the tree</param>
		private void RenderNodes(List<TreeNode> nodes, IHasTreeNodes parent, int depth)
		{
			if (nodes == null || nodes.Count == 0)
				return;

			foreach (var node in nodes)
			{
				// Get display style for this node
				Style nodeStyle;

				if (_flattenedNodes.Contains(node) && _selectedIndex == _flattenedNodes.IndexOf(node) && _hasFocus)
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

		/// <summary>
		/// Updates the flattened nodes list for easier navigation
		/// </summary>
		private void UpdateFlattenedNodes()
		{
			_flattenedNodes.Clear();
			FlattenNodes(_rootNodes);

			// Ensure selected index is valid
			if (_flattenedNodes.Count > 0)
			{
				_selectedIndex = Math.Max(0, Math.Min(_selectedIndex, _flattenedNodes.Count - 1));
			}
			else
			{
				_selectedIndex = -1;
			}
		}
	}

	/// <summary>
	/// Represents a tree node in the TreeControl
	/// </summary>
	public class TreeNode
	{
		private List<TreeNode> _children = new();

		/// <summary>
		/// Creates a new tree node with specified text
		/// </summary>
		/// <param name="text">The text label for the node</param>
		public TreeNode(string text)
		{
			Text = text;
		}

		/// <summary>
		/// Gets the children of this node
		/// </summary>
		public ReadOnlyCollection<TreeNode> Children => _children.AsReadOnly();

		/// <summary>
		/// Gets or sets whether this node is expanded
		/// </summary>
		public bool IsExpanded { get; set; } = true;

		/// <summary>
		/// Gets or sets a custom object associated with this node
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text label of this node
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Gets or sets the optional color for this node's text
		/// </summary>
		public Color? TextColor { get; set; }

		/// <summary>
		/// Adds a child node to this node
		/// </summary>
		/// <param name="node">The child node to add</param>
		/// <returns>The added child node</returns>
		public TreeNode AddChild(TreeNode node)
		{
			_children.Add(node);
			return node;
		}

		/// <summary>
		/// Adds a child node with specified text
		/// </summary>
		/// <param name="text">The text label for the child node</param>
		/// <returns>The newly created and added child node</returns>
		public TreeNode AddChild(string text)
		{
			var node = new TreeNode(text);
			_children.Add(node);
			return node;
		}

		/// <summary>
		/// Clears all child nodes
		/// </summary>
		public void ClearChildren()
		{
			_children.Clear();
		}

		/// <summary>
		/// Removes a child node
		/// </summary>
		/// <param name="node">The node to remove</param>
		/// <returns>True if the node was found and removed, false otherwise</returns>
		public bool RemoveChild(TreeNode node)
		{
			return _children.Remove(node);
		}
	}
}