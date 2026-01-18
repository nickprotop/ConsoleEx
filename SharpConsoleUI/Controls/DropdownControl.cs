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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Color = Spectre.Console.Color;
using Size = System.Drawing.Size;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A dropdown/combobox control that displays a list of selectable items.
	/// Supports keyboard navigation, type-ahead search, and custom item formatting.
	/// </summary>
	public class DropdownControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDOMPaintable
	{
		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _autoAdjustWidth = true;
		private Color? _backgroundColorValue;
		private int _containerScrollOffsetBeforeDrop = 0;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private Color? _highlightBackgroundColorValue;
		private Color? _highlightForegroundColorValue;
		private bool _isDropdownOpen = false;
		private bool _isEnabled = true;
		private ItemFormatterEvent? _itemFormatter;
		private List<DropdownItem> _items = new List<DropdownItem>();
		private DateTime _lastKeyTime = DateTime.MinValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private int _maxVisibleItems = 5;
		private string _prompt = "Select an item:";
		private readonly StringBuilder _searchBuilder = new();
		private string _searchText = string.Empty;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Local state - controls own their selection/highlight state
		private int _selectedIndex = -1;
		private int _highlightedIndex = -1;
		private int _dropdownScrollOffset = 0;

		// Mouse state tracking
		private bool _isHeaderPressed;
		private int _mouseHoveredIndex = -1;

		// Portal state for dropdown overlay
		private LayoutNode? _dropdownPortal;
		private DropdownPortalContent? _portalContent;
		private Rectangle _dropdownBounds;
		private bool _opensUpward;
		private LayoutRect _lastLayoutBounds;

		// Read-only helpers
		private int CurrentSelectedIndex => _selectedIndex;
		private int CurrentHighlightedIndex => _highlightedIndex;
		private int CurrentDropdownScrollOffset => _dropdownScrollOffset;

		// Helper to set scroll offset
		private void SetDropdownScrollOffset(int offset)
		{
			_dropdownScrollOffset = Math.Max(0, offset);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DropdownControl"/> class with string items.
		/// </summary>
		/// <param name="prompt">The prompt text displayed in the header.</param>
		/// <param name="items">Optional collection of string items to populate the dropdown.</param>
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

		/// <summary>
		/// Initializes a new instance of the <see cref="DropdownControl"/> class with dropdown items.
		/// </summary>
		/// <param name="prompt">The prompt text displayed in the header.</param>
		/// <param name="items">Optional collection of <see cref="DropdownItem"/> objects to populate the dropdown.</param>
		public DropdownControl(string prompt, IEnumerable<DropdownItem>? items = null)
		{
			_prompt = prompt;
			if (items != null)
			{
				_items.AddRange(items);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DropdownControl"/> class with only a prompt.
		/// </summary>
		/// <param name="prompt">The prompt text displayed in the header.</param>
		public DropdownControl(string prompt)
		{
			_prompt = prompt;
		}

		/// <summary>
		/// Delegate for custom item formatting in the dropdown list.
		/// </summary>
		/// <param name="item">The dropdown item to format.</param>
		/// <param name="isSelected">Whether the item is currently selected.</param>
		/// <param name="hasFocus">Whether the dropdown control has focus.</param>
		/// <returns>The formatted string representation of the item.</returns>
		public delegate string ItemFormatterEvent(DropdownItem item, bool isSelected, bool hasFocus);

		/// <summary>
		/// Occurs when the selected index changes.
		/// </summary>
		public event EventHandler<int>? SelectedIndexChanged;

		/// <summary>
		/// Occurs when the selected item changes.
		/// </summary>
		public event EventHandler<DropdownItem?>? SelectedItemChanged;

		/// <summary>
		/// Occurs when the selected value (text) changes.
		/// </summary>
		public event EventHandler<string?>? SelectedValueChanged;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		#region IMouseAwareControl Events and Properties

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		/// <inheritdoc/>
		public bool WantsMouseEvents => _isEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => _isEnabled;

		#endregion

		/// <summary>
		/// Gets the actual rendered height of the control based on cached content.
		/// </summary>
		/// <returns>The total number of lines including header, items, and margins, or null if not rendered.</returns>
		public int? ActualHeight
		{
			get
			{
				// Constant height - header (1 line) plus margins
				// Dropdown items render via portal overlay, not affecting control height
				return 1 + _margin.Top + _margin.Bottom;
			}
		}

		/// <summary>
		/// Gets the actual rendered width of the control based on cached content.
		/// </summary>
		/// <returns>The maximum line width in characters, or null if content has not been rendered.</returns>
		public int? ActualWidth
		{
			get
			{
				// Calculate optimal width based on items
				int maxItemWidth = 0;
				foreach (var item in _items)
				{
					int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text) + 4;
					if (itemLength > maxItemWidth) maxItemWidth = itemLength;
				}

				int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt) + 5;
				int dropdownWidth = Math.Max(promptLength, maxItemWidth);

				return dropdownWidth + _margin.Left + _margin.Right;
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

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
		/// Gets or sets the background color of the dropdown in its normal state.
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

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets the background color when the control has focus.
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
		/// Gets or sets the foreground color when the control has focus.
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
		/// Gets or sets the foreground color of the dropdown in its normal state.
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
				if (_hasFocus != value)
				{
					_hasFocus = value;
					// Note: Dropdown closing is handled by SetFocus(), not here
					// to ensure proper portal cleanup via CloseDropdown()
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Gets or sets the background color for the highlighted item in the dropdown list (keyboard or mouse).
		/// </summary>
		public Color HighlightBackgroundColor
		{
			get => _highlightBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.DropdownHighlightBackgroundColor ?? Color.Blue;
			set
			{
				_highlightBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color for the highlighted item in the dropdown list (keyboard or mouse).
		/// </summary>
		public Color HighlightForegroundColor
		{
			get => _highlightForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.DropdownHighlightForegroundColor ?? Color.White;
			set
			{
				_highlightForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the dropdown list is currently expanded.
		/// </summary>
		public bool IsDropdownOpen
		{
			get => _isDropdownOpen;
			set
			{
				if (_isDropdownOpen != value)
				{
					if (value)
					{
						OpenDropdown();
					}
					else
					{
						CloseDropdown();
					}
				}
			}
		}

		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <summary>
		/// Gets or sets the custom item formatter delegate for rendering dropdown items.
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
		/// Gets or sets the list of dropdown items.
		/// </summary>
		public List<DropdownItem> Items
		{
			get => _items;
			set
			{
				_items = value;
				int currentSel = CurrentSelectedIndex;
				if (currentSel >= _items.Count)
				{
					int oldIndex = currentSel;
					int newSel = _items.Count > 0 ? 0 : -1;
					_selectedIndex = newSel;
					_highlightedIndex = newSel;
					if (oldIndex != newSel)
					{
						SelectedIndexChanged?.Invoke(this, newSel);
						SelectedItemChanged?.Invoke(this, newSel >= 0 && newSel < _items.Count ? _items[newSel] : null);
						SelectedValueChanged?.Invoke(this, newSel >= 0 && newSel < _items.Count ? _items[newSel].Text : null);
					}
				}
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the margin around the control content.
		/// </summary>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <summary>
		/// Gets or sets the maximum number of items visible in the dropdown list without scrolling.
		/// </summary>
		public int MaxVisibleItems
		{
			get => _maxVisibleItems;
			set
			{
				_maxVisibleItems = Math.Max(1, value);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the prompt text displayed in the dropdown header.
		/// </summary>
		public string Prompt
		{
			get => _prompt;
			set
			{
				_prompt = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the index of the currently selected item. Returns -1 if no item is selected.
		/// </summary>
		public int SelectedIndex
		{
			get => CurrentSelectedIndex;
			set
			{
				int currentSel = CurrentSelectedIndex;
				if (value >= -1 && value < _items.Count && currentSel != value)
				{
					int oldIndex = currentSel;
					_selectedIndex = value;
					_highlightedIndex = value;
					Container?.Invalidate(true);

					// Ensure selected item is visible when dropdown is open
					if (_isDropdownOpen && value >= 0)
					{
						EnsureSelectedItemVisible();
					}

					// Trigger events
					if (oldIndex != value)
					{
						SelectedIndexChanged?.Invoke(this, value);
						SelectedItemChanged?.Invoke(this, (value >= 0 && value < _items.Count) ?
							_items[value] : null);

						// Keep for backward compatibility
						string? selectedValue = (value >= 0 && value < _items.Count) ?
							_items[value].Text : null;
						SelectedValueChanged?.Invoke(this, selectedValue);
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the currently selected dropdown item. Returns null if no item is selected.
		/// </summary>
		public DropdownItem? SelectedItem
		{
			get
			{
				int sel = CurrentSelectedIndex;
				return sel >= 0 && sel < _items.Count ? _items[sel] : null;
			}
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

		/// <summary>
		/// Gets or sets the text of the currently selected item. Returns null if no item is selected.
		/// </summary>
		public string? SelectedValue
		{
			get
			{
				int sel = CurrentSelectedIndex;
				return sel >= 0 && sel < _items.Count ? _items[sel].Text : null;
			}
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

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <summary>
		/// Gets or sets the items as a list of strings for simplified access.
		/// </summary>
		public List<string> StringItems
		{
			get => _items.Select(i => i.Text).ToList();
			set
			{
				_items = value.Select(text => new DropdownItem(text)).ToList();
				int currentSel = CurrentSelectedIndex;
				if (currentSel >= _items.Count)
				{
					int oldIndex = currentSel;
					int newSel = _items.Count > 0 ? 0 : -1;
					_selectedIndex = newSel;
					_highlightedIndex = newSel;
					if (oldIndex != newSel)
					{
						SelectedIndexChanged?.Invoke(this, newSel);
						SelectedItemChanged?.Invoke(this, newSel >= 0 && newSel < _items.Count ? _items[newSel] : null);
						SelectedValueChanged?.Invoke(this, newSel >= 0 && newSel < _items.Count ? _items[newSel].Text : null);
					}
				}
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the fixed width of the control. When null, the control auto-sizes based on content.
		/// </summary>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

		/// <summary>
		/// Adds a new item to the dropdown list.
		/// </summary>
		/// <param name="item">The dropdown item to add.</param>
		public void AddItem(DropdownItem item)
		{
			_items.Add(item);
			if (CurrentSelectedIndex == -1 && _items.Count == 1)
			{
				_selectedIndex = 0;
				_highlightedIndex = 0;
				SelectedIndexChanged?.Invoke(this, 0);
				SelectedItemChanged?.Invoke(this, _items[0]);
				SelectedValueChanged?.Invoke(this, _items[0].Text);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a new item to the dropdown list with text and an optional icon.
		/// </summary>
		/// <param name="text">The text to display for the item.</param>
		/// <param name="icon">The icon character or string to display.</param>
		/// <param name="iconColor">Optional color for the icon.</param>
		public void AddItem(string text, string icon, Color? iconColor = null)
		{
			AddItem(new DropdownItem(text, icon, iconColor));
		}

		/// <summary>
		/// Adds a new item to the dropdown list with text only.
		/// </summary>
		/// <param name="text">The text to display for the item.</param>
		public void AddItem(string text)
		{
			AddItem(new DropdownItem(text));
		}

		/// <summary>
		/// Removes all items from the dropdown list and resets selection state.
		/// </summary>
		public void ClearItems()
		{
			_items.Clear();

			// Clear local selection state
			int oldIndex = _selectedIndex;
			_selectedIndex = -1;
			_highlightedIndex = -1;

			// Clear scroll state
			_dropdownScrollOffset = 0;

			Container?.Invalidate(true);

			if (oldIndex != -1)
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
			int width = ActualWidth ?? 0;
			int height = ActualHeight ?? 1;
			return new System.Drawing.Size(width, height);
		}

		/// <summary>
		/// Invalidates the cached content, forcing a re-render on the next draw.
		/// </summary>
		public void Invalidate()
		{
			Container?.Invalidate(true, this);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			int currentHighlight = CurrentHighlightedIndex;
			int currentSelection = CurrentSelectedIndex;

			switch (key.Key)
			{
				case ConsoleKey.Enter:
					if (_isDropdownOpen)
					{
						// Select the currently highlighted item and close the dropdown
						if (currentHighlight >= 0 && currentHighlight < _items.Count)
						{
							SelectedIndex = currentHighlight; // Actually select the highlighted item
						}
						// Use the property setter to handle scroll offset
						IsDropdownOpen = false;
						return true;
					}
					else if (_items.Count > 0)
					{
						// Open dropdown - use property setter to handle scroll offset
						IsDropdownOpen = true;
						_highlightedIndex = currentSelection;
						return true;
					}
					return false;

				case ConsoleKey.Escape:
					if (_isDropdownOpen)
					{
						// Close dropdown without changing selection - reset highlighted to selected
						_highlightedIndex = currentSelection;
						// Use property setter to handle scroll offset
						IsDropdownOpen = false;
						return true;
					}
					return false;

				case ConsoleKey.DownArrow:
					if (_isDropdownOpen)
					{
						if (currentHighlight < _items.Count - 1)
						{
							_highlightedIndex = currentHighlight + 1;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					else if (_items.Count > 0)
					{
						// Open dropdown - use property to trigger OpenDropdown()
						IsDropdownOpen = true;
						_highlightedIndex = currentSelection;
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (_isDropdownOpen)
					{
						if (currentHighlight > 0)
						{
							_highlightedIndex = currentHighlight - 1;
							EnsureHighlightedItemVisible();
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
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_isDropdownOpen && _items.Count > 0)
					{
						_highlightedIndex = _items.Count - 1;
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_isDropdownOpen && currentHighlight > 0)
					{
						int newIndex = Math.Max(0, currentHighlight - ( 1));
						_highlightedIndex = newIndex;
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_isDropdownOpen && currentHighlight < _items.Count - 1)
					{
						int newIndex = Math.Min(_items.Count - 1, currentHighlight + ( 1));
						_highlightedIndex = newIndex;
						EnsureHighlightedItemVisible();
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
							_searchBuilder.Clear();
							_searchBuilder.Append(key.KeyChar);
						}
						else
						{
							_searchBuilder.Append(key.KeyChar);
						}

						_searchText = _searchBuilder.ToString();
						_lastKeyTime = DateTime.Now;

						// Search for items starting with the search text
						for (int i = 0; i < _items.Count; i++)
						{
							if (_items[i].Text.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase))
							{
								_highlightedIndex = i;
								EnsureHighlightedItemVisible();
								Container?.Invalidate(true);
								return true;
							}
						}
					}
					return false;
			}
		}

		#region IMouseAwareControl Implementation

		/// <summary>
		/// Processes mouse events for the dropdown header.
		/// Note: When the dropdown list is open, it's rendered as a portal and
		/// receives events via ProcessPortalMouseEvent instead.
		/// </summary>
		/// <param name="args">Mouse event arguments with control-relative coordinates.</param>
		/// <returns>True if the event was handled.</returns>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled)
				return false;

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				_isHeaderPressed = false;
				if (!_hasFocus)
				{
					_mouseHoveredIndex = -1;
				}
				MouseLeave?.Invoke(this, args);
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse enter
			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return true;
			}

			// Check if click is on header (Y == 0 for single-line header)
			bool isOnHeader = args.Position.Y == 0;

			// Handle mouse move
			if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return true;
			}

			// Handle mouse down
			if (args.HasAnyFlag(MouseFlags.Button1Pressed))
			{
				if (isOnHeader)
				{
					_isHeaderPressed = true;

					// Capture focus on mouse down
					if (!_hasFocus)
					{
						SetFocus(true, FocusReason.Mouse);
					}

					Container?.Invalidate(true);
					return true;
				}

				// Click outside header area
				return false;
			}

			// Handle mouse up
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				bool wasHeaderPressed = _isHeaderPressed;
				_isHeaderPressed = false;

				if (wasHeaderPressed && isOnHeader)
				{
					// Toggle dropdown
					IsDropdownOpen = !_isDropdownOpen;

					MouseClick?.Invoke(this, args);
					Container?.Invalidate(true);
					return true;
				}

				return true;
			}

			return false;
		}

		/// <summary>
		/// Processes mouse events from the dropdown portal (when dropdown list is open).
		/// Called by DropdownPortalContent when the portal receives mouse events.
		/// </summary>
		/// <param name="args">Mouse event arguments with portal-relative coordinates.</param>
		/// <returns>True if the event was handled.</returns>
		internal bool ProcessPortalMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled || !_isDropdownOpen)
				return false;

			// Portal coordinates are relative to portal bounds (no border, starts at 0,0)
			int contentY = args.Position.Y;
			int contentX = args.Position.X;

			// Calculate visible structure for hit testing
			int effectiveMaxVisibleItems =  _maxVisibleItems;
			bool hasScrollIndicator = (_items.Count > effectiveMaxVisibleItems);
			int visibleItemCount = Math.Min(effectiveMaxVisibleItems, _items.Count - _dropdownScrollOffset);
			int scrollIndicatorRow = hasScrollIndicator ? visibleItemCount : -1;

			// Check if click is on scroll indicator row
			bool isOnScrollIndicatorRow = hasScrollIndicator && contentY == scrollIndicatorRow;

			// Find which item is at this Y position (accounting for scroll offset)
			int hitItemIndex = -1;
			if (contentY >= 0 && !isOnScrollIndicatorRow)
			{
				int itemIndex = contentY + _dropdownScrollOffset;
				if (itemIndex >= 0 && itemIndex < _items.Count)
				{
					hitItemIndex = itemIndex;
				}
			}

			// Handle mouse wheel scrolling
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				if (_dropdownScrollOffset > 0)
				{
					SetDropdownScrollOffset(_dropdownScrollOffset - 1);
					Container?.Invalidate(true);
					args.Handled = true;
					return true; // Consumed
				}
				else
				{
					return false; // Allow parent to handle
				}
			}
			else if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int maxScroll = Math.Max(0, _items.Count - effectiveMaxVisibleItems);
				if (_dropdownScrollOffset < maxScroll)
				{
					SetDropdownScrollOffset(_dropdownScrollOffset + 1);
					Container?.Invalidate(true);
					args.Handled = true;
					return true; // Consumed
				}
				else
				{
					return false; // Allow parent to handle
				}
			}

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (!_hasFocus)
				{
					_mouseHoveredIndex = -1;
				}
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse move (hover)
			if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
			{
				if (hitItemIndex != _mouseHoveredIndex)
				{
					_mouseHoveredIndex = hitItemIndex;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Handle scroll indicator clicks
			if (isOnScrollIndicatorRow)
			{
				// Only act on mouse release to avoid double-scroll
				if (args.HasFlag(MouseFlags.Button1Released))
				{
					int dropdownWidth = _dropdownBounds.Width;

					// Small click areas: ▲ at positions 0-2, ▼ at last 3 positions
					if (contentX <= 2 && _dropdownScrollOffset > 0)
					{
						// Click on ▲ area - scroll up
						SetDropdownScrollOffset(_dropdownScrollOffset - 1);
						Container?.Invalidate(true);
					}
					else if (contentX >= dropdownWidth - 3)
					{
						// Click on ▼ area - scroll down
						int maxScroll = Math.Max(0, _items.Count - effectiveMaxVisibleItems);
						if (_dropdownScrollOffset < maxScroll)
						{
							SetDropdownScrollOffset(_dropdownScrollOffset + 1);
							Container?.Invalidate(true);
						}
					}
				}
				return true; // Consume all events on scroll indicator row
			}

			// Handle mouse down on items
			if (args.HasAnyFlag(MouseFlags.Button1Pressed))
			{
				if (hitItemIndex >= 0)
				{
					_mouseHoveredIndex = hitItemIndex;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Handle mouse up - select item
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				if (hitItemIndex >= 0 && hitItemIndex == _mouseHoveredIndex)
				{
					// Select the item and close dropdown
					SelectedIndex = hitItemIndex;
					IsDropdownOpen = false;
					_mouseHoveredIndex = -1;

					MouseClick?.Invoke(this, args);
					Container?.Invalidate(true);
				}
				return true;
			}

			return false;
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int dropdownWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch
				? constraints.MaxWidth - _margin.Left - _margin.Right
				: calculateOptimalWidth(constraints.MaxWidth));

			// Ensure width can accommodate content
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text) + 4;
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			if (_autoAdjustWidth)
				dropdownWidth = Math.Max(dropdownWidth, maxItemWidth + 4);

			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt);
			int minWidth = Math.Max(promptLength + 5, maxItemWidth + 4);
			dropdownWidth = Math.Max(dropdownWidth, minWidth);

			// Calculate height - constant (header only), dropdown items render via portal
			int height = 1 + _margin.Top + _margin.Bottom;

			int width = dropdownWidth + _margin.Left + _margin.Right;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			// Store bounds for portal positioning
			_lastLayoutBounds = bounds;

			Color backgroundColor;
			Color foregroundColor;
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

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

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground);

			// Calculate dropdown width
			int dropdownWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : calculateOptimalWidth(targetWidth));
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text) + 4;
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}
			if (_autoAdjustWidth)
				dropdownWidth = Math.Max(dropdownWidth, maxItemWidth + 4);

			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt);
			int minWidth = Math.Max(promptLength + 5, maxItemWidth + 4);
			dropdownWidth = Math.Min(Math.Max(dropdownWidth, minWidth), targetWidth);

			// Calculate alignment offset
			int alignOffset = 0;
			if (dropdownWidth < targetWidth)
			{
				switch (_horizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - dropdownWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - dropdownWidth;
						break;
				}
			}

			int selectedIdx = CurrentSelectedIndex;
			int highlightedIdx = CurrentHighlightedIndex;
			int dropdownScroll = CurrentDropdownScrollOffset;

			// Render header
			string selectedText = selectedIdx >= 0 && selectedIdx < _items.Count ? _items[selectedIdx].Text : "(None)";
			// Arrow shows direction: ▲ when open upward, ▼ when closed or open downward
			string arrow = _isDropdownOpen && _opensUpward ? "▲" : "▼";
			int maxSelectedTextLength = dropdownWidth - promptLength - 5;
			if (maxSelectedTextLength > 0 && selectedText.Length > maxSelectedTextLength)
				selectedText = selectedText.Substring(0, Math.Max(0, maxSelectedTextLength - 3)) + "...";

			string headerContent = $"{_prompt} {selectedText} {arrow}";
			int headerVisibleLength = AnsiConsoleHelper.StripSpectreLength(headerContent);
			if (headerVisibleLength < dropdownWidth)
				headerContent += new string(' ', dropdownWidth - headerVisibleLength);

			int paintY = startY;
			if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
			{
				if (_margin.Left > 0)
					buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', foregroundColor, windowBackground);

				if (alignOffset > 0)
					buffer.FillRect(new LayoutRect(startX, paintY, alignOffset, 1), ' ', foregroundColor, windowBackground);

				var ansiLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(headerContent, dropdownWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? string.Empty;
				var cells = AnsiParser.Parse(ansiLine, foregroundColor, backgroundColor);
				buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

				int rightFillStart = startX + alignOffset + dropdownWidth;
				int rightFillWidth = bounds.Right - rightFillStart - _margin.Right;
				if (rightFillWidth > 0)
					buffer.FillRect(new LayoutRect(rightFillStart, paintY, rightFillWidth, 1), ' ', foregroundColor, windowBackground);

				if (_margin.Right > 0)
					buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
			}
			paintY++;

			// Note: Dropdown items are rendered via portal overlay (PaintDropdownListInternal)
			// This keeps the control height constant and allows the list to extend beyond parent bounds

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, paintY, foregroundColor, windowBackground);
		}

		#endregion

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = HasFocus;
			HasFocus = focus;

			if (focus && !hadFocus)
			{
				// Gaining focus
				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
				// Losing focus - close dropdown and clear mouse state
				_mouseHoveredIndex = -1;
				_isHeaderPressed = false;
				_highlightedIndex = CurrentSelectedIndex; // Reset highlight to selection
				if (_isDropdownOpen)
				{
					IsDropdownOpen = false;
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

		#region Portal Methods

		/// <summary>
		/// Opens the dropdown and creates the portal overlay.
		/// </summary>
		private void OpenDropdown()
		{
			if (_isDropdownOpen) return;

			_isDropdownOpen = true;

			// Calculate portal bounds
			CalculatePortalBounds();

			// Create portal content and add to window
			var window = Container as Window ?? FindContainingWindow();
			if (window != null)
			{
				_portalContent = new DropdownPortalContent(this);
				_dropdownPortal = window.CreatePortal(this, _portalContent);
			}

			Container?.Invalidate(true);
		}

		/// <summary>
		/// Closes the dropdown and removes the portal overlay.
		/// </summary>
		private void CloseDropdown()
		{
			if (!_isDropdownOpen) return;

			// Remove portal
			if (_dropdownPortal != null)
			{
				var window = Container as Window ?? FindContainingWindow();
				if (window != null)
				{
					window.RemovePortal(this, _dropdownPortal);
				}
				_dropdownPortal = null;
				_portalContent = null;
			}

			_isDropdownOpen = false;
			_mouseHoveredIndex = -1;

			Container?.Invalidate(true);
		}

		/// <summary>
		/// Calculates the portal bounds with auto-flip logic.
		/// If the dropdown would extend past the window bottom, it flips upward.
		/// </summary>
		private void CalculatePortalBounds()
		{
			// Get screen dimensions
			int screenWidth = 160;
			int screenHeight = 40;

			var window = Container as Window ?? FindContainingWindow();
			if (window != null)
			{
				screenWidth = window.Width;
				screenHeight = window.Height;
			}

			// Calculate dropdown dimensions
			int dropdownWidth = CalculateDropdownWidth();
			int effectiveMaxVisibleItems =  _maxVisibleItems;
			int visibleItems = Math.Min(effectiveMaxVisibleItems, _items.Count);
			int hasScrollIndicator = (_items.Count > visibleItems) ? 1 : 0;
			int dropdownHeight = visibleItems + hasScrollIndicator;

			// Calculate header position from last layout bounds
			int headerX = _lastLayoutBounds.X + _margin.Left;
			int headerY = _lastLayoutBounds.Y + _margin.Top;

			// Calculate alignment offset (same logic as PaintDOM)
			int targetWidth = _lastLayoutBounds.Width - _margin.Left - _margin.Right;
			int alignOffset = 0;
			if (dropdownWidth < targetWidth)
			{
				switch (_horizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - dropdownWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - dropdownWidth;
						break;
				}
			}

			int dropdownX = headerX + alignOffset;

			// Check if dropdown fits below or needs to flip upward
			int spaceBelow = screenHeight - (headerY + 1); // +1 for header height
			int spaceAbove = headerY;

			if (dropdownHeight > spaceBelow && spaceAbove > spaceBelow)
			{
				// Flip upward
				_opensUpward = true;
				int dropdownY = headerY - dropdownHeight;
				// Clamp to screen bounds
				if (dropdownY < 0)
				{
					dropdownHeight += dropdownY; // Reduce height
					dropdownY = 0;
				}
				_dropdownBounds = new Rectangle(dropdownX, dropdownY, dropdownWidth, dropdownHeight);
			}
			else
			{
				// Open downward (default)
				_opensUpward = false;
				int dropdownY = headerY + 1; // Below header
				// Clamp height to available space
				if (dropdownY + dropdownHeight > screenHeight)
				{
					dropdownHeight = screenHeight - dropdownY;
				}
				_dropdownBounds = new Rectangle(dropdownX, dropdownY, dropdownWidth, dropdownHeight);
			}
		}

		/// <summary>
		/// Calculates the optimal dropdown width.
		/// </summary>
		private int CalculateDropdownWidth()
		{
			int targetWidth = _lastLayoutBounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) targetWidth = 20;

			int dropdownWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : calculateOptimalWidth(targetWidth));

			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text) + 4;
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			if (_autoAdjustWidth)
				dropdownWidth = Math.Max(dropdownWidth, maxItemWidth + 4);

			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt);
			int minWidth = Math.Max(promptLength + 5, maxItemWidth + 4);
			dropdownWidth = Math.Min(Math.Max(dropdownWidth, minWidth), targetWidth);

			return dropdownWidth;
		}

		/// <summary>
		/// Gets the portal bounds for positioning the dropdown overlay.
		/// </summary>
		internal Rectangle GetPortalBounds()
		{
			return _dropdownBounds;
		}

		/// <summary>
		/// Paints the dropdown list items (called by portal content).
		/// </summary>
		internal void PaintDropdownListInternal(CharacterBuffer buffer, LayoutRect clipRect)
		{
			if (_items.Count == 0) return;

			// Get colors for dropdown list (uses dedicated dropdown theme colors)
			Color backgroundColor;
			Color foregroundColor;

			if (!_isEnabled)
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			else
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.DropdownBackgroundColor ?? Color.Grey23;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.DropdownForegroundColor ?? Color.White;
			}

			int selectedIdx = CurrentSelectedIndex;
			int highlightedIdx = CurrentHighlightedIndex;
			int dropdownScroll = CurrentDropdownScrollOffset;

			// Use mouse hover index if active, otherwise keyboard highlight
			int effectiveHighlight = _mouseHoveredIndex >= 0 ? _mouseHoveredIndex : highlightedIdx;

			int dropdownWidth = _dropdownBounds.Width;
			int startX = _dropdownBounds.X;
			int paintY = _dropdownBounds.Y;

			int effectiveMaxVisibleItems =  _maxVisibleItems;
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - dropdownScroll);

			// Clamp to available height in bounds
			int availableHeight = _dropdownBounds.Height;
			bool hasScrollIndicator = (_items.Count > effectiveMaxVisibleItems);
			int maxItemsInBounds = hasScrollIndicator ? availableHeight - 1 : availableHeight;
			itemsToShow = Math.Min(itemsToShow, maxItemsInBounds);

			for (int i = 0; i < itemsToShow; i++)
			{
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					int itemIndex = i + dropdownScroll;
					if (itemIndex >= _items.Count) break;

					string itemText = _itemFormatter != null
						? _itemFormatter(_items[itemIndex], itemIndex == selectedIdx, _hasFocus)
						: _items[itemIndex].Text;

					if (AnsiConsoleHelper.StripSpectreLength(itemText) > dropdownWidth - 4)
						itemText = itemText.Substring(0, Math.Max(0, dropdownWidth - 7)) + "...";

					Color itemBg = (itemIndex == selectedIdx) ? HighlightBackgroundColor : backgroundColor;
					Color itemFg = (itemIndex == selectedIdx) ? HighlightForegroundColor : foregroundColor;

					// Override with hover highlight
					if (itemIndex == effectiveHighlight)
					{
						itemBg = HighlightBackgroundColor;
						itemFg = HighlightForegroundColor;
					}

					string selectionIndicator = itemIndex == highlightedIdx ? "● " : "  ";
					string itemContent = selectionIndicator + itemText;
					int visibleTextLength = 2 + AnsiConsoleHelper.StripSpectreLength(itemText);
					int paddingNeeded = Math.Max(0, dropdownWidth - visibleTextLength);
					if (paddingNeeded > 0)
						itemContent += new string(' ', paddingNeeded);

					var itemAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(itemContent, dropdownWidth, 1, false, itemBg, itemFg).FirstOrDefault() ?? string.Empty;
					var itemCells = AnsiParser.Parse(itemAnsi, itemFg, itemBg);
					buffer.WriteCellsClipped(startX, paintY, itemCells, clipRect);
				}
				paintY++;
			}

			// Render scroll indicators if needed
			if (hasScrollIndicator && paintY < _dropdownBounds.Bottom)
			{
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					string scrollIndicator = (dropdownScroll > 0 ? "▲" : " ");
					int scrollPadding = dropdownWidth - 2;
					if (scrollPadding > 0)
						scrollIndicator += new string(' ', scrollPadding);
					scrollIndicator += (dropdownScroll + itemsToShow < _items.Count ? "▼" : " ");

					var scrollAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(scrollIndicator, dropdownWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? string.Empty;
					var scrollCells = AnsiParser.Parse(scrollAnsi, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(startX, paintY, scrollCells, clipRect);
				}
			}
		}

		#endregion

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
			if (_horizontalAlignment == HorizontalAlignment.Stretch && availableWidth.HasValue)
			{
				return availableWidth.Value;
			}

			return minWidth;
		}

		private void EnsureHighlightedItemVisible()
		{
			int highlightedIdx = CurrentHighlightedIndex;
			if (highlightedIdx < 0)
				return;

			// GUI framework approach: viewport is defined by MaxVisibleItems, not container space
			int effectiveMaxVisibleItems = _maxVisibleItems;

			int scrollOffset = CurrentDropdownScrollOffset;

			if (highlightedIdx < scrollOffset)
			{
				SetDropdownScrollOffset(highlightedIdx);
			}
			else if (highlightedIdx >= scrollOffset + effectiveMaxVisibleItems)
			{
				SetDropdownScrollOffset(highlightedIdx - effectiveMaxVisibleItems + 1);
			}
		}

		// Ensures the selected item is visible in the dropdown
		private void EnsureSelectedItemVisible()
		{
			int selectedIdx = CurrentSelectedIndex;
			if (selectedIdx < 0)
				return;

			// GUI framework approach: viewport is defined by MaxVisibleItems, not container space
			int effectiveMaxVisibleItems = _maxVisibleItems;

			int scrollOffset = CurrentDropdownScrollOffset;

			if (selectedIdx < scrollOffset)
			{
				SetDropdownScrollOffset(selectedIdx);
			}
			else if (selectedIdx >= scrollOffset + effectiveMaxVisibleItems)
			{
				SetDropdownScrollOffset(selectedIdx - effectiveMaxVisibleItems + 1);
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

	/// <summary>
	/// Represents an item in a <see cref="DropdownControl"/> with text, optional icon, and metadata.
	/// </summary>
	public class DropdownItem
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DropdownItem"/> class.
		/// </summary>
		/// <param name="text">The display text for the item.</param>
		/// <param name="icon">Optional icon character or string to display before the text.</param>
		/// <param name="iconColor">Optional color for the icon.</param>
		public DropdownItem(string text, string? icon = null, Color? iconColor = null)
		{
			Text = text;
			Icon = icon;
			IconColor = iconColor;
		}

		/// <summary>
		/// Gets or sets the icon character or string displayed before the item text.
		/// </summary>
		public string? Icon { get; set; }

		/// <summary>
		/// Gets or sets the color of the icon.
		/// </summary>
		public Color? IconColor { get; set; }

		/// <summary>
		/// Gets or sets whether the item is enabled and can be selected.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets custom data associated with this item.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the display text for the item.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Implicitly converts a string to a <see cref="DropdownItem"/> for convenience.
		/// </summary>
		/// <param name="text">The text to convert.</param>
		/// <returns>A new <see cref="DropdownItem"/> with the specified text.</returns>
		public static implicit operator DropdownItem(string text) => new DropdownItem(text);
	}

	/// <summary>
	/// Internal portal content control for rendering dropdown lists as overlays.
	/// This control is created by DropdownControl when the dropdown opens and is added as a portal child
	/// to render outside the normal bounds of the dropdown control.
	/// </summary>
	internal class DropdownPortalContent : IWindowControl, IDOMPaintable, IMouseAwareControl
	{
		private readonly DropdownControl _owner;

		public DropdownPortalContent(DropdownControl owner)
		{
			_owner = owner;
		}

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => false;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			return _owner.ProcessPortalMouseEvent(args);
		}

		#endregion

		#region IWindowControl Implementation

		public int? ActualWidth => _owner.GetPortalBounds().Width;
		public int? ActualHeight => _owner.GetPortalBounds().Height;
		public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
		public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
		public IContainer? Container { get; set; }
		public Margin Margin { get; set; } = new Margin(0, 0, 0, 0);
		public StickyPosition StickyPosition { get; set; } = StickyPosition.None;
		public string? Name { get; set; }
		public object? Tag { get; set; }
		public bool Visible { get; set; } = true;
		public int? Width { get; set; }

		public Size GetLogicalContentSize()
		{
			var bounds = _owner.GetPortalBounds();
			return new Size(bounds.Width, bounds.Height);
		}

		/// <summary>
		/// Gets the absolute bounds for portal positioning.
		/// </summary>
		public Rectangle GetPortalBounds()
		{
			return _owner.GetPortalBounds();
		}

		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		public void Dispose()
		{
			// Nothing to dispose
		}

		#endregion

		#region IDOMPaintable Implementation

		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			var bounds = _owner.GetPortalBounds();
			return new LayoutSize(bounds.Width, bounds.Height);
		}

		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
							 Color defaultFg, Color defaultBg)
		{
			_owner.PaintDropdownListInternal(buffer, clipRect);
		}

		#endregion
	}
}
