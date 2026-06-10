using SharpConsoleUI;
using SharpConsoleUI.Highlighting;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;
using MarkdownStyle = SharpConsoleUI.Configuration.MarkdownStyle;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkdownCodeHighlightTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;
		private static List<Cell> Render(string md)
			=> MarkupParser.Parse(MarkdownToMarkup.Convert(md), Fg, Bg);
		private static string CellString(IEnumerable<Cell> cells)
			=> string.Concat(cells.Select(c => c.Character.ToString()));

		[Fact]
		public void FencedCsharp_KeywordGetsHighlightColor_NotPlainCodeFg()
		{
			var cells = Render("```csharp\nvar x = 1;\n```");
			Assert.Contains(cells, c => c.Background == MarkdownStyle.Default.CodeBackground);
			Assert.Contains(cells, c => c.Background == MarkdownStyle.Default.CodeBackground
				&& c.Foreground != MarkdownStyle.Default.CodeForeground
				&& c.Character.ToString().Trim().Length > 0);
			Assert.Contains("var x = 1;", CellString(cells));
		}

		[Fact]
		public void UnknownLanguage_FallsBackToFlat()
		{
			var cells = Render("```rust\nlet x = 1;\n```");
			Assert.Contains("let x = 1;", CellString(cells));
			Assert.All(cells.Where(c => c.Background == MarkdownStyle.Default.CodeBackground
					&& c.Character.ToString().Trim().Length > 0),
				c => Assert.Equal(MarkdownStyle.Default.CodeForeground, c.Foreground));
		}

		[Fact]
		public void NoLanguageFence_FallsBackToFlat()
		{
			var cells = Render("```\nplain text\n```");
			Assert.All(cells.Where(c => c.Background == MarkdownStyle.Default.CodeBackground
					&& c.Character.ToString().Trim().Length > 0),
				c => Assert.Equal(MarkdownStyle.Default.CodeForeground, c.Foreground));
		}

		[Fact]
		public void PerStyleOverride_WinsOverRegistry()
		{
			var sentinel = new Color(1, 2, 3);
			var custom = new WholeLineHighlighter(sentinel);
			var style = MarkdownStyle.Default with
			{
				CodeHighlighters = new Dictionary<string, SharpConsoleUI.Controls.ISyntaxHighlighter> { ["csharp"] = custom }
			};
			var native = MarkdownToMarkup.Convert("```csharp\nvar x;\n```", style);
			var cells = MarkupParser.Parse(native, Fg, Bg);
			Assert.Contains(cells, c => c.Foreground == sentinel);
		}

		[Fact]
		public void CopyStaysPlainText_ForHighlightedBlock()
		{
			var text = CellString(Render("```csharp\nvar x = 1;\n```"));
			Assert.DoesNotContain("[#", text);
			Assert.Contains("var x = 1;", text);
		}

		private sealed class WholeLineHighlighter : SharpConsoleUI.Controls.ISyntaxHighlighter
		{
			private readonly Color _c;
			public WholeLineHighlighter(Color c) => _c = c;
			public (IReadOnlyList<SharpConsoleUI.Controls.SyntaxToken> Tokens, SharpConsoleUI.Controls.SyntaxLineState EndState)
				Tokenize(string line, int lineIndex, SharpConsoleUI.Controls.SyntaxLineState startState)
			{
				var toks = line.Length > 0
					? new List<SharpConsoleUI.Controls.SyntaxToken> { new(0, line.Length, _c) }
					: new List<SharpConsoleUI.Controls.SyntaxToken>();
				return (toks, startState);
			}
		}
	}
}
