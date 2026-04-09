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
	public class HtmlInlineFlowTests
	{
		private static List<LayoutLine> Flow(string bodyContent, int maxWidth = 80)
		{
			var doc = HtmlTestHelpers.ParseHtml($"<html><body>{bodyContent}</body></html>");
			return HtmlInlineFlow.FlowInline(
				doc.Body!.ChildNodes,
				maxWidth,
				Color.White,
				Color.Black);
		}

		[Fact]
		public void PlainText_SingleLineOfCells()
		{
			var lines = Flow("Hello world");
			Assert.Single(lines);
			var text = HtmlTestHelpers.CellsToText(lines[0].Cells);
			Assert.Equal("Hello world", text);
		}

		[Fact]
		public void BoldText_HasBoldDecoration()
		{
			var lines = Flow("<b>bold</b>");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			var text = HtmlTestHelpers.CellsToText(cells);
			Assert.Equal("bold", text);
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Bold));
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 3, TextDecoration.Bold));
		}

		[Fact]
		public void ItalicText_HasItalicDecoration()
		{
			var lines = Flow("<i>italic</i>");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Italic));
		}

		[Fact]
		public void NestedBoldItalic_HasBothDecorations()
		{
			var lines = Flow("<b><i>both</i></b>");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Bold));
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Italic));
		}

		[Fact]
		public void Link_HasUnderlineAndLinkColor_TracksLinkRegion()
		{
			var lines = Flow("<a href=\"https://example.com\">click here</a>");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			var text = HtmlTestHelpers.CellsToText(cells);
			Assert.Equal("click here", text);

			// Should have underline decoration
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Underline));

			// Should have link color foreground
			Assert.Equal(HtmlConstants.DefaultLinkColor, cells[0].Foreground);

			// Should have link region
			var links = HtmlTestHelpers.GetAllLinks(lines.ToArray());
			Assert.Single(links);
			Assert.Equal("https://example.com", links[0].Url);
		}

		[Fact]
		public void InlineCode_HasCodeBackground()
		{
			var lines = Flow("<code>foo()</code>");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			Assert.Equal(HtmlConstants.DefaultCodeBackground, cells[0].Background);
			Assert.Equal(HtmlConstants.DefaultCodeBackground, cells[4].Background);
		}

		[Fact]
		public void ImageAltText_RendersWithDimItalic()
		{
			var lines = Flow("<img alt=\"Logo\" />");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			var text = HtmlTestHelpers.CellsToText(cells);
			Assert.Equal("[Logo]", text);
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Dim));
			Assert.True(HtmlTestHelpers.HasDecoration(cells, 0, TextDecoration.Italic));
		}

		[Fact]
		public void LongText_WrapsAtMaxWidth()
		{
			// 20-char width, text that must wrap
			var lines = Flow("Hello world this is a longer sentence", 20);
			Assert.True(lines.Count > 1, "Expected multiple lines");
			foreach (var line in lines)
				Assert.True(line.Cells.Length <= 20, $"Line exceeded maxWidth: {line.Cells.Length}");

			var fullText = HtmlTestHelpers.LinesToText(lines.ToArray());
			// All words should be present
			Assert.Contains("Hello", fullText);
			Assert.Contains("sentence", fullText);
		}

		[Fact]
		public void MixedInlineElements_CorrectOrderAndDecorations()
		{
			var lines = Flow("normal <b>bold</b> <i>italic</i> end");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			var text = HtmlTestHelpers.CellsToText(cells);
			Assert.Equal("normal bold italic end", text);

			// 'b' in "bold" is at index 7
			int bIdx = HtmlTestHelpers.FindChar(cells, 'b');
			Assert.True(HtmlTestHelpers.HasDecoration(cells, bIdx, TextDecoration.Bold));

			// First 'i' in "italic"
			// Find the 'i' that has italic decoration
			int iIdx = -1;
			for (int j = 0; j < cells.Length; j++)
			{
				if (cells[j].Character == new System.Text.Rune('i') &&
				    cells[j].Decorations.HasFlag(TextDecoration.Italic))
				{
					iIdx = j;
					break;
				}
			}
			Assert.True(iIdx >= 0, "Should find italic 'i'");

			// 'e' in "end" should have no decorations
			int eIdx = text.LastIndexOf('e');
			Assert.Equal(TextDecoration.None, cells[eIdx].Decorations);
		}

		[Fact]
		public void Strikethrough_HasStrikethroughDecoration()
		{
			var lines = Flow("<s>deleted</s>");
			Assert.Single(lines);
			Assert.True(HtmlTestHelpers.HasDecoration(lines[0].Cells, 0, TextDecoration.Strikethrough));
		}

		[Fact]
		public void Underline_HasUnderlineDecoration()
		{
			var lines = Flow("<u>underlined</u>");
			Assert.Single(lines);
			Assert.True(HtmlTestHelpers.HasDecoration(lines[0].Cells, 0, TextDecoration.Underline));
		}

		[Fact]
		public void InlineColorStyle_CorrectForeground()
		{
			var lines = Flow("<span style=\"color: rgb(255, 0, 0)\">red text</span>");
			Assert.Single(lines);
			var cells = lines[0].Cells;
			Assert.Equal(new Color(255, 0, 0), cells[0].Foreground);
		}

		[Fact]
		public void LinkWrappingAcrossLines_LinkRegionPerLine()
		{
			// Link text is 20 chars, width is 10 => should wrap
			var lines = Flow("<a href=\"https://x.com\">click this long link</a>", 10);
			Assert.True(lines.Count >= 2, "Link should wrap across lines");

			var allLinks = HtmlTestHelpers.GetAllLinks(lines.ToArray());
			Assert.True(allLinks.Count >= 2, $"Expected link regions on multiple lines, got {allLinks.Count}");

			foreach (var link in allLinks)
				Assert.Equal("https://x.com", link.Url);
		}
	}
}
