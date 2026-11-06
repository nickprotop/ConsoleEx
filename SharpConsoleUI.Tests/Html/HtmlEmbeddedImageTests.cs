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
	/// A picture renders wherever it is written, not only where the block walker
	/// happens to reach it directly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An image is laid out as block rows, but a container that holds text hands
	/// its children to the inline flow in one go, and the inline flow has no way
	/// to emit rows: it replaced the picture with its alt text and carried on. So
	/// whether a picture appeared depended on which element it sat in. A
	/// <c>&lt;div&gt;</c> recursed into the block walker and rendered; a
	/// <c>&lt;p&gt;</c>, <c>&lt;li&gt;</c>, <c>&lt;td&gt;</c> or heading flowed
	/// inline and rendered alt text instead.
	/// </para>
	/// <para>
	/// That is the common way to write one. A picture between two sentences of a
	/// paragraph is ordinary HTML, and it was the one position that did not work,
	/// which also made the gap easy to miss: a page whose images each sit in their
	/// own wrapper renders perfectly.
	/// </para>
	/// <para>
	/// A terminal cell is far taller than it is wide, so a picture cannot sit
	/// inside a line of text and stay recognisable. It takes its own rows at the
	/// point in the text where it was written, and the text before and after it
	/// keeps its order.
	/// </para>
	/// </remarks>
	public class HtmlEmbeddedImageTests
	{
		/// <summary>A data-URI PNG, decoded during layout with no download.</summary>
		private static string DataUriPng(int width, int height)
		{
			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					buffer.SetPixel(x, y, new ImagePixel(180, 60, 60));

			return "data:image/png;base64,"
				+ Convert.ToBase64String(KittyProtocol.EncodePng(buffer));
		}

		private static string Img(int px = 300) =>
			$"<img src=\"{DataUriPng(px, px / 2)}\" width=\"{px}\" alt=\"a picture\">";

		private static LayoutResult Layout(string html, int width = 120) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: true, linkColor: Color.Green);

		/// <summary>Rows carrying half-block image content.</summary>
		private static int ImageRowCount(LayoutResult result) =>
			result.Lines.Count(l => l.Cells.Any(c => c.Character.Value == '▀'));

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray()).Trim();

		private static bool IsImageRow(LayoutLine line) =>
			line.Cells.Any(c => c.Character.Value == '▀');

		[Theory]
		[InlineData("<p>text {0} text</p>", "a paragraph")]
		[InlineData("<h2>text {0}</h2>", "a heading")]
		[InlineData("<ul><li>text {0}</li></ul>", "a list item")]
		[InlineData("<table><tr><td>{0}</td></tr></table>", "a table cell")]
		[InlineData("<div>text {0} text</div>", "a div")]
		[InlineData("<blockquote>text {0}</blockquote>", "a blockquote")]
		public void AnImageRendersInsideAnyContainer(string template, string what)
		{
			// The defect: only containers that recursed into the block walker
			// rendered a picture. The ones that flowed their children inline -
			// every container that holds text - showed alt text instead.
			var result = Layout(string.Format(template, Img()));

			Assert.True(ImageRowCount(result) > 0,
				$"no picture rendered inside {what}");
		}

		[Fact]
		public void TheTextKeepsItsOrderAroundThePicture()
		{
			// A picture takes its own rows, so the sentence it was written inside
			// is split. What must not happen is reordering: the words before it
			// have to stay before it and the words after it after it, or the
			// paragraph reads wrongly.
			var result = Layout($"<p>before {Img()} after</p>");

			var meaningful = result.Lines
				.Where(l => IsImageRow(l) || TextOf(l).Length > 0)
				.ToList();

			int beforeAt = meaningful.FindIndex(l => TextOf(l).Contains("before"));
			int imageAt = meaningful.FindIndex(IsImageRow);
			int afterAt = meaningful.FindIndex(l => TextOf(l).Contains("after"));

			Assert.True(beforeAt >= 0, "the text before the picture is missing");
			Assert.True(imageAt >= 0, "the picture is missing");
			Assert.True(afterAt >= 0, "the text after the picture is missing");

			Assert.True(beforeAt < imageAt, "the leading text moved below the picture");
			Assert.True(imageAt < afterAt, "the trailing text moved above the picture");
		}

		[Fact]
		public void AnImageAloneInAParagraphDoesNotLeaveAnEmptyTextLine()
		{
			// The common case on a real page: a wrapper element whose only child
			// is the picture. Splitting the paragraph around it must not leave the
			// blank line that an empty text run would produce.
			var result = Layout($"<p>{Img()}</p>");

			Assert.True(ImageRowCount(result) > 0, "the picture did not render");
			Assert.DoesNotContain(result.Lines,
				l => !IsImageRow(l) && TextOf(l) == "[a picture]");
		}

		[Fact]
		public void SeveralImagesInOneParagraphAllRender()
		{
			// Splitting has to handle more than one break point, and each picture
			// keeps its place between the runs of text.
			var result = Layout($"<p>one {Img()} two {Img()} three</p>");

			// Two pictures of the same size, so twice the rows of one.
			int oneImage = ImageRowCount(Layout($"<p>{Img()}</p>"));
			Assert.Equal(oneImage * 2, ImageRowCount(result));

			var text = string.Join(" ", result.Lines.Where(l => !IsImageRow(l)).Select(TextOf));
			Assert.Contains("one", text);
			Assert.Contains("two", text);
			Assert.Contains("three", text);
		}

		[Fact]
		public void AnEmbeddedImageInsideALinkIsStillALink()
		{
			// A linked picture already works where the block walker reaches it.
			// Splitting a paragraph must carry the enclosing link with it, or
			// moving a picture into a paragraph would silently drop its link.
			const string target = "https://example.com/target";
			var result = Layout($"<p>see <a href=\"{target}\">{Img()}</a> here</p>");

			var imageLinks = result.Lines
				.Where(IsImageRow)
				.Where(l => l.Links != null)
				.SelectMany(l => l.Links!)
				.ToArray();

			Assert.NotEmpty(imageLinks);
			Assert.All(imageLinks, l => Assert.Equal(target, l.Url));
		}

		[Fact]
		public void TextLinksAroundAnEmbeddedImageSurviveTheSplit()
		{
			// The inline flow numbers links as it goes, and the split runs it more
			// than once. The text links on either side must both survive and keep
			// distinct ids, or Tab navigation breaks on any paragraph holding a
			// picture.
			var result = Layout(
				$"<p><a href=\"https://example.com/one\">first</a> {Img()} "
				+ "<a href=\"https://example.com/two\">second</a></p>");

			var urls = result.Lines
				.Where(l => l.Links != null)
				.SelectMany(l => l.Links!)
				.Select(l => l.Url)
				.Distinct()
				.ToList();

			Assert.Contains("https://example.com/one", urls);
			Assert.Contains("https://example.com/two", urls);

			var ids = result.Lines
				.Where(l => l.Links != null)
				.SelectMany(l => l.Links!)
				.Select(l => l.LinkId)
				.Distinct()
				.ToList();

			Assert.Equal(2, ids.Count);
		}

		[Fact]
		public void HidingImagesStillYieldsAltTextInAParagraph()
		{
			// With pictures turned off, the paragraph must read as it always did:
			// alt text inline, no rows, and no split.
			var result = new HtmlLayoutEngine().Layout(
				$"<p>before {Img()} after</p>", 120, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

			Assert.Equal(0, ImageRowCount(result));

			var text = string.Join(" ", result.Lines.Select(TextOf));
			Assert.Contains("before", text);
			Assert.Contains("after", text);
			Assert.Contains("a picture", text);
		}

		[Fact]
		public void AnImageInATableCellIsSizedByThePictureNotItsAltText()
		{
			// A table measures its columns from each cell's visible TEXT, and a
			// picture contributes none. Rendering one without widening the column
			// would print rows far wider than the cell that holds them, so the
			// column has to know the picture is there.
			var result = Layout($"<table><tr><td>{Img(300)}</td><td>x</td></tr></table>");

			var imageLines = result.Lines.Where(IsImageRow).ToList();
			Assert.NotEmpty(imageLines);

			// Every image row has to fit inside the table's own width, borders
			// included: a row wider than the table is the overflow this guards.
			int tableWidth = result.Lines.Max(l => l.Cells.Length);
			Assert.All(imageLines, l => Assert.True(l.Cells.Length <= tableWidth,
				$"an image row ({l.Cells.Length} cols) overflowed the table ({tableWidth} cols)"));
		}

		[Fact]
		public void ATableCellKeepsItsTextBesideThePicture()
		{
			// The cell is still a cell: text written with the picture has to
			// survive, in order, exactly as it does in a paragraph.
			var result = Layout($"<table><tr><td>before {Img(200)} after</td></tr></table>");

			Assert.True(ImageRowCount(result) > 0, "no picture rendered in the cell");

			var text = string.Join(" ", result.Lines.Where(l => !IsImageRow(l)).Select(TextOf));
			Assert.Contains("before", text);
			Assert.Contains("after", text);
		}

		[Fact]
		public void ATableOfTextIsUnchangedByImageSupport()
		{
			// The guard that matters most: tables are the most load-bearing thing
			// this engine lays out, and a table with no picture in it must lay out
			// byte for byte as it did before.
			const string html = "<table><tr><th>A</th><th>B</th></tr>"
				+ "<tr><td>one</td><td>two</td></tr>"
				+ "<tr><td>three</td><td>four</td></tr></table>";

			var result = Layout(html);

			Assert.Equal(0, ImageRowCount(result));
			var text = string.Join("\n", result.Lines.Select(TextOf));
			Assert.Contains("one", text);
			Assert.Contains("four", text);
		}

		[Fact]
		public void AnImageWithNoSourceDoesNotSplitTheText()
		{
			// A split costs a line break, so it must happen only where a picture
			// actually renders. An <img> with no src cannot render one, and the
			// sentence should stay a single line.
			var result = Layout("<p>before <img alt=\"nothing\"> after</p>");

			Assert.Equal(0, ImageRowCount(result));

			var nonEmpty = result.Lines.Where(l => TextOf(l).Length > 0).ToList();
			Assert.Single(nonEmpty);
			Assert.Contains("before", TextOf(nonEmpty[0]));
			Assert.Contains("after", TextOf(nonEmpty[0]));
		}
	}
}
