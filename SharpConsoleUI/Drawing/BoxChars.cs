// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drawing
{
	/// <summary>
	/// Box drawing character sets for borders and frames.
	/// Provides predefined character sets and a helper method to get characters from BorderStyle.
	/// </summary>
	public readonly record struct BoxChars(
		char TopLeft,
		char TopRight,
		char BottomLeft,
		char BottomRight,
		char Horizontal,
		char Vertical)
	{
		/// <summary>
		/// Single-line box characters.
		/// </summary>
		public static BoxChars Single => new('┌', '┐', '└', '┘', '─', '│');

		/// <summary>
		/// Double-line box characters.
		/// </summary>
		public static BoxChars Double => new('╔', '╗', '╚', '╝', '═', '║');

		/// <summary>
		/// Rounded box characters (single-line with rounded corners).
		/// </summary>
		public static BoxChars Rounded => new('╭', '╮', '╰', '╯', '─', '│');

		/// <summary>
		/// ASCII box characters for terminals with limited character support.
		/// </summary>
		public static BoxChars Ascii => new('+', '+', '+', '+', '-', '|');

		/// <summary>
		/// Empty box characters (spaces) for invisible borders that preserve layout.
		/// </summary>
		public static BoxChars None => new(' ', ' ', ' ', ' ', ' ', ' ');

		/// <summary>
		/// Gets BoxChars for the specified BorderStyle.
		/// </summary>
		/// <param name="style">The border style to get characters for.</param>
		/// <param name="isActive">For DoubleLine style, whether to use double (active) or single (inactive) characters.</param>
		/// <returns>The appropriate BoxChars for the style.</returns>
		public static BoxChars FromBorderStyle(BorderStyle style, bool isActive = true) => style switch
		{
			BorderStyle.Single => Single,
			BorderStyle.Rounded => Rounded,
			BorderStyle.DoubleLine => isActive ? Double : Single,
			BorderStyle.None => None,
			_ => Single
		};
	}
}
