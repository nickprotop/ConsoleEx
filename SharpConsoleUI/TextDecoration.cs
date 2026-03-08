// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

namespace SharpConsoleUI
{
	/// <summary>
	/// Text decoration flags for styled rendering.
	/// Multiple decorations can be combined using bitwise OR.
	/// </summary>
	[Flags]
	public enum TextDecoration
	{
		None = 0,
		Bold = 1 << 0,
		Italic = 1 << 1,
		Underline = 1 << 2,
		Dim = 1 << 3,
		Strikethrough = 1 << 4,
		Invert = 1 << 5,
		Blink = 1 << 6,
	}
}
