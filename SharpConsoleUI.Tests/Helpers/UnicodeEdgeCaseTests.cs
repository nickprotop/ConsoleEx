using SharpConsoleUI.Helpers;
using System.Globalization;
using System.Text;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	/// <summary>
	/// Comprehensive Unicode edge case tests for UnicodeWidth.
	/// Covers emoji sequences, combining marks, Mc/Mn distinction,
	/// invisible characters, and mixed-script strings.
	/// </summary>
	public class UnicodeEdgeCaseTests
	{
		#region Emoji ZWJ Sequences

		[Fact]
		public void GetRuneWidth_ZWJ_Returns0()
		{
			// U+200D Zero Width Joiner
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x200D)));
		}

		[Fact]
		public void GetStringWidth_FamilyEmoji_CountsBaseAndModifiers()
		{
			// 👨‍👩‍👦 = 👨(2) + ZWJ(0) + 👩(2) + ZWJ(0) + 👦(2) = 6
			string family = "\U0001F468\u200D\U0001F469\u200D\U0001F466";
			Assert.Equal(6, UnicodeWidth.GetStringWidth(family));
		}

		[Fact]
		public void GetStringWidth_FourPersonFamily_CountsCorrectly()
		{
			// 👨‍👩‍👧‍👦 = 👨(2) + ZWJ(0) + 👩(2) + ZWJ(0) + 👧(2) + ZWJ(0) + 👦(2) = 8
			string family = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";
			Assert.Equal(8, UnicodeWidth.GetStringWidth(family));
		}

		[Fact]
		public void GetStringWidth_WomanTechnologist_CountsCorrectly()
		{
			// 👩‍💻 = 👩(2) + ZWJ(0) + 💻(2) = 4
			string techWoman = "\U0001F469\u200D\U0001F4BB";
			Assert.Equal(4, UnicodeWidth.GetStringWidth(techWoman));
		}

		#endregion

		#region Variation Selectors (FE0E text, FE0F emoji)

		[Fact]
		public void GetRuneWidth_FE0F_Returns0()
		{
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0xFE0F)));
		}

		[Fact]
		public void GetRuneWidth_FE0E_Returns0()
		{
			// U+FE0E Variation Selector-15 (text presentation)
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0xFE0E)));
		}

		[Fact]
		public void GetStringWidth_EmojiWithFE0F_ZeroWidthContributes0()
		{
			// ⚡ (U+26A1, width 2) + FE0F (0) = 2
			Assert.Equal(2, UnicodeWidth.GetStringWidth("\u26A1\uFE0F"));
		}

		[Fact]
		public void GetStringWidth_CharWithFE0E_ZeroWidthContributes0()
		{
			// ✏ (U+270F, width 1) + FE0E (0) = 1
			Assert.Equal(1, UnicodeWidth.GetStringWidth("\u270F\uFE0E"));
		}

		[Fact]
		public void GetStringWidth_DuplicateVariationSelectors_AllZeroWidth()
		{
			// ⚡ + FE0F + FE0F = 2 + 0 + 0 = 2
			Assert.Equal(2, UnicodeWidth.GetStringWidth("\u26A1\uFE0F\uFE0F"));
		}

		#endregion

		#region Skin Tone Modifiers (Fitzpatrick Scale)

		[Fact]
		public void GetRuneWidth_SkinToneModifiers_Returns0()
		{
			// Fitzpatrick modifiers U+1F3FB-1F3FF: Wcwidth returns 0, category ModifierSymbol (Sk)
			// They are zero-width and attach to preceding emoji as combiners
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1F3FB))); // Light
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1F3FC))); // Medium-light
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1F3FD))); // Medium
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1F3FE))); // Medium-dark
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1F3FF))); // Dark
		}

		[Fact]
		public void GetStringWidth_EmojiWithSkinTone_ModifierIsZeroWidth()
		{
			// 👋(2) + 🏻(0, zero-width modifier) = 2
			string wave = "\U0001F44B\U0001F3FB";
			Assert.Equal(2, UnicodeWidth.GetStringWidth(wave));
		}

		[Fact]
		public void GetStringWidth_AllFiveSkinTones_Consistent()
		{
			// Each base + modifier = 2 (modifier is zero-width)
			string[] tones = {
				"\U0001F44D\U0001F3FB", // 👍🏻
				"\U0001F44D\U0001F3FC", // 👍🏼
				"\U0001F44D\U0001F3FD", // 👍🏽
				"\U0001F44D\U0001F3FE", // 👍🏾
				"\U0001F44D\U0001F3FF", // 👍🏿
			};
			foreach (var tone in tones)
				Assert.Equal(2, UnicodeWidth.GetStringWidth(tone));
		}

		#endregion

		#region Keycap Sequences

		[Fact]
		public void GetStringWidth_KeycapSequence_CountsCorrectly()
		{
			// 1️⃣ = '1'(1) + FE0F(0) + U+20E3(0, combining enclosing keycap) = 1
			string keycap = "1\uFE0F\u20E3";
			Assert.Equal(1, UnicodeWidth.GetStringWidth(keycap));
		}

		[Fact]
		public void GetStringWidth_HashKeycap_CountsCorrectly()
		{
			// #️⃣ = '#'(1) + FE0F(0) + U+20E3(0) = 1
			string keycap = "#\uFE0F\u20E3";
			Assert.Equal(1, UnicodeWidth.GetStringWidth(keycap));
		}

		[Fact]
		public void GetRuneWidth_EnclosingKeycap_Returns0()
		{
			// U+20E3 Combining Enclosing Keycap — Mn category
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x20E3)));
		}

		#endregion

		#region Flag Emoji (Regional Indicators)

		[Fact]
		public void GetRuneWidth_RegionalIndicator_Returns1()
		{
			// Regional indicator symbols (U+1F1E6-U+1F1FF) — Wcwidth returns 1
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x1F1FA))); // U
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x1F1F8))); // S
		}

		[Fact]
		public void GetStringWidth_USFlag_CountsEachIndicator()
		{
			// 🇺🇸 = Regional U(1) + Regional S(1) = 2
			// Note: Wcwidth treats these as width-1 each
			string usFlag = "\U0001F1FA\U0001F1F8";
			Assert.Equal(2, UnicodeWidth.GetStringWidth(usFlag));
		}

		[Fact]
		public void GetStringWidth_JapanFlag_CountsEachIndicator()
		{
			// 🇯🇵 = Regional J(1) + Regional P(1) = 2
			string jpFlag = "\U0001F1EF\U0001F1F5";
			Assert.Equal(2, UnicodeWidth.GetStringWidth(jpFlag));
		}

		[Fact]
		public void GetStringWidth_SingleRegionalIndicator_Width1()
		{
			// Lone regional indicator (invalid flag) — still width 1
			Assert.Equal(1, UnicodeWidth.GetStringWidth("\U0001F1FA"));
		}

		#endregion

		#region Combining Marks — Multiple and Chained

		[Fact]
		public void GetStringWidth_MultipleCombiningAccents_OnlyBaseWidth()
		{
			// A + combining grave(0) + combining acute(0) + combining tilde(0) + combining circumflex(0) = 1
			string s = "A\u0300\u0301\u0303\u0302";
			Assert.Equal(1, UnicodeWidth.GetStringWidth(s));
		}

		[Fact]
		public void GetStringWidth_CombiningAfterWideChar_BaseWidthOnly()
		{
			// 中(2) + combining grave(0) = 2
			Assert.Equal(2, UnicodeWidth.GetStringWidth("\u4E2D\u0300"));
		}

		[Fact]
		public void GetStringWidth_CombiningAfterEmoji_BaseWidthOnly()
		{
			// 🔥(2) + combining grave(0) = 2
			Assert.Equal(2, UnicodeWidth.GetStringWidth("\U0001F525\u0300"));
		}

		[Fact]
		public void GetRuneWidth_CombiningFromDifferentBlocks_AllZero()
		{
			// Latin combining: U+0300-U+036F
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x0300))); // Combining Grave
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x036F))); // Combining Latin Small Letter X

			// Combining Diacritical Marks Extended: U+1AB0-U+1AFF
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1AB0)));

			// Combining Diacritical Marks Supplement: U+1DC0-U+1DFF
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x1DC0)));
		}

		[Fact]
		public void GetRuneWidth_EnclosingMarks_Returns0()
		{
			// Combining Enclosing Circle U+20DD
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x20DD)));
			// Combining Enclosing Square U+20DE
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x20DE)));
		}

		#endregion

		#region Spacing Combining Marks (Mc) — Cross-Script Coverage

		[Fact]
		public void GetRuneWidth_MalayalamMcMark_Returns1()
		{
			// U+0D3E MALAYALAM VOWEL SIGN AA — Mc
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x0D3E)));
		}

		[Fact]
		public void GetRuneWidth_GujaratiMcMark_Returns1()
		{
			// U+0ABE GUJARATI VOWEL SIGN AA — Mc
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x0ABE)));
		}

		[Fact]
		public void GetRuneWidth_OriyaMcMark_Returns1()
		{
			// U+0B3E ORIYA VOWEL SIGN AA — Mc
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x0B3E)));
		}

		[Fact]
		public void GetRuneWidth_KannadaMcMark_Returns1()
		{
			// U+0CBE KANNADA VOWEL SIGN AA — Mc
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x0CBE)));
		}

		[Fact]
		public void GetRuneWidth_TeluguVowelSign_Returns0()
		{
			// U+0C3E TELUGU VOWEL SIGN AA — category Mn (NonSpacingMark), NOT Mc
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x0C3E)));
		}

		[Fact]
		public void GetRuneWidth_KhmerMcMark_Returns1()
		{
			// U+17B6 KHMER VOWEL SIGN AA — Mc
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x17B6)));
		}

		[Fact]
		public void GetRuneWidth_BurmeseMcMark_Returns1()
		{
			// U+103B MYANMAR CONSONANT SIGN MEDIAL YA — Mc
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x103B)));
		}

		[Theory]
		[InlineData(0x093E, "Devanagari")]
		[InlineData(0x09BE, "Bengali")]
		[InlineData(0x0ABE, "Gujarati")]
		[InlineData(0x0B3E, "Oriya")]
		[InlineData(0x0BBE, "Tamil")]
		[InlineData(0x0CBE, "Kannada")]
		[InlineData(0x0D3E, "Malayalam")]
		[InlineData(0x17B6, "Khmer")]
		[InlineData(0x103B, "Myanmar")]
		public void GetRuneWidth_IndicMcVowelSignAA_Returns1(int codepoint, string script)
		{
			var rune = new Rune(codepoint);
			Assert.Equal(UnicodeCategory.SpacingCombiningMark, Rune.GetUnicodeCategory(rune));
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(rune));
		}

		[Fact]
		public void GetRuneWidth_ThaiSaraA_Returns1_AsOtherLetter()
		{
			// U+0E30 THAI CHARACTER SARA A — category Lo (OtherLetter), Wcwidth returns 1 naturally
			// Not Mc, but still width 1
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x0E30)));
		}

		[Fact]
		public void McOverride_DoesNotAffectMnMarks()
		{
			// Verify Mn marks from same scripts are still 0
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x094D))); // Devanagari Virama (Mn)
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x0300))); // Combining Grave (Mn)
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x0947))); // Devanagari Vowel E (Mn)
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x09CD))); // Bengali Virama (Mn)
		}

		[Fact]
		public void GetStringWidth_ConsecutiveMcMarks()
		{
			// क(1) + ा(Mc,1) + ी(Mc,1) = 3
			Assert.Equal(3, UnicodeWidth.GetStringWidth("\u0915\u093E\u0940"));
		}

		[Fact]
		public void GetStringWidth_McMarkAtStart()
		{
			// ा(Mc,1) standalone — still width 1
			Assert.Equal(1, UnicodeWidth.GetStringWidth("\u093E"));
		}

		#endregion

		#region Invisible / Formatting Characters

		[Fact]
		public void GetRuneWidth_ZeroWidthSpace_Returns0()
		{
			// U+200B Zero Width Space
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x200B)));
		}

		[Fact]
		public void GetRuneWidth_ZeroWidthNonJoiner_Returns0()
		{
			// U+200C Zero Width Non-Joiner
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x200C)));
		}

		[Fact]
		public void GetRuneWidth_WordJoiner_Returns0()
		{
			// U+2060 Word Joiner
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x2060)));
		}

		[Fact]
		public void GetRuneWidth_SoftHyphen_Returns1()
		{
			// U+00AD Soft Hyphen — Wcwidth returns 1 (Format category, treated as visible)
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x00AD)));
		}

		[Fact]
		public void GetRuneWidth_LeftToRightMark_Returns0()
		{
			// U+200E Left-to-Right Mark
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x200E)));
		}

		[Fact]
		public void GetRuneWidth_RightToLeftMark_Returns0()
		{
			// U+200F Right-to-Left Mark
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x200F)));
		}

		[Fact]
		public void GetRuneWidth_BOM_Returns0()
		{
			// U+FEFF Byte Order Mark / Zero-Width No-Break Space
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0xFEFF)));
		}

		[Fact]
		public void GetStringWidth_StringWithInvisibles_OnlyCountsVisible()
		{
			// "Hello" + ZWSP + "World" = 10 (ZWSP is 0)
			Assert.Equal(10, UnicodeWidth.GetStringWidth("Hello\u200BWorld"));
		}

		[Fact]
		public void GetStringWidth_StringWithBidiMarks_OnlyCountsVisible()
		{
			// "Hi" + LRM + RLM + "Bye" = 5
			Assert.Equal(5, UnicodeWidth.GetStringWidth("Hi\u200E\u200FBye"));
		}

		#endregion

		#region Control Characters

		[Fact]
		public void GetRuneWidth_NullChar_Returns0()
		{
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune('\0')));
		}

		[Fact]
		public void GetRuneWidth_Tab_Returns0()
		{
			// Wcwidth returns -1 for control chars, we map to 0
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune('\t')));
		}

		[Fact]
		public void GetRuneWidth_ControlChars_Return0()
		{
			// C0 control characters
			for (int i = 0; i < 0x20; i++)
			{
				Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(i)));
			}
			// DEL
			Assert.Equal(0, UnicodeWidth.GetRuneWidth(new Rune(0x7F)));
		}

		#endregion

		#region Mixed Script Strings

		[Fact]
		public void GetStringWidth_CjkEmojiDevanagariLatin()
		{
			// "中"(2) + "🔥"(2) + "न"(1) + "ा"(Mc,1) + "A"(1) = 7
			Assert.Equal(7, UnicodeWidth.GetStringWidth("\u4E2D\U0001F525\u0928\u093EA"));
		}

		[Fact]
		public void GetStringWidth_ArabicText_NarrowWidth()
		{
			// Arabic characters are narrow (width 1 each)
			// مرحبا = 5 chars, each width 1
			Assert.Equal(5, UnicodeWidth.GetStringWidth("\u0645\u0631\u062D\u0628\u0627"));
		}

		[Fact]
		public void GetStringWidth_HebrewText_NarrowWidth()
		{
			// שלום = 4 chars, each width 1
			Assert.Equal(4, UnicodeWidth.GetStringWidth("\u05E9\u05DC\u05D5\u05DD"));
		}

		[Fact]
		public void GetStringWidth_KoreanMixed_CorrectWidth()
		{
			// "한" (Hangul, 2) + "A" (1) + "글" (Hangul, 2) = 5
			Assert.Equal(5, UnicodeWidth.GetStringWidth("\uD55CA\uAE00"));
		}

		[Fact]
		public void GetStringWidth_EmojiWithCjkAndSkinTone()
		{
			// "中"(2) + "👍"(2) + "🏽"(0, zero-width modifier) + "文"(2) = 6
			Assert.Equal(6, UnicodeWidth.GetStringWidth("\u4E2D\U0001F44D\U0001F3FD\u6587"));
		}

		#endregion

		#region Precomposed vs Decomposed

		[Fact]
		public void GetStringWidth_PrecomposedE_Returns1()
		{
			// é (U+00E9, precomposed) = width 1
			Assert.Equal(1, UnicodeWidth.GetStringWidth("\u00E9"));
		}

		[Fact]
		public void GetStringWidth_DecomposedE_Returns1()
		{
			// e (U+0065) + combining acute (U+0301) = 1 + 0 = 1
			Assert.Equal(1, UnicodeWidth.GetStringWidth("e\u0301"));
		}

		[Fact]
		public void GetStringWidth_BothFormsInSameString_SameTotal()
		{
			// "é" precomposed + "è" decomposed = 1 + (1 + 0) = 2
			string precomposed = "\u00E9";
			string decomposed = "e\u0300";
			Assert.Equal(2, UnicodeWidth.GetStringWidth(precomposed + decomposed));
		}

		#endregion

		#region Ligatures and Special Forms

		[Fact]
		public void GetRuneWidth_LatinLigatures_Returns1()
		{
			// U+FB00 ff ligature, U+FB01 fi ligature
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0xFB00)));
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0xFB01)));
		}

		[Fact]
		public void GetRuneWidth_EnclosedAlphanumerics_Width()
		{
			// U+2460 ① — narrow
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x2460)));
			// U+2461 ②
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(0x2461)));
		}

		#endregion

		#region Boundary / Stress Tests

		[Fact]
		public void GetStringWidth_EmptyString_Returns0()
		{
			Assert.Equal(0, UnicodeWidth.GetStringWidth(""));
		}

		[Fact]
		public void GetStringWidth_SingleAsciiChar_Returns1()
		{
			Assert.Equal(1, UnicodeWidth.GetStringWidth("X"));
		}

		[Fact]
		public void GetStringWidth_OnlyZeroWidthChars_Returns0()
		{
			// ZWJ + FE0F + combining grave = 0 + 0 + 0 = 0
			Assert.Equal(0, UnicodeWidth.GetStringWidth("\u200D\uFE0F\u0300"));
		}

		[Fact]
		public void GetStringWidth_LongMixedString_CorrectWidth()
		{
			// Build a complex string with many categories
			var sb = new StringBuilder();
			sb.Append("Hello");         // 5
			sb.Append("\u4E2D\u6587");   // 4 (2 CJK)
			sb.Append("\U0001F525");     // 2 (emoji)
			sb.Append("\uFE0F");         // 0 (variation selector)
			sb.Append("e\u0301");        // 1 (e + combining)
			sb.Append("\u0928\u093E");   // 2 (Devanagari + Mc)
			sb.Append(" ");             // 1
			// Total: 5 + 4 + 2 + 0 + 1 + 2 + 1 = 15
			Assert.Equal(15, UnicodeWidth.GetStringWidth(sb.ToString()));
		}

		[Fact]
		public void GetStringWidth_ManyCombiningMarks_StillWidth1()
		{
			// A + 10 combining accents = width 1
			var sb = new StringBuilder("A");
			for (int i = 0; i < 10; i++)
				sb.Append('\u0300'); // combining grave
			Assert.Equal(1, UnicodeWidth.GetStringWidth(sb.ToString()));
		}

		#endregion
	}
}
