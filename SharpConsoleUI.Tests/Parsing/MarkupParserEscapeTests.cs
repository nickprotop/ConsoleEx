using Xunit;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserEscapeTests
	{
		[Fact]
		public void Escape_PlainText_Unchanged()
		{
			Assert.Equal("hello", MarkupParser.Escape("hello"));
		}

		[Fact]
		public void Escape_OpenBracket_Doubled()
		{
			Assert.Equal("[[", MarkupParser.Escape("["));
		}

		[Fact]
		public void Escape_CloseBracket_Doubled()
		{
			Assert.Equal("]]", MarkupParser.Escape("]"));
		}

		[Fact]
		public void Escape_MarkupTag_FullyEscaped()
		{
			Assert.Equal("[[red]]", MarkupParser.Escape("[red]"));
		}

		[Fact]
		public void Escape_AlreadyEscaped_DoubledAgain()
		{
			Assert.Equal("[[[[", MarkupParser.Escape("[["));
		}

		[Fact]
		public void Escape_EmptyString_ReturnsEmpty()
		{
			Assert.Equal("", MarkupParser.Escape(""));
		}

		[Fact]
		public void Escape_Null_ReturnsEmpty()
		{
			Assert.Equal("", MarkupParser.Escape(null!));
		}

		[Fact]
		public void Escape_NoBrackets_Unchanged()
		{
			Assert.Equal("hello world", MarkupParser.Escape("hello world"));
		}

		[Fact]
		public void Escape_TextWithBrackets_BracketsEscaped()
		{
			Assert.Equal("Hello [[world]]", MarkupParser.Escape("Hello [world]"));
		}

		[Fact]
		public void Escape_MultipleBracketPairs_AllEscaped()
		{
			Assert.Equal("[[]][[]]", MarkupParser.Escape("[][]"));
		}

		[Fact]
		public void Escape_OnlyBrackets_AllEscaped()
		{
			Assert.Equal("[[]]", MarkupParser.Escape("[]"));
		}
	}
}
