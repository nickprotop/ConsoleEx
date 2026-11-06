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
	/// A grid too narrow to hold its columns stacks them instead of shredding
	/// the text into a vertical column of single characters.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A page designs its grid for a screen hundreds of pixels wide. A terminal
	/// has about a hundred columns in total, so a desktop grid of twelve or
	/// fifteen tracks leaves each one a handful of characters. The widths were
	/// floored at one column and the text wrapped inside that, which prints one
	/// letter per line: a page of vertical confetti where an article should be.
	/// </para>
	/// <para>
	/// The honest answer in a terminal is that such a grid is not a grid. A
	/// screen that cannot give a column enough room to hold a word is a screen
	/// where the columns belong underneath each other, which is what a browser
	/// does at its narrowest breakpoint.
	/// </para>
	/// </remarks>
	public class HtmlGridCollapseTests
	{
		private const int Width = 120;

		private static LayoutResult Layout(string html, int width = Width) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray()).Trim();

		/// <summary>
		/// The longest run of consecutive lines holding exactly one character.
		/// </summary>
		/// <remarks>
		/// This is what the defect looks like from the outside, and it stays true
		/// however the fix is implemented: it does not assert a column count, a
		/// track width, or any particular page's numbers.
		/// </remarks>
		private static int LongestSingleCharacterRun(LayoutResult result)
		{
			int longest = 0, run = 0;

			foreach (var line in result.Lines)
			{
				if (TextOf(line).Length == 1)
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

		private static string Grid(string columns, params string[] cells) =>
			$"<div style=\"display:grid;grid-template-columns:{columns}\">"
			+ string.Concat(cells.Select(c => $"<div>{c}</div>"))
			+ "</div>";

		[Theory]
		[InlineData("repeat(15, 1fr)", "fifteen equal tracks")]
		[InlineData("repeat(12, 1fr)", "twelve equal tracks")]
		[InlineData("10px 1fr", "a sliver beside a track")]
		[InlineData("25px repeat(2, 1fr)", "a narrow fixed track")]
		public void AGridTooNarrowForItsColumnsDoesNotShredItsText(string columns, string what)
		{
			// The defect: a track narrower than a word wrapped one character per
			// line. Three in a row is already unmistakable on screen, and no
			// ordinary prose produces it.
			var result = Layout(Grid(columns,
				"Economy four minutes ago", "Politics", "Sport"));

			int run = LongestSingleCharacterRun(result);

			Assert.True(run < 3,
				$"{what}: text was shredded into {run} consecutive single-character lines");
		}

		[Fact]
		public void TextInACollapsedGridSurvivesWhole()
		{
			// Collapsing must not lose anything: every word stays, and stays
			// readable on one line rather than spread down the screen.
			var result = Layout(Grid("repeat(15, 1fr)",
				"Economy four minutes ago", "Politics", "Sport"));

			var text = string.Join(" ", result.Lines.Select(TextOf));

			Assert.Contains("Economy", text);
			Assert.Contains("Politics", text);
			Assert.Contains("Sport", text);
		}

		[Fact]
		public void AGridWideEnoughForItsColumnsStillLaysOutSideBySide()
		{
			// The guard: collapsing is for a grid that cannot fit, not for every
			// grid. Two roomy columns must still share a line, or this fix would
			// flatten every grid on every page.
			var result = Layout(Grid("1fr 1fr", "Left", "Right"));

			Assert.Contains(result.Lines, l =>
			{
				var t = TextOf(l);
				return t.Contains("Left") && t.Contains("Right");
			});
		}

		[Fact]
		public void AGridWhoseTracksCannotHoldAWordStacksItsCells()
		{
			// What collapsing means: each cell gets the full width and its own
			// line, the way a browser stacks them at its narrowest breakpoint.
			//
			// Twenty tracks, not fifteen: at this width fifteen still leaves eight
			// columns each, which wraps per word and stays readable, so it is
			// correctly left alone. The rule is about a track too narrow for a
			// word, not about a track count.
			var result = Layout(Grid("repeat(20, 1fr)", "Alpha", "Beta"));

			// Asserted on position in the line list rather than on Y: a line's Y is
			// reassigned by whichever context finally places it, so it says
			// nothing here, while the order of the lines is what a reader sees.
			var lines = result.Lines.ToList();
			int alpha = lines.FindIndex(l => TextOf(l).Contains("Alpha"));
			int beta = lines.FindIndex(l => TextOf(l).Contains("Beta"));

			Assert.True(alpha >= 0, "the first cell's text is missing");
			Assert.True(beta >= 0, "the second cell's text is missing");
			Assert.NotEqual(alpha, beta);

			// And neither shares a line with the other: stacked, not merged.
			Assert.DoesNotContain(lines, l =>
			{
				var t = TextOf(l);
				return t.Contains("Alpha") && t.Contains("Beta");
			});
		}
	}
}
