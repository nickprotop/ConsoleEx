using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using System.Text;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserWideCharTests
	{
		#region StripLength with wide chars

		[Fact]
		public void StripLength_CjkText_ReturnsDisplayWidth()
		{
			Assert.Equal(4, MarkupParser.StripLength("\u4e2d\u6587"));
		}

		[Fact]
		public void StripLength_MixedMarkupAndCjk_ReturnsDisplayWidth()
		{
			Assert.Equal(8, MarkupParser.StripLength("[bold]\u4e2d\u6587[/]test"));
		}

		[Fact]
		public void StripLength_CjkWithColors_ReturnsDisplayWidth()
		{
			Assert.Equal(4, MarkupParser.StripLength("[red]\u4e2d[/][blue]\u6587[/]"));
		}

		[Fact]
		public void StripLength_NestedMarkupWithCjk_Correct()
		{
			// A(1) + \u4e2d(2) + B(1) + \u6587(2) = 6
			Assert.Equal(6, MarkupParser.StripLength("[bold red]A\u4e2dB\u6587[/]"));
		}

		[Fact]
		public void StripLength_FullwidthPunctuation_Returns2()
		{
			Assert.Equal(2, MarkupParser.StripLength("\uff01"));
		}

		[Fact]
		public void StripLength_AsciiOnly_ReturnsLength()
		{
			Assert.Equal(5, MarkupParser.StripLength("Hello"));
		}

		[Fact]
		public void StripLength_EmptyString_Returns0()
		{
			Assert.Equal(0, MarkupParser.StripLength(""));
		}

		#endregion

		#region Parse with wide chars

		[Fact]
		public void Parse_CjkChar_ProducesTwoCells()
		{
			var cells = MarkupParser.Parse("\u4e2d", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('\u4e2d'), cells[0].Character);
			Assert.False(cells[0].IsWideContinuation);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_MixedText_CorrectCellCount()
		{
			// "A\u4e2dB" = A(1 cell) + \u4e2d(2 cells) + B(1 cell) = 4 cells
			var cells = MarkupParser.Parse("A\u4e2dB", Color.White, Color.Black);

			Assert.Equal(4, cells.Count);
			Assert.Equal(new Rune('A'), cells[0].Character);
			Assert.False(cells[0].IsWideContinuation);
			Assert.Equal(new Rune('\u4e2d'), cells[1].Character);
			Assert.False(cells[1].IsWideContinuation);
			Assert.True(cells[2].IsWideContinuation);
			Assert.Equal(new Rune('B'), cells[3].Character);
			Assert.False(cells[3].IsWideContinuation);
		}

		[Fact]
		public void Parse_CjkWithMarkup_ContinuationInheritsStyle()
		{
			var cells = MarkupParser.Parse("[red]\u4e2d[/]", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(Color.Red, cells[0].Foreground);
			Assert.Equal(Color.Red, cells[1].Foreground);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_MultipleCjk_AllHaveContinuations()
		{
			var cells = MarkupParser.Parse("\u4e2d\u6587", Color.White, Color.Black);

			Assert.Equal(4, cells.Count);
			Assert.Equal(new Rune('\u4e2d'), cells[0].Character);
			Assert.False(cells[0].IsWideContinuation);
			Assert.True(cells[1].IsWideContinuation);
			Assert.Equal(new Rune('\u6587'), cells[2].Character);
			Assert.False(cells[2].IsWideContinuation);
			Assert.True(cells[3].IsWideContinuation);
		}

		[Fact]
		public void Parse_ContinuationCellHasCorrectProperties()
		{
			var cells = MarkupParser.Parse("\u4e2d", Color.White, Color.Black);

			var continuation = cells[1];
			Assert.True(continuation.IsWideContinuation);
			Assert.Equal(new Rune(' '), continuation.Character);
		}

		[Fact]
		public void Parse_AsciiNoContinuation()
		{
			var cells = MarkupParser.Parse("AB", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.False(cells[0].IsWideContinuation);
			Assert.False(cells[1].IsWideContinuation);
		}

		#endregion

		#region Truncate with wide chars

		[Fact]
		public void Truncate_CjkTruncatedAtWideBoundary_NoHalfChar()
		{
			// "ABCD\u4e2d\u6587" = A(1)+B(1)+C(1)+D(1)+\u4e2d(2)+\u6587(2) = 8 columns
			// Truncate to width 5: ABCD=4, adding \u4e2d would be 6 > 5, so result is "ABCD"
			var result = MarkupParser.Truncate("ABCD\u4e2d\u6587", 5);
			Assert.Equal("ABCD", result);
		}

		[Fact]
		public void Truncate_ExactWidthForWideChar_Included()
		{
			// "ABCD\u4e2d" = A(1)+B(1)+C(1)+D(1)+\u4e2d(2) = 6 columns
			var result = MarkupParser.Truncate("ABCD\u4e2d", 6);
			Assert.Equal("ABCD\u4e2d", result);
		}

		[Fact]
		public void Truncate_AllCjk_TruncatesCorrectly()
		{
			// "\u4e2d\u6587\u5b57" = 6 columns, truncate to 4 = "\u4e2d\u6587"
			var result = MarkupParser.Truncate("\u4e2d\u6587\u5b57", 4);
			Assert.Equal("\u4e2d\u6587", result);
		}

		[Fact]
		public void Truncate_MixedText_CorrectTruncation()
		{
			// "AB\u4e2dCD" = A(1)+B(1)+\u4e2d(2) = 4 at "AB\u4e2d"
			var result = MarkupParser.Truncate("AB\u4e2dCD", 4);
			Assert.Equal("AB\u4e2d", result);
		}

		[Fact]
		public void Truncate_WidthZero_ReturnsEmpty()
		{
			var result = MarkupParser.Truncate("\u4e2d\u6587", 0);
			Assert.Equal(string.Empty, result);
		}

		#endregion

		#region ParseLines with wide chars

		[Fact]
		public void ParseLines_CjkWrapsAtDisplayWidth()
		{
			// "AAAA\u4e2d\u6587" = A(1)+A(1)+A(1)+A(1)+\u4e2d(2)+\u6587(2) = 8 columns
			// Width=6: first line fits "AAAA\u4e2d" (4+2=6 columns, 6 cells including continuation)
			var lines = MarkupParser.ParseLines("AAAA\u4e2d\u6587", 6, Color.White, Color.Black);

			Assert.True(lines.Count >= 1);
			Assert.Equal(6, lines[0].Count);
			Assert.Equal(new Rune('A'), lines[0][0].Character);
			Assert.Equal(new Rune('A'), lines[0][3].Character);
			Assert.Equal(new Rune('\u4e2d'), lines[0][4].Character);
			Assert.True(lines[0][5].IsWideContinuation);
		}

		[Fact]
		public void ParseLines_WideCharDoesntFitOnLine_MovedToNextLine()
		{
			// Width=5, text="AAAA\u4e2d" = A(1)+A(1)+A(1)+A(1)+\u4e2d(2) = 6 columns
			// At width 5: AAAA=4 cells, 1 column left, \u4e2d needs 2 -> doesn't fit
			// \u4e2d (2 cells) goes to next line
			var lines = MarkupParser.ParseLines("AAAA\u4e2d", 5, Color.White, Color.Black);

			Assert.Equal(2, lines.Count);
			Assert.Equal(4, lines[0].Count);
			Assert.Equal(2, lines[1].Count);
			Assert.Equal(new Rune('\u4e2d'), lines[1][0].Character);
			Assert.True(lines[1][1].IsWideContinuation);
		}

		#endregion

		#region Emoji (Surrogate Pair) Tests

		[Fact]
		public void StripLength_EmojiInMarkup_ReturnsCorrectWidth()
		{
			// "Hello " (6) + 🔥 (2) = 8
			Assert.Equal(8, MarkupParser.StripLength("[red]Hello \U0001F525[/]"));
		}

		[Fact]
		public void StripLength_MultipleEmoji_ReturnsCorrectWidth()
		{
			// 🔥(2) + 🎉(2) + 💩(2) = 6
			Assert.Equal(6, MarkupParser.StripLength("\U0001F525\U0001F389\U0001F4A9"));
		}

		[Fact]
		public void StripLength_MixedEmojiAndCjk_ReturnsCorrectWidth()
		{
			// 中(2) + 🔥(2) + A(1) = 5
			Assert.Equal(5, MarkupParser.StripLength("\u4e2d\U0001F525A"));
		}

		[Fact]
		public void Parse_Emoji_ProducesTwoCells()
		{
			var cells = MarkupParser.Parse("\U0001F4A9", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune(0x1F4A9), cells[0].Character);
			Assert.False(cells[0].IsWideContinuation);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_EmojiInMarkup_CorrectCells()
		{
			// "[bold]A🔥B[/]" = A(1) + 🔥(2) + B(1) = 4 cells
			var cells = MarkupParser.Parse("[bold]A\U0001F525B[/]", Color.White, Color.Black);

			Assert.Equal(4, cells.Count);
			Assert.Equal(new Rune('A'), cells[0].Character);
			Assert.Equal(new Rune(0x1F525), cells[1].Character);
			Assert.True(cells[2].IsWideContinuation);
			Assert.Equal(new Rune('B'), cells[3].Character);
		}

		[Fact]
		public void Parse_EmojiWithColor_ContinuationInheritsStyle()
		{
			var cells = MarkupParser.Parse("[red]\U0001F600[/]", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(Color.Red, cells[0].Foreground);
			Assert.Equal(Color.Red, cells[1].Foreground);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_MixedEmojiCjkAscii_CorrectLayout()
		{
			// "A中🔥B" = A(1) + 中(2) + 🔥(2) + B(1) = 6 cells
			var cells = MarkupParser.Parse("A\u4e2d\U0001F525B", Color.White, Color.Black);

			Assert.Equal(6, cells.Count);
			Assert.Equal(new Rune('A'), cells[0].Character);
			Assert.Equal(new Rune('\u4e2d'), cells[1].Character);
			Assert.True(cells[2].IsWideContinuation);
			Assert.Equal(new Rune(0x1F525), cells[3].Character);
			Assert.True(cells[4].IsWideContinuation);
			Assert.Equal(new Rune('B'), cells[5].Character);
		}

		[Fact]
		public void Truncate_EmojiAtBoundary_ExcludedIfNoRoom()
		{
			// "AB🔥CD" = A(1)+B(1)+🔥(2)+C(1)+D(1) = 6 columns
			// Truncate to 3: AB=2, 🔥 would be 4 > 3, so "AB"
			var result = MarkupParser.Truncate("AB\U0001F525CD", 3);
			Assert.Equal("AB", result);
		}

		[Fact]
		public void Truncate_EmojiExactFit_Included()
		{
			// "AB🔥" = A(1)+B(1)+🔥(2) = 4 columns
			var result = MarkupParser.Truncate("AB\U0001F525", 4);
			Assert.Equal("AB\U0001F525", result);
		}

		[Fact]
		public void Truncate_MultipleEmoji_TruncatesCorrectly()
		{
			// "🔥🎉💩" = 6 columns, truncate to 4 = "🔥🎉"
			var result = MarkupParser.Truncate("\U0001F525\U0001F389\U0001F4A9", 4);
			Assert.Equal("\U0001F525\U0001F389", result);
		}

		#endregion
	}
}
