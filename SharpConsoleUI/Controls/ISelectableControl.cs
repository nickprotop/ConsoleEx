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
	/// Implemented by controls that expose a mouse-driven text selection coordinated by the
	/// owning window's <see cref="Core.SelectionManager"/>. Only one selectable control may own
	/// the active selection per window; starting a selection in one control clears any other.
	/// The selected text is always plain text (markup/decorations stripped) suitable for copying.
	/// </summary>
	public interface ISelectableControl : IWindowControl
	{
		/// <summary>Gets whether this control currently has a non-empty text selection.</summary>
		bool HasSelection { get; }

		/// <summary>
		/// Gets the currently selected text as plain text (any markup tags removed).
		/// Returns an empty string when nothing is selected.
		/// </summary>
		string GetSelectedText();

		/// <summary>Clears this control's selection, if any.</summary>
		void ClearSelection();

		/// <summary>
		/// Raised when the selection changes. The string payload is the current selected text
		/// (empty when the selection was cleared).
		/// </summary>
		event EventHandler<string>? SelectionChanged;
	}
}
