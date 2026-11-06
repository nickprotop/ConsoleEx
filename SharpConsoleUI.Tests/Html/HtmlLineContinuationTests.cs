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
	/// A line records whether it ended because the text ran out of room.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A paragraph laid out at eighty columns is a dozen screen lines, and which
	/// of those breaks the author wrote is not recoverable afterwards: a line
	/// that happens to end at the margin looks exactly like one that was broken
	/// to fit. Copying such a paragraph without knowing the difference puts a
	/// hard newline every viewport width, which is the common way a paste is
	/// ruined.
	/// </para>
	/// <para>
	/// The inline flow knows at the moment it decides. Of the places it finishes
	/// a line, two are real breaks - a <c>&lt;br&gt;</c> and the end of the flow -
	/// and three are width checks. Recording that is exact where inferring it
	/// from the line's length would guess.
	/// </para>
	/// </remarks>
	public class HtmlLineContinuationTests
	{
		private const int Width = 40;

		private static LayoutResult Layout(string html, int width = Width) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray()).TrimEnd();

		private static LayoutLine[] NonEmpty(LayoutResult result) =>
			result.Lines.Where(l => TextOf(l).Length > 0).ToArray();

		/// <summary>Words that wrap predictably, long enough to need several lines.</summary>
		private static string Words(int count) =>
			string.Join(" ", Enumerable.Range(0, count).Select(i => $"word{i:D2}"));

		[Fact]
		public void AWrappedParagraphMarksEveryLineButTheLast()
		{
			var result = Layout($"<p>{Words(40)}</p>");
			var lines = NonEmpty(result);

			Assert.True(lines.Length > 2, "test precondition: the text should wrap");

			for (int i = 0; i < lines.Length - 1; i++)
			{
				Assert.True(lines[i].ContinuesNextLine,
					$"line {i} wrapped but does not say so: '{TextOf(lines[i])}'");
			}

			Assert.False(lines[^1].ContinuesNextLine,
				"the last line of a paragraph does not continue");
		}

		[Fact]
		public void AnAuthoredBreakIsNotAWrap()
		{
			// A <br> is the author's own break and has to survive copying.
			var result = Layout("<p>one<br>two</p>");

			var first = NonEmpty(result).First(l => TextOf(l).Contains("one"));

			Assert.False(first.ContinuesNextLine);
		}

		[Fact]
		public void AParagraphEndingExactlyAtTheEdgeIsNotMarked()
		{
			// The case that defeats inference: a line whose text happens to fill
			// the viewport exactly looks identical, by its length, to one that was
			// broken to fit. Only the recorded decision tells them apart.
			var exact = new string('x', Width);
			var result = Layout($"<p>{exact}</p>");

			var line = NonEmpty(result).Single();

			Assert.Equal(Width, TextOf(line).Length);
			Assert.False(line.ContinuesNextLine,
				"a paragraph that ends at the margin is not a wrap");
		}

		[Fact]
		public void AContinuedCodeLineIsAWrap()
		{
			// A code line wider than the viewport now continues onto further rows
			// rather than being truncated. That continuation is a wrap: copying it
			// must not insert a newline the author never wrote.
			var code = new string('a', 100);
			var result = Layout($"<pre>{code}</pre>");
			var lines = NonEmpty(result);

			Assert.True(lines.Length > 1, "test precondition: the code line should continue");

			for (int i = 0; i < lines.Length - 1; i++)
				Assert.True(lines[i].ContinuesNextLine, $"continued code row {i} does not say so");

			Assert.False(lines[^1].ContinuesNextLine);
		}

		[Fact]
		public void AnAuthoredNewlineInCodeIsNotAWrap()
		{
			// The opposite case in the same block: a newline the author typed.
			var result = Layout("<pre>a\nb</pre>");

			var first = NonEmpty(result).First(l => TextOf(l) == "a");

			Assert.False(first.ContinuesNextLine);
		}

		[Fact]
		public void ABulletedItemKeepsTheFlagThroughItsRebuild()
		{
			// A list item's first line is rebuilt to carry its bullet. A rebuild
			// that forgets a field silently drops it, which is how this line lost
			// IsImage and its link positions before.
			var result = Layout($"<ul><li>{Words(25)}</li></ul>");

			var first = NonEmpty(result).First();

			Assert.True(first.ContinuesNextLine,
				"the bullet rebuild dropped the continuation flag");
		}

		[Fact]
		public void AQuotedParagraphKeepsTheFlagThroughItsRebuild()
		{
			// The other rebuild site: the blockquote bar.
			var result = Layout($"<blockquote>{Words(25)}</blockquote>");

			var first = NonEmpty(result).First();

			Assert.True(first.ContinuesNextLine,
				"the blockquote rebuild dropped the continuation flag");
		}
	}
}
