// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using System.Linq;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Controls how the tab header area is rendered.
	/// </summary>
	public enum TabHeaderStyle
	{
		/// <summary>Single row: tab titles followed by ─ fill to the right.</summary>
		Classic,
		/// <summary>Two rows: tab titles row, then a plain ─ separator line below.</summary>
		Separator,
		/// <summary>Two rows: tab titles row, then a separator line with ═ under the active tab (╡/╞ connectors).</summary>
		AccentedSeparator
	}

	/// <summary>
	/// A tab control that displays multiple pages of content, with tab headers for switching between them.
	/// Uses visibility toggling to show/hide tab content efficiently.
	/// </summary>
	public partial class TabControl : BaseControl, IContainer,
		IMouseAwareControl, IInteractiveControl, IContainerControl,
		IFocusableControl, IFocusableContainerWithHeader
	{
		private readonly List<TabPage> _tabPages = new();
		private readonly object _tabLock = new();
		private int _activeTabIndex = -1;
		private TabHeaderStyle _headerStyle = TabHeaderStyle.Classic;
		private int? _height;
		private bool _selectOnRightClick = false;

		// IContainer properties
		private Color _backgroundColor = Color.Black;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

		/// <summary>
		/// Gets or sets the visual style used to render the tab header area.
		/// </summary>
		public TabHeaderStyle HeaderStyle
		{
			get => _headerStyle;
			set { _headerStyle = value; OnPropertyChanged(); Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets whether a right-click on a tab header selects that tab
		/// before firing the <see cref="MouseRightClick"/> event.
		/// Default: false (preserves backward compatibility).
		/// </summary>
		public bool SelectOnRightClick
		{
			get => _selectOnRightClick;
			set { _selectOnRightClick = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Returns the number of rows consumed by the tab header (1 for Classic, 2 for Separator/AccentedSeparator).
		/// </summary>
		public int TabHeaderHeight => _headerStyle == TabHeaderStyle.Classic ? 1 : 2;

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
		/// <param name="isClosable">When true, a × close button is shown in the tab header.</param>
		public void AddTab(string title, IWindowControl content, bool isClosable = false)
		{
			var tabPage = new TabPage { Title = title, Content = content, IsClosable = isClosable };
			int count;
			lock (_tabLock)
			{
				_tabPages.Add(tabPage);
				count = _tabPages.Count;
			}
			content.Container = this;

			// Set visibility based on whether this is the active tab
			content.Visible = count - 1 == _activeTabIndex;

			TabAdded?.Invoke(this, new TabEventArgs(tabPage, count - 1));

			// Auto-activate the first tab added (standard UI framework behavior)
			if (count == 1)
				ActiveTabIndex = 0;

			// New content control must be added to the DOM layout tree
			this.GetParentWindow()?.ForceRebuildLayout();
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
			TabChangingEventArgs? changingArgs = null;
			TabChangedEventArgs? changedArgs = null;

			// Phase 1: Validate and prepare event args under lock
			lock (_tabLock)
			{
				if (_activeTabIndex != value && value >= 0 && value < _tabPages.Count)
				{
					var oldTab = _activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count ? _tabPages[_activeTabIndex] : null;
					var newTab = value >= 0 && value < _tabPages.Count ? _tabPages[value] : null;
					changingArgs = new TabChangingEventArgs(_activeTabIndex, value, oldTab, newTab);
				}
			}

			if (changingArgs == null)
				return;

			// Phase 2: Fire TabChanging event outside lock (can be canceled)
			TabChanging?.Invoke(this, changingArgs);
			if (changingArgs.Cancel)
				return;

			// Phase 3: Commit the change under lock
			lock (_tabLock)
			{
				// Re-validate in case state changed while we were outside the lock
				if (value >= 0 && value < _tabPages.Count)
				{
					// Toggle visibility and release focus from old tab's content
					if (_activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count)
					{
						var oldContent = _tabPages[_activeTabIndex].Content;
						oldContent.Visible = false;

						// If the focused control lives inside the old tab, clear focus
						var window = this.GetParentWindow();
						var focused = window?.FocusService?.FocusedControl as IWindowControl;
						if (focused != null && ContainsFocusedControl(oldContent, focused))
							window!.FocusService!.ClearControlFocus(FocusChangeReason.Programmatic);
					}

					_activeTabIndex = value;
					OnPropertyChanged();
					_tabPages[_activeTabIndex].Content.Visible = true;

					changedArgs = new TabChangedEventArgs(changingArgs.OldIndex, changingArgs.NewIndex, changingArgs.OldTab, changingArgs.NewTab);
				}
			}

			// Phase 4: Fire TabChanged event outside lock
			if (changedArgs != null)
			{
				TabChanged?.Invoke(this, changedArgs);
				Invalidate(true);
			}
		}
		}

		/// <summary>
		/// Gets the read-only list of tab pages.
		/// </summary>
		public IReadOnlyList<TabPage> TabPages { get { lock (_tabLock) { return _tabPages.ToList().AsReadOnly(); } } }

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

	/// <summary>
	/// Raised when the user clicks the close (×) button on a closable tab.
	/// The tab is NOT automatically removed — subscribe and call RemoveTab to close it.
	/// </summary>
	public event EventHandler<TabEventArgs>? TabCloseRequested;

	#endregion

	#region Convenience Properties

	/// <summary>
	/// Gets the currently active tab page, or null if no tabs exist.
	/// </summary>
	public TabPage? ActiveTab
	{
		get
		{
			lock (_tabLock)
			{
				return _activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count
					? _tabPages[_activeTabIndex]
					: null;
			}
		}
	}

	/// <summary>
	/// Gets the number of tabs in the control.
	/// </summary>
	public int TabCount { get { lock (_tabLock) { return _tabPages.Count; } } }

	/// <summary>
	/// Gets whether the control has any tabs.
	/// </summary>
	public bool HasTabs { get { lock (_tabLock) { return _tabPages.Count > 0; } } }

	/// <summary>
	/// Gets the titles of all tabs.
	/// </summary>
	public IEnumerable<string> TabTitles { get { lock (_tabLock) { return _tabPages.Select(t => t.Title).ToList(); } } }

	#endregion

		/// <summary>
		/// Inserts a new tab at the specified index.
		/// </summary>
		/// <param name="index">The index where the tab should be inserted.</param>
		/// <param name="title">The title displayed in the tab header.</param>
		/// <param name="content">The control to display when this tab is active.</param>
		public void InsertTab(int index, string title, IWindowControl content)
		{
			TabPage tabPage;
			lock (_tabLock)
			{
				if (index < 0 || index > _tabPages.Count)
					return;

				tabPage = new TabPage { Title = title, Content = content };
				_tabPages.Insert(index, tabPage);
				content.Container = this;
				content.Visible = false;

				// Adjust active tab index if needed
				if (index <= _activeTabIndex)
				{
					_activeTabIndex++;
				}
			}

			TabAdded?.Invoke(this, new TabEventArgs(tabPage, index));
			Invalidate(true);
		}

		#region IWindowControl Implementation (overrides from BaseControl)

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				// Calculate based on tab headers or content width
				List<TabPage> snapshot;
				lock (_tabLock) { snapshot = _tabPages.ToList(); }
				int headerWidth = CalculateHeaderWidth(snapshot);
				int maxContentWidth = 0;

				foreach (var tab in snapshot)
				{
					maxContentWidth = Math.Max(maxContentWidth, tab.Content.ContentWidth ?? 0);
				}

				return Math.Max(headerWidth, maxContentWidth);
			}
		}

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				// Update container for all tab content
				List<TabPage> snapshot;
				lock (_tabLock) { snapshot = _tabPages.ToList(); }
				foreach (var tab in snapshot)
				{
					tab.Content.Container = value;
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
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = Width ?? ContentWidth ?? 0;
			int height = _height ?? (TabHeaderHeight + 10); // Default height if not specified

			lock (_tabLock)
			{
				if (!_height.HasValue && _activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count)
				{
					// Dynamic sizing based on active tab
					var activeTabSize = _tabPages[_activeTabIndex].Content.GetLogicalContentSize();
					height = TabHeaderHeight + activeTabSize.Height;
				}
			}

			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			lock (_tabLock)
			{
				foreach (var tab in _tabPages)
				{
					tab.Content.Dispose();
				}
				_tabPages.Clear();
			}
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => _backgroundColor;
			set { _backgroundColor = value; OnPropertyChanged(); Invalidate(true); }
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
			// Delegate to parent container
			return Container?.GetVisibleHeightForControl(control);
		}

		#endregion

		#region IContainerControl Implementation

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			lock (_tabLock) { return _tabPages.Select(tp => tp.Content).ToList(); }
		}

		#endregion

		/// <summary>
		/// Checks whether the target control exists anywhere in the subtree
		/// rooted at the given ancestor, using top-down child enumeration.
		/// </summary>
		private static bool ContainsFocusedControl(IWindowControl root, IWindowControl target)
		{
			if (ReferenceEquals(root, target))
				return true;
			if (root is IContainerControl container)
			{
				foreach (var child in container.GetChildren())
				{
					if (ContainsFocusedControl(child, target))
						return true;
				}
			}
			return false;
		}
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
