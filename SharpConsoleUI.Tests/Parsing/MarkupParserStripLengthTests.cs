using Xunit;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserStripLengthTests
	{
		[Fact]
		public void StripLength_PlainText_ReturnsStringLength()
		{
			Assert.Equal(5, MarkupParser.StripLength("hello"));
		}

		[Fact]
		public void StripLength_RedTag_ReturnsVisibleLength()
		{
			Assert.Equal(4, MarkupParser.StripLength("[red]text[/]"));
		}

		[Fact]
		public void StripLength_EscapedOpenBracket_CountsAsOne()
		{
			Assert.Equal(1, MarkupParser.StripLength("[["));
		}

		[Fact]
		public void StripLength_EscapedCloseBracket_CountsAsOne()
		{
			Assert.Equal(1, MarkupParser.StripLength("]]"));
		}

		[Fact]
		public void StripLength_EmptyString_ReturnsZero()
		{
			Assert.Equal(0, MarkupParser.StripLength(""));
		}

		[Fact]
		public void StripLength_Null_ReturnsZero()
		{
			Assert.Equal(0, MarkupParser.StripLength(null!));
		}

		[Fact]
		public void StripLength_TagsOnly_ReturnsZero()
		{
			Assert.Equal(0, MarkupParser.StripLength("[red][/]"));
		}

		[Fact]
		public void StripLength_MultiLine_ReturnsMaxLineLength()
		{
			Assert.Equal(3, MarkupParser.StripLength("abc\ndef"));
		}

		[Fact]
		public void StripLength_MultiLineWithMarkup_ReturnsMaxVisibleLineLength()
		{
			Assert.Equal(4, MarkupParser.StripLength("[red]ab[/]\n[blue]cdef[/]"));
		}

		[Fact]
		public void StripLength_Nested_ReturnsVisibleLength()
		{
			Assert.Equal(5, MarkupParser.StripLength("[bold][red]hello[/][/]"));
		}

		[Fact]
		public void StripLength_EscapedBracketsAsLiteral_CorrectCount()
		{
			// [[bold]] → [bold] which is 6 visible chars
			Assert.Equal(6, MarkupParser.StripLength("[[bold]]"));
		}

		[Fact]
		public void StripLength_InvalidTag_CountsAsLiteral()
		{
			// [invalid] is skipped as a tag, but StripLength skips all [...] tags
			// regardless of validity. So [invalid] → 0 visible, "text" → 4
			Assert.Equal(4, MarkupParser.StripLength("[invalid]text"));
		}

		[Fact]
		public void StripLength_OnlyTagsNoText_ReturnsZero()
		{
			Assert.Equal(0, MarkupParser.StripLength("[bold][italic][/][/]"));
		}

		[Fact]
		public void StripLength_LongTextWithManyTags_CorrectCount()
		{
			// "hello" + " " + "wor" + "ld" = 5+1+3+2 = 11
			Assert.Equal(11, MarkupParser.StripLength("[bold]hello[/] [red]wor[/]ld"));
		}

		[Fact]
		public void StripLength_BrokenOpenBracket_CountsAsLiteral()
		{
			// "[" at end with no closing → counted as 1 visible char
			Assert.Equal(5, MarkupParser.StripLength("text["));
		}
	}
}
