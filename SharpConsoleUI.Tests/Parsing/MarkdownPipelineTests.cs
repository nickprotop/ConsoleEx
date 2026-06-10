using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkdownPipelineTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;
		private static string CellString(IEnumerable<Cell> cells)
			=> string.Concat(cells.Select(c => c.Character.ToString()));

		[Fact]
		public void MixedMarkdownAndMarkup_BothRender()
		{
			var cells = MarkupParser.Parse("[red]alert:[/] [markdown]**bold**[/]", Fg, Bg);
			var text = CellString(cells);
			Assert.Contains("alert:", text);
			Assert.Contains("bold", text);
			Assert.DoesNotContain("**", text);
		}

		[Fact]
		public void MarkdownInsideColorScope_EmphasisInheritsColor()
		{
			var red = new Color(255, 0, 0);
			var cells = MarkupParser.Parse("[#FF0000][markdown]**x**[/][/]", Fg, Bg);
			var xCell = cells.First(c => c.Character.ToString() == "x");
			Assert.Equal(red, xCell.Foreground);   // emphasis colorless => inherits red scope
			Assert.True((xCell.Decorations & TextDecoration.Bold) != 0);
		}

		[Fact]
		public void MultipleRegions_RenderIndependently()
		{
			var cells = MarkupParser.Parse("[markdown]*a*[/] mid [markdown]*b*[/]", Fg, Bg);
			var text = CellString(cells);
			Assert.Contains("a", text);
			Assert.Contains("mid", text);
			Assert.Contains("b", text);
		}

		[Fact]
		public void ParseLines_WrapsLongMarkdownParagraph()
		{
			var md = "[markdown]" + string.Join(" ", Enumerable.Repeat("word", 30)) + "[/]";
			var lines = MarkupParser.ParseLines(md, 20, Fg, Bg);
			Assert.True(lines.Count > 1);                       // wrapped
			Assert.All(lines, l => Assert.True(l.Count <= 20)); // within width
		}
	}
}
