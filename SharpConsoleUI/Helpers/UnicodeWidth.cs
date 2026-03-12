// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;
using Wcwidth;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides display width information for Unicode characters.
	/// Delegates to the Wcwidth library for accurate width calculation
	/// including zero-width characters (combining marks, variation selectors, ZWJ).
	/// Spacing Combining Marks (Unicode category Mc) are corrected to width 1,
	/// as they occupy visual space in terminals despite Wcwidth marking them zero-width.
	/// </summary>
	public static class UnicodeWidth
	{
		public static int GetCharWidth(char c)
		{
			int w = UnicodeCalculator.GetWidth(c);
			if (w <= 0)
			{
				// Spacing Combining Marks (Mc) occupy visual space in terminals
				if (w == 0 && Rune.GetUnicodeCategory(new Rune(c)) == UnicodeCategory.SpacingCombiningMark)
					return 1;
				return 0;
			}
			return w;
		}

		public static int GetRuneWidth(Rune r)
		{
			int w = UnicodeCalculator.GetWidth(r.Value);
			if (w <= 0)
			{
				// Spacing Combining Marks (Mc) occupy visual space in terminals
				if (w == 0 && Rune.GetUnicodeCategory(r) == UnicodeCategory.SpacingCombiningMark)
					return 1;
				return 0;
			}
			return w;
		}

		public static bool IsWide(char c) => GetCharWidth(c) == 2;

		public static bool IsWideRune(Rune r) => GetRuneWidth(r) == 2;

		public static bool IsZeroWidth(Rune r) => GetRuneWidth(r) == 0;

		/// <summary>
		/// Returns the total display width of a string, summing per-Rune widths.
		/// Zero-width characters contribute 0 to the total.
		/// </summary>
		public static int GetStringWidth(string s)
		{
			if (string.IsNullOrEmpty(s))
				return 0;

			int w = 0;
			foreach (var rune in s.EnumerateRunes())
				w += GetRuneWidth(rune);
			return w;
		}
	}
}
