// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class ListControl
	{
		/// <summary>
		/// Gets the index of the currently highlighted item (for arrow key navigation). -1 if no item is highlighted.
		/// </summary>
		public int HighlightedIndex
		{
			get => _highlightedIndex;
		}

		/// <summary>
		/// Gets or sets whether to auto-highlight on focus gain.
		/// When true, the control will highlight the selected item (or first item) when focused.
		/// Default: true (fixes UX issue where focus had no visual feedback).
		/// </summary>
		public bool AutoHighlightOnFocus
		{
			get => _autoHighlightOnFocus;
			set
			{
				_autoHighlightOnFocus = value;
			}
		}

		// Calculate effective visible items for scroll logic
		private int GetEffectiveVisibleItems()
		{
			// Query actual visible height from container (accounts for clipping)
			int? actualVisibleHeight = Container?.GetVisibleHeightForControl(this);
			int actualVisibleItems = int.MaxValue;

			if (actualVisibleHeight.HasValue && actualVisibleHeight.Value > 0)
			{
				// Account for title bar and scroll indicator
				int titleHeight = string.IsNullOrEmpty(_title) ? 0 : 1;
				int scrollIndicatorHeight = 1; // Assume scroll indicator present when scrolling
				int availableForItems = Math.Max(1, actualVisibleHeight.Value - titleHeight - scrollIndicatorHeight);

				// Count how many items actually fit based on their line heights
				// Start from current scroll offset to match what's actually visible
				int scrollOffset = CurrentScrollOffset;
				actualVisibleItems = 0;
				int heightUsed = 0;

				for (int i = scrollOffset; i < _items.Count; i++)
				{
					int itemHeight = _items[i].Lines.Count;
					if (heightUsed + itemHeight <= availableForItems)
					{
						actualVisibleItems++;
						heightUsed += itemHeight;
					}
					else
					{
						break;
					}
				}

				actualVisibleItems = Math.Max(1, actualVisibleItems);
			}

			// If user set MaxVisibleItems, use the minimum of that and actual visible
			// This ensures scrolling works even when MaxVisibleItems > actual visible area
			if (_maxVisibleItems.HasValue)
				return Math.Min(_maxVisibleItems.Value, actualVisibleItems);

			// If we have actual visible height, use it
			if (actualVisibleItems < int.MaxValue)
				return actualVisibleItems;

			// Fall back to calculated max from last render
			if (_calculatedMaxVisibleItems.HasValue)
				return _calculatedMaxVisibleItems.Value;

			// Ultimate fallback
			return Math.Min(10, _items.Count);
		}

		private void EnsureHighlightedItemVisible()
		{
			int highlightedIndex = CurrentHighlightedIndex;
			if (highlightedIndex < 0)
				return;

			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			int scrollOffset = CurrentScrollOffset;

			if (highlightedIndex < scrollOffset)
			{
				SetScrollOffset(highlightedIndex);
			}
			else if (highlightedIndex >= scrollOffset + effectiveMaxVisibleItems)
			{
				SetScrollOffset(highlightedIndex - effectiveMaxVisibleItems + 1);
			}
		}

		private void EnsureSelectedItemVisible()
		{
			int selectedIndex = CurrentSelectedIndex;
			if (selectedIndex < 0)
				return;

			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			int scrollOffset = CurrentScrollOffset;

			if (selectedIndex < scrollOffset)
			{
				SetScrollOffset(selectedIndex);
			}
			else if (selectedIndex >= scrollOffset + effectiveMaxVisibleItems)
			{
				SetScrollOffset(selectedIndex - effectiveMaxVisibleItems + 1);
			}
		}
	}
}
