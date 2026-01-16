using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Helper class for managing selection state updates across controls.
/// Eliminates code duplication and prevents double event firing bugs.
/// </summary>
public static class SelectionStateHelper
{
	/// <summary>
	/// Updates selection index with proper guard clause to prevent duplicate events.
	/// </summary>
	/// <param name="currentIndex">Reference to the current selected index field</param>
	/// <param name="newIndex">The new index to select</param>
	/// <param name="maxIndex">Maximum valid index (exclusive upper bound)</param>
	/// <param name="onChanged">Optional callback invoked once if selection changed</param>
	/// <returns>True if selection changed, false otherwise</returns>
	public static bool UpdateSelection(
		ref int currentIndex,
		int newIndex,
		int maxIndex,
		Action<int>? onChanged = null)
	{
		// Guard clause: prevent unnecessary updates
		if (currentIndex == newIndex)
			return false;

		// Bounds check
		if (newIndex < 0 || newIndex >= maxIndex)
			return false;

		// Update index
		int oldIndex = currentIndex;
		currentIndex = newIndex;

		// Fire event exactly once
		onChanged?.Invoke(currentIndex);

		return true;
	}

	/// <summary>
	/// Updates selection and adjusts scroll offset to ensure item is visible.
	/// Combines selection update with viewport scrolling logic.
	/// </summary>
	/// <param name="currentIndex">Reference to the current selected index field</param>
	/// <param name="newIndex">The new index to select</param>
	/// <param name="maxIndex">Maximum valid index (exclusive upper bound)</param>
	/// <param name="scrollOffset">Reference to the scroll offset field</param>
	/// <param name="visibleItems">Number of items visible in viewport</param>
	/// <param name="onChanged">Optional callback invoked once if selection changed</param>
	/// <returns>True if selection changed, false otherwise</returns>
	public static bool UpdateSelectionWithScroll(
		ref int currentIndex,
		int newIndex,
		int maxIndex,
		ref int scrollOffset,
		int visibleItems,
		Action<int>? onChanged = null)
	{
		// Guard clause: prevent unnecessary updates
		if (currentIndex == newIndex)
			return false;

		// Bounds check
		if (newIndex < 0 || newIndex >= maxIndex)
			return false;

		// Update index
		int oldIndex = currentIndex;
		currentIndex = newIndex;

		// Adjust scroll to ensure visibility
		ScrollingHelper.EnsureIndexVisible(currentIndex, ref scrollOffset, visibleItems, maxIndex);

		// Fire event exactly once
		onChanged?.Invoke(currentIndex);

		return true;
	}

	/// <summary>
	/// Updates selection by relative offset (e.g., +1 for down, -1 for up).
	/// </summary>
	/// <param name="currentIndex">Reference to the current selected index field</param>
	/// <param name="offset">Offset to apply (positive for down, negative for up)</param>
	/// <param name="maxIndex">Maximum valid index (exclusive upper bound)</param>
	/// <param name="onChanged">Optional callback invoked once if selection changed</param>
	/// <returns>True if selection changed, false otherwise</returns>
	public static bool UpdateSelectionByOffset(
		ref int currentIndex,
		int offset,
		int maxIndex,
		Action<int>? onChanged = null)
	{
		int newIndex = currentIndex + offset;
		newIndex = Math.Max(0, Math.Min(newIndex, maxIndex - 1));
		return UpdateSelection(ref currentIndex, newIndex, maxIndex, onChanged);
	}

	/// <summary>
	/// Updates selection by page offset (PageUp/PageDown).
	/// </summary>
	/// <param name="currentIndex">Reference to the current selected index field</param>
	/// <param name="pageSize">Number of items per page</param>
	/// <param name="direction">Direction: 1 for PageDown, -1 for PageUp</param>
	/// <param name="maxIndex">Maximum valid index (exclusive upper bound)</param>
	/// <param name="onChanged">Optional callback invoked once if selection changed</param>
	/// <returns>True if selection changed, false otherwise</returns>
	public static bool UpdateSelectionByPage(
		ref int currentIndex,
		int pageSize,
		int direction,
		int maxIndex,
		Action<int>? onChanged = null)
	{
		int offset = pageSize * direction;
		return UpdateSelectionByOffset(ref currentIndex, offset, maxIndex, onChanged);
	}
}
