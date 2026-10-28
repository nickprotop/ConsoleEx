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
	/// The former name of <see cref="FigletControl"/>, kept so existing code keeps
	/// compiling.
	/// </summary>
	/// <remarks>
	/// The format is FIGlet; "Figle" was a misspelling that reached the public
	/// surface. The factory was always <c>Controls.Figlet()</c>, so the two
	/// disagreed. Deriving rather than aliasing keeps <c>is</c>, <c>as</c> and
	/// pattern matching against <see cref="FigletControl"/> working for anything
	/// built through this name.
	/// </remarks>
	[Obsolete("Renamed to FigletControl; this name will be removed in a future major version.")]
	public class FigleControl : FigletControl
	{
	}
}
