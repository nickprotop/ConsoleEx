// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class TreeControl
	{
		// Deferred event tracking for keyboard processing
		private TreeNodeEventArgs? _deferredSelectionChanged;
		private TreeNodeEventArgs? _deferredExpandCollapse;
		private TreeNodeEventArgs? _deferredNodeActivated;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled)
				return false;

			// Reset deferred events
			_deferredSelectionChanged = null;
			_deferredExpandCollapse = null;
			_deferredNodeActivated = null;
			bool result = false;

			lock (_treeLock)
			{
				if (_flattenedNodes.Count == 0)
					return false;

				if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

				int selectedIndex = CurrentSelectedIndex;
				int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
				int count = _flattenedNodes.Count;

				switch (key.Key)
				{
					case ConsoleKey.UpArrow:
						if (SelectionStateHelper.UpdateSelectionWithScroll(
							ref _selectedIndex,
							selectedIndex - 1,
							count,
							ref _scrollOffset,
							effectiveMaxVisibleItems,
							DeferSelectionChanged))
						{
							Container?.Invalidate(true);
							result = true;
						}
						break;

					case ConsoleKey.DownArrow:
						if (SelectionStateHelper.UpdateSelectionWithScroll(
							ref _selectedIndex,
							selectedIndex + 1,
							count,
							ref _scrollOffset,
							effectiveMaxVisibleItems,
							DeferSelectionChanged))
						{
							Container?.Invalidate(true);
							result = true;
						}
						break;

					case ConsoleKey.PageUp:
					{
						int pageSize = effectiveMaxVisibleItems;
						int newIndex = Math.Max(0, selectedIndex - pageSize);
						if (SelectionStateHelper.UpdateSelectionWithScroll(
							ref _selectedIndex,
							newIndex,
							count,
							ref _scrollOffset,
							effectiveMaxVisibleItems,
							DeferSelectionChanged))
						{
							Container?.Invalidate(true);
							result = true;
						}
						break;
					}

					case ConsoleKey.PageDown:
					{
						int pageSize = effectiveMaxVisibleItems;
						int newIndex = Math.Min(count - 1, selectedIndex + pageSize);
						if (SelectionStateHelper.UpdateSelectionWithScroll(
							ref _selectedIndex,
							newIndex,
							count,
							ref _scrollOffset,
							effectiveMaxVisibleItems,
							DeferSelectionChanged))
						{
							Container?.Invalidate(true);
							result = true;
						}
						break;
					}

					case ConsoleKey.RightArrow:
					{
						var selNode = _selectedIndex >= 0 && _selectedIndex < count ? _flattenedNodes[_selectedIndex] : null;
						if (selNode != null && !selNode.IsExpanded)
						{
							selNode.IsExpanded = true;
							_deferredExpandCollapse = new TreeNodeEventArgs(selNode);
							UpdateFlattenedNodes();
							Container?.Invalidate(true);
							result = true;
						}
						break;
					}

					case ConsoleKey.LeftArrow:
					{
						var selNode = _selectedIndex >= 0 && _selectedIndex < count ? _flattenedNodes[_selectedIndex] : null;
						if (selNode != null)
						{
							if (selNode.IsExpanded && selNode.Children.Count > 0)
							{
								// Collapse the expanded node
								selNode.IsExpanded = false;
								_deferredExpandCollapse = new TreeNodeEventArgs(selNode);
								UpdateFlattenedNodes();
								Container?.Invalidate(true);
								result = true;
							}
						}
						break;
					}

					case ConsoleKey.Spacebar:
					case ConsoleKey.Enter:
					{
						var selNode = _selectedIndex >= 0 && _selectedIndex < count ? _flattenedNodes[_selectedIndex] : null;
						if (selNode != null)
						{
							if (selNode.Children.Count > 0)
							{
								// Toggle expand/collapse on directories
								selNode.IsExpanded = !selNode.IsExpanded;
								_deferredExpandCollapse = new TreeNodeEventArgs(selNode);
								UpdateFlattenedNodes();
							}
							else
							{
								// Activate leaf node (open file)
								_deferredNodeActivated = new TreeNodeEventArgs(selNode);
							}
							Container?.Invalidate(true);
							result = true;
						}
						break;
					}

					case ConsoleKey.Home:
						if (SelectionStateHelper.UpdateSelectionWithScroll(
							ref _selectedIndex,
							0,
							count,
							ref _scrollOffset,
							effectiveMaxVisibleItems,
							DeferSelectionChanged))
						{
							Container?.Invalidate(true);
							result = true;
						}
						break;

					case ConsoleKey.End:
						if (SelectionStateHelper.UpdateSelectionWithScroll(
							ref _selectedIndex,
							count - 1,
							count,
							ref _scrollOffset,
							effectiveMaxVisibleItems,
							DeferSelectionChanged))
						{
							Container?.Invalidate(true);
							result = true;
						}
						break;
				}
			}

			// Fire deferred events outside the lock
			if (_deferredSelectionChanged != null)
				SelectedNodeChanged?.Invoke(this, _deferredSelectionChanged);
			if (_deferredExpandCollapse != null)
				NodeExpandCollapse?.Invoke(this, _deferredExpandCollapse);
			if (_deferredNodeActivated != null)
				NodeActivated?.Invoke(this, _deferredNodeActivated);

			return result;
		}

		// Deferred version of OnSelectionChanged (captures event args instead of firing)
		private void DeferSelectionChanged(int newIndex)
		{
			var selectedNode = newIndex >= 0 && newIndex < _flattenedNodes.Count ? _flattenedNodes[newIndex] : null;
			_deferredSelectionChanged = new TreeNodeEventArgs(selectedNode);
		}

		// Helper method to invoke selection changed event (called by SelectionStateHelper)
		private void OnSelectionChanged(int newIndex)
		{
			var selectedNode = newIndex >= 0 && newIndex < _flattenedNodes.Count ? _flattenedNodes[newIndex] : null;
			SelectedNodeChanged?.Invoke(this, new TreeNodeEventArgs(selectedNode));
		}
	}
}
