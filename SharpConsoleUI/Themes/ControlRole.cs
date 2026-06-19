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
	/// Semantic colour role a control can adopt. <see cref="Default"/> means the control uses its
	/// normal colour-resolution path (no role); the other values map to theme seed colours and are
	/// resolved to a coordinated colour set by <c>RoleResolver</c>.
	/// </summary>
	public enum ControlRole
	{
		/// <summary>No role — the control resolves colours as it normally would.</summary>
		Default,
		/// <summary>The main accent role (borders, primary buttons).</summary>
		Primary,
		/// <summary>A secondary accent (e.g. cancel buttons).</summary>
		Secondary,
		/// <summary>A tertiary accent.</summary>
		Tertiary,
		/// <summary>Informational (cyan/blue).</summary>
		Info,
		/// <summary>Success (green).</summary>
		Success,
		/// <summary>Warning (amber).</summary>
		Warning,
		/// <summary>Danger / error (red).</summary>
		Danger
	}

	/// <summary>Interaction state used when resolving role colours.</summary>
	public enum RoleState
	{
		/// <summary>Normal (resting) state.</summary>
		Normal,
		/// <summary>Focused state — the role colour is brightened.</summary>
		Focused,
		/// <summary>Disabled state — the role colours are alpha-damped.</summary>
		Disabled
	}

	/// <summary>
	/// A coordinated set of colours derived from a single <see cref="ControlRole"/>: the foreground
	/// when the role colour is used as text on the window surface (<see cref="Text"/>), the role fill
	/// (<see cref="Background"/>), the readable text on that fill (<see cref="TextOnBackground"/>),
	/// and the border (<see cref="Border"/>). All values are concrete and contrast-checked.
	/// </summary>
	public readonly record struct RoleColors(Color Text, Color Background, Color TextOnBackground, Color Border);
}
