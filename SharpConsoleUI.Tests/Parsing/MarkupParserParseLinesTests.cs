using Xunit;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserParseLinesTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		private static string CellString(List<Cell> cells)
			=> new(cells.Select(c => c.Character).ToArray());

		[Fact]
		public void ParseLines_SingleLineFitsWidth_OneLine()
		{
			var result = MarkupParser.ParseLines("hello", 10, Fg, Bg);
			Assert.Single(result);
			Assert.Equal("hello", CellString(result[0]));
		}

		[Fact]
		public void ParseLines_LongTextWrapsAtLastSpace()
		{
			// "hello world" = 11 chars, fits in width=11. Space at position 11
			// is a natural break point, so first line should be "hello world".
			var result = MarkupParser.ParseLines("hello world foo", 11, Fg, Bg);
			Assert.True(result.Count >= 2);
			Assert.Equal("hello world", CellString(result[0]));
		}

		[Fact]
		public void ParseLines_NoSpace_ForceBreakAtWidth()
		{
			var result = MarkupParser.ParseLines("abcdefghij", 5, Fg, Bg);
			Assert.Equal(2, result.Count);
			Assert.Equal("abcde", CellString(result[0]));
			Assert.Equal("fghij", CellString(result[1]));
		}

		[Fact]
		public void ParseLines_ExplicitNewline_SeparateLines()
		{
			var result = MarkupParser.ParseLines("abc\ndef", 10, Fg, Bg);
			Assert.Equal(2, result.Count);
			Assert.Equal("abc", CellString(result[0]));
			Assert.Equal("def", CellString(result[1]));
		}

		[Fact]
		public void ParseLines_WidthOne_OneCharPerLine()
		{
			var result = MarkupParser.ParseLines("abc", 1, Fg, Bg);
			Assert.Equal(3, result.Count);
			Assert.All(result, line => Assert.Single(line));
		}

		[Fact]
		public void ParseLines_EmptyString_ReturnsOneEmptyLine()
		{
			var result = MarkupParser.ParseLines("", 10, Fg, Bg);
			Assert.Single(result);
			Assert.Empty(result[0]);
		}

		[Fact]
		public void ParseLines_NullString_ReturnsOneEmptyLine()
		{
			var result = MarkupParser.ParseLines(null!, 10, Fg, Bg);
			Assert.Single(result);
			Assert.Empty(result[0]);
		}

		[Fact]
		public void ParseLines_TrailingSpacesTrimmedOnWrap()
		{
			var result = MarkupParser.ParseLines("hello world", 5, Fg, Bg);
			Assert.Equal(2, result.Count);
			// Trailing spaces on first line should be trimmed
			Assert.DoesNotContain(' ', CellString(result[0]).TrimEnd());
			Assert.DoesNotContain(result[0], c => c.Character == ' ' && result[0].IndexOf(c) == result[0].Count - 1);
		}

		[Fact]
		public void ParseLines_VeryLongWord_ForceBreak()
		{
			var result = MarkupParser.ParseLines("abcdefghijklmno", 5, Fg, Bg);
			Assert.Equal(3, result.Count);
			Assert.Equal("abcde", CellString(result[0]));
			Assert.Equal("fghij", CellString(result[1]));
			Assert.Equal("klmno", CellString(result[2]));
		}

		[Fact]
		public void ParseLines_MultipleWrapsNeeded()
		{
			var result = MarkupParser.ParseLines("a b c d e f", 3, Fg, Bg);
			Assert.True(result.Count >= 3);
		}

		[Fact]
		public void ParseLines_TagsDontCountTowardWidth()
		{
			var result = MarkupParser.ParseLines("[red]hello[/]", 5, Fg, Bg);
			Assert.Single(result);
			Assert.Equal("hello", CellString(result[0]));
		}

		[Fact]
		public void ParseLines_WidthZero_ReturnsEmptyLine()
		{
			var result = MarkupParser.ParseLines("text", 0, Fg, Bg);
			Assert.Single(result);
			Assert.Empty(result[0]);
		}

		[Fact]
		public void ParseLines_StyledTextPreservedAcrossLines()
		{
			// When text wraps, Parse is called per explicit line, so style from
			// one line won't carry to the next explicit line. But within a single
			// long line, cells retain their style after wrapping.
			var result = MarkupParser.ParseLines("[red]abcdef[/]", 3, Fg, Bg);
			Assert.Equal(2, result.Count);
			Assert.All(result[0], c => Assert.Equal(Color.Red, c.Foreground));
			Assert.All(result[1], c => Assert.Equal(Color.Red, c.Foreground));
		}
	}
}
