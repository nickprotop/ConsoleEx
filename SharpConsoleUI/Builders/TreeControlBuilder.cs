// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using TreeNode = SharpConsoleUI.Controls.TreeNode;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for tree controls
/// </summary>
public sealed class TreeControlBuilder
{
	private readonly List<TreeNode> _rootNodes = new();
	private TreeGuide _guide = TreeGuide.Line;
	private string _indent = "  ";
	private int? _maxVisibleItems;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private Color _highlightBackgroundColor = Color.Blue;
	private Color _highlightForegroundColor = Color.White;
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private bool _isEnabled = true;
	private int? _width;
	private int? _height;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private string? _name;
	private object? _tag;

	// Event handlers
	private EventHandler<TreeNodeEventArgs>? _nodeExpandCollapseHandler;
	private EventHandler<TreeNodeEventArgs>? _selectedNodeChangedHandler;
	private EventHandler? _gotFocusHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<TreeNodeEventArgs>? _nodeExpandCollapseWithWindowHandler;
	private WindowEventHandler<TreeNodeEventArgs>? _selectedNodeChangedWithWindowHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	/// <summary>
	/// Adds a root node to the tree
	/// </summary>
	/// <param name="node">The node to add</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder AddRootNode(TreeNode node)
	{
		if (node != null)
			_rootNodes.Add(node);
		return this;
	}

	/// <summary>
	/// Adds a root node with the specified text
	/// </summary>
	/// <param name="text">The text for the node</param>
	/// <returns>The newly created node (for chaining node operations)</returns>
	public TreeNode AddRootNode(string text)
	{
		var node = new TreeNode(text);
		_rootNodes.Add(node);
		return node;
	}

	/// <summary>
	/// Adds multiple root nodes to the tree
	/// </summary>
	/// <param name="nodes">The nodes to add</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder AddRootNodes(params TreeNode[] nodes)
	{
		foreach (var node in nodes)
		{
			if (node != null)
				_rootNodes.Add(node);
		}
		return this;
	}

	/// <summary>
	/// Adds multiple root nodes to the tree
	/// </summary>
	/// <param name="nodes">The nodes to add</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder AddRootNodes(IEnumerable<TreeNode> nodes)
	{
		foreach (var node in nodes)
		{
			if (node != null)
				_rootNodes.Add(node);
		}
		return this;
	}

	/// <summary>
	/// Sets the tree guide style
	/// </summary>
	/// <param name="guide">The guide style</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithGuide(TreeGuide guide)
	{
		_guide = guide;
		return this;
	}

	/// <summary>
	/// Sets the indent string for each level
	/// </summary>
	/// <param name="indent">The indent string</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithIndent(string indent)
	{
		_indent = indent ?? "  ";
		return this;
	}

	/// <summary>
	/// Sets the maximum number of visible items
	/// </summary>
	/// <param name="count">The maximum visible items</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithMaxVisibleItems(int count)
	{
		_maxVisibleItems = count;
		return this;
	}

	/// <summary>
	/// Sets the background and foreground colors
	/// </summary>
	/// <param name="foreground">The foreground color</param>
	/// <param name="background">The background color</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the background color
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the highlight colors for selected items
	/// </summary>
	/// <param name="foreground">The highlight foreground color</param>
	/// <param name="background">The highlight background color</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithHighlightColors(Color foreground, Color background)
	{
		_highlightForegroundColor = foreground;
		_highlightBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The horizontal alignment</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the tree horizontally
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder Centered()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="left">Left margin</param>
	/// <param name="top">Top margin</param>
	/// <param name="right">Right margin</param>
	/// <param name="bottom">Bottom margin</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	/// <param name="margin">The margin value for all sides</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The control width</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the height
	/// </summary>
	/// <param name="height">The control height</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the control is visible</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the enabled state
	/// </summary>
	/// <param name="enabled">Whether the control is enabled</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder Enabled(bool enabled = true)
	{
		_isEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Disables the tree control
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder Disabled()
	{
		_isEnabled = false;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the node expand/collapse event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnNodeExpandCollapse(EventHandler<TreeNodeEventArgs> handler)
	{
		_nodeExpandCollapseHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the node expand/collapse event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnNodeExpandCollapse(WindowEventHandler<TreeNodeEventArgs> handler)
	{
		_nodeExpandCollapseWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected node changed event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnSelectedNodeChanged(EventHandler<TreeNodeEventArgs> handler)
	{
		_selectedNodeChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected node changed event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnSelectedNodeChanged(WindowEventHandler<TreeNodeEventArgs> handler)
	{
		_selectedNodeChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the got focus event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the got focus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the lost focus event handler
	/// </summary>
	/// <param name="handler">The event handler</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the lost focus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public TreeControlBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the tree control
	/// </summary>
	/// <returns>The configured tree control</returns>
	public TreeControl Build()
	{
		var tree = new TreeControl
		{
			Guide = _guide,
			Indent = _indent,
			MaxVisibleItems = _maxVisibleItems,
			HighlightBackgroundColor = _highlightBackgroundColor,
			HighlightForegroundColor = _highlightForegroundColor,
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			IsEnabled = _isEnabled,
			Width = _width,
			Height = _height,
			StickyPosition = _stickyPosition,
			Name = _name,
			Tag = _tag
		};

		// Set optional colors
		if (_backgroundColor.HasValue)
			tree.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue)
			tree.ForegroundColor = _foregroundColor.Value;

		// Add root nodes
		foreach (var node in _rootNodes)
		{
			tree.AddRootNode(node);
		}

		// Attach standard event handlers
		if (_nodeExpandCollapseHandler != null)
			tree.NodeExpandCollapse += _nodeExpandCollapseHandler;
		if (_selectedNodeChangedHandler != null)
			tree.SelectedNodeChanged += _selectedNodeChangedHandler;
		if (_gotFocusHandler != null)
			tree.GotFocus += _gotFocusHandler;
		if (_lostFocusHandler != null)
			tree.LostFocus += _lostFocusHandler;

		// Attach window-aware event handlers
		if (_nodeExpandCollapseWithWindowHandler != null)
		{
			tree.NodeExpandCollapse += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_nodeExpandCollapseWithWindowHandler(sender, args, window);
			};
		}
		if (_selectedNodeChangedWithWindowHandler != null)
		{
			tree.SelectedNodeChanged += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedNodeChangedWithWindowHandler(sender, args, window);
			};
		}
		if (_gotFocusWithWindowHandler != null)
		{
			tree.GotFocus += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, args, window);
			};
		}
		if (_lostFocusWithWindowHandler != null)
		{
			tree.LostFocus += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, args, window);
			};
		}

		return tree;
	}

	/// <summary>
	/// Implicit conversion to TreeControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built tree control</returns>
	public static implicit operator TreeControl(TreeControlBuilder builder) => builder.Build();
}
