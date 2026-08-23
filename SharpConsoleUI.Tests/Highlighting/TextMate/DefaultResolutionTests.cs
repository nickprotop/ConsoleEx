using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using Xunit;

namespace SharpConsoleUI.Tests.Highlighting.TextMate
{
	public class DefaultResolutionTests
	{
		[Fact]
		public void For_ResolvesTextMateLanguages_WithNoRegistrationCall()
		{
			// No RegisterAll() anywhere: resolution must work out of the box.
			Assert.NotNull(SyntaxHighlighters.For("rust"));
			Assert.NotNull(SyntaxHighlighters.For("go"));
			Assert.True(SyntaxHighlighters.Has("python"));
		}

		[Fact]
		public void For_Sql_NowResolves_WasNullBeforeTextMate()
		{
			// CxSql calls this; it returned null before the TextMate engine landed.
			Assert.NotNull(SyntaxHighlighters.For("sql"));
		}

		[Theory]
		[InlineData("csharp")]
		[InlineData("cs")]
		[InlineData("json")]
		[InlineData("javascript")]
		[InlineData("js")]
		[InlineData("node")]
		[InlineData("mjs")]
		[InlineData("cjs")]
		[InlineData("css")]
		[InlineData("html")]
		[InlineData("htm")]
		[InlineData("xml")]
		[InlineData("yaml")]
		[InlineData("yml")]
		[InlineData("razor")]
		[InlineData("cshtml")]
		[InlineData("dockerfile")]
		[InlineData("docker")]
		[InlineData("sln")]
		[InlineData("diff")]
		[InlineData("patch")]
		[InlineData("markdown")]
		[InlineData("md")]
		[InlineData("bash")]
		[InlineData("sh")]
		[InlineData("shell")]
		[InlineData("zsh")]
		public void EveryHistoricalAlias_StillResolves(string alias)
		{
			// These aliases all worked before TextMate; none may silently regress.
			Assert.NotNull(SyntaxHighlighters.For(alias));
		}

		[Fact]
		public void ExplicitRegistration_WinsOverTextMateFallback()
		{
			var custom = new StubHighlighter();
			SyntaxHighlighters.Register("register-precedence-test", custom);
			Assert.Same(custom, SyntaxHighlighters.For("register-precedence-test"));
		}

		[Fact]
		public void For_UnknownLanguage_StillReturnsNull()
		{
			Assert.Null(SyntaxHighlighters.For("definitely-not-a-language"));
			Assert.Null(SyntaxHighlighters.For(null));
			Assert.Null(SyntaxHighlighters.For(""));
		}

		[Fact]
		public void For_IsCaseInsensitive_AndTrims()
		{
			Assert.NotNull(SyntaxHighlighters.For("CSHARP"));
			Assert.NotNull(SyntaxHighlighters.For("  rust  "));
		}

		private sealed class StubHighlighter : ISyntaxHighlighter
		{
			public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
				Tokenize(string line, int lineIndex, SyntaxLineState startState)
				=> (Array.Empty<SyntaxToken>(), SyntaxLineState.Initial);
		}
	}
}
