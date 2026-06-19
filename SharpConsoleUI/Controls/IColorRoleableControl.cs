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
	/// A control whose colors can be driven by a semantic <see cref="ColorRole"/> instead of being
	/// set individually. The role's colors are derived from the active theme's palette. Controls that
	/// have no themable surface simply do not implement this interface.
	/// </summary>
	public interface IColorRoleableControl
	{
		/// <summary>The semantic color role. <see cref="ColorRole.Default"/> = no role (normal resolution).</summary>
		ColorRole ColorRole { get; set; }

		/// <summary>
		/// Optional <see cref="ThemeMode"/> override for role-colour derivation. When non-null, the role's
		/// dark/light seed colours are resolved as if the theme were in this mode, regardless of the theme's
		/// own <see cref="ITheme.Mode"/>. When null (the default), the active theme's mode is used.
		/// </summary>
		ThemeMode? ColorRoleMode { get; set; }

		/// <summary>When true and a role is set, renders outline style (role color on text + border, surface fill).</summary>
		bool Outline { get; set; }
	}
}
