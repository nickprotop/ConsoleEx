// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A scrollable list control that supports selection, highlighting, and keyboard navigation.
	/// </summary>
	public class ListControl : IWindowControl, IInteractiveControl, IFocusableControl, IDOMPaintable
	{
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
		private int _localScrollOffset = 0;  // Local fallback for scroll offset

		// Convenience property to access SelectionStateService
		private SelectionStateService? SelectionService => Container?.GetConsoleWindowSystem?.SelectionStateService;

		// Convenience property to access ScrollStateService
		private ScrollStateService? ScrollService => Container?.GetConsoleWindowSystem?.ScrollStateService;

		// Read-only helpers that read from state services (single source of truth)
		private int CurrentSelectedIndex => SelectionService?.GetSelectedIndex(this) ?? -1;
		private int CurrentHighlightedIndex => SelectionService?.GetHighlightedIndex(this) ?? -1;
		private int CurrentScrollOffset => ScrollService?.GetVerticalOffset(this) ?? _localScrollOffset;

		// Helper to set scroll offset - updates both service and local fallback
		private void SetScrollOffset(int offset)
		{
			_localScrollOffset = Math.Max(0, offset);
			// Use GetEffectiveVisibleItems() to get the actual visible item count (considering clipping)
			// This ensures MaxVerticalOffset is calculated correctly
			int viewportItems = GetEffectiveVisibleItems();
			ScrollService?.UpdateDimensions(this, 0, _items.Count, 0, viewportItems);
			ScrollService?.SetVerticalOffset(this, _localScrollOffset);
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

				int indicatorSpace = _isSelectable ? 4 : 0;
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
		public IContainer? Container { get; set; }

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
					SelectionService?.SetSelectedIndex(this, newSel);
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
				if (value >= -1 && value < _items.Count && oldIndex != value)
				{
					// Write to state service (single source of truth)
					SelectionService?.SetSelectedIndex(this, value);
					Container?.Invalidate(true);

					// Ensure selected item is visible
					if (value >= 0)
					{
						EnsureSelectedItemVisible();
					}

					// Trigger events
					SelectedIndexChanged?.Invoke(this, value);
					SelectedItemChanged?.Invoke(this, (value >= 0 && value < _items.Count) ?
						_items[value] : null);
					SelectedValueChanged?.Invoke(this, (value >= 0 && value < _items.Count) ?
						_items[value].Text : null);
				}
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
					SelectionService?.SetSelectedIndex(this, newSel);
				}
				Container?.Invalidate(true);
			}
		}

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
			SelectionService?.ClearAll(this);
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
			Container = null;
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

			int indicatorSpace = _isSelectable ? 4 : 0;
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
					if (highlightedIndex < _items.Count - 1)
					{
						SelectionService?.SetHighlightedIndex(this, highlightedIndex + 1);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (highlightedIndex > 0)
					{
						SelectionService?.SetHighlightedIndex(this, highlightedIndex - 1);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.Enter:
					if (highlightedIndex >= 0 && highlightedIndex < _items.Count)
					{
						SelectedIndex = highlightedIndex;
						return true;
					}
					return false;

				case ConsoleKey.Home:
					if (_items.Count > 0)
					{
						SelectionService?.SetHighlightedIndex(this, 0);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_items.Count > 0)
					{
						SelectionService?.SetHighlightedIndex(this, _items.Count - 1);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (highlightedIndex > 0)
					{
						SelectionService?.SetHighlightedIndex(this, Math.Max(0, highlightedIndex - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1)));
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (highlightedIndex < _items.Count - 1)
					{
						SelectionService?.SetHighlightedIndex(this, Math.Min(_items.Count - 1, highlightedIndex + (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1)));
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
								SelectionService?.SetHighlightedIndex(this, i);
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
			int indicatorSpace = _isSelectable ? 4 : 0;

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
			ScrollService?.UpdateDimensions(this, 0, _items.Count, 0, effectiveMaxVisibleItems);

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

			int indicatorSpace = _isSelectable ? 4 : 0;
			int listWidth = bounds.Width - _margin.Left - _margin.Right;
			if (listWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int currentY = startY;

			// Fill top margin
			for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', foregroundColor, windowBackground);
				}
			}

			bool hasTitle = !string.IsNullOrEmpty(_title);
			int scrollOffset = CurrentScrollOffset;
			int selectedIndex = CurrentSelectedIndex;
			int highlightedIndex = CurrentHighlightedIndex;

			// Initialize highlighted index if needed
			if (highlightedIndex == -1 && selectedIndex >= 0)
			{
				SelectionService?.SetHighlightedIndex(this, selectedIndex);
				highlightedIndex = selectedIndex;
			}

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

					string titleBarContent = " " + _title + " ";
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
			ScrollService?.UpdateDimensions(this, 0, _items.Count, 0, effectiveMaxVisibleItems);

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
						Color itemBg, itemFg;
						if (_isSelectable && itemIndex == highlightedIndex && _hasFocus)
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

						// Build item content
						string selectionIndicator = "";
						if (_isSelectable && lineIndex == 0)
						{
							selectionIndicator = (itemIndex == selectedIndex) ? "[x] " : "[ ] ";
						}
						else if (_isSelectable)
						{
							selectionIndicator = "    ";
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
			for (int y = currentY; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', foregroundColor, windowBackground);
				}
			}
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
			// Keep the highlighted index when losing focus - standard UI behavior
			// The highlight shows where the user was, not what was selected
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