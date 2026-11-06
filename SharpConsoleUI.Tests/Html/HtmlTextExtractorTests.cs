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
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Turning a run of screen lines back into the text the document said.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The lines are built directly here rather than laid out from HTML, so each
	/// case states one rule and nothing else can be blamed when it fails. What
	/// the layout puts in those lines is Task 1's business, and has its own
	/// tests.
	/// </para>
	/// </remarks>
	public class HtmlTextExtractorTests
	{
		/// <summary>A line of plain text, with the given continuation state.</summary>
		/// <summary>
		/// A line of plain text.
		/// </summary>
		/// <param name="text">The line's characters.</param>
		/// <param name="continues">Whether the layout broke this line.</param>
		/// <param name="betweenWords">
		/// Whether that break fell between words. True by default because a prose
		/// wrap is the ordinary case; a code line or an over-long word broken
		/// mid-token passes false, and rejoins with nothing rather than a space.
		/// </param>
		private static LayoutLine Line(string text, bool continues = false, bool betweenWords = true) =>
			new LayoutLine(0, 0, text.Length,
				text.Select(c => new Cell(c, Color.White, Color.Black)).ToArray(),
				TextAlignment.Left, links: null, isImage: false, continuesNextLine: continues,
				brokeBetweenWords: continues && betweenWords);

		/// <summary>A rendered picture: cells that are pixels, carrying no text.</summary>
		private static LayoutLine ImageLine(int width = 8) =>
			new LayoutLine(0, 0, width,
				Enumerable.Repeat(new Cell('▀', Color.White, Color.Black), width).ToArray(),
				TextAlignment.Left, links: null, isImage: true);

		/// <summary>Extracts the whole of every given line.</summary>
		private static string All(params LayoutLine[] lines) =>
			HtmlTextExtractor.Extract(lines, 0, 0, lines.Length - 1, lines[^1].Cells.Length);

		[Fact]
		public void AWrappedLineJoinsToTheNextWithASpace()
		{
			// The whole point: a paragraph the author wrote as one line comes back
			// as one line, whatever width it was shown at.
			var text = All(Line("alpha", continues: true), Line("beta"));

			Assert.Equal("alpha beta", text);
		}

		[Fact]
		public void AnUnwrappedLineJoinsWithANewline()
		{
			// A break the author wrote survives.
			var text = All(Line("alpha"), Line("beta"));

			Assert.Equal("alpha\nbeta", text);
		}

		[Fact]
		public void ASingleLineSelectionReturnsJustTheSpan()
		{
			var lines = new[] { Line("hello world") };

			Assert.Equal("world", HtmlTextExtractor.Extract(lines, 0, 6, 0, 11));
		}

		[Fact]
		public void TrailingSpaceOnAWrappedLineDoesNotDouble()
		{
			// A wrapped line often keeps the space it broke at. Joining with
			// another space would give two, which shows up in every paste.
			var text = All(Line("alpha ", continues: true), Line("beta"));

			Assert.Equal("alpha beta", text);
		}

		[Fact]
		public void AnImageRowContributesNothing()
		{
			// A rendered picture stores only pixel cells; its alt text is never
			// kept. Copying the glyphs would paste a row of half-blocks, and
			// inventing a placeholder would put text in the paste that is not in
			// the document.
			var text = All(Line("alpha"), ImageLine(), Line("beta"));

			Assert.Equal("alpha\nbeta", text);
		}

		[Fact]
		public void AnImageRowDoesNotBreakAJoin()
		{
			// A picture between two halves of a wrapped sentence must not turn the
			// wrap into a break: the sentence still joins across it.
			var text = All(Line("alpha", continues: true), ImageLine(), Line("beta"));

			Assert.Equal("alpha beta", text);
		}

		[Fact]
		public void AnEmptyLineIsKept()
		{
			// The blank line between two paragraphs is part of the text.
			var text = All(Line("alpha"), Line(""), Line("beta"));

			Assert.Equal("alpha\n\nbeta", text);
		}

		[Fact]
		public void ABreakInsideATokenRejoinsWithNothing()
		{
			// A code line, or a single word too long for the viewport: the break
			// consumed no space, so putting one back would corrupt what is
			// pasted. This is the distinction BrokeBetweenWords exists for.
			var text = All(Line("alpha", continues: true, betweenWords: false), Line("beta"));

			Assert.Equal("alphabeta", text);
		}

		[Fact]
		public void AnEmptySelectionReturnsEmpty()
		{
			// A press with no drag behind it selects nothing, and nothing is what
			// it must yield rather than the line it landed on.
			var lines = new[] { Line("hello") };

			Assert.Equal(string.Empty, HtmlTextExtractor.Extract(lines, 0, 3, 0, 3));
		}
	}
}
