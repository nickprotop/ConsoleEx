// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Alignment options for the tab strip header within the control width.
	/// </summary>
	public enum TabStripAlignment
	{
		/// <summary>Tabs aligned to the left (default).</summary>
		Left,
		/// <summary>Tabs centered in available width.</summary>
		Center,
		/// <summary>Tabs aligned to the right.</summary>
		Right,
		/// <summary>Tabs expand to fill the full width equally.</summary>
		Stretch
	}

	/// <summary>
	/// Event arguments for tab selection changes.
	/// </summary>
	public class TabSelectedEventArgs : EventArgs
	{
		/// <summary>Gets the index of the previously selected tab (-1 if none).</summary>
		public int OldIndex { get; }

		/// <summary>Gets the index of the newly selected tab (-1 if none).</summary>
		public int NewIndex { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TabSelectedEventArgs"/> class.
		/// </summary>
		public TabSelectedEventArgs(int oldIndex, int newIndex)
		{
			OldIndex = oldIndex;
			NewIndex = newIndex;
		}
	}

	/// <summary>
	/// A tabbed container control that manages multiple content pages with a switchable tab strip header.
	/// Each tab page contains an embedded <see cref="ScrollablePanelControl"/> for content.
	/// Supports keyboard and mouse navigation, tab strip mode, and configurable content borders.
	/// </summary>
	public class TabControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl,
		IContainer, IDOMPaintable, IDirectionalFocusControl, IContainerControl, IFocusTrackingContainer
	{
		private readonly List<TabPage> _tabPages = new();
		private readonly Dictionary<TabPage, IWindowControl?> _savedFocusPerTab = new();
		private int _selectedTabIndex = -1;
		private bool _tabStripFocused = false;
		private int _focusedTabHeaderIndex = 0;
		private bool _showContentBorder = true;
		private TabStripAlignment _tabStripAlignment = TabStripAlignment.Left;

		// IWindowControl properties
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Fill;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private int? _height;
		private bool _isEnabled = true;

		// Focus state
		private bool _hasFocus = false;
		private bool _focusFromBackward = false;

		// IContainer properties
		private IContainer? _container;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private bool _isDirty = true;

		// Tab header color overrides
		private Color? _tabHeaderActiveBackgroundColor;
		private Color? _tabHeaderActiveForegroundColor;
		private Color? _tabHeaderBackgroundColor;
		private Color? _tabHeaderForegroundColor;
		private Color? _tabHeaderDisabledForegroundColor;
		private Color? _tabHeaderDisabledBackgroundColor;
		private Color? _tabContentBorderColor;

		// Actual rendered bounds
		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="TabControl"/> class.
		/// </summary>
		public TabControl()
		{
		}

		/// <summary>
		/// Creates a new <see cref="TabControlBuilder"/> for fluent construction.
		/// </summary>
		public static TabControlBuilder Create() => new TabControlBuilder();

		#region Events

		/// <summary>
		/// Event fired when the selected tab changes.
		/// </summary>
		public event EventHandler<TabSelectedEventArgs>? SelectedTabChanged;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		#pragma warning disable CS0067
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets whether to show a content border around the active tab's content area.
		/// </summary>
		public bool ShowContentBorder
		{
			get => _showContentBorder;
			set => PropertySetterHelper.SetBoolProperty(ref _showContentBorder, value, _container);
		}

		/// <summary>
		/// Gets or sets the tab strip alignment within the control width.
		/// </summary>
		public TabStripAlignment TabStripAlignment
		{
			get => _tabStripAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _tabStripAlignment, value, _container);
		}

		/// <summary>
		/// Gets the number of tab pages.
		/// </summary>
		public int TabCount => _tabPages.Count;

		/// <summary>
		/// Gets the tab pages collection.
		/// </summary>
		public IReadOnlyList<TabPage> TabPages => _tabPages.AsReadOnly();

		/// <summary>
		/// Gets or sets the index of the selected tab. -1 when no tabs exist.
		/// Setting to a disabled or hidden tab index is prevented.
		/// </summary>
		public int SelectedTabIndex
		{
			get => _selectedTabIndex;
			set => SetSelectedTabIndex(value);
		}

		/// <summary>
		/// Gets the currently selected tab page, or null if no tab is selected.
		/// </summary>
		public TabPage? SelectedTab =>
			_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count
				? _tabPages[_selectedTabIndex]
				: null;

		/// <summary>
		/// Gets whether the tab strip is currently in keyboard navigation mode.
		/// </summary>
		public bool IsTabStripFocused => _tabStripFocused;

		#endregion

		#region Color Properties

		/// <summary>
		/// Gets or sets the background color for the active (selected) tab header.
		/// </summary>
		public Color TabHeaderActiveBackgroundColor
		{
			get => ColorResolver.ResolveTabHeaderActiveBackground(_tabHeaderActiveBackgroundColor, _container);
			set { _tabHeaderActiveBackgroundColor = value; _container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color for the active (selected) tab header.
		/// </summary>
		public Color TabHeaderActiveForegroundColor
		{
			get => ColorResolver.ResolveTabHeaderActiveForeground(_tabHeaderActiveForegroundColor, _container);
			set { _tabHeaderActiveForegroundColor = value; _container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the background color for inactive tab headers.
		/// </summary>
		public Color TabHeaderBackgroundColor
		{
			get => ColorResolver.ResolveTabHeaderBackground(_tabHeaderBackgroundColor, _container);
			set { _tabHeaderBackgroundColor = value; _container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color for inactive tab headers.
		/// </summary>
		public Color TabHeaderForegroundColor
		{
			get => ColorResolver.ResolveTabHeaderForeground(_tabHeaderForegroundColor, _container);
			set { _tabHeaderForegroundColor = value; _container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color for disabled tab headers.
		/// </summary>
		public Color TabHeaderDisabledForegroundColor
		{
			get => ColorResolver.ResolveTabHeaderDisabledForeground(_tabHeaderDisabledForegroundColor, _container);
			set { _tabHeaderDisabledForegroundColor = value; _container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the background color for disabled tab headers.
		/// </summary>
		public Color TabHeaderDisabledBackgroundColor
		{
			get => ColorResolver.ResolveTabHeaderDisabledBackground(_tabHeaderDisabledBackgroundColor, _container);
			set { _tabHeaderDisabledBackgroundColor = value; _container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the border color for the content area.
		/// </summary>
		public Color TabContentBorderColor
		{
			get => ColorResolver.ResolveTabContentBorder(_tabContentBorderColor, _container);
			set { _tabContentBorderColor = value; _container?.Invalidate(true); }
		}

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ContentWidth => _width;

		/// <inheritdoc/>
		public int? ContentHeight => _height;

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, _container);
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, _container);
		}

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				// Propagate container to active tab's content panel
				foreach (var page in _tabPages)
				{
					page.Content.Container = this;
				}
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set { _margin = value; _container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, _container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set
			{
				if (_visible != value)
				{
					_visible = value;
					if (!_visible && _hasFocus)
					{
						SetFocus(false, FocusReason.Programmatic);
					}
					_container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, _container);
		}

		/// <inheritdoc/>
		public int? Height
		{
			get => _height;
			set => PropertySetterHelper.SetDimensionProperty(ref _height, value, _container);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			int tabStripHeight = 1;
			int borderHeight = _showContentBorder ? 2 : 0;
			int contentHeight = 0;

			if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
			{
				contentHeight = _tabPages[_selectedTabIndex].Content.GetLogicalContentSize().Height;
			}

			int width = _width ?? 80;
			int height = tabStripHeight + borderHeight + contentHeight + _margin.Top + _margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Invalidate(false, null);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			foreach (var page in _tabPages.ToList())
			{
				page.Content.Dispose();
				page.Owner = null;
			}
			_tabPages.Clear();
			_savedFocusPerTab.Clear();
			_selectedTabIndex = -1;
			_container = null;
		}

		#endregion

		#region IInteractiveControl Implementation

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set => SetFocus(value, FocusReason.Programmatic);
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, _container);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus) return false;

			// Ctrl+Tab / Ctrl+Shift+Tab: switch tabs from any mode
			if (key.Key == ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Control))
			{
				bool backward = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
				if (backward)
					SelectPreviousTab();
				else
					SelectNextTab();
				return true;
			}

			if (_tabStripFocused)
			{
				return ProcessKeyInTabStripMode(key);
			}
			else
			{
				return ProcessKeyInContentMode(key);
			}
		}

		private bool ProcessKeyInTabStripMode(ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
				{
					MoveTabStripHighlight(backward: true);
					return true;
				}

				case ConsoleKey.RightArrow:
				{
					MoveTabStripHighlight(backward: false);
					return true;
				}

				case ConsoleKey.Enter:
				{
					// Select the highlighted tab
					if (_focusedTabHeaderIndex >= 0 && _focusedTabHeaderIndex < _tabPages.Count)
					{
						var page = _tabPages[_focusedTabHeaderIndex];
						if (page.IsEnabled && page.IsVisible)
						{
							SetSelectedTabIndex(_focusedTabHeaderIndex);
						}
					}
					_container?.Invalidate(true);
					return true;
				}

				case ConsoleKey.Tab:
				{
					// Tab: enter content of selected tab
					if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
					{
						var page = _tabPages[_selectedTabIndex];
						var contentPanel = page.Content;
						bool hasFocusableContent = contentPanel.Children.Any(c =>
							c is IFocusableControl fc && fc.CanReceiveFocus);

						if (hasFocusableContent)
						{
							_tabStripFocused = false;
							if (!TryRestoreSavedFocus(page))
								contentPanel.SetFocus(true, FocusReason.Keyboard);
							_container?.Invalidate(true);
							return true;
						}
					}
					// No focusable content — exit control
					return false;
				}

				case ConsoleKey.Escape:
				{
					// Escape in tab strip mode: exit control
					return false;
				}
			}

			return false;
		}

		private bool ProcessKeyInContentMode(ConsoleKeyInfo key)
		{
			// Delegate to active tab's content panel first
			if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
			{
				var contentPanel = _tabPages[_selectedTabIndex].Content;
				if (contentPanel.HasFocus && contentPanel.ProcessKey(key))
				{
					return true;
				}
			}

			// Handle Escape: enter tab-strip mode
			if (key.Key == ConsoleKey.Escape)
			{
				// Unfocus content
				if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
				{
					var contentPanel = _tabPages[_selectedTabIndex].Content;
					if (contentPanel.HasFocus)
					{
						contentPanel.SetFocus(false, FocusReason.Programmatic);
					}
				}

				_tabStripFocused = true;
				_focusedTabHeaderIndex = _selectedTabIndex >= 0 ? _selectedTabIndex : 0;
				_container?.Invalidate(true);
				return true;
			}

			// Tab/Shift+Tab: if content didn't handle it, the focus has reached
			// the edge of the content children — exit the TabControl
			if (key.Key == ConsoleKey.Tab)
			{
				return false;
			}

			return false;
		}

		#endregion

		#region IFocusableControl Implementation

		/// <inheritdoc/>
		public bool CanReceiveFocus
		{
			get
			{
				if (!_visible || !_isEnabled) return false;
				// Need at least one enabled, visible tab
				return _tabPages.Any(p => p.IsEnabled && p.IsVisible);
			}
		}

		/// <inheritdoc/>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_focusFromBackward = backward;
			SetFocus(focus, FocusReason.Keyboard);
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			if (_hasFocus == focus) return;

			var hadFocus = _hasFocus;
			_hasFocus = focus;

			if (focus)
			{
				if (_selectedTabIndex < 0)
				{
					// Auto-select first enabled, visible tab
					SelectFirstAvailableTab();
				}

				// Try to focus content
				if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
				{
					var page = _tabPages[_selectedTabIndex];
					var contentPanel = page.Content;
					bool hasFocusableContent = contentPanel.Children.Any(c =>
						c is IFocusableControl fc && fc.CanReceiveFocus);

					if (hasFocusableContent)
					{
						_tabStripFocused = false;

						// Try saved state first, then direction-based
						if (!TryRestoreSavedFocus(page))
						{
							if (contentPanel is IDirectionalFocusControl dfc)
								dfc.SetFocusWithDirection(true, _focusFromBackward);
							else
								contentPanel.SetFocus(true, reason);
						}
					}
					else
					{
						// No focusable content — enter tab strip mode
						_tabStripFocused = true;
						_focusedTabHeaderIndex = _selectedTabIndex;
					}
				}
				else
				{
					_tabStripFocused = true;
					_focusedTabHeaderIndex = 0;
				}

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				// Losing focus — save focus state and unfocus content
				if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
				{
					var page = _tabPages[_selectedTabIndex];
					var contentPanel = page.Content;

					// Save which child had focus
					var focusedChild = contentPanel.Children
						.FirstOrDefault(c => c is IFocusableControl fc && fc.HasFocus);
					if (focusedChild != null)
						_savedFocusPerTab[page] = focusedChild;

					if (contentPanel.HasFocus)
					{
						contentPanel.SetFocus(false, reason);
					}
				}
				_tabStripFocused = false;

				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			_container?.Invalidate(true);

			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => _isEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => _isEnabled && CanReceiveFocus;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled || !WantsMouseEvents || args.Handled)
				return false;

			int contentX = _margin.Left;
			int contentY = _margin.Top;
			int contentWidth = _actualWidth - _margin.Left - _margin.Right;

			// Check if click is in tab strip area (row 0 of content)
			if (args.Position.Y == contentY)
			{
				if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked))
				{
					int clickedTabIndex = GetTabIndexAtX(args.Position.X - contentX, contentWidth);
					if (clickedTabIndex >= 0 && clickedTabIndex < _tabPages.Count)
					{
						var page = _tabPages[clickedTabIndex];
						if (page.IsEnabled && page.IsVisible)
						{
							SetSelectedTabIndex(clickedTabIndex);
							// Focus content after tab switch via mouse
							if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
							{
								var cp = _tabPages[_selectedTabIndex].Content;
								bool hasFocusableContent = cp.Children.Any(c =>
									c is IFocusableControl fc && fc.CanReceiveFocus);
								if (hasFocusableContent)
								{
									_tabStripFocused = false;
									cp.SetFocus(true, FocusReason.Mouse);
								}
								else
								{
									_tabStripFocused = true;
									_focusedTabHeaderIndex = _selectedTabIndex;
								}
							}
							args.Handled = true;
							MouseClick?.Invoke(this, args);
							return true;
						}
					}
				}
				return false;
			}

			// Click is in content area — delegate to active tab's content panel
			if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
			{
				int tabStripHeight = 1;
				int borderTop = _showContentBorder ? 1 : 0;
				int borderLeft = _showContentBorder ? 1 : 0;
				int contentAreaY = contentY + tabStripHeight + borderTop;

				if (args.Position.Y >= contentAreaY)
				{
					var contentPanel = _tabPages[_selectedTabIndex].Content;
					if (contentPanel is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
					{
						var relativePos = new System.Drawing.Point(
							args.Position.X - contentX - borderLeft,
							args.Position.Y - contentAreaY);
						var childArgs = args.WithPosition(relativePos);
						return mouseAware.ProcessMouseEvent(childArgs);
					}
				}
			}

			return false;
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, _container);
			set { _backgroundColorValue = value; Invalidate(true); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveForeground(_foregroundColorValue, _container);
			set { _foregroundColorValue = value; Invalidate(true); }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => _container?.GetConsoleWindowSystem;

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
			_container?.Invalidate(redrawAll, callerControl ?? this);
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			// Return the content area height for the active tab's content
			int tabStripHeight = 1;
			int borderHeight = _showContentBorder ? 2 : 0;
			int totalHeight = _actualHeight - _margin.Top - _margin.Bottom;
			int contentAreaHeight = totalHeight - tabStripHeight - borderHeight;
			return contentAreaHeight > 0 ? contentAreaHeight : null;
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = _width ?? constraints.MaxWidth;
			int tabStripHeight = 1;
			int borderHeight = _showContentBorder ? 2 : 0;

			int totalHeight;
			if (_height.HasValue)
			{
				totalHeight = _height.Value;
			}
			else if (_verticalAlignment == VerticalAlignment.Fill)
			{
				totalHeight = constraints.MaxHeight;
			}
			else
			{
				// Size to content
				int contentHeight = 0;
				if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
				{
					var contentPanel = _tabPages[_selectedTabIndex].Content;
					if (contentPanel is IDOMPaintable paintable)
					{
						int availableContentWidth = Math.Max(1, width - _margin.Left - _margin.Right - (_showContentBorder ? 2 : 0));
						var contentConstraints = new LayoutConstraints(1, availableContentWidth, 1, int.MaxValue);
						contentHeight = paintable.MeasureDOM(contentConstraints).Height;
					}
					else
					{
						contentHeight = contentPanel.GetLogicalContentSize().Height;
					}
				}
				totalHeight = tabStripHeight + borderHeight + contentHeight + _margin.Top + _margin.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(totalHeight, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;
			Color containerBg = _container?.BackgroundColor ?? defaultBg;

			int contentX = bounds.X + _margin.Left;
			int contentY = bounds.Y + _margin.Top;
			int contentWidth = bounds.Width - _margin.Left - _margin.Right;
			int contentHeight = bounds.Height - _margin.Top - _margin.Bottom;

			// Fill margins with container background
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, contentY, fgColor, containerBg);

			for (int y = contentY; y < contentY + contentHeight && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					if (_margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, y, _margin.Left, 1), ' ', fgColor, containerBg);
					if (_margin.Right > 0)
						buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, y, _margin.Right, 1), ' ', fgColor, containerBg);
				}
			}

			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentY + contentHeight, fgColor, containerBg);

			if (contentWidth <= 0 || contentHeight <= 0) return;

			// 1. Paint tab strip (row 0)
			PaintTabStrip(buffer, contentX, contentY, contentWidth, clipRect, bgColor, fgColor);

			int tabStripHeight = 1;
			int borderTop = _showContentBorder ? 1 : 0;
			int borderBottom = _showContentBorder ? 1 : 0;
			int borderLeft = _showContentBorder ? 1 : 0;
			int borderRight = _showContentBorder ? 1 : 0;

			// 2. Paint content border (if enabled)
			if (_showContentBorder)
			{
				Color borderColor = TabContentBorderColor;
				int borderY = contentY + tabStripHeight;
				int borderWidth = contentWidth;
				int borderHeight = contentHeight - tabStripHeight;

				if (borderHeight > 0)
				{
					// Top border
					if (borderY >= clipRect.Y && borderY < clipRect.Bottom)
					{
						buffer.SetCell(contentX, borderY, '┌', borderColor, bgColor);
						for (int x = contentX + 1; x < contentX + borderWidth - 1; x++)
						{
							if (x >= clipRect.X && x < clipRect.Right)
								buffer.SetCell(x, borderY, '─', borderColor, bgColor);
						}
						if (contentX + borderWidth - 1 >= clipRect.X && contentX + borderWidth - 1 < clipRect.Right)
							buffer.SetCell(contentX + borderWidth - 1, borderY, '┐', borderColor, bgColor);
					}

					// Side borders and content background
					for (int y = borderY + 1; y < borderY + borderHeight - 1; y++)
					{
						if (y >= clipRect.Y && y < clipRect.Bottom)
						{
							buffer.SetCell(contentX, y, '│', borderColor, bgColor);
							// Fill content area background
							for (int x = contentX + 1; x < contentX + borderWidth - 1; x++)
							{
								if (x >= clipRect.X && x < clipRect.Right)
									buffer.SetCell(x, y, ' ', fgColor, bgColor);
							}
							if (contentX + borderWidth - 1 >= clipRect.X && contentX + borderWidth - 1 < clipRect.Right)
								buffer.SetCell(contentX + borderWidth - 1, y, '│', borderColor, bgColor);
						}
					}

					// Bottom border
					int bottomY = borderY + borderHeight - 1;
					if (bottomY > borderY && bottomY >= clipRect.Y && bottomY < clipRect.Bottom)
					{
						buffer.SetCell(contentX, bottomY, '└', borderColor, bgColor);
						for (int x = contentX + 1; x < contentX + borderWidth - 1; x++)
						{
							if (x >= clipRect.X && x < clipRect.Right)
								buffer.SetCell(x, bottomY, '─', borderColor, bgColor);
						}
						if (contentX + borderWidth - 1 >= clipRect.X && contentX + borderWidth - 1 < clipRect.Right)
							buffer.SetCell(contentX + borderWidth - 1, bottomY, '┘', borderColor, bgColor);
					}
				}
			}
			else
			{
				// No border — fill content background
				int panelY = contentY + tabStripHeight;
				int panelHeight = contentHeight - tabStripHeight;
				for (int y = panelY; y < panelY + panelHeight; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(contentX, y, contentWidth, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// 3. Paint active tab's content
			if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
			{
				var contentPanel = _tabPages[_selectedTabIndex].Content;
				if (contentPanel is IDOMPaintable paintable)
				{
					int panelX = contentX + borderLeft;
					int panelY = contentY + tabStripHeight + borderTop;
					int panelWidth = contentWidth - borderLeft - borderRight;
					int panelHeight = contentHeight - tabStripHeight - borderTop - borderBottom;

					if (panelWidth > 0 && panelHeight > 0)
					{
						var panelBounds = new LayoutRect(panelX, panelY, panelWidth, panelHeight);
						var panelClip = panelBounds.Intersect(clipRect);

						if (panelClip.Width > 0 && panelClip.Height > 0)
						{
							paintable.PaintDOM(buffer, panelBounds, panelClip, fgColor, bgColor);
						}
					}
				}
			}

			_isDirty = false;
		}

		private void PaintTabStrip(CharacterBuffer buffer, int x, int y, int totalWidth, LayoutRect clipRect, Color bgColor, Color fgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			// Fill the entire tab strip row with background
			buffer.FillRect(new LayoutRect(x, y, totalWidth, 1), ' ', fgColor, bgColor);

			// Calculate tab widths
			var visibleTabs = new List<(int index, TabPage page, int width)>();
			int totalTabsWidth = 0;

			for (int i = 0; i < _tabPages.Count; i++)
			{
				var page = _tabPages[i];
				if (!page.IsVisible) continue;

				int titleLen = AnsiConsoleHelper.StripSpectreLength(page.Title);
				int tabWidth = titleLen + 4; // │ Title │ = 2 for │s + 2 for spaces

				visibleTabs.Add((i, page, tabWidth));
				totalTabsWidth += tabWidth;
			}

			if (visibleTabs.Count == 0) return;

			// Handle stretch mode
			if (_tabStripAlignment == TabStripAlignment.Stretch && visibleTabs.Count > 0)
			{
				int perTab = totalWidth / visibleTabs.Count;
				int remainder = totalWidth % visibleTabs.Count;
				totalTabsWidth = 0;
				for (int i = 0; i < visibleTabs.Count; i++)
				{
					int w = perTab + (i < remainder ? 1 : 0);
					visibleTabs[i] = (visibleTabs[i].index, visibleTabs[i].page, w);
					totalTabsWidth += w;
				}
			}

			// Calculate starting X based on alignment
			int startX;
			switch (_tabStripAlignment)
			{
				case TabStripAlignment.Center:
					startX = x + Math.Max(0, (totalWidth - totalTabsWidth) / 2);
					break;
				case TabStripAlignment.Right:
					startX = x + Math.Max(0, totalWidth - totalTabsWidth);
					break;
				default: // Left and Stretch
					startX = x;
					break;
			}

			int currentX = startX;
			bool overflow = false;

			foreach (var (tabIndex, page, tabWidth) in visibleTabs)
			{
				// Check for overflow
				if (currentX + tabWidth > x + totalWidth)
				{
					overflow = true;
					// Render overflow indicator
					int ellipsisX = x + totalWidth - 2;
					if (ellipsisX >= clipRect.X && ellipsisX < clipRect.Right)
					{
						buffer.SetCell(ellipsisX, y, '…', fgColor, bgColor);
						buffer.SetCell(ellipsisX + 1, y, '│', fgColor, bgColor);
					}
					break;
				}

				// Determine colors for this tab
				Color tabBg, tabFg;
				if (!page.IsEnabled)
				{
					tabBg = TabHeaderDisabledBackgroundColor;
					tabFg = TabHeaderDisabledForegroundColor;
				}
				else if (tabIndex == _selectedTabIndex)
				{
					tabBg = TabHeaderActiveBackgroundColor;
					tabFg = TabHeaderActiveForegroundColor;
				}
				else
				{
					tabBg = TabHeaderBackgroundColor;
					tabFg = TabHeaderForegroundColor;
				}

				// Determine if this tab header is highlighted in strip mode
				bool isStripHighlighted = _tabStripFocused && _hasFocus && tabIndex == _focusedTabHeaderIndex;

				// Draw separator
				if (currentX >= clipRect.X && currentX < clipRect.Right)
					buffer.SetCell(currentX, y, '│', fgColor, bgColor);

				// Draw tab title
				int titleAreaWidth = tabWidth - 2; // Exclude leading │ and trailing │
				string titleText;
				if (isStripHighlighted)
				{
					titleText = $">{page.Title}<";
				}
				else
				{
					titleText = $" {page.Title} ";
				}

				// Render using Spectre markup pipeline
				int displayWidth = Math.Max(1, titleAreaWidth);
				var ansiLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(titleText, displayWidth, 1, false, tabBg, tabFg).FirstOrDefault() ?? string.Empty;
				var cells = AnsiParser.Parse(ansiLine, tabFg, tabBg);
				buffer.WriteCellsClipped(currentX + 1, y, cells, clipRect);

				currentX += tabWidth - 1; // -1 because the trailing │ of this tab is the leading │ of the next
			}

			// Draw final separator if not overflowed
			if (!overflow && currentX >= x && currentX < x + totalWidth)
			{
				if (currentX >= clipRect.X && currentX < clipRect.Right)
					buffer.SetCell(currentX, y, '│', fgColor, bgColor);
			}
		}

		#endregion

		#region IContainerControl Implementation

		/// <summary>
		/// Gets the children of the active tab's content panel for Tab navigation traversal.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
			{
				return _tabPages[_selectedTabIndex].Content.Children;
			}
			return Array.Empty<IWindowControl>();
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				_tabStripFocused = false;

				// Live-track focused child for focus preservation across tab switches.
				// This captures the state before Escape/scroll-mode can clear it.
				if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
				{
					var page = _tabPages[_selectedTabIndex];
					var focusedChild = page.Content.Children
						.FirstOrDefault(c => c is IFocusableControl fc && fc.HasFocus);
					if (focusedChild != null)
						_savedFocusPerTab[page] = focusedChild;
				}

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}

			_container?.Invalidate(true);
		}

		#endregion

		#region Tab Management

		/// <summary>
		/// Adds a new tab page.
		/// </summary>
		/// <param name="page">The tab page to add.</param>
		public void AddTab(TabPage page)
		{
			_tabPages.Add(page);
			page.Owner = this;
			page.Content.Container = this;

			// Auto-select first tab
			if (_tabPages.Count == 1)
			{
				SetSelectedTabIndexInternal(0);
			}

			Invalidate(true);
		}

		/// <summary>
		/// Adds a new tab page with the specified title.
		/// </summary>
		/// <param name="title">The title for the new tab.</param>
		/// <returns>The created tab page for further configuration.</returns>
		public TabPage AddTab(string title)
		{
			var page = new TabPage(title);
			AddTab(page);
			return page;
		}

		/// <summary>
		/// Inserts a tab page at the specified index.
		/// </summary>
		public void InsertTab(int index, TabPage page)
		{
			index = Math.Clamp(index, 0, _tabPages.Count);
			_tabPages.Insert(index, page);
			page.Owner = this;
			page.Content.Container = this;

			// Adjust selected index if needed
			if (_selectedTabIndex >= index)
				_selectedTabIndex++;

			// Auto-select if this is the first tab
			if (_tabPages.Count == 1)
			{
				SetSelectedTabIndexInternal(0);
			}

			Invalidate(true);
		}

		/// <summary>
		/// Removes a tab page.
		/// </summary>
		public void RemoveTab(TabPage page)
		{
			int index = _tabPages.IndexOf(page);
			if (index < 0) return;
			RemoveTabAt(index);
		}

		/// <summary>
		/// Removes the tab page at the specified index.
		/// </summary>
		public void RemoveTabAt(int index)
		{
			if (index < 0 || index >= _tabPages.Count) return;

			var page = _tabPages[index];

			// Unfocus content if removing the selected tab
			if (index == _selectedTabIndex && page.Content.HasFocus)
			{
				page.Content.SetFocus(false, FocusReason.Programmatic);
			}

			page.Content.Container = null;
			page.Owner = null;
			_savedFocusPerTab.Remove(page);
			_tabPages.RemoveAt(index);

			// Adjust selection
			if (_tabPages.Count == 0)
			{
				_selectedTabIndex = -1;
			}
			else if (index == _selectedTabIndex)
			{
				// Select adjacent tab
				int newIndex = Math.Min(index, _tabPages.Count - 1);
				// Find nearest enabled+visible tab
				int? found = FindNearestAvailableTab(newIndex);
				_selectedTabIndex = found ?? -1;
			}
			else if (index < _selectedTabIndex)
			{
				_selectedTabIndex--;
			}

			Invalidate(true);
		}

		/// <summary>
		/// Removes all tab pages and disposes their content.
		/// </summary>
		public void ClearTabs()
		{
			// Unfocus current content
			if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabPages.Count)
			{
				var contentPanel = _tabPages[_selectedTabIndex].Content;
				if (contentPanel.HasFocus)
					contentPanel.SetFocus(false, FocusReason.Programmatic);
			}

			foreach (var page in _tabPages)
			{
				page.Content.Container = null;
				page.Content.Dispose();
				page.Owner = null;
			}
			_tabPages.Clear();
			_savedFocusPerTab.Clear();
			_selectedTabIndex = -1;

			Invalidate(true);
		}

		/// <summary>
		/// Selects the next available tab (wraps around).
		/// </summary>
		public void SelectNextTab()
		{
			if (_tabPages.Count == 0) return;

			int start = _selectedTabIndex >= 0 ? _selectedTabIndex + 1 : 0;
			for (int i = 0; i < _tabPages.Count; i++)
			{
				int idx = (start + i) % _tabPages.Count;
				if (_tabPages[idx].IsEnabled && _tabPages[idx].IsVisible)
				{
					SetSelectedTabIndex(idx);
					return;
				}
			}
		}

		/// <summary>
		/// Selects the previous available tab (wraps around).
		/// </summary>
		public void SelectPreviousTab()
		{
			if (_tabPages.Count == 0) return;

			int start = _selectedTabIndex >= 0 ? _selectedTabIndex - 1 + _tabPages.Count : _tabPages.Count - 1;
			for (int i = 0; i < _tabPages.Count; i++)
			{
				int idx = (start - i + _tabPages.Count) % _tabPages.Count;
				if (_tabPages[idx].IsEnabled && _tabPages[idx].IsVisible)
				{
					SetSelectedTabIndex(idx);
					return;
				}
			}
		}

		#endregion

		#region Private Methods

		private void SetSelectedTabIndex(int value)
		{
			if (value == _selectedTabIndex) return;

			// Clamp
			if (_tabPages.Count == 0) { _selectedTabIndex = -1; return; }
			value = Math.Clamp(value, 0, _tabPages.Count - 1);

			// Prevent selecting disabled or hidden tabs
			var page = _tabPages[value];
			if (!page.IsEnabled || !page.IsVisible) return;

			SetSelectedTabIndexInternal(value);
		}

		private void SetSelectedTabIndexInternal(int newIndex)
		{
			int oldIndex = _selectedTabIndex;
			if (oldIndex == newIndex) return;

			// Save focus state and unfocus old tab's content
			if (oldIndex >= 0 && oldIndex < _tabPages.Count)
			{
				var oldPage = _tabPages[oldIndex];
				var oldContent = oldPage.Content;

				// Save which child had focus before switching away
				var focusedChild = oldContent.Children
					.FirstOrDefault(c => c is IFocusableControl fc && fc.HasFocus);
				if (focusedChild != null)
					_savedFocusPerTab[oldPage] = focusedChild;

				if (oldContent.HasFocus)
				{
					oldContent.SetFocus(false, FocusReason.Programmatic);
				}
			}

			_selectedTabIndex = newIndex;

			// If we have focus and are in content mode, focus the new tab's content
			if (_hasFocus && !_tabStripFocused && newIndex >= 0 && newIndex < _tabPages.Count)
			{
				var newPage = _tabPages[newIndex];

				// Try to restore previously focused child
				if (!TryRestoreSavedFocus(newPage))
				{
					// Default: delegate to content panel (focuses first/last child)
					var newContent = newPage.Content;
					bool hasFocusableContent = newContent.Children.Any(c =>
						c is IFocusableControl fc && fc.CanReceiveFocus);

					if (hasFocusableContent)
					{
						newContent.SetFocus(true, FocusReason.Keyboard);
					}
				}
			}

			_container?.Invalidate(true);
			SelectedTabChanged?.Invoke(this, new TabSelectedEventArgs(oldIndex, newIndex));
		}

		/// <summary>
		/// Attempts to restore focus to the previously focused child in the given tab page.
		/// Returns true if focus was successfully restored.
		/// </summary>
		private bool TryRestoreSavedFocus(TabPage page)
		{
			if (!_savedFocusPerTab.TryGetValue(page, out var savedChild) || savedChild == null)
				return false;

			// Validate the saved child is still in the tab and focusable
			if (!page.Content.Children.Contains(savedChild))
			{
				_savedFocusPerTab.Remove(page);
				return false;
			}

			if (savedChild is not IFocusableControl fc || !fc.CanReceiveFocus)
			{
				_savedFocusPerTab.Remove(page);
				return false;
			}

			// Focus the saved child directly — the notification chain
			// (NotifyParentWindowOfFocusChange) updates ScrollablePanelControl._focusedChild
			// and TabControl state automatically.
			fc.SetFocus(true, FocusReason.Programmatic);
			return true;
		}

		private void SelectFirstAvailableTab()
		{
			for (int i = 0; i < _tabPages.Count; i++)
			{
				if (_tabPages[i].IsEnabled && _tabPages[i].IsVisible)
				{
					SetSelectedTabIndexInternal(i);
					return;
				}
			}
		}

		private int? FindNearestAvailableTab(int startIndex)
		{
			// Search forward then backward from startIndex
			for (int i = startIndex; i < _tabPages.Count; i++)
			{
				if (_tabPages[i].IsEnabled && _tabPages[i].IsVisible)
					return i;
			}
			for (int i = startIndex - 1; i >= 0; i--)
			{
				if (_tabPages[i].IsEnabled && _tabPages[i].IsVisible)
					return i;
			}
			return null;
		}

		private void MoveTabStripHighlight(bool backward)
		{
			var visibleEnabledTabs = new List<int>();
			for (int i = 0; i < _tabPages.Count; i++)
			{
				if (_tabPages[i].IsVisible && _tabPages[i].IsEnabled)
					visibleEnabledTabs.Add(i);
			}

			if (visibleEnabledTabs.Count == 0) return;

			int currentPos = visibleEnabledTabs.IndexOf(_focusedTabHeaderIndex);
			if (currentPos < 0)
			{
				_focusedTabHeaderIndex = visibleEnabledTabs[0];
				_container?.Invalidate(true);
				return;
			}

			int newPos;
			if (backward)
			{
				newPos = (currentPos - 1 + visibleEnabledTabs.Count) % visibleEnabledTabs.Count;
			}
			else
			{
				newPos = (currentPos + 1) % visibleEnabledTabs.Count;
			}

			_focusedTabHeaderIndex = visibleEnabledTabs[newPos];
			_container?.Invalidate(true);
		}

		private int GetTabIndexAtX(int relativeX, int contentWidth)
		{
			int currentX = 0;

			// Calculate same tab widths as PaintTabStrip
			var visibleTabs = new List<(int index, int width)>();
			int totalTabsWidth = 0;

			for (int i = 0; i < _tabPages.Count; i++)
			{
				var page = _tabPages[i];
				if (!page.IsVisible) continue;

				int titleLen = AnsiConsoleHelper.StripSpectreLength(page.Title);
				int tabWidth = titleLen + 4;

				visibleTabs.Add((i, tabWidth));
				totalTabsWidth += tabWidth;
			}

			if (visibleTabs.Count == 0) return -1;

			// Handle stretch mode
			if (_tabStripAlignment == TabStripAlignment.Stretch)
			{
				int perTab = contentWidth / visibleTabs.Count;
				int remainder = contentWidth % visibleTabs.Count;
				totalTabsWidth = 0;
				for (int j = 0; j < visibleTabs.Count; j++)
				{
					int w = perTab + (j < remainder ? 1 : 0);
					visibleTabs[j] = (visibleTabs[j].index, w);
					totalTabsWidth += w;
				}
			}

			// Calculate starting offset based on alignment
			int startOffset;
			switch (_tabStripAlignment)
			{
				case TabStripAlignment.Center:
					startOffset = Math.Max(0, (contentWidth - totalTabsWidth) / 2);
					break;
				case TabStripAlignment.Right:
					startOffset = Math.Max(0, contentWidth - totalTabsWidth);
					break;
				default:
					startOffset = 0;
					break;
			}

			currentX = startOffset;

			foreach (var (tabIndex, tabWidth) in visibleTabs)
			{
				if (relativeX >= currentX && relativeX < currentX + tabWidth)
					return tabIndex;
				currentX += tabWidth - 1; // Shared separator
			}

			return -1;
		}

		#endregion
	}
}
