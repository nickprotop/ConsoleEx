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
	/// A table's rules meet only where a column boundary actually exists.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A cell that spans the width of the table has no divider inside it, and the
	/// content row correctly leaves one out. The horizontal rules above and below
	/// it were drawn from the column widths alone, with a cross at every boundary
	/// regardless of what the neighbouring rows contained, so each rule grew a
	/// junction pointing into a cell that no vertical rule ever reached.
	/// </para>
	/// <para>
	/// The result reads as a broken frame: stubs of line hanging in open space,
	/// which is exactly where the eye goes. An encyclopedia's infobox is built
	/// this way - a full-width heading over a two-column body - so it shows on
	/// the first table most pages display.
	/// </para>
	/// </remarks>
	public class HtmlTableSpanBorderTests
	{
		private const char Cross = '┼';
		private const char TopTee = '┬';
		private const char BottomTee = '┴';
		private const char Vertical = '│';

		private static LayoutResult Layout(string html, int width = 80) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray());

		/// <summary>The interior column boundaries of a row, by cell index.</summary>
		private static int[] DividersOf(LayoutLine line)
		{
			var text = TextOf(line);
			return Enumerable.Range(1, Math.Max(0, text.Length - 2))
				.Where(i => text[i] == Vertical)
				.ToArray();
		}

		private static int[] JunctionsOf(LayoutLine line)
		{
			var text = TextOf(line);
			return Enumerable.Range(0, text.Length)
				.Where(i => text[i] == Cross || text[i] == TopTee || text[i] == BottomTee)
				.ToArray();
		}

		private static bool IsRule(LayoutLine line) =>
			TextOf(line).Contains('─');

		/// <summary>An infobox: a full-width heading over a two-column body.</summary>
		private const string Infobox =
			"<table>"
			+ "<tr><th colspan=\"2\">Binomial name</th></tr>"
			+ "<tr><td>Kingdom:</td><td>Animalia</td></tr>"
			+ "<tr><td>Phylum:</td><td>Chordata</td></tr>"
			+ "<tr><td colspan=\"2\">Synonyms</td></tr>"
			+ "</table>";

		[Fact]
		public void ARuleCarriesNoJunctionWhereNeitherNeighbourIsDivided()
		{
			// The defect, stated as the rule it breaks: a junction is where two
			// vertical rules meet, so one may stand only where a row above or
			// below actually draws a divider at that column.
			var result = Layout(Infobox);
			var lines = result.Lines.ToList();

			for (int i = 0; i < lines.Count; i++)
			{
				if (!IsRule(lines[i]))
					continue;

				var above = i > 0 && !IsRule(lines[i - 1]) ? DividersOf(lines[i - 1]) : Array.Empty<int>();
				var below = i + 1 < lines.Count && !IsRule(lines[i + 1]) ? DividersOf(lines[i + 1]) : Array.Empty<int>();

				foreach (var x in JunctionsOf(lines[i]))
				{
					Assert.True(above.Contains(x) || below.Contains(x),
						$"line {i}: a junction at column {x} meets no divider above or below");
				}
			}
		}

		[Fact]
		public void ASpanningRowStillHasNoDividerInsideIt()
		{
			// The guard for the half that already worked: the content row of a
			// spanning cell must stay undivided, or a fix to the rules could
			// "agree" with the rows by breaking them instead.
			var result = Layout(Infobox);

			var heading = result.Lines.First(l => TextOf(l).Contains("Binomial name"));

			Assert.Empty(DividersOf(heading));
		}

		[Fact]
		public void AnOrdinaryTableKeepsEveryJunction()
		{
			// The guard that matters most: a table with no spans at all must be
			// drawn exactly as it always was, junctions and all.
			var result = Layout(
				"<table>"
				+ "<tr><th>A</th><th>B</th></tr>"
				+ "<tr><td>one</td><td>two</td></tr>"
				+ "</table>");

			var lines = result.Lines.ToList();
			var rules = lines.Where(IsRule).ToList();

			Assert.NotEmpty(rules);
			Assert.All(rules, r => Assert.NotEmpty(JunctionsOf(r)));
		}

		[Fact]
		public void TheTableStillReadsAsOneFrame()
		{
			// Removing a junction must not remove the rule: the line still has to
			// cross the full width, or the frame falls open.
			var result = Layout(Infobox);

			foreach (var rule in result.Lines.Where(IsRule))
			{
				var text = TextOf(rule);
				Assert.DoesNotContain(' ', text);
			}
		}
	}
}
