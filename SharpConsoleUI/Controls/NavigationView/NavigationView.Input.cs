// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Controls
{
	public partial class NavigationView
	{
		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => true;

#pragma warning disable CS0067 // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

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
			// Route focus between nav pane and content pane on click events.
			// The FocusCoordinator handles window-level focus (targeting NavigationView),
			// but it can't unfocus sibling containers within the NavigationView —
			// that's an internal concern only the NavigationView knows about.
			bool isClick = args.HasAnyFlag(
				Drivers.MouseFlags.Button1Pressed, Drivers.MouseFlags.Button1Clicked,
				Drivers.MouseFlags.Button2Pressed, Drivers.MouseFlags.Button2Clicked,
				Drivers.MouseFlags.Button3Pressed, Drivers.MouseFlags.Button3Clicked);

			if (isClick)
			{
				bool clickedNavSide = args.Position.X < _navColumn.ActualWidth;
				if (clickedNavSide)
					FocusNavPane();
				else
					FocusContentPanel();
				_hasFocus = true;
			}

			// Delegate to the grid for mouse event handling
			return _grid.ProcessMouseEvent(args);
		}

		#endregion

		#region IInteractiveControl / IFocusableControl Implementation

		private bool _hasFocus;

		/// <summary>
		/// Whether the nav pane has focus — derived from the scroll panel's own focus state (single source of truth).
		/// </summary>
		private bool NavPaneHasFocus => _navScrollPanel.HasFocus;

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;
				OnPropertyChanged();

				if (value && !hadFocus)
				{
					FocusNavPane();
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					_grid.HasFocus = false;
					LostFocus?.Invoke(this, EventArgs.Empty);
				}

				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		/// <inheritdoc/>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			if (focus)
			{
				_hasFocus = true;
				if (backward)
					FocusContentPanel();
				else
					FocusNavPane();
				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				HasFocus = false;
			}
		}

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (NavPaneHasFocus)
				return ProcessNavPaneKey(key);
			else
				return ProcessContentPanelKey(key);
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			// The grid is our only child — propagate focus tracking upward
			if (child == _grid || child is HorizontalGridControl)
			{
				if (hasFocus)
				{
					if (!_hasFocus)
					{
						_hasFocus = true;
						GotFocus?.Invoke(this, EventArgs.Empty);
					}
				}
				else if (!hasFocus && _hasFocus)
				{
					_hasFocus = false;
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}

			Container?.Invalidate(true);
		}

		#endregion

		#region Keyboard Navigation

		private bool ProcessNavPaneKey(ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					return MoveSelection(-1);

				case ConsoleKey.DownArrow:
					return MoveSelection(1);

				case ConsoleKey.Home:
					return SelectFirstEnabled();

				case ConsoleKey.End:
					return SelectLastEnabled();

				case ConsoleKey.Enter:
				case ConsoleKey.Spacebar:
					return HandleInvokeKey();

				case ConsoleKey.LeftArrow:
					return HandleLeftArrow();

				case ConsoleKey.RightArrow:
					return HandleRightArrow();

				case ConsoleKey.Tab when key.Modifiers == 0:
					FocusContentPanel();
					return true;

				default:
					return false;
			}
		}

		private bool ProcessContentPanelKey(ConsoleKeyInfo key)
		{
			// Let the content panel process the key first
			if (_contentPanel.ProcessKey(key))
				return true;

			// If content didn't handle it, check for nav-return keys
			if (key.Key == ConsoleKey.LeftArrow
				|| (key.Key == ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Shift)))
			{
				// In Minimal mode, open the portal instead of focusing the hidden nav pane
				if (_currentDisplayMode == NavigationViewDisplayMode.Minimal)
				{
					OpenNavigationPortal();
					return true;
				}

				FocusNavPane();
				return true;
			}

			return false;
		}

		private bool MoveSelection(int direction)
		{
			int target;
			lock (_itemsLock)
			{
				if (_items.Count == 0) return false;

				int candidate = _selectedIndex + direction;
				while (candidate >= 0 && candidate < _items.Count)
				{
					if (_items[candidate].IsEnabled
						&& _items[candidate].ItemType == NavigationItemType.Item
						&& IsItemVisible(candidate))
						break;
					candidate += direction;
				}

				if (candidate < 0 || candidate >= _items.Count
					|| !_items[candidate].IsEnabled
					|| _items[candidate].ItemType != NavigationItemType.Item
					|| !IsItemVisible(candidate))
					return false;

				target = candidate;
			}

			SelectedIndex = target;
			return true;
		}

		private bool SelectFirstEnabled()
		{
			int target;
			lock (_itemsLock)
			{
				target = -1;
				for (int i = 0; i < _items.Count; i++)
				{
					if (_items[i].IsEnabled && _items[i].ItemType == NavigationItemType.Item
						&& IsItemVisible(i))
					{
						target = i;
						break;
					}
				}
			}

			if (target < 0) return false;
			SelectedIndex = target;
			return true;
		}

		private bool SelectLastEnabled()
		{
			int target;
			lock (_itemsLock)
			{
				target = -1;
				for (int i = _items.Count - 1; i >= 0; i--)
				{
					if (_items[i].IsEnabled && _items[i].ItemType == NavigationItemType.Item
						&& IsItemVisible(i))
					{
						target = i;
						break;
					}
				}
			}

			if (target < 0) return false;
			SelectedIndex = target;
			return true;
		}

		/// <summary>
		/// Handles Enter/Space: toggle header expand/collapse, or fire ItemInvoked for regular items.
		/// </summary>
		private bool HandleInvokeKey()
		{
			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count
					&& _items[_selectedIndex].ItemType == NavigationItemType.Header)
				{
					var header = _items[_selectedIndex];
					// ToggleHeaderExpanded needs to be called outside the lock
					// but we captured the header reference
					System.Threading.Tasks.Task.CompletedTask.ContinueWith(_ => { }, System.Threading.Tasks.TaskScheduler.Default);
				}
			}

			// Check again outside lock for header toggle
			NavigationItem? headerToToggle = null;
			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count
					&& _items[_selectedIndex].ItemType == NavigationItemType.Header)
				{
					headerToToggle = _items[_selectedIndex];
				}
			}

			if (headerToToggle != null)
			{
				ToggleHeaderExpanded(headerToToggle);
				return true;
			}

			FireItemInvoked();
			return true;
		}

		/// <summary>
		/// Left arrow on a sub-item collapses its parent header.
		/// </summary>
		private bool HandleLeftArrow()
		{
			NavigationItem? parentHeader = null;
			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
				{
					parentHeader = _items[_selectedIndex].ParentHeader;
				}
			}

			if (parentHeader != null && parentHeader.IsExpanded)
			{
				ToggleHeaderExpanded(parentHeader);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Right arrow on a header expands it. Otherwise moves focus to content.
		/// </summary>
		private bool HandleRightArrow()
		{
			NavigationItem? headerToExpand = null;
			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
				{
					var current = _items[_selectedIndex];
					if (current.ItemType == NavigationItemType.Header && !current.IsExpanded)
					{
						headerToExpand = current;
					}
				}
			}

			if (headerToExpand != null)
			{
				ToggleHeaderExpanded(headerToExpand);
				return true;
			}

			FocusContentPanel();
			return true;
		}

		private void FireItemInvoked()
		{
			NavigationItemChangedEventArgs? args;
			lock (_itemsLock)
			{
				if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
					return;

				var item = _items[_selectedIndex];
				args = new NavigationItemChangedEventArgs(
					_selectedIndex, _selectedIndex, item, item);
			}

			ItemInvoked?.Invoke(this, args);
		}

		#endregion

		#region Focus Helpers

		private void FocusNavPane()
		{
			// Internal focus switch between sibling panes — toggle HasFocus directly
			// (RequestFocus would unfocus the entire NavigationView chain unnecessarily)
			if (_contentPanel is IFocusableControl fc)
				fc.HasFocus = false;
			_navScrollPanel.HasFocus = true;

			// Sync coordinator's focus path with the actual focused leaf
			var window = (this as IWindowControl).GetParentWindow();
			window?.FocusCoord?.UpdateFocusPath(_navScrollPanel as IWindowControl);

			Container?.Invalidate(true);
		}

		private void FocusContentPanel()
		{
			// Internal focus switch between sibling panes — toggle HasFocus directly
			_navScrollPanel.HasFocus = false;
			if (_contentPanel is IFocusableControl fc)
				fc.HasFocus = true;

			// Note: no explicit UpdateFocusPath here — the content panel's SetFocus
			// delegates to a child (if any) and the notification chain updates the
			// coordinator path automatically via UpdateCoordinatorFocusPath.

			Container?.Invalidate(true);
		}

		#endregion
	}
}
