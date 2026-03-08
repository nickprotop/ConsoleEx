using Xunit;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserTruncateTests
	{
		[Fact]
		public void Truncate_PlainTextWithinLimit_Unchanged()
		{
			Assert.Equal("hello", MarkupParser.Truncate("hello", 10));
		}

		[Fact]
		public void Truncate_PlainTextExceedingLimit_Truncated()
		{
			Assert.Equal("hel", MarkupParser.Truncate("hello", 3));
		}

		[Fact]
		public void Truncate_TaggedText_TruncatesToVisibleLength()
		{
			var result = MarkupParser.Truncate("[red]hello world[/]", 5);
			Assert.Equal("[red]hello[/]", result);
		}

		[Fact]
		public void Truncate_NestedTags_AllProperlyClosed()
		{
			var result = MarkupParser.Truncate("[bold][red]testing[/][/]", 2);
			Assert.Equal("[bold][red]te[/][/]", result);
		}

		[Fact]
		public void Truncate_EscapedBrackets_CountedCorrectly()
		{
			// [[ is 1 visible char, a is 1 visible char = 2 visible
			var result = MarkupParser.Truncate("[[ab", 2);
			Assert.Equal("[[a", result);
		}

		[Fact]
		public void Truncate_MaxLengthZero_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, MarkupParser.Truncate("[red]text[/]", 0));
		}

		[Fact]
		public void Truncate_MaxLengthGreaterThanText_Unchanged()
		{
			var input = "[red]hi[/]";
			Assert.Equal(input, MarkupParser.Truncate(input, 100));
		}

		[Fact]
		public void Truncate_EmptyInput_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, MarkupParser.Truncate("", 5));
		}

		[Fact]
		public void Truncate_NullInput_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, MarkupParser.Truncate(null!, 5));
		}

		[Fact]
		public void Truncate_MultipleOpenTagsAtTruncation_AllClosed()
		{
			var result = MarkupParser.Truncate("[bold][italic][red]hello[/][/][/]", 3);
			Assert.Equal("[bold][italic][red]hel[/][/][/]", result);
		}

		[Fact]
		public void Truncate_TagsContributeZeroToVisibleCount()
		{
			// Tags don't count: [red][bold] = 0 visible, "ab" = 2 visible
			var result = MarkupParser.Truncate("[red][bold]abcdef[/][/]", 2);
			Assert.Equal("[red][bold]ab[/][/]", result);
		}

		[Fact]
		public void Truncate_EscapedCloseBrackets_CountedCorrectly()
		{
			// ]] is 1 visible char
			var result = MarkupParser.Truncate("a]]b", 2);
			Assert.Equal("a]]", result);
		}

		[Fact]
		public void Truncate_BrokenTag_TreatedAsLiteral()
		{
			// "[" at end is a broken tag → counted as visible char
			var result = MarkupParser.Truncate("ab[", 3);
			Assert.Equal("ab[", result);
		}
	}
}
