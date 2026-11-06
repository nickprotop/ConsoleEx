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
	/// An image wrapped in a link is a link.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Links are built by the inline flow, which tracks an open <c>&lt;a&gt;</c>
	/// and closes a region per line. An image is emitted as block rows that
	/// construct their lines directly, so it never passed through that
	/// machinery: the picture rendered and the link was dropped. Not clickable,
	/// not hoverable, not a Tab stop.
	/// </para>
	/// <para>
	/// That is how an encyclopedia wraps every thumbnail, so on a real article
	/// no picture could be followed at all. The same gap applied to the alt text
	/// shown when an image does not arrive: text standing in for a linked image,
	/// itself not a link.
	/// </para>
	/// </remarks>
	public class HtmlLinkedImageTests
	{
		private const string Target = "https://example.com/target";

		/// <summary>A data-URI PNG of the given size.</summary>
		private static string DataUriPng(int width, int height)
		{
			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					buffer.SetPixel(x, y, new ImagePixel(180, 60, 60));

			return "data:image/png;base64,"
				+ Convert.ToBase64String(KittyProtocol.EncodePng(buffer));
		}

		private static LayoutResult Layout(string html, int width = 120) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: true, linkColor: Color.Green);

		private static LinkRegion[] AllLinks(LayoutResult result) =>
			result.Lines.Where(l => l.Links != null).SelectMany(l => l.Links!).ToArray();

		/// <summary>Rows carrying half-block image content.</summary>
		private static int ImageRowCount(LayoutResult result) =>
			result.Lines.Count(l => l.Cells.Any(c => c.Character.Value == '▀'));

		[Fact]
		public void ALinkedImageIsALink()
		{
			// The defect: the picture arrived and the link did not.
			var html = $"<a href=\"{Target}\"><img src=\"{DataUriPng(300, 150)}\" width=\"300\" alt=\"a\"></a>";

			var result = Layout(html);

			Assert.True(ImageRowCount(result) > 0, "the image did not render at all");
			Assert.NotEmpty(AllLinks(result));
			Assert.All(AllLinks(result), l => Assert.Equal(Target, l.Url));
		}

		[Fact]
		public void TheWholeImageIsOneTabStop()
		{
			// An image is many rows, and a region is per row, so without a shared
			// id a single picture would be a dozen separate Tab stops. The
			// inline flow already keeps one id across the lines of a wrapped
			// link; an image has to do the same.
			var html = $"<a href=\"{Target}\"><img src=\"{DataUriPng(400, 300)}\" width=\"400\" alt=\"a\"></a>";

			var result = Layout(html);
			var links = AllLinks(result);

			Assert.True(links.Length > 1, "test precondition: the image should span several rows");
			Assert.Single(links.Select(l => l.LinkId).Distinct());
		}

		[Fact]
		public void TheLinkRegionCoversTheImage()
		{
			// A click anywhere on the picture has to land on it, so the region
			// spans the rendered columns rather than a single cell.
			var html = $"<a href=\"{Target}\"><img src=\"{DataUriPng(350, 200)}\" width=\"350\" alt=\"a\"></a>";

			var result = Layout(html);

			foreach (var line in result.Lines.Where(l => l.Links != null))
			{
				int imageColumns = line.Cells.Count(c => c.Character.Value == '▀');
				if (imageColumns == 0) continue;

				var link = line.Links![0];
				Assert.Equal(0, link.StartX);
				Assert.Equal(imageColumns, link.EndX);
			}
		}

		[Fact]
		public void ImageRowsAreMarkedSoFocusDoesNotScrambleThem()
		{
			// The focus highlight inverts every cell of a link region. On a
			// half-block image that swaps the top and bottom pixel of each cell,
			// so a focused picture would visibly scramble. The rows say they are
			// an image and the highlight leaves their colours alone.
			var html = $"<a href=\"{Target}\"><img src=\"{DataUriPng(300, 150)}\" width=\"300\" alt=\"a\"></a>";

			var result = Layout(html);

			var imageLines = result.Lines
				.Where(l => l.Cells.Any(c => c.Character.Value == '▀'))
				.ToList();

			Assert.NotEmpty(imageLines);
			Assert.All(imageLines, l => Assert.True(l.IsImage,
				"an image row did not declare itself an image"));
		}

		[Fact]
		public void AnUnlinkedImageGainsNoLink()
		{
			// The guard: a bare image is not clickable, and must not become a
			// Tab stop that goes nowhere.
			var html = $"<img src=\"{DataUriPng(300, 150)}\" width=\"300\" alt=\"a\">";

			var result = Layout(html);

			Assert.True(ImageRowCount(result) > 0, "the image did not render at all");
			Assert.Empty(AllLinks(result));
		}

		[Fact]
		public void AltTextForALinkedImageIsALink()
		{
			// When the picture does not arrive its alt text stands in for it, and
			// what it stands in for is a link. Text that reads as a link and does
			// nothing is worse than either alternative.
			//
			// An http image is never fetched during layout, so this is the real
			// path a page takes before its images load.
			var html = $"<a href=\"{Target}\"><img src=\"https://example.com/missing.png\" alt=\"A picture\"></a>";

			var result = Layout(html);
			var links = AllLinks(result);

			Assert.NotEmpty(links);
			Assert.All(links, l => Assert.Equal(Target, l.Url));
		}

		[Fact]
		public void ALinkedImageSharesTheLinkNumberingOfTheText()
		{
			// Ids come from one counter, so an image takes its place in the Tab
			// order rather than colliding with a text link or sitting outside the
			// sequence.
			var html = "<p><a href=\"https://example.com/one\">first</a></p>"
				+ $"<a href=\"{Target}\"><img src=\"{DataUriPng(300, 150)}\" width=\"300\" alt=\"a\"></a>"
				+ "<p><a href=\"https://example.com/two\">last</a></p>";

			var result = Layout(html);
			var ids = AllLinks(result).Select(l => l.LinkId).Distinct().OrderBy(i => i).ToList();

			Assert.Equal(3, ids.Count);
			Assert.Equal(ids.Count, ids.Distinct().Count());
		}
	}
}
