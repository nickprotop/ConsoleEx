using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkdownMarkupTagTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;
		private static string CellString(List<Cell> cells)
			=> string.Concat(cells.Select(c => c.Character.ToString()));
		private static int CountOccurrences(string haystack, string needle)
		{
			int count = 0, idx = 0;
			while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
			{
				count++;
				idx += needle.Length;
			}
			return count;
		}

		[Fact]
		public void MarkdownTag_RendersSameAsDirectConvert()
		{
			var viaTag = MarkupParser.Parse("[markdown]# Hi[/]", Fg, Bg);
			var direct = MarkupParser.Parse(MarkdownToMarkup.Convert("# Hi"), Fg, Bg);
			Assert.Equal(CellString(direct), CellString(viaTag));
		}

		[Fact]
		public void MultiLineMarkdownRegion_ParsesViaParseLines()
		{
			var lines = MarkupParser.ParseLines("[markdown]# T\n- a\n- b[/]", 40, Fg, Bg);
			var all = string.Concat(lines.SelectMany(l => l.Select(c => c.Character.ToString())));
			Assert.Contains("T", all);
			Assert.Contains("• a", all);
			Assert.Contains("• b", all);
			Assert.DoesNotContain("#", all);
		}

		[Fact]
		public void NoMarkdownTag_PlainMarkupUnaffected()
		{
			var s = "[red]hello[/] [bold]world[/]";
			Assert.Equal("hello world", CellString(MarkupParser.Parse(s, Fg, Bg)));
		}

		[Fact]
		public void UnclosedMarkdownTag_RendersToEndOfString()
		{
			// No [/]: everything after [markdown] is rendered as Markdown to end-of-string.
			string text = "";
			var ex = Record.Exception(() =>
			{
				text = CellString(MarkupParser.Parse("[markdown]# Hi\n**bold**", Fg, Bg));
			});
			Assert.Null(ex);                          // still never throws
			Assert.DoesNotContain("[markdown]", text); // opening tag not leaked
			Assert.DoesNotContain("#", text);          // heading processed
			Assert.DoesNotContain("**", text);         // bold processed
			Assert.Contains("Hi", text);
			Assert.Contains("bold", text);
		}

		[Fact]
		public void CopyAsPlainText_NoMarkdownOrMarkupSyntax()
		{
			var text = CellString(MarkupParser.Parse("[markdown]# Title\n**bold** `code`[/]", Fg, Bg));
			Assert.DoesNotContain("#", text);
			Assert.DoesNotContain("**", text);
			Assert.DoesNotContain("`", text);
			Assert.DoesNotContain("[bold]", text);
		}

		[Fact]
		public void EscapedMarkdownLiteral_WithRealTagLater_NotConfused()
		{
			var text = CellString(MarkupParser.Parse("[[markdown]] then [markdown]# Hi[/] end", Fg, Bg));
			Assert.Contains("[markdown] then", text); // escaped stays literal [markdown]
			Assert.Contains("Hi", text);
			Assert.Contains("end", text);
			Assert.DoesNotContain("# Hi", text);      // real region was converted
		}

		[Fact]
		public void MarkdownRegion_WithLiteralMarkdownTagInContent_DoesNotLeakOpeningTag()
		{
			// The demo case: markdown content mentions `[markdown]` in a code span.
			var text = CellString(MarkupParser.Parse(
				"[markdown]# Title\n\nThe `[markdown]` tag parses text.[/]", Fg, Bg));
			// The opening tag (region delimiter) must NOT leak. Before the fix the region
			// was treated as unclosed and emitted verbatim, producing a leading "[markdown]"
			// AND a second one from the code span (two occurrences) with the raw "#" heading.
			// After the fix only the inline-code content "[markdown]" remains (one occurrence)
			// and the heading marker is stripped.
			Assert.False(text.StartsWith("[markdown]"));  // opening tag must NOT leak as literal
			Assert.Equal(1, CountOccurrences(text, "[markdown]")); // only the code-span content
			Assert.Contains("Title", text);
			Assert.DoesNotContain("#", text);             // markdown actually processed (heading stripped)
		}

		[Fact]
		public void MarkdownRegion_NonNesting_ClosesAtFirstSlashTag()
		{
			// Inner literal [markdown] is content; region closes at the first [/].
			var text = CellString(MarkupParser.Parse(
				"before [markdown]see `[markdown]` here[/] after", Fg, Bg));
			Assert.Contains("before ", text);
			Assert.Contains("after", text);
			Assert.Contains("see ", text);
			// the inner literal [markdown] from the code span should appear as visible content text
			Assert.Contains("[markdown]", text);
		}
	}
}
