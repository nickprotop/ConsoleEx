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
	/// Implemented by controls that accept pasted text as an atomic block. The owning window routes
	/// both terminal-native bracketed paste and Ctrl+V to the focused control's <see cref="Paste"/>.
	/// </summary>
	public interface IPasteTarget
	{
		/// <summary>Inserts <paramref name="text"/> at the current position as a single paste.</summary>
		void Paste(string text);
	}
}
