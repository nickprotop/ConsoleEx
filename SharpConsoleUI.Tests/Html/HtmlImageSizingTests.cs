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
	/// How a declared image width becomes a number of terminal columns.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two paths used to answer this differently. An image sized by the HTML
	/// <c>width</c> attribute divided its pixels by 1, so a 220px thumbnail asked
	/// for 220 columns and filled the page. The same image sized by a stylesheet
	/// went through the generic CSS px-to-column helper, which divides by 8, and
	/// came out at 28 columns. One image, an eightfold difference, decided by
	/// which of the two places happened to declare the width.
	/// </para>
	/// <para>
	/// Neither constant was right, because a declared pixel width has no fixed
	/// meaning in columns: it depends on how wide the page is. These tests pin
	/// the rule that replaced them - a declared width is a fraction of a nominal
	/// browser viewport, and that fraction is applied to the columns actually
	/// available.
	/// </para>
	/// </remarks>
	public class HtmlImageSizingTests
	{
		/// <summary>A decodable PNG of the given pixel size, inline so nothing is fetched.</summary>
		private static string DataUriPng(int width, int height)
		{
			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					buffer.SetPixel(x, y, new ImagePixel(200, 40, 40));

			var png = KittyProtocol.EncodePng(buffer);
			return "data:image/png;base64," + Convert.ToBase64String(png);
		}

		/// <summary>
		/// The width of the widest image row produced for the document.
		/// </summary>
		/// <remarks>
		/// Read from the laid-out lines rather than from a painted buffer: the
		/// question is what width layout chose, and a painted surface would also
		/// be clipped by the control, hiding an over-wide answer instead of
		/// reporting it.
		/// </remarks>
		private static int RenderedImageWidth(string html, int pageWidth)
		{
			var result = new HtmlLayoutEngine().Layout(
				html, pageWidth, Color.White, Color.Black, showImages: true,
				imageCache: new System.Collections.Generic.Dictionary<string, PixelBuffer?>());

			int widest = 0;
			foreach (var line in result.Lines)
			{
				// An image row is solid colour: no alt text, no spaces. Alt-text
				// fallback lines are text, so they would not qualify.
				if (line.Cells.Length == 0) continue;
				if (line.Cells.Any(c => c.Character.Value == ' ')) continue;
				if (line.Cells.Length > widest) widest = line.Cells.Length;
			}
			return widest;
		}

		/// <summary>
		/// A declared width as a fraction of the nominal viewport, in columns.
		/// </summary>
		/// <remarks>
		/// Computed from the constants rather than written out, so the tests say
		/// WHY a number is expected. A test that hardcoded 18 would have to be
		/// re-derived by hand if the nominal viewport changed, and would not say
		/// which of the two it was pinning.
		/// </remarks>
		/// <summary>An img tag declaring the given width in CSS pixels.</summary>
		private static string Img(double declaredPx)
		{
			int px = (int)Math.Round(declaredPx);
			// Served pixels match the declared width, so the attribute is taken at
			// face value rather than reconciled against a different real size.
			return $"<img src=\"{DataUriPng(px, Math.Max(1, px / 2))}\" width=\"{px}\" alt=\"a\">";
		}

		private static int Expected(int declaredPx, int pageWidth) =>
			(int)Math.Round(pageWidth * Math.Min(1.0, declaredPx / HtmlConstants.CssContentWidthPx));

		[Fact]
		public void AStylesheetWidthAndAnAttributeWidthAgree()
		{
			// The defect itself: one image and one declared size, asking for two
			// different widths depending on which of the two places wrote it.
			// The attribute path divided by 1 and filled the page; the
			// stylesheet path divided by 8 and came out eight times smaller.
			var src = DataUriPng(220, 110);

			int fromAttribute = RenderedImageWidth(
				$"<img src=\"{src}\" width=\"220\" alt=\"a\">", 160);

			int fromStylesheet = RenderedImageWidth(
				$"<img src=\"{src}\" style=\"width:220px\" alt=\"a\">", 160);

			// Asserted against the expected value as well as against each other:
			// the two agreed before this fix too, by both being clamped to the
			// full page width, so equality alone would have passed on the bug.
			Assert.Equal(Expected(220, 160), fromAttribute);
			Assert.Equal(fromAttribute, fromStylesheet);
		}

		[Fact]
		public void ADeclaredWidthIsAFractionOfThePage()
		{
			// A 220px thumbnail is a thumbnail: about an eighth of a 1920px
			// viewport, so about an eighth of the columns. Before the fix this
			// was the entire page width.
			var src = DataUriPng(220, 110);
			int pageWidth = 160;

			int columns = RenderedImageWidth($"<img src=\"{src}\" width=\"220\" alt=\"a\">", pageWidth);

			Assert.Equal(Expected(220, pageWidth), columns);
			Assert.True(columns < pageWidth / 2,
				$"a 220px thumbnail took {columns} of {pageWidth} columns");
		}

		[Fact]
		public void AWidthIsAFractionOfTheContentColumn()
		{
			// The rule itself, stated once: a declared width is a fraction of the
			// content column a page is written for, and that fraction is spent on
			// the columns actually available.
			//
			// Checked at the ratios rather than against a remembered number, so
			// the test says what the rule IS instead of what one constant happens
			// to be. A quarter-column image gets a quarter of the line; a half
			// gets a half.
			int page = 120;
			double column = HtmlConstants.CssContentWidthPx;

			int quarter = RenderedImageWidth(Img(column / 4), page);
			int half = RenderedImageWidth(Img(column / 2), page);

			Assert.Equal(page / 4, quarter);
			Assert.Equal(page / 2, half);
		}

		[Fact]
		public void TheContentColumnIsNarrowerThanTheViewport()
		{
			// WHY there are two constants, and the thing that actually broke.
			//
			// A browser window holds a sidebar, margins and chrome; the text and
			// pictures sit in a narrower column inside it, and that column is what
			// an author sizes a picture for. A terminal renders none of that
			// furniture, so its text runs the full width - the column IS the line.
			//
			// Measuring a picture against the whole window therefore shrinks it
			// relative to the words beside it, by exactly the ratio between the
			// two. Pinning the inequality is what stops the column constant being
			// "corrected" back to the viewport, which is the regression this
			// guards.
			Assert.True(HtmlConstants.CssContentWidthPx < HtmlConstants.CssViewportWidthPx,
				"the content column must be narrower than the viewport, or images "
					+ "are measured against furniture a terminal does not render");
		}

		[Fact]
		public void AnImageWiderThanTheColumnSpansTheLine()
		{
			// An image sized for the full column - or beyond it - is meant to span
			// the text, so it spans the line. Expressed relative to the constant
			// rather than as a literal, so it tests the clamp and not arithmetic
			// on a number copied out of the source.
			int page = 120;

			Assert.Equal(page, RenderedImageWidth(Img(HtmlConstants.CssContentWidthPx), page));
			Assert.Equal(page, RenderedImageWidth(Img(HtmlConstants.CssContentWidthPx * 3), page));
		}

		[Fact]
		public void ASmallDecorationStaysADecoration()
		{
			// The guard on widening the scale: a small image must not become a
			// picture. Stated as a fraction of the column so it holds whatever the
			// column is - a fortieth of the text is a glyph at any width.
			int page = 120;

			int columns = RenderedImageWidth(Img(HtmlConstants.CssContentWidthPx / 40), page);

			Assert.True(columns <= 4, $"a decoration took {columns} of {page} columns");
		}

		[Fact]
		public void TheSameImageIsWiderOnAWiderPage()
		{
			// "Adapted to the room": the declared width is a fraction, so it
			// resolves to more columns when there are more columns to spend.
			//
			// Both values are asserted exactly. Before the fix each one equalled
			// its page width, so the ratio held by accident and a bare
			// wide > narrow comparison passed against the bug.
			var src = DataUriPng(400, 200);
			string html = $"<img src=\"{src}\" width=\"400\" alt=\"a\">";

			int narrow = RenderedImageWidth(html, 60);
			int wide = RenderedImageWidth(html, 200);

			Assert.Equal(Expected(400, 60), narrow);
			Assert.Equal(Expected(400, 200), wide);
			Assert.True(narrow < 60 && wide < 200,
				$"still filling the page: {narrow}/60 and {wide}/200");
		}

		[Fact]
		public void AnImageNeverOutgrowsThePage()
		{
			// A hero image declares more pixels than the page can hold. It is
			// clamped to the page rather than overflowing into the border - the
			// one case where filling the width IS the right answer.
			var src = DataUriPng(4000, 1000);
			int columns = RenderedImageWidth($"<img src=\"{src}\" width=\"4000\" alt=\"a\">", 80);

			Assert.Equal(80, columns);
		}

		[Fact]
		public void AnUndeclaredImageIsSizedByItsOwnPixels()
		{
			// No width anywhere: the image's own pixel size is the declaration,
			// and it goes through the same fraction. A 640px image is a third of
			// the viewport, so a third of the page - not all of it.
			var src = DataUriPng(640, 320);
			int pageWidth = 120;

			int columns = RenderedImageWidth($"<img src=\"{src}\" alt=\"a\">", pageWidth);

			Assert.Equal(Expected(640, pageWidth), columns);
			Assert.True(columns < pageWidth, $"undeclared image filled the page ({columns})");
		}

		[Fact]
		public void TheServedPixelsWinOverAMisleadingWidthAttribute()
		{
			// Wikipedia's <img width> carries the ORIGINAL file's resolution, not
			// the size it is shown at: a thumbnail served as 250 real pixels
			// declares width="3640". Taken at face value that fills the page, and
			// the article renders as a column of giant photographs.
			//
			// The served pixels are what the page actually chose, so they cap the
			// declared width. Nothing is upscaled past the data that exists.
			var src = DataUriPng(250, 160);
			int pageWidth = 120;

			int columns = RenderedImageWidth(
				$"<img src=\"{src}\" width=\"3640\" alt=\"a\">", pageWidth);

			Assert.Equal(Expected(250, pageWidth), columns);
			Assert.True(columns < pageWidth, $"a 250px thumbnail filled the page ({columns})");
		}

		[Fact]
		public void TheServedPixelsAlsoLiftAnUnderstatedWidthAttribute()
		{
			// The same page's other habit: a width attribute left over from a
			// narrower layout - width="135" on an image served at 250 pixels.
			// Believing it throws away pixels that were downloaded and shrinks a
			// readable thumbnail to a smudge.
			//
			// The declared width and the served width disagree about the same
			// image, and the served one is the one backed by actual data.
			var src = DataUriPng(250, 160);
			int pageWidth = 120;

			int columns = RenderedImageWidth(
				$"<img src=\"{src}\" width=\"135\" alt=\"a\">", pageWidth);

			Assert.Equal(Expected(250, pageWidth), columns);
		}

		[Fact]
		public void AnImageDeliberatelyShownSmallerThanItsPixelsStaysSmall()
		{
			// The limit of that rule. A page may legitimately serve a big image
			// and ask for it small - a 1600px photo shown as a 200px inset. That
			// is a layout decision, not stale metadata, and it is respected: the
			// served pixels raise a width only up to the image's own size, never
			// beyond what the page asked for when the page asked for less than
			// it sent AND said so in a stylesheet, which is where real layout
			// intent lives.
			var src = DataUriPng(1600, 900);
			int pageWidth = 120;

			int columns = RenderedImageWidth(
				$"<img src=\"{src}\" style=\"width:200px\" alt=\"a\">", pageWidth);

			Assert.Equal(Expected(200, pageWidth), columns);
		}

		[Fact]
		public void ATinyIconStaysSmall()
		{
			// The opposite end: a 32px icon must not be inflated into a banner.
			// This is what a fill-the-width rule gets wrong, and it is why the
			// honest fraction is worth having.
			var src = DataUriPng(32, 32);
			int columns = RenderedImageWidth($"<img src=\"{src}\" width=\"32\" alt=\"a\">", 160);

			Assert.True(columns <= 8, $"a 32px icon took {columns} columns");
		}
	}
}
