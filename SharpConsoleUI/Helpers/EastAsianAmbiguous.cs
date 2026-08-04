// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// The Unicode East Asian Width "Ambiguous" (EAW=A) class: characters whose cell width is not a
	/// property of the character but of the context it is rendered in. They were historically encoded
	/// in both Western and East Asian character sets, so a terminal configured for CJK draws them two
	/// cells wide and a Western one draws them narrow — and both are correct.
	/// <para>
	/// Wcwidth resolves the class to width 1 and offers no mode ("Choosing single-width for these
	/// characters is easy to justify as the appropriate long-term solution"), which is the right
	/// default but leaves a terminal that says otherwise unserved. This table is the data needed to
	/// honour <see cref="TerminalCapabilities.AmbiguousCharactersAreWide"/>.
	/// </para>
	/// <para>
	/// Generated from the Unicode Character Database, EastAsianWidth.txt, version 15.0.0 — the same
	/// version <see cref="UnicodeWidth"/> resolves against by default. Ranges are sorted, merged and
	/// non-overlapping, so a binary search answers a lookup.
	/// </para>
	/// </summary>
	internal static class EastAsianAmbiguous
	{
		// Flattened [lo, hi] inclusive pairs, ascending. 179 ranges.
		private static readonly int[] Ranges =
		{
			0x000A1, 0x000A1,
			0x000A4, 0x000A4,
			0x000A7, 0x000A8,
			0x000AA, 0x000AA,
			0x000AD, 0x000AE,
			0x000B0, 0x000B4,
			0x000B6, 0x000BA,
			0x000BC, 0x000BF,
			0x000C6, 0x000C6,
			0x000D0, 0x000D0,
			0x000D7, 0x000D8,
			0x000DE, 0x000E1,
			0x000E6, 0x000E6,
			0x000E8, 0x000EA,
			0x000EC, 0x000ED,
			0x000F0, 0x000F0,
			0x000F2, 0x000F3,
			0x000F7, 0x000FA,
			0x000FC, 0x000FC,
			0x000FE, 0x000FE,
			0x00101, 0x00101,
			0x00111, 0x00111,
			0x00113, 0x00113,
			0x0011B, 0x0011B,
			0x00126, 0x00127,
			0x0012B, 0x0012B,
			0x00131, 0x00133,
			0x00138, 0x00138,
			0x0013F, 0x00142,
			0x00144, 0x00144,
			0x00148, 0x0014B,
			0x0014D, 0x0014D,
			0x00152, 0x00153,
			0x00166, 0x00167,
			0x0016B, 0x0016B,
			0x001CE, 0x001CE,
			0x001D0, 0x001D0,
			0x001D2, 0x001D2,
			0x001D4, 0x001D4,
			0x001D6, 0x001D6,
			0x001D8, 0x001D8,
			0x001DA, 0x001DA,
			0x001DC, 0x001DC,
			0x00251, 0x00251,
			0x00261, 0x00261,
			0x002C4, 0x002C4,
			0x002C7, 0x002C7,
			0x002C9, 0x002CB,
			0x002CD, 0x002CD,
			0x002D0, 0x002D0,
			0x002D8, 0x002DB,
			0x002DD, 0x002DD,
			0x002DF, 0x002DF,
			0x00300, 0x0036F,
			0x00391, 0x003A1,
			0x003A3, 0x003A9,
			0x003B1, 0x003C1,
			0x003C3, 0x003C9,
			0x00401, 0x00401,
			0x00410, 0x0044F,
			0x00451, 0x00451,
			0x02010, 0x02010,
			0x02013, 0x02016,
			0x02018, 0x02019,
			0x0201C, 0x0201D,
			0x02020, 0x02022,
			0x02024, 0x02027,
			0x02030, 0x02030,
			0x02032, 0x02033,
			0x02035, 0x02035,
			0x0203B, 0x0203B,
			0x0203E, 0x0203E,
			0x02074, 0x02074,
			0x0207F, 0x0207F,
			0x02081, 0x02084,
			0x020AC, 0x020AC,
			0x02103, 0x02103,
			0x02105, 0x02105,
			0x02109, 0x02109,
			0x02113, 0x02113,
			0x02116, 0x02116,
			0x02121, 0x02122,
			0x02126, 0x02126,
			0x0212B, 0x0212B,
			0x02153, 0x02154,
			0x0215B, 0x0215E,
			0x02160, 0x0216B,
			0x02170, 0x02179,
			0x02189, 0x02189,
			0x02190, 0x02199,
			0x021B8, 0x021B9,
			0x021D2, 0x021D2,
			0x021D4, 0x021D4,
			0x021E7, 0x021E7,
			0x02200, 0x02200,
			0x02202, 0x02203,
			0x02207, 0x02208,
			0x0220B, 0x0220B,
			0x0220F, 0x0220F,
			0x02211, 0x02211,
			0x02215, 0x02215,
			0x0221A, 0x0221A,
			0x0221D, 0x02220,
			0x02223, 0x02223,
			0x02225, 0x02225,
			0x02227, 0x0222C,
			0x0222E, 0x0222E,
			0x02234, 0x02237,
			0x0223C, 0x0223D,
			0x02248, 0x02248,
			0x0224C, 0x0224C,
			0x02252, 0x02252,
			0x02260, 0x02261,
			0x02264, 0x02267,
			0x0226A, 0x0226B,
			0x0226E, 0x0226F,
			0x02282, 0x02283,
			0x02286, 0x02287,
			0x02295, 0x02295,
			0x02299, 0x02299,
			0x022A5, 0x022A5,
			0x022BF, 0x022BF,
			0x02312, 0x02312,
			0x02460, 0x024E9,
			0x024EB, 0x0254B,
			0x02550, 0x02573,
			0x02580, 0x0258F,
			0x02592, 0x02595,
			0x025A0, 0x025A1,
			0x025A3, 0x025A9,
			0x025B2, 0x025B3,
			0x025B6, 0x025B7,
			0x025BC, 0x025BD,
			0x025C0, 0x025C1,
			0x025C6, 0x025C8,
			0x025CB, 0x025CB,
			0x025CE, 0x025D1,
			0x025E2, 0x025E5,
			0x025EF, 0x025EF,
			0x02605, 0x02606,
			0x02609, 0x02609,
			0x0260E, 0x0260F,
			0x0261C, 0x0261C,
			0x0261E, 0x0261E,
			0x02640, 0x02640,
			0x02642, 0x02642,
			0x02660, 0x02661,
			0x02663, 0x02665,
			0x02667, 0x0266A,
			0x0266C, 0x0266D,
			0x0266F, 0x0266F,
			0x0269E, 0x0269F,
			0x026BF, 0x026BF,
			0x026C6, 0x026CD,
			0x026CF, 0x026D3,
			0x026D5, 0x026E1,
			0x026E3, 0x026E3,
			0x026E8, 0x026E9,
			0x026EB, 0x026F1,
			0x026F4, 0x026F4,
			0x026F6, 0x026F9,
			0x026FB, 0x026FC,
			0x026FE, 0x026FF,
			0x0273D, 0x0273D,
			0x02776, 0x0277F,
			0x02B56, 0x02B59,
			0x03248, 0x0324F,
			0x0E000, 0x0F8FF,
			0x0FE00, 0x0FE0F,
			0x0FFFD, 0x0FFFD,
			0x1F100, 0x1F10A,
			0x1F110, 0x1F12D,
			0x1F130, 0x1F169,
			0x1F170, 0x1F18D,
			0x1F18F, 0x1F190,
			0x1F19B, 0x1F1AC,
			0xE0100, 0xE01EF,
			0xF0000, 0xFFFFD,
			0x100000, 0x10FFFD,
		};

		/// <summary>
		/// Ranges deliberately NOT treated as wide even when the terminal reports ambiguous-as-wide,
		/// because SharpConsoleUI draws its own chrome from them: window borders, scrollbars, progress
		/// bars, checkbox and tree glyphs, menu arrows.
		/// <para>
		/// This is a deliberate inaccuracy and worth being clear about. On a terminal that really does
		/// render these two cells wide, excluding them does not make the chrome correct — the terminal
		/// draws what it draws. It keeps the chrome exactly as (in)correct as it is today, while letting
		/// the policy fix ordinary text: Greek, Cyrillic, degree signs, ellipses, curly quotes. Making
		/// chrome genuinely correct means dropping this exclusion AND auditing every renderer that
		/// assumes one glyph fills one column.
		/// </para>
		/// </summary>
		private static readonly int[] ChromeExclusions =
		{
			0x2190, 0x21FF, // Arrows — menu/submenu indicators (→ alone appears ~380 times)
			0x2500, 0x257F, // Box Drawing — window borders, separators, table rules (~1500 uses)
			0x2580, 0x259F, // Block Elements — scrollbar thumbs, progress bars, shading
			0x25A0, 0x25FF, // Geometric Shapes — checkbox, radio, tree expanders, spinners
		};

		// Deliberately NOT excluded: Miscellaneous Symbols (U+2600..U+26FF). The only glyph this
		// library draws from that block is ⚠ (U+26A0), which is East Asian NEUTRAL rather than
		// Ambiguous, so the policy never touches it. Excluding the block would have narrowed 69
		// genuinely-ambiguous codepoints (★, ☎, …) in user content to protect nothing.

		/// <summary>
		/// True when <paramref name="codepoint"/> is East Asian Ambiguous AND not one of the glyph
		/// ranges this library draws its own chrome from.
		/// </summary>
		public static bool IsWidenedWhenAmbiguousWide(int codepoint)
			=> Contains(Ranges, codepoint) && !Contains(ChromeExclusions, codepoint);

		/// <summary>True when the codepoint is East Asian Ambiguous, chrome exclusions aside.</summary>
		public static bool IsAmbiguous(int codepoint) => Contains(Ranges, codepoint);

		/// <summary>True when the codepoint is one of the chrome ranges held at width 1.</summary>
		public static bool IsChromeExcluded(int codepoint) => Contains(ChromeExclusions, codepoint);

		private static bool Contains(int[] table, int codepoint)
		{
			int lo = 0, hi = table.Length / 2 - 1;
			while (lo <= hi)
			{
				int mid = (lo + hi) >> 1;
				if (codepoint < table[mid * 2]) hi = mid - 1;
				else if (codepoint > table[mid * 2 + 1]) lo = mid + 1;
				else return true;
			}
			return false;
		}
	}
}
