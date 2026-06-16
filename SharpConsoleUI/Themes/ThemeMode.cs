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
	/// The declared light/dark mode of a theme. A label of the theme's identity (set by its author),
	/// not a computed value.
	/// </summary>
	public enum ThemeMode
	{
		/// <summary>A light theme (light surfaces, dark text).</summary>
		Light,

		/// <summary>A dark theme (dark surfaces, light text).</summary>
		Dark
	}
}
