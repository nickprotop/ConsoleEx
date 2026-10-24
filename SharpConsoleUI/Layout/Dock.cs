// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// The edge a docked child claims from the remaining space.
/// </summary>
/// <remarks>
/// Docking is ordered, not coordinate-based: each docked child consumes a strip from the edge it
/// names, and every later child sees the smaller rectangle that is left. This is why two children
/// docked to the same edge stack rather than overlap, and why declaration order is semantic.
/// </remarks>
public enum Dock
{
	/// <summary>Not docked; the child shares whatever space remains after every docked child.</summary>
	None = 0,

	/// <summary>Claims a full-height strip from the left of the remaining rectangle.</summary>
	Left,

	/// <summary>Claims a full-width strip from the top of the remaining rectangle.</summary>
	Top,

	/// <summary>Claims a full-height strip from the right of the remaining rectangle.</summary>
	Right,

	/// <summary>Claims a full-width strip from the bottom of the remaining rectangle.</summary>
	Bottom,
}
