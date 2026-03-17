// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
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
		private readonly ScrollablePanelControl _navScrollPanel;
		private readonly MarkupControl _contentHeader;
		private readonly ScrollablePanelControl _contentPanel;

		private readonly HorizontalGridControl _contentHeaderGrid;
		private readonly ColumnContainer _contentHeaderTitleColumn;
		private readonly ColumnContainer _contentHeaderToolbarColumn;
		private readonly ToolbarControl _contentToolbar;

		private readonly List<NavigationItem> _items = new();
		private readonly List<MarkupControl> _itemControls = new();
		private readonly Dictionary<NavigationItem, Action<ScrollablePanelControl>> _contentFactories = new();
		private readonly object _itemsLock = new();
		private IReadOnlyList<NavigationItem>? _cachedReadOnlyItems;

		private int _selectedIndex = -1;
		private int _previousSelectedIndex = -1;

		// Configuration
		private int _navPaneWidth = ControlDefaults.DefaultNavigationViewPaneWidth;
		private string? _paneHeaderText;
		private Color _selectedItemBackground = new Color(
			ControlDefaults.NavigationViewSelectedBgR,
			ControlDefaults.NavigationViewSelectedBgG,
			ControlDefaults.NavigationViewSelectedBgB);
		private Color _selectedItemForeground = Color.White;
		private Color _itemForeground = Color.Grey;
		private char _selectionIndicator = '▸';
		private BorderStyle _contentBorderStyle = BorderStyle.Rounded;
		private Color? _contentBorderColor;
		private Color? _contentBackgroundColor;
		private Padding _contentPadding = new Padding(1, 0, 1, 0);
		private bool _showContentHeader = true;

		// Responsive display mode
		private NavigationViewDisplayMode _paneDisplayMode = NavigationViewDisplayMode.Auto;
		private int _expandedThreshold = ControlDefaults.DefaultNavigationViewExpandedThreshold;
		private int _compactThreshold = ControlDefaults.DefaultNavigationViewCompactThreshold;
		private int _compactPaneWidth = ControlDefaults.DefaultNavigationViewCompactPaneWidth;
		private bool _animateTransitions = true;
		private NavigationViewDisplayMode _currentDisplayMode = NavigationViewDisplayMode.Expanded;
		private int _effectiveNavWidth;
		private int _lastKnownWidth;

		// IContainer properties
		private Color? _backgroundColorValue;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="NavigationView"/> class.
		/// </summary>
		public NavigationView()
		{
			_effectiveNavWidth = _navPaneWidth;

			_grid = new HorizontalGridControl();

			_navColumn = new ColumnContainer(_grid);
			_contentColumn = new ColumnContainer(_grid);

			_paneHeader = new MarkupControl(new List<string>());
			_paneHeader.Margin = new Margin(0, 1, 0, 0);

			_navScrollPanel = new ScrollablePanelControl
			{
				BorderStyle = BorderStyle.None,
				Padding = new Padding(0, 0, 0, 0),
				VerticalAlignment = VerticalAlignment.Fill
			};

			_contentHeader = new MarkupControl(new List<string>());
			_contentHeader.Margin = new Margin(0, 0, 0, 0);

			_contentPanel = new ScrollablePanelControl
			{
				BorderStyle = _contentBorderStyle,
				Padding = _contentPadding,
				VerticalAlignment = VerticalAlignment.Fill
			};

			// Build content header grid (title on left, toolbar on right)
			_contentHeaderGrid = new HorizontalGridControl();
			_contentHeaderGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
			_contentHeaderGrid.Margin = new Margin(0, 1, 0, 0);

			_contentHeaderTitleColumn = new ColumnContainer(_contentHeaderGrid);
			_contentHeaderToolbarColumn = new ColumnContainer(_contentHeaderGrid)
			{
				FlexFactor = 0  // Toolbar column sizes to content; title column takes remaining space
			};

			_contentHeaderTitleColumn.AddContent(_contentHeader);

			_contentToolbar = new ToolbarControl();
			_contentToolbar.HorizontalAlignment = HorizontalAlignment.Right;
			_contentToolbar.Visible = false;
			_contentHeaderToolbarColumn.AddContent(_contentToolbar);
			_contentHeaderToolbarColumn.Width = 0;

			_contentHeaderGrid.AddColumn(_contentHeaderTitleColumn);
			_contentHeaderGrid.AddColumn(_contentHeaderToolbarColumn);

			// Build column structure
			_navColumn.AddContent(_paneHeader);
			_navColumn.AddContent(_navScrollPanel);
			_contentColumn.AddContent(_contentHeaderGrid);
			_contentColumn.AddContent(_contentPanel);

			_grid.AddColumn(_navColumn);
			_grid.AddColumn(_contentColumn);

			_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
			_grid.VerticalAlignment = VerticalAlignment.Fill;

			// Wire content header click to open portal in Minimal mode —
			// only when clicking near the hamburger character (first few chars)
			_contentHeader.MouseClick += (_, args) =>
			{
				if (_currentDisplayMode == NavigationViewDisplayMode.Minimal
					&& args.Position.X < ControlDefaults.NavigationViewHamburgerClickWidth)
					OpenNavigationPortal();
			};

			// Wire pane header click to open portal in Compact mode
			_paneHeader.MouseClick += (_, _) =>
			{
				if (_currentDisplayMode == NavigationViewDisplayMode.Compact)
					OpenNavigationPortal();
			};

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
			set
			{
				if (SetProperty(ref _navPaneWidth, Math.Max(ControlDefaults.MinNavigationViewPaneWidth, value)))
				{
					if (_currentDisplayMode == NavigationViewDisplayMode.Expanded)
						_effectiveNavWidth = _navPaneWidth;
					SyncInternalControls();
				}
			}
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
				_contentHeaderGrid.Visible = value;
				OnPropertyChanged();
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the content panel for direct access.
		/// </summary>
		public ScrollablePanelControl ContentPanel => _contentPanel;

		#endregion

		#region Responsive Display Mode Properties

		/// <summary>
		/// Gets or sets the pane display mode. When set to <see cref="NavigationViewDisplayMode.Auto"/>,
		/// the mode is resolved based on available width and configured thresholds.
		/// </summary>
		public NavigationViewDisplayMode PaneDisplayMode
		{
			get => _paneDisplayMode;
			set
			{
				if (_paneDisplayMode == value) return;
				_paneDisplayMode = value;
				OnPropertyChanged();
				// Re-evaluate mode with current width
				if (_lastKnownWidth > 0)
					CheckAndApplyDisplayMode(_lastKnownWidth);
				else if (value != NavigationViewDisplayMode.Auto)
					ApplyDisplayMode(value);
			}
		}

		/// <summary>
		/// Gets or sets the width threshold at or above which Auto mode resolves to Expanded.
		/// </summary>
		public int ExpandedThreshold
		{
			get => _expandedThreshold;
			set { if (SetProperty(ref _expandedThreshold, value) && _lastKnownWidth > 0) CheckAndApplyDisplayMode(_lastKnownWidth); }
		}

		/// <summary>
		/// Gets or sets the width threshold at or above which Auto mode resolves to Compact.
		/// Below this threshold, Auto resolves to Minimal.
		/// </summary>
		public int CompactThreshold
		{
			get => _compactThreshold;
			set { if (SetProperty(ref _compactThreshold, value) && _lastKnownWidth > 0) CheckAndApplyDisplayMode(_lastKnownWidth); }
		}

		/// <summary>
		/// Gets or sets the width of the navigation pane in Compact display mode.
		/// </summary>
		public int CompactPaneWidth
		{
			get => _compactPaneWidth;
			set
			{
				if (SetProperty(ref _compactPaneWidth, value) && _currentDisplayMode == NavigationViewDisplayMode.Compact)
				{
					_effectiveNavWidth = value;
					SyncInternalControls();
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets whether width transitions are animated.
		/// </summary>
		public bool AnimateTransitions
		{
			get => _animateTransitions;
			set => SetProperty(ref _animateTransitions, value);
		}

		/// <summary>
		/// Gets the currently resolved display mode (never <see cref="NavigationViewDisplayMode.Auto"/>).
		/// </summary>
		public NavigationViewDisplayMode CurrentDisplayMode => _currentDisplayMode;

		#endregion

		#region Events

		/// <summary>
		/// Raised when the resolved display mode changes.
		/// </summary>
		public event EventHandler<NavigationViewDisplayMode>? DisplayModeChanged;

		/// <summary>
		/// Raised before the selected item changes. Can be canceled.
		/// </summary>
		public event EventHandler<NavigationItemChangingEventArgs>? SelectedItemChanging;

		/// <summary>
		/// Raised after the selected item has changed.
		/// </summary>
		public event EventHandler<NavigationItemChangedEventArgs>? SelectedItemChanged;

		/// <summary>
		/// Raised when the selected item is invoked (Enter/Space key or double-click on already-selected item).
		/// </summary>
		public event EventHandler<NavigationItemChangedEventArgs>? ItemInvoked;

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container, Color.Black);
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
		/// Propagates gradient background state from the parent container.
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
		public override int? ContentWidth =>
			(Width ?? _grid.ContentWidth) + Margin.Left + Margin.Right;

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = (Width ?? _grid.GetLogicalContentSize().Width) + Margin.Left + Margin.Right;
			int height = _grid.GetLogicalContentSize().Height + Margin.Top + Margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			_grid.Dispose();
		}

		#endregion

		#region Content Toolbar

		/// <summary>
		/// Gets the content toolbar control for direct access.
		/// </summary>
		public ToolbarControl ContentToolbar => _contentToolbar;

		/// <summary>
		/// Adds a control to the content toolbar.
		/// </summary>
		public void AddContentToolbarItem(IWindowControl item)
		{
			_contentToolbar.AddItem(item);
			SyncToolbarVisibility();
		}

		/// <summary>
		/// Adds a button to the content toolbar with an optional click handler.
		/// </summary>
		public ButtonControl AddContentToolbarButton(string text, EventHandler<ButtonControl>? onClick = null)
		{
			var button = new ButtonControl { Text = text };
			if (onClick != null)
				button.Click += onClick;
			_contentToolbar.AddItem(button);
			SyncToolbarVisibility();
			return button;
		}

		/// <summary>
		/// Adds a vertical separator to the content toolbar.
		/// </summary>
		public void AddContentToolbarSeparator()
		{
			_contentToolbar.AddItem(new SeparatorControl());
			SyncToolbarVisibility();
		}

		/// <summary>
		/// Removes an item from the content toolbar.
		/// </summary>
		public void RemoveContentToolbarItem(IWindowControl item)
		{
			_contentToolbar.RemoveItem(item);
			SyncToolbarVisibility();
		}

		/// <summary>
		/// Clears all items from the content toolbar.
		/// </summary>
		public void ClearContentToolbar()
		{
			_contentToolbar.Clear();
			SyncToolbarVisibility();
		}

		private void SyncToolbarVisibility()
		{
			bool hasItems = _contentToolbar.Items.Any();
			_contentToolbar.Visible = hasItems;
			_contentHeaderToolbarColumn.Width = hasItems ? null : 0;
			Invalidate(true);
		}

		#endregion

		#region Internal Helpers

		private void SyncInternalControls()
		{
			// Don't override nav column width during animation — the animation
			// controls _navColumn.Width directly via onUpdate callbacks.
			if (!_isAnimatingWidth)
				_navColumn.Width = _effectiveNavWidth;

			_contentPanel.BorderStyle = _contentBorderStyle;
			_contentPanel.Padding = _contentPadding;

			if (_contentBorderColor.HasValue)
				_contentPanel.BorderColor = _contentBorderColor;
			if (_contentBackgroundColor.HasValue)
				_contentPanel.BackgroundColor = _contentBackgroundColor.Value;
		}

		#endregion
	}
}
