using Xunit;
using System.Text;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Tests.Helpers
{
	public class UnicodeWidthTests
	{
		#region GetCharWidth Tests

		[Theory]
		[InlineData('A', 1)]
		[InlineData('z', 1)]
		[InlineData('!', 1)]
		[InlineData(' ', 1)]
		public void GetCharWidth_AsciiChar_Returns1(char c, int expected)
		{
			Assert.Equal(expected, UnicodeWidth.GetCharWidth(c));
		}

		[Fact]
		public void GetCharWidth_CjkIdeograph_Returns2()
		{
			// U+4E2D 中, U+65E5 日, U+672C 本
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x4E2D));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x65E5));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x672C));
		}

		[Fact]
		public void GetCharWidth_Hiragana_Returns2()
		{
			// U+3042 あ, U+3044 い, U+3046 う
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x3042));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x3044));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x3046));
		}

		[Fact]
		public void GetCharWidth_Katakana_Returns2()
		{
			// U+30A2 ア, U+30A4 イ, U+30A6 ウ
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x30A2));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x30A4));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0x30A6));
		}

		[Fact]
		public void GetCharWidth_HangulSyllable_Returns2()
		{
			// U+D55C 한, U+AE00 글
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0xD55C));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0xAE00));
		}

		[Fact]
		public void GetCharWidth_FullwidthLatin_Returns2()
		{
			// U+FF21 Ａ, U+FF22 Ｂ
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0xFF21));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0xFF22));
		}

		[Fact]
		public void GetCharWidth_FullwidthSymbol_Returns2()
		{
			// U+FF04 ＄, U+FF05 ％
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0xFF04));
			Assert.Equal(2, UnicodeWidth.GetCharWidth((char)0xFF05));
		}

		[Fact]
		public void GetCharWidth_HalfwidthKatakana_Returns1()
		{
			// U+FF71 ｱ — halfwidth forms are NOT wide
			Assert.Equal(1, UnicodeWidth.GetCharWidth((char)0xFF71));
		}

		#endregion

		#region IsWide Tests

		[Fact]
		public void IsWide_RangeBoundaries()
		{
			// CJK Unified Ideographs: U+4E00 - U+9FFF
			Assert.True(UnicodeWidth.IsWide((char)0x4E00));
			Assert.True(UnicodeWidth.IsWide((char)0x9FFF));

			// Hangul Syllables: U+AC00 - U+D7AF
			Assert.True(UnicodeWidth.IsWide((char)0xAC00));
			Assert.True(UnicodeWidth.IsWide((char)0xD7AF));

			// Fullwidth Forms: U+FF01 - U+FF60
			Assert.True(UnicodeWidth.IsWide((char)0xFF01));
			Assert.True(UnicodeWidth.IsWide((char)0xFF60));

			// CJK Compatibility Ideographs: U+F900 - U+FAFF
			Assert.True(UnicodeWidth.IsWide((char)0xF900));
			Assert.True(UnicodeWidth.IsWide((char)0xFAFF));
		}

		[Theory]
		[InlineData('A')]
		[InlineData('z')]
		[InlineData(' ')]
		[InlineData('~')]
		public void IsWide_BelowRange_ReturnsFalse_Ascii(char c)
		{
			Assert.False(UnicodeWidth.IsWide(c));
		}

		[Fact]
		public void IsWide_BelowRange_ReturnsFalse_Latin1()
		{
			// Latin-1 Supplement: U+00C0 (À), U+00FF (ÿ)
			Assert.False(UnicodeWidth.IsWide((char)0x00C0));
			Assert.False(UnicodeWidth.IsWide((char)0x00FF));
		}

		[Fact]
		public void IsWide_HangulJamo_Leading()
		{
			// Hangul Jamo leading consonants: U+1100 - U+115F
			Assert.True(UnicodeWidth.IsWide((char)0x1100));
			Assert.True(UnicodeWidth.IsWide((char)0x115F));
		}

		[Fact]
		public void IsWide_AngleBrackets()
		{
			// Left/Right-Pointing Angle Bracket: U+2329, U+232A
			Assert.True(UnicodeWidth.IsWide((char)0x2329));
			Assert.True(UnicodeWidth.IsWide((char)0x232A));
		}

		[Fact]
		public void IsWide_CjkRadicals()
		{
			// CJK Radicals Supplement starts at U+2E80
			Assert.True(UnicodeWidth.IsWide((char)0x2E80));
		}

		[Fact]
		public void IsWide_CjkSymbolsAndPunctuation()
		{
			// CJK Symbols and Punctuation starts at U+3000
			Assert.True(UnicodeWidth.IsWide((char)0x3000));
		}

		#endregion

		#region GetStringWidth Tests

		[Fact]
		public void GetStringWidth_EmptyString_Returns0()
		{
			Assert.Equal(0, UnicodeWidth.GetStringWidth(string.Empty));
		}

		[Fact]
		public void GetStringWidth_Null_Returns0()
		{
			Assert.Equal(0, UnicodeWidth.GetStringWidth(null!));
		}

		[Fact]
		public void GetStringWidth_AsciiOnly_ReturnsLength()
		{
			Assert.Equal(5, UnicodeWidth.GetStringWidth("Hello"));
		}

		[Fact]
		public void GetStringWidth_CjkOnly_ReturnsDoubleLength()
		{
			// "中文" = 2 chars, each width 2 = 4
			string cjk = new string(new[] { (char)0x4E2D, (char)0x6587 });
			Assert.Equal(4, UnicodeWidth.GetStringWidth(cjk));
		}

		[Fact]
		public void GetStringWidth_Mixed_ReturnsCorrectWidth()
		{
			// "Hello世界" = 5 ASCII (width 1 each) + 2 CJK (width 2 each) = 9
			string mixed = "Hello" + new string(new[] { (char)0x4E16, (char)0x754C });
			Assert.Equal(9, UnicodeWidth.GetStringWidth(mixed));
		}

		[Fact]
		public void GetStringWidth_WithSpaces()
		{
			// "A 中 B" = A(1) + space(1) + 中(2) + space(1) + B(1) = 6
			string s = "A " + (char)0x4E2D + " B";
			Assert.Equal(6, UnicodeWidth.GetStringWidth(s));
		}

		[Fact]
		public void GetStringWidth_FullwidthLatin()
		{
			// "ＡＢ" = 2 fullwidth chars, each width 2 = 4
			string fw = new string(new[] { (char)0xFF21, (char)0xFF22 });
			Assert.Equal(4, UnicodeWidth.GetStringWidth(fw));
		}

		[Fact]
		public void GetStringWidth_WithEmoji_CountsAsWide()
		{
			// "Hello💩" = 5 ASCII (width 1 each) + 1 emoji (width 2) = 7
			Assert.Equal(7, UnicodeWidth.GetStringWidth("Hello\U0001F4A9"));
		}

		[Fact]
		public void GetStringWidth_EmojiOnly_CorrectWidth()
		{
			// "🔥🎉" = 2 emoji × width 2 = 4
			Assert.Equal(4, UnicodeWidth.GetStringWidth("\U0001F525\U0001F389"));
		}

		#endregion

		#region GetRuneWidth Tests

		[Fact]
		public void GetRuneWidth_AsciiRune_Returns1()
		{
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune('A')));
			Assert.Equal(1, UnicodeWidth.GetRuneWidth(new Rune(' ')));
		}

		[Fact]
		public void GetRuneWidth_CjkRune_Returns2()
		{
			Assert.Equal(2, UnicodeWidth.GetRuneWidth(new Rune(0x4E2D))); // 中
		}

		[Fact]
		public void GetRuneWidth_EmojiRune_Returns2()
		{
			Assert.Equal(2, UnicodeWidth.GetRuneWidth(new Rune(0x1F4A9))); // 💩
			Assert.Equal(2, UnicodeWidth.GetRuneWidth(new Rune(0x1F525))); // 🔥
			Assert.Equal(2, UnicodeWidth.GetRuneWidth(new Rune(0x1F600))); // 😀
		}

		[Fact]
		public void GetRuneWidth_SupplementaryCjk_Returns2()
		{
			// CJK Extension B: U+20000
			Assert.Equal(2, UnicodeWidth.GetRuneWidth(new Rune(0x20000)));
		}

		#endregion

		#region IsWideRune Tests

		[Fact]
		public void IsWideRune_EmojiBlocks_ReturnsTrue()
		{
			// Emoticons block: U+1F600
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F600)));
			// Misc Symbols: U+1F300
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F300)));
			// Transport: U+1F680
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F680)));
		}

		[Fact]
		public void IsWideRune_BmpNarrow_ReturnsFalse()
		{
			Assert.False(UnicodeWidth.IsWideRune(new Rune('A')));
			Assert.False(UnicodeWidth.IsWideRune(new Rune(' ')));
		}

		[Fact]
		public void IsWideRune_BmpWide_ReturnsTrue()
		{
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x4E2D))); // 中
		}

		[Fact]
		public void IsWideRune_CjkExtensionB_ReturnsTrue()
		{
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x20000)));
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x2A6DF)));
		}

		[Fact]
		public void IsWideRune_CjkCompatSupplement_ReturnsTrue()
		{
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x2F800)));
		}

		[Fact]
		public void IsWideRune_NeutralWidthEmoji_ReturnsFalse()
		{
			// These emoji have East_Asian_Width = "N" (Neutral) in Unicode 15.0
			// Terminals render them as 1-wide
			Assert.False(UnicodeWidth.IsWideRune(new Rune(0x1F336)));  // 🌶 Hot Pepper
			Assert.False(UnicodeWidth.IsWideRune(new Rune(0x1F37D)));  // 🍽 Fork and Knife with Plate
			Assert.False(UnicodeWidth.IsWideRune(new Rune(0x1F43F)));  // 🐿 Chipmunk
			Assert.False(UnicodeWidth.IsWideRune(new Rune(0x1F441)));  // 🗡 (Eye)
			Assert.False(UnicodeWidth.IsWideRune(new Rune(0x1F54F)));  // EAW=N gap
		}

		[Fact]
		public void IsWideRune_WideEmojiBoundaries_ReturnsTrue()
		{
			// Verify boundaries of wide emoji ranges are correctly classified
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F004)));   // 🀄 Mahjong
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F0CF)));   // 🃏 Joker
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F300)));   // Start of Misc Symbols
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F335)));   // End before 🌶 gap
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F337)));   // Start after 🌶 gap
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F37C)));   // End before 🍽 gap
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F37E)));   // Start after 🍽 gap
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F4A9)));   // 💩 Pile of Poo
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F64F)));   // 🙏 Folding Hands
		}

		[Fact]
		public void IsWideRune_IsolatedWideEmoji_ReturnsTrue()
		{
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F18E)));   // 🆎
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F3F4)));   // 🏴
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F440)));   // 👀
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F57A)));   // 🕺
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F5A4)));   // 🖤
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F6CC)));   // 🛌
			Assert.True(UnicodeWidth.IsWideRune(new Rune(0x1F7F0)));   // 🟰
		}

		#endregion
	}
}
