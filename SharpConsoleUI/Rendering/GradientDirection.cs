// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Rendering
{
	/// <summary>
	/// Specifies the direction in which a gradient is applied.
	/// </summary>
	public enum GradientDirection
	{
		/// <summary>Left to right.</summary>
		Horizontal,
		/// <summary>Top to bottom.</summary>
		Vertical,
		/// <summary>Top-left to bottom-right.</summary>
		DiagonalDown,
		/// <summary>Bottom-left to top-right.</summary>
		DiagonalUp
	}
}
