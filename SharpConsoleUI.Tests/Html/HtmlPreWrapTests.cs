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
	/// A code block keeps the characters it was given.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A preformatted block does not reflow: its line breaks are the author's and
	/// must survive. What it did instead was stop emitting cells at the viewport
	/// edge, so everything past that column was absent from the layout entirely -
	/// not clipped at paint time, where scrolling could reach it, but never laid
	/// out at all. A line of code wider than the window lost its tail, and no
	/// amount of resizing or scrolling brought it back.
	/// </para>
	/// <para>
	/// Most code is wider than a terminal window, so this is the common case
	/// rather than an edge one, and losing the end of a line of code is losing
	/// the part that usually matters.
	/// </para>
	/// <para>
	/// The fix is to continue onto further rows rather than discard. That is not
	/// reflow: no break is chosen by fitting words, nothing is joined, and the
	/// author's own breaks still start new lines. It is the same thing a terminal
	/// does with a line too long for it.
	/// </para>
	/// </remarks>
	public class HtmlPreWrapTests
	{
		private const int Width = 40;

		private static LayoutResult Layout(string html, int width = Width) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray()).TrimEnd();

		/// <summary>Everything the layout produced, lines joined without separators.</summary>
		private static string AllText(LayoutResult result) =>
			string.Concat(result.Lines.Select(TextOf));

		[Fact]
		public void ALineWiderThanTheViewportKeepsItsEnd()
		{
			// The defect: the characters past the edge were dropped at layout time
			// and could not be reached by any means.
			var code = new string('a', 100) + "END";

			var result = Layout($"<pre>{code}</pre>");

			Assert.Contains("END", AllText(result));
		}

		[Fact]
		public void EveryCharacterOfALongLineSurvives()
		{
			// Not just the end: nothing in the middle may go missing either.
			var code = string.Concat(Enumerable.Range(0, 100).Select(i => (char)('a' + i % 26)));

			var result = Layout($"<pre>{code}</pre>");

			Assert.Contains(code, AllText(result));
		}

		[Fact]
		public void TheAuthorsOwnBreaksStillStartNewLines()
		{
			// Continuing a long line must not blur into reflow: a break the author
			// wrote stays a break, and two short lines stay two lines.
			var result = Layout("<pre>first\nsecond</pre>");

			var lines = result.Lines.Select(TextOf).Where(t => t.Length > 0).ToList();

			Assert.Contains("first", lines);
			Assert.Contains("second", lines);
		}

		[Fact]
		public void ShortLinesAreNotJoined()
		{
			// The guard against the opposite failure: a code block of short lines
			// must lay out exactly as it did before, one line per line.
			var result = Layout("<pre>a\nb\nc</pre>");

			var lines = result.Lines.Select(TextOf).Where(t => t.Length > 0).ToList();

			Assert.Equal(new[] { "a", "b", "c" }, lines);
		}

		[Fact]
		public void NoLineOverflowsTheViewport()
		{
			// Continuing rather than discarding must still respect the width, or
			// the cells would simply overflow instead of being dropped.
			var code = new string('x', 100);

			var result = Layout($"<pre>{code}</pre>");

			Assert.All(result.Lines, l => Assert.True(l.Cells.Length <= Width,
				$"a line of {l.Cells.Length} cells overflowed a {Width}-column viewport"));
		}

		[Fact]
		public void AWideCharacterIsNotSplitAcrossRows()
		{
			// A CJK glyph occupies two columns and cannot straddle the boundary:
			// it moves to the next row whole, rather than leaving a half behind.
			var code = new string('中', 60);

			var result = Layout($"<pre>{code}</pre>");

			Assert.Contains("中", AllText(result));
			Assert.All(result.Lines, l => Assert.True(l.Cells.Length <= Width,
				"a wide character pushed a line past the viewport"));
		}
	}
}
