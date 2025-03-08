// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;

namespace ConsoleEx.Controls
{
	public class ListControl : IWIndowControl, IInteractiveControl
	{
		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private Alignment _alignment = Alignment.Left;
		private bool _autoAdjustWidth = true;
		private Color? _backgroundColorValue;
		private List<string>? _cachedContent;
		private int? _calculatedMaxVisibleItems;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private Color? _highlightBackgroundColorValue;
		private int _highlightedIndex = -1;
		private Color? _highlightForegroundColorValue;
		private bool _invalidated = true;
		private bool _isEnabled = true;
		private bool _isSelectable = true;
		private ItemFormatterEvent? _itemFormatter;
		private List<ListItem> _items = new List<ListItem>();
		private DateTime _lastKeyTime = DateTime.MinValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private int _maxVisibleItems = 10;
		private int _scrollOffset = 0;
		private string _searchText = string.Empty;
		private int _selectedIndex = -1;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string _title = "List";
		private bool _visible = true;
		private int? _width;

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

		public ListControl(string? title, IEnumerable<ListItem>? items)
		{
			_title = title ?? string.Empty;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		public ListControl(IEnumerable<ListItem>? items)
		{
			_title = string.Empty;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		public ListControl()
		{
			_title = string.Empty;
		}

		public ListControl(string title)
		{
			_title = title;
		}

		public delegate string ItemFormatterEvent(ListItem item, bool isSelected, bool hasFocus);

		// Events
		public event EventHandler<int>? SelectedIndexChanged;

		public event EventHandler<ListItem?>? SelectedItemChanged;

		public event EventHandler<string?>? SelectedValueChanged;

		public int? ActualHeight
		{
			get
			{
				if (_cachedContent == null) return null;
				return _cachedContent.Count;
			}
		}

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

		public bool AutoAdjustWidth
		{
			get => _autoAdjustWidth;
			set
			{
				_autoAdjustWidth = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public IContainer? Container { get; set; }

		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_focusedBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
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

		public Color HighlightBackgroundColor
		{
			get => _highlightBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonSelectedBackgroundColor ?? Color.DarkBlue;
			set
			{
				_highlightBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color HighlightForegroundColor
		{
			get => _highlightForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonSelectedForegroundColor ?? Color.White;
			set
			{
				_highlightForegroundColorValue = value;
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

		public bool IsSelectable
		{
			get => _isSelectable;
			set
			{
				_isSelectable = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public ItemFormatterEvent? ItemFormatter
		{
			get => _itemFormatter;
			set
			{
				_itemFormatter = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public List<ListItem> Items
		{
			get => _items;
			set
			{
				_items = value;
				if (_selectedIndex >= _items.Count)
				{
					_selectedIndex = _items.Count > 0 ? 0 : -1;
				}
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

		public int MaxVisibleItems
		{
			get => _maxVisibleItems;
			set
			{
				_maxVisibleItems = Math.Max(1, value);
				_cachedContent = null;
				_invalidated = true;
				Container?.Invalidate(true);
			}
		}

		public int SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				if (!_isSelectable)
					return;

				if (value >= -1 && value < _items.Count && _selectedIndex != value)
				{
					int oldIndex = _selectedIndex;
					_selectedIndex = value;
					_cachedContent = null;
					Container?.Invalidate(true);

					// Ensure selected item is visible
					if (_selectedIndex >= 0)
					{
						EnsureSelectedItemVisible();
					}

					// Trigger events
					if (oldIndex != _selectedIndex)
					{
						SelectedIndexChanged?.Invoke(this, _selectedIndex);
						SelectedItemChanged?.Invoke(this, (_selectedIndex >= 0 && _selectedIndex < _items.Count) ?
							_items[_selectedIndex] : null);
						SelectedValueChanged?.Invoke(this, (_selectedIndex >= 0 && _selectedIndex < _items.Count) ?
							_items[_selectedIndex].Text : null);
					}
				}
			}
		}

		public ListItem? SelectedItem
		{
			get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
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

		public string? SelectedValue
		{
			get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex].Text : null;
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

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public List<string> StringItems
		{
			get => _items.Select(i => i.Text).ToList();
			set
			{
				_items = value.Select(text => new ListItem(text)).ToList();
				if (_selectedIndex >= _items.Count)
				{
					_selectedIndex = _items.Count > 0 ? 0 : -1;
				}
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public string Title
		{
			get => _title;
			set
			{
				_title = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

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

		// Methods
		public void AddItem(ListItem item)
		{
			_items.Add(item);
			if (_selectedIndex == -1 && _items.Count == 1 && _isSelectable)
			{
				_selectedIndex = 0;
				SelectedIndexChanged?.Invoke(this, _selectedIndex);
				SelectedItemChanged?.Invoke(this, _items[_selectedIndex]);
				SelectedValueChanged?.Invoke(this, _items[_selectedIndex].Text);
			}
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		public void AddItem(string text, string? icon = null, Color? iconColor = null)
		{
			AddItem(new ListItem(text, icon, iconColor));
		}

		public void AddItem(string text)
		{
			AddItem(new ListItem(text));
		}

		public void ClearItems()
		{
			_items.Clear();
			_selectedIndex = -1;
			_highlightedIndex = -1;
			_scrollOffset = 0;
			_cachedContent = null;
			Container?.Invalidate(true);

			if (_isSelectable)
			{
				SelectedIndexChanged?.Invoke(this, _selectedIndex);
				SelectedItemChanged?.Invoke(this, null);
				SelectedValueChanged?.Invoke(this, null);
			}
		}

		public void Dispose()
		{
			Container = null;
		}

		public (int Left, int Top)? GetCursorPosition()
		{
			return null; // List control doesn't need a cursor position
		}

		public void Invalidate()
		{
			_invalidated = true;
			_cachedContent = null;
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			// If control is not selectable, we just handle scrolling
			if (!_isSelectable)
			{
				switch (key.Key)
				{
					case ConsoleKey.DownArrow:
						if (_scrollOffset < _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems))
						{
							_scrollOffset++;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.UpArrow:
						if (_scrollOffset > 0)
						{
							_scrollOffset--;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageDown:
						int pageSize = _calculatedMaxVisibleItems ?? _maxVisibleItems;
						if (_scrollOffset < _items.Count - pageSize)
						{
							_scrollOffset = Math.Min(_items.Count - pageSize, _scrollOffset + pageSize);
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageUp:
						if (_scrollOffset > 0)
						{
							_scrollOffset = Math.Max(0, _scrollOffset - (_calculatedMaxVisibleItems ?? _maxVisibleItems));
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.Home:
						if (_scrollOffset > 0)
						{
							_scrollOffset = 0;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.End:
						int availableItems = _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems);
						if (_scrollOffset < availableItems && availableItems > 0)
						{
							_scrollOffset = availableItems;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					default:
						return false;
				}
			}

			// If control is selectable, we handle both scrolling and selection
			switch (key.Key)
			{
				case ConsoleKey.DownArrow:
					if (_highlightedIndex < _items.Count - 1)
					{
						_highlightedIndex++;
						EnsureHighlightedItemVisible();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (_highlightedIndex > 0)
					{
						_highlightedIndex--;
						EnsureHighlightedItemVisible();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.Enter:
					if (_highlightedIndex >= 0 && _highlightedIndex < _items.Count)
					{
						SelectedIndex = _highlightedIndex;
						return true;
					}
					return false;

				case ConsoleKey.Home:
					if (_items.Count > 0)
					{
						_highlightedIndex = 0;
						EnsureHighlightedItemVisible();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_items.Count > 0)
					{
						_highlightedIndex = _items.Count - 1;
						EnsureHighlightedItemVisible();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_highlightedIndex > 0)
					{
						_highlightedIndex = Math.Max(0, _highlightedIndex - (_calculatedMaxVisibleItems ?? _maxVisibleItems));
						EnsureHighlightedItemVisible();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_highlightedIndex < _items.Count - 1)
					{
						_highlightedIndex = Math.Min(_items.Count - 1, _highlightedIndex + (_calculatedMaxVisibleItems ?? _maxVisibleItems));
						EnsureHighlightedItemVisible();
						_cachedContent = null;
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
								EnsureHighlightedItemVisible();
								_cachedContent = null;
								Container?.Invalidate(true);
								return true;
							}
						}
					}
					return false;
			}
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (!_invalidated && _cachedContent != null)
				return _cachedContent;

			_cachedContent = new List<string>();

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

			// Calculate effective width
			int listWidth = _width ?? (_alignment == Alignment.Strecth ? (availableWidth ?? 40) : 40);

			// Ensure width can accommodate content
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
				if (itemLength > maxItemWidth)
					maxItemWidth = itemLength;
			}

			// Add space for selection indicator (● ) if selectable
			int indicatorSpace = _isSelectable ? 2 : 0;

			if (_autoAdjustWidth)
			{
				listWidth = Math.Max(listWidth, maxItemWidth + indicatorSpace + 4);
			}

			// Only check title length if we have a title
			bool hasTitle = !string.IsNullOrEmpty(_title);

			if (hasTitle)
			{
				int titleLength = AnsiConsoleHelper.StripSpectreLength(_title);
				int minWidth = Math.Max(titleLength + 5, maxItemWidth + indicatorSpace + 4); // Add padding
				listWidth = Math.Max(listWidth, minWidth);
			}
			else
			{
				// No title, just consider item widths
				int minWidth = maxItemWidth + indicatorSpace + 4; // Add padding
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
			int effectiveMaxVisibleItems = _maxVisibleItems;

			if (availableHeight.HasValue)
			{
				// Account for title (if present), margin and scroll indicators
				int usedHeight = (hasTitle ? 1 : 0) + _margin.Top + _margin.Bottom;
				if (_scrollOffset > 0 || _items.Count > _maxVisibleItems)
				{
					usedHeight += 1; // Add space for scroll indicator
				}

				int availableItemSpace = Math.Max(1, availableHeight.Value - usedHeight);
				effectiveMaxVisibleItems = Math.Min(_maxVisibleItems, availableItemSpace);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

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
				_cachedContent.AddRange(titleLine);
			}

			// Initialize highlighted index if needed
			if (_highlightedIndex == -1 && _selectedIndex >= 0)
			{
				_highlightedIndex = _selectedIndex;
			}

			// Calculate how many items we can show
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - _scrollOffset);

			// Render each visible item
			for (int i = 0; i < itemsToShow; i++)
			{
				int itemIndex = i + _scrollOffset;
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
						lineText = _itemFormatter(_items[itemIndex], itemIndex == _selectedIndex, _hasFocus);
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

					// For selectable lists, highlight the selected item
					if (_isSelectable && itemIndex == _highlightedIndex && _hasFocus)
					{
						itemBg = HighlightBackgroundColor;
						itemFg = HighlightForegroundColor;
					}
					// Handle selected but not highlighted
					else if (_isSelectable && itemIndex == _selectedIndex)
					{
						itemBg = backgroundColor; // Use control background
						itemFg = foregroundColor; // Use control foreground
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
						selectionIndicator = (itemIndex == _selectedIndex) ? "[x] " : "[ ] ";
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
					_cachedContent.AddRange(itemLine);
				}
			}

			// Add scroll indicators if needed
			if (_scrollOffset > 0 || _scrollOffset + itemsToShow < _items.Count)
			{
				string scrollIndicator = "";

				// Top scroll indicator
				if (_scrollOffset > 0)
					scrollIndicator += "▲";
				else
					scrollIndicator += " ";

				// Center padding
				int scrollPadding = listWidth - 2;
				if (scrollPadding > 0)
					scrollIndicator += new string(' ', scrollPadding);

				// Bottom scroll indicator
				if (_scrollOffset + itemsToShow < _items.Count)
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
				_cachedContent.AddRange(scrollLine);
			}

			// Add top margin
			if (_margin.Top > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(_cachedContent.FirstOrDefault() ?? string.Empty);
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

				_cachedContent.InsertRange(0, topMargin);
			}

			// Add bottom margin
			if (_margin.Bottom > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(_cachedContent.FirstOrDefault() ?? string.Empty);
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

				_cachedContent.AddRange(bottomMargin);
			}

			_invalidated = false;
			return _cachedContent;
		}

		public void SetFocus(bool focus, bool backward)
		{
			_hasFocus = focus;
			if (!_hasFocus)
			{
				_highlightedIndex = _selectedIndex; // Reset highlighted index
			}
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		private int CalculateTotalVisibleItemsHeight()
		{
			int totalHeight = 0;
			int itemsToCount = Math.Min(_calculatedMaxVisibleItems ?? _maxVisibleItems, _items.Count - _scrollOffset);

			for (int i = 0; i < itemsToCount; i++)
			{
				int itemIndex = i + _scrollOffset;
				if (itemIndex < _items.Count)
				{
					totalHeight += _items[itemIndex].Lines.Count;
				}
			}

			return totalHeight;
		}

		private void EnsureHighlightedItemVisible()
		{
			if (_highlightedIndex < 0)
				return;

			// Calculate effective max visible items considering available space
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? _maxVisibleItems;

			// Now use effective max visible items for scrolling logic
			if (_highlightedIndex < _scrollOffset)
			{
				_scrollOffset = _highlightedIndex;
			}
			else if (_highlightedIndex >= _scrollOffset + effectiveMaxVisibleItems)
			{
				_scrollOffset = _highlightedIndex - effectiveMaxVisibleItems + 1;
			}
		}

		private void EnsureSelectedItemVisible()
		{
			if (_selectedIndex < 0)
				return;

			// Calculate effective max visible items considering available space
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? _maxVisibleItems;

			// Now use effective max visible items for scrolling logic
			if (_selectedIndex < _scrollOffset)
			{
				_scrollOffset = _selectedIndex;
			}
			else if (_selectedIndex >= _scrollOffset + effectiveMaxVisibleItems)
			{
				_scrollOffset = _selectedIndex - effectiveMaxVisibleItems + 1;
			}
		}
	}

	public class ListItem
	{
		private List<string>? _lines;
		private string _text;

		public ListItem(string text, string? icon = null, Color? iconColor = null)
		{
			_text = string.Empty;
			Text = text;
			Icon = icon;
			IconColor = iconColor;
		}

		public string? Icon { get; set; }
		public Color? IconColor { get; set; }
		public bool IsEnabled { get; set; } = true;

		// New property to access the text as separate lines
		public List<string> Lines => _lines ?? new List<string> { Text };

		public object? Tag { get; set; }

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

		// Implicit conversion operator for backward compatibility
		public static implicit operator ListItem(string text) => new ListItem(text);
	}
}