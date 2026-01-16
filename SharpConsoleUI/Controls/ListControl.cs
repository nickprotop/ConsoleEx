// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies the selection mode for a ListControl.
	/// </summary>
	public enum ListSelectionMode
	{
		/// <summary>
		/// Highlight and selection are merged. Only one index tracked.
		/// No [x] markers shown. Like TreeControl behavior.
		/// </summary>
		Simple,

		/// <summary>
		/// Highlight and selection are separate. Two indices tracked.
		/// [x] markers show selected items, [ ] shows highlighted item.
		/// Like DropdownControl behavior. (Default)
		/// </summary>
		Complex
	}

	/// <summary>
	/// A scrollable list control that supports selection, highlighting, and keyboard navigation.
	/// </summary>
	public class ListControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDOMPaintable
	{
		/// <summary>
		/// Creates a fluent builder for constructing a ListControl.
		/// </summary>
		/// <returns>A new ListBuilder instance.</returns>
		public static Builders.ListBuilder Create()
		{
			return new Builders.ListBuilder();
		}

		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _autoAdjustWidth = false;
		private Color? _backgroundColorValue;
		private int? _calculatedMaxVisibleItems;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private Color? _highlightBackgroundColorValue;
		private Color? _highlightForegroundColorValue;
		private bool _isEnabled = true;
		private bool _isSelectable = true;
		private ItemFormatterEvent? _itemFormatter;
		private List<ListItem> _items = new List<ListItem>();
		private DateTime _lastKeyTime = DateTime.MinValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private int? _maxVisibleItems = null;

		private string _searchText = string.Empty;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string _title = "List";
		private bool _visible = true;
		private int? _width;
		private int _scrollOffset = 0;
		private IContainer? _container;

		// Local state - controls own their selection/highlight state
		private int _selectedIndex = -1;
		private int _highlightedIndex = -1;

		// Mouse interaction state
		private int _hoveredIndex = -1;                        // Mouse hover tracking
		private DateTime _lastClickTime = DateTime.MinValue;   // Double-click detection
		private int _lastClickIndex = -1;                      // Double-click detection

		// Selection mode configuration
		private ListSelectionMode _selectionMode = ListSelectionMode.Complex;
		private bool _hoverHighlightsItems = true;
		private bool _autoHighlightOnFocus = true;
		private int _mouseWheelScrollSpeed = 3;
		private bool _doubleClickActivates = true;
		private int _doubleClickThresholdMs = 500;
		private bool _showSelectionMarkers = true;  // Show [x]/[ ] markers

		// Read-only helpers
		private int CurrentSelectedIndex => _selectedIndex;
		private int CurrentHighlightedIndex => _highlightedIndex;
		private int CurrentScrollOffset => _scrollOffset;

		// Helper to set scroll offset
		private void SetScrollOffset(int offset)
		{
			_scrollOffset = Math.Max(0, offset);
		}

		// Helper to find containing Window by traversing container hierarchy
		private Window? FindContainingWindow()
		{
			IContainer? current = Container;
			while (current != null)
			{
				if (current is Window window)
					return window;
				if (current is IWindowControl control)
					current = control.Container;
				else
					break;
			}
			return null;
		}

		// Calculate effective visible items for scroll logic
		private int GetEffectiveVisibleItems()
		{
			// Query actual visible height from container (accounts for clipping)
			int? actualVisibleHeight = Container?.GetVisibleHeightForControl(this);
			int actualVisibleItems = int.MaxValue;

			if (actualVisibleHeight.HasValue && actualVisibleHeight.Value > 0)
			{
				// Account for title bar and scroll indicator
				int titleHeight = string.IsNullOrEmpty(_title) ? 0 : 1;
				int scrollIndicatorHeight = 1; // Assume scroll indicator present when scrolling
				int availableForItems = Math.Max(1, actualVisibleHeight.Value - titleHeight - scrollIndicatorHeight);

				// Count how many items actually fit based on their line heights
				// Start from current scroll offset to match what's actually visible
				int scrollOffset = CurrentScrollOffset;
				actualVisibleItems = 0;
				int heightUsed = 0;

				for (int i = scrollOffset; i < _items.Count; i++)
				{
					int itemHeight = _items[i].Lines.Count;
					if (heightUsed + itemHeight <= availableForItems)
					{
						actualVisibleItems++;
						heightUsed += itemHeight;
					}
					else
					{
						break;
					}
				}

				actualVisibleItems = Math.Max(1, actualVisibleItems);
			}

			// If user set MaxVisibleItems, use the minimum of that and actual visible
			// This ensures scrolling works even when MaxVisibleItems > actual visible area
			if (_maxVisibleItems.HasValue)
				return Math.Min(_maxVisibleItems.Value, actualVisibleItems);

			// If we have actual visible height, use it
			if (actualVisibleItems < int.MaxValue)
				return actualVisibleItems;

			// Fall back to calculated max from last render
			if (_calculatedMaxVisibleItems.HasValue)
				return _calculatedMaxVisibleItems.Value;

			// Ultimate fallback
			return Math.Min(10, _items.Count);
		}

		/// <summary>
		/// Initializes a new ListControl with a title and string items.
		/// </summary>
		/// <param name="title">The title displayed at the top of the list.</param>
		/// <param name="items">The initial items to populate the list.</param>
		public ListControl(string? title, IEnumerable<string>? items)
		{
			_title = title ?? string.Empty;
			if (items != null)
			{
				foreach (var item in items)
				{
					_items.Add(new ListItem(item));
				}
			}
		}

		/// <summary>
		/// Initializes a new ListControl with string items and no title.
		/// </summary>
		/// <param name="items">The initial items to populate the list.</param>
		public ListControl(IEnumerable<string>? items)
		{
			_title = string.Empty;
			if (items != null)
			{
				foreach (var item in items)
				{
					_items.Add(new ListItem(item));
				}
			}
		}

		/// <summary>
		/// Initializes a new ListControl with a title and ListItem objects.
		/// </summary>
		/// <param name="title">The title displayed at the top of the list.</param>
		/// <param name="items">The initial ListItem objects to populate the list.</param>
		public ListControl(string? title, IEnumerable<ListItem>? items)
		{
			_title = title ?? string.Empty;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		/// <summary>
		/// Initializes a new ListControl with ListItem objects and no title.
		/// </summary>
		/// <param name="items">The initial ListItem objects to populate the list.</param>
		public ListControl(IEnumerable<ListItem>? items)
		{
			_title = string.Empty;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		/// <summary>
		/// Initializes a new empty ListControl with no title.
		/// </summary>
		public ListControl()
		{
			_title = string.Empty;
		}

		/// <summary>
		/// Initializes a new empty ListControl with a title.
		/// </summary>
		/// <param name="title">The title displayed at the top of the list.</param>
		public ListControl(string title)
		{
			_title = title;
		}

		/// <summary>
		/// Delegate for custom item formatting.
		/// </summary>
		/// <param name="item">The item to format.</param>
		/// <param name="isSelected">Whether the item is currently selected.</param>
		/// <param name="hasFocus">Whether the list control has focus.</param>
		/// <returns>The formatted string to display.</returns>
		public delegate string ItemFormatterEvent(ListItem item, bool isSelected, bool hasFocus);

		/// <summary>
		/// Occurs when the selected index changes.
		/// </summary>
		public event EventHandler<int>? SelectedIndexChanged;

		/// <summary>
		/// Occurs when the selected item changes.
		/// </summary>
		public event EventHandler<ListItem?>? SelectedItemChanged;

		/// <summary>
		/// Occurs when the selected value (text) changes.
		/// </summary>
		public event EventHandler<string?>? SelectedValueChanged;

		/// <summary>
		/// Occurs when an item is activated (Enter or double-click).
		/// </summary>
		public event EventHandler<ListItem>? ItemActivated;

		/// <summary>
		/// Occurs when the highlighted index changes due to arrow key navigation.
		/// Fires before the item is selected/activated. Use this for real-time
		/// preview updates as the user browses through items.
		/// </summary>
		public event EventHandler<int>? HighlightChanged;

		/// <summary>
		/// Occurs when the control is clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Occurs when the mouse enters the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <summary>
		/// Occurs when the mouse leaves the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Occurs when the mouse moves over the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <summary>
		/// Occurs when an item is hovered by the mouse.
		/// The index is the hovered item index, or -1 if mouse left all items.
		/// </summary>
		public event EventHandler<int>? ItemHovered;

		/// <summary>
		/// Occurs when an item is double-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Gets the actual rendered height in lines.
		/// </summary>
		public int? ActualHeight
		{
			get
			{
				// Calculate based on content
				bool hasTitle = !string.IsNullOrEmpty(_title);
				int titleHeight = hasTitle ? 1 : 0;
				int visibleItems = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? Math.Min(10, _items.Count);
				int itemsHeight = 0;
				int scrollOffset = CurrentScrollOffset;
				for (int i = 0; i < Math.Min(visibleItems, _items.Count - scrollOffset); i++)
				{
					int itemIndex = i + scrollOffset;
					if (itemIndex < _items.Count)
						itemsHeight += _items[itemIndex].Lines.Count;
				}
				bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + visibleItems < _items.Count;
				return titleHeight + itemsHeight + (hasScrollIndicator ? 1 : 0) + _margin.Top + _margin.Bottom;
			}
		}

		/// <summary>
		/// Gets the actual rendered width in characters.
		/// </summary>
		public int? ActualWidth
		{
			get
			{
				// Calculate based on content
				int maxItemWidth = 0;
				foreach (var item in _items)
				{
					int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
					if (itemLength > maxItemWidth) maxItemWidth = itemLength;
				}

				// Calculate indicator space: only needed in Complex mode with markers
				int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;
				int titleLength = string.IsNullOrEmpty(_title) ? 0 : AnsiConsoleHelper.StripSpectreLength(_title) + 5;

				int width = _width ?? Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);
				return width + _margin.Left + _margin.Right;
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set
			{
				_horizontalAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set
			{
				_verticalAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the control automatically adjusts its width to fit content.
		/// </summary>
		public bool AutoAdjustWidth
		{
			get => _autoAdjustWidth;
			set
			{
				_autoAdjustWidth = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the list.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set => _container = value;
		}

		/// <summary>
		/// Gets or sets the background color when the list has focus.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_focusedBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the list has focus.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of list items.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
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
		/// Gets or sets the background color for highlighted items.
		/// </summary>
		public Color HighlightBackgroundColor
		{
			get => _highlightBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonSelectedBackgroundColor ?? Color.DarkBlue;
			set
			{
				_highlightBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color for highlighted items.
		/// </summary>
		public Color HighlightForegroundColor
		{
			get => _highlightForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonSelectedForegroundColor ?? Color.White;
			set
			{
				_highlightForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the list is enabled for user interaction.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether items can be selected in the list.
		/// </summary>
		public bool IsSelectable
		{
			get => _isSelectable;
			set
			{
				_isSelectable = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets a custom formatter for rendering list items.
		/// </summary>
		public ItemFormatterEvent? ItemFormatter
		{
			get => _itemFormatter;
			set
			{
				_itemFormatter = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the collection of items in the list.
		/// </summary>
		public List<ListItem> Items
		{
			get => _items;
			set
			{
				_items = value;
				// Adjust selection if out of bounds
				int currentSel = CurrentSelectedIndex;
				if (currentSel >= _items.Count)
				{
					int newSel = _items.Count > 0 ? 0 : -1;
					int oldIndex = _selectedIndex;
				_selectedIndex = newSel;
				if (oldIndex != _selectedIndex)
				{
					SelectedIndexChanged?.Invoke(this, _selectedIndex);
					SelectedItemChanged?.Invoke(this, SelectedItem);
					SelectedValueChanged?.Invoke(this, SelectedValue);
				}
				}
				Container?.Invalidate(true);
			}
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
		/// Gets or sets the maximum number of items visible at once. If null, calculated from available height.
		/// </summary>
		public int? MaxVisibleItems
		{
			get => _maxVisibleItems;
			set
			{
				_maxVisibleItems = value.HasValue ? Math.Max(1, value.Value) : null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the index of the currently selected item. -1 if no item is selected.
		/// </summary>
		public int SelectedIndex
		{
			get => CurrentSelectedIndex;
			set
			{
				if (!_isSelectable)
					return;

				int oldIndex = CurrentSelectedIndex;
				var oldItem = SelectedItem;
				var oldValue = SelectedValue;

				// Early return if no change
				if (oldIndex == value)
					return;

				// Validate range
				if (value < -1 || value >= _items.Count)
					return;

				_selectedIndex = value;

				// Sync highlight in Simple mode
				if (_selectionMode == ListSelectionMode.Simple)
				{
					_highlightedIndex = value;
				}

				// Fire events ONCE
				SelectedIndexChanged?.Invoke(this, value);

				if (SelectedItem != oldItem)
					SelectedItemChanged?.Invoke(this, SelectedItem);

				if (SelectedValue != oldValue)
					SelectedValueChanged?.Invoke(this, SelectedValue);

				// Ensure selected item is visible
				if (value >= 0)
				{
					EnsureSelectedItemVisible();
				}

				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the index of the currently highlighted item (for arrow key navigation). -1 if no item is highlighted.
		/// </summary>
		public int HighlightedIndex
		{
			get => _highlightedIndex;
		}

		/// <summary>
		/// Gets the index of the currently hovered item (mouse cursor). -1 if no item is hovered.
		/// </summary>
		public int HoveredIndex
		{
			get => _hoveredIndex;
		}

		/// <summary>
		/// Gets or sets the selection mode (Simple or Complex).
		/// Simple: Highlight and selection are merged (like TreeControl).
		/// Complex: Highlight and selection are separate (like DropdownControl).
		/// Default: Complex (backward compatible).
		/// </summary>
		public ListSelectionMode SelectionMode
		{
			get => _selectionMode;
			set
			{
				_selectionMode = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether mouse hover highlights items visually.
		/// Default: true.
		/// </summary>
		public bool HoverHighlightsItems
		{
			get => _hoverHighlightsItems;
			set
			{
				_hoverHighlightsItems = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to auto-highlight on focus gain.
		/// When true, the control will highlight the selected item (or first item) when focused.
		/// Default: true (fixes UX issue where focus had no visual feedback).
		/// </summary>
		public bool AutoHighlightOnFocus
		{
			get => _autoHighlightOnFocus;
			set
			{
				_autoHighlightOnFocus = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of lines to scroll with mouse wheel.
		/// Default: 3.
		/// </summary>
		public int MouseWheelScrollSpeed
		{
			get => _mouseWheelScrollSpeed;
			set
			{
				_mouseWheelScrollSpeed = Math.Max(1, value);
			}
		}

		/// <summary>
		/// Gets or sets whether double-click activates items.
		/// Default: true.
		/// </summary>
		public bool DoubleClickActivates
		{
			get => _doubleClickActivates;
			set
			{
				_doubleClickActivates = value;
			}
		}

		/// <summary>
		/// Gets or sets the double-click threshold in milliseconds.
		/// Default: 500.
		/// </summary>
		public int DoubleClickThresholdMs
		{
			get => _doubleClickThresholdMs;
			set
			{
				_doubleClickThresholdMs = Math.Max(100, value);
			}
		}

		/// <summary>
		/// Gets or sets the currently selected item.
		/// </summary>
		public ListItem? SelectedItem
		{
			get { int idx = CurrentSelectedIndex; return idx >= 0 && idx < _items.Count ? _items[idx] : null; }
			set
			{
				if (!_isSelectable || value == null)
				{
					SelectedIndex = -1;
					return;
				}

				int index = _items.IndexOf(value);
				if (index >= 0)
				{
					SelectedIndex = index;
				}
			}
		}

		/// <summary>
		/// Gets or sets the text of the currently selected item.
		/// </summary>
		public string? SelectedValue
		{
			get { int idx = CurrentSelectedIndex; return idx >= 0 && idx < _items.Count ? _items[idx].Text : null; }
			set
			{
				if (!_isSelectable || value == null)
				{
					SelectedIndex = -1;
					return;
				}

				for (int i = 0; i < _items.Count; i++)
				{
					if (_items[i].Text == value)
					{
						SelectedIndex = i;
						break;
					}
				}
			}
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the list items as simple strings.
		/// </summary>
		public List<string> StringItems
		{
			get => _items.Select(i => i.Text).ToList();
			set
			{
				_items = value.Select(text => new ListItem(text)).ToList();
				int currentSel = CurrentSelectedIndex;
				if (currentSel >= _items.Count)
				{
					int newSel = _items.Count > 0 ? 0 : -1;
					int oldIndex = _selectedIndex;
				_selectedIndex = newSel;
				if (oldIndex != _selectedIndex)
				{
					SelectedIndexChanged?.Invoke(this, _selectedIndex);
					SelectedItemChanged?.Invoke(this, SelectedItem);
					SelectedValueChanged?.Invoke(this, SelectedValue);
				}
				}
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the title displayed at the top of the list.
		/// </summary>
		public string Title
		{
			get => _title;
			set
			{
				_title = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set
			{
				_visible = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
				if (_width != validatedValue)
				{
					_width = validatedValue;
					Container?.Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Adds a ListItem to the list.
		/// </summary>
		/// <param name="item">The item to add.</param>
		public void AddItem(ListItem item)
		{
			_items.Add(item);
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a new item with text, optional icon, and icon color.
		/// </summary>
		/// <param name="text">The text of the item.</param>
		/// <param name="icon">Optional icon to display.</param>
		/// <param name="iconColor">Optional color for the icon.</param>
		public void AddItem(string text, string? icon = null, Color? iconColor = null)
		{
			AddItem(new ListItem(text, icon, iconColor));
		}

		/// <summary>
		/// Adds a new item with the specified text.
		/// </summary>
		/// <param name="text">The text of the item.</param>
		public void AddItem(string text)
		{
			AddItem(new ListItem(text));
		}

		/// <summary>
		/// Removes all items from the list.
		/// </summary>
		public void ClearItems()
		{
			_items.Clear();

			// Clear state via services (single source of truth)
			_selectedIndex = -1;
		_highlightedIndex = -1;
			SetScrollOffset(0);

			Container?.Invalidate(true);

			if (_isSelectable)
			{
				SelectedIndexChanged?.Invoke(this, -1);
				SelectedItemChanged?.Invoke(this, null);
				SelectedValueChanged?.Invoke(this, null);
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			// Setting Container to null triggers unsubscription via property setter
			Container = null;

			// Clear event handlers to prevent memory leaks
			HighlightChanged = null;
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// Calculate content size directly
			bool hasTitle = !string.IsNullOrEmpty(_title);
			int titleHeight = hasTitle ? 1 : 0;
			int visibleItems = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? Math.Min(10, _items.Count);
			int itemsHeight = 0;
			int scrollOffset = CurrentScrollOffset;

			for (int i = 0; i < Math.Min(visibleItems, _items.Count - scrollOffset); i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex < _items.Count)
					itemsHeight += _items[itemIndex].Lines.Count;
			}

			bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + visibleItems < _items.Count;
			int height = titleHeight + itemsHeight + (hasScrollIndicator ? 1 : 0) + _margin.Top + _margin.Bottom;

			// Calculate indicator space: only needed in Complex mode with markers
			int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			int titleLength = string.IsNullOrEmpty(_title) ? 0 : AnsiConsoleHelper.StripSpectreLength(_title) + 5;
			int width = _width ?? Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);
			width += _margin.Left + _margin.Right;

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
			if (!_isEnabled || !_hasFocus)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			// If control is not selectable, we just handle scrolling
			if (!_isSelectable)
			{
				int scrollOffset = CurrentScrollOffset;
				switch (key.Key)
				{
					case ConsoleKey.DownArrow:
						if (scrollOffset < _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10))
						{
							SetScrollOffset(scrollOffset + 1);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.UpArrow:
						if (scrollOffset > 0)
						{
							SetScrollOffset(scrollOffset - 1);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageDown:
						int pageSize = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10;
						if (scrollOffset < _items.Count - pageSize)
						{
							SetScrollOffset(Math.Min(_items.Count - pageSize, scrollOffset + pageSize));
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageUp:
						if (scrollOffset > 0)
						{
							SetScrollOffset(Math.Max(0, scrollOffset - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10)));
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.Home:
						if (scrollOffset > 0)
						{
							SetScrollOffset(0);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.End:
						int availableItems = _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10);
						if (scrollOffset < availableItems && availableItems > 0)
						{
							SetScrollOffset(availableItems);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					default:
						return false;
				}
			}

			// If control is selectable, we handle both scrolling and selection
			int highlightedIndex = CurrentHighlightedIndex;
			switch (key.Key)
			{
				case ConsoleKey.DownArrow:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_selectionMode == ListSelectionMode.Simple)
					{
						// Simple mode: Move selection + highlight together
						if (_selectedIndex < _items.Count - 1)
						{
							SelectedIndex = _selectedIndex + 1;
							_highlightedIndex = _selectedIndex;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					else
					{
						// Complex mode: Move highlight only
						if (highlightedIndex < _items.Count - 1)
						{
							_highlightedIndex = highlightedIndex + 1;
							HighlightChanged?.Invoke(this, _highlightedIndex);
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.UpArrow:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_selectionMode == ListSelectionMode.Simple)
					{
						// Simple mode: Move selection + highlight together
						if (_selectedIndex > 0)
						{
							SelectedIndex = _selectedIndex - 1;
							_highlightedIndex = _selectedIndex;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					else
					{
						// Complex mode: Move highlight only
						if (highlightedIndex > 0)
						{
							_highlightedIndex = highlightedIndex - 1;
							HighlightChanged?.Invoke(this, _highlightedIndex);
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.Enter:
					if (highlightedIndex >= 0 && highlightedIndex < _items.Count)
					{
						if (_selectionMode == ListSelectionMode.Simple)
						{
							// Simple mode: Already selected (highlight = selection), just activate
							var item = _items[highlightedIndex];
							if (item.IsEnabled)
							{
								ItemActivated?.Invoke(this, item);
							}
						}
						else
						{
							// Complex mode: Two-step Enter
							// First Enter: If highlight != selection, commit to selection (no activate)
							// Second Enter: If highlight == selection, activate

							if (_selectedIndex != highlightedIndex)
							{
								// First Enter: Commit highlight to selection (browse → selected)
								SelectedIndex = highlightedIndex;
								// Don't fire ItemActivated yet!
							}
							else
							{
								// Second Enter: Already selected, now activate
								var item = _items[highlightedIndex];
								if (item.IsEnabled)
								{
									ItemActivated?.Invoke(this, item);
								}
							}
						}
						return true;
					}
					else if (_items.Count > 0)
					{
						// Nothing highlighted: First Enter initializes highlight
						if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
						{
							_highlightedIndex = _selectedIndex;
						}
						else
						{
							_highlightedIndex = 0;  // Highlight first item
						}
						HighlightChanged?.Invoke(this, _highlightedIndex);
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.Home:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_items.Count > 0)
					{
						_highlightedIndex = 0;
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_items.Count > 0)
					{
						_highlightedIndex = _items.Count - 1;
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (highlightedIndex > 0)
					{
						_highlightedIndex = Math.Max(0, highlightedIndex - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1));
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (highlightedIndex < _items.Count - 1)
					{
						_highlightedIndex = Math.Min(_items.Count - 1, highlightedIndex + (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1));
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				default:
					// Check if it's a letter/number key for quick selection
					if (!char.IsControl(key.KeyChar))
					{
						// Check if this is part of a search sequence or new search
						if ((DateTime.Now - _lastKeyTime) > _searchResetDelay)
						{
							_searchText = key.KeyChar.ToString();
						}
						else
						{
							_searchText += key.KeyChar;
						}

						_lastKeyTime = DateTime.Now;

						// Search for items starting with the search text
						for (int i = 0; i < _items.Count; i++)
						{
							if (_items[i].Text.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase))
							{
								_highlightedIndex = i;
							HighlightChanged?.Invoke(this, _highlightedIndex);
								EnsureHighlightedItemVisible();
								Container?.Invalidate(true);
								return true;
							}
						}
					}
					return false;
			}
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Calculate indicator space: only needed in Complex mode with markers
			int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;

			// Calculate max item width
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			// Calculate list width
			int listWidth;
			if (_width.HasValue)
			{
				listWidth = _width.Value;
			}
			else if (_horizontalAlignment == HorizontalAlignment.Stretch)
			{
				listWidth = constraints.MaxWidth - _margin.Left - _margin.Right;
			}
			else
			{
				int titleLength = string.IsNullOrEmpty(_title) ? 0 : AnsiConsoleHelper.StripSpectreLength(_title) + 5;
				listWidth = Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);
				listWidth = Math.Max(listWidth, 40);
			}

			if (_autoAdjustWidth)
			{
				int contentWidth = 0;
				foreach (var item in _items)
				{
					int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
					contentWidth = Math.Max(contentWidth, itemLength);
				}
				listWidth = Math.Max(listWidth, contentWidth + indicatorSpace + 4);
			}

			int width = listWidth + _margin.Left + _margin.Right;

			// Calculate height
			bool hasTitle = !string.IsNullOrEmpty(_title);
			int titleHeight = hasTitle ? 1 : 0;
			int scrollOffset = CurrentScrollOffset;

			int effectiveMaxVisibleItems;
			if (_maxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = _maxVisibleItems.Value;
			}
			else if (_verticalAlignment == VerticalAlignment.Fill)
			{
				// Check if we have unbounded constraints
				bool isUnbounded = constraints.MaxHeight >= int.MaxValue / 2;

				if (isUnbounded)
				{
					// With unbounded constraints and Fill alignment, return a reasonable default
					// instead of trying to fit all items. The parent container should provide
					// proper bounded constraints during the arrange phase.
					effectiveMaxVisibleItems = Math.Min(10, _items.Count);
				}
				else
				{
					// When VerticalAlignment.Fill with bounded constraints, use available height
					int availableContentHeight = constraints.MaxHeight - titleHeight - _margin.Top - _margin.Bottom - 1;
					effectiveMaxVisibleItems = 0;
					int heightUsed = 0;
					for (int i = scrollOffset; i < _items.Count; i++)
					{
						int itemHeight = _items[i].Lines.Count;
						if (heightUsed + itemHeight <= availableContentHeight)
						{
							effectiveMaxVisibleItems++;
							heightUsed += itemHeight;
						}
						else break;
					}
					effectiveMaxVisibleItems = Math.Max(1, effectiveMaxVisibleItems);
				}
			}
			else
			{
				effectiveMaxVisibleItems = Math.Min(10, _items.Count);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;
			
			int itemsHeight = 0;
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - scrollOffset);
			for (int i = 0; i < itemsToShow; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex < _items.Count)
					itemsHeight += _items[itemIndex].Lines.Count;
			}

			bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + itemsToShow < _items.Count;
			int height = titleHeight + itemsHeight + (hasScrollIndicator ? 1 : 0) + _margin.Top + _margin.Bottom;

			// VerticalAlignment.Fill is handled during arrangement, not measurement.
			// Measurement should return actual content height, not constraints.MaxHeight.
			// This prevents integer overflow when measured with unbounded height.

			var result = new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);

			return result;
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			Color backgroundColor;
			Color foregroundColor;
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			// Determine colors based on enabled/focused state
			if (!_isEnabled)
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			else if (_hasFocus)
			{
				backgroundColor = FocusedBackgroundColor;
				foregroundColor = FocusedForegroundColor;
			}
			else
			{
				backgroundColor = BackgroundColor;
				foregroundColor = ForegroundColor;
			}

			// Calculate indicator space: only needed in Complex mode with markers
			int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;
			int listWidth = bounds.Width - _margin.Left - _margin.Right;
			if (listWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int currentY = startY;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground);

			bool hasTitle = !string.IsNullOrEmpty(_title);
			int scrollOffset = CurrentScrollOffset;
			int selectedIndex = CurrentSelectedIndex;
			int highlightedIndex = CurrentHighlightedIndex;

			// Note: Highlight initialization moved to SetFocus to avoid firing events during render

			// Render title
			if (hasTitle && currentY < bounds.Bottom)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				{
					// Fill left margin
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
					}

					string titleBarContent = _title;
					int titleLen = AnsiConsoleHelper.StripSpectreLength(titleBarContent);
					if (titleLen < listWidth)
					{
						titleBarContent += new string(' ', listWidth - titleLen);
					}

					var titleAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(titleBarContent, listWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? "";
					var titleCells = AnsiParser.Parse(titleAnsi, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(startX, currentY, titleCells, clipRect);

					// Fill right margin
					if (_margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
					}
				}
				currentY++;
			}

			// Calculate effective visible items
			int availableContentHeight = bounds.Height - _margin.Top - _margin.Bottom - (hasTitle ? 1 : 0) - 1;
			int effectiveMaxVisibleItems;

			if (_maxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = _maxVisibleItems.Value;
			}
			else
			{
				effectiveMaxVisibleItems = 0;
				int heightUsed = 0;
				for (int i = scrollOffset; i < _items.Count; i++)
				{
					int itemHeight = _items[i].Lines.Count;
					if (heightUsed + itemHeight <= availableContentHeight)
					{
						effectiveMaxVisibleItems++;
						heightUsed += itemHeight;
					}
					else break;
				}
				effectiveMaxVisibleItems = Math.Max(1, effectiveMaxVisibleItems);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;
			
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - scrollOffset);

			// Render each visible item
			for (int i = 0; i < itemsToShow && currentY < bounds.Bottom - _margin.Bottom - 1; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex >= _items.Count) break;

				List<string> itemLines = _items[itemIndex].Lines;

				for (int lineIndex = 0; lineIndex < itemLines.Count && currentY < bounds.Bottom - _margin.Bottom - 1; lineIndex++)
				{
					if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					{
						// Fill left margin
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
						}

						string lineText = itemLines[lineIndex];
						if (lineIndex == 0 && _itemFormatter != null)
						{
							lineText = _itemFormatter(_items[itemIndex], itemIndex == selectedIndex, _hasFocus);
						}

						// Truncate if necessary
						int maxTextWidth = listWidth - (indicatorSpace + 2);
						if (AnsiConsoleHelper.StripSpectreLength(lineText) > maxTextWidth && maxTextWidth > 3)
						{
							lineText = lineText.Substring(0, Math.Max(0, maxTextWidth - 3)) + "...";
						}

						// Determine colors for this item
						// Priority: Disabled > Hovered > Highlighted > Selected > Normal
						Color itemBg, itemFg;
						bool isHovered = (itemIndex == _hoveredIndex);

						if (!IsEnabled)
						{
							itemBg = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
							itemFg = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
						}
						else if (isHovered && _hoverHighlightsItems && _hasFocus)
						{
							// Hover takes precedence when control has focus
							// Use theme hover colors if available, otherwise fall back to highlight colors
							var theme = Container?.GetConsoleWindowSystem?.Theme;
							itemBg = theme?.ListHoverBackgroundColor ?? HighlightBackgroundColor;
							itemFg = theme?.ListHoverForegroundColor ?? HighlightForegroundColor;
						}
						else if (_isSelectable && itemIndex == highlightedIndex && _hasFocus)
						{
							itemBg = HighlightBackgroundColor;
							itemFg = HighlightForegroundColor;
						}
						else if (_isSelectable && itemIndex == highlightedIndex && !_hasFocus)
						{
							itemBg = Container?.GetConsoleWindowSystem?.Theme?.ListUnfocusedHighlightBackgroundColor ?? HighlightBackgroundColor;
							itemFg = Container?.GetConsoleWindowSystem?.Theme?.ListUnfocusedHighlightForegroundColor ?? Color.Grey;
						}
						else
						{
							itemBg = backgroundColor;
							itemFg = foregroundColor;
						}

						// Build item content with selection markers
						string selectionIndicator = "";
						if (_isSelectable && lineIndex == 0)
						{
							// Show markers only in Complex mode
							if (_selectionMode == ListSelectionMode.Complex && _showSelectionMarkers)
							{
								if (itemIndex == selectedIndex)
									selectionIndicator = "[ x ] ";
								else if (itemIndex == highlightedIndex && _hasFocus)
									selectionIndicator = "[ > ] ";
								else
									selectionIndicator = "     ";
							}
							// In Simple mode, no markers
						}
						else if (_isSelectable && lineIndex > 0)
						{
							// Continuation lines: add spacing if Complex mode
							if (_selectionMode == ListSelectionMode.Complex && _showSelectionMarkers)
							{
								selectionIndicator = "     ";
							}
						}

						string itemContent;
						if (lineIndex == 0 && _items[itemIndex].Icon != null)
						{
							string iconText = _items[itemIndex].Icon!;
							Color iconColor = _items[itemIndex].IconColor ?? itemFg;
							string iconMarkup = $"[{iconColor.ToMarkup()}]{iconText}[/] ";
							int iconVisibleLength = AnsiConsoleHelper.StripSpectreLength(iconText) + 1;
							itemContent = selectionIndicator + iconMarkup + lineText;
							int visibleTextLength = selectionIndicator.Length + iconVisibleLength + AnsiConsoleHelper.StripSpectreLength(lineText);
							int paddingNeeded = Math.Max(0, listWidth - visibleTextLength);
							if (paddingNeeded > 0) itemContent += new string(' ', paddingNeeded);
						}
						else
						{
							string indent = "";
							if (lineIndex > 0 && _items[itemIndex].Icon != null)
							{
								string iconText = _items[itemIndex].Icon!;
								int iconWidth = AnsiConsoleHelper.StripSpectreLength(iconText) + 1;
								indent = new string(' ', iconWidth);
							}
							itemContent = selectionIndicator + indent + lineText;
							int visibleTextLength = selectionIndicator.Length + indent.Length + AnsiConsoleHelper.StripSpectreLength(lineText);
							int paddingNeeded = Math.Max(0, listWidth - visibleTextLength);
							if (paddingNeeded > 0) itemContent += new string(' ', paddingNeeded);
						}

						var itemAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(itemContent, listWidth, 1, false, itemBg, itemFg).FirstOrDefault() ?? "";
						var itemCells = AnsiParser.Parse(itemAnsi, itemFg, itemBg);
						buffer.WriteCellsClipped(startX, currentY, itemCells, clipRect);

						// Fill right margin
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
						}
					}
					currentY++;
				}
			}

			// Fill empty lines if VerticalAlignment.Fill
			if (_verticalAlignment == VerticalAlignment.Fill)
			{
				int scrollIndicatorY = bounds.Bottom - _margin.Bottom - 1;
				while (currentY < scrollIndicatorY)
				{
					if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					{
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
						}
						buffer.FillRect(new LayoutRect(startX, currentY, listWidth, 1), ' ', foregroundColor, backgroundColor);
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
						}
					}
					currentY++;
				}
			}

			// Render scroll indicators
			bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + itemsToShow < _items.Count;
			if (hasScrollIndicator && currentY < bounds.Bottom - _margin.Bottom)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				{
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
					}

					string scrollIndicator = "";
					scrollIndicator += scrollOffset > 0 ? "▲" : " ";
					int scrollPadding = listWidth - 2;
					if (scrollPadding > 0) scrollIndicator += new string(' ', scrollPadding);
					scrollIndicator += (scrollOffset + itemsToShow < _items.Count) ? "▼" : " ";

					var scrollAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(scrollIndicator, listWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? "";
					var scrollCells = AnsiParser.Parse(scrollAnsi, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(startX, currentY, scrollCells, clipRect);

					if (_margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
					}
				}
				currentY++;
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, currentY, foregroundColor, windowBackground);
		}

		#endregion

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

			if (focus && !hadFocus)
			{
				// Initialize highlight on focus gain
				if (_autoHighlightOnFocus && _highlightedIndex == -1)
				{
					if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
					{
						_highlightedIndex = _selectedIndex;
					}
					else if (_items.Count > 0)
					{
						_highlightedIndex = 0;  // Highlight first item
					}

					if (_highlightedIndex >= 0)
					{
						HighlightChanged?.Invoke(this, _highlightedIndex);
					}
				}

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
				// Reset highlight to selection on focus loss (DropdownControl pattern)
				if (_selectionMode == ListSelectionMode.Complex)
				{
					_highlightedIndex = _selectedIndex;
				}

				// Clear hover state
				if (_hoveredIndex != -1)
				{
					_hoveredIndex = -1;
					ItemHovered?.Invoke(this, -1);
				}

				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			Container?.Invalidate(true);

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		// IMouseAwareControl implementation
		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Don't process if already handled
			if (args.Handled)
				return false;

			// Handle mouse leave - clear hover state
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (_hoveredIndex != -1)
				{
					_hoveredIndex = -1;
					ItemHovered?.Invoke(this, -1);
					Container?.Invalidate(true);
				}
				MouseLeave?.Invoke(this, args);
				return true;
			}

			// Calculate which item the mouse is over
			int titleOffset = string.IsNullOrEmpty(_title) ? 0 : 1;
			int relativeY = args.Position.Y - titleOffset;
			int hoveredIndex = -1;

			// Get visible height to properly calculate item index
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			if (relativeY >= 0 && relativeY < Math.Min(_items.Count, effectiveMaxVisibleItems))
			{
				hoveredIndex = _scrollOffset + relativeY;
				if (hoveredIndex >= _items.Count)
					hoveredIndex = -1;
			}

			// Update hover state (visual feedback only, doesn't change highlight/selection)
			if (_hoverHighlightsItems && hoveredIndex != _hoveredIndex)
			{
				_hoveredIndex = hoveredIndex;
				ItemHovered?.Invoke(this, hoveredIndex);
				Container?.Invalidate(true);
			}

			// Handle mouse wheel scrolling (no impact on selection/highlight)
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				if (_scrollOffset > 0)
				{
					_scrollOffset = Math.Max(0, _scrollOffset - _mouseWheelScrollSpeed);
					Container?.Invalidate(true);
				}
				args.Handled = true;
				return true;
			}
			else if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int maxScroll = Math.Max(0, _items.Count - effectiveMaxVisibleItems);
				if (_scrollOffset < maxScroll)
				{
					_scrollOffset = Math.Min(maxScroll, _scrollOffset + _mouseWheelScrollSpeed);
					Container?.Invalidate(true);
				}
				args.Handled = true;
				return true;
			}

			// Handle double-click event from driver
			if (args.HasFlag(MouseFlags.Button1DoubleClicked) && _doubleClickActivates)
			{
				if (relativeY >= 0 && relativeY < _items.Count)
				{
					int clickedIndex = _scrollOffset + relativeY;
					if (clickedIndex >= 0 && clickedIndex < _items.Count)
					{
						// Commit highlight to selection
						if (_selectedIndex != clickedIndex)
						{
							SelectedIndex = clickedIndex;
						}

						MouseDoubleClick?.Invoke(this, args);

						// Fire ItemActivated
						var item = _items[clickedIndex];
						if (item.IsEnabled)
						{
							ItemActivated?.Invoke(this, item);
						}

						Container?.Invalidate(true);
						args.Handled = true;
						return true;
					}
				}
			}

			// Handle mouse clicks - set focus, select item, detect double-click
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				// Set focus on click
				if (!HasFocus && CanFocusWithMouse)
				{
					SetFocus(true, FocusReason.Mouse);
				}

				if (relativeY >= 0 && relativeY < _items.Count)
				{
					int clickedIndex = _scrollOffset + relativeY;
					if (clickedIndex >= 0 && clickedIndex < _items.Count)
					{
						// Detect double-click
						var now = DateTime.UtcNow;
						var timeSince = (now - _lastClickTime).TotalMilliseconds;
						bool isDoubleClick = _doubleClickActivates &&
											 clickedIndex == _lastClickIndex &&
											 timeSince <= _doubleClickThresholdMs;

						_lastClickTime = now;
						_lastClickIndex = clickedIndex;

						// ✅ FIX: Behavior depends on SelectionMode
						if (_selectionMode == ListSelectionMode.Simple)
						{
							// Simple mode: Merged state, set both
							SelectedIndex = clickedIndex;
							_highlightedIndex = clickedIndex;
						}
						else
						{
							// Complex mode: Click is browsing action (like arrow keys)
							// Clear any existing selection and highlight clicked item
							if (_selectedIndex != -1)
							{
								_selectedIndex = -1;
								SelectedIndexChanged?.Invoke(this, -1);
							}
							_highlightedIndex = clickedIndex;
							HighlightChanged?.Invoke(this, clickedIndex);
						}

						// Double click: Commit to selection and activate
						if (isDoubleClick)
						{
							// Commit highlight to selection
							if (_selectedIndex != clickedIndex)
							{
								SelectedIndex = clickedIndex;
							}

							MouseDoubleClick?.Invoke(this, args);

							// Fire ItemActivated (like Enter key)
							var item = _items[clickedIndex];
							if (item.IsEnabled)
							{
								ItemActivated?.Invoke(this, item);
							}
						}
						else
						{
							// Fire mouse click event
							MouseClick?.Invoke(this, args);
						}

						Container?.Invalidate(true);
					}
				}

				args.Handled = true;
				return true;
			}

			// Handle mouse movement
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
			}

			return false;
		}

		private int CalculateTotalVisibleItemsHeight()
		{
			int totalHeight = 0;
			int scrollOffset = CurrentScrollOffset;
			int itemsToCount = Math.Min(_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1, _items.Count - scrollOffset);

			for (int i = 0; i < itemsToCount; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex < _items.Count)
				{
					totalHeight += _items[itemIndex].Lines.Count;
				}
			}

			return totalHeight;
		}

		private void EnsureHighlightedItemVisible()
		{
			int highlightedIndex = CurrentHighlightedIndex;
			if (highlightedIndex < 0)
				return;

			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			int scrollOffset = CurrentScrollOffset;

			if (highlightedIndex < scrollOffset)
			{
				SetScrollOffset(highlightedIndex);
			}
			else if (highlightedIndex >= scrollOffset + effectiveMaxVisibleItems)
			{
				SetScrollOffset(highlightedIndex - effectiveMaxVisibleItems + 1);
			}
		}

		private void EnsureSelectedItemVisible()
		{
			int selectedIndex = CurrentSelectedIndex;
			if (selectedIndex < 0)
				return;

			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			int scrollOffset = CurrentScrollOffset;

			if (selectedIndex < scrollOffset)
			{
				SetScrollOffset(selectedIndex);
			}
			else if (selectedIndex >= scrollOffset + effectiveMaxVisibleItems)
			{
				SetScrollOffset(selectedIndex - effectiveMaxVisibleItems + 1);
			}
		}
	}

	/// <summary>
	/// Represents an item in a ListControl.
	/// </summary>
	public class ListItem
	{
		private List<string>? _lines;
		private string _text;

		/// <summary>
		/// Initializes a new ListItem with text, optional icon, and icon color.
		/// </summary>
		/// <param name="text">The text content of the item.</param>
		/// <param name="icon">Optional icon to display before the text.</param>
		/// <param name="iconColor">Optional color for the icon.</param>
		public ListItem(string text, string? icon = null, Color? iconColor = null)
		{
			_text = string.Empty;
			Text = text;
			Icon = icon;
			IconColor = iconColor;
		}

		/// <summary>
		/// Gets or sets the icon displayed before the item text.
		/// </summary>
		public string? Icon { get; set; }

		/// <summary>
		/// Gets or sets the color of the icon.
		/// </summary>
		public Color? IconColor { get; set; }

		/// <summary>
		/// Gets or sets whether this item is enabled.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Gets the text split into separate lines for multi-line items.
		/// </summary>
		public List<string> Lines => _lines ?? new List<string> { Text };

		/// <summary>
		/// Gets or sets a custom object associated with this item.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text content of the item.
		/// </summary>
		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				// Split the text into lines when the text is set
				_lines = value?.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				if (_lines != null && _lines.Count == 0)
				{
					_lines = new List<string> { "" };
				}
			}
		}

		/// <summary>
		/// Implicitly converts a string to a ListItem for convenience.
		/// </summary>
		/// <param name="text">The text to convert.</param>
		public static implicit operator ListItem(string text) => new ListItem(text);
	}
}