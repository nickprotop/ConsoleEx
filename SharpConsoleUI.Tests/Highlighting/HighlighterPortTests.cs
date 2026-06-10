using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Highlighting
{
	public class HighlighterPortTests
	{
		[Fact]
		public void CSharp_KeywordStringNumberComment_GetDistinctColors()
		{
			var hl = new CSharpSyntaxHighlighter();
			var (tokens, _) = hl.Tokenize("var s = \"hi\"; // n=1", 0, SyntaxLineState.Initial);
			Assert.NotEmpty(tokens);
			Assert.Contains(tokens, t => t.Length > 0);
			Assert.True(tokens.Select(t => t.ForegroundColor).Distinct().Count() >= 2);
		}

		[Fact]
		public void CSharp_BlockComment_StateCarriesAcrossLines()
		{
			var hl = new CSharpSyntaxHighlighter();
			var (_, mid) = hl.Tokenize("/* start", 0, SyntaxLineState.Initial);
			var (tokens2, _) = hl.Tokenize("still comment */ var x;", 1, mid);
			Assert.Contains(tokens2, t => t.StartIndex == 0 && t.Length > 0);
		}

		[Theory]
		[InlineData("json")]
		[InlineData("js")]
		[InlineData("css")]
		[InlineData("html")]
		[InlineData("xml")]
		[InlineData("yaml")]
		[InlineData("razor")]
		[InlineData("dockerfile")]
		[InlineData("sln")]
		[InlineData("diff")]
		[InlineData("markdown")]
		public void EveryBuiltInLanguage_Resolves_AndTokenizesWithoutThrowing(string lang)
		{
			var hl = SyntaxHighlighters.For(lang);
			Assert.NotNull(hl);
			var ex = Record.Exception(() =>
			{
				var (tokens, end) = hl!.Tokenize("name: value  // x = 1", 0, SyntaxLineState.Initial);
				Assert.NotNull(tokens);
				Assert.NotNull(end);
			});
			Assert.Null(ex);
		}

		[Fact]
		public void Json_KeyStringNumber_ProduceTokens()
		{
			var hl = SyntaxHighlighters.For("json");
			var (tokens, _) = hl!.Tokenize("{ \"a\": 1, \"b\": \"x\" }", 0, SyntaxLineState.Initial);
			Assert.NotEmpty(tokens);
			Assert.True(tokens.Select(t => t.ForegroundColor).Distinct().Count() >= 2);
		}
	}
}
