// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Size = System.Drawing.Size;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A dropdown/combobox control that displays a list of selectable items.
	/// Supports keyboard navigation, type-ahead search, and custom item formatting.
	/// </summary>
	public partial class DropdownControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IColorRoleableControl
	{

		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set => SetProperty(ref _role, value);
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set => SetProperty(ref _colorRoleMode, value);
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set => SetProperty(ref _outline, value);
		}

		#endregion

		private readonly TimeSpan _searchResetDelay = TimeSpan.FromSeconds(1.5);
		private bool _autoAdjustWidth = true;
		private Color? _backgroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private Color? _highlightBackgroundColorValue;
		private Color? _highlightForegroundColorValue;
		private bool _isDropdownOpen = false;
		private bool _isEnabled = true;
		private ItemFormatterEvent? _itemFormatter;
		private List<DropdownItem> _items = new List<DropdownItem>();
		private readonly object _dropdownLock = new();
		private DateTime _lastKeyTime = DateTime.MinValue;
		private int _maxVisibleItems = 5;
		private string _prompt = "Select an item:";
		private readonly StringBuilder _searchBuilder = new();
		private string _searchText = string.Empty;

		// Local state - controls own their selection/highlight state
		private int _selectedIndex = -1;
		private int _highlightedIndex = -1;
		private int _dropdownScrollOffset = 0;

		// Mouse state tracking
		private bool _isHeaderPressed;
		private int _mouseHoveredIndex = -1;
		private bool _dismissedByOutsideClick;

		// Portal state for dropdown overlay
		private LayoutNode? _dropdownPortal;
		private DropdownPortalContent? _portalContent;
		private Rectangle _dropdownBounds;
		private bool _opensUpward;
		private LayoutRect _lastLayoutBounds;
		private int _lastHeaderWidth;
		private int _lastAlignOffset;

		private static string? GetItemValue(DropdownItem item) => item.Value ?? item.Text;

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
				if (_items.Count > 0)
				{
					_selectedIndex = 0;
					_highlightedIndex = 0;
				}
				SyncItemOwners();
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
				if (_items.Count > 0)
				{
					_selectedIndex = 0;
					_highlightedIndex = 0;
				}
				SyncItemOwners();
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

		/// <summary>Async counterpart of <see cref="SelectedIndexChanged"/>.</summary>
		public event Core.AsyncEventHandler<int>? SelectedIndexChangedAsync;

		/// <summary>
		/// Occurs when the selected item changes.
		/// </summary>
		public event EventHandler<DropdownItem?>? SelectedItemChanged;

		/// <summary>Async counterpart of <see cref="SelectedItemChanged"/>.</summary>
		public event Core.AsyncEventHandler<DropdownItem?>? SelectedItemChangedAsync;

		/// <summary>
		/// Occurs when the selected value (text) changes.
		/// </summary>
		public event EventHandler<string?>? SelectedValueChanged;

		/// <summary>Async counterpart of <see cref="SelectedValueChanged"/>.</summary>
		public event Core.AsyncEventHandler<string?>? SelectedValueChangedAsync;

		#region IMouseAwareControl Events and Properties

