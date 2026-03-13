// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events
{
	/// <summary>
	/// Event args raised after the selected navigation item has changed.
	/// </summary>
	public class NavigationItemChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Index of the previously selected item.
		/// </summary>
		public int OldIndex { get; }

		/// <summary>
		/// Index of the newly selected item.
		/// </summary>
		public int NewIndex { get; }

		/// <summary>
		/// The previously selected item, or null if none was selected.
		/// </summary>
		public Controls.NavigationItem? OldItem { get; }

		/// <summary>
		/// The newly selected item.
		/// </summary>
		public Controls.NavigationItem? NewItem { get; }

		/// <summary>
		/// Initializes a new instance of NavigationItemChangedEventArgs.
		/// </summary>
		public NavigationItemChangedEventArgs(int oldIndex, int newIndex,
			Controls.NavigationItem? oldItem, Controls.NavigationItem? newItem)
		{
			OldIndex = oldIndex;
			NewIndex = newIndex;
			OldItem = oldItem;
			NewItem = newItem;
		}
	}

	/// <summary>
	/// Event args raised before the selected navigation item changes. Can be canceled.
	/// </summary>
	public class NavigationItemChangingEventArgs : EventArgs
	{
		/// <summary>
		/// Index of the currently selected item.
		/// </summary>
		public int OldIndex { get; }

		/// <summary>
		/// Index of the item being switched to.
		/// </summary>
		public int NewIndex { get; }

		/// <summary>
		/// The currently selected item, or null if none was selected.
		/// </summary>
		public Controls.NavigationItem? OldItem { get; }

		/// <summary>
		/// The item being switched to.
		/// </summary>
		public Controls.NavigationItem? NewItem { get; }

		/// <summary>
		/// Set to true to cancel the selection change.
		/// </summary>
		public bool Cancel { get; set; }

		/// <summary>
		/// Initializes a new instance of NavigationItemChangingEventArgs.
		/// </summary>
		public NavigationItemChangingEventArgs(int oldIndex, int newIndex,
			Controls.NavigationItem? oldItem, Controls.NavigationItem? newItem)
		{
			OldIndex = oldIndex;
			NewIndex = newIndex;
			OldItem = oldItem;
			NewItem = newItem;
			Cancel = false;
		}
	}
}
