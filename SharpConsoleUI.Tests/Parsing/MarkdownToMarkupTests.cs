using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;
using MarkdownStyle = SharpConsoleUI.Configuration.MarkdownStyle;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkdownToMarkupTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		private static string CellString(List<Cell> cells)
			=> string.Concat(cells.Select(c => c.Character.ToString()));

		private static List<Cell> Render(string md)
			=> MarkupParser.Parse(MarkdownToMarkup.Convert(md), Fg, Bg);

		[Fact]
		public void Bold_EmitsBoldTag_VisibleTextOnly()
		{
			var cells = Render("**bold**");
			Assert.Equal("bold", CellString(cells).Trim());
			Assert.All(cells.Where(c => c.Character.ToString() != " "),
				c => Assert.True((c.Decorations & TextDecoration.Bold) != 0));
		}

		[Fact]
		public void Italic_EmitsItalicTag()
		{
			var cells = Render("*italic*");
			Assert.Equal("italic", CellString(cells).Trim());
			Assert.Contains(cells, c => (c.Decorations & TextDecoration.Italic) != 0);
		}

		[Fact]
		public void Strikethrough_EmitsStrikeTag()
		{
			var cells = Render("~~gone~~");
			Assert.Equal("gone", CellString(cells).Trim());
			Assert.Contains(cells, c => (c.Decorations & TextDecoration.Strikethrough) != 0);
		}

		[Fact]
		public void InlineCode_UsesStyleCodeColors()
		{
			var cells = Render("`x`");
			var codeCell = cells.First(c => c.Character.ToString() == "x");
			Assert.Equal(MarkdownStyle.Default.CodeBackground, codeCell.Background);
			Assert.Equal(MarkdownStyle.Default.CodeForeground, codeCell.Foreground);
		}

		[Fact]
		public void Link_DropsUrl_KeepsStyledText()
		{
			var cells = Render("[click](https://example.com)");
			Assert.Equal("click", CellString(cells).Trim());
			Assert.DoesNotContain("http", CellString(cells));
			Assert.Contains(cells, c => c.Character.ToString() == "k"
				&& c.Foreground == MarkdownStyle.Default.LinkColor);
		}

		[Fact]
		public void LiteralBrackets_StayLiteral_NotTags()
		{
			var cells = Render("see [x] here");
			Assert.Contains("[x]", CellString(cells));
		}

		[Fact]
		public void Convert_NullOrEmpty_ReturnsEmpty()
		{
			Assert.Equal("", MarkdownToMarkup.Convert(""));
			Assert.Equal("", MarkdownToMarkup.Convert(null!));
		}

		[Theory]
		[InlineData("# H1")]
		[InlineData("## H2")]
		[InlineData("###### H6")]
		public void Heading_VisibleText_StripsHashMarker(string md)
		{
			var cells = Render(md);
			var text = CellString(cells).Trim();
			Assert.False(text.Contains('#'));
		}

		[Theory]
		[InlineData("#### H4")]
		[InlineData("##### H5")]
		[InlineData("###### H6")]
		public void DeepHeadings_StayColorless_InheritScopeFg(string md)
		{
			// H4–H6 have no default color: text keeps the inherited scope fg (white = Fg).
			var cells = Render(md);
			Assert.All(cells.Where(c => c.Character.ToString().Trim().Length > 0),
				c => Assert.Equal(Fg, c.Foreground));
		}

		[Theory]
		[InlineData("# H1")]
		[InlineData("## H2")]
		[InlineData("### H3")]
		public void TopHeadings_UseDefaultHeadingColor(string md)
		{
			// H1–H3 carry the cohesive blue-family default color (distinct from the inherited fg).
			var cells = Render(md);
			Assert.Contains(cells.Where(c => c.Character.ToString().Trim().Length > 0),
				c => c.Foreground != Fg);
		}

		[Fact]
		public void Heading_IsBold()
		{
			var cells = Render("# Title");
			Assert.Contains(cells, c => (c.Decorations & TextDecoration.Bold) != 0);
		}

		[Fact]
		public void ThematicBreak_EmitsHorizontalRuleChars()
		{
			var cells = Render("---");
			Assert.Contains("─", CellString(cells)); // U+2500 box drawings light horizontal
		}

		[Fact]
		public void BulletList_UsesBulletGlyph()
		{
			var text = CellString(Render("- one\n- two"));
			Assert.Contains("• one", text); // • = U+2022
			Assert.Contains("• two", text);
		}

		[Fact]
		public void NumberedList_UsesNumbers()
		{
			var text = CellString(Render("1. first\n2. second"));
			Assert.Contains("1. first", text);
			Assert.Contains("2. second", text);
		}

		[Fact]
		public void NestedList_Indents()
		{
			var text = CellString(Render("- outer\n  - inner"));
			Assert.Contains("  • inner", text); // inner indented by ListIndent(2)
		}

		[Fact]
		public void Blockquote_UsesQuoteGlyphAndColor()
		{
			var cells = Render("> quoted");
			Assert.Contains("│", CellString(cells)); // │ = U+2502
			Assert.Contains(cells, c => c.Character.ToString() == "q"
				&& c.Foreground == MarkdownStyle.Default.QuoteColor);
		}

		[Fact]
		public void FencedCodeBlock_ShadedLines_PreservesText()
		{
			var cells = Render("```\nint x = 1;\n```");
			var text = CellString(cells);
			Assert.Contains("int x = 1;", text);
			Assert.Contains(cells, c => c.Character.ToString() == "i"
				&& c.Background == MarkdownStyle.Default.CodeBackground);
		}

		[Fact]
		public void Table_RendersBoxDrawingAndCells()
		{
			var md = "| A | B |\n|---|---|\n| 1 | 2 |";
			var text = CellString(Render(md));
			Assert.Contains("┌", text); // U+250C
			Assert.Contains("│", text); // U+2502
			Assert.Contains("A", text);
			Assert.Contains("1", text);
		}

		[Fact]
		public void Table_WideGlyph_ColumnAligns()
		{
			var md = "| X |\n|---|\n| \U0001F4E6 |";
			var lines = CellString(Render(md)).Split('\n');
			Assert.Contains("└", string.Concat(lines)); // table closed
			string headerLine = lines.First(l => l.Contains("X"));
			string dataLine = lines.First(l => l.Contains("\U0001F4E6"));
			// Both content rows occupy the same column width (wide glyph measured as 2).
			Assert.Equal(MarkupParser.StripLength(headerLine), MarkupParser.StripLength(dataLine));
		}

		[Fact]
		public void Blockquote_WithNestedList_DoesNotDropContent()
		{
			var text = CellString(Render("> intro\n>\n> - item"));
			Assert.Contains("intro", text);
			Assert.Contains("item", text); // nested list content must survive
		}

		[Fact]
		public void Blockquote_MultiLine_EachLineGetsQuoteBar()
		{
			var native = MarkdownToMarkup.Convert("> line one\n> line two");
			// Two quote bars (one per visual line), and no newline trapped inside a color tag.
			int barCount = native.Split('│').Length - 1; // U+2502
			Assert.Equal(2, barCount);
			// The rendered cells should contain the bar glyph twice and both line texts.
			var text = CellString(MarkupParser.Parse(native, Fg, Bg));
			Assert.Contains("line one", text);
			Assert.Contains("line two", text);
			Assert.Equal(2, text.Split('│').Length - 1);
		}

		[Fact]
		public void Blockquote_MultiLine_SecondLineIsQuoteColored()
		{
			var cells = Render("> line one\n> line two");
			// a cell from the SECOND line ('t' in 'two') must carry the quote color, proving
			// the second line is inside its own quote span (not left default).
			var twoCell = cells.FirstOrDefault(c => c.Character.ToString() == "w");
			Assert.Contains(cells, c => c.Foreground == MarkdownStyle.Default.QuoteColor
				&& c.Character.ToString().Trim().Length > 0);
		}

		[Theory]
		[InlineData("a<br>b")]
		[InlineData("a<br/>b")]
		[InlineData("a<br />b")]
		public void HtmlBr_BecomesNewline(string md)
		{
			// changlv #59: <br> / <br/> / <br /> is a hard line break, not dropped text. Markdig parses it
			// as an HtmlInline (not a LineBreakInline), so it must be translated to '\n' explicitly.
			string markup = MarkdownToMarkup.Convert(md);
			Assert.Contains("\n", markup);
			Assert.Equal("a\nb", markup);
		}

		[Fact]
		public void OtherHtmlInline_StillDropped()
		{
			// Only <br> is line-break HTML; other inline HTML tags remain stripped (unchanged behavior).
			string markup = MarkdownToMarkup.Convert("a<span>x</span>b");
			Assert.DoesNotContain("<span", markup);
			Assert.Contains("a", markup);
			Assert.Contains("b", markup);
		}

		[Fact]
		public void TableRowSeparators_Off_ByDefault_HeaderRuleOnly()
		{
			// Default: a rule under the header, none between body rows (standard markdown). Exactly one
			// mid-rule (├) plus the top (┌) and bottom (└).
			string md = "| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n| 5 | 6 |";
			string markup = MarkdownToMarkup.Convert(md);
			Assert.Equal(1, markup.Count(ch => ch == '├')); // header separator only
			Assert.Equal(1, markup.Count(ch => ch == '┌'));
			Assert.Equal(1, markup.Count(ch => ch == '└'));
		}

		[Fact]
		public void TableRowSeparators_On_DrawsRuleBetweenEveryBodyRow()
		{
			// Opt-in: a rule under the header AND between each pair of body rows (not after the last).
			// 3 body rows -> header rule + 2 inter-row rules = 3 total '├'.
			string md = "| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n| 5 | 6 |";
			var style = MarkdownStyle.Default with { TableRowSeparators = true };
			string markup = MarkdownToMarkup.Convert(md, style);
			Assert.Equal(3, markup.Count(ch => ch == '├')); // header + 2 between the 3 body rows
			Assert.Equal(1, markup.Count(ch => ch == '┌')); // still one top
			Assert.Equal(1, markup.Count(ch => ch == '└')); // still one bottom
		}

		[Fact]
		public void HtmlBr_InsideTableCell_RendersAsTwoLines()
		{
			// changlv #59: <br> inside a table cell must split the cell across two rendered lines
			// (previously "line1<br>line2" collapsed to "line1line2"). The multi-line cell renderer
			// already emits one terminal line per '\n' in the cell content.
			string md = "| Name | Note |\n|---|---|\n| Alice | line1<br>line2 |";
			var rows = MarkupParser.ParseLines($"[markdown]{md}", 40, Fg, Bg, out _);
			var visible = rows
				.Select(r => string.Concat(r.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString())))
				.ToList();
			// The concatenated bug string must NOT appear; the two fragments appear on separate rows.
			Assert.DoesNotContain(visible, l => l.Contains("line1line2"));
			Assert.Contains(visible, l => l.Contains("line1") && !l.Contains("line2"));
			Assert.Contains(visible, l => l.Contains("line2") && !l.Contains("line1"));
		}
	}
}
