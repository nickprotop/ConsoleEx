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
	public class HtmlTableLayoutTests
	{
		private static LayoutLine[] LayoutTable(string tableHtml, int maxWidth = 60)
		{
			var doc = HtmlTestHelpers.ParseHtml($"<html><body>{tableHtml}</body></html>");
			var table = doc.QuerySelector("table")!;
			return HtmlTableLayout.LayoutTable(table, maxWidth, Color.White, Color.Black);
		}

		[Fact]
		public void Simple2x2_RendersWithBorders()
		{
			var lines = LayoutTable(
				"<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>");

			Assert.True(lines.Length > 0, "Should produce lines");

			var text = HtmlTestHelpers.LinesToText(lines);

			// Should contain box-drawing characters
			Assert.Contains("┌", text);
			Assert.Contains("┐", text);
			Assert.Contains("└", text);
			Assert.Contains("┘", text);
			Assert.Contains("│", text);
			Assert.Contains("─", text);

			// Should contain cell content
			Assert.Contains("A", text);
			Assert.Contains("B", text);
			Assert.Contains("C", text);
			Assert.Contains("D", text);
		}

		[Fact]
		public void TableWithTh_RendersBoldHeaders()
		{
			var lines = LayoutTable(
				"<table><tr><th>Name</th><th>Age</th></tr><tr><td>Alice</td><td>30</td></tr></table>");

			// Find line containing "Name"
			LayoutLine? headerLine = null;
			foreach (var line in lines)
			{
				var text = HtmlTestHelpers.CellsToText(line.Cells);
				if (text.Contains("Name"))
				{
					headerLine = line;
					break;
				}
			}

			Assert.NotNull(headerLine);

			// Find the 'N' cell and check for Bold
			int nameIdx = -1;
			for (int i = 0; i < headerLine.Value.Cells.Length; i++)
			{
				if (headerLine.Value.Cells[i].Character == new System.Text.Rune('N'))
				{
					nameIdx = i;
					break;
				}
			}

			Assert.True(nameIdx >= 0, "Should find 'N' character");
			Assert.True(headerLine.Value.Cells[nameIdx].Decorations.HasFlag(TextDecoration.Bold),
				"Header cells should have Bold decoration");
		}

		[Fact]
		public void Table_DistributesWidthProportionally()
		{
			var lines = LayoutTable(
				"<table><tr><td>Short</td><td>A much longer cell content here</td></tr></table>",
				maxWidth: 50);

			// No line should exceed maxWidth
			foreach (var line in lines)
			{
				Assert.True(line.Cells.Length <= 50,
					$"Line width {line.Cells.Length} exceeds maxWidth 50");
			}
		}

		[Fact]
		public void Table_WrapsLongContent()
		{
			var lines = LayoutTable(
				"<table><tr><td>ThisIsAVeryLongWordThatShouldBeWrapped</td><td>Short</td></tr></table>",
				maxWidth: 30);

			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Short", text);

			// The table should have rendered (more than just borders)
			// Count content rows (lines with │ but not ─)
			int contentLines = 0;
			foreach (var line in lines)
			{
				var lineText = HtmlTestHelpers.CellsToText(line.Cells);
				if (lineText.Contains("│") && !lineText.Contains("─"))
					contentLines++;
			}

			// With wrapping, there should be multiple content lines for one row
			Assert.True(contentLines >= 2,
				$"Expected wrapped content to produce multiple lines, got {contentLines}");
		}

		[Fact]
		public void EmptyTable_ReturnsNoLines()
		{
			var lines = LayoutTable("<table></table>");
			Assert.Empty(lines);
		}

		[Fact]
		public void CellContent_CollapsesInteriorWhitespace_NoControlCharsInCells()
		{
			// td with inline children split across source lines — raw IText nodes contain \n
			var lines = LayoutTable(
				"<table><tr><td>Foo\n<b>Bar</b>\tBaz</td><td>Q</td></tr></table>",
				maxWidth: 60);

			// No cell in any row may contain a control character. A single stray \n or \t in a
			// Cell.Character corrupts the terminal output stream when the buffer is flushed.
			foreach (var line in lines)
			{
				foreach (var cell in line.Cells)
				{
					int v = cell.Character.Value;
					Assert.True(v >= 0x20 || v == 0, $"cell contains control char U+{v:X4}");
				}
			}

			// Row lines must have the same cell count as border lines — no drift from zero-width runes.
			int expectedWidth = lines[0].Cells.Length;
			foreach (var line in lines)
				Assert.Equal(expectedWidth, line.Cells.Length);

			// Content is collapsed to "Foo Bar Baz"
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Foo Bar Baz", text);
		}

		[Fact]
		public void NestedTable_DoesNotFlattenChildRowsIntoParent()
		{
			// Parent has 1 row with 2 cells; nested table inside the first cell has 2 rows × 1 cell.
			// Before the fix, QuerySelectorAll("tr") returned all 3 <tr>s with inconsistent
			// column counts, corrupting the parent table's layout.
			var html =
				"<table>" +
					"<tr><td>Outer<table><tr><td>Inner1</td></tr><tr><td>Inner2</td></tr></table></td><td>Right</td></tr>" +
				"</table>";
			var lines = LayoutTable(html, maxWidth: 60);

			// All lines should have identical width (no column-count mismatch between rows).
			int expectedWidth = lines[0].Cells.Length;
			foreach (var line in lines)
				Assert.Equal(expectedWidth, line.Cells.Length);

			// "Right" (the parent's second cell content) must be preserved, not overwritten
			// by flattened inner rows.
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Right", text);
			Assert.Contains("Outer", text);
		}

		[Fact]
		public void TheadTbodyTfoot_RowsAreCollected()
		{
			var lines = LayoutTable(
				"<table>" +
					"<thead><tr><th>H</th></tr></thead>" +
					"<tbody><tr><td>B</td></tr></tbody>" +
					"<tfoot><tr><td>F</td></tr></tfoot>" +
				"</table>");

			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("H", text);
			Assert.Contains("B", text);
			Assert.Contains("F", text);
		}

		[Fact]
		public void Br_InsideCell_BecomesSpace()
		{
			var lines = LayoutTable("<table><tr><td>Foo<br>Bar</td></tr></table>");
			var text = HtmlTestHelpers.LinesToText(lines);
			// <br> inside a table cell is rendered as a space separator — the cell is on one line
			// with "Foo Bar" (we don't currently wrap on <br> inside cells).
			Assert.Contains("Foo Bar", text);
		}
	}
}
