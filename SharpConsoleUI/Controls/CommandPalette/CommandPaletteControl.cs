// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Text;

#pragma warning disable CS1591

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A fuzzy-search command palette control similar to VS Code's Ctrl+P.
	/// Displays a search bar and filtered list of items with keyboard navigation.
	/// </summary>
	public partial class CommandPaletteControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		/// <summary>
		/// Creates a fluent builder for constructing a CommandPaletteControl.
		/// </summary>
		public static Builders.CommandPaletteBuilder Create()
		{
			return new Builders.CommandPaletteBuilder();
		}

		#region Fields

		private List<CommandPaletteItem> _items = new();
		private List<(CommandPaletteItem Item, FuzzyMatchResult Result)> _filteredItems = new();
		private readonly StringBuilder _searchBuilder = new();
		private string _searchText = string.Empty;
		private int _selectedIndex;
		private int _scrollOffset;
		private bool _hasFocus;
		private bool _isEnabled = true;
		private bool _isVisible;

		private string _placeholder = ControlDefaults.CommandPaletteDefaultPlaceholder;
		private int _maxVisibleItems = ControlDefaults.CommandPaletteDefaultMaxVisibleItems;
		private int _paletteWidth = ControlDefaults.CommandPaletteDefaultWidth;
		private bool _showCategories;
		private bool _showShortcuts = true;

		// Portal state
		private LayoutNode? _portal;
		private CommandPalettePortalContent? _portalContent;

		// Performance: cache text measurements
		private readonly TextMeasurementCache _textMeasurementCache = new(Parsing.MarkupParser.StripLength);

		#endregion

		#region Constructors

		public CommandPaletteControl(IEnumerable<CommandPaletteItem>? items = null)
		{
			if (items != null)
				_items.AddRange(items);
			RefreshFilteredItems();
		}

		#endregion

		#region Properties

		public IReadOnlyList<CommandPaletteItem> Items => _items.AsReadOnly();

		public CommandPaletteItem? SelectedItem
		{
			get
			{
				if (_selectedIndex >= 0 && _selectedIndex < _filteredItems.Count)
					return _filteredItems[_selectedIndex].Item;
				return null;
			}
		}

		public string SearchText => _searchText;

		public string Placeholder
		{
			get => _placeholder;
			set => _placeholder = value ?? ControlDefaults.CommandPaletteDefaultPlaceholder;
		}

		public int MaxVisibleItems
		{
			get => _maxVisibleItems;
			set => _maxVisibleItems = Math.Max(ControlDefaults.DefaultMinimumVisibleItems, value);
		}

		public int PaletteWidth
		{
			get => _paletteWidth;
			set => _paletteWidth = Math.Max(ControlDefaults.DefaultWindowMinimumWidth, value);
		}

		public bool ShowCategories
		{
			get => _showCategories;
			set => _showCategories = value;
		}

		public bool ShowShortcuts
		{
			get => _showShortcuts;
			set => _showShortcuts = value;
		}

		public bool IsVisible => _isVisible;

		public override int? ContentWidth => _paletteWidth;

		#endregion

		#region Events

		public event EventHandler<CommandPaletteItem>? ItemSelected;
		public event EventHandler<string>? SearchChanged;
		public event EventHandler? Dismissed;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		#pragma warning disable CS0067
		public event EventHandler<MouseEventArgs>? MouseClick;
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
		public event EventHandler<MouseEventArgs>? MouseRightClick;
		public event EventHandler<MouseEventArgs>? MouseEnter;
		public event EventHandler<MouseEventArgs>? MouseLeave;
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		#endregion

		#region IFocusableControl

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_hasFocus == value) return;
				_hasFocus = value;
				if (value)
					GotFocus?.Invoke(this, EventArgs.Empty);
				else
					LostFocus?.Invoke(this, EventArgs.Empty);
				Container?.Invalidate(true);
			}
		}

		public bool CanReceiveFocus => _isEnabled && _isVisible;

		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		#endregion

		#region IInteractiveControl

		public bool IsEnabled
		{
			get => _isEnabled;
			set => _isEnabled = value;
		}

		#endregion

		#region IMouseAwareControl

		public bool WantsMouseEvents => _isVisible;
		public bool CanFocusWithMouse => _isVisible;

		#endregion

		#region Public Methods

		public void Show()
		{
			if (_isVisible) return;

			_isVisible = true;
			ClearSearch();
			_selectedIndex = 0;
			_scrollOffset = 0;

			var window = FindContainingWindow();
			if (window != null)
			{
				_portalContent = new CommandPalettePortalContent(this);
				_portal = window.CreatePortal(this, _portalContent);
			}

			Container?.Invalidate(true);
		}

		public void Hide()
		{
			if (!_isVisible) return;

			_isVisible = false;

			if (_portal != null)
			{
				var window = FindContainingWindow();
				window?.RemovePortal(this, _portal);
				_portal = null;
				_portalContent = null;
			}

			Container?.Invalidate(true);
		}

		public void SetItems(IEnumerable<CommandPaletteItem> items)
		{
			_items = new List<CommandPaletteItem>(items);
			RefreshFilteredItems();
			_selectedIndex = 0;
			_scrollOffset = 0;
			Container?.Invalidate(true);
		}

		public void ClearSearch()
		{
			_searchBuilder.Clear();
			_searchText = string.Empty;
			RefreshFilteredItems();
			_selectedIndex = 0;
			_scrollOffset = 0;
		}

		#endregion

		#region Private Helpers

		private void RefreshFilteredItems()
		{
			_filteredItems = FuzzyMatcher.FilterAndSort(_searchText, _items);
		}

		private void SelectCurrentItem()
		{
			if (_selectedIndex >= 0 && _selectedIndex < _filteredItems.Count)
			{
				var item = _filteredItems[_selectedIndex].Item;
				if (item.IsEnabled)
				{
					ItemSelected?.Invoke(this, item);
					Hide();
					item.Action?.Invoke();
				}
			}
		}

		private void DismissPalette()
		{
			Hide();
			Dismissed?.Invoke(this, EventArgs.Empty);
		}

		private void EnsureSelectedVisible()
		{
			if (_selectedIndex < _scrollOffset)
				_scrollOffset = _selectedIndex;
			else if (_selectedIndex >= _scrollOffset + _maxVisibleItems)
				_scrollOffset = _selectedIndex - _maxVisibleItems + 1;
		}

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

		private int GetCachedTextLength(string text)
		{
			return _textMeasurementCache.GetCachedLength(text);
		}

		#endregion
	}
}
