// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

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
		IFocusableControl, IFocusableContainerWithHeader, IColorRoleableControl
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

		private readonly List<TabPage> _tabPages = new();
		private readonly object _tabLock = new();
		private int _activeTabIndex = -1;
		private TabHeaderStyle _headerStyle = TabHeaderStyle.Classic;
		private int? _height;
		private bool _selectOnRightClick = false;

		// IContainer properties
		// Null = follow the active theme (no hardcoded black/white that overrides the theme).
		private Color? _backgroundColor;
		private Color? _foregroundColor;

		/// <summary>
		/// Gets or sets the visual style used to render the tab header area.
		/// </summary>
		public TabHeaderStyle HeaderStyle
		{
			get => _headerStyle;
			set { _headerStyle = value; OnPropertyChanged(); Invalidate(Invalidation.Relayout); }
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

		// Per-state tab header colors. All null by default → resolve through theme.
		private Color? _activeFocusedForeground;
		private Color? _activeFocusedBackground;
		private Color? _activeUnfocusedForeground;
		private Color? _activeUnfocusedBackground;
		private Color? _inactiveFocusedForeground;
		private Color? _inactiveFocusedBackground;
		private Color? _inactiveUnfocusedForeground;
		private Color? _inactiveUnfocusedBackground;

		/// <summary>Active tab foreground when the tab strip has keyboard focus. Null = theme default.</summary>
		public Color? ActiveFocusedForegroundColor
		{
			get => _activeFocusedForeground;
			set { _activeFocusedForeground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Active tab background when the tab strip has keyboard focus. Null = theme default.</summary>
		public Color? ActiveFocusedBackgroundColor
		{
			get => _activeFocusedBackground;
			set { _activeFocusedBackground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Active tab foreground when the tab strip does not have focus. Null = theme default.</summary>
		public Color? ActiveUnfocusedForegroundColor
		{
			get => _activeUnfocusedForeground;
			set { _activeUnfocusedForeground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Active tab background when the tab strip does not have focus. Null = theme default.</summary>
		public Color? ActiveUnfocusedBackgroundColor
		{
			get => _activeUnfocusedBackground;
			set { _activeUnfocusedBackground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Inactive tab foreground when the tab strip has keyboard focus. Null = theme default.</summary>
		public Color? InactiveFocusedForegroundColor
		{
			get => _inactiveFocusedForeground;
			set { _inactiveFocusedForeground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Inactive tab background when the tab strip has keyboard focus. Null = theme default.</summary>
		public Color? InactiveFocusedBackgroundColor
		{
			get => _inactiveFocusedBackground;
			set { _inactiveFocusedBackground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Inactive tab foreground when the tab strip does not have focus. Null = theme default.</summary>
		public Color? InactiveUnfocusedForegroundColor
		{
			get => _inactiveUnfocusedForeground;
			set { _inactiveUnfocusedForeground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Inactive tab background when the tab strip does not have focus. Null = theme default.</summary>
		public Color? InactiveUnfocusedBackgroundColor
		{
			get => _inactiveUnfocusedBackground;
			set { _inactiveUnfocusedBackground = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		private bool _showTabHeader = true;

		/// <summary>
		/// Gets or sets whether the tab header bar is visible.
		/// When false, only the active tab's content is shown with no header row.
		/// Default: true.
		/// </summary>
		public bool ShowTabHeader
		{
			get => _showTabHeader;
			set { _showTabHeader = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Returns the number of rows consumed by the tab header (1 for Classic, 2 for Separator/AccentedSeparator).
		/// </summary>
		public int TabHeaderHeight => !_showTabHeader ? 0 : (_headerStyle == TabHeaderStyle.Classic ? 1 : 2);

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
			tabPage.Owner = this; // enable in-place property mutation to self-invalidate
			int count;
			lock (_tabLock)
			{
				_tabPages.Add(tabPage);
				count = _tabPages.Count;
			}
			content.Container = this;

			// NOTE: do NOT touch content.Visible here — that flag is owned by the caller.
			// The active page is selected by ActiveTabIndex / GetChildren(), so only the
			// active tab's content is built into the layout tree (see issue #53).

			Core.AsyncEvent.Raise(TabAdded, TabAddedAsync, this, new TabEventArgs(tabPage, count - 1), Container?.GetConsoleWindowSystem?.LogService);

			// Auto-activate the first tab added (standard UI framework behavior)
			if (count == 1)
				ActiveTabIndex = 0;

			// New content control must be added to the DOM layout tree
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Called by an owned <see cref="TabPage"/> when a header-affecting property (<see cref="TabPage.Title"/>
		/// or <see cref="TabPage.IsClosable"/>) changes in place. Re-lays out the header. The
		/// <see cref="_tabLock"/> re-entrancy guard makes this a no-op when the change originated from an internal
		/// mutator that already holds the lock and invalidates itself (e.g. <see cref="SetTabTitle"/>).
		/// </summary>
		internal void OnTabPageHeaderChanged()
		{
			if (Monitor.IsEntered(_tabLock)) return; // internal mutator already invalidates
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Called by an owned <see cref="TabPage"/> when its <see cref="TabPage.Content"/> is replaced in place.
		/// Rebuilds the DOM layout tree and re-lays out. (For a managed swap that also disposes the old content
		/// and wires the new content's container, prefer <see cref="SetTabContent"/>.) No-ops under the lock.
		/// </summary>
		internal void OnTabPageContentChanged()
		{
			if (Monitor.IsEntered(_tabLock)) return; // internal mutator (SetTabContent) already invalidates
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(Invalidation.Relayout);
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

							// NOTE: do NOT set oldContent.Visible = false — that flag belongs to the
							// caller. The old page leaves the layout tree because GetChildren() now
							// returns only the new active tab. Focus release below uses
							// ContainsFocusedControl, not Visible, so it is unaffected (issue #53).

							// If the focused control lives inside the old tab, clear its focus through FocusManager
							// then re-focus the TabControl itself so Tab navigation still works
							var window = this.GetParentWindow();
							var focused = window?.FocusManager.FocusedControl as IWindowControl;
							if (focused != null && ContainsFocusedControl(oldContent, focused))
							{
								window?.FocusManager.SetFocus(null, FocusReason.Programmatic);
								// Re-focus TabControl so it remains the active focus target
								if (this is IFocusableControl tabFc)
									window?.FocusManager.SetFocus(tabFc, FocusReason.Programmatic);
							}
						}

						_activeTabIndex = value;
						OnPropertyChanged();
						// Active page becomes visible via GetChildren()/layout, not by mutating
						// the caller-owned Content.Visible flag (issue #53).

						changedArgs = new TabChangedEventArgs(changingArgs.OldIndex, changingArgs.NewIndex, changingArgs.OldTab, changingArgs.NewTab);
					}
				}

				// Phase 4: Fire TabChanged event outside lock
				if (changedArgs != null)
				{
					Core.AsyncEvent.Raise(TabChanged, TabChangedAsync, this, changedArgs, Container?.GetConsoleWindowSystem?.LogService);

					// The set of children exposed for layout changed (GetChildren() now returns
					// the NEW active page), so the DOM layout tree must be rebuilt — a plain
					// Invalidate only re-measures the existing tree. Previously the tree held
					// every page and switching just toggled the caller-owned Content.Visible
					// flag, which #53 forbids.
					this.GetParentWindow()?.ForceRebuildLayout();
					Invalidate(Invalidation.Relayout);
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

		/// <summary>Async counterpart of <see cref="TabChanged"/>.</summary>
		public event Core.AsyncEventHandler<TabChangedEventArgs>? TabChangedAsync;

		/// <summary>
		/// Raised when a tab is added to the control.
		/// </summary>
		public event EventHandler<TabEventArgs>? TabAdded;

		/// <summary>Async counterpart of <see cref="TabAdded"/>.</summary>
		public event Core.AsyncEventHandler<TabEventArgs>? TabAddedAsync;

		/// <summary>
		/// Raised when a tab is removed from the control.
		/// </summary>
		public event EventHandler<TabEventArgs>? TabRemoved;

		/// <summary>Async counterpart of <see cref="TabRemoved"/>.</summary>
		public event Core.AsyncEventHandler<TabEventArgs>? TabRemovedAsync;

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
				tabPage.Owner = this; // enable in-place property mutation to self-invalidate
				_tabPages.Insert(index, tabPage);
				content.Container = this;
				// NOTE: do NOT touch content.Visible — caller owns it (issue #53).

				// Adjust active tab index if needed
				if (index <= _activeTabIndex)
				{
					_activeTabIndex++;
				}
			}

			Core.AsyncEvent.Raise(TabAdded, TabAddedAsync, this, new TabEventArgs(tabPage, index), Container?.GetConsoleWindowSystem?.LogService);
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(Invalidation.Relayout);
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
				// Tab content's Container should point at this TabControl (which
				// implements IContainer), not at our parent.  This keeps the
				// invalidation chain correct: child → TabControl → parent.
				List<TabPage> snapshot;
				lock (_tabLock) { snapshot = _tabPages.ToList(); }
				foreach (var tab in snapshot)
				{
					tab.Content.Container = this;
				}
			}
		}

		/// <summary>
		/// Gets or sets the explicit height of the control.
		/// Minimum height is 2 (1 for header, 1 for content).
		/// </summary>
		public override int? Height
		{
			get => _height;
			set
			{
				if (value.HasValue && value.Value < 2)
					throw new ArgumentException("TabControl minimum height is 2 (1 header + 1 content line)");
				_height = value;
				OnPropertyChanged();
				Container?.Invalidate(Invalidation.Relayout);
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
					tab.Owner = null; // detach so a disposed page can't invalidate this dead control
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
			get => Helpers.ColorResolver.ResolveBackground(_backgroundColor, Container);
			set { _backgroundColor = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => Helpers.ColorResolver.ResolveForeground(_foregroundColor, Container);
			set { _foregroundColor = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		public void Invalidate(Invalidation work, IWindowControl? callerControl = null)
		{
			Container?.Invalidate(work, this);
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
		/// <remarks>
		/// Returns only the active tab's content so that focus traversal and layout
		/// do not include controls from inactive tabs.
		/// </remarks>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			lock (_tabLock)
			{
				if (_activeTabIndex >= 0 && _activeTabIndex < _tabPages.Count)
					return new[] { _tabPages[_activeTabIndex].Content };
				return Array.Empty<IWindowControl>();
			}
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
		private string _title = "";
		private IWindowControl _content = null!;
		private bool _isClosable;

		/// <summary>
		/// The <see cref="TabControl"/> that owns this page, or null if the page is not attached to a
		/// control. Set by the control when the page is added (and cleared when removed). When set,
		/// header-affecting property changes (<see cref="Title"/>, <see cref="IsClosable"/>) and
		/// content changes auto-invalidate the owning control — the consumer no longer needs to call
		/// Invalidate after mutating a page in place.
		/// </summary>
		internal TabControl? Owner { get; set; }

		/// <summary>
		/// Gets or sets the title displayed in the tab header. Changing it re-lays out the header
		/// (title width changes) and invalidates the owning control automatically.
		/// </summary>
		public string Title
		{
			get => _title;
			set
			{
				if (_title == value) return;
				_title = value;
				Owner?.OnTabPageHeaderChanged();
			}
		}

		/// <summary>
		/// Gets or sets the control displayed when this tab is active. Prefer
		/// <see cref="TabControl.SetTabContent"/> for a live swap (it disposes the old content and wires the
		/// new content's container); assigning here invalidates the owner but does NOT re-wire the container.
		/// </summary>
		public IWindowControl Content
		{
			get => _content;
			set
			{
				if (ReferenceEquals(_content, value)) return;
				_content = value;
				Owner?.OnTabPageContentChanged();
			}
		}

		/// <summary>
		/// Gets or sets custom metadata associated with this tab. Not rendered; no invalidation.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets whether this tab can be closed by the user. Changing it re-lays out the header
		/// (the close affordance adds a column) and invalidates the owning control automatically.
		/// </summary>
		public bool IsClosable
		{
			get => _isClosable;
			set
			{
				if (_isClosable == value) return;
				_isClosable = value;
				Owner?.OnTabPageHeaderChanged();
			}
		}

		/// <summary>
		/// Gets or sets the tooltip text shown when hovering over the tab header.
		/// Future feature: tooltip display on hover. Not currently rendered; no invalidation.
		/// </summary>
		public string? Tooltip { get; set; }
	}
}
