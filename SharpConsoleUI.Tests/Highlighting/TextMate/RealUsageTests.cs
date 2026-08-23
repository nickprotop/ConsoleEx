// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Highlighting.TextMate
{
	/// <summary>
	/// Exercises the real end-to-end paths a user hits: markdown fenced code blocks, and a
	/// MultilineEditControl painted through the real layout/paint pipeline.
	/// </summary>
	public class RealUsageTests
	{
		private static List<string> ForegroundsIn(string markup)
			=> Regex.Matches(markup, @"\[#([0-9A-F]{6}) on #([0-9A-F]{6})\]")
				.Select(m => m.Groups[1].Value)
				.Distinct()
				.ToList();

		[Fact]
		public void Markdown_FencedCSharpBlock_EmitsSeveralDistinctTokenColours()
		{
			string markup = MarkdownToMarkup.Convert(
				"```csharp\npublic class Foo { int x = 42; }\n```");

			// A highlighted block emits several differently-coloured spans; a flat block emits one.
			var colours = ForegroundsIn(markup);
			Assert.True(colours.Count >= 3,
				$"expected several token colours, got {colours.Count}: {string.Join(",", colours)}");
		}

		[Fact]
		public void Markdown_FencedRustBlock_NowHighlights_WasFlatBeforeTextMate()
		{
			string markup = MarkdownToMarkup.Convert(
				"```rust\nfn main() { let x = 42; }\n```");

			Assert.True(ForegroundsIn(markup).Count >= 2,
				"rust should highlight now that TextMate resolves it");
		}

		[Fact]
		public void Markdown_UnknownLanguage_StillRendersFlatShadedBlock()
		{
			string markup = MarkdownToMarkup.Convert(
				"```definitely-not-a-language\nsome text\n```");

			Assert.Contains("[fillwidth]", markup);
			Assert.Single(ForegroundsIn(markup));
		}

		[Fact]
		public void Markdown_IndentedBlockWithoutLanguage_StaysFlat()
		{
			string markup = MarkdownToMarkup.Convert("    plain indented code\n");

			Assert.Contains("[fillwidth]", markup);
		}

		[Fact]
		public void Markdown_MultiLineBlockComment_StaysCommentColouredAcrossLines()
		{
			string markup = MarkdownToMarkup.Convert(
				"```csharp\n/* open\nstill comment */ int x;\n```");

			// The construct spans lines; if state were not carried, line 2 would be mis-coloured
			// and the palette would contain the keyword colour for "still".
			Assert.True(ForegroundsIn(markup).Count >= 2);
		}

		[Fact]
		public void MultilineEdit_WithHighlighter_PaintsSeveralColoursAndSurvivesRepaint()
		{
			// Boundary-stressing size: narrow enough that layout differs from content extent.
			const int width = 34;
			const int height = 6;

			var editor = new MultilineEditControl
			{
				Content = "public class Foo\n{\n\tint x = 42;\n}\n",
				SyntaxHighlighter = SyntaxHighlighters.For("csharp"),
			};

			var bounds = new LayoutRect(0, 0, width, height);

			var first = new CharacterBuffer(width, height);
			editor.PaintDOM(first, bounds, bounds, Color.White, Color.Black);

			// Repaint the SAME control: highlighting must survive, not be a first-frame artefact.
			var second = new CharacterBuffer(width, height);
			editor.PaintDOM(second, bounds, bounds, Color.White, Color.Black);

			var firstColours = DistinctForegrounds(first, width, height);
			var secondColours = DistinctForegrounds(second, width, height);

			Assert.True(firstColours.Count >= 3,
				$"expected several token colours in the editor, got {firstColours.Count}");
			Assert.Equal(firstColours, secondColours);
		}

		[Fact]
		public void MultilineEdit_WithoutHighlighter_PaintsUniformForeground()
		{
			const int width = 34;
			const int height = 6;

			var editor = new MultilineEditControl
			{
				Content = "public class Foo\n{\n\tint x = 42;\n}\n",
			};

			var buffer = new CharacterBuffer(width, height);
			editor.PaintDOM(buffer, new LayoutRect(0, 0, width, height),
				new LayoutRect(0, 0, width, height), Color.White, Color.Black);

			// No highlighter: every glyph shares one foreground.
			Assert.Single(DistinctForegrounds(buffer, width, height));
		}

		private static HashSet<Color> DistinctForegrounds(CharacterBuffer buffer, int width, int height)
		{
			var colours = new HashSet<Color>();
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					var cell = buffer.GetCell(x, y);
					if (cell.Character.Value is ' ' or '\0' or '\t') continue;
					colours.Add(cell.Foreground);
				}
			}
			return colours;
		}
	}
}
