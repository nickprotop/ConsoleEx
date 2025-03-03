// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleEx.Controls
{
	public class DropdownControl : IWIndowControl, IInteractiveControl
	{
		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private Alignment _alignment = Alignment.Left;
		private bool _autoAdjustWidth = true;
		private Color? _backgroundColorValue;
		private List<string>? _cachedContent;
		private int _dropdownScrollOffset = 0;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private Color? _highlightBackgroundColorValue;
		private Color? _highlightForegroundColorValue;
		private bool _invalidated = true;
		private bool _isDropdownOpen = false;
		private bool _isEnabled = true;
		private ItemFormatterEvent? _itemFormatter;
		private List<DropdownItem> _items = new List<DropdownItem>();
		private DateTime _lastKeyTime = DateTime.MinValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private int _maxVisibleItems = 5;
		private string _prompt = "Select an item:";
		private string _searchText = string.Empty;
		private int _selectedIndex = -1;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Constructor with optional prompt and items
		public DropdownControl(string prompt = "Select an item:", IEnumerable<string>? items = null)
		{
			_prompt = prompt;
			if (items != null)
			{
				foreach (var item in items)
				{
					_items.Add(new DropdownItem(item));
				}
			}
		}

		public DropdownControl(string prompt, IEnumerable<DropdownItem>? items = null)
		{
			_prompt = prompt;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		public DropdownControl(string prompt)
		{
			_prompt = prompt;
		}

		public delegate string ItemFormatterEvent(DropdownItem item, bool isSelected, bool hasFocus);

		// Events
		public event EventHandler<int>? SelectedIndexChanged;

		public event EventHandler<DropdownItem?>? SelectedItemChanged;

		public event EventHandler<string?>? SelectedValueChanged;

		// Properties
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
		{ get => _alignment; set { _alignment = value; _cachedContent = null; Container?.Invalidate(true); } }

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
			get => _highlightBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_highlightBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color HighlightForegroundColor
		{
			get => _highlightForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_highlightForegroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool IsDropdownOpen
		{
			get => _isDropdownOpen;
			set
			{
				_isDropdownOpen = value;
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

		public List<DropdownItem> Items
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
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public int MaxVisibleItems
		{
			get => _maxVisibleItems;
			set
			{
				_maxVisibleItems = Math.Max(1, value);
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public string Prompt
		{
			get => _prompt;
			set
			{
				_prompt = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public int SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				if (value >= -1 && value < _items.Count && _selectedIndex != value)
				{
					int oldIndex = _selectedIndex;
					_selectedIndex = value;
					_cachedContent = null;
					Container?.Invalidate(true);

					// Ensure selected item is visible when dropdown is open
					if (_isDropdownOpen && _selectedIndex >= 0)
					{
						EnsureSelectedItemVisible();
					}

					// Trigger events
					if (oldIndex != _selectedIndex)
					{
						SelectedIndexChanged?.Invoke(this, _selectedIndex);
						SelectedItemChanged?.Invoke(this, (_selectedIndex >= 0 && _selectedIndex < _items.Count) ?
							_items[_selectedIndex] : null);

						// Keep for backward compatibility
						string? selectedValue = (_selectedIndex >= 0 && _selectedIndex < _items.Count) ?
							_items[_selectedIndex].Text : null;
						SelectedValueChanged?.Invoke(this, selectedValue);
					}
				}
			}
		}

		public DropdownItem? SelectedItem
		{
			get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
			set
			{
				if (value == null)
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
				if (value == null)
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
				_items = value.Select(text => new DropdownItem(text)).ToList();
				if (_selectedIndex >= _items.Count)
				{
					_selectedIndex = _items.Count > 0 ? 0 : -1;
				}
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool Visible
		{ get => _visible; set { _visible = value; _cachedContent = null; Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

		// Adds a new item to the dropdown
		public void AddItem(DropdownItem item)
		{
			_items.Add(item);
			if (_selectedIndex == -1 && _items.Count == 1)
			{
				_selectedIndex = 0;
				SelectedIndexChanged?.Invoke(this, _selectedIndex);
				SelectedItemChanged?.Invoke(this, _items[_selectedIndex]);
			}
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		public void AddItem(string text, string icon, Color? iconColor = null)
		{
			AddItem(new DropdownItem(text, icon, iconColor));
		}

		public void AddItem(string text)
		{
			AddItem(new DropdownItem(text));
		}

		// Clears all items from the dropdown
		public void ClearItems()
		{
			_items.Clear();
			_selectedIndex = -1;
			_dropdownScrollOffset = 0;
			_cachedContent = null;
			Container?.Invalidate(true);

			SelectedIndexChanged?.Invoke(this, _selectedIndex);
			SelectedValueChanged?.Invoke(this, null);
		}

		public void Dispose()
		{
			Container = null;
		}

		public (int Left, int Top)? GetCursorPosition()
		{
			return null; // Dropdown doesn't need a cursor position
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

			switch (key.Key)
			{
				case ConsoleKey.Enter:
					if (_isDropdownOpen)
					{
						// Close dropdown and keep selection
						_isDropdownOpen = false;
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					else if (_items.Count > 0)
					{
						// Open dropdown
						_isDropdownOpen = true;
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.Escape:
					if (_isDropdownOpen)
					{
						// Close dropdown without changing selection
						_isDropdownOpen = false;
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.DownArrow:
					if (_isDropdownOpen)
					{
						if (_selectedIndex < _items.Count - 1)
						{
							SelectedIndex = _selectedIndex + 1;
							return true;
						}
					}
					else if (_items.Count > 0)
					{
						// Open dropdown
						_isDropdownOpen = true;
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (_isDropdownOpen)
					{
						if (_selectedIndex > 0)
						{
							SelectedIndex = _selectedIndex - 1;
							return true;
						}
					}
					return false;

				case ConsoleKey.Home:
					if (_isDropdownOpen && _items.Count > 0)
					{
						SelectedIndex = 0;
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_isDropdownOpen && _items.Count > 0)
					{
						SelectedIndex = _items.Count - 1;
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_isDropdownOpen && _selectedIndex > 0)
					{
						SelectedIndex = Math.Max(0, _selectedIndex - _maxVisibleItems);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_isDropdownOpen && _selectedIndex < _items.Count - 1)
					{
						SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + _maxVisibleItems);
						return true;
					}
					return false;

				default:
					// Check if it's a letter/number key for quick selection
					if (!char.IsControl(key.KeyChar) && _isDropdownOpen)
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
								SelectedIndex = i;
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
			Color windowBackground = Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			Color windowForeground = Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;

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
			int dropdownWidth = _width ?? (_alignment == Alignment.Strecth ? (availableWidth ?? 40) : 40);

			// Ensure width can accommodate content
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
				if (itemLength > maxItemWidth)
					maxItemWidth = itemLength;
			}

			if (_autoAdjustWidth)
			{
				dropdownWidth = Math.Max(dropdownWidth, maxItemWidth + 4);
			}

			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt);
			int minWidth = Math.Max(promptLength + 5, maxItemWidth + 4); // Add padding
			dropdownWidth = Math.Max(dropdownWidth, minWidth);

			// Calculate padding for alignment
			int paddingLeft = 0;
			if (_alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, dropdownWidth);
			}
			else if (_alignment == Alignment.Right && availableWidth.HasValue)
			{
				paddingLeft = availableWidth.Value - dropdownWidth;
			}

			// Render header with selected item
			string selectedText = _selectedIndex >= 0 && _selectedIndex < _items.Count
				? _items[_selectedIndex].Text
				: "(None)";

			// Truncate selected text if needed and add arrow indicator
			string arrow = _isDropdownOpen ? "▲" : "▼";
			int maxSelectedTextLength = dropdownWidth - promptLength - 5;

			if (selectedText.Length > maxSelectedTextLength)
			{
				selectedText = selectedText.Substring(0, maxSelectedTextLength - 3) + "...";
			}

			// Format header text
			string headerContent = $"{_prompt} {selectedText} {arrow}";

			// Ensure header fits within dropdown width
			if (AnsiConsoleHelper.StripSpectreLength(headerContent) < dropdownWidth)
			{
				headerContent = headerContent.PadRight(dropdownWidth);
			}

			// Render header
			List<string> headerLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
				headerContent,
				dropdownWidth,
				1,
				false,
				backgroundColor,
				foregroundColor
			);

			// Apply padding and margins to header
			for (int i = 0; i < headerLine.Count; i++)
			{
				// Add alignment padding
				if (paddingLeft > 0)
				{
					headerLine[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', paddingLeft),
						paddingLeft,
						1,
						false,
						Container?.BackgroundColor,
						null
					).FirstOrDefault() + headerLine[i];
				}

				// Add left and right margins
				headerLine[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', _margin.Left),
					_margin.Left,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault() + headerLine[i];

				headerLine[i] = headerLine[i] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', _margin.Right),
					_margin.Right,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault();
			}

			// Add header to result
			_cachedContent.AddRange(headerLine);

			// If dropdown is open, render list items
			if (_isDropdownOpen && _items.Count > 0)
			{
				// Determine how many items to display
				int itemsToShow = Math.Min(_maxVisibleItems, _items.Count - _dropdownScrollOffset);

				// Render each visible item
				for (int i = 0; i < itemsToShow; i++)
				{
					int itemIndex = i + _dropdownScrollOffset;
					if (itemIndex >= _items.Count)
						break;

					string itemText = _itemFormatter != null
						? _itemFormatter(_items[itemIndex], itemIndex == _selectedIndex, _hasFocus)
						: _items[itemIndex].Text;

					// Truncate if necessary
					if (AnsiConsoleHelper.StripSpectreLength(itemText) > dropdownWidth - 4)
					{
						itemText = itemText.Substring(0, dropdownWidth - 7) + "...";
					}

					string itemContent;

					// Use highlight colors for selected item, normal colors for others
					Color itemBg = (itemIndex == _selectedIndex) ? HighlightBackgroundColor : backgroundColor;
					Color itemFg = (itemIndex == _selectedIndex) ? HighlightForegroundColor : foregroundColor;

					// Add selection indicator and padding
					if (_items[itemIndex].Icon != null)
					{
						string iconText = _items[itemIndex].Icon;
						Color iconColor = _items[itemIndex].IconColor ?? itemFg;

						// Create icon markup with proper color
						string iconMarkup = $"[{iconColor.ToMarkup()}]{iconText}[/] ";

						// Add icon to the start of the item content
						itemContent = (itemIndex == _selectedIndex ? "● " : "  ") + iconMarkup + itemText;
					}
					else
					{
						// No icon, use standard rendering
						itemContent = (itemIndex == _selectedIndex ? "● " : "  ") + itemText;
					}

					// Ensure item fits within dropdown width
					if (AnsiConsoleHelper.StripSpectreLength(itemContent) < dropdownWidth)
					{
						itemContent = itemContent.PadRight(dropdownWidth);
					}

					List<string> itemLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						itemContent,
						dropdownWidth,
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

					// Add item to result
					_cachedContent.AddRange(itemLine);
				}

				// Add scroll indicators if needed
				if (_dropdownScrollOffset > 0 || _dropdownScrollOffset + _maxVisibleItems < _items.Count)
				{
					string scrollIndicator = "";

					// Top scroll indicator
					if (_dropdownScrollOffset > 0)
						scrollIndicator += "▲";
					else
						scrollIndicator += " ";

					// Center padding
					int scrollPadding = dropdownWidth - 2;
					if (scrollPadding > 0)
						scrollIndicator += new string(' ', scrollPadding);

					// Bottom scroll indicator
					if (_dropdownScrollOffset + _maxVisibleItems < _items.Count)
						scrollIndicator += "▼";
					else
						scrollIndicator += " ";

					List<string> scrollLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						scrollIndicator,
						dropdownWidth,
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
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		// Ensures the selected item is visible in the dropdown
		private void EnsureSelectedItemVisible()
		{
			if (_selectedIndex < 0)
				return;

			if (_selectedIndex < _dropdownScrollOffset)
			{
				_dropdownScrollOffset = _selectedIndex;
			}
			else if (_selectedIndex >= _dropdownScrollOffset + _maxVisibleItems)
			{
				_dropdownScrollOffset = _selectedIndex - _maxVisibleItems + 1;
			}
		}
	}

	public class DropdownItem
	{
		public DropdownItem(string text, string? icon = null, Color? iconColor = null)
		{
			Text = text;
			Icon = icon;
			IconColor = iconColor;
		}

		public string? Icon { get; set; }
		public Color? IconColor { get; set; }
		public bool IsEnabled { get; set; } = true;
		public object? Tag { get; set; }
		public string Text { get; set; }

		// Implicit conversion operator for backward compatibility
		public static implicit operator DropdownItem(string text) => new DropdownItem(text);
	}
}