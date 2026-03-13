// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A navigation view control with a left navigation pane and a right content area.
	/// Inspired by WinUI's NavigationView pattern, encapsulating nav item selection,
	/// content switching, and header updates into a single reusable control.
	/// </summary>
	public partial class NavigationView : BaseControl, IInteractiveControl,
		IFocusableControl, IMouseAwareControl, IContainer, IContainerControl,
		IFocusTrackingContainer, IDirectionalFocusControl
	{
		private readonly HorizontalGridControl _grid;
		private readonly ColumnContainer _navColumn;
		private readonly ColumnContainer _contentColumn;
		private readonly MarkupControl _paneHeader;
		private readonly MarkupControl _contentHeader;
		private readonly ScrollablePanelControl _contentPanel;

		private readonly List<NavigationItem> _items = new();
		private readonly List<MarkupControl> _itemControls = new();
		private readonly Dictionary<NavigationItem, Action<ScrollablePanelControl>> _contentFactories = new();
		private readonly object _itemsLock = new();

		private int _selectedIndex = -1;

		// Configuration
		private int _navPaneWidth = 26;
		private string? _paneHeaderText;
		private Color _selectedItemBackground = new Color(40, 50, 80);
		private Color _selectedItemForeground = Color.White;
		private Color _itemForeground = Color.Grey;
		private char _selectionIndicator = '▸';
		private BorderStyle _contentBorderStyle = BorderStyle.Rounded;
		private Color? _contentBorderColor;
		private Color? _contentBackgroundColor;
		private Padding _contentPadding = new Padding(1, 0, 1, 0);
		private bool _showContentHeader = true;

		// IContainer properties
		private Color? _backgroundColorValue;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="NavigationView"/> class.
		/// </summary>
		public NavigationView()
		{
			_grid = new HorizontalGridControl();

			_navColumn = new ColumnContainer(_grid);
			_contentColumn = new ColumnContainer(_grid);

			_paneHeader = new MarkupControl(new List<string>());
			_paneHeader.Margin = new Margin(0, 1, 0, 0);

			_contentHeader = new MarkupControl(new List<string>());
			_contentHeader.Margin = new Margin(1, 1, 0, 0);

			_contentPanel = new ScrollablePanelControl
			{
				BorderStyle = _contentBorderStyle,
				Padding = _contentPadding,
				VerticalAlignment = VerticalAlignment.Fill
			};

			// Build column structure
			_navColumn.AddContent(_paneHeader);
			_contentColumn.AddContent(_contentHeader);
			_contentColumn.AddContent(_contentPanel);

			_grid.AddColumn(_navColumn);
			_grid.AddColumn(_contentColumn);

			_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
			_grid.VerticalAlignment = VerticalAlignment.Fill;

			SyncInternalControls();
		}

		/// <summary>
		/// Gets the internal grid control for layout integration.
		/// </summary>
		internal HorizontalGridControl InternalGrid => _grid;

		#region Configuration Properties

		/// <summary>
		/// Gets or sets the width of the left navigation pane in characters.
		/// </summary>
		public int NavPaneWidth
		{
			get => _navPaneWidth;
			set { if (SetProperty(ref _navPaneWidth, Math.Max(10, value))) SyncInternalControls(); }
		}

		/// <summary>
		/// Gets or sets the markup text shown as the navigation pane header.
		/// </summary>
		public string? PaneHeader
		{
			get => _paneHeaderText;
			set
			{
				_paneHeaderText = value;
				if (value != null)
					_paneHeader.SetContent(new List<string> { value, "" });
				else
					_paneHeader.SetContent(new List<string>());
				OnPropertyChanged();
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color for the selected navigation item.
		/// </summary>
		public Color SelectedItemBackground
		{
			get => _selectedItemBackground;
			set { if (SetProperty(ref _selectedItemBackground, value)) RefreshAllItemMarkup(); }
		}

		/// <summary>
		/// Gets or sets the foreground color for the selected navigation item.
		/// </summary>
		public Color SelectedItemForeground
		{
			get => _selectedItemForeground;
			set { if (SetProperty(ref _selectedItemForeground, value)) RefreshAllItemMarkup(); }
		}

		/// <summary>
		/// Gets or sets the foreground color for unselected navigation items.
		/// </summary>
		public Color ItemForeground
		{
			get => _itemForeground;
			set { if (SetProperty(ref _itemForeground, value)) RefreshAllItemMarkup(); }
		}

		/// <summary>
		/// Gets or sets the character used as the selection indicator prefix.
		/// </summary>
		public char SelectionIndicator
		{
			get => _selectionIndicator;
			set { if (SetProperty(ref _selectionIndicator, value)) RefreshAllItemMarkup(); }
		}

		/// <summary>
		/// Gets or sets the border style for the content panel.
		/// </summary>
		public BorderStyle ContentBorderStyle
		{
			get => _contentBorderStyle;
			set { if (SetProperty(ref _contentBorderStyle, value)) SyncInternalControls(); }
		}

		/// <summary>
		/// Gets or sets the border color for the content panel.
		/// </summary>
		public Color? ContentBorderColor
		{
			get => _contentBorderColor;
			set { if (SetProperty(ref _contentBorderColor, value)) SyncInternalControls(); }
		}

		/// <summary>
		/// Gets or sets the background color for the content panel.
		/// </summary>
		public Color? ContentBackgroundColor
		{
			get => _contentBackgroundColor;
			set { if (SetProperty(ref _contentBackgroundColor, value)) SyncInternalControls(); }
		}

		/// <summary>
		/// Gets or sets the padding inside the content panel.
		/// </summary>
		public Padding ContentPadding
		{
			get => _contentPadding;
			set { _contentPadding = value; SyncInternalControls(); OnPropertyChanged(); Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets whether the content header (title + subtitle) is shown.
		/// </summary>
		public bool ShowContentHeader
		{
			get => _showContentHeader;
			set
			{
				if (_showContentHeader == value) return;
				_showContentHeader = value;
				_contentHeader.Visible = value;
				OnPropertyChanged();
				Invalidate(true);
			}
		}

		#endregion

		#region Item Management

		/// <summary>
		/// Adds a navigation item to the control.
		/// </summary>
		/// <param name="item">The navigation item to add.</param>
		public void AddItem(NavigationItem item)
		{
			MarkupControl itemControl;
			int count;
			lock (_itemsLock)
			{
				_items.Add(item);
				count = _items.Count;

				itemControl = new MarkupControl(new List<string>
				{
					FormatNavItem(item, false)
				});

				// Wire click handler
				int idx = count - 1;
				itemControl.MouseClick += (_, _) =>
				{
					if (item.IsEnabled)
						SelectedIndex = idx;
				};

				_itemControls.Add(itemControl);
			}

			_navColumn.AddContent(itemControl);

			// Auto-select the first item added
			if (count == 1)
				SelectedIndex = 0;

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Adds a navigation item with the specified properties.
		/// </summary>
		/// <returns>The created NavigationItem.</returns>
		public NavigationItem AddItem(string text, string? icon = null, string? subtitle = null)
		{
			var item = new NavigationItem(text, icon, subtitle);
			AddItem(item);
			return item;
		}

		/// <summary>
		/// Removes the navigation item at the specified index.
		/// </summary>
		public void RemoveItem(int index)
		{
			lock (_itemsLock)
			{
				if (index < 0 || index >= _items.Count) return;

				var item = _items[index];
				var control = _itemControls[index];
				_items.RemoveAt(index);
				_itemControls.RemoveAt(index);
				_contentFactories.Remove(item);
				_navColumn.RemoveContent(control);

				// Adjust selected index
				if (_selectedIndex == index)
				{
					_selectedIndex = _items.Count > 0 ? Math.Min(index, _items.Count - 1) : -1;
					if (_selectedIndex >= 0) ApplySelection(_selectedIndex);
				}
				else if (_selectedIndex > index)
				{
					_selectedIndex--;
				}
			}

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Removes the specified navigation item.
		/// </summary>
		public void RemoveItem(NavigationItem item)
		{
			int index;
			lock (_itemsLock) { index = _items.IndexOf(item); }
			if (index >= 0) RemoveItem(index);
		}

		/// <summary>
		/// Removes all navigation items.
		/// </summary>
		public void ClearItems()
		{
			lock (_itemsLock)
			{
				foreach (var control in _itemControls)
					_navColumn.RemoveContent(control);

				_items.Clear();
				_itemControls.Clear();
				_contentFactories.Clear();
				_selectedIndex = -1;
			}

			_contentPanel.ClearContents();
			_contentHeader.SetContent(new List<string>());

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Gets the read-only list of navigation items.
		/// </summary>
		public IReadOnlyList<NavigationItem> Items
		{
			get { lock (_itemsLock) { return _items.ToList().AsReadOnly(); } }
		}

		/// <summary>
		/// Gets the currently selected navigation item, or null if none is selected.
		/// </summary>
		public NavigationItem? SelectedItem
		{
			get
			{
				lock (_itemsLock)
				{
					return _selectedIndex >= 0 && _selectedIndex < _items.Count
						? _items[_selectedIndex]
						: null;
				}
			}
		}

		/// <summary>
		/// Gets or sets the index of the currently selected navigation item.
		/// </summary>
		public int SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				NavigationItemChangingEventArgs? changingArgs = null;
				NavigationItemChangedEventArgs? changedArgs = null;

				// Phase 1: Validate and prepare event args under lock
				lock (_itemsLock)
				{
					if (_selectedIndex != value && value >= 0 && value < _items.Count && _items[value].IsEnabled)
					{
						var oldItem = _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
						var newItem = _items[value];
						changingArgs = new NavigationItemChangingEventArgs(_selectedIndex, value, oldItem, newItem);
					}
				}

				if (changingArgs == null) return;

				// Phase 2: Fire changing event (cancelable)
				SelectedItemChanging?.Invoke(this, changingArgs);
				if (changingArgs.Cancel) return;

				// Phase 3: Commit change under lock
				lock (_itemsLock)
				{
					if (value >= 0 && value < _items.Count)
					{
						var oldIndex = _selectedIndex;
						_selectedIndex = value;
						OnPropertyChanged();
						ApplySelection(value);
						changedArgs = new NavigationItemChangedEventArgs(
							changingArgs.OldIndex, changingArgs.NewIndex,
							changingArgs.OldItem, changingArgs.NewItem);
					}
				}

				// Phase 4: Fire changed event
				if (changedArgs != null)
				{
					SelectedItemChanged?.Invoke(this, changedArgs);
					Invalidate(true);
				}
			}
		}

		#endregion

		#region Content Management

		/// <summary>
		/// Registers a content factory delegate for a navigation item.
		/// The delegate is called to populate the content panel when the item is selected.
		/// </summary>
		public void SetItemContent(NavigationItem item, Action<ScrollablePanelControl> populate)
		{
			lock (_itemsLock)
			{
				_contentFactories[item] = populate;
			}

			// If this is the currently selected item, apply the content now
			if (SelectedItem == item)
			{
				_contentPanel.ClearContents();
				populate(_contentPanel);
				_contentPanel.ScrollToTop();
				Invalidate(true);
			}
		}

		/// <summary>
		/// Registers a content factory delegate for the navigation item at the specified index.
		/// </summary>
		public void SetItemContent(int index, Action<ScrollablePanelControl> populate)
		{
			lock (_itemsLock)
			{
				if (index >= 0 && index < _items.Count)
					SetItemContent(_items[index], populate);
			}
		}

		/// <summary>
		/// Gets the content panel for direct access.
		/// </summary>
		public ScrollablePanelControl ContentPanel => _contentPanel;

		#endregion

		#region Events

		/// <summary>
		/// Raised before the selected item changes. Can be canceled.
		/// </summary>
		public event EventHandler<NavigationItemChangingEventArgs>? SelectedItemChanging;

		/// <summary>
		/// Raised after the selected item has changed.
		/// </summary>
		public event EventHandler<NavigationItemChangedEventArgs>? SelectedItemChanged;

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set { _backgroundColorValue = value; OnPropertyChanged(); Invalidate(true); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => _foregroundColor;
			set { _foregroundColor = value; OnPropertyChanged(); Invalidate(true); }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		public bool IsDirty
		{
			get => _isDirty;
			set => _isDirty = value;
		}

		/// <inheritdoc/>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			_isDirty = true;
			Container?.Invalidate(redrawAll, this);
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			return Container?.GetVisibleHeightForControl(control);
		}

		/// <summary>
		/// Propagates gradient background state from the parent container,
		/// allowing child controls to preserve the gradient when painting.
		/// </summary>
		bool IContainer.HasGradientBackground => Container?.HasGradientBackground ?? false;

		#endregion

		#region IContainerControl Implementation

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			return new IWindowControl[] { _grid };
		}

		#endregion

		#region IWindowControl Overrides

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				_grid.Container = this;
			}
		}

		/// <inheritdoc/>
		public override int? ContentWidth => Width ?? _grid.ContentWidth;

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = Width ?? _grid.GetLogicalContentSize().Width;
			int height = _grid.GetLogicalContentSize().Height;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			_grid.Dispose();
		}

		#endregion

		#region Internal Helpers

		private void SyncInternalControls()
		{
			_navColumn.Width = _navPaneWidth;
			_contentPanel.BorderStyle = _contentBorderStyle;
			_contentPanel.Padding = _contentPadding;

			if (_contentBorderColor.HasValue)
				_contentPanel.BorderColor = _contentBorderColor;
			if (_contentBackgroundColor.HasValue)
				_contentPanel.BackgroundColor = _contentBackgroundColor.Value;
		}

		private string FormatNavItem(NavigationItem item, bool selected)
		{
			var icon = item.Icon != null ? $"{item.Icon} " : "";
			int padWidth = _navPaneWidth - 4 - (item.Icon != null ? item.Icon.Length + 1 : 0);
			if (padWidth < 1) padWidth = 1;

			var paddedText = (icon + item.Text).PadRight(padWidth);

			if (selected)
			{
				var bg = _selectedItemBackground;
				return $"[bold white on rgb({bg.R},{bg.G},{bg.B})]  {_selectionIndicator} {paddedText}[/]";
			}
			else
			{
				return $"[dim]    {paddedText}[/]";
			}
		}

		private void ApplySelection(int newIndex)
		{
			// Update all item markup
			for (int i = 0; i < _items.Count && i < _itemControls.Count; i++)
			{
				_itemControls[i].SetContent(new List<string>
				{
					FormatNavItem(_items[i], i == newIndex)
				});
			}

			// Update content header
			if (_showContentHeader && newIndex >= 0 && newIndex < _items.Count)
			{
				var item = _items[newIndex];
				var headerLines = new List<string> { $"[bold white]{item.Text}[/]" };
				if (item.Subtitle != null)
					headerLines.Add($"[dim]{item.Subtitle}[/]");
				_contentHeader.SetContent(headerLines);
			}

			// Switch content
			_contentPanel.ClearContents();
			if (newIndex >= 0 && newIndex < _items.Count && _contentFactories.TryGetValue(_items[newIndex], out var factory))
			{
				factory(_contentPanel);
			}
			_contentPanel.ScrollToTop();
		}

		private void RefreshAllItemMarkup()
		{
			lock (_itemsLock)
			{
				for (int i = 0; i < _items.Count && i < _itemControls.Count; i++)
				{
					_itemControls[i].SetContent(new List<string>
					{
						FormatNavItem(_items[i], i == _selectedIndex)
					});
				}
			}
		}

		#endregion
	}
}
