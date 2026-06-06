// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Specifies the layout mode for the Start menu panel.
/// </summary>
public enum StartMenuLayout
{
	/// <summary>Single-column compact layout with categories as flyout submenus.</summary>
	SingleColumn,
	/// <summary>Two-column layout with quick actions (left) and window list (right).</summary>
	TwoColumn
}
