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
	/// The subset of Unicode Line Breaking Algorithm (UAX #14) break classes relevant to terminal text.
	/// </summary>
	internal enum LineBreakClass
	{
		AL, // Alphabetic (letters / identifier chars — words)
		NU, // Numeric (digits)
		ID, // Ideographic (CJK — breaks freely)
		SP, // Space
		BA, // Break-After (soft hyphen, some dashes)
		BB, // Break-Before
		GL, // Glue / non-breaking (NBSP)
		OP, // Open punctuation ( [ {
		CL, // Close punctuation ) ] }
		QU, // Quotation " '
		IS, // Infix separator . , : ;
		SY, // Symbol /
		PR, // Prefix (currency $ £ ¥)
		PO, // Postfix (% ¢ °)
		HY, // Hyphen-minus
		ZW, // Zero-width space
		CM, // Combining mark
		WJ, // Word joiner
		XX  // Other → resolved as AL (keeps unknown runs whole)
	}

	/// <summary>
	/// Classifies a <see cref="Rune"/> into its <see cref="LineBreakClass"/> per UAX #14, for the scripts
	/// terminal text contains. Pure and allocation-free; AOT-safe (no dependency beyond the BCL and the
	/// existing <see cref="UnicodeWidth"/>). The explicit char table is consulted BEFORE the Unicode general
	/// category because several break classes share one category (e.g. '.' ',' ':' '/' '%' '"' are all
	/// OtherPunctuation; NBSP and space are both SpaceSeparator; ZWSP/WJ/soft-hyphen are all Format).
	/// </summary>
	internal static class LineBreakClassifier
	{
		public static LineBreakClass Classify(Rune r)
		{
			int v = r.Value;

			// 1. Explicit characters whose break class is not derivable from category alone.
			switch (v)
			{
				case 0x0020: return LineBreakClass.SP;   // SPACE
				case 0x00A0: return LineBreakClass.GL;   // NO-BREAK SPACE
				case 0x200B: return LineBreakClass.ZW;   // ZERO WIDTH SPACE
				case 0x2060: return LineBreakClass.WJ;   // WORD JOINER
				case 0xFEFF: return LineBreakClass.WJ;   // ZERO WIDTH NO-BREAK SPACE (BOM) — glue/joiner
				case 0x00AD: return LineBreakClass.BA;   // SOFT HYPHEN — break opportunity
				case '-': return LineBreakClass.HY;      // HYPHEN-MINUS
				case '.': case ',': case ':': case ';': return LineBreakClass.IS;
				case '/': return LineBreakClass.SY;
				case '(': case '[': case '{': return LineBreakClass.OP;
				case ')': case ']': case '}': return LineBreakClass.CL;
				case '"': case '\'': return LineBreakClass.QU;
				case '%': return LineBreakClass.PO;
				case '_': return LineBreakClass.AL;      // underscore binds in identifiers (write_time)
			}

			// Currency symbols → PR (prefix), bind to following number.
			var cat = Rune.GetUnicodeCategory(r);
			if (cat == UnicodeCategory.CurrencySymbol) return LineBreakClass.PR;

			// 2. East-Asian wide → ideographic (breaks freely on both sides).
			if (UnicodeWidth.IsWideRune(r)) return LineBreakClass.ID;

			// 3. General category buckets.
			switch (cat)
			{
				case UnicodeCategory.UppercaseLetter:
				case UnicodeCategory.LowercaseLetter:
				case UnicodeCategory.TitlecaseLetter:
				case UnicodeCategory.ModifierLetter:
				case UnicodeCategory.OtherLetter:
					return LineBreakClass.AL;

				case UnicodeCategory.DecimalDigitNumber:
					return LineBreakClass.NU;

				case UnicodeCategory.NonSpacingMark:
				case UnicodeCategory.SpacingCombiningMark:
				case UnicodeCategory.EnclosingMark:
					return LineBreakClass.CM;

				case UnicodeCategory.SpaceSeparator:
					return LineBreakClass.SP; // other spaces break like SPACE

				case UnicodeCategory.OpenPunctuation:
					return LineBreakClass.OP;
				case UnicodeCategory.ClosePunctuation:
				case UnicodeCategory.FinalQuotePunctuation:
					return LineBreakClass.CL;
				case UnicodeCategory.InitialQuotePunctuation:
					return LineBreakClass.OP;
				case UnicodeCategory.DashPunctuation:
					return LineBreakClass.HY;

				default:
					return LineBreakClass.XX; // resolved as AL by the rule layer (keeps runs whole)
			}
		}
	}
}
