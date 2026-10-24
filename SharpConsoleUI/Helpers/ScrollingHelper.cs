// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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
}
