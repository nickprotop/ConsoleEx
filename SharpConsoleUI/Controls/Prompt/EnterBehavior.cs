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
	/// What the Enter key does in a <see cref="PromptControl"/>.
	/// </summary>
	public enum EnterBehavior
	{
		/// <summary>
		/// Enter raises <see cref="PromptControl.Entered"/>. The default, and the meaning Enter has
		/// always had here, so turning on <see cref="PromptControl.Multiline"/> does not silently
		/// change what the key does. When multiline, <c>Alt+Enter</c> (or <c>Ctrl+L</c>) inserts a
		/// newline.
		/// </summary>
		Submit,

		/// <summary>
		/// Enter inserts a newline and <c>Ctrl+Enter</c> submits, like a text area.
		/// <para>
		/// Note that no Unix terminal reports <c>Ctrl+Enter</c> distinctly from <c>Enter</c> without
		/// CSI-u or <c>modifyOtherKeys</c>, neither of which the Unix input parser enables — so on
		/// Linux a control in this mode has no keyboard submit, and the application is expected to
		/// submit from a button or by raising the event itself. <see cref="Submit"/> is the default
		/// for that reason.
		/// </para>
		/// </summary>
		InsertNewline
	}
}
