// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Html;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Table cells carry formatted content, not flattened text.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A cell used to hold a <c>string</c>, produced by walking the element and
	/// concatenating every text node. Everything else about the content was
	/// thrown away at that point: a link lost its colour and its clickable
	/// region, bold lost its weight, and block children ran together with no
	/// separator because only their characters survived.
	/// </para>
	/// <para>
	/// It shows worst on a page built entirely out of tables. Hacker News
	/// rendered as an unbroken wall - "(github.com)119 points by x | 43
	/// comments2.Claude Code sends..." - with not one of its links reachable.
	/// </para>
	/// </remarks>
	public class HtmlTableRichCellTests
	{
		private static LayoutResult Layout(string html, int width = 80) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				linkColor: Color.Blue);

		private static int CellsColoured(LayoutResult r, Color colour) =>
			r.Lines.Sum(l => l.Cells.Count(c => c.Foreground == colour));

		private static int LinkRegions(LayoutResult r) =>
			r.Lines.Sum(l => l.Links?.Length ?? 0);

		[Fact]
		public void ALinkInACellIsColouredDifferentlyFromPlainText()
		{
			// Asserted as "not the body colour" rather than as an exact value: the
			// link colour is held to the contrast floor against the surface it
			// lands on, so the painted colour is a readable version of the one
			// requested rather than the literal value.
			var result = Layout(
				"<table><tr><td>PLAIN</td><td><a href=\"https://a.com\">HERE</a></td></tr></table>");

			var linkColours = result.Lines
				.SelectMany(l => l.Cells)
				.Where(c => c.Character.Value is 'H' or 'E' or 'R')
				.Select(c => c.Foreground)
				.Distinct()
				.ToList();

			Assert.NotEmpty(linkColours);
			Assert.DoesNotContain(Color.White, linkColours);
		}

		[Fact]
		public void ALinkInACellIsClickable()
		{
			// Colour alone is cosmetic. The region is what makes the link
			// reachable with the mouse and the keyboard, and a table used to
			// produce none at all.
			var result = Layout("<table><tr><td><a href=\"https://a.com\">HERE</a></td></tr></table>");

			Assert.Equal(1, LinkRegions(result));
		}

		[Fact]
		public void EachCellKeepsItsOwnLink()
		{
			// Two links in one row must stay two separate targets, each pointing
			// where its own cell says.
			var result = Layout(
				"<table><tr>"
					+ "<td><a href=\"https://one.com\">ONE</a></td>"
					+ "<td><a href=\"https://two.com\">TWO</a></td>"
					+ "</tr></table>");

			Assert.Equal(2, LinkRegions(result));

			var urls = result.Lines
				.Where(l => l.Links != null)
				.SelectMany(l => l.Links!)
				.Select(r => r.Url)
				.ToList();

			Assert.Contains("https://one.com", urls);
			Assert.Contains("https://two.com", urls);
		}

		[Fact]
		public void ALinkInANestedTableIsStillReachable()
		{
			// The shape that matters: Hacker News puts its story list in a table
			// inside a cell of the page's outer table.
			var result = Layout(
				"<table><tr><td>"
					+ "<table><tr><td><a href=\"https://deep.com\">DEEP</a></td></tr></table>"
					+ "</td></tr></table>");

			Assert.True(LinkRegions(result) >= 1, "a link in a nested table was unreachable");
		}

		[Fact]
		public void BoldInACellKeepsItsWeight()
		{
			var result = Layout("<table><tr><td><b>STRONG</b></td></tr></table>");

			bool anyBold = result.Lines.Any(l =>
				l.Cells.Any(c => c.Decorations.HasFlag(TextDecoration.Bold)
					&& c.Character.Value is >= 'A' and <= 'Z'));

			Assert.True(anyBold, "bold text in a cell lost its weight");
		}

		[Fact]
		public void BlockChildrenInACellDoNotRunTogether()
		{
			// The mesh. Two paragraphs in one cell were concatenated into
			// "FIRSTSECOND" because only their characters survived.
			var result = Layout(
				"<table><tr><td><p>FIRST</p><p>SECOND</p></td></tr></table>", width: 40);

			var text = HtmlTestHelpers.LinesToText(result.Lines);

			Assert.DoesNotContain("FIRSTSECOND", text);
			Assert.Contains("FIRST", text);
			Assert.Contains("SECOND", text);
		}

		[Fact]
		public void TheTableStillDrawsItsBordersAndAligns()
		{
			// The rewrite must not cost the table its shape: every rendered row
			// is the same width, and the borders are still there.
			var result = Layout(
				"<table><tr><th>Head</th><th>Other</th></tr>"
					+ "<tr><td>a</td><td>b</td></tr></table>", width: 40);

			Assert.NotEmpty(result.Lines);

			// Blank spacing lines around the table are not rows; only the rows
			// themselves have to agree on width.
			var widths = result.Lines
				.Where(l => l.Cells.Length > 0)
				.Select(l => l.Cells.Length).Distinct().ToList();
			Assert.True(widths.Count == 1,
				"table rows came out at different widths: " + string.Join(", ", widths));

			var text = HtmlTestHelpers.LinesToText(result.Lines);
			Assert.Contains("┌", text); // top-left corner
			Assert.Contains("│", text); // vertical rule
			Assert.Contains("Head", text);
		}

		[Fact]
		public void LongCellTextStillWraps()
		{
			// Wrapping moved from strings to cells; it still has to happen, and
			// the row must stay rectangular when it does.
			var result = Layout(
				"<table><tr><td>one two three four five six seven eight nine ten</td></tr></table>",
				width: 30);

			Assert.True(result.Lines.Length > 3, "long cell content did not wrap onto more rows");
			Assert.True(result.Lines
					.Where(l => l.Cells.Length > 0)
					.Select(l => l.Cells.Length).Distinct().Count() == 1,
				"wrapped rows were not all the same width");
		}
	}
}
