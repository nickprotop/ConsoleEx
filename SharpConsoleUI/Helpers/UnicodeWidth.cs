// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides display width information for Unicode characters.
	/// Characters with East Asian Width property "Wide" or "Fullwidth" occupy 2 terminal columns.
	/// Supports both BMP characters (via char) and supplementary plane characters (via Rune).
	/// </summary>
	public static class UnicodeWidth
	{
		/// <summary>
		/// Returns the display width of a character: 2 for wide/fullwidth, 1 for all others.
		/// </summary>
		public static int GetCharWidth(char c) => IsWide(c) ? 2 : 1;

		/// <summary>
		/// Returns the display width of a Rune: 2 for wide/fullwidth, 1 for all others.
		/// </summary>
		public static int GetRuneWidth(Rune r) => IsWideRune(r) ? 2 : 1;

		/// <summary>
		/// Returns the total display width of a string, summing per-Rune widths.
		/// Correctly handles surrogate pairs for supplementary plane characters.
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

		/// <summary>
		/// Returns true if the character occupies 2 terminal columns (East Asian Wide/Fullwidth).
		/// Ranges are sorted by codepoint for efficient short-circuit evaluation.
		/// </summary>
		public static bool IsWide(char c)
		{
			// Fast path: ASCII and most Latin/Greek/Cyrillic are narrow
			if (c < 0x1100)
				return false;

			// Hangul Jamo (U+1100-U+115F): leading consonants are wide
			if (c <= 0x115F)
				return true;

			// Fast path: gap between Hangul Jamo and next wide range
			if (c < 0x2329)
				return false;

			// Left/Right-Pointing Angle Bracket (U+2329-U+232A)
			if (c <= 0x232A)
				return true;

			// Fast path: gap
			if (c < 0x2E80)
				return false;

			// CJK Radicals Supplement (U+2E80-U+2EFF)
			if (c <= 0x2EFF)
				return true;

			// Kangxi Radicals (U+2F00-U+2FDF)
			if (c <= 0x2FDF)
				return true;

			// Fast path: small gap
			if (c < 0x2FF0)
				return false;

			// Ideographic Description Characters (U+2FF0-U+2FFF)
			if (c <= 0x2FFF)
				return true;

			// CJK Symbols and Punctuation (U+3000-U+303F)
			// Hiragana (U+3040-U+309F)
			// Katakana (U+30A0-U+30FF)
			// Bopomofo (U+3100-U+312F)
			// Hangul Compatibility Jamo (U+3130-U+318F)
			// Kanbun (U+3190-U+319F)
			// Bopomofo Extended (U+31A0-U+31BF)
			// CJK Strokes (U+31C0-U+31EF)
			// Katakana Phonetic Extensions (U+31F0-U+31FF)
			// Enclosed CJK Letters and Months (U+3200-U+32FF)
			// CJK Compatibility (U+3300-U+33FF)
			// CJK Unified Ideographs Extension A (U+3400-U+4DBF)
			if (c >= 0x3000 && c <= 0x4DBF)
				return true;

			// CJK Unified Ideographs (U+4E00-U+9FFF)
			if (c >= 0x4E00 && c <= 0x9FFF)
				return true;

			// Yi Syllables (U+A000-U+A48F)
			// Yi Radicals (U+A490-U+A4CF)
			if (c >= 0xA000 && c <= 0xA4CF)
				return true;

			// Hangul Syllables (U+AC00-U+D7AF)
			if (c >= 0xAC00 && c <= 0xD7AF)
				return true;

			// CJK Compatibility Ideographs (U+F900-U+FAFF)
			if (c >= 0xF900 && c <= 0xFAFF)
				return true;

			// CJK Compatibility Forms (U+FE30-U+FE4F)
			if (c >= 0xFE30 && c <= 0xFE4F)
				return true;

			// Fullwidth Forms (U+FF01-U+FF60) — fullwidth ASCII and symbols
			// Note: U+FF61-U+FFDC are halfwidth (Katakana, Hangul) — NOT wide
			if (c >= 0xFF01 && c <= 0xFF60)
				return true;

			// Fullwidth symbol variants (U+FFE0-U+FFE6)
			if (c >= 0xFFE0 && c <= 0xFFE6)
				return true;

			return false;
		}

		/// <summary>
		/// Returns true if the Rune occupies 2 terminal columns.
		/// Handles both BMP (delegates to IsWide) and supplementary plane characters.
		/// Emoji ranges use precise Unicode 15.0 East_Asian_Width "W" values
		/// (NOT the blanket 1F000-1FAFF range, which includes neutral-width emoji).
		/// </summary>
		public static bool IsWideRune(Rune r)
		{
			int cp = r.Value;

			// BMP: delegate to existing char-based logic
			if (cp <= 0xFFFF)
				return IsWide((char)cp);

			// Fast path: below supplementary wide ranges
			if (cp < 0x1F004)
				return false;

			// === Emoji/symbol ranges with East_Asian_Width = "W" (Unicode 15.0) ===
			// These are scattered with gaps — emoji like U+1F336 (🌶) and U+1F37D (🍽)
			// have EAW = "N" and render 1-wide in terminals.

			// Isolated wide emoji codepoints
			if (cp == 0x1F004) return true;  // 🀄 Mahjong Red Dragon
			if (cp == 0x1F0CF) return true;  // 🃏 Joker
			if (cp == 0x1F18E) return true;  // 🆎 AB Button

			// Squared CJK/Latin symbols
			if (cp >= 0x1F191 && cp <= 0x1F19A) return true;  // 🆑-🆚

			// Enclosed ideographic supplement
			if (cp >= 0x1F200 && cp <= 0x1F202) return true;
			if (cp >= 0x1F210 && cp <= 0x1F23B) return true;
			if (cp >= 0x1F240 && cp <= 0x1F248) return true;
			if (cp >= 0x1F250 && cp <= 0x1F251) return true;
			if (cp >= 0x1F260 && cp <= 0x1F265) return true;

			// Misc Symbols and Pictographs, Emoticons, etc.
			if (cp >= 0x1F300 && cp <= 0x1F320) return true;
			if (cp >= 0x1F32D && cp <= 0x1F335) return true;
			// Gap: U+1F336 (🌶) is EAW=N
			if (cp >= 0x1F337 && cp <= 0x1F37C) return true;
			// Gap: U+1F37D (🍽) is EAW=N
			if (cp >= 0x1F37E && cp <= 0x1F393) return true;
			// Gap: U+1F394-U+1F39F are EAW=N
			if (cp >= 0x1F3A0 && cp <= 0x1F3CA) return true;
			// Gap: U+1F3CB-U+1F3CE are EAW=N
			if (cp >= 0x1F3CF && cp <= 0x1F3D3) return true;
			// Gap: U+1F3D4-U+1F3DF are EAW=N
			if (cp >= 0x1F3E0 && cp <= 0x1F3F0) return true;
			// Gap: U+1F3F1-U+1F3F3 are EAW=N
			if (cp == 0x1F3F4) return true;  // 🏴 Black Flag
			// Gap: U+1F3F5-U+1F3F7 are EAW=N
			if (cp >= 0x1F3F8 && cp <= 0x1F43E) return true;
			// Gap: U+1F43F is EAW=N
			if (cp == 0x1F440) return true;  // 👀 Eyes
			// Gap: U+1F441 is EAW=N
			if (cp >= 0x1F442 && cp <= 0x1F4FC) return true;
			// Gap: U+1F4FD-U+1F4FE are EAW=N
			if (cp >= 0x1F4FF && cp <= 0x1F53D) return true;
			// Gap: U+1F53E-U+1F54A are EAW=N
			if (cp >= 0x1F54B && cp <= 0x1F54E) return true;
			// Gap: U+1F54F is EAW=N
			if (cp >= 0x1F550 && cp <= 0x1F567) return true;
			// Gap: U+1F568-U+1F579 are EAW=N
			if (cp == 0x1F57A) return true;  // 🕺 Man Dancing
			// Gap
			if (cp >= 0x1F595 && cp <= 0x1F596) return true;  // 🖕🖖
			// Gap
			if (cp == 0x1F5A4) return true;  // 🖤 Black Heart
			// Gap
			if (cp >= 0x1F5FB && cp <= 0x1F64F) return true;

			// Transport and Map Symbols
			if (cp >= 0x1F680 && cp <= 0x1F6C5) return true;
			// Gap: U+1F6C6-U+1F6CB are EAW=N
			if (cp == 0x1F6CC) return true;  // 🛌 Sleeping Accommodation
			// Gap: U+1F6CD-U+1F6CF are EAW=N
			if (cp >= 0x1F6D0 && cp <= 0x1F6D2) return true;
			// Gap: U+1F6D3-U+1F6D4 are EAW=N
			if (cp >= 0x1F6D5 && cp <= 0x1F6D7) return true;
			// Gap
			if (cp >= 0x1F6DC && cp <= 0x1F6DF) return true;
			// Gap
			if (cp >= 0x1F6EB && cp <= 0x1F6EC) return true;
			// Gap
			if (cp >= 0x1F6F4 && cp <= 0x1F6FC) return true;

			// Geometric shapes extended
			if (cp >= 0x1F7E0 && cp <= 0x1F7EB) return true;
			if (cp == 0x1F7F0) return true;

			// Supplemental Symbols and Pictographs
			if (cp >= 0x1F90C && cp <= 0x1F93A) return true;
			if (cp >= 0x1F93C && cp <= 0x1F945) return true;
			if (cp >= 0x1F947 && cp <= 0x1F9FF) return true;

			// Chess symbols
			if (cp >= 0x1FA00 && cp <= 0x1FA53) return true;

			// Extended-A
			if (cp >= 0x1FA60 && cp <= 0x1FA6D) return true;
			if (cp >= 0x1FA70 && cp <= 0x1FA7C) return true;
			if (cp >= 0x1FA80 && cp <= 0x1FA88) return true;
			if (cp >= 0x1FA90 && cp <= 0x1FABD) return true;
			if (cp >= 0x1FABF && cp <= 0x1FAC5) return true;
			if (cp >= 0x1FACE && cp <= 0x1FADB) return true;
			if (cp >= 0x1FAE0 && cp <= 0x1FAE8) return true;
			if (cp >= 0x1FAF0 && cp <= 0x1FAF8) return true;

			// === CJK supplementary ideographs ===

			// CJK Unified Ideographs Extension B: U+20000-U+2A6DF
			if (cp >= 0x20000 && cp <= 0x2A6DF)
				return true;

			// CJK Unified Ideographs Extension C: U+2A700-U+2B73F
			if (cp >= 0x2A700 && cp <= 0x2B73F)
				return true;

			// CJK Unified Ideographs Extension D: U+2B740-U+2B81F
			if (cp >= 0x2B740 && cp <= 0x2B81F)
				return true;

			// CJK Unified Ideographs Extension E: U+2B820-U+2CEAF
			if (cp >= 0x2B820 && cp <= 0x2CEAF)
				return true;

			// CJK Unified Ideographs Extension F: U+2CEB0-U+2EBEF
			if (cp >= 0x2CEB0 && cp <= 0x2EBEF)
				return true;

			// CJK Compatibility Ideographs Supplement: U+2F800-U+2FA1F
			if (cp >= 0x2F800 && cp <= 0x2FA1F)
				return true;

			// CJK Unified Ideographs Extension G: U+30000-U+3134F
			if (cp >= 0x30000 && cp <= 0x3134F)
				return true;

			return false;
		}
	}
}
