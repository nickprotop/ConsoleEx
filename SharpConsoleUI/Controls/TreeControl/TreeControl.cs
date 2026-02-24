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
	public partial class TreeControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		private readonly List<TreeNode> _rootNodes = new();
		private Color? _backgroundColorValue;
		private int? _calculatedMaxVisibleItems;
		private List<TreeNode> _flattenedNodes = new();
		private Color? _foregroundColorValue;
		private TreeGuide _guide = TreeGuide.Line;
		private bool _hasFocus = false;
		private int? _height;
		private string _indent = "  ";
		private bool _isEnabled = true;

		// Local selection state
		private int _selectedIndex = 0;
		private int _scrollOffset = 0;

		// Mouse interaction state
		private int _hoveredIndex = -1;
		private readonly object _clickLock = new object();
		private DateTime _lastClickTime = DateTime.MinValue;
		private int _lastClickIndex = -1;

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
		public override int? ContentWidth
		{
			get
			{
				if (_flattenedNodes.Count == 0) return Margin.Left + Margin.Right;

				int maxLength = 0;
				var guideChars = GetGuideChars();
				foreach (var node in _flattenedNodes)
				{
					int depth = GetNodeDepth(node);
					bool[] ancestorIsLast = GetAncestorIsLastArray(node);
					string prefix = BuildTreePrefix(depth, ancestorIsLast, guideChars);
					string displayText = node.Text ?? string.Empty;
					string expandIndicator = node.Children.Count > 0 ? " [-]" : "";
					int length = GetCachedTextLength(prefix + displayText + expandIndicator);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + Margin.Left + Margin.Right;
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
		/// Event that fires when a node is activated (double-clicked or Enter pressed on a leaf node).
		/// </summary>
		public event EventHandler<TreeNodeEventArgs>? NodeActivated;

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
						EnsureSelectedItemVisible();
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
		protected override void OnDisposing()
		{
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
		public override System.Drawing.Size GetLogicalContentSize()
		{
			UpdateFlattenedNodes();
			int width = ContentWidth ?? 0;
			int height = _flattenedNodes.Count + Margin.Top + Margin.Bottom;
			return new System.Drawing.Size(width, height);
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
				EnsureSelectedItemVisible();
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
					EnsureSelectedItemVisible();
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

			// When gaining focus via keyboard/programmatic, scroll to show the selected node.
			// For mouse focus, the user is clicking a specific item - don't touch the scroll.
			if (focus && SelectedNode != null && reason != FocusReason.Mouse)
			{
				EnsureSelectedItemVisible();
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
	}
}
