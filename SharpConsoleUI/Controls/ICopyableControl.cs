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
	/// Implemented by selectable controls that support a configurable copy-to-clipboard shortcut.
	/// The owning window consults these members so the copy key can be customized or disabled
	/// per control, rather than being a fixed Ctrl+C.
	/// </summary>
	public interface ICopyableControl : ISelectableControl
	{
		/// <summary>Gets whether the keyboard copy shortcut is enabled for this control.</summary>
		bool CopyEnabled { get; }

		/// <summary>Gets the key that triggers a copy (combined with <see cref="CopyModifiers"/>).</summary>
		ConsoleKey CopyKey { get; }

		/// <summary>Gets the modifier keys required for the copy shortcut (e.g. Control).</summary>
		ConsoleModifiers CopyModifiers { get; }

		/// <summary>
		/// Copies the current selection (plain text) to the clipboard.
		/// Returns <c>true</c> if something was copied.
		/// </summary>
		bool CopySelectionToClipboard();
	}
}
