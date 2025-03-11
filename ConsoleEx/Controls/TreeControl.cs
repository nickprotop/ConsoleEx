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
using System.Text;

namespace ConsoleEx.Controls
{
	public class TreeControl : IWIndowControl, IInteractiveControl
	{
		private readonly List<TreeNode> _rootNodes = new();
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColor;
		private List<string>? _cachedContent;
		private int? _calculatedMaxVisibleItems;
		private bool _fillHeight = false;
		private List<TreeNode> _flattenedNodes = new();
		private Color? _foregroundColor;
		private TreeGuide _guide = TreeGuide.Line;
		private bool _hasFocus = false;
		private int? _height;
		private string _indent = "  ";
		private bool _isEnabled = true;
		private Margin _margin = new(0, 0, 0, 0);
		private int _scrollOffset = 0;
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
		{ get { return _backgroundColor ?? Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black; } set { _backgroundColor = value; Invalidate(); } }

		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets whether the control should fill all available height with empty lines
		/// </summary>
		public bool FillHeight
		{
			get => _fillHeight;
			set
			{
				_fillHeight = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

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
		/// Gets or sets the explicit height of the control.
		/// If null, control height is based on content until available height.
		/// </summary>
		public int? Height
		{
			get => _height;
			set
			{
				_height = value;
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

		// Add property to track visible items
		/// <summary>
		/// Gets or sets the maximum number of items to display at once.
		/// If null, shows as many as will fit in available height.
		/// </summary>
		public int? MaxVisibleItems { get; set; }

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

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					if (_selectedIndex > 0)
					{
						_selectedIndex--;
						EnsureSelectedItemVisible();
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
						EnsureSelectedItemVisible();
						OnSelectedNodeChanged?.Invoke(this, SelectedNode);
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.PageUp:
					if (_selectedIndex > 0)
					{
						// Move up by a page (max visible items)
						int pageSize = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
						_selectedIndex = Math.Max(0, _selectedIndex - pageSize);
						EnsureSelectedItemVisible();
						OnSelectedNodeChanged?.Invoke(this, SelectedNode);
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					break;

				case ConsoleKey.PageDown:
					if (_selectedIndex < _flattenedNodes.Count - 1)
					{
						// Move down by a page (max visible items)
						int pageSize = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
						_selectedIndex = Math.Min(_flattenedNodes.Count - 1, _selectedIndex + pageSize);
						EnsureSelectedItemVisible();
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
						EnsureSelectedItemVisible();
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
						EnsureSelectedItemVisible();
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

			// Update the flattened nodes list
			UpdateFlattenedNodes();

			// Calculate effective content width
			int effectiveWidth = _width ?? availableWidth ?? 80;
			int contentWidth = effectiveWidth - _margin.Left - _margin.Right;

			// Determine how many items can be displayed
			int effectiveMaxVisibleItems;
			bool hasScrollIndicator = false;

			if (MaxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = MaxVisibleItems.Value;
				hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			}
			else if (availableHeight.HasValue)
			{
				// Account for margin and scroll indicators when calculating max items
				int availableContentHeight = availableHeight.Value - _margin.Top - _margin.Bottom - 1; // -1 for potential scroll indicator
				effectiveMaxVisibleItems = availableContentHeight;
				hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			}
			else
			{
				// Default if no height constraint
				effectiveMaxVisibleItems = 10;
				hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			}

			// Store calculated max visible items
			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			// Ensure scroll offset is within valid range
			int maxScrollOffset = Math.Max(0, _flattenedNodes.Count - effectiveMaxVisibleItems);
			_scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxScrollOffset));

			// Ensure selected item is visible
			EnsureSelectedItemVisible();

			// Prepare output buffer
			var renderedContent = new List<string>();

			// Get visible nodes based on scroll offset
			var visibleNodes = new List<TreeNode>();
			int endIndex = Math.Min(_scrollOffset + effectiveMaxVisibleItems, _flattenedNodes.Count);

			// Map flattened nodes back to tree structure for rendering
			for (int i = _scrollOffset; i < endIndex; i++)
			{
				visibleNodes.Add(_flattenedNodes[i]);
			}

			// Render nodes
			if (visibleNodes.Count > 0)
			{
				// We need to create a parent-child structure for visible nodes
				Dictionary<TreeNode, TreeNode?> nodeParents = new Dictionary<TreeNode, TreeNode?>();
				foreach (var node in _flattenedNodes)
				{
					nodeParents[node] = FindParentNode(node);
				}

				// Group visible nodes by their root node
				Dictionary<TreeNode, List<TreeNode>> nodesByRoot = new Dictionary<TreeNode, List<TreeNode>>();

				foreach (var node in visibleNodes)
				{
					TreeNode? currentNode = node;
					TreeNode rootNode = node;

					// Find the root node for this visible node
					while (currentNode != null)
					{
						TreeNode? parent = nodeParents.TryGetValue(currentNode, out var p) ? p : null;
						if (parent == null || !visibleNodes.Contains(parent))
						{
							rootNode = currentNode;
							break;
						}
						currentNode = parent;
					}

					if (!nodesByRoot.ContainsKey(rootNode))
					{
						nodesByRoot[rootNode] = new List<TreeNode>();
					}
					nodesByRoot[rootNode].Add(node);
				}

				// Render visible nodes
				foreach (var rootNode in nodesByRoot.Keys)
				{
					// Find the depth of this root node
					int rootDepth = GetNodeDepth(rootNode);

					// Custom rendering for visible nodes
					RenderTreeNodeSubset(rootNode, renderedContent, rootDepth, contentWidth, visibleNodes);
				}
			}

			// Apply margins and alignment
			var finalContent = new List<string>();

			for (int i = 0; i < renderedContent.Count; i++)
			{
				string line = renderedContent[i];

				// Apply horizontal alignment
				int paddingLeft = 0;
				if (_alignment == Alignment.Center)
				{
					int visibleLength = AnsiConsoleHelper.StripAnsiStringLength(line);
					paddingLeft = Math.Max(0, (effectiveWidth - visibleLength - _margin.Left - _margin.Right) / 2);
				}
				else if (_alignment == Alignment.Right)
				{
					int visibleLength = AnsiConsoleHelper.StripAnsiStringLength(line);
					paddingLeft = Math.Max(0, effectiveWidth - visibleLength - _margin.Left - _margin.Right);
				}

				// Apply left margin and alignment padding
				string leftPadding = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', _margin.Left + paddingLeft),
					_margin.Left + paddingLeft,
					1,
					false,
					BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				// Apply right margin
				string rightPadding = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', _margin.Right),
					_margin.Right,
					1,
					false,
					BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				finalContent.Add(leftPadding + line + rightPadding);
			}

			// Apply top margin
			if (_margin.Top > 0)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', effectiveWidth),
					effectiveWidth,
					1,
					false,
					BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				finalContent.InsertRange(0, Enumerable.Repeat(emptyLine, _margin.Top));
			}

			// Add scroll indicator if needed
			if (hasScrollIndicator)
			{
				string scrollIndicator = "";

				// Up arrow if not at the top
				if (_scrollOffset > 0)
					scrollIndicator += "▲";
				else
					scrollIndicator += " ";

				// Padding in the middle
				int scrollPadding = effectiveWidth - 2;
				if (scrollPadding > 0)
					scrollIndicator += new string(' ', scrollPadding);

				// Down arrow if not at the bottom
				if (_scrollOffset + effectiveMaxVisibleItems < _flattenedNodes.Count)
					scrollIndicator += "▼";
				else
					scrollIndicator += " ";

				string formattedScrollIndicator = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					scrollIndicator,
					effectiveWidth,
					1,
					false,
					BackgroundColor,
					ForegroundColor
				).FirstOrDefault() ?? string.Empty;

				finalContent.Add(formattedScrollIndicator);
			}

			// Apply bottom margin
			if (_margin.Bottom > 0)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', effectiveWidth),
					effectiveWidth,
					1,
					false,
					BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				finalContent.AddRange(Enumerable.Repeat(emptyLine, _margin.Bottom));
			}

			// Handle height constraints
			if (availableHeight.HasValue)
			{
				// Determine the actual height to use
				int actualHeight;

				if (_height.HasValue)
				{
					// Use the smaller of specified height or available height
					actualHeight = Math.Min(_height.Value, availableHeight.Value);
				}
				else
				{
					// Use available height as max height but don't exceed content
					actualHeight = Math.Min(finalContent.Count, availableHeight.Value);
				}

				// Truncate content if it exceeds the actual height
				if (finalContent.Count > actualHeight)
				{
					finalContent = finalContent.Take(actualHeight).ToList();
				}

				// Fill with empty lines if FillHeight is true and content is less than actual height
				if (_fillHeight && finalContent.Count < actualHeight)
				{
					string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', effectiveWidth),
						effectiveWidth,
						1,
						false,
						BackgroundColor,
						null
					).FirstOrDefault() ?? string.Empty;

					int linesToAdd = actualHeight - finalContent.Count;
					finalContent.AddRange(Enumerable.Repeat(emptyLine, linesToAdd));
				}
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
			if (_selectedIndex < 0 || _flattenedNodes.Count == 0)
				return;

			// Calculate effective max visible items considering available space
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;

			// Adjust scroll offset if selected item is outside visible range
			if (_selectedIndex < _scrollOffset)
			{
				_scrollOffset = _selectedIndex;
			}
			else if (_selectedIndex >= _scrollOffset + effectiveMaxVisibleItems)
			{
				_scrollOffset = _selectedIndex - effectiveMaxVisibleItems + 1;
			}

			// Ensure scroll offset is valid
			_scrollOffset = Math.Max(0, Math.Min(_scrollOffset, _flattenedNodes.Count - effectiveMaxVisibleItems));
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
		/// Find the parent node of a given node in the tree
		/// </summary>
		/// <param name="node">The node to find parent for</param>
		/// <returns>Parent node or null if node is a root node</returns>
		private TreeNode? FindParentNode(TreeNode node)
		{
			if (node == null || _rootNodes.Contains(node))
				return null;

			return FindParentNodeRecursive(node, _rootNodes);
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
		/// Calculates the depth of a node in the tree
		/// </summary>
		/// <param name="node">The node to calculate depth for</param>
		/// <returns>The depth of the node (0 for root nodes)</returns>
		private int GetNodeDepth(TreeNode node)
		{
			if (node == null)
				return -1;

			if (_rootNodes.Contains(node))
				return 0;

			int depth = 0;
			TreeNode? current = node;

			while (current != null)
			{
				TreeNode? parent = FindParentNode(current);
				if (parent == null)
					break;

				depth++;
				current = parent;
			}

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

		private void RenderTreeNodes(List<TreeNode> nodes, List<string> output, int depth, int contentWidth)
		{
			if (nodes == null || nodes.Count == 0)
				return;

			// Define tree guide characters
			var guideChars = GetGuideChars();

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
				if (_flattenedNodes.Contains(node) && _selectedIndex == _flattenedNodes.IndexOf(node) && _hasFocus)
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
				int visibleLength = AnsiConsoleHelper.StripAnsiStringLength(nodeText);

				// Truncate if necessary to fit in the available width
				if (visibleLength > contentWidth)
				{
					// Truncate the displayText, not the prefix
					int prefixLength = AnsiConsoleHelper.StripAnsiStringLength(prefix);
					int maxTextLength = contentWidth - prefixLength - (node.Children.Count > 0 ? 4 : 0) - 3; // 3 for "..."

					if (maxTextLength > 0)
					{
						displayText = displayText.Substring(0, Math.Min(displayText.Length, maxTextLength)) + "...";
						nodeText = prefix + displayText + expandCollapseIndicator;
						visibleLength = AnsiConsoleHelper.StripAnsiStringLength(nodeText);
					}
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

			// Determine if this is a "last" node at its level
			bool isLast = IsLastChildInParent(rootNode);

			// Build the tree prefix (the line graphics)
			string prefix = BuildTreePrefix(depth, isLast, guideChars);

			// Get node style and text
			string displayText = rootNode.Text ?? string.Empty;
			Color textColor;
			Color backgroundColor;

			// Determine colors for this node
			if (_flattenedNodes.Contains(rootNode) && _selectedIndex == _flattenedNodes.IndexOf(rootNode) && _hasFocus)
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
			int visibleLength = AnsiConsoleHelper.StripAnsiStringLength(nodeText);

			// Truncate if necessary to fit in the available width
			if (visibleLength > contentWidth)
			{
				// Truncate the displayText, not the prefix
				int prefixLength = AnsiConsoleHelper.StripAnsiStringLength(prefix);
				int maxTextLength = contentWidth - prefixLength - (rootNode.Children.Count > 0 ? 4 : 0) - 3; // 3 for "..."

				if (maxTextLength > 0)
				{
					displayText = displayText.Substring(0, Math.Min(displayText.Length, maxTextLength)) + "...";
					nodeText = prefix + displayText + expandCollapseIndicator;
					visibleLength = AnsiConsoleHelper.StripAnsiStringLength(nodeText);
				}
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