// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides sanitization for text entering the character buffer.
	/// Prevents terminal escape injection by filtering control characters,
	/// BiDi overrides, and other unsafe runes before they reach cells.
	/// </summary>
	public static class TextSanitizer
	{
		/// <summary>
		/// The replacement character used when an unsafe rune is encountered.
		/// </summary>
		public static readonly Rune ReplacementCharacter = new('\uFFFD');

		/// <summary>
		/// Returns true if the rune must never reach a cell in the character buffer.
		/// This includes C0/C1 control characters, DEL, BiDi overrides, surrogates,
		/// and noncharacters.
		/// </summary>
		public static bool IsUnsafeRune(Rune r)
		{
			int value = r.Value;

			// C0 controls: U+0000-U+001F (ESC, BEL, LF, CR, TAB, etc.)
			if (value <= 0x001F)
				return true;

			// DEL
			if (value == 0x007F)
				return true;

			// C1 controls: U+0080-U+009F
			if (value >= 0x0080 && value <= 0x009F)
				return true;

			// BiDi overrides and isolates
			if (value >= 0x202A && value <= 0x202E)
				return true;
			if (value >= 0x2066 && value <= 0x2069)
				return true;

			// Unicode surrogates (should not appear as valid Rune, but defense-in-depth)
			if (value >= 0xD800 && value <= 0xDFFF)
				return true;

			// Noncharacters: U+FDD0-U+FDEF
			if (value >= 0xFDD0 && value <= 0xFDEF)
				return true;

			// Noncharacters: U+xFFFE and U+xFFFF for each plane
			if ((value & 0xFFFF) == 0xFFFE || (value & 0xFFFF) == 0xFFFF)
				return true;

			return false;
		}

		/// <summary>
		/// Returns the rune unchanged if safe, or U+FFFD if it is unsafe.
		/// </summary>
		public static Rune SanitizeRune(Rune r)
		{
			return IsUnsafeRune(r) ? ReplacementCharacter : r;
		}

		/// <summary>
		/// Returns true only for runes that are legitimately zero-width and safe
		/// to attach as combiners. This includes Unicode categories Mn (nonspacing mark),
		/// Me (enclosing mark), Mc (spacing combining mark), and Cf (format) minus BiDi
		/// overrides, plus VS1-VS16, ZWJ (U+200D), ZWNJ (U+200C), emoji skin tone
		/// modifiers (U+1F3FB-U+1F3FF), and regional indicator symbols (U+1F1E6-U+1F1FF).
		/// </summary>
		public static bool IsSafeCombiner(Rune r)
		{
			int value = r.Value;

			// Reject anything on the unsafe list first
			if (IsUnsafeRune(r))
				return false;

			// ZWJ and ZWNJ are safe zero-width joiners
			if (value == 0x200D || value == 0x200C)
				return true;

			// Variation selectors VS1-VS16 (U+FE00-U+FE0F)
			if (value >= 0xFE00 && value <= 0xFE0F)
				return true;

			// Variation selectors supplement (U+E0100-U+E01EF)
			if (value >= 0xE0100 && value <= 0xE01EF)
				return true;

			// Emoji skin tone modifiers (Fitzpatrick scale)
			if (value >= 0x1F3FB && value <= 0x1F3FF)
				return true;

			// Regional indicator symbols (used in flag sequences)
			if (value >= 0x1F1E6 && value <= 0x1F1FF)
				return true;

			// Tag characters used in emoji tag sequences (e.g., flag subdivisions)
			if (value >= 0xE0020 && value <= 0xE007F)
				return true;

			var category = CharUnicodeInfo.GetUnicodeCategory(value);

			// Mn: Nonspacing mark (combining accents, diacritics)
			// Me: Enclosing mark (circles, squares around characters)
			// Mc: Spacing combining mark (Indic vowel signs, etc.)
			if (category == UnicodeCategory.NonSpacingMark ||
				category == UnicodeCategory.EnclosingMark ||
				category == UnicodeCategory.SpacingCombiningMark)
				return true;

			// Cf: Format characters, but only those not already blocked
			// (BiDi overrides are blocked by IsUnsafeRune above)
			if (category == UnicodeCategory.Format)
				return true;

			return false;
		}
	}
}
