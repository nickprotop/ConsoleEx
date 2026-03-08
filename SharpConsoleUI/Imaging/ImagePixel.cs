// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Imaging
{
	/// <summary>
	/// Represents a single pixel with 24-bit RGB color.
	/// </summary>
	public readonly record struct ImagePixel(byte R, byte G, byte B);
}
