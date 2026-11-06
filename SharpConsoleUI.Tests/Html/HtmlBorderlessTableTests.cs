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
	/// A table that says it has no border is laid out without one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every table used to be drawn in box-drawing lines, whatever it said about
	/// itself. For a data table that is right; for the tables an older page uses
	/// to position things it is not, and three of the four tables on the Hacker
	/// News front page declare <c>border="0"</c> for exactly that reason.
	/// </para>
	/// <para>
	/// The result was a page of nested boxes - one around each story, one around
	/// each empty spacer cell - where the author had asked for none. The columns
	/// still do their work; only the lines go away.
	/// </para>
	/// </remarks>
	public class HtmlBorderlessTableTests
	{
		private static string Render(string html, int width = 70) =>
			HtmlTestHelpers.LinesToText(
				new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black).Lines);

		/// <summary>Any of the box-drawing characters a bordered table uses.</summary>
		private static bool HasBoxDrawing(string text) =>
			text.Any(c => c is '┌' or '┐' or '└' or '┘'
				or '─' or '│' or '┼' or '┬' or '┴'
				or '├' or '┤');

		[Fact]
		public void ATableDeclaringNoBorderGetsNone()
		{
			var text = Render("<table border=\"0\"><tr><td>Alpha</td><td>Beta</td></tr></table>");

			Assert.False(HasBoxDrawing(text), "a border=\"0\" table was drawn with box lines");
			Assert.Contains("Alpha", text);
			Assert.Contains("Beta", text);
		}

		[Fact]
		public void ATableDeclaringABorderKeepsIt()
		{
			var text = Render("<table border=\"1\"><tr><td>Alpha</td></tr></table>");

			Assert.True(HasBoxDrawing(text), "a border=\"1\" table lost its lines");
		}

		[Fact]
		public void ATableSayingNothingKeepsItsBorder()
		{
			// The common case, and the one that must not change: a data table
			// written without the attribute still reads best with lines, and
			// every existing page and test depends on it.
			var text = Render("<table><tr><td>Alpha</td></tr></table>");

			Assert.True(HasBoxDrawing(text), "a plain table lost its lines");
		}

		[Fact]
		public void ABorderlessTableStillAlignsItsColumns()
		{
			// Dropping the lines is not dropping the layout. The columns are what
			// a layout table is for, so they still have to line up.
			var text = Render(
				"<table border=\"0\">"
					+ "<tr><td>a</td><td>LONGER</td></tr>"
					+ "<tr><td>bb</td><td>X</td></tr>"
					+ "</table>", width: 40);

			var rows = text.Split('\n').Where(l => l.Trim().Length > 0).ToList();
			Assert.True(rows.Count >= 2, "the borderless table lost its rows");

			int first = rows[0].IndexOf("LONGER", System.StringComparison.Ordinal);
			int second = rows[1].IndexOf('X');
			Assert.True(first > 0 && first == second,
				$"second column started at {first} then {second}");
		}

		[Fact]
		public void ABorderlessTableNestedInOneIsAlsoBare()
		{
			// How a page of layout tables is actually built, and where the boxes
			// multiplied: a borderless table inside a borderless cell.
			var text = Render(
				"<table border=\"0\"><tr><td>"
					+ "<table border=\"0\"><tr><td>Inner</td></tr></table>"
					+ "</td></tr></table>");

			Assert.False(HasBoxDrawing(text), "a nested border=\"0\" table was still boxed");
			Assert.Contains("Inner", text);
		}

		[Fact]
		public void ALinkInABorderlessTableIsStillALink()
		{
			// Losing the chrome must not lose the content's behaviour.
			var result = new HtmlLayoutEngine().Layout(
				"<table border=\"0\"><tr><td><a href=\"https://a.com\">HERE</a></td></tr></table>",
				70, Color.White, Color.Black, linkColor: Color.Green);

			Assert.Equal(1, result.Lines.Sum(l => l.Links?.Length ?? 0));
		}
	}
}
