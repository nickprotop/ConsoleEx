using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;
using MarkdownStyle = SharpConsoleUI.Configuration.MarkdownStyle;

namespace SharpConsoleUI.Tests.Controls
{
	public class MarkdownCodeBlockWidthTests
	{
		[Fact]
		public void CodeBlock_Background_FillsFullWidth()
		{
			// 'short' code line in a wide control: the shaded code bg must reach the right edge.
			var ctrl = SharpConsoleUI.Builders.Controls.Markdown("```\nshort\n```").Build();
			var buf = new CharacterBuffer(30, 8);
			var bounds = new LayoutRect(0, 0, 28, 6);
			ctrl.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);

			var codeBg = MarkdownStyle.Default.CodeBackground;
			// Find the row containing 'short'
			int codeRow = -1;
			for (int y = 0; y < 6 && codeRow < 0; y++)
			{
				var row = string.Concat(Enumerable.Range(0, 28).Select(x => buf.GetCell(x, y).Character.ToString()));
				if (row.Contains("short")) codeRow = y;
			}
			Assert.True(codeRow >= 0, "code row not found");

			// Every cell on that row (to near the right edge) should carry the code background.
			int codeBgCells = Enumerable.Range(0, 28).Count(x =>
			{
				var bg = buf.GetCell(x, codeRow).Background;
				return bg.R == codeBg.R && bg.G == codeBg.G && bg.B == codeBg.B;
			});
			// Before the fix this was ~7 (just the text). After: should span most of the 28-wide row.
			Assert.True(codeBgCells >= 24, $"code bg only covered {codeBgCells}/28 cells — not full width");
		}

		[Fact]
		public void NormalText_DoesNotFillBackground()
		{
			// A normal markdown paragraph must NOT get a full-width background (regression guard).
			var ctrl = SharpConsoleUI.Builders.Controls.Markdown("hello world").Build();
			var buf = new CharacterBuffer(30, 6);
			var bounds = new LayoutRect(0, 0, 28, 4);
			ctrl.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);
			var codeBg = MarkdownStyle.Default.CodeBackground;
			// no cell should carry the code background
			bool any = Enumerable.Range(0, 4).Any(y => Enumerable.Range(0, 28).Any(x =>
			{
				var bg = buf.GetCell(x, y).Background; return bg.R == codeBg.R && bg.G == codeBg.G && bg.B == codeBg.B;
			}));
			Assert.False(any);
		}
	}
}
