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
using SharpConsoleUI.Imaging;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// A line that gains a prefix keeps everything it already was.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The quote bar is added by rebuilding each line with two cells in front of
	/// it. A rebuild has to carry everything the line knew, and this one carried
	/// only the alignment and the links - so an image inside a blockquote lost
	/// the flag that says it is an image, and every link inside one kept the
	/// column range it had before the bar pushed it two places right.
	/// </para>
	/// <para>
	/// Both are invisible until something reads them. The image flag is what
	/// stops the focus highlight from inverting half-block pixels and scrambling
	/// the picture; the link range is what decides which cells highlight and
	/// which cells a click lands on.
	/// </para>
	/// </remarks>
	public class HtmlLinePrefixRebuildTests
	{
		private const string Target = "https://example.com/target";

		private static string DataUriPng(int width, int height)
		{
			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					buffer.SetPixel(x, y, new ImagePixel(180, 60, 60));

			return "data:image/png;base64,"
				+ Convert.ToBase64String(KittyProtocol.EncodePng(buffer));
		}

		private static LayoutResult Layout(string html, bool showImages = false, int width = 80) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: showImages, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray());

		/// <summary>The characters a link region actually covers.</summary>
		private static string Covered(LayoutLine line, LinkRegion link) =>
			new string(line.Cells
				.Skip(link.StartX)
				.Take(link.EndX - link.StartX)
				.Select(c => (char)c.Character.Value)
				.ToArray());

		[Fact]
		public void ALinkInABlockquoteCoversItsOwnText()
		{
			// The defect: the bar shifts every cell two places right, and the link
			// region kept its old columns. The region covered the bar and lost the
			// end of the word, so the highlight sat in the wrong place and a click
			// on the last characters missed.
			var result = Layout($"<blockquote><a href=\"{Target}\">LINK</a></blockquote>");

			var line = result.Lines.Single(l => l.Links != null);

			Assert.Equal("LINK", Covered(line, line.Links![0]));
		}

		[Fact]
		public void ALinkOutsideABlockquoteIsUnaffected()
		{
			// The control: the same link with no bar in front of it. This passed
			// before the fix and must keep passing, since it is the behaviour the
			// blockquote case is being brought into line with.
			var result = Layout($"<p><a href=\"{Target}\">LINK</a></p>");

			var line = result.Lines.Single(l => l.Links != null);

			Assert.Equal("LINK", Covered(line, line.Links![0]));
		}

		[Fact]
		public void TwoLinksInABlockquoteBothCoverTheirOwnText()
		{
			// Every region on the line shifts, not just the first.
			var result = Layout(
				$"<blockquote><a href=\"{Target}\">ONE</a> and <a href=\"{Target}\">TWO</a></blockquote>");

			var line = result.Lines.Single(l => l.Links != null);
			var covered = line.Links!.Select(l => Covered(line, l)).ToArray();

			Assert.Equal(new[] { "ONE", "TWO" }, covered);
		}

		[Fact]
		public void AnImageInABlockquoteStillSaysItIsAnImage()
		{
			// A half-block image row carries one pixel in the glyph foreground and
			// another in its background, so the focus highlight has to leave its
			// colours alone. It reads LayoutLine.IsImage to know, and the rebuild
			// dropped the flag: a linked picture inside a blockquote would have
			// been scrambled the moment it was focused.
			var html = $"<blockquote><img src=\"{DataUriPng(300, 150)}\" width=\"300\" alt=\"a\"></blockquote>";

			var result = Layout(html, showImages: true);

			var imageLines = result.Lines
				.Where(l => l.Cells.Any(c => c.Character.Value == '▀'))
				.ToList();

			Assert.NotEmpty(imageLines);
			Assert.All(imageLines, l => Assert.True(l.IsImage,
				"an image row inside a blockquote did not declare itself an image"));
		}

		[Fact]
		public void ALinkInAListItemCoversItsOwnText()
		{
			// The same defect in the other rebuild site. A list item prepends its
			// bullet the same way a blockquote prepends its bar, and left its link
			// regions behind in the same way - so a link in any bulleted list had
			// a click target two columns off, which is most link lists on a page.
			var result = Layout($"<ul><li><a href=\"{Target}\">LINK</a></li></ul>");

			var line = result.Lines.Single(l => l.Links != null);

			Assert.Equal("LINK", Covered(line, line.Links![0]));
		}

		[Fact]
		public void ALinkOnAWrappedListItemLineIsNotShifted()
		{
			// Only the FIRST line of an item carries the bullet, so only that line
			// shifts. Shifting the rest would push their links two columns right
			// of the text, swapping one misalignment for another.
			var result = Layout(
				$"<ul><li>{new string('x', 70)} <a href=\"{Target}\">LINK</a></li></ul>");

			var line = result.Lines.Last(l => l.Links != null);

			Assert.Equal("LINK", Covered(line, line.Links![0]));
		}

		[Fact]
		public void TheBulletIsStillThere()
		{
			// The guard for the list-item rebuild, matching the bar's.
			var result = Layout("<ul><li>item</li></ul>");

			var line = result.Lines.First(l => TextOf(l).Contains("item"));

			Assert.Contains("item", TextOf(line));
			Assert.DoesNotMatch("^item", TextOf(line).TrimStart());
		}

		[Fact]
		public void TheBarIsStillThere()
		{
			// The guard: carrying the fields forward must not cost the bar itself,
			// which is the whole point of the rebuild.
			var result = Layout("<blockquote>quoted</blockquote>");

			var line = result.Lines.First(l => TextOf(l).Contains("quoted"));

			Assert.Contains(HtmlConstants.BlockquoteBar, TextOf(line));
			Assert.StartsWith(HtmlConstants.BlockquoteBar.ToString(), TextOf(line).TrimStart());
		}
	}
}
