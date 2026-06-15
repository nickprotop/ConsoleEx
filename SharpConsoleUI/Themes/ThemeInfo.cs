// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Information about a registered theme including its name, description, and factory method.
	/// </summary>
	/// <param name="Name">The unique name of the theme.</param>
	/// <param name="Description">A description of the theme's visual style and characteristics.</param>
	/// <param name="Factory">A factory method that creates a new instance of the theme.</param>
	public record ThemeInfo(string Name, string Description, Func<ITheme> Factory);
}
