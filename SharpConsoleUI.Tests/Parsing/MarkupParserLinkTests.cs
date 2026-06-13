using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserLinkTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;
		private static string Str(List<Cell> cells)
			=> string.Concat(cells.FindAll(c => !c.IsWideContinuation).ConvertAll(c => c.Character.ToString()));

		[Fact]
		public void Parse_LinkTag_StripsTag_RecordsSpan()
		{
			var cells = MarkupParser.Parse("[link=https://a.com]text[/]", Fg, Bg, out var spans);
			Assert.Equal("text", Str(cells));
			var s = Assert.Single(spans);
			Assert.Equal("https://a.com", s.Url);
			Assert.Equal(0, s.StartCol);
			Assert.Equal(4, s.EndCol);
			Assert.Equal("text", s.Text);
		}

		[Fact]
		public void Parse_LinkTag_UnescapesUrl()
		{
			var cells = MarkupParser.Parse("[link=http://h/a%20b%5Dc]x[/]", Fg, Bg, out var spans);
			Assert.Equal("http://h/a b]c", Assert.Single(spans).Url);
		}

		[Fact]
		public void Parse_LinkWithInnerStyle_SpanCoversStyledText()
		{
			var cells = MarkupParser.Parse("[link=u][#66A8E0 underline]hi[/][/]", Fg, Bg, out var spans);
			Assert.Equal("hi", Str(cells));
			var s = Assert.Single(spans);
			Assert.Equal(0, s.StartCol);
			Assert.Equal(2, s.EndCol);
			Assert.Equal("u", s.Url);
		}

		[Fact]
		public void Parse_NoLink_ProducesEmptySpans_AndIdenticalCells()
		{
			var withOut = MarkupParser.Parse("[red]hi[/]", Fg, Bg, out var spans);
			var legacy = MarkupParser.Parse("[red]hi[/]", Fg, Bg);
			Assert.Empty(spans);
			Assert.Equal(legacy, withOut);
		}

		[Fact]
		public void StripLength_LinkTag_IsZeroWidth()
		{
			Assert.Equal(4, MarkupParser.StripLength("[link=https://a.com]text[/]"));
		}

		[Fact]
		public void Remove_LinkTag_ReturnsVisibleText()
		{
			Assert.Equal("text", MarkupParser.Remove("[link=https://a.com]text[/]"));
		}

		[Fact]
		public void Parse_TwoLinksOnOneLine_RecordsTwoSpans()
		{
			var cells = MarkupParser.Parse("[link=a]xx[/] [link=b]yy[/]", Fg, Bg, out var spans);
			Assert.Equal("xx yy", Str(cells));
			Assert.Equal(2, spans.Count);
			Assert.Equal("a", spans[0].Url);
			Assert.Equal(0, spans[0].StartCol);
			Assert.Equal(2, spans[0].EndCol);
			Assert.Equal("b", spans[1].Url);
			Assert.Equal(3, spans[1].StartCol);   // after "xx "
			Assert.Equal(5, spans[1].EndCol);
		}

		[Fact]
		public void Parse_WideCharInLink_SpanUsesDisplayColumns_TextHasOneRune()
		{
			// 📦 is a wide (2-column) emoji. Parse emits a base cell + a continuation cell.
			var cells = MarkupParser.Parse("[link=u]📦[/]", Fg, Bg, out var spans);
			var s = Assert.Single(spans);
			Assert.Equal("u", s.Url);
			Assert.Equal(0, s.StartCol);
			Assert.Equal(2, s.EndCol);            // two display columns for the wide char
			Assert.Equal("📦", s.Text);           // continuation cell skipped → one rune
		}

		[Fact]
		public void ParseLines_NoWrap_KeepsSingleSpan()
		{
			var lines = MarkupParser.ParseLines("[link=u]abc[/]", 80, Fg, Bg, out var perLine);
			Assert.Single(lines);
			var s = Assert.Single(perLine[0]);
			Assert.Equal("u", s.Url);
			Assert.Equal(0, s.StartCol);
			Assert.Equal(3, s.EndCol);
		}

		[Fact]
		public void ParseLines_WrapSplitsLink_SpanSplitsAcrossRows_SameUrl()
		{
			// "aaaa [link=u]bbbb cccc[/]" wrapped at width 10
			var lines = MarkupParser.ParseLines("aaaa [link=u]bbbb cccc[/]", 10, Fg, Bg, out var perLine);
			Assert.True(lines.Count >= 2);
			foreach (var rowSpans in perLine)
				foreach (var s in rowSpans)
					Assert.Equal("u", s.Url);
			int rowsWithLink = perLine.FindAll(r => r.Count > 0).Count;
			Assert.True(rowsWithLink >= 2);
		}

		[Fact]
		public void ParseLines_NoLink_AllRowsEmptySpans()
		{
			MarkupParser.ParseLines("plain text here", 5, Fg, Bg, out var perLine);
			Assert.All(perLine, r => Assert.Empty(r));
		}

		[Fact]
		public void ParseLines_OutCount_MatchesLineCount()
		{
			var lines = MarkupParser.ParseLines("aaaa [link=u]bbbb cccc[/] dddd eeee", 10, Fg, Bg, out var perLine);
			Assert.Equal(lines.Count, perLine.Count);   // invariant: one span-list per rendered row
		}

		[Fact]
		public void ParseLines_LinkSpanningThreeRows_RebasesColumnsExactly()
		{
			// A 15-char link hard-wrapped at width 5 → 3 rows of 5. The link covers every column of every row.
			var lines = MarkupParser.ParseLines("[link=u]abcdefghijklmno[/]", 5, Fg, Bg, out var perLine);
			Assert.Equal(3, lines.Count);
			Assert.Equal(lines.Count, perLine.Count);
			for (int r = 0; r < 3; r++)
			{
				var s = Assert.Single(perLine[r]);
				Assert.Equal("u", s.Url);
				Assert.Equal(0, s.StartCol);
				Assert.Equal(5, s.EndCol);   // each row fully covered by the link
			}
		}

		[Fact]
		public void ParseLines_LinkStartingMidRow_RebasesBoundaryClampedColumns()
		{
			// "ab" (no link) then an 8-char hard-wrapped link, width 5 → "abcde" / "fghij".
			// Row 0's link portion is clamped to a NON-[0,width) window; row 1 rebases to start 0.
			var lines = MarkupParser.ParseLines("ab[link=u]cdefghij[/]", 5, Fg, Bg, out var perLine);
			Assert.Equal(lines.Count, perLine.Count);
			foreach (var rowSpans in perLine)
				foreach (var s in rowSpans)
					Assert.Equal("u", s.Url);

			Assert.Equal(2, lines.Count);
			var s0 = Assert.Single(perLine[0]);
			Assert.Equal((2, 5), (s0.StartCol, s0.EndCol));   // row 0 "abcde": link starts at col 2, clamped to row end
			var s1 = Assert.Single(perLine[1]);
			Assert.Equal((0, 5), (s1.StartCol, s1.EndCol));   // row 1 "fghij": rebased — link covers whole row
		}
	}
}
