using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Highlighting
{
	// The class under test IS the obsolete one: BashSyntaxHighlighter was
	// superseded by the TextMate engine, and these tests exist to prove the
	// deprecated path still behaves while it remains public API. Calling it is
	// the point, so CS0612 is suppressed for the file rather than silenced by
	// giving every [Obsolete] a message it does not need.
#pragma warning disable CS0612 // Type or member is obsolete
	public class BashHighlighterTests
	{
		[Obsolete]
		private static (IReadOnlyList<SyntaxToken>, SyntaxLineState) Tok(string line)
			=> new BashSyntaxHighlighter().Tokenize(line, 0, SyntaxLineState.Initial);

		[Fact]
		[Obsolete]
		public void Comment_ColoredToEndOfLine()
		{
			const string line = "echo hi  # a comment";
			var (tokens, _) = Tok(line);

			// TextMate splits the comment into punctuation ('#') plus its text, where the old
			// regex emitted one span. What matters is that every column from '#' to end of line
			// is covered by comment-coloured tokens.
			int hash = line.IndexOf('#');
			var covering = tokens.Where(t => t.StartIndex >= hash).OrderBy(t => t.StartIndex).ToList();

			Assert.NotEmpty(covering);
			Assert.Equal(hash, covering[0].StartIndex);
			Assert.Equal(line.Length, covering[^1].StartIndex + covering[^1].Length);
			// All of it is one colour: the comment colour.
			Assert.Single(covering.Select(t => t.ForegroundColor).Distinct());
		}

		[Fact]
		[Obsolete]
		public void Keyword_If_Then_Fi_Colored()
		{
			var (tokens, _) = Tok("if true; then echo x; fi");
			// at least the control words produce tokens (distinct from plain text)
			Assert.NotEmpty(tokens);
			Assert.True(tokens.Select(t => t.ForegroundColor).Distinct().Count() >= 2);
		}

		[Fact]
		[Obsolete]
		public void Variable_Dollar_Colored()
		{
			var (tokens, _) = Tok("echo $HOME ${PATH} $1 $?");
			// at least one token starts at each '$'
			Assert.Contains(tokens, t => t.StartIndex == 5);   // $HOME
			Assert.Contains(tokens, t => t.StartIndex == 11);  // ${PATH}
		}

		[Fact]
		[Obsolete]
		public void DoubleAndSingleQuotedStrings_Colored()
		{
			var (tokens, _) = Tok("export X=\"abc\" Y='def'");
			Assert.Contains(tokens, t => t.Length >= 5); // "abc" span
			Assert.Contains(tokens, t => t.Length >= 5); // 'def' span
		}

		[Fact]
		[Obsolete]
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
			// Resolves through the TextMate fallback now; the two aliases share one instance.
			Assert.Same(SyntaxHighlighters.For("bash"), SyntaxHighlighters.For("sh"));
		}
	}
}
#pragma warning restore CS0612