#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Occurs when the control is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

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
		public int? ContentHeight
		{
			get
			{
				// Constant height - header (1 line) plus margins
				// Dropdown items render via portal overlay, not affecting control height
				return 1 + Margin.Top + Margin.Bottom;
			}
		}

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				return calculateHeaderWidth(null) + Margin.Left + Margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets whether the control automatically adjusts its width to fit content.
		/// </summary>
		public bool AutoAdjustWidth
		{
			get => _autoAdjustWidth;
			set => SetProperty(ref _autoAdjustWidth, value);
		}

		/// <summary>
		/// Gets or sets the background color of the dropdown in its normal state.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		// Container inherited from BaseControl

		/// <summary>
		/// Gets or sets the background color when the control has focus.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => ResolveBackground(ColorRoleState.Focused);
			set => SetProperty(ref _focusedBackgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color when the control has focus.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => ResolveForeground(ColorRoleState.Focused);
			set => SetProperty(ref _focusedForegroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color of the dropdown in its normal state.
		/// </summary>
		public Color ForegroundColor
		{
			get => ResolveForeground(CurrentRoleState);
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Computes the current role state from the dropdown's enabled/focus state so role colours
		/// reflect the same visual state the renderer paints.
		/// </summary>
		private ColorRoleState CurrentRoleState =>
			!_isEnabled ? ColorRoleState.Disabled : (ComputeHasFocus() ? ColorRoleState.Focused : ColorRoleState.Normal);

		/// <summary>
		/// Resolves the painted header foreground. The dropdown header paints text on its fill, so the
		/// role contributes <see cref="ColorResolver.ColorRoleTextOnBackground"/>. Explicit override wins,
		/// then the role, then the legacy per-state default. For <see cref="ColorRole.Default"/> (no
		/// role) the role helper returns null, so this is the legacy value. Pure in <paramref name="state"/>.
		/// </summary>
		private Color ResolveForeground(ColorRoleState state)
		{
			if (state == ColorRoleState.Disabled)
			{
				return ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.DropdownDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			if (state == ColorRoleState.Focused)
			{
				return _focusedForegroundColorValue
					?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.DropdownFocusedForegroundColor ?? Color.White;
			}
			return _foregroundColorValue
				?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
				?? Container?.GetConsoleWindowSystem?.Theme?.DropdownForegroundColor ?? Color.White;
		}

		/// <summary>
		/// Resolves the painted header background fill: explicit override, then the role background
		/// (<see cref="ColorResolver.ColorRoleBackground"/>), then the legacy per-state default.
		/// Pure in <paramref name="state"/>.
		/// </summary>
		private Color ResolveBackground(ColorRoleState state)
		{
			if (state == ColorRoleState.Disabled)
			{
				return ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.DropdownDisabledBackgroundColor ?? Color.Grey;
			}
			if (state == ColorRoleState.Focused)
			{
				return _focusedBackgroundColorValue
					?? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.DropdownFocusedBackgroundColor ?? Color.Blue;
			}
			return _backgroundColorValue
				?? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
				?? ColorResolver.ResolveBackground(null, Container);
		}

		/// <summary>
		/// Gets or sets the background color for the highlighted item in the dropdown list (keyboard or mouse).
		/// </summary>
		public Color HighlightBackgroundColor
		{
			get => _highlightBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.DropdownHighlightBackgroundColor ?? Color.Blue;
			set => SetProperty(ref _highlightBackgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color for the highlighted item in the dropdown list (keyboard or mouse).
		/// </summary>
		public Color HighlightForegroundColor
		{
			get => _highlightForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.DropdownHighlightForegroundColor ?? Color.White;
			set => SetProperty(ref _highlightForegroundColorValue, (Color?)value);
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

		/// <summary>
		/// Gets or sets whether the dropdown is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <summary>
		/// Called by an owned <see cref="DropdownItem"/> when one of its display properties
		/// changes, so the control re-renders with the appropriate invalidation level.
		/// </summary>
		internal void OnItemInvalidated(Invalidation work) => Container?.Invalidate(work);

		/// <summary>
		/// Sets the <see cref="DropdownItem.Owner"/> back-reference on every current item so
		/// their display-property setters notify this control. Called after bulk item mutations.
		/// </summary>
		private void SyncItemOwners()
		{
			lock (_dropdownLock)
			{
				foreach (var it in _items)
					it.Owner = this;
			}
		}

		/// <summary>
		/// Gets or sets the custom item formatter delegate for rendering dropdown items.
		/// </summary>
		public ItemFormatterEvent? ItemFormatter
		{
			get => _itemFormatter;
			set => SetProperty(ref _itemFormatter, value);
		}

		/// <summary>
		/// Gets or sets the list of dropdown items.
		/// </summary>
		public List<DropdownItem> Items
		{
			get { lock (_dropdownLock) { return _items; } }
			set
			{
				lock (_dropdownLock) { _items = value; }
				SyncItemOwners();
				OnPropertyChanged();
				int currentSel = CurrentSelectedIndex;
				if (currentSel >= _items.Count)
				{
					int oldIndex = currentSel;
					int newSel = _items.Count > 0 ? 0 : -1;
					_selectedIndex = newSel;
					_highlightedIndex = newSel;
					if (oldIndex != newSel)
					{
						var log = Container?.GetConsoleWindowSystem?.LogService;
						Core.AsyncEvent.Raise(SelectedIndexChanged, SelectedIndexChangedAsync, this, newSel, log);
						Core.AsyncEvent.Raise(SelectedItemChanged, SelectedItemChangedAsync, this, newSel >= 0 && newSel < _items.Count ? _items[newSel] : null, log);
						Core.AsyncEvent.Raise(SelectedValueChanged, SelectedValueChangedAsync, this, newSel >= 0 && newSel < _items.Count ? GetItemValue(_items[newSel]) : null, log);
					}
				}
				Invalidate(Invalidation.Relayout);
			}
		}

		// Margin inherited from BaseControl

		/// <summary>
		/// Gets or sets the maximum number of items visible in the dropdown list without scrolling.
		/// </summary>
		public int MaxVisibleItems
		{
			get => _maxVisibleItems;
			set => SetProperty(ref _maxVisibleItems, value, v => Math.Max(1, v));
		}

		/// <summary>
		/// Gets or sets the prompt text displayed in the dropdown header.
		/// </summary>
		public string Prompt
		{
			get => _prompt;
			set => SetProperty(ref _prompt, value);
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
					OnPropertyChanged();
					Invalidate(Invalidation.Relayout);

					// Ensure selected item is visible when dropdown is open
					if (_isDropdownOpen && value >= 0)
					{
						EnsureSelectedItemVisible();
					}

					// Trigger events
					if (oldIndex != value)
					{
						var log = Container?.GetConsoleWindowSystem?.LogService;
						Core.AsyncEvent.Raise(SelectedIndexChanged, SelectedIndexChangedAsync, this, value, log);
						Core.AsyncEvent.Raise(SelectedItemChanged, SelectedItemChangedAsync, this, (value >= 0 && value < _items.Count) ?
							_items[value] : null, log);

						string? selectedValue = (value >= 0 && value < _items.Count) ?
							GetItemValue(_items[value]) : null;
						Core.AsyncEvent.Raise(SelectedValueChanged, SelectedValueChangedAsync, this, selectedValue, log);
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
				return sel >= 0 && sel < _items.Count ? GetItemValue(_items[sel]) : null;
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
					if (_items[i].Value == value || _items[i].Text == value)
					{
						SelectedIndex = i;
						break;
					}
				}
			}
		}

		// StickyPosition inherited from BaseControl

		/// <summary>
		/// Gets or sets the items as a list of strings for simplified access.
		/// </summary>
		public List<string> StringItems
		{
			get { lock (_dropdownLock) { return _items.Select(i => i.Text).ToList(); } }
			set
			{
				lock (_dropdownLock) { _items = value.Select(text => new DropdownItem(text)).ToList(); }
				SyncItemOwners();
				OnPropertyChanged();
				int currentSel = CurrentSelectedIndex;
				if (currentSel >= _items.Count)
				{
					int oldIndex = currentSel;
					int newSel = _items.Count > 0 ? 0 : -1;
					_selectedIndex = newSel;
					_highlightedIndex = newSel;
					if (oldIndex != newSel)
					{
						var log = Container?.GetConsoleWindowSystem?.LogService;
						Core.AsyncEvent.Raise(SelectedIndexChanged, SelectedIndexChangedAsync, this, newSel, log);
						Core.AsyncEvent.Raise(SelectedItemChanged, SelectedItemChangedAsync, this, newSel >= 0 && newSel < _items.Count ? _items[newSel] : null, log);
						Core.AsyncEvent.Raise(SelectedValueChanged, SelectedValueChangedAsync, this, newSel >= 0 && newSel < _items.Count ? GetItemValue(_items[newSel]) : null, log);
					}
				}
				Invalidate(Invalidation.Relayout);
			}
		}

		// Name, Tag, Visible, Width inherited from BaseControl

		/// <summary>
		/// Adds a new item to the dropdown list.
		/// </summary>
		/// <param name="item">The dropdown item to add.</param>
		public void AddItem(DropdownItem item)
		{
			int count;
			lock (_dropdownLock)
			{
				item.Owner = this;
				_items.Add(item);
				count = _items.Count;
			}
			if (CurrentSelectedIndex == -1 && count == 1)
			{
				_selectedIndex = 0;
				_highlightedIndex = 0;
				var log = Container?.GetConsoleWindowSystem?.LogService;
				Core.AsyncEvent.Raise(SelectedIndexChanged, SelectedIndexChangedAsync, this, 0, log);
				Core.AsyncEvent.Raise(SelectedItemChanged, SelectedItemChangedAsync, this, item, log);
				Core.AsyncEvent.Raise(SelectedValueChanged, SelectedValueChangedAsync, this, GetItemValue(item), log);
			}
			Invalidate(Invalidation.Relayout);
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
			lock (_dropdownLock) { _items.Clear(); }

			// Clear local selection state
			int oldIndex = _selectedIndex;
			_selectedIndex = -1;
			_highlightedIndex = -1;

			// Clear scroll state
			_dropdownScrollOffset = 0;

			Invalidate(Invalidation.Relayout);

			if (oldIndex != -1)
			{
				var log = Container?.GetConsoleWindowSystem?.LogService;
				Core.AsyncEvent.Raise(SelectedIndexChanged, SelectedIndexChangedAsync, this, -1, log);
				Core.AsyncEvent.Raise(SelectedItemChanged, SelectedItemChangedAsync, this, null, log);
				Core.AsyncEvent.Raise(SelectedValueChanged, SelectedValueChangedAsync, this, null, log);
			}
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = ContentWidth ?? 0;
			int height = ContentHeight ?? 1;
			return new System.Drawing.Size(width, height);
		}

		private Window? _subscribedWindow;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				var newWindow = this.GetParentWindow();
				if (!ReferenceEquals(newWindow, _subscribedWindow))
				{
					if (_subscribedWindow != null)
						_subscribedWindow.FocusManager.FocusChanged -= OnFocusChanged;
					_subscribedWindow = newWindow;
					if (_subscribedWindow != null)
						_subscribedWindow.FocusManager.FocusChanged += OnFocusChanged;
				}
			}
		}

		private void OnFocusChanged(object? sender, Core.FocusChangedEventArgs e)
		{
			if (ReferenceEquals(e.Previous, this))
			{
				_mouseHoveredIndex = -1;
				_isHeaderPressed = false;
				_highlightedIndex = CurrentSelectedIndex;
				if (_isDropdownOpen)
					IsDropdownOpen = false;
			}
		}

		// Calculate header width using the standard ComboBox pattern:
		// sized to the widest item text for stable layout across selection changes.
		private int calculateHeaderWidth(int? availableWidth)
		{
			if (Width.HasValue)
				return Width.Value;
			if (HorizontalAlignment == HorizontalAlignment.Stretch && availableWidth.HasValue)
				return availableWidth.Value;

			List<DropdownItem> snapshot;
			lock (_dropdownLock) { snapshot = _items.ToList(); }

			// Find the longest item text (for stable width across selection changes)
			string longestText = "(None)";
			int longestLen = Parsing.MarkupParser.StripLength(longestText);
			foreach (var item in snapshot)
			{
				int len = Parsing.MarkupParser.StripLength(item.Text);
				if (len > longestLen) { longestLen = len; longestText = item.Text; }
			}

			// Build header string with longest item and measure exact display width
			string arrow = ControlDefaults.DropdownClosedArrow;
			string header = $"{_prompt} {longestText} {arrow}";
			return Parsing.MarkupParser.StripLength(header);
		}

		// Calculate portal (dropdown list) width based on all items with icons and indicators.
		private int calculatePortalWidth(int? availableWidth)
		{
			if (HorizontalAlignment == HorizontalAlignment.Stretch && availableWidth.HasValue)
				return availableWidth.Value;

			List<DropdownItem> snapshot;
			lock (_dropdownLock) { snapshot = _items.ToList(); }

			// Calculate maximum item width including icons, selection indicators, and padding
			int maxItemWidth = 0;
			foreach (var item in snapshot)
			{
				// Base length includes text
				int itemLength = Parsing.MarkupParser.StripLength(item.Text);

				// Add space for selection indicator (2 chars: "● " or "  ")
				itemLength += 2;

				// Add space for icon if present
				if (item.Icon != null)
				{
					itemLength += Parsing.MarkupParser.StripLength(item.Icon) + 1; // +1 for space after icon
				}

				// Add some padding for comfortable viewing
				itemLength += 2;

				if (itemLength > maxItemWidth)
					maxItemWidth = itemLength;
			}

			// Portal should be at least as wide as the header
			int headerWidth = calculateHeaderWidth(availableWidth);
			int portalWidth = Math.Max(maxItemWidth, headerWidth);

			// If width is specified, use it as minimum
			if (Width.HasValue)
				portalWidth = Math.Max(portalWidth, Width.Value);

			return portalWidth;
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
	}
}
