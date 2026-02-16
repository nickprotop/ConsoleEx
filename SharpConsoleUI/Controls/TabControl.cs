// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using Spectre.Console;
using Color = Spectre.Console.Color;
using System.Linq;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A tab control that displays multiple pages of content, with tab headers for switching between them.
	/// Uses visibility toggling to show/hide tab content efficiently.
	/// </summary>
	public class TabControl : IWindowControl, IContainer, IDOMPaintable,
		IMouseAwareControl, IInteractiveControl, IContainerControl
	{
		private readonly List<TabPage> _tabPages = new();
		private int _activeTabIndex = 0;
		private const int TAB_HEADER_HEIGHT = 1;

		// IWindowControl properties
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private int? _height;

		// IContainer properties
		private IContainer? _container;
		private Color _backgroundColor = Color.Black;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

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
		/// Adds a new tab to the control.
		/// </summary>
		/// <param name="title">The title displayed in the tab header.</param>
		/// <param name="content">The control to display when this tab is active.</param>
		public void AddTab(string title, IWindowControl content)
		{
			var tabPage = new TabPage { Title = title, Content = content };
			_tabPages.Add(tabPage);
			content.Container = this;

			// Set visibility based on whether this is the active tab
			content.Visible = _tabPages.Count - 1 == _activeTabIndex;

			TabAdded?.Invoke(this, new TabEventArgs(tabPage, _tabPages.Count - 1));

			Invalidate(true);
		}

		/// <summary>
		/// Gets or sets the index of the currently active tab.
		/// </summary>
		public int ActiveTabIndex
		{
			get => _activeTabIndex;
		set
		{
			if (_activeTabIndex != value && value >= 0 && value < _tabPages.Count)
			{
				// Raise TabChanging event (can be canceled)
				var oldTab = _activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count ? _tabPages[_activeTabIndex] : null;
				var newTab = value >= 0 && value < _tabPages.Count ? _tabPages[value] : null;
				var changingArgs = new TabChangingEventArgs(_activeTabIndex, value, oldTab, newTab);
				TabChanging?.Invoke(this, changingArgs);

				if (changingArgs.Cancel)
					return;

				// Toggle visibility
				if (_activeTabIndex < _tabPages.Count)
					_tabPages[_activeTabIndex].Content.Visible = false;

				_activeTabIndex = value;
				_tabPages[_activeTabIndex].Content.Visible = true;

				// Raise TabChanged event
				var changedArgs = new TabChangedEventArgs(changingArgs.OldIndex, changingArgs.NewIndex, changingArgs.OldTab, changingArgs.NewTab);
				TabChanged?.Invoke(this, changedArgs);

				Invalidate(true);
			}
		}
		}

		/// <summary>
		/// Gets the read-only list of tab pages.
		/// </summary>
		public IReadOnlyList<TabPage> TabPages => _tabPages.AsReadOnly();

	#region Events

	/// <summary>
	/// Raised before the active tab changes. Can be canceled.
	/// </summary>
	public event EventHandler<TabChangingEventArgs>? TabChanging;

	/// <summary>
	/// Raised after the active tab has changed.
	/// </summary>
	public event EventHandler<TabChangedEventArgs>? TabChanged;

	/// <summary>
	/// Raised when a tab is added to the control.
	/// </summary>
	public event EventHandler<TabEventArgs>? TabAdded;

	/// <summary>
	/// Raised when a tab is removed from the control.
	/// </summary>
	public event EventHandler<TabEventArgs>? TabRemoved;

	#endregion

	#region Convenience Properties

	/// <summary>
	/// Gets the currently active tab page, or null if no tabs exist.
	/// </summary>
	public TabPage? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count
		? _tabPages[_activeTabIndex]
		: null;

	/// <summary>
	/// Gets the number of tabs in the control.
	/// </summary>
	public int TabCount => _tabPages.Count;

	/// <summary>
	/// Gets whether the control has any tabs.
	/// </summary>
	public bool HasTabs => _tabPages.Count > 0;

	/// <summary>
	/// Gets the titles of all tabs.
	/// </summary>
	public IEnumerable<string> TabTitles => _tabPages.Select(t => t.Title);

	#endregion

	#region Tab Management Methods

	/// <summary>
	/// Removes a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab to remove.</param>
	public void RemoveTab(int index)
	{
		if (index < 0 || index >= _tabPages.Count)
			return;

		var tabPage = _tabPages[index];
		_tabPages.RemoveAt(index);
		tabPage.Content.Dispose();

		// Adjust active tab index
		if (_tabPages.Count == 0)
		{
			_activeTabIndex = 0;
		}
		else if (index == _activeTabIndex)
		{
			// Removing active tab
			if (_activeTabIndex >= _tabPages.Count)
			{
				// Was last tab, go to previous
				_activeTabIndex = _tabPages.Count - 1;
			}
			// else: stay at same index (next tab slides into position)
			_tabPages[_activeTabIndex].Content.Visible = true;
		}
		else if (index < _activeTabIndex)
		{
			// Removing tab before active, decrement index
			_activeTabIndex--;
		}

		TabRemoved?.Invoke(this, new TabEventArgs(tabPage, index));
		Invalidate(true);
	}

	/// <summary>
	/// Removes a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab to remove.</param>
	public void RemoveTabAt(int index) => RemoveTab(index);

	/// <summary>
	/// Removes the first tab with the specified title.
	/// </summary>
	/// <param name="title">The title of the tab to remove.</param>
	/// <returns>True if a tab was removed, false otherwise.</returns>
	public bool RemoveTab(string title)
	{
		int index = _tabPages.FindIndex(t => t.Title == title);
		if (index >= 0)
		{
			RemoveTab(index);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Removes all tabs from the control.
	/// </summary>
	public void ClearTabs()
	{
		while (_tabPages.Count > 0)
		{
			RemoveTab(0);
		}
	}

	/// <summary>
	/// Finds a tab by title.
	/// </summary>
	/// <param name="title">The title to search for.</param>
	/// <returns>The tab page, or null if not found.</returns>
	public TabPage? FindTab(string title)
	{
		return _tabPages.FirstOrDefault(t => t.Title == title);
	}

	/// <summary>
	/// Gets a tab by index.
	/// </summary>
	/// <param name="index">The index of the tab.</param>
	/// <returns>The tab page, or null if index is out of range.</returns>
	public TabPage? GetTab(int index)
	{
		return index >= 0 && index < _tabPages.Count ? _tabPages[index] : null;
	}

	/// <summary>
	/// Checks if a tab with the specified title exists.
	/// </summary>
	/// <param name="title">The title to search for.</param>
	/// <returns>True if a tab with the title exists, false otherwise.</returns>
	public bool HasTab(string title)
	{
		return _tabPages.Any(t => t.Title == title);
	}

	/// <summary>
	/// Switches to the next tab (wraps around to first tab).
	/// </summary>
	public void NextTab()
	{
		if (_tabPages.Count > 0)
		{
			ActiveTabIndex = (_activeTabIndex + 1) % _tabPages.Count;
		}
	}

	/// <summary>
	/// Switches to the previous tab (wraps around to last tab).
	/// </summary>
	public void PreviousTab()
	{
		if (_tabPages.Count > 0)
		{
			ActiveTabIndex = (_activeTabIndex - 1 + _tabPages.Count) % _tabPages.Count;
		}
	}

	/// <summary>
	/// Switches to a tab by title.
	/// </summary>
	/// <param name="title">The title of the tab to switch to.</param>
	/// <returns>True if the tab was found and switched to, false otherwise.</returns>
	public bool SwitchToTab(string title)
	{
		int index = _tabPages.FindIndex(t => t.Title == title);
		if (index >= 0)
		{
			ActiveTabIndex = index;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Sets the title of a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab.</param>
	/// <param name="newTitle">The new title.</param>
	public void SetTabTitle(int index, string newTitle)
	{
		if (index >= 0 && index < _tabPages.Count)
		{
			_tabPages[index].Title = newTitle;
			Invalidate(true);
		}
	}

	/// <summary>
	/// Sets the content of a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab.</param>
	/// <param name="newContent">The new content control.</param>
	public void SetTabContent(int index, IWindowControl newContent)
	{
		if (index >= 0 && index < _tabPages.Count)
		{
			var oldContent = _tabPages[index].Content;
			oldContent.Dispose();
			_tabPages[index].Content = newContent;
			newContent.Container = this;
			newContent.Visible = index == _activeTabIndex;
			Invalidate(true);
		}
	}

	/// <summary>
	/// Inserts a new tab at the specified index.
	/// </summary>
	/// <param name="index">The index where the tab should be inserted.</param>
	/// <param name="title">The title displayed in the tab header.</param>
	/// <param name="content">The control to display when this tab is active.</param>
	public void InsertTab(int index, string title, IWindowControl content)
	{
		if (index < 0 || index > _tabPages.Count)
			return;

		var tabPage = new TabPage { Title = title, Content = content };
		_tabPages.Insert(index, tabPage);
		content.Container = this;
		content.Visible = false;

		// Adjust active tab index if needed
		if (index <= _activeTabIndex)
		{
			_activeTabIndex++;
		}

		TabAdded?.Invoke(this, new TabEventArgs(tabPage, index));
		Invalidate(true);
	}

	#endregion


		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ContentWidth
		{
			get
			{
				// Calculate based on tab headers or content width
				int headerWidth = CalculateHeaderWidth();
				int maxContentWidth = 0;

				foreach (var tab in _tabPages)
				{
					maxContentWidth = Math.Max(maxContentWidth, tab.Content.ContentWidth ?? 0);
				}

				return Math.Max(headerWidth, maxContentWidth);
			}
		}

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
			set
			{
				_horizontalAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set
			{
				_verticalAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				// Update container for all tab content
				foreach (var tab in _tabPages)
				{
					tab.Content.Container = value;
				}
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
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
				_visible = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
				if (_width != validatedValue)
				{
					_width = validatedValue;
					Container?.Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the explicit height of the control.
		/// Minimum height is 2 (1 for header, 1 for content).
		/// </summary>
		public int? Height
		{
			get => _height;
			set
			{
				if (value.HasValue && value.Value < 2)
					throw new ArgumentException("TabControl minimum height is 2 (1 header + 1 content line)");
				_height = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			int width = _width ?? ContentWidth ?? 0;
			int height = _height ?? (TAB_HEADER_HEIGHT + 10); // Default height if not specified

			if (!_height.HasValue && _activeTabIndex < _tabPages.Count)
			{
				// Dynamic sizing based on active tab
				var activeTabSize = _tabPages[_activeTabIndex].Content.GetLogicalContentSize();
				height = TAB_HEADER_HEIGHT + activeTabSize.Height;
			}

			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_isDirty = true;
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			foreach (var tab in _tabPages)
			{
				tab.Content.Dispose();
			}
			_tabPages.Clear();
			Container = null;
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => _backgroundColor;
			set
			{
				_backgroundColor = value;
				Invalidate();
			}
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => _foregroundColor;
			set
			{
				_foregroundColor = value;
				Invalidate();
			}
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
			// Delegate to parent container
			return Container?.GetVisibleHeightForControl(control);
		}

		#endregion

		#region IContainerControl Implementation

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			return _tabPages.Select(tp => tp.Content).ToList();
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Layout system handles this via TabLayout
			// This won't be called directly, but provide fallback
			int height = _height ?? (TAB_HEADER_HEIGHT + 10); // Default height
			int width = _width ?? constraints.MaxWidth;
			return new LayoutSize(width, height);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			// Paint tab headers at Y=0
			PaintTabHeaders(buffer, bounds, defaultFg, defaultBg);

			// Tab content painted by layout system
			_isDirty = false;
		}

		private void PaintTabHeaders(CharacterBuffer buffer, LayoutRect bounds,
			Color defaultFg, Color defaultBg)
		{
			var bgColor = ColorResolver.ResolveBackground(_backgroundColor, Container, defaultBg);
			int x = bounds.X;

			for (int i = 0; i < _tabPages.Count; i++)
			{
				bool isActive = i == _activeTabIndex;
				var tabColor = isActive ? Color.Cyan1 : Color.Grey;
				var title = $" {_tabPages[i].Title} ";

				// Draw tab title
				foreach (char c in title)
				{
					if (x - bounds.X < bounds.Width)  // Fixed: use relative position
					{
						buffer.SetCell(x, bounds.Y, c, tabColor, bgColor);
						x++;
					}
				}

				// Draw separator
				if (x - bounds.X < bounds.Width && i < _tabPages.Count - 1)  // Fixed: use relative position
				{
					buffer.SetCell(x, bounds.Y, '│', Color.Grey, bgColor);
					x++;
				}
			}

			// Fill remaining header space
			while (x - bounds.X < bounds.Width)  // Fixed: use relative position
			{
				buffer.SetCell(x, bounds.Y, '─', Color.Grey, bgColor);
				x++;
			}
		}

		private int CalculateHeaderWidth()
		{
			int width = 0;
			for (int i = 0; i < _tabPages.Count; i++)
			{
				width += _tabPages[i].Title.Length + 2; // " title "
				if (i < _tabPages.Count - 1)
					width += 1; // separator
			}
			return width;
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => false; // TabControl itself not focusable

		#pragma warning disable CS0067 // Event never raised (interface requirement)
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
			// Only handle clicks on tab headers (Y=0 relative to control)
			if (args.Position.Y == 0)
			{
				// Calculate which tab was clicked
				int clickX = args.Position.X;
				int currentX = 0;

				for (int i = 0; i < _tabPages.Count; i++)
				{
					int tabWidth = _tabPages[i].Title.Length + 2 + 1; // " title " + separator
					if (clickX >= currentX && clickX < currentX + tabWidth - 1)
					{
						if (args.HasFlag(MouseFlags.Button1Clicked))
						{
							ActiveTabIndex = i;
							return true;
						}
					}
					currentX += tabWidth;
				}
			}

			// Content clicks handled by child controls automatically
			return false;
		}

		#endregion

		#region IInteractiveControl Implementation

		/// <inheritdoc/>
		public bool HasFocus { get; set; }

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// Optional: Ctrl+Tab to switch tabs (with or without Shift)
			if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.Tab)
			{
				bool shiftPressed = (key.Modifiers & ConsoleModifiers.Shift) != 0;
				int newIndex = shiftPressed
					? (_activeTabIndex - 1 + _tabPages.Count) % _tabPages.Count
					: (_activeTabIndex + 1) % _tabPages.Count;
				ActiveTabIndex = newIndex;
				return true;
			}
			return false;
		}

		#endregion
	}

	/// <summary>
	/// Represents a single tab page with a title and content.
	/// </summary>
	public class TabPage
	{
		/// <summary>
		/// Gets or sets the title displayed in the tab header.
		/// </summary>
		public string Title { get; set; } = "";

		/// <summary>
		/// Gets or sets the control displayed when this tab is active.
		/// </summary>
		public IWindowControl Content { get; set; } = null!;

		/// <summary>
		/// Gets or sets custom metadata associated with this tab.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets whether this tab can be closed by the user.
		/// Future feature: close button in tab header.
		/// </summary>
		public bool IsClosable { get; set; }

		/// <summary>
		/// Gets or sets the tooltip text shown when hovering over the tab header.
		/// Future feature: tooltip display on hover.
		/// </summary>
		public string? Tooltip { get; set; }
	}
}
