using Xunit;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserRemoveTests
	{
		[Fact]
		public void Remove_PlainText_Unchanged()
		{
			Assert.Equal("hello", MarkupParser.Remove("hello"));
		}

		[Fact]
		public void Remove_RedTag_ReturnsTextOnly()
		{
			Assert.Equal("text", MarkupParser.Remove("[red]text[/]"));
		}

		[Fact]
		public void Remove_NestedTags_ReturnsPlainText()
		{
			Assert.Equal("hello world", MarkupParser.Remove("[bold][red]hello[/] world[/]"));
		}

		[Fact]
		public void Remove_EscapedBrackets_ConvertedToSingle()
		{
			Assert.Equal("[text]", MarkupParser.Remove("[[text]]"));
		}

		[Fact]
		public void Remove_EmptyString_ReturnsEmpty()
		{
			Assert.Equal("", MarkupParser.Remove(""));
		}

		[Fact]
		public void Remove_Null_ReturnsEmpty()
		{
			Assert.Equal("", MarkupParser.Remove(null!));
		}

		[Fact]
		public void Remove_TagsOnly_ReturnsEmpty()
		{
			Assert.Equal("", MarkupParser.Remove("[red][/]"));
		}

		[Fact]
		public void Remove_DeepNested_ReturnsSingleChar()
		{
			Assert.Equal("x", MarkupParser.Remove("[a][b]x[/][/]"));
		}

		[Fact]
		public void Remove_UnclosedBracketAtEnd_LiteralBracket()
		{
			Assert.Equal("text[", MarkupParser.Remove("text["));
		}

		[Fact]
		public void Remove_RealWorldMarkup_StripsAllTags()
		{
			Assert.Equal("Error: warning",
				MarkupParser.Remove("[bold red on blue]Error:[/] [yellow]warning[/]"));
		}

		[Fact]
		public void Remove_StrayCloseBracket_KeptAsLiteral()
		{
			// Single ] without ]] is kept as literal
			var result = MarkupParser.Remove("a]b");
			Assert.Equal("a]b", result);
		}

		[Fact]
		public void Remove_EscapedCloseBrackets_ConvertedToSingle()
		{
			Assert.Equal("a]b", MarkupParser.Remove("a]]b"));
		}
	}
}
