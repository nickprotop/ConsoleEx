// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Interface for containers that can host portals (Window and DesktopPortal).
	/// Portals are overlay LayoutNodes that render on top of normal content.
	/// </summary>
	public interface IPortalHost
	{
		/// <summary>
		/// Creates a portal overlay for the specified control.
		/// </summary>
		/// <param name="ownerControl">The control creating the portal.</param>
		/// <param name="portalContent">The content to render as an overlay.</param>
		/// <returns>The portal LayoutNode for later removal, or null if creation failed.</returns>
		LayoutNode? CreatePortal(IWindowControl ownerControl, IWindowControl portalContent);

		/// <summary>
		/// Removes a portal overlay.
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal.</param>
		/// <param name="portalNode">The portal LayoutNode returned by CreatePortal.</param>
		void RemovePortal(IWindowControl ownerControl, LayoutNode portalNode);
	}
}
