// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Preset frame sets for <see cref="SpinnerControl"/>.
/// </summary>
public enum SpinnerStyle
{
	/// <summary>Braille rotation (default). Reliably narrow on modern terminals.</summary>
	Braille,
	/// <summary>Quarter-circle rotation.</summary>
	Circle,
	/// <summary>ASCII dots (".  " / ".. " / "..."). Fixed 3-column width.</summary>
	Dots,
	/// <summary>ASCII line spinner (- \ | /).</summary>
	Line,
	/// <summary>Arc rotation.</summary>
	Arc,
	/// <summary>Bouncing braille dot.</summary>
	Bounce
}
