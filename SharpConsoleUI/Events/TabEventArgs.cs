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
	/// Event args for tab change events (after tab has changed).
	/// </summary>
	public class TabChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Index of the previously active tab.
		/// </summary>
		public int OldIndex { get; }

		/// <summary>
		/// Index of the newly active tab.
		/// </summary>
		public int NewIndex { get; }

		/// <summary>
		/// The previously active tab page.
		/// </summary>
		public Controls.TabPage? OldTab { get; }

		/// <summary>
		/// The newly active tab page.
		/// </summary>
		public Controls.TabPage? NewTab { get; }

		/// <summary>
		/// Initializes a new instance of TabChangedEventArgs.
		/// </summary>
		public TabChangedEventArgs(int oldIndex, int newIndex, Controls.TabPage? oldTab, Controls.TabPage? newTab)
		{
			OldIndex = oldIndex;
			NewIndex = newIndex;
			OldTab = oldTab;
			NewTab = newTab;
		}
	}

	/// <summary>
	/// Event args for tab changing events (before tab changes, cancelable).
	/// </summary>
	public class TabChangingEventArgs : EventArgs
	{
		/// <summary>
		/// Index of the currently active tab.
		/// </summary>
		public int OldIndex { get; }

		/// <summary>
		/// Index of the tab being switched to.
		/// </summary>
		public int NewIndex { get; }

		/// <summary>
		/// The currently active tab page.
		/// </summary>
		public Controls.TabPage? OldTab { get; }

		/// <summary>
		/// The tab page being switched to.
		/// </summary>
		public Controls.TabPage? NewTab { get; }

		/// <summary>
		/// Set to true to cancel the tab change.
		/// </summary>
		public bool Cancel { get; set; }

		/// <summary>
		/// Initializes a new instance of TabChangingEventArgs.
		/// </summary>
		public TabChangingEventArgs(int oldIndex, int newIndex, Controls.TabPage? oldTab, Controls.TabPage? newTab)
		{
			OldIndex = oldIndex;
			NewIndex = newIndex;
			OldTab = oldTab;
			NewTab = newTab;
			Cancel = false;
		}
	}

	/// <summary>
	/// Event args for tab added/removed events.
	/// </summary>
	public class TabEventArgs : EventArgs
	{
		/// <summary>
		/// The tab page that was added or removed.
		/// </summary>
		public Controls.TabPage TabPage { get; }

		/// <summary>
		/// The index where the tab was added or removed.
		/// </summary>
		public int Index { get; }

		/// <summary>
		/// Initializes a new instance of TabEventArgs.
		/// </summary>
		public TabEventArgs(Controls.TabPage tabPage, int index)
		{
			TabPage = tabPage;
			Index = index;
		}
	}
}
