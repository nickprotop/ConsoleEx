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
	/// Visual style used to render a <see cref="CollapsiblePanel"/>'s header.
	/// </summary>
	public enum CollapsibleHeaderStyle
	{
		/// <summary>A single clickable header row (indicator + title), no border.</summary>
		Borderless,

		/// <summary>A bordered box with the title embedded in the top border (PanelControl-style).</summary>
		Bordered
	}

	/// <summary>
	/// Controls how a <see cref="CollapsiblePanel"/> animates expand/collapse.
	/// </summary>
	public enum CollapsibleAnimationMode
	{
		/// <summary>Toggle re-layouts instantly (default).</summary>
		None,

		/// <summary>Animate the body height open/closed over a short duration.</summary>
		Height
	}
}
