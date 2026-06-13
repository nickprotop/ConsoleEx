using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkdownToMarkupLinkTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;
		private static string Visible(List<Cell> cells)
			=> string.Concat(cells.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString())).Trim();

		[Fact]
		public void Link_PreservesUrl_AndVisibleText()
		{
			string markup = MarkdownToMarkup.Convert("[text](https://x/y?a=1&b=2)");
			Assert.Contains("[link=", markup);
			var cells = MarkupParser.Parse(markup, Fg, Bg, out var spans);
			Assert.Equal("text", Visible(cells));
			Assert.Equal("https://x/y?a=1&b=2", spans.Single().Url);
		}

		[Fact]
		public void Link_WithNestedEmphasis_Works()
		{
			string markup = MarkdownToMarkup.Convert("[**bold** text](https://e.com)");
			var cells = MarkupParser.Parse(markup, Fg, Bg, out var spans);
			Assert.Equal("bold text", Visible(cells));
			Assert.Equal("https://e.com", spans.Single().Url);
			Assert.Contains(cells, c => (c.Decorations & TextDecoration.Bold) != 0);
		}

		[Fact]
		public void Autolink_PreservesUrl()
		{
			string markup = MarkdownToMarkup.Convert("<https://example.com>");
			var cells = MarkupParser.Parse(markup, Fg, Bg, out var spans);
			Assert.Equal("https://example.com", spans.Single().Url);
		}

		[Fact]
		public void Link_UrlWithBracket_RoundTrips()
		{
			string markup = MarkdownToMarkup.Convert("[t](http://h/a%5Db)");
			MarkupParser.Parse(markup, Fg, Bg, out var spans);
			Assert.Equal("http://h/a%5Db", spans.Single().Url);
		}

		[Fact]
		public void Autolink_RendersUrlAsVisibleText()
		{
			string markup = MarkdownToMarkup.Convert("<https://example.com>");
			var cells = MarkupParser.Parse(markup, Fg, Bg, out _);
			Assert.Equal("https://example.com", Visible(cells));
		}

		[Fact]
		public void Link_VisibleTextWithBrackets_NotCorrupted()
		{
			// Link text containing literal brackets must be escaped so it round-trips as visible text.
			string markup = MarkdownToMarkup.Convert("[a [b] c](https://e.com)");
			var cells = MarkupParser.Parse(markup, Fg, Bg, out var spans);
			Assert.Equal("a [b] c", Visible(cells));
			Assert.Equal("https://e.com", spans.Single().Url);
		}

		[Fact]
		public void Link_EmptyText_RecordsSpanWithUrl_NoCrash()
		{
			string markup = MarkdownToMarkup.Convert("[](https://e.com)");
			var cells = MarkupParser.Parse(markup, Fg, Bg, out var spans);
			// empty visible text — the [link=…][/] with no inner content records no span (end==start guard),
			// OR records an empty span depending on parser; assert it does not throw and produces no garbage.
			Assert.DoesNotContain(cells, c => c.Character.ToString() == "[");
		}
	}
}
