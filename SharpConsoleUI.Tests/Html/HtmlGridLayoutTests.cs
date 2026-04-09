// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	public class HtmlGridLayoutTests
	{
		private static LayoutLine[] LayoutGrid(string html, int maxWidth = 60)
		{
			var doc = HtmlTestHelpers.ParseHtml(html);
			var gridEl = doc.QuerySelector("[style*='display']")
				?? doc.QuerySelector("div")!;

			var style = HtmlStyleResolver.Resolve(gridEl, Color.White, Color.Black);
			var templateCols = style.GridTemplateColumns ?? "1fr 1fr";
			var gap = style.GridGap;

			return HtmlGridLayout.LayoutGrid(gridEl, maxWidth, templateCols, gap, Color.White, Color.Black);
		}

		[Fact]
		public void TwoEqualColumns_BothOnSameLine()
		{
			var html = @"<html><body>
				<div style='display:grid; grid-template-columns: 1fr 1fr'>
					<div>Left</div>
					<div>Right</div>
				</div>
			</body></html>";

			var lines = LayoutGrid(html, 40);
			var text = HtmlTestHelpers.LinesToText(lines);

			Assert.Contains("Left", text);
			Assert.Contains("Right", text);

			// Both should appear on the same line (line 0 content)
			bool foundBothOnSameLine = false;
			foreach (var line in lines)
			{
				var lineText = HtmlTestHelpers.CellsToText(line.Cells);
				if (lineText.Contains("Left") && lineText.Contains("Right"))
				{
					foundBothOnSameLine = true;
					break;
				}
			}

			Assert.True(foundBothOnSameLine, "Both 'Left' and 'Right' should be on the same line");
		}

		[Fact]
		public void FixedPlusFlex_BothCellsRender()
		{
			var html = @"<html><body>
				<div style='display:grid; grid-template-columns: 80px 1fr'>
					<div>Fixed</div>
					<div>Flex</div>
				</div>
			</body></html>";

			var lines = LayoutGrid(html, 60);
			var text = HtmlTestHelpers.LinesToText(lines);

			Assert.Contains("Fixed", text);
			Assert.Contains("Flex", text);
		}

		[Fact]
		public void WithGap_RightStartsFurtherRight()
		{
			var htmlNoGap = @"<html><body>
				<div style='display:grid; grid-template-columns: 1fr 1fr'>
					<div>Left</div>
					<div>Right</div>
				</div>
			</body></html>";

			var htmlWithGap = @"<html><body>
				<div style='display:grid; grid-template-columns: 1fr 1fr; gap: 16px'>
					<div>Left</div>
					<div>Right</div>
				</div>
			</body></html>";

			var linesNoGap = LayoutGrid(htmlNoGap, 40);
			var linesWithGap = LayoutGrid(htmlWithGap, 40);

			// Find position of "Right" in each
			int rightPosNoGap = FindTextPosition(linesNoGap, "Right");
			int rightPosWithGap = FindTextPosition(linesWithGap, "Right");

			Assert.True(rightPosNoGap >= 0, "Should find 'Right' without gap");
			Assert.True(rightPosWithGap >= 0, "Should find 'Right' with gap");
			Assert.True(rightPosWithGap > rightPosNoGap,
				$"With gap, 'Right' should start further right ({rightPosWithGap} vs {rightPosNoGap})");
		}

		[Fact]
		public void MultiRowGrid_AllItemsRender()
		{
			var html = @"<html><body>
				<div style='display:grid; grid-template-columns: 1fr 1fr'>
					<div>A</div>
					<div>B</div>
					<div>C</div>
					<div>D</div>
				</div>
			</body></html>";

			var lines = LayoutGrid(html, 40);
			var text = HtmlTestHelpers.LinesToText(lines);

			Assert.Contains("A", text);
			Assert.Contains("B", text);
			Assert.Contains("C", text);
			Assert.Contains("D", text);
		}

		[Fact]
		public void GridAdaptsToWidth()
		{
			var html = @"<html><body>
				<div style='display:grid; grid-template-columns: 1fr 1fr'>
					<div>Hello</div>
					<div>World</div>
				</div>
			</body></html>";

			var linesWide = LayoutGrid(html, 80);
			var linesNarrow = LayoutGrid(html, 20);

			// Both should render content
			var textWide = HtmlTestHelpers.LinesToText(linesWide);
			var textNarrow = HtmlTestHelpers.LinesToText(linesNarrow);

			Assert.Contains("Hello", textWide);
			Assert.Contains("World", textWide);
			Assert.Contains("Hello", textNarrow);
			Assert.Contains("World", textNarrow);

			// Narrow lines should be shorter or equal
			int maxWideWidth = 0;
			int maxNarrowWidth = 0;
			foreach (var line in linesWide)
				if (line.Cells.Length > maxWideWidth) maxWideWidth = line.Cells.Length;
			foreach (var line in linesNarrow)
				if (line.Cells.Length > maxNarrowWidth) maxNarrowWidth = line.Cells.Length;

			Assert.True(maxNarrowWidth <= maxWideWidth,
				$"Narrow ({maxNarrowWidth}) should not exceed wide ({maxWideWidth})");
		}

		[Fact]
		public void ParseColumnDefs_FrUnits()
		{
			var defs = HtmlGridLayout.ParseColumnDefs("1fr 2fr");
			Assert.Equal(2, defs.Count);
			Assert.Equal(ColumnType.Fr, defs[0].Type);
			Assert.Equal(1.0, defs[0].Value);
			Assert.Equal(ColumnType.Fr, defs[1].Type);
			Assert.Equal(2.0, defs[1].Value);
		}

		[Fact]
		public void ParseColumnDefs_PxUnit()
		{
			var defs = HtmlGridLayout.ParseColumnDefs("80px 1fr");
			Assert.Equal(2, defs.Count);
			Assert.Equal(ColumnType.Fixed, defs[0].Type);
			Assert.Equal(10.0, defs[0].Value); // 80/8 = 10
			Assert.Equal(ColumnType.Fr, defs[1].Type);
		}

		[Fact]
		public void ParseColumnDefs_EmUnit()
		{
			var defs = HtmlGridLayout.ParseColumnDefs("5em");
			Assert.Single(defs);
			Assert.Equal(ColumnType.Fixed, defs[0].Type);
			Assert.Equal(10.0, defs[0].Value); // 5 * 2 = 10
		}

		private static int FindTextPosition(LayoutLine[] lines, string text)
		{
			foreach (var line in lines)
			{
				var lineText = HtmlTestHelpers.CellsToText(line.Cells);
				int idx = lineText.IndexOf(text, System.StringComparison.Ordinal);
				if (idx >= 0)
					return idx;
			}
			return -1;
		}
	}
}
