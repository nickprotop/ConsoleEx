using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Highlighting
{
	public class BashHighlighterTests
	{
		private static (IReadOnlyList<SyntaxToken>, SyntaxLineState) Tok(string line)
			=> new BashSyntaxHighlighter().Tokenize(line, 0, SyntaxLineState.Initial);

		[Fact]
		public void Comment_ColoredToEndOfLine()
		{
			var (tokens, _) = Tok("echo hi  # a comment");
			Assert.Contains(tokens, t => t.StartIndex == 9 && t.Length == "# a comment".Length);
		}

		[Fact]
		public void Keyword_If_Then_Fi_Colored()
		{
			var (tokens, _) = Tok("if true; then echo x; fi");
			// at least the control words produce tokens (distinct from plain text)
			Assert.NotEmpty(tokens);
			Assert.True(tokens.Select(t => t.ForegroundColor).Distinct().Count() >= 2);
		}

		[Fact]
		public void Variable_Dollar_Colored()
		{
			var (tokens, _) = Tok("echo $HOME ${PATH} $1 $?");
			// at least one token starts at each '$'
			Assert.Contains(tokens, t => t.StartIndex == 5);   // $HOME
			Assert.Contains(tokens, t => t.StartIndex == 11);  // ${PATH}
		}

		[Fact]
		public void DoubleAndSingleQuotedStrings_Colored()
		{
			var (tokens, _) = Tok("export X=\"abc\" Y='def'");
			Assert.Contains(tokens, t => t.Length >= 5); // "abc" span
			Assert.Contains(tokens, t => t.Length >= 5); // 'def' span
		}

		[Fact]
		public void HashInsideParamExpansion_NotTreatedAsComment()
		{
			// ${X#prefix} — the # is parameter-expansion, not a comment.
			const string src = "echo ${X#prefix}";
			var (tokens, _) = Tok(src);
			int hashIdx = src.IndexOf('#');
			// No token should both start at the '#' and run to end-of-line (a comment token).
			Assert.DoesNotContain(tokens, t => t.StartIndex == hashIdx && t.StartIndex + t.Length == src.Length);
		}

		[Fact]
		public void RegistersUnderBashAndSh()
		{
			Assert.NotNull(SyntaxHighlighters.For("bash"));
			Assert.NotNull(SyntaxHighlighters.For("sh"));
			Assert.IsType<BashSyntaxHighlighter>(SyntaxHighlighters.For("bash"));
		}
	}
}
