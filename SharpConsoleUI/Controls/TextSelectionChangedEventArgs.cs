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
	/// Event data describing a change to a control's text selection.
	/// </summary>
	public class TextSelectionChangedEventArgs : EventArgs
	{
		/// <summary>Initializes a new instance of the <see cref="TextSelectionChangedEventArgs"/> class.</summary>
		/// <param name="hasSelection">Whether a non-empty selection currently exists.</param>
		/// <param name="selectedText">The currently selected plain text (empty when cleared).</param>
		public TextSelectionChangedEventArgs(bool hasSelection, string selectedText)
		{
			HasSelection = hasSelection;
			SelectedText = selectedText;
		}

		/// <summary>Gets whether a non-empty selection currently exists.</summary>
		public bool HasSelection { get; }

		/// <summary>Gets the currently selected plain text (empty when the selection was cleared).</summary>
		public string SelectedText { get; }
	}
}
