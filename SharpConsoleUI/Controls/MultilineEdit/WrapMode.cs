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
	/// Specifies how text wrapping is handled in multiline controls.
	/// </summary>
	public enum WrapMode
	{
		/// <summary>No text wrapping; lines extend beyond viewport width.</summary>
		NoWrap,
		/// <summary>Wrap text at character boundaries.</summary>
		Wrap,
		/// <summary>Wrap text at word boundaries when possible.</summary>
		WrapWords
	}
}
