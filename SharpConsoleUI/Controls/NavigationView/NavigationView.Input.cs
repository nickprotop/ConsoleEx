// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

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
			}

			// Delegate to the grid for mouse event handling
			return _grid.ProcessMouseEvent(args);
		}

		#endregion

		#region IInteractiveControl / IFocusableControl Implementation


		/// <summary>
		/// Whether the nav pane has focus — derived from the scroll panel's own focus state (single source of truth).
		/// </summary>
		private bool NavPaneHasFocus => _navScrollPanel.HasFocus;

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeIsInFocusPath();
		}

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled) return false;

			if (NavPaneHasFocus)
				return ProcessNavPaneKey(key);
			else
				return ProcessContentPanelKey(key);
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

		/// <summary>
		/// Whether the item at <paramref name="index"/> can be a keyboard-selection stop.
		/// A visible, enabled <see cref="NavigationItemType.Item"/> qualifies, and so does a
		/// visible <see cref="NavigationItemType.Header"/> — headers are navigable (so the cursor
		/// can rest on them to expand/collapse via Enter/Right/Left) even though they carry
		/// <c>IsEnabled = false</c> (which only marks them as non-content targets). Separators are
		/// never navigable. Must be called under <see cref="_itemsLock"/>.
		/// </summary>
		private bool IsNavigable(int index)
		{
			var item = _items[index];
			if (!IsItemVisible(index)) return false;
			if (item.ItemType == NavigationItemType.Header) return true;
			return item.IsEnabled && item.ItemType == NavigationItemType.Item;
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
					if (IsNavigable(candidate))
						break;
					candidate += direction;
				}

				if (candidate < 0 || candidate >= _items.Count || !IsNavigable(candidate))
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
					if (IsNavigable(i))
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
					if (IsNavigable(i))
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
		/// Left arrow walks the hierarchy toward the root (tree-style):
		/// an expanded header collapses; a sub-item moves the selection up to its parent header.
		/// Panel-switching is intentionally NOT done here — Tab/Shift+Tab own focus movement
		/// between the nav and content panes. Returns false when nothing applies.
		/// </summary>
		private bool HandleLeftArrow()
		{
			NavigationItem? current = null;
			NavigationItem? parentHeader = null;
			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
				{
					current = _items[_selectedIndex];
					parentHeader = current.ParentHeader;
				}
			}

			if (current == null) return false;

			// Expanded header → collapse in place.
			if (current.ItemType == NavigationItemType.Header && current.IsExpanded)
			{
				ToggleHeaderExpanded(current);
				return true;
			}

			// Sub-item → move selection up to its parent header.
			if (parentHeader != null)
			{
				int headerIndex;
				lock (_itemsLock) { headerIndex = _items.IndexOf(parentHeader); }
				if (headerIndex >= 0)
				{
					SelectedIndex = headerIndex;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Right arrow walks the hierarchy away from the root (tree-style):
		/// a collapsed header expands; an expanded header moves the selection to its first child.
		/// Panel-switching is intentionally NOT done here — Tab/Shift+Tab own focus movement
		/// between the nav and content panes. Returns false when nothing applies.
		/// </summary>
		private bool HandleRightArrow()
		{
			NavigationItem? current = null;
			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
					current = _items[_selectedIndex];
			}

			if (current == null || current.ItemType != NavigationItemType.Header)
				return false;

			// Collapsed header → expand it (stays selected on the header).
			if (!current.IsExpanded)
			{
				ToggleHeaderExpanded(current);
				return true;
			}

			// Expanded header → descend to its first child item.
			int firstChildIndex = -1;
			lock (_itemsLock)
			{
				for (int i = 0; i < _items.Count; i++)
				{
					if (ReferenceEquals(_items[i].ParentHeader, current) && IsNavigable(i))
					{
						firstChildIndex = i;
						break;
					}
				}
			}

			if (firstChildIndex >= 0)
			{
				SelectedIndex = firstChildIndex;
				return true;
			}

			return false;
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

			Core.AsyncEvent.Raise(ItemInvoked, ItemInvokedAsync, this, args, Container?.GetConsoleWindowSystem?.LogService);
		}

		#endregion

		#region Focus Helpers

		private void FocusNavPane()
		{
			// Internal focus switch between sibling panes — go through FocusManager
			// so FocusedControl stays current and SetFocus callbacks fire correctly.
			var window = (this as IWindowControl).GetParentWindow();
			if (window != null)
			{
				window.FocusManager.SetFocus(_navScrollPanel, FocusReason.Keyboard);
			}
			else
			{
				Container?.Invalidate(Invalidation.Repaint);
			}
			Container?.Invalidate(Invalidation.Repaint);
		}

		private void FocusContentPanel()
		{
			// Internal focus switch between sibling panes — go through FocusManager.
			// Set SavedFocus to a self-reference sentinel so RequestFocus detects
			// scroll mode and does NOT immediately delegate to the first child via
			// GetInitialFocus.  The next Tab keypress will then tab into children.
			var window = (this as IWindowControl).GetParentWindow();
			if (_contentPanel is IFocusableControl fc)
			{
				if (window != null)
				{
					_contentPanel.SavedFocus = _contentPanel;
					window.FocusManager.SetFocus(fc, FocusReason.Keyboard);
				}
				else
				{
					Container?.Invalidate(Invalidation.Repaint);
				}
			}

			Container?.Invalidate(Invalidation.Repaint);
		}

		#endregion
	}
}
