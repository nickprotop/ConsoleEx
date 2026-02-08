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
	/// Specifies when scrollbars should be displayed.
	/// </summary>
	public enum ScrollbarVisibility
	{
		/// <summary>Show scrollbars only when content exceeds viewport.</summary>
		Auto,
		/// <summary>Always show scrollbars regardless of content size.</summary>
		Always,
		/// <summary>Never show scrollbars.</summary>
		Never
	}
}
