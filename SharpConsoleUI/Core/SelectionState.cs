// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Reason for a selection change
	/// </summary>
	public enum SelectionChangeReason
	{
		/// <summary>Selection changed programmatically</summary>
		Programmatic,
		/// <summary>Selection changed via keyboard navigation</summary>
		Keyboard,
		/// <summary>Selection changed via mouse click</summary>
		Mouse,
		/// <summary>Selection cleared due to data change</summary>
		DataChange
	}

	/// <summary>
	/// Immutable record representing selection state for a control
	/// </summary>
	public record SelectionState
	{
		/// <summary>
		/// The selected index (-1 if nothing selected)
		/// </summary>
		public int SelectedIndex { get; init; } = -1;

		/// <summary>
		/// The highlighted index (for keyboard navigation, -1 if nothing highlighted)
		/// </summary>
		public int HighlightedIndex { get; init; } = -1;

		/// <summary>
		/// Whether this control supports multiple selection
		/// </summary>
		public bool IsMultiSelect { get; init; }

		/// <summary>
		/// Additional selected indices for multi-select (empty for single-select)
		/// </summary>
		public IReadOnlySet<int> SelectedIndices { get; init; } = new HashSet<int>();

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Creates a new selection state
		/// </summary>
		public SelectionState()
		{
			UpdateTime = DateTime.UtcNow;
		}

		/// <summary>
		/// Empty selection state
		/// </summary>
		public static readonly SelectionState Empty = new();

		/// <summary>
		/// Returns true if an item is selected
		/// </summary>
		public bool HasSelection => SelectedIndex >= 0 || SelectedIndices.Count > 0;

		/// <summary>
		/// Returns true if the specified index is selected
		/// </summary>
		public bool IsSelected(int index)
		{
			return SelectedIndex == index || SelectedIndices.Contains(index);
		}
	}

	/// <summary>
	/// Event arguments for selection changes
	/// </summary>
	public class SelectionChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The control whose selection changed
		/// </summary>
		public IWindowControl Control { get; }

		/// <summary>
		/// The previous selection state
		/// </summary>
		public SelectionState PreviousState { get; }

		/// <summary>
		/// The new selection state
		/// </summary>
		public SelectionState NewState { get; }

		/// <summary>
		/// The reason for the selection change
		/// </summary>
		public SelectionChangeReason Reason { get; }

		/// <summary>
		/// Whether the selected index changed
		/// </summary>
		public bool SelectedIndexChanged => PreviousState.SelectedIndex != NewState.SelectedIndex;

		/// <summary>
		/// Whether the highlighted index changed
		/// </summary>
		public bool HighlightedIndexChanged => PreviousState.HighlightedIndex != NewState.HighlightedIndex;

		/// <summary>
		/// Initializes a new instance of the <see cref="SelectionChangedEventArgs"/> class.
		/// </summary>
		/// <param name="control">The control whose selection changed.</param>
		/// <param name="previousState">The previous selection state before the change.</param>
		/// <param name="newState">The new selection state after the change.</param>
		/// <param name="reason">The reason for the selection change.</param>
		public SelectionChangedEventArgs(IWindowControl control, SelectionState previousState, SelectionState newState, SelectionChangeReason reason)
		{
			Control = control;
			PreviousState = previousState;
			NewState = newState;
			Reason = reason;
		}
	}
}
