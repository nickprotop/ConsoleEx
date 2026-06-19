// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control whose colors can be driven by a semantic <see cref="ControlRole"/> instead of being
	/// set individually. The role's colors are derived from the active theme's palette. Controls that
	/// have no themable surface simply do not implement this interface.
	/// </summary>
	public interface IRoleableControl
	{
		/// <summary>The semantic color role. <see cref="ControlRole.Default"/> = no role (normal resolution).</summary>
		ControlRole Role { get; set; }

		/// <summary>When true and a role is set, renders outline style (role color on text + border, surface fill).</summary>
		bool Outline { get; set; }
	}
}
