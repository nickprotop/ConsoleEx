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
	/// Specifies the selection mode for a ListControl.
	/// </summary>
	public enum ListSelectionMode
	{
		/// <summary>
		/// Highlight and selection are merged. Only one index tracked.
		/// No [x] markers shown. Like TreeControl behavior.
		/// </summary>
		Simple,

		/// <summary>
		/// Highlight and selection are separate. Two indices tracked.
		/// [x] markers show selected items, [ ] shows highlighted item.
		/// Like DropdownControl behavior. (Default)
		/// </summary>
		Complex
	}
}
