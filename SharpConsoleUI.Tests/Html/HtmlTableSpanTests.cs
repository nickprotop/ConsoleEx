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
	/// <c>colspan</c> and <c>rowspan</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Both were ignored, so a cell declaring <c>colspan="2"</c> occupied one
	/// column and pushed everything after it one place to the left. The row
	/// below ended up in a different column from the row above, and the table
	/// stopped being a grid.
	/// </para>
	/// <para>
	/// A Hacker News story is exactly that shape: a rank, a vote arrow and a
	/// title on one row, then <c>&lt;td colspan="2"&gt;&lt;/td&gt;</c> and the
	/// score on the next. Without the span the score landed under the vote
	/// arrow instead of under the title it belongs to.
	/// </para>
	/// </remarks>
	public class HtmlTableSpanTests
	{
		private static string[] Rows(string html, int width = 60) =>
			HtmlTestHelpers.LinesToText(
					new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black).Lines)
				.Split('\n')
				.Where(l => l.Trim().Length > 0)
				.ToArray();

		/// <summary>Where a word starts, in columns.</summary>
		private static int ColumnOf(string line, string word) =>
			line.IndexOf(word, StringComparison.Ordinal);

		[Fact]
		public void AColspanCellPushesLaterCellsPastTheColumnsItCovers()
		{
			// The Hacker News story shape, reduced: three columns, and a second
			// row that skips the first two with one spanning cell.
			var rows = Rows(
				"<table border=\"0\">"
					+ "<tr><td>1.</td><td>vote</td><td>TITLE</td></tr>"
					+ "<tr><td colspan=\"2\"></td><td>SCORE</td></tr>"
					+ "</table>");

			Assert.True(rows.Length >= 2, "the table lost a row");

			int title = ColumnOf(rows[0], "TITLE");
			int score = ColumnOf(rows[1], "SCORE");

			Assert.True(title > 0, "the title was not laid out");
			Assert.Equal(title, score);
		}

		[Fact]
		public void AColspanCellIsAsWideAsTheColumnsItCovers()
		{
			// A spanning cell gets the width of its columns plus the separator
			// between them, so long content in it uses the room it was given
			// rather than wrapping as if it were one column.
			var rows = Rows(
				"<table border=\"0\">"
					+ "<tr><td>aaaa</td><td>bbbb</td></tr>"
					+ "<tr><td colspan=\"2\">one two three four</td></tr>"
					+ "</table>", width: 40);

			// The spanning content fits on one line because it has both columns.
			Assert.Contains(rows, r => r.Contains("one two three four", StringComparison.Ordinal));
		}

		[Fact]
		public void ColspanCountsTowardTheTablesColumnTotal()
		{
			// A table whose widest row reaches its width through a span still has
			// that many columns, or the rows that spell them out would overflow.
			var rows = Rows(
				"<table border=\"1\">"
					+ "<tr><td colspan=\"3\">wide</td></tr>"
					+ "<tr><td>a</td><td>b</td><td>c</td></tr>"
					+ "</table>", width: 40);

			var widths = rows.Select(r => r.TrimEnd().Length).Distinct().ToList();
			Assert.True(widths.Count == 1,
				"rows disagreed on width: " + string.Join(", ", widths));
		}

		[Fact]
		public void ARowspanCellHoldsItsColumnOnFollowingRows()
		{
			// A cell spanning two rows means the row beneath it has one fewer
			// cell to place, and what it does place belongs in the NEXT column.
			var rows = Rows(
				"<table border=\"0\">"
					+ "<tr><td rowspan=\"2\">SIDE</td><td>FIRST</td></tr>"
					+ "<tr><td>SECOND</td></tr>"
					+ "</table>");

			Assert.True(rows.Length >= 2, "the table lost a row");

			int first = ColumnOf(rows[0], "FIRST");
			int second = ColumnOf(rows[1], "SECOND");

			Assert.True(first > 0, "the first row was not laid out");
			Assert.Equal(first, second);
		}

		[Fact]
		public void AnAbsurdSpanDoesNotRunPastTheTable()
		{
			// Real pages carry nonsense values. A span wider than the table is
			// clamped rather than producing a row wider than every other.
			var rows = Rows(
				"<table border=\"1\">"
					+ "<tr><td colspan=\"99\">wide</td></tr>"
					+ "<tr><td>a</td><td>b</td></tr>"
					+ "</table>", width: 40);

			var widths = rows.Select(r => r.TrimEnd().Length).Distinct().ToList();
			Assert.True(widths.Count == 1,
				"rows disagreed on width: " + string.Join(", ", widths));
		}

		[Fact]
		public void APlainTableIsUnaffected()
		{
			// The overwhelmingly common case has no spans at all and must lay out
			// exactly as it did before.
			var rows = Rows(
				"<table border=\"1\"><tr><th>H1</th><th>H2</th></tr>"
					+ "<tr><td>a</td><td>b</td></tr></table>", width: 40);

			Assert.Contains(rows, r => r.Contains("H1", StringComparison.Ordinal));
			Assert.Contains(rows, r => r.Contains("b", StringComparison.Ordinal));
			Assert.True(rows.Select(r => r.TrimEnd().Length).Distinct().Count() == 1);
		}
	}
}
