// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Whether a row's checkbox is off, on, or showing that the rows under it disagree.
/// </summary>
/// <remarks>
/// <see cref="Indeterminate"/> is only ever computed, never assigned: a row reaches it when some
/// but not all of its descendants are checked. A row with no children can never hold it.
/// </remarks>
public enum TableCheckState
{
	/// <summary>Not checked.</summary>
	Unchecked,

	/// <summary>Checked.</summary>
	Checked,

	/// <summary>Some of the rows nested under this one are checked and some are not.</summary>
	Indeterminate,
}
