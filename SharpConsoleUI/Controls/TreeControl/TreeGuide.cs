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
	/// Defines the line style used for tree control guide lines.
	/// </summary>
	public enum TreeGuide
	{
		/// <summary>Single-line box-drawing characters (├─└│).</summary>
		Line,
		/// <summary>ASCII characters (+\|-).</summary>
		Ascii,
		/// <summary>Double-line box-drawing characters (╠═╚║).</summary>
		DoubleLine,
		/// <summary>Bold/heavy box-drawing characters (┣━┗┃).</summary>
		BoldLine
	}
}
