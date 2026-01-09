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

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for list controls
/// </summary>
public sealed class ListBuilder
{
	private readonly List<ListItem> _items = new();
	private string _title = "List";
	private int? _maxVisibleItems;
	private bool _isSelectable = true;
	private bool _autoAdjustWidth = false;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private EventHandler<ListItem>? _itemActivatedHandler;
	private EventHandler<int>? _selectionChangedHandler;
	private EventHandler<ListItem?>? _selectedItemChangedHandler;
	private WindowEventHandler<ListItem>? _itemActivatedWithWindowHandler;
	private WindowEventHandler<int>? _selectionChangedWithWindowHandler;
	private WindowEventHandler<ListItem?>? _selectedItemChangedWithWindowHandler;

	/// <summary>
	/// Sets the list title
	/// </summary>
	public ListBuilder WithTitle(string title)
	{
		_title = title;
		return this;
	}

	/// <summary>
	/// Adds an item to the list
	/// </summary>
	public ListBuilder AddItem(string text, object? tag = null)
	{
		_items.Add(new ListItem(text) { Tag = tag });
		return this;
	}

	/// <summary>
	/// Adds an item with icon to the list
	/// </summary>
	public ListBuilder AddItem(string text, string icon, Color? iconColor = null, object? tag = null)
	{
		_items.Add(new ListItem(text, icon, iconColor) { Tag = tag });
		return this;
	}

	/// <summary>
	/// Adds a ListItem to the list
	/// </summary>
	public ListBuilder AddItem(ListItem item)
	{
		_items.Add(item);
		return this;
	}

	/// <summary>
	/// Adds multiple items to the list
	/// </summary>
	public ListBuilder AddItems(params string[] items)
	{
		foreach (var item in items)
			_items.Add(new ListItem(item));
		return this;
	}

	/// <summary>
	/// Adds multiple ListItems to the list
	/// </summary>
	public ListBuilder AddItems(IEnumerable<ListItem> items)
	{
		_items.AddRange(items);
		return this;
	}

	/// <summary>
	/// Sets the maximum number of visible items
	/// </summary>
	public ListBuilder MaxVisibleItems(int count)
	{
		_maxVisibleItems = count;
		return this;
	}

	/// <summary>
	/// Sets whether items are selectable
	/// </summary>
	public ListBuilder Selectable(bool selectable = true)
	{
		_isSelectable = selectable;
		return this;
	}

	/// <summary>
	/// Enables auto-adjust width based on content
	/// </summary>
	public ListBuilder AutoAdjustWidth(bool autoAdjust = true)
	{
		_autoAdjustWidth = autoAdjust;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	public ListBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	public ListBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	public ListBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	public ListBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	public ListBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	public ListBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	public ListBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	public ListBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the item activated event handler (Enter key or double-click)
	/// </summary>
	public ListBuilder OnItemActivated(EventHandler<ListItem> handler)
	{
		_itemActivatedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the item activated event handler with window access
	/// </summary>
	public ListBuilder OnItemActivated(WindowEventHandler<ListItem> handler)
	{
		_itemActivatedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selection changed event handler (index-based)
	/// </summary>
	public ListBuilder OnSelectionChanged(EventHandler<int> handler)
	{
		_selectionChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selection changed event handler with window access
	/// </summary>
	public ListBuilder OnSelectionChanged(WindowEventHandler<int> handler)
	{
		_selectionChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected item changed event handler
	/// </summary>
	public ListBuilder OnSelectedItemChanged(EventHandler<ListItem?> handler)
	{
		_selectedItemChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected item changed event handler with window access
	/// </summary>
	public ListBuilder OnSelectedItemChanged(WindowEventHandler<ListItem?> handler)
	{
		_selectedItemChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the list control
	/// </summary>
	public ListControl Build()
	{
		var list = new ListControl(_title)
		{
			MaxVisibleItems = _maxVisibleItems,
			IsSelectable = _isSelectable,
			AutoAdjustWidth = _autoAdjustWidth,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag
		};

		foreach (var item in _items)
			list.AddItem(item);

		// Attach standard handlers
		if (_itemActivatedHandler != null)
			list.ItemActivated += _itemActivatedHandler;
		if (_selectionChangedHandler != null)
			list.SelectedIndexChanged += _selectionChangedHandler;
		if (_selectedItemChangedHandler != null)
			list.SelectedItemChanged += _selectedItemChangedHandler;

		// Attach window-aware handlers
		if (_itemActivatedWithWindowHandler != null)
		{
			list.ItemActivated += (sender, item) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_itemActivatedWithWindowHandler(sender, item, window);
			};
		}
		if (_selectionChangedWithWindowHandler != null)
		{
			list.SelectedIndexChanged += (sender, idx) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectionChangedWithWindowHandler(sender, idx, window);
			};
		}
		if (_selectedItemChangedWithWindowHandler != null)
		{
			list.SelectedItemChanged += (sender, item) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedItemChangedWithWindowHandler(sender, item, window);
			};
		}

		return list;
	}

	/// <summary>
	/// Implicit conversion to ListControl
	/// </summary>
	public static implicit operator ListControl(ListBuilder builder) => builder.Build();
}
