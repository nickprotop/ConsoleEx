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
using SharpConsoleUI.Drivers;
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
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private Color? _focusedBackgroundColor;
	private Color? _focusedForegroundColor;
	private Color? _highlightBackgroundColor;
	private Color? _highlightForegroundColor;
	private EventHandler<ListItem>? _itemActivatedHandler;
	private EventHandler<int>? _selectionChangedHandler;
	private EventHandler<ListItem?>? _selectedItemChangedHandler;
	private WindowEventHandler<ListItem>? _itemActivatedWithWindowHandler;
	private WindowEventHandler<int>? _selectionChangedWithWindowHandler;
	private WindowEventHandler<ListItem?>? _selectedItemChangedWithWindowHandler;
	private EventHandler<string?>? _selectedValueChangedHandler;
	private WindowEventHandler<string?>? _selectedValueChangedWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;
	private EventHandler<EventArgs>? _checkedItemsChangedHandler;
	private bool _checkboxMode = false;
	private EventHandler<int>? _itemHoveredHandler;
	private WindowEventHandler<int>? _itemHoveredWithWindowHandler;
	private EventHandler<MouseEventArgs>? _mouseDoubleClickHandler;
	private WindowEventHandler<MouseEventArgs>? _mouseDoubleClickWithWindowHandler;
	private bool _hoverHighlightsItems = true;
	private bool _autoHighlightOnFocus = true;
	private int _mouseWheelScrollSpeed = 3;
	private bool _doubleClickActivates = true;
	private int _doubleClickThresholdMs = 500;

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
	/// Sets the sticky position
	/// </summary>
	public ListBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	public ListBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	public ListBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color
	/// </summary>
	public ListBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color
	/// </summary>
	public ListBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both foreground and background colors
	/// </summary>
	public ListBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the focused background color
	/// </summary>
	public ListBuilder WithFocusedBackgroundColor(Color color)
	{
		_focusedBackgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the focused foreground color
	/// </summary>
	public ListBuilder WithFocusedForegroundColor(Color color)
	{
		_focusedForegroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both focused foreground and background colors
	/// </summary>
	public ListBuilder WithFocusedColors(Color foreground, Color background)
	{
		_focusedForegroundColor = foreground;
		_focusedBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the highlight background color for selected items
	/// </summary>
	public ListBuilder WithHighlightBackgroundColor(Color color)
	{
		_highlightBackgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the highlight foreground color for selected items
	/// </summary>
	public ListBuilder WithHighlightForegroundColor(Color color)
	{
		_highlightForegroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both highlight foreground and background colors for selected items
	/// </summary>
	public ListBuilder WithHighlightColors(Color foreground, Color background)
	{
		_highlightForegroundColor = foreground;
		_highlightBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets whether mouse hover highlights items visually.
	/// Default: true.
	/// </summary>
	public ListBuilder WithHoverHighlighting(bool enabled = true)
	{
		_hoverHighlightsItems = enabled;
		return this;
	}

	/// <summary>
	/// Sets whether to auto-highlight on focus gain.
	/// When true, the control will highlight the selected item (or first item) when focused.
	/// Default: true.
	/// </summary>
	public ListBuilder WithAutoHighlightOnFocus(bool enabled = true)
	{
		_autoHighlightOnFocus = enabled;
		return this;
	}

	/// <summary>
	/// Sets the number of lines to scroll with mouse wheel.
	/// Default: 3.
	/// </summary>
	public ListBuilder WithMouseWheelScrollSpeed(int speed)
	{
		_mouseWheelScrollSpeed = Math.Max(1, speed);
		return this;
	}

	/// <summary>
	/// Sets whether double-click activates items and the threshold in milliseconds.
	/// Default: enabled with 500ms threshold.
	/// </summary>
	public ListBuilder WithDoubleClickActivation(bool enabled = true, int thresholdMs = 500)
	{
		_doubleClickActivates = enabled;
		_doubleClickThresholdMs = Math.Max(100, thresholdMs);
		return this;
	}

	/// <summary>
	/// Sets the item hovered event handler (mouse hover over items).
	/// The index is the hovered item index, or -1 if mouse left all items.
	/// </summary>
	public ListBuilder OnItemHovered(EventHandler<int> handler)
	{
		_itemHoveredHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the item hovered event handler with window access.
	/// The index is the hovered item index, or -1 if mouse left all items.
	/// </summary>
	public ListBuilder OnItemHovered(WindowEventHandler<int> handler)
	{
		_itemHoveredWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the mouse double-click event handler.
	/// Fires when an item is double-clicked (before ItemActivated).
	/// </summary>
	public ListBuilder OnMouseDoubleClick(EventHandler<MouseEventArgs> handler)
	{
		_mouseDoubleClickHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the mouse double-click event handler with window access.
	/// Fires when an item is double-clicked (before ItemActivated).
	/// </summary>
	public ListBuilder OnMouseDoubleClick(WindowEventHandler<MouseEventArgs> handler)
	{
		_mouseDoubleClickWithWindowHandler = handler;
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
	/// Sets the selected value changed event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the selected value changes</param>
	/// <returns>The builder for chaining</returns>
	public ListBuilder OnSelectedValueChanged(EventHandler<string?> handler)
	{
		_selectedValueChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected value changed event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ListBuilder OnSelectedValueChanged(WindowEventHandler<string?> handler)
	{
		_selectedValueChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the list receives focus</param>
	/// <returns>The builder for chaining</returns>
	public ListBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ListBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the list loses focus</param>
	/// <returns>The builder for chaining</returns>
	public ListBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public ListBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Enables checkbox mode — items display [x]/[ ] prefixes and Space toggles checked state.
	/// </summary>
	public ListBuilder WithCheckboxMode(bool enabled = true)
	{
		_checkboxMode = enabled;
		return this;
	}

	/// <summary>
	/// Sets the handler called when any item's checked state changes.
	/// </summary>
	public ListBuilder OnCheckedItemsChanged(EventHandler<EventArgs> handler)
	{
		_checkedItemsChangedHandler = handler;
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
			Tag = _tag,
			StickyPosition = _stickyPosition,
			// Apply interaction properties
			CheckboxMode = _checkboxMode,
			HoverHighlightsItems = _hoverHighlightsItems,
			AutoHighlightOnFocus = _autoHighlightOnFocus,
			MouseWheelScrollSpeed = _mouseWheelScrollSpeed,
			DoubleClickActivates = _doubleClickActivates,
			DoubleClickThresholdMs = _doubleClickThresholdMs
		};

		// Apply colors if specified
		if (_backgroundColor.HasValue)
			list.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue)
			list.ForegroundColor = _foregroundColor.Value;
		if (_focusedBackgroundColor.HasValue)
			list.FocusedBackgroundColor = _focusedBackgroundColor.Value;
		if (_focusedForegroundColor.HasValue)
			list.FocusedForegroundColor = _focusedForegroundColor.Value;
		if (_highlightBackgroundColor.HasValue)
			list.HighlightBackgroundColor = _highlightBackgroundColor.Value;
		if (_highlightForegroundColor.HasValue)
			list.HighlightForegroundColor = _highlightForegroundColor.Value;

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

		// Attach SelectedValueChanged handlers
		if (_selectedValueChangedHandler != null)
		{
			list.SelectedValueChanged += _selectedValueChangedHandler;
		}

		if (_selectedValueChangedWithWindowHandler != null)
		{
			list.SelectedValueChanged += (sender, value) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedValueChangedWithWindowHandler(sender, value, window);
			};
		}

		// Attach GotFocus handlers
		if (_gotFocusHandler != null)
		{
			list.GotFocus += _gotFocusHandler;
		}

		if (_gotFocusWithWindowHandler != null)
		{
			list.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach LostFocus handlers
		if (_lostFocusHandler != null)
		{
			list.LostFocus += _lostFocusHandler;
		}

		if (_lostFocusWithWindowHandler != null)
		{
			list.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach CheckedItemsChanged handler
		if (_checkedItemsChangedHandler != null)
		{
			list.CheckedItemsChanged += _checkedItemsChangedHandler;
		}

		// Attach ItemHovered handlers
		if (_itemHoveredHandler != null)
		{
			list.ItemHovered += _itemHoveredHandler;
		}

		if (_itemHoveredWithWindowHandler != null)
		{
			list.ItemHovered += (sender, index) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_itemHoveredWithWindowHandler(sender, index, window);
			};
		}

		// Attach MouseDoubleClick handlers
		if (_mouseDoubleClickHandler != null)
		{
			list.MouseDoubleClick += _mouseDoubleClickHandler;
		}

		if (_mouseDoubleClickWithWindowHandler != null)
		{
			list.MouseDoubleClick += (sender, args) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_mouseDoubleClickWithWindowHandler(sender, args, window);
			};
		}

		return list;
	}

	/// <summary>
	/// Implicit conversion to ListControl
	/// </summary>
	public static implicit operator ListControl(ListBuilder builder) => builder.Build();
}
