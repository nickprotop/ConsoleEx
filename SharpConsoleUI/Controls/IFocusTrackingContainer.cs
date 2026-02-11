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
	/// Interface for containers that track which child control has focus.
	/// When a child receives focus programmatically (bypassing the container's
	/// own focus management), this notification keeps the container's internal
	/// focus tracking in sync.
	/// </summary>
	public interface IFocusTrackingContainer
	{
		/// <summary>
		/// Notifies the container that a child control's focus state has changed.
		/// Called by the focus notification chain when walking up the container hierarchy.
		/// </summary>
		/// <param name="child">The child control whose focus changed.</param>
		/// <param name="hasFocus">Whether the child now has focus.</param>
		void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus);
	}
}
