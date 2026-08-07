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

		[Fact]
		public void CodeBlock_Background_FillsFullWidth_OnEveryWrappedRow()
		{
			// THE REAL THING: a code line LONGER than the control, painted through the real control
			// at a narrow width so it actually wraps. The existing test above uses a short line, so
			// it never reaches WrapCellLine -- which is exactly where the marker was being dropped,
			// and why the shading visibly stopped at the last character on long code lines.
			var longLine = "var result = await client.SendRequestAsync(endpoint, payload, cancellationToken);";
			var ctrl = SharpConsoleUI.Builders.Controls.Markdown("```csharp\n" + longLine + "\n```").Build();

			var buf = new CharacterBuffer(40, 14);
			var bounds = new LayoutRect(0, 0, 38, 12);
			ctrl.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);

			var codeBg = MarkdownStyle.Default.CodeBackground;
			bool IsCodeBg(int x, int y)
			{
				var bg = buf.GetCell(x, y).Background;
				return bg.R == codeBg.R && bg.G == codeBg.G && bg.B == codeBg.B;
			}

			// Every row carrying code text must be shaded all the way to the right edge.
			int checkedRows = 0;
			for (int y = 0; y < 12; y++)
			{
				var row = string.Concat(Enumerable.Range(0, 38).Select(x => buf.GetCell(x, y).Character.ToString()));
				if (row.Trim().Length == 0 || !IsCodeBg(1, y)) continue;

				checkedRows++;
				int shaded = Enumerable.Range(0, 38).Count(x => IsCodeBg(x, y));
				Assert.True(shaded >= 36,
					$"row {y} (\"{row.TrimEnd()}\") shaded only {shaded}/38 cells -- background stops early");
			}

			Assert.True(checkedRows >= 2, $"expected the long line to wrap into 2+ code rows, saw {checkedRows}");
		}

		[Theory]
		[InlineData("")]        // plain fence
		[InlineData("csharp")]  // syntax-highlighted fence (a separate emit path)
		public void CodeBlock_EveryLine_FillsFullWidth_NotJustTheLast(string lang)
		{
			// THE REAL THING, and the case every earlier test missed: a MULTI-LINE code block whose
			// lines are SHORTER than the control, so none of them wrap. Each line emits its own
			// [fillwidth], but the whole block is parsed in ONE pass -- so only the final line used to
			// end up flagged, and the ones above it painted an unshaded tail. Run over both emit paths
			// (plain and highlighted) because they build the markup differently.
			//
			// Deliberately unequal line lengths: if a row were somehow inheriting a neighbour's cell,
			// equal lengths would hide it.
			var codeLines = new[] { "alpha = 1234567;", "beta = 22;", "gamma = 333333;" };
			var md = "```" + lang + "\n" + string.Join("\n", codeLines) + "\n```";

			var ctrl = SharpConsoleUI.Builders.Controls.Markdown(md).Build();
			const int W = 48;
			var buf = new CharacterBuffer(W + 2, 16);
			var bounds = new LayoutRect(0, 0, W, 14);
			ctrl.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);

			var codeBg = MarkdownStyle.Default.CodeBackground;
			bool IsCodeBg(int x, int y)
			{
				var bg = buf.GetCell(x, y).Background;
				return bg.R == codeBg.R && bg.G == codeBg.G && bg.B == codeBg.B;
			}

			int matched = 0;
			foreach (var code in codeLines)
			{
				int row = -1;
				for (int y = 0; y < 14 && row < 0; y++)
				{
					var text = string.Concat(Enumerable.Range(0, W).Select(x => buf.GetCell(x, y).Character.ToString()));
					if (text.Contains(code)) row = y;
				}
				Assert.True(row >= 0, $"code line \"{code}\" not found in the painted output");

				matched++;
				int shaded = Enumerable.Range(0, W).Count(x => IsCodeBg(x, row));
				Assert.True(shaded >= W - 1,
					$"line \"{code}\" (row {row}) shaded only {shaded}/{W} cells — background stops at the text");
			}

			Assert.Equal(codeLines.Length, matched);
		}
	}
}
