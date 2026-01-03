// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Core;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A scrollable list control that supports selection, highlighting, and keyboard navigation.
	/// </summary>
	public class ListControl : IWindowControl, IInteractiveControl, IFocusableControl
	{
		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private Alignment _alignment = Alignment.Left;
		private bool _autoAdjustWidth = false;
		private Color? _backgroundColorValue;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private int? _calculatedMaxVisibleItems;
		private bool _fillHeight = false;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private Color? _highlightBackgroundColorValue;
		private Color? _highlightForegroundColorValue;
		private bool _invalidated = true;
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
			_title = string.Empty;
		}

		/// <summary>
		/// Initializes a new empty ListControl with a title.
		/// </summary>
		/// <param name="title">The title displayed at the top of the list.</param>
		public ListControl(string title)
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
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
				var content = _contentCache.Content;
				return content?.Count;
			}
		}

		/// <summary>
		/// Gets the actual rendered width in characters.
		/// </summary>
		public int? ActualWidth
		{
			get
			{
				var content = _contentCache.Content;
				if (content == null) return null;
				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		/// <inheritdoc/>
		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets whether the control fills all available vertical space.
		/// </summary>
		public bool FillHeight
		{
			get => _fillHeight;
			set
			{
				_fillHeight = value;
				_contentCache.Invalidate();
				_invalidated = true;
				Container?.Invalidate(true);
			}
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
				_invalidated = true;
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
					_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
					_contentCache.Invalidate(InvalidationReason.SizeChanged);
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
			_contentCache.Invalidate();
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

			_contentCache.Invalidate();
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
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_invalidated = true;
			_contentCache.Invalidate();
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
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.UpArrow:
						if (scrollOffset > 0)
						{
							SetScrollOffset(scrollOffset - 1);
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageDown:
						int pageSize = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10;
						if (scrollOffset < _items.Count - pageSize)
						{
							SetScrollOffset(Math.Min(_items.Count - pageSize, scrollOffset + pageSize));
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageUp:
						if (scrollOffset > 0)
						{
							SetScrollOffset(Math.Max(0, scrollOffset - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10)));
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.Home:
						if (scrollOffset > 0)
						{
							SetScrollOffset(0);
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.End:
						int availableItems = _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10);
						if (scrollOffset < availableItems && availableItems > 0)
						{
							SetScrollOffset(availableItems);
							_contentCache.Invalidate();
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
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (highlightedIndex > 0)
					{
						SelectionService?.SetHighlightedIndex(this, highlightedIndex - 1);
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
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
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_items.Count > 0)
					{
						SelectionService?.SetHighlightedIndex(this, _items.Count - 1);
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (highlightedIndex > 0)
					{
						SelectionService?.SetHighlightedIndex(this, Math.Max(0, highlightedIndex - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1)));
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (highlightedIndex < _items.Count - 1)
					{
						SelectionService?.SetHighlightedIndex(this, Math.Min(_items.Count - 1, highlightedIndex + (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1)));
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
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
								_contentCache.Invalidate();
								Container?.Invalidate(true);
								return true;
							}
						}
					}
					return false;
			}
		}

		/// <inheritdoc/>
		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			var layoutService = Container?.GetConsoleWindowSystem?.LayoutStateService;

			// Smart invalidation: check if re-render is needed due to size change
			if (layoutService == null || layoutService.NeedsRerender(this, availableWidth, availableHeight))
			{
				// Dimensions changed - invalidate cache
				_contentCache.Invalidate(InvalidationReason.SizeChanged);
			}
			else
			{
				// Dimensions unchanged - return cached content if available
				var cached = _contentCache.Content;
				if (cached != null) return cached;
			}

			// Update available space tracking
			layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

			return _contentCache.GetOrRender(() =>
			{

				var content = new List<string>();

				// Get appropriate colors based on state
				Color backgroundColor;
				Color foregroundColor;
				Color windowBackground = Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			Color windowForeground = Container?.ForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;

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

			// Add space for selection indicator ([X] ) if selectable
			int indicatorSpace = _isSelectable ? 4 : 0;

			// Ensure width can accommodate content
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
				if (itemLength > maxItemWidth)
					maxItemWidth = itemLength;
			}

			// Calculate effective width based on different scenarios
			int listWidth;

			if (_width.HasValue)
			{
				// If width is explicitly defined, use it
				listWidth = _width.Value;
			}
			else if (_alignment == Alignment.Stretch && availableWidth.HasValue)
			{
				// When stretch and availableWidth is known, use full available width
				listWidth = availableWidth.Value - _margin.Left - _margin.Right;
			}
			else
			{
				// Calculate based on content width (items and title)
				foreach (var item in _items)
				{
					int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
					if (itemLength > maxItemWidth)
						maxItemWidth = itemLength;
				}

				// Consider title length if present
				int titleLength = string.IsNullOrEmpty(_title) ? 0 : AnsiConsoleHelper.StripSpectreLength(_title) + 5;

				// Base width is the maximum of item widths or title width plus padding
				listWidth = Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);

				// For non-stretch modes, limit to available width if provided
				if (availableWidth.HasValue)
				{
					listWidth = Math.Min(listWidth, availableWidth.Value) - _margin.Left - _margin.Right;
				}
				else
				{
					// Default minimum width when no constraints are available
					listWidth = Math.Max(listWidth, 40);
				}
			}

			// Apply autoAdjustWidth if enabled (only expands width, never shrinks)
			if (_autoAdjustWidth)
			{
				int contentWidth = 0;
				foreach (var item in _items)
				{
					int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
					contentWidth = Math.Max(contentWidth, itemLength);
				}

				int minRequiredWidth = contentWidth + indicatorSpace + 4;

				// Auto adjust can only make the width larger, not smaller
				listWidth = Math.Max(listWidth, minRequiredWidth);

				// But still respect available width if provided
				if (availableWidth.HasValue)
				{
					listWidth = Math.Min(listWidth, availableWidth.Value);
				}

				listWidth -= _margin.Left + _margin.Right;
			}

			// Only check title length if we have a title
			bool hasTitle = !string.IsNullOrEmpty(_title);

			if (hasTitle)
			{
				int titleLength = AnsiConsoleHelper.StripSpectreLength(_title);
				int minWidth = Math.Max(titleLength + 5, maxItemWidth + indicatorSpace + 4); // Add padding
				listWidth = Math.Max(listWidth, minWidth);
			}

			// Calculate padding for alignment
			int paddingLeft = 0;
			if (_alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, listWidth);
			}
			else if (_alignment == Alignment.Right && availableWidth.HasValue)
			{
				paddingLeft = availableWidth.Value - listWidth;
			}

			// Determine how many items to show based on available height and maxVisibleItems
			int scrollOffset = CurrentScrollOffset;
			int selectedIndex = CurrentSelectedIndex;
			int highlightedIndex = CurrentHighlightedIndex;
			int effectiveMaxVisibleItems;
			bool hasScrollIndicator = scrollOffset > 0 || _items.Count > (_maxVisibleItems ?? 10000);
			int titleHeight = hasTitle ? 1 : 0;
			int scrollIndicatorHeight = hasScrollIndicator ? 1 : 0;
			int marginHeight = _margin.Top + _margin.Bottom;

			if (_maxVisibleItems.HasValue)
			{
				// Use specified max visible items if provided
				effectiveMaxVisibleItems = _maxVisibleItems.Value;
			}
			else if (availableHeight.HasValue)
			{
				// Calculate based on available height if no max is specified
				// Account for title, margins and scroll indicators
				int availableContentHeight = availableHeight.Value - titleHeight - marginHeight - scrollIndicatorHeight;

				// Count how many items we can fit based on their actual line counts
				effectiveMaxVisibleItems = 0;
				int heightUsed = 0;

				for (int i = scrollOffset; i < _items.Count; i++)
				{
					int itemHeight = _items[i].Lines.Count;
					if (heightUsed + itemHeight <= availableContentHeight)  // Use <= to include items that exactly fit
					{
						effectiveMaxVisibleItems++;
						heightUsed += itemHeight;
					}
					else
					{
						break;
					}
				}

				// Ensure we show at least one item even if it doesn't fully fit
				effectiveMaxVisibleItems = Math.Max(1, effectiveMaxVisibleItems);
			}
			else
			{
				// Default if neither is available
				effectiveMaxVisibleItems = 10;
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			// Update scroll dimensions so MaxVerticalOffset is calculated correctly
			ScrollService?.UpdateDimensions(this, 0, _items.Count, 0, effectiveMaxVisibleItems);

			// Calculate how many items we can show
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - scrollOffset);

			// Render title bar only if title is not null or empty
			if (hasTitle)
			{
				string titleBarContent = " " + _title + " ";

				if (AnsiConsoleHelper.StripSpectreLength(titleBarContent) < listWidth)
				{
					int padding = listWidth - AnsiConsoleHelper.StripSpectreLength(titleBarContent);
					titleBarContent += new string(' ', padding);
				}

				List<string> titleLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					titleBarContent,
					listWidth,
					1,
					false,
					backgroundColor,
					foregroundColor
				);

				// Apply padding and margins to title
				for (int i = 0; i < titleLine.Count; i++)
				{
					// Add alignment padding
					if (paddingLeft > 0)
					{
						titleLine[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							new string(' ', paddingLeft),
							paddingLeft,
							1,
							false,
							Container?.BackgroundColor,
							null
						).FirstOrDefault() + titleLine[i];
					}

					// Add left margin
					titleLine[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', _margin.Left),
						_margin.Left,
						1,
						false,
						Container?.BackgroundColor,
						null
					).FirstOrDefault() + titleLine[i];

					// Add right margin
					titleLine[i] = titleLine[i] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', _margin.Right),
						_margin.Right,
						1,
						false,
						Container?.BackgroundColor,
						null
					).FirstOrDefault();
				}

				// Add title to result
				content.AddRange(titleLine);
			}

			// Initialize highlighted index if needed
			if (highlightedIndex == -1 && selectedIndex >= 0)
			{
				SelectionService?.SetHighlightedIndex(this, selectedIndex);
				highlightedIndex = selectedIndex;
			}

			// Render each visible item
			for (int i = 0; i < itemsToShow; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex >= _items.Count)
					break;

				// Get all lines for this item
				List<string> itemLines = _items[itemIndex].Lines;

				// Process each line of the item
				for (int lineIndex = 0; lineIndex < itemLines.Count; lineIndex++)
				{
					string lineText = itemLines[lineIndex];

					// Only use formatter on the first line for now
					// (could be extended to format each line differently)
					if (lineIndex == 0 && _itemFormatter != null)
					{
						lineText = _itemFormatter(_items[itemIndex], itemIndex == selectedIndex, _hasFocus);
					}

					// Truncate if necessary
					int maxTextWidth = listWidth - (indicatorSpace + 2); // Account for selection indicator and padding
					if (AnsiConsoleHelper.StripSpectreLength(lineText) > maxTextWidth)
					{
						lineText = lineText.Substring(0, maxTextWidth - 3) + "...";
					}

					// Determine colors for this item
					Color itemBg;
					Color itemFg;

					// For selectable lists, highlight the selected/highlighted item
					if (_isSelectable && itemIndex == highlightedIndex && _hasFocus)
					{
						// Focused: full highlight colors
						itemBg = HighlightBackgroundColor;
						itemFg = HighlightForegroundColor;
					}
					else if (_isSelectable && itemIndex == highlightedIndex && !_hasFocus)
					{
						// Unfocused: dimmed highlight (standard UI behavior - show selection when unfocused)
						// Use a slightly darker/lighter version of highlight colors
						itemBg = HighlightBackgroundColor;
						itemFg = Color.Grey;
					}
					// Handle selected but not highlighted (when navigating away from selected item)
					else if (_isSelectable && itemIndex == selectedIndex && _hasFocus)
					{
						itemBg = backgroundColor;
						itemFg = foregroundColor;
					}
					else
					{
						itemBg = backgroundColor;
						itemFg = foregroundColor;
					}

					string itemContent;

					// Only show selection indicator on first line of a multi-line item
					string selectionIndicator = "";
					if (_isSelectable && lineIndex == 0)
					{
						selectionIndicator = (itemIndex == selectedIndex) ? "[x] " : "[ ] ";
					}
					else if (_isSelectable)
					{
						// For subsequent lines, use empty space for alignment
						selectionIndicator = "    ";
					}

					// Handle items with icons (only on first line)
					if (lineIndex == 0 && _items[itemIndex].Icon != null)
					{
						string iconText = _items[itemIndex].Icon!;
						Color iconColor = _items[itemIndex].IconColor ?? itemFg;

						// Create icon markup with proper color
						string iconMarkup = $"[{iconColor.ToMarkup()}]{iconText}[/] ";

						// Calculate actual visible length of icon plus markup (not including ANSI sequences)
						int iconVisibleLength = AnsiConsoleHelper.StripSpectreLength(iconText) + 1; // +1 for the space

						// Add icon to the start of the item content
						itemContent = selectionIndicator + iconMarkup + lineText;

						// Calculate actual visual text length (without ANSI escape sequences)
						int visibleTextLength = selectionIndicator.Length + iconVisibleLength + AnsiConsoleHelper.StripSpectreLength(lineText);

						// Calculate the padding needed
						int paddingNeeded = Math.Max(0, listWidth - visibleTextLength);

						// Add padding to the end
						if (paddingNeeded > 0)
						{
							itemContent += new string(' ', paddingNeeded);
						}
					}
					else
					{
						// Handle subsequent lines or no icon
						// For lines after the first in multi-line items, indent to align with text in first line
						string indent = "";
						if (lineIndex > 0 && _items[itemIndex].Icon != null)
						{
							// Match the indentation of text on the first line (icon width + space)
							string iconText = _items[itemIndex].Icon!;
							int iconWidth = AnsiConsoleHelper.StripSpectreLength(iconText) + 1;
							indent = new string(' ', iconWidth);
						}

						itemContent = selectionIndicator + indent + lineText;

						// Calculate actual visual text length
						int visibleTextLength = selectionIndicator.Length + indent.Length + AnsiConsoleHelper.StripSpectreLength(lineText);

						// Add padding to the end
						int paddingNeeded = Math.Max(0, listWidth - visibleTextLength);
						if (paddingNeeded > 0)
						{
							itemContent += new string(' ', paddingNeeded);
						}
					}

					List<string> itemLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						itemContent,
						listWidth,
						1,
						false,
						itemBg,
						itemFg
					);

					// Apply padding and margins
					for (int j = 0; j < itemLine.Count; j++)
					{
						// Add alignment padding
						if (paddingLeft > 0)
						{
							itemLine[j] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
								new string(' ', paddingLeft),
								paddingLeft,
								1,
								false,
								Container?.BackgroundColor,
								null
							).FirstOrDefault() + itemLine[j];
						}

						// Add left margin
						itemLine[j] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							new string(' ', _margin.Left),
							_margin.Left,
							1,
							false,
							Container?.BackgroundColor,
							null
						).FirstOrDefault() + itemLine[j];

						// Add right margin
						itemLine[j] = itemLine[j] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							new string(' ', _margin.Right),
							_margin.Right,
							1,
							false,
							Container?.BackgroundColor,
							null
						).FirstOrDefault();
					}

					// Add item line to result
					content.AddRange(itemLine);
				}
			}

			// Add fill lines if FillHeight is true and we have space to fill
			if (_fillHeight && availableHeight.HasValue)
			{
				// Get current visible content height excluding margins
				int currentContentHeight = content.Count;

				// Account for title and scroll indicators that are already part of the content
				hasTitle = !string.IsNullOrEmpty(_title);
				hasScrollIndicator = (scrollOffset > 0 || scrollOffset + itemsToShow < _items.Count);

				// Calculate the target height we want to fill; title is already in content, so only reserve space for indicator
				int targetHeight = availableHeight.Value - _margin.Top - _margin.Bottom - (hasScrollIndicator ? 1 : 0);

				// Calculate how many empty lines we need to add
				int emptyLinesNeeded = targetHeight - currentContentHeight;

				if (emptyLinesNeeded > 0)
				{
					// Create empty lines with proper background color
					string emptyLine = new string(' ', listWidth);

					for (int i = 0; i < emptyLinesNeeded; i++)
					{
						List<string> fillerLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							emptyLine,
							listWidth,
							1,
							false,
							backgroundColor,
							foregroundColor
						);

						// Apply padding and margins
						for (int j = 0; j < fillerLine.Count; j++)
						{
							// Add alignment padding
							if (paddingLeft > 0)
							{
								fillerLine[j] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
									new string(' ', paddingLeft),
									paddingLeft,
									1,
									false,
									Container?.BackgroundColor,
									null
								).FirstOrDefault() + fillerLine[j];
							}

							// Add left margin
							fillerLine[j] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
								new string(' ', _margin.Left),
								_margin.Left,
								1,
								false,
								Container?.BackgroundColor,
								null
							).FirstOrDefault() + fillerLine[j];

							// Add right margin
							fillerLine[j] = fillerLine[j] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
								new string(' ', _margin.Right),
								_margin.Right,
								1,
								false,
								Container?.BackgroundColor,
								null
							).FirstOrDefault();
						}

						content.AddRange(fillerLine);
					}
				}
			}

			// Add scroll indicators if needed
			if (scrollOffset > 0 || scrollOffset + itemsToShow < _items.Count)
			{
				string scrollIndicator = "";

				// Top scroll indicator
				if (scrollOffset > 0)
					scrollIndicator += "▲";
				else
					scrollIndicator += " ";

				// Center padding
				int scrollPadding = listWidth - 2;
				if (scrollPadding > 0)
					scrollIndicator += new string(' ', scrollPadding);

				// Bottom scroll indicator
				if (scrollOffset + itemsToShow < _items.Count)
					scrollIndicator += "▼";
				else
					scrollIndicator += " ";

				List<string> scrollLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					scrollIndicator,
					listWidth,
					1,
					false,
					backgroundColor,
					foregroundColor
				);

				// Apply padding and margins
				for (int i = 0; i < scrollLine.Count; i++)
				{
					// Add alignment padding
					if (paddingLeft > 0)
					{
						scrollLine[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							new string(' ', paddingLeft),
							paddingLeft,
							1,
							false,
							Container?.BackgroundColor,
							null
						).FirstOrDefault() + scrollLine[i];
					}

					// Add left margin
					scrollLine[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', _margin.Left),
						_margin.Left,
						1,
						false,
						Container?.BackgroundColor,
						null
					).FirstOrDefault() + scrollLine[i];

					// Add right margin
					scrollLine[i] = scrollLine[i] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', _margin.Right),
						_margin.Right,
						1,
						false,
						Container?.BackgroundColor,
						null
					).FirstOrDefault();
				}

				// Add scroll indicator to result
				content.AddRange(scrollLine);
			}

			// Add top margin
			if (_margin.Top > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(content.FirstOrDefault() ?? string.Empty);
				var topMargin = Enumerable.Repeat(
					AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', finalWidth),
						finalWidth,
						1,
						false,
						windowBackground,
						windowForeground
					).FirstOrDefault() ?? string.Empty,
					_margin.Top
				).ToList();

				content.InsertRange(0, topMargin);
			}

			// Add bottom margin
			if (_margin.Bottom > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(content.FirstOrDefault() ?? string.Empty);
				var bottomMargin = Enumerable.Repeat(
					AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', finalWidth),
						finalWidth,
						1,
						false,
						windowBackground,
						windowForeground
					).FirstOrDefault() ?? string.Empty,
					_margin.Bottom
				).ToList();

				content.AddRange(bottomMargin);
			}

			_invalidated = false;
			return content;
		});
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
			// Keep the highlighted index when losing focus - standard UI behavior
			// The highlight shows where the user was, not what was selected
			_contentCache.Invalidate();
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