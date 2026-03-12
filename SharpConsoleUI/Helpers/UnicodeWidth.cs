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
		/// <summary>
		/// Returns the display width of a character in terminal columns (0, 1, or 2).
		/// Spacing Combining Marks (Mc) are corrected to width 1.
		/// </summary>
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

		/// <summary>
		/// Returns the display width of a Rune in terminal columns (0, 1, or 2).
		/// Spacing Combining Marks (Mc) are corrected to width 1.
		/// </summary>
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

		/// <summary>Returns true if the character occupies 2 terminal columns.</summary>
		public static bool IsWide(char c) => GetCharWidth(c) == 2;

		/// <summary>Returns true if the Rune occupies 2 terminal columns.</summary>
		public static bool IsWideRune(Rune r) => GetRuneWidth(r) == 2;

		/// <summary>Returns true if the Rune occupies 0 terminal columns (combining mark, etc.).</summary>
		public static bool IsZeroWidth(Rune r) => GetRuneWidth(r) == 0;

		/// <summary>
		/// Returns true if the rune is Variation Selector 16 (U+FE0F),
		/// which widens certain emoji from 1 to 2 terminal columns.
		/// </summary>
		public static bool IsVS16(Rune r) => r.Value == 0xFE0F;

		/// <summary>
		/// Returns true if VS16 (U+FE0F) after this rune widens it from 1 to 2 columns.
		/// Uses the Wcwidth library's Vs16Table for accurate lookup.
		/// Returns false if the terminal does not support VS16 widening
		/// (detected at startup via TerminalCapabilities.Probe).
		/// </summary>
		public static bool IsVs16Widened(Rune baseRune)
			=> TerminalCapabilities.SupportsVS16Widening
				&& Vs16Table.GetTable(Unicode.Version_9_0_0).Find(baseRune.Value) == 1;

		/// <summary>
		/// Returns the total display width of a string, summing per-Rune widths.
		/// Zero-width characters contribute 0 to the total.
		/// VS16 (U+FE0F) after a widenable emoji adds 1 column (widening from 1 to 2).
		/// </summary>
		public static int GetStringWidth(string s)
		{
			if (string.IsNullOrEmpty(s))
				return 0;

			int w = 0;
			Rune? lastMeasured = null;
			foreach (var rune in s.EnumerateRunes())
			{
				if (IsVS16(rune) && lastMeasured != null && IsVs16Widened(lastMeasured.Value))
				{
					w += 1; // widen from 1→2
					lastMeasured = null;
					continue;
				}
				int rw = GetRuneWidth(rune);
				if (rw > 0) lastMeasured = rune;
				w += rw;
			}
			return w;
		}
	}
}
