using System.Collections.Concurrent;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using SharpConsoleUI.Highlighting.TextMate;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Highlighting.TextMate
{
	public class TextMateHighlighterTests
	{
		[Fact]
		public void Tokenize_ColoursKeywordsAndStringsDifferently()
		{
			var hl = TextMateEngine.Instance.GetHighlighter("csharp");
			Assert.NotNull(hl);

			var (tokens, _) = hl!.Tokenize("string s = \"hi\";", 0, SyntaxLineState.Initial);

			Assert.NotEmpty(tokens);
			var keyword = tokens.First(t => t.StartIndex == 0);
			// Index 11 is the opening quote; TextMate emits it as its own token and the
			// content "hi" at 12. Both carry the string colour, so accept either.
			var literal = tokens.First(t => t.StartIndex >= 11 && t.StartIndex <= 12);
			Assert.NotEqual(keyword.ForegroundColor, literal.ForegroundColor);
		}

		[Fact]
		public void Tokenize_CarriesBlockCommentStateAcrossLines()
		{
			var hl = TextMateEngine.Instance.GetHighlighter("csharp")!;

			var (_, afterOpen) = hl.Tokenize("/* open", 0, SyntaxLineState.Initial);
			var (tokens, _) = hl.Tokenize("still comment */ int x;", 1, afterOpen);

			Assert.NotEmpty(tokens);
			Assert.Equal(0, tokens[0].StartIndex);

			// "int" appears after the comment closes and must differ in colour from the comment.
			var intToken = tokens.First(t => t.StartIndex >= 17);
			Assert.NotEqual(tokens[0].ForegroundColor, intToken.ForegroundColor);
		}

		[Fact]
		public void Tokenize_StateIsCarried_NotRestarted()
		{
			var hl = TextMateEngine.Instance.GetHighlighter("csharp")!;

			var (_, afterOpen) = hl.Tokenize("/* open", 0, SyntaxLineState.Initial);

			// The same line tokenized fresh vs. inside an open comment must differ.
			var (inComment, _) = hl.Tokenize("int x;", 1, afterOpen);
			var (fresh, _) = hl.Tokenize("int x;", 1, SyntaxLineState.Initial);

			Assert.NotEqual(fresh[0].ForegroundColor, inComment[0].ForegroundColor);
		}

		[Fact]
		public void GetHighlighter_ResolvesLanguagesTheBuiltInsNeverCovered()
		{
			Assert.NotNull(TextMateEngine.Instance.GetHighlighter("rust"));
			Assert.NotNull(TextMateEngine.Instance.GetHighlighter("python"));
			Assert.NotNull(TextMateEngine.Instance.GetHighlighter("sql"));
			Assert.NotNull(TextMateEngine.Instance.GetHighlighter("go"));
		}

		[Fact]
		public void GetHighlighter_UnknownLanguage_ReturnsNull()
		{
			Assert.Null(TextMateEngine.Instance.GetHighlighter("not-a-real-language"));
			Assert.Null(TextMateEngine.Instance.GetHighlighter(null));
			Assert.Null(TextMateEngine.Instance.GetHighlighter(""));
		}

		[Fact]
		public void GetHighlighter_IsCached_SameInstancePerLanguage()
		{
			var a = TextMateEngine.Instance.GetHighlighter("csharp");
			var b = TextMateEngine.Instance.GetHighlighter("CSharp");
			Assert.Same(a, b);
		}

		[Fact]
		public void Tokenize_ConcurrentAcrossThreads_DoesNotThrowOrCorrupt()
		{
			// TextMateSharp compiles grammar rules lazily DURING tokenization, mutating shared
			// per-grammar state. Without serialization this corrupts the grammar and throws.
			// The language here must be one no other test has warmed up, so rule compilation
			// genuinely happens under contention — that is what reproduces the bug.
			var hl = TextMateEngine.Instance.GetHighlighter("typescript");
			Assert.NotNull(hl);

			string[] lines =
			{
				"export class Foo { }",
				"/* block */ const x: number = 42;",
				"const s = `interpolated ${x}`;",
				"async function f(): Promise<void> { await g(); }",
				"type A = { b: string[] };",
			};

			var errors = new ConcurrentBag<Exception>();
			Parallel.For(0, 64, _ =>
			{
				try
				{
					SyntaxLineState state = SyntaxLineState.Initial;
					foreach (string line in lines)
					{
						var (tokens, next) = hl!.Tokenize(line, 0, state);
						state = next;
						Assert.NotEmpty(tokens);
					}
				}
				catch (Exception ex)
				{
					errors.Add(ex);
				}
			});

			Assert.Empty(errors);
		}

		[Fact]
		public void Tokenize_ConcurrentAcrossDifferentLanguages_DoesNotThrow()
		{
			string[] languages = { "python", "go", "ruby", "java", "php", "lua" };
			var errors = new ConcurrentBag<Exception>();

			Parallel.ForEach(languages, lang =>
			{
				try
				{
					var hl = TextMateEngine.Instance.GetHighlighter(lang);
					Assert.NotNull(hl);
					var (tokens, _) = hl!.Tokenize("x = 1", 0, SyntaxLineState.Initial);
					Assert.NotNull(tokens);
				}
				catch (Exception ex)
				{
					errors.Add(ex);
				}
			});

			Assert.Empty(errors);
		}

		[Fact]
		public void Tokenize_EmptyLine_ReturnsNoTokensAndUsableState()
		{
			var hl = TextMateEngine.Instance.GetHighlighter("csharp")!;
			var (tokens, endState) = hl.Tokenize(string.Empty, 0, SyntaxLineState.Initial);

			Assert.NotNull(endState);
			Assert.Empty(tokens);
		}

		[Fact]
		public void Tokenize_TokensStayWithinLineBounds()
		{
			var hl = TextMateEngine.Instance.GetHighlighter("csharp")!;
			const string line = "var s = \"unterminated";

			var (tokens, _) = hl.Tokenize(line, 0, SyntaxLineState.Initial);

			foreach (var t in tokens)
			{
				Assert.True(t.StartIndex >= 0);
				Assert.True(t.StartIndex + t.Length <= line.Length,
					$"token {t.StartIndex}+{t.Length} exceeds line length {line.Length}");
			}
		}

		[Fact]
		public void Tokenize_WideAndCombiningCharacters_StayInBounds()
		{
			var hl = TextMateEngine.Instance.GetHighlighter("csharp")!;
			const string line = "var s = \"日本語 🎉 café\";";

			var (tokens, _) = hl.Tokenize(line, 0, SyntaxLineState.Initial);

			foreach (var t in tokens)
				Assert.True(t.StartIndex + t.Length <= line.Length);
		}
	}
}
