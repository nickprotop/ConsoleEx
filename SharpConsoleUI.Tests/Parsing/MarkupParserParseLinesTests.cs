using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserParseLinesTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		private static string CellString(List<Cell> cells)
			=> string.Concat(cells.Select(c => c.Character.ToString()));

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
			Assert.DoesNotContain(result[0], c => c.Character == new Rune(' ') && result[0].IndexOf(c) == result[0].Count - 1);
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
		public void ParseLines_CjkRun_BreaksBetweenCharactersToFillWidth()
		{
			// A short CJK token, a space, then a long spaceless CJK run. Old behavior broke at the
			// early space (col 6), wasting the rest of the line because the run had no spaces.
			// CJK characters are valid break points, so the first line should fill the width (#63).
			string text = "中中中 中中中中中中中中";
			var result = MarkupParser.ParseLines(text, 12, Fg, Bg);

			Assert.True(result[0].Count >= 10,
				$"Expected first line to fill ~12 columns via CJK breaking, but got {result[0].Count}.");
		}

		[Fact]
		public void ParseLines_MixedCjkAndLatin_KeepsLatinWordsIntact()
		{
			// CJK breaks per-character, but Latin runs (no internal break points) stay whole:
			// "Git" must never be split across rows even though it sits inside a CJK run (#63).
			string text = "中中中中中中Git中中中中中中";
			var result = MarkupParser.ParseLines(text, 8, Fg, Bg);

			string joined = string.Concat(result.Select(CellString));
			foreach (var line in result)
			{
				string s = CellString(line);
				// "Git" is intact iff it never straddles a row boundary: each row contains either
				// all of "Git" or none of it.
				bool hasPartialGit = (s.Contains('G') || s.Contains('i') || s.Contains('t'))
					&& !s.Contains("Git");
				Assert.False(hasPartialGit, $"Latin word 'Git' was split across rows. Row: '{s.TrimEnd()}'");
			}
			Assert.Contains("Git", joined);
		}

		[Fact]
		public void ParseLines_CjkRun_DoesNotSplitWideCharPair()
		{
			// Every wrapped row must start on a wide-char lead cell, never on a continuation cell.
			string text = string.Concat(Enumerable.Repeat("中", 20)); // 20 CJK = 40 columns
			var result = MarkupParser.ParseLines(text, 7, Fg, Bg);

			Assert.All(result, line =>
			{
				if (line.Count > 0)
					Assert.False(line[0].IsWideContinuation, "A wrapped row must not start mid wide-char pair.");
			});
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
