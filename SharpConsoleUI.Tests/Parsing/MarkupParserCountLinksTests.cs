using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserCountLinksTests
	{
		[Theory]
		[InlineData("plain text", 0)]
		[InlineData("[red]styled[/] no links", 0)]
		[InlineData("[link=https://a.com]x[/]", 1)]
		[InlineData("[link=a]x[/] and [link=b]y[/]", 2)]
		[InlineData("[link=u][bold]nested[/][/]", 1)]      // nested style inside one link = 1
		[InlineData("[linkable]not a link[/]", 0)]   // starts with "link" but not "link=" — must NOT count
		public void CountLinks_PlainAndNativeMarkup(string markup, int expected)
		{
			Assert.Equal(expected, MarkupParser.CountLinks(markup));
		}

		[Fact]
		public void CountLinks_MarkdownContent_AppliesPreprocessing()
		{
			// [markdown] region: the link only becomes [link=…] after markdown preprocessing.
			Assert.Equal(1, MarkupParser.CountLinks("[markdown][text](https://x)[/]"));
		}

		[Fact]
		public void CountLinks_MarkdownMultipleLinks()
		{
			Assert.Equal(2, MarkupParser.CountLinks("[markdown][a](http://1) and [b](http://2)[/]"));
		}

		[Fact]
		public void CountLinks_NullOrEmpty_Zero()
		{
			Assert.Equal(0, MarkupParser.CountLinks(null!));
			Assert.Equal(0, MarkupParser.CountLinks(""));
		}
	}
}
