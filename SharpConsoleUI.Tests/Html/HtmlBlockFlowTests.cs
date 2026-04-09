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
	public class HtmlBlockFlowTests
	{
		private static LayoutLine[] Flow(string bodyContent, int maxWidth = 80)
		{
			var doc = HtmlTestHelpers.ParseHtml($"<html><body>{bodyContent}</body></html>");
			return HtmlBlockFlow.FlowBlocks(
				doc.Body!,
				maxWidth,
				Color.White,
				Color.Black);
		}

		[Fact]
		public void Paragraph_RendersWrappedText()
		{
			var lines = Flow("<p>Hello world this is a paragraph</p>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Hello world this is a paragraph", text);
		}

		[Fact]
		public void TwoParagraphs_HaveSpacingBetween()
		{
			var lines = Flow("<p>First</p><p>Second</p>", 80);

			// Find lines containing "First" and "Second"
			int firstY = -1;
			int secondY = -1;
			for (int i = 0; i < lines.Length; i++)
			{
				var text = HtmlTestHelpers.CellsToText(lines[i].Cells);
				if (text.Contains("First")) firstY = i;
				if (text.Contains("Second")) secondY = i;
			}

			Assert.True(firstY >= 0, "Should find 'First' line");
			Assert.True(secondY >= 0, "Should find 'Second' line");
			// There should be at least 1 empty line between them (blockSpacing=1 after first para)
			Assert.True(secondY - firstY > 1, $"Expected spacing between paragraphs, firstY={firstY}, secondY={secondY}");
		}

		[Fact]
		public void H1_RendersBoldAndUnderline()
		{
			var lines = Flow("<h1>Title</h1>", 80);

			// Find line containing "Title"
			LayoutLine? titleLine = null;
			foreach (var line in lines)
			{
				var text = HtmlTestHelpers.CellsToText(line.Cells);
				if (text.Contains("Title"))
				{
					titleLine = line;
					break;
				}
			}

			Assert.NotNull(titleLine);
			// Check that cells have Bold and Underline decorations
			var cells = titleLine.Value.Cells;
			Assert.True(cells.Length > 0);
			Assert.True(cells[0].Decorations.HasFlag(TextDecoration.Bold), "H1 should be bold");
			Assert.True(cells[0].Decorations.HasFlag(TextDecoration.Underline), "H1 should be underlined");
		}

		[Fact]
		public void H2_RendersBold()
		{
			var lines = Flow("<h2>Subtitle</h2>", 80);

			LayoutLine? headingLine = null;
			foreach (var line in lines)
			{
				var text = HtmlTestHelpers.CellsToText(line.Cells);
				if (text.Contains("Subtitle"))
				{
					headingLine = line;
					break;
				}
			}

			Assert.NotNull(headingLine);
			var cells = headingLine.Value.Cells;
			Assert.True(cells.Length > 0);
			Assert.True(cells[0].Decorations.HasFlag(TextDecoration.Bold), "H2 should be bold");
		}

		[Fact]
		public void Hr_RendersHorizontalLine()
		{
			var lines = Flow("<hr>", 40);

			// Find the hr line (non-empty line with HorizontalRuleChar)
			LayoutLine? hrLine = null;
			foreach (var line in lines)
			{
				if (line.Cells.Length > 0 && line.Cells[0].Character == new System.Text.Rune(HtmlConstants.HorizontalRuleChar))
				{
					hrLine = line;
					break;
				}
			}

			Assert.NotNull(hrLine);
			Assert.Equal(40, hrLine.Value.Cells.Length);
		}

		[Fact]
		public void UnorderedList_RendersBullets()
		{
			var lines = Flow("<ul><li>Apple</li><li>Banana</li></ul>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("•", text);
			Assert.Contains("Apple", text);
			Assert.Contains("Banana", text);
		}

		[Fact]
		public void OrderedList_RendersNumbers()
		{
			var lines = Flow("<ol><li>First</li><li>Second</li></ol>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("1.", text);
			Assert.Contains("2.", text);
			Assert.Contains("First", text);
			Assert.Contains("Second", text);
		}

		[Fact]
		public void NestedList_IncreasesIndent()
		{
			var lines = Flow("<ul><li>Outer<ul><li>Inner</li></ul></li></ul>", 80);

			int outerX = -1;
			int innerX = -1;
			foreach (var line in lines)
			{
				var text = HtmlTestHelpers.CellsToText(line.Cells);
				if (text.Contains("Outer") && outerX < 0) outerX = line.X;
				if (text.Contains("Inner") && innerX < 0) innerX = line.X;
			}

			Assert.True(outerX >= 0, "Should find Outer line");
			Assert.True(innerX >= 0, "Should find Inner line");
			Assert.True(innerX > outerX, $"Inner indent ({innerX}) should be greater than outer ({outerX})");
		}

		[Fact]
		public void Blockquote_IndentsWithBar()
		{
			var lines = Flow("<blockquote>Quoted text</blockquote>", 80);

			bool foundBar = false;
			foreach (var line in lines)
			{
				foreach (var cell in line.Cells)
				{
					if (cell.Character == new System.Text.Rune(HtmlConstants.BlockquoteBar))
					{
						foundBar = true;
						break;
					}
				}
				if (foundBar) break;
			}

			Assert.True(foundBar, "Blockquote should contain bar character");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Quoted text", text);
		}

		[Fact]
		public void Pre_PreservesWhitespace()
		{
			var lines = Flow("<pre>line1\nline2\n  indented</pre>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("line1", text);
			Assert.Contains("line2", text);
			Assert.Contains("  indented", text);
		}

		[Fact]
		public void Div_RendersAsBlock()
		{
			var lines = Flow("<div>Block content</div>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Block content", text);
		}

		[Fact]
		public void DisplayNone_SkipsElement()
		{
			var lines = Flow("<p>Visible</p><p style='display:none'>Hidden</p>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Visible", text);
			Assert.DoesNotContain("Hidden", text);
		}

		[Fact]
		public void Script_SkipsContent()
		{
			var lines = Flow("<p>Visible</p><script>alert('hi')</script>", 80);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Visible", text);
			Assert.DoesNotContain("alert", text);
		}

		[Fact]
		public void TextAlignCenter_SetsAlignment()
		{
			var lines = Flow("<p style='text-align:center'>Centered</p>", 80);

			LayoutLine? centeredLine = null;
			foreach (var line in lines)
			{
				var text = HtmlTestHelpers.CellsToText(line.Cells);
				if (text.Contains("Centered"))
				{
					centeredLine = line;
					break;
				}
			}

			Assert.NotNull(centeredLine);
			Assert.Equal(TextAlignment.Center, centeredLine.Value.Alignment);
		}

		[Fact]
		public void EmptyBody_ReturnsNoLines()
		{
			var lines = Flow("", 80);
			Assert.Empty(lines);
		}

		[Fact]
		public void MalformedHtml_DoesNotCrash()
		{
			// Unclosed tags, nested improperly
			var lines = Flow("<p>Hello <b>world<p>Another</b> paragraph", 80);
			// Should not throw — just verify it returns something
			Assert.NotNull(lines);
		}
	}
}
