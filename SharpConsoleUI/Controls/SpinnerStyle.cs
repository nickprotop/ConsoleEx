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
	Bounce,
	/// <summary>Twinkling star (✶✸✹✺✷). Reserves 2 columns (ambiguous-width glyph).</summary>
	Star,
	/// <summary>Vertical growing bar (▁▃▄▅▆▇).</summary>
	GrowVertical,
	/// <summary>Horizontal growing bar (▏▎▍▌▋▊▉).</summary>
	GrowHorizontal,
	/// <summary>Empty/filled square blink (□■). Reserves 2 columns (ambiguous-width glyph).</summary>
	Toggle,
	/// <summary>Rotating arrow (←↖↑↗→↘↓↙). Reserves 2 columns (ambiguous-width glyph).</summary>
	Arrow,
	/// <summary>ASCII bouncing bar ([==  ]). Fixed 6-column width.</summary>
	BouncingBar,
	/// <summary>Aesthetic progress bar (▰▰▰▱▱▱). Fixed 6-column width.</summary>
	AestheticBar,
	/// <summary>Classic braille throbber (⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏). The widely-recognized npm/CLI spinner; distinct from the heavier <see cref="Braille"/> rotation.</summary>
	BrailleDots,
	/// <summary>Bouncing ASCII dots (".  " → "..." → " .." → "   "). Fixed 3-column width.</summary>
	DotsBounce
}
