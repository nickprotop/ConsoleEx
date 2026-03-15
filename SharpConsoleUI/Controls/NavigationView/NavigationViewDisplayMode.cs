// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies how the NavigationView navigation pane is displayed.
	/// </summary>
	public enum NavigationViewDisplayMode
	{
		/// <summary>
		/// The display mode is automatically resolved based on available width
		/// and the configured thresholds.
		/// </summary>
		Auto,

		/// <summary>
		/// The full navigation pane is always shown with icons and text.
		/// </summary>
		Expanded,

		/// <summary>
		/// The navigation pane is always shown in a narrow icon-only column.
		/// </summary>
		Compact,

		/// <summary>
		/// The navigation pane is hidden. A hamburger menu in the content
		/// header opens an overlay portal with the full navigation list.
		/// </summary>
		Minimal
	}
}
