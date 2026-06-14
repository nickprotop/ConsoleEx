using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	public class UnicodeWidthColumnMappingTests
	{
		[Theory]
		[InlineData("abc", 0, 0)]
		[InlineData("abc", 2, 2)]
		[InlineData("abc", 5, 3)]            // past end clamps to length
		[InlineData("中文", 0, 0)]
		[InlineData("中文", 2, 1)]           // column 2 = after first wide char = char index 1
		[InlineData("中文", 1, 0)]           // column 1 lands INSIDE first wide char → its start (index 0)
		[InlineData("中文", 4, 2)]
		[InlineData("a中b", 1, 1)]           // after 'a'
		[InlineData("a中b", 3, 2)]           // 'a'(1) + 中(2 cols) = col 3 → char index 2
		public void ColumnToCharOffset_MapsColumnsToCharIndices(string s, int column, int expectedCharOffset)
		{
			Assert.Equal(expectedCharOffset, UnicodeWidth.ColumnToCharOffset(s, column));
		}

		[Theory]
		[InlineData("abc", 0, 0)]
		[InlineData("abc", 2, 2)]
		[InlineData("中文", 1, 2)]           // 1 char (中) = 2 columns
		[InlineData("中文", 2, 4)]
		[InlineData("a中b", 2, 3)]           // 'a'(1) + 中(2) = 3 columns
		public void CharOffsetToColumn_MapsCharIndicesToColumns(string s, int charOffset, int expectedColumn)
		{
			Assert.Equal(expectedColumn, UnicodeWidth.CharOffsetToColumn(s, charOffset));
		}

		[Fact]
		public void TakeColumns_AdvancesByWholeRunes_NeverSplitsWideChar()
		{
			// "中文测" in 3 columns: must take 1 char (中=2 cols), NOT split into the 2nd column.
			var (endChar, width) = UnicodeWidth.TakeColumns("中文测", 0, 3);
			Assert.Equal(1, endChar);   // only 中 fits (2 cols; adding 文 would be 4 > 3)
			Assert.Equal(2, width);
		}

		[Fact]
		public void TakeColumns_NonEmptyRemainder_AlwaysAdvances()
		{
			// Guard against infinite-loop/zero-step: a wide char wider than maxColumns must still
			// advance by one rune (take it whole) rather than return 0.
			var (endChar, _) = UnicodeWidth.TakeColumns("中文", 0, 1);
			Assert.True(endChar >= 1, "TakeColumns must advance at least one rune for a non-empty remainder");
		}

		[Fact]
		public void ColumnToChar_And_CharToColumn_RoundTrip()
		{
			string s = "abc中文📦def";
			for (int ci = 0; ci <= s.Length; ci++)
			{
				int col = UnicodeWidth.CharOffsetToColumn(s, ci);
				int back = UnicodeWidth.ColumnToCharOffset(s, col);
				Assert.True(back <= ci); // round-trip lands at/before ci on a rune boundary
			}
		}

		// --- Combining marks: the 0-width combiner must NOT consume a display column ---

		[Fact]
		public void GetStringWidth_BaseCharPlusCombiningAcute_CombinerIsZeroWidth()
		{
			// "a" + U+0301 (combining acute) + "bc" = 3 display columns, 4 UTF-16 units.
			const string s = "ábc";
			Assert.Equal(4, s.Length);                          // sanity: 4 code units
			Assert.Equal(3, UnicodeWidth.GetStringWidth(s));    // combiner contributes 0 columns
		}

		[Fact]
		public void GetRuneWidth_CombiningAcute_IsZero()
		{
			// Verify the model: U+0301 is 0-wide (basis for the combining-mark wrap tests).
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new System.Text.Rune(0x0301)));
		}

		[Fact]
		public void GetStringWidth_LineOfCombiningMarks_CountsBasesOnly()
		{
			// 6 base 'e' chars each followed by a combining acute → 6 columns (combiners are 0).
			const string s = "éééééé";
			Assert.Equal(12, s.Length);                         // 6 bases + 6 combiners
			Assert.Equal(6, UnicodeWidth.GetStringWidth(s));
		}

		// --- VS16 (U+FE0F): the library WIDENS the preceding emoji-presentation base to 2 columns ---

		[Fact]
		public void GetStringWidth_GearPlusVariationSelector16_IsWidenedToTwoColumns()
		{
			// U+2699 GEAR alone is reported 1-wide; U+FE0F is 0-wide on its own.
			// The width model treats "gear + VS16" as emoji presentation = 2 columns.
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new System.Text.Rune(0x2699)));
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new System.Text.Rune(0xFE0F)));
			Assert.Equal(2, UnicodeWidth.GetStringWidth("⚙️"));        // = "⚙️"
		}

		[Fact]
		public void GetStringWidth_Vs16WidenedGlyphInLine_CountsAsTwoColumns()
		{
			// "⚙️ text" = gear+VS16 (2) + space (1) + "text" (4) = 7 columns.
			Assert.Equal(7, UnicodeWidth.GetStringWidth("⚙️ text"));
		}

		// --- Lone/unpaired surrogates: the transient state while typing an emoji must NOT throw ---

		[Theory]
		[InlineData(1)]
		[InlineData(5)]
		[InlineData(10)]
		[InlineData(30)]
		public void TakeColumns_LoneHighSurrogate_DoesNotThrow(int maxColumns)
		{
			// "\uD83D" is the HIGH surrogate of 📦 (U+1F4E6) with no low surrogate — the exact
			// transient state while the user is mid-typing the emoji. Rune.GetRuneAt throws on
			// this; TakeColumns must instead treat it as a width-1 glyph and make progress.
			const string s = "abc中文测试\uD83D";

			var ex = Record.Exception(() => UnicodeWidth.TakeColumns(s, 0, maxColumns));
			Assert.Null(ex);

			var (endChar, width) = UnicodeWidth.TakeColumns(s, 0, maxColumns);
			Assert.True(endChar > 0, "TakeColumns must advance past the start for a non-empty string");
			Assert.True(width >= 0);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(5)]
		[InlineData(10)]
		[InlineData(30)]
		public void TakeColumns_LoneLowSurrogate_DoesNotThrow(int maxColumns)
		{
			// Leading lone LOW surrogate (also unpaired) must be handled without throwing.
			const string s = "\uDCE6abc";

			var ex = Record.Exception(() => UnicodeWidth.TakeColumns(s, 0, maxColumns));
			Assert.Null(ex);

			var (endChar, width) = UnicodeWidth.TakeColumns(s, 0, maxColumns);
			Assert.True(endChar > 0, "TakeColumns must advance past a leading lone surrogate");
			Assert.True(width >= 0);
		}

		[Theory]
		[InlineData(2, 2, 2)]    // "ab" (2 cols) fills maxColumns exactly
		[InlineData(3, 3, 3)]    // "abc" (3 cols)
		[InlineData(100, 12, 16)] // whole string: a b c 中(2) 文(2) 测(2) 试(2) 📦(2) d e f = 16 cols, 12 UTF-16 units (📦 is a surrogate pair)
		public void TakeColumns_WellFormedEmoji_StillCorrect(int maxColumns, int expectedEndChar, int expectedWidth)
		{
			// Valid input (complete 📦 surrogate pair) must be unaffected by the lone-surrogate guard.
			const string s = "abc中文测试📦def";
			var (endChar, width) = UnicodeWidth.TakeColumns(s, 0, maxColumns);
			Assert.Equal(expectedEndChar, endChar);
			Assert.Equal(expectedWidth, width);
		}
	}
}
