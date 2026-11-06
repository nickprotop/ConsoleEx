// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Html;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Whitespace a page asks for in pixels, spent in terminal rows.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A page says <c>margin: 20px</c> and means a small gap on a screen
	/// hundreds of pixels tall. Turned into rows, twenty pixels becomes several
	/// lines, and a modern page nests dozens of wrapper elements each asking for
	/// their own. On a real article this left thirty six percent of the page
	/// blank, with runs of fourteen empty rows between paragraphs: the reader
	/// scrolls through emptiness looking for the text.
	/// </para>
	/// <para>
	/// Worse, a wrapper that renders nothing at all still spent its margin. A
	/// layout div exists to position its children on a screen; in a terminal it
	/// positions nothing, and charging rows for it is paying for a service not
	/// rendered.
	/// </para>
	/// </remarks>
	public class HtmlBlankLineCollapseTests
	{
		private static LayoutResult Layout(string html, int width = 80) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray()).Trim();

		private static int BlankCount(LayoutResult result) =>
			result.Lines.Count(l => TextOf(l).Length == 0);

		/// <summary>The longest run of consecutive blank lines.</summary>
		private static int LongestBlankRun(LayoutResult result)
		{
			int longest = 0, run = 0;

			foreach (var line in result.Lines)
			{
				if (TextOf(line).Length == 0)
				{
					run++;
					longest = Math.Max(longest, run);
				}
				else
				{
					run = 0;
				}
			}

			return longest;
		}

		[Fact]
		public void AWrapperThatRendersNothingCostsNoRows()
		{
			// The clearest case: a div with a margin and no content at all. It
			// positions nothing in a terminal, so it should take no space.
			var withWrapper = Layout("<div style=\"margin:20px\"></div><p>hello</p>");
			var without = Layout("<p>hello</p>");

			Assert.Equal(BlankCount(without), BlankCount(withWrapper));
		}

		[Fact]
		public void NestedEmptyWrappersDoNotAccumulate()
		{
			// How a real page is built: wrappers inside wrappers, each with its
			// own margin. Three of them cost seven blank lines for no content.
			var html = "<div style=\"margin:20px\"><div style=\"padding:20px\">"
				+ "<div style=\"margin:20px\"></div></div></div><p>hello</p>";

			var result = Layout(html);

			Assert.Equal(BlankCount(Layout("<p>hello</p>")), BlankCount(result));
		}

		[Fact]
		public void GapsBetweenParagraphsStayReadable()
		{
			// Whitespace is not the enemy; runs of it are. Two paragraphs may be
			// separated, but not by a screenful.
			var html = "<div style=\"margin:30px\"><p>first</p></div>"
				+ "<div style=\"margin:30px\"><p>second</p></div>";

			var result = Layout(html);

			Assert.True(LongestBlankRun(result) <= 2,
				$"a run of {LongestBlankRun(result)} blank lines separates two paragraphs");
		}

		[Fact]
		public void ParagraphsAreStillSeparated()
		{
			// The guard against over-correcting: text must not run together.
			var result = Layout("<p>first</p><p>second</p>");

			var lines = result.Lines.Select(TextOf).ToList();
			int first = lines.FindIndex(t => t.Contains("first"));
			int second = lines.FindIndex(t => t.Contains("second"));

			Assert.True(first >= 0 && second >= 0, "both paragraphs should render");
			Assert.True(second > first + 1,
				"the paragraphs ran together with no gap between them");
		}

		[Fact]
		public void TextInsideAWrapperStillRenders()
		{
			// The guard that matters most: collapsing the spacing of a wrapper
			// must never collapse the wrapper's content with it.
			var result = Layout("<div style=\"margin:20px\"><p>hello</p></div>");

			Assert.Contains(result.Lines, l => TextOf(l).Contains("hello"));
		}

		[Fact]
		public void ARealArticleIsMostlyText()
		{
			// The symptom as a reader meets it, stated as a proportion so it
			// holds for any page rather than one site's numbers. A page that is
			// more than half blank is one the reader scrolls through hunting for
			// the article.
			var paragraphs = string.Concat(Enumerable.Range(0, 10).Select(i =>
				$"<div class=\"w\" style=\"margin:20px\"><div style=\"padding:15px\">"
				+ $"<p>Paragraph number {i} with enough words in it to occupy a line.</p>"
				+ "</div></div>"));

			var result = Layout(paragraphs);
			int blank = BlankCount(result);

			// One blank line per paragraph is the healthy floor: ten paragraphs
			// separated by ten gaps. What this guards is that the dozens of
			// nested wrappers around them add nothing on top of that, and that no
			// run of blanks appears anywhere.
			Assert.True(blank <= 10, $"{blank} blank lines for 10 paragraphs");
			Assert.True(LongestBlankRun(result) <= 1,
				$"a run of {LongestBlankRun(result)} blank lines in an article");
		}
	}
}
