// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Immutable padding values for content spacing.
	/// Replaces Spectre.Console.Padding.
	/// </summary>
	public readonly record struct Padding(int Left, int Top, int Right, int Bottom)
	{
		/// <summary>Creates uniform padding on all sides.</summary>
		public Padding(int all) : this(all, all, all, all) { }

		/// <summary>Creates padding with horizontal and vertical values.</summary>
		public Padding(int horizontal, int vertical) : this(horizontal, vertical, horizontal, vertical) { }

		/// <summary>No padding.</summary>
		public static Padding None => new(0, 0, 0, 0);
	}
}
