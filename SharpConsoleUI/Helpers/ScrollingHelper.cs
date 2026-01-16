namespace SharpConsoleUI.Helpers;

/// <summary>
/// Helper class for managing viewport scrolling logic across scrollable controls.
/// Consolidates duplicated scroll adjustment code from TreeControl, ListControl, and DropdownControl.
/// </summary>
public static class ScrollingHelper
{
	/// <summary>
	/// Adjusts scroll offset to ensure the selected index is visible in the viewport.
	/// If the item is above the viewport, scrolls up. If below, scrolls down.
	/// </summary>
	/// <param name="selectedIndex">The index that must be visible</param>
	/// <param name="scrollOffset">Reference to the scroll offset field to update</param>
	/// <param name="visibleItems">Number of items visible in the viewport</param>
	/// <param name="totalItems">Total number of items in the list</param>
	public static void EnsureIndexVisible(
		int selectedIndex,
		ref int scrollOffset,
		int visibleItems,
		int totalItems)
	{
		// If item is above visible area, scroll up
		if (selectedIndex < scrollOffset)
		{
			scrollOffset = selectedIndex;
		}
		// If item is below visible area, scroll down
		else if (selectedIndex >= scrollOffset + visibleItems)
		{
			scrollOffset = selectedIndex - visibleItems + 1;
		}

		// Clamp scroll offset to valid range
		scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, totalItems - visibleItems)));
	}

	/// <summary>
	/// Centers the selected index in the viewport if possible.
	/// Useful for jump operations (PageUp/PageDown, search results).
	/// </summary>
	/// <param name="selectedIndex">The index to center</param>
	/// <param name="scrollOffset">Reference to the scroll offset field to update</param>
	/// <param name="visibleItems">Number of items visible in the viewport</param>
	/// <param name="totalItems">Total number of items in the list</param>
	public static void CenterIndexInViewport(
		int selectedIndex,
		ref int scrollOffset,
		int visibleItems,
		int totalItems)
	{
		// Try to center the item
		int halfVisible = visibleItems / 2;
		scrollOffset = selectedIndex - halfVisible;

		// Clamp to valid range
		scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, totalItems - visibleItems)));
	}

	/// <summary>
	/// Scrolls the viewport by a relative delta (e.g., mouse wheel, arrow keys).
	/// </summary>
	/// <param name="scrollOffset">Reference to the scroll offset field to update</param>
	/// <param name="delta">Amount to scroll (positive = down, negative = up)</param>
	/// <param name="totalItems">Total number of items in the list</param>
	/// <param name="visibleItems">Number of items visible in the viewport</param>
	public static void ScrollByDelta(
		ref int scrollOffset,
		int delta,
		int totalItems,
		int visibleItems)
	{
		scrollOffset += delta;

		// Clamp to valid range
		scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, totalItems - visibleItems)));
	}

	/// <summary>
	/// Calculates the maximum scroll offset for a given list.
	/// </summary>
	/// <param name="totalItems">Total number of items</param>
	/// <param name="visibleItems">Number of visible items in viewport</param>
	/// <returns>Maximum valid scroll offset</returns>
	public static int GetMaxScrollOffset(int totalItems, int visibleItems)
	{
		return Math.Max(0, totalItems - visibleItems);
	}

	/// <summary>
	/// Checks if an index is currently visible in the viewport.
	/// </summary>
	/// <param name="index">The index to check</param>
	/// <param name="scrollOffset">Current scroll offset</param>
	/// <param name="visibleItems">Number of items visible in viewport</param>
	/// <returns>True if the index is visible, false otherwise</returns>
	public static bool IsIndexVisible(int index, int scrollOffset, int visibleItems)
	{
		return index >= scrollOffset && index < scrollOffset + visibleItems;
	}
}
