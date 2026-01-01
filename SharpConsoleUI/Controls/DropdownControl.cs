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
using Spectre.Console;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public class DropdownControl : IWindowControl, IInteractiveControl, IFocusableControl
	{
		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private Alignment _alignment = Alignment.Left;
		private bool _autoAdjustWidth = true;
		private Color? _backgroundColorValue;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private int? _calculatedMaxVisibleItems;
		private int _containerScrollOffsetBeforeDrop = 0;
		private int _dropdownScrollOffset = 0;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private Color? _highlightBackgroundColorValue;
		private int _highlightedIndex = -1;
		private Color? _highlightForegroundColorValue;
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
			_contentCache = new ThreadSafeCache<List<string>>(this);
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
			_contentCache = new ThreadSafeCache<List<string>>(this);
			_prompt = prompt;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		public DropdownControl(string prompt)
		{
			_contentCache = new ThreadSafeCache<List<string>>(this);
			_prompt = prompt;
		}

		public delegate string ItemFormatterEvent(DropdownItem item, bool isSelected, bool hasFocus);

		// Events
		public event EventHandler<int>? SelectedIndexChanged;

		public event EventHandler<DropdownItem?>? SelectedItemChanged;

		public event EventHandler<string?>? SelectedValueChanged;

		public event EventHandler? GotFocus;

		public event EventHandler? LostFocus;

		public int? ActualHeight
		{
			get
			{
				var content = _contentCache.Content;
				if (content == null) return null;

				// Return the total number of lines in the rendered content
				// This includes the header, dropdown items (if open), scroll indicators, and margins
				return content.Count;
			}
		}

		// Properties
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

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

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

		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

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

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_hasFocus != value)
				{
					_hasFocus = value;
					if (!_hasFocus)
					{
						// Collapse the dropdown when it loses focus
						_isDropdownOpen = false;
						_highlightedIndex = _selectedIndex; // Reset highlighted index
					}
					_contentCache.Invalidate();
					Container?.Invalidate(true);
					
					if (value)
						GotFocus?.Invoke(this, EventArgs.Empty);
					else
						LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public bool CanReceiveFocus => IsEnabled;

		public Color HighlightBackgroundColor
		{
			get => _highlightBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_highlightBackgroundColorValue = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		public Color HighlightForegroundColor
		{
			get => _highlightForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_highlightForegroundColorValue = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		public bool IsDropdownOpen
		{
			get => _isDropdownOpen;
			set
			{
				// On state change
				if (_isDropdownOpen != value)
				{
					// Find the containing window by traversing the container hierarchy
					Window? containerWindow = FindContainingWindow();

					bool containerIsWindow = containerWindow != null;

					// If opening, store current scroll offset
					if (value && !_isDropdownOpen && containerIsWindow)
					{
						_containerScrollOffsetBeforeDrop = containerWindow!.ScrollOffset;
					}

					_isDropdownOpen = value;
					_contentCache.Invalidate();
					Container?.Invalidate(true);

					// If closing, restore scroll offset (after content is recalculated)
					if (!value && containerIsWindow)
					{
						// Wait for container to update, then restore scroll offset
						Task.Run(async () =>
						{
							await Task.Delay(1); // Give time for rendering to complete
							RestoreContainerScrollOffset(containerWindow!);
						});
					}
				}
				else
				{
					_isDropdownOpen = value;
					_contentCache.Invalidate();
					
					Container?.Invalidate(true);
				}
			}
		}

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
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

		public int MaxVisibleItems
		{
			get => _maxVisibleItems;
			set
			{
				_maxVisibleItems = Math.Max(1, value);
				_contentCache.Invalidate();
				
				Container?.Invalidate(true);
			}
		}

		public string Prompt
		{
			get => _prompt;
			set
			{
				_prompt = value;
				_contentCache.Invalidate();
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
					_contentCache.Invalidate();
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
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

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
	}		// Adds a new item to the dropdown
		public void AddItem(DropdownItem item)
		{
			_items.Add(item);
			if (_selectedIndex == -1 && _items.Count == 1)
			{
				_selectedIndex = 0;
				SelectedIndexChanged?.Invoke(this, _selectedIndex);
				SelectedItemChanged?.Invoke(this, _items[_selectedIndex]);
			}
			_contentCache.Invalidate();
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
			_contentCache.Invalidate();
			Container?.Invalidate(true);

			SelectedIndexChanged?.Invoke(this, _selectedIndex);
			SelectedValueChanged?.Invoke(this, null);
		}

		public void Dispose()
		{
			Container = null;
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}

		public void Invalidate()
		{
			_contentCache.Invalidate();
			Container?.Invalidate(true, this);
		}		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			switch (key.Key)
			{
				case ConsoleKey.Enter:
					if (_isDropdownOpen)
					{
						// Select the currently highlighted item and close the dropdown
						if (_highlightedIndex >= 0 && _highlightedIndex < _items.Count)
						{
							SelectedIndex = _highlightedIndex; // Actually select the highlighted item
						}
						// Use the property setter to handle scroll offset
						IsDropdownOpen = false;
						return true;
					}
					else if (_items.Count > 0)
					{
						// Open dropdown - use property setter to handle scroll offset
						IsDropdownOpen = true;
						_highlightedIndex = _selectedIndex; // Initialize highlighted index with selected index
						return true;
					}
					return false;

				case ConsoleKey.Escape:
					if (_isDropdownOpen)
					{
						// Close dropdown without changing selection
						_highlightedIndex = _selectedIndex; // Reset highlighted index
															// Use property setter to handle scroll offset
						IsDropdownOpen = false;
						return true;
					}
					return false;

				case ConsoleKey.DownArrow:
					if (_isDropdownOpen)
					{
						if (_highlightedIndex < _items.Count - 1)
						{
							_highlightedIndex++;
							EnsureHighlightedItemVisible();
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
					}
					else if (_items.Count > 0)
					{
						// Open dropdown
						_isDropdownOpen = true;
						_highlightedIndex = _selectedIndex; // Initialize highlighted index with selected index
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (_isDropdownOpen)
					{
						if (_highlightedIndex > 0)
						{
							_highlightedIndex--;
							EnsureHighlightedItemVisible();
							_contentCache.Invalidate();
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.Home:
					if (_isDropdownOpen && _items.Count > 0)
					{
						_highlightedIndex = 0;
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_isDropdownOpen && _items.Count > 0)
					{
						_highlightedIndex = _items.Count - 1;
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_isDropdownOpen && _highlightedIndex > 0)
					{
						_highlightedIndex = Math.Max(0, _highlightedIndex - _calculatedMaxVisibleItems ?? 1);
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_isDropdownOpen && _highlightedIndex < _items.Count - 1)
					{
						_highlightedIndex = Math.Min(_items.Count - 1, _highlightedIndex + _calculatedMaxVisibleItems ?? 1);
						EnsureHighlightedItemVisible();
						_contentCache.Invalidate();
						Container?.Invalidate(true);
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
								_highlightedIndex = i; // Update highlighted index only
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

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
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

				// Calculate effective width
				int dropdownWidth = _width ?? (_alignment == Alignment.Stretch ? (availableWidth ?? 40) : calculateOptimalWidth(availableWidth));

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
			content.AddRange(headerLine);

			// If dropdown is open, render list items
			if (_isDropdownOpen && _items.Count > 0)
			{
				// Calculate available space for dropdown items considering margins and container height
				int availableSpaceForDropdown = 10000;

				//if (availableHeight.HasValue)
				//{
				//	int usedHeight = 1 + _margin.Top + _margin.Bottom + ((_dropdownScrollOffset > 0 || _items.Count > _maxVisibleItems) ? 1 : 0);
				//	availableSpaceForDropdown = Math.Max(1, availableHeight.Value - usedHeight);
				//}

				// Determine how many items to display based on available height and maxVisibleItems
				int effectiveMaxVisibleItems = Math.Min(_maxVisibleItems, availableSpaceForDropdown);

				_calculatedMaxVisibleItems = effectiveMaxVisibleItems; // Update maxVisibleItems to actual value

				// Now calculate actual items to show considering scroll offset
				int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - _dropdownScrollOffset);

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

					// Add selection indicator and calculate visual length for padding
					string selectionIndicator = (itemIndex == _highlightedIndex ? "● " : "  ");

					// Add selection indicator and padding
					if (_items[itemIndex].Icon != null)
					{
						string iconText = _items[itemIndex].Icon!;
						Color iconColor = _items[itemIndex].IconColor ?? itemFg;

						// Create icon markup with proper color
						string iconMarkup = $"[{iconColor.ToMarkup()}]{iconText}[/] ";

						// Calculate actual visible length of icon plus markup (not including ANSI sequences)
						int iconVisibleLength = AnsiConsoleHelper.StripSpectreLength(iconText) + 1; // +1 for the space

						// Add icon to the start of the item content - use highlightedIndex for visual highlight
						itemContent = selectionIndicator + iconMarkup + itemText;

						// Calculate actual visual text length (without ANSI escape sequences)
						int visibleTextLength = 2 + iconVisibleLength + AnsiConsoleHelper.StripSpectreLength(itemText);

						// Calculate the padding needed
						int paddingNeeded = Math.Max(0, dropdownWidth - visibleTextLength);

						// Add padding to the end
						if (paddingNeeded > 0)
						{
							itemContent += new string(' ', paddingNeeded);
						}
					}
					else
					{
						// No icon, use standard rendering - use highlightedIndex for visual highlight
						itemContent = selectionIndicator + itemText;

						// Calculate actual visual text length (without ANSI escape sequences)
						int visibleTextLength = 2 + AnsiConsoleHelper.StripSpectreLength(itemText);

						// Calculate the padding needed
						int paddingNeeded = Math.Max(0, dropdownWidth - visibleTextLength);

						// Add padding to the end
						if (paddingNeeded > 0)
						{
							itemContent += new string(' ', paddingNeeded);
						}
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
					content.AddRange(itemLine);
				}

				// Add scroll indicators if needed
				if (_dropdownScrollOffset > 0 || _dropdownScrollOffset + itemsToShow < _items.Count)
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
					if (_dropdownScrollOffset + itemsToShow < _items.Count)
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
					content.AddRange(scrollLine);
				}
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

			return content;
			});
		}

		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		// Calculate effective width
		// Calculate effective width
		private int calculateOptimalWidth(int? availableWidth)
		{
			// Calculate maximum item width including icons, selection indicators, and padding
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				// Base length includes text plus basic padding
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text);

				// Add space for selection indicator (2 chars: "● " or "  ")
				itemLength += 2;

				// Add space for icon if present
				if (item.Icon != null)
				{
					itemLength += AnsiConsoleHelper.StripSpectreLength(item.Icon) + 1; // +1 for space after icon
				}

				// Add some padding for comfortable viewing
				itemLength += 2;

				if (itemLength > maxItemWidth)
					maxItemWidth = itemLength;
			}

			// Consider prompt length for header row
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt);

			// Calculate header length: prompt + space + selected text + arrow
			// Allow at least 10 chars for selected text display
			int headerLength = promptLength + 1 + 10 + 3; // +1 for space, +3 for arrow and padding

			// Take the greater of max item width or header length
			int minWidth = Math.Max(headerLength, maxItemWidth);

			// If width is specified, use it as minimum
			if (_width.HasValue)
			{
				minWidth = Math.Max(minWidth, _width.Value);
			}

			// If stretch alignment and available width is provided, use available width
			if (_alignment == Alignment.Stretch && availableWidth.HasValue)
			{
				return availableWidth.Value;
			}

			return minWidth;
		}

		private void EnsureHighlightedItemVisible()
		{
			if (_highlightedIndex < 0)
				return;

			// Calculate effective max visible items considering available space
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? 1;

			// Now use effective max visible items for scrolling logic
			if (_highlightedIndex < _dropdownScrollOffset)
			{
				_dropdownScrollOffset = _highlightedIndex;
			}
			else if (_highlightedIndex >= _dropdownScrollOffset + effectiveMaxVisibleItems)
			{
				_dropdownScrollOffset = _highlightedIndex - effectiveMaxVisibleItems + 1;
			}
		}

		// Ensures the selected item is visible in the dropdown
		private void EnsureSelectedItemVisible()
		{
			if (_selectedIndex < 0)
				return;

			// Calculate effective max visible items considering available space
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? 1;

			// Now use effective max visible items for scrolling logic
			if (_selectedIndex < _dropdownScrollOffset)
			{
				_dropdownScrollOffset = _selectedIndex;
			}
			else if (_selectedIndex >= _dropdownScrollOffset + effectiveMaxVisibleItems)
			{
				_dropdownScrollOffset = _selectedIndex - effectiveMaxVisibleItems + 1;
			}
		}

		// Helper method to traverse up the container hierarchy until finding a Window instance
		private Window? FindContainingWindow()
		{
			// Start with the immediate container
			IContainer? currentContainer = Container;

			// Maximum number of levels to prevent infinite loops in case of circular references
			const int MaxLevels = 10;
			int level = 0;

			// Continue traversing up until we find a Window or reach the top
			while (currentContainer != null && level < MaxLevels)
			{
				// If the current container is a Window, return it
				if (currentContainer is Window window)
				{
					return window;
				}

				// If the current container is an IWindowControl, move up to its container
				if (currentContainer is IWindowControl control)
				{
					currentContainer = control.Container;
				}
				else
				{
					if (currentContainer is ColumnContainer columnContainer)
					{
						currentContainer = columnContainer.HorizontalGridContent.Container;
					}
					else
					{
						break;
					}
				}

				level++;
			}

			// If we didn't find a Window in the hierarchy, return null
			return null;
		}

		private void RestoreContainerScrollOffset(Window containerWindow)
		{
			// Use reflection to set the private _scrollOffset field in the Window class
			// since there's no public method to set it directly
			var scrollOffsetField = typeof(Window).GetField("_scrollOffset",
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Instance);

			if (scrollOffsetField != null)
			{
				scrollOffsetField.SetValue(containerWindow, _containerScrollOffsetBeforeDrop);
				containerWindow.Invalidate(true);
			}
			else
			{
				// Fallback if reflection doesn't work - simulate key presses
				containerWindow.GoToTop();
				for (int i = 0; i < _containerScrollOffsetBeforeDrop; i++)
				{
					containerWindow.ProcessInput(new ConsoleKeyInfo(
						'\0', ConsoleKey.DownArrow, false, false, false));
				}
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