// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Text;
using Color = Spectre.Console.Color;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A scrollable list control that supports selection, highlighting, and keyboard navigation.
	/// </summary>
	public partial class ListControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		/// <summary>
		/// Creates a fluent builder for constructing a ListControl.
		/// </summary>
		/// <returns>A new ListBuilder instance.</returns>
		public static Builders.ListBuilder Create()
		{
			return new Builders.ListBuilder();
		}

		#region Fields

		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
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
		private int? _maxVisibleItems = null;

		private readonly StringBuilder _searchBuilder = new();
		private string _searchText = string.Empty;
		private string _title = "List";
		private int _scrollOffset = 0;

		// Local state
		private int _selectedIndex = -1;

		// Mouse interaction state
		private int _hoveredIndex = -1;                        // Mouse hover tracking
		private readonly object _clickLock = new object();     // Thread safety for double-click detection
		private DateTime _lastClickTime = DateTime.MinValue;   // Double-click detection (protected by _clickLock)
		private int _lastClickIndex = -1;                      // Double-click detection (protected by _clickLock)

		// Interaction configuration
		private bool _checkboxMode = false;
		private bool _hoverHighlightsItems = true;
		private bool _autoHighlightOnFocus = true;
		private int _mouseWheelScrollSpeed = ControlDefaults.DefaultMinimumVisibleItems;
		private bool _doubleClickActivates = true;
		private int _doubleClickThresholdMs = ControlDefaults.DefaultDoubleClickThresholdMs;

		// Performance: Cache for expensive text measurement operations
		private readonly TextMeasurementCache _textMeasurementCache = new(AnsiConsoleHelper.StripSpectreLength);

		// Thread-safety: _items may be modified from background threads (async build callbacks, etc.)
		// while the render loop reads from the main thread.
		private readonly object _itemsLock = new();

		// Read-only helpers
		private int CurrentSelectedIndex => _selectedIndex;
		private int CurrentScrollOffset => _scrollOffset;

		#endregion

		#region Private Helpers

		// Helper to set scroll offset
		private void SetScrollOffset(int offset)
		{
			_scrollOffset = Math.Max(0, offset);
		}

		// Helper to get cached text length (expensive operation)
		private int GetCachedTextLength(string text)
		{
			return _textMeasurementCache.GetCachedLength(text);
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

		#endregion

		#region Constructors

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

		#endregion

		#region Events

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
		/// Occurs when the control is clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <summary>
		/// Occurs when the mouse enters the control area.
		/// </summary>
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future implementation
		public event EventHandler<MouseEventArgs>? MouseEnter;
#pragma warning restore CS0414

		/// <summary>
		/// Occurs when the mouse leaves the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Occurs when the mouse moves over the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		/// <summary>
		/// Occurs when an item is hovered by the mouse.
		/// The index is the hovered item index, or -1 if mouse left all items.
		/// </summary>
		public event EventHandler<int>? ItemHovered;

		/// <summary>
		/// Occurs when an item is double-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the background color of the list control.
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
		/// Gets or sets the foreground color of the list control.
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
		/// Gets or sets whether the list control is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
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
		/// Gets or sets the collection of items displayed in the list.
		/// </summary>
		public List<ListItem> Items
		{
			get => _items;
			set
			{
				lock (_itemsLock) { _items = value; }
				_textMeasurementCache.InvalidateCache(); // Clear cache when items change
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
		/// Gets or sets whether items display checkboxes ([x]/[ ]) and Space toggles checked state.
		/// </summary>
		public bool CheckboxMode
		{
			get => _checkboxMode;
			set { _checkboxMode = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Occurs when the checked state of any item changes.
		/// </summary>
		public event EventHandler<EventArgs>? CheckedItemsChanged;

		/// <summary>
		/// Returns all items where IsChecked is true.
		/// </summary>
		public List<ListItem> GetCheckedItems()
			=> _items.Where(i => i.IsChecked).ToList();

		/// <summary>
		/// Sets all items' IsChecked state and fires CheckedItemsChanged once.
		/// </summary>
		public void SetAllChecked(bool value)
		{
			foreach (var item in _items)
				item.IsChecked = value;
			CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
			Container?.Invalidate(true);
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

		/// <summary>
		/// Gets or sets the list items as simple strings.
		/// </summary>
		public List<string> StringItems
		{
			get => _items.Select(i => i.Text).ToList();
			set
			{
				_items = value.Select(text => new ListItem(text)).ToList();
				_textMeasurementCache.InvalidateCache(); // Clear cache when items change
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

		#endregion

		#region Item Management

		/// <summary>
		/// Adds a ListItem to the list.
		/// </summary>
		/// <param name="item">The item to add.</param>
		public void AddItem(ListItem item)
		{
			lock (_itemsLock) { _items.Add(item); }
			_textMeasurementCache.InvalidateCache(); // Clear cache when items change
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
			lock (_itemsLock) { _items.Clear(); }
			_textMeasurementCache.InvalidateCache(); // Clear cache when items cleared

			// Clear state via services (single source of truth)
			_selectedIndex = -1;
			SetScrollOffset(0);

			Container?.Invalidate(true);

			if (_isSelectable)
			{
				SelectedIndexChanged?.Invoke(this, -1);
				SelectedItemChanged?.Invoke(this, null);
				SelectedValueChanged?.Invoke(this, null);
			}
		}

		#endregion

		#region Lifecycle

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			// Clear all event handlers to prevent memory leaks
			SelectedIndexChanged = null;
			SelectedItemChanged = null;
			SelectedValueChanged = null;
			ItemActivated = null;
			CheckedItemsChanged = null;
			MouseClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
			ItemHovered = null;
			MouseDoubleClick = null;
			GotFocus = null;
			LostFocus = null;
		}

		#endregion

		#region IFocusableControl

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
				// Auto-select first item on focus gain if nothing is selected
				if (_autoHighlightOnFocus && _isSelectable && _selectedIndex == -1 && _items.Count > 0)
				{
					_selectedIndex = 0;
					SelectedIndexChanged?.Invoke(this, 0);
					SelectedItemChanged?.Invoke(this, SelectedItem);
					SelectedValueChanged?.Invoke(this, SelectedValue);
					EnsureSelectedItemVisible();
				}

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
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

		#endregion
	}
}
