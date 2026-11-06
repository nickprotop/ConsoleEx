// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpConsoleUI.Html;
using SharpConsoleUI.Imaging;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// The viewport a page is told it has, and what the page does with it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A declared pixel width is read as a fraction of a nominal viewport, so
	/// the engine has to state what that viewport is. It states it twice: once
	/// to our own measuring code, and once to AngleSharp's render device, which
	/// is what answers a page's own questions about its viewport - a media query
	/// and a <c>vw</c> length.
	/// </para>
	/// <para>
	/// The device was given <c>DeviceWidth</c> only. Media queries read
	/// <c>ViewPortWidth</c>, a separate property that defaulted to zero, so
	/// every <c>min-width</c> query was false and every <c>max-width</c> query
	/// was true whatever the number beside it: a page got its phone layout and
	/// never its desktop one.
	/// </para>
	/// <para>
	/// A <c>vw</c> length is a related symptom of the same gap - it throws
	/// inside AngleSharp without a sized device - but it does not reach our
	/// output, because the resolver reads cascaded values rather than computed
	/// ones and so never asks for the pixels. Setting the viewport fixes it
	/// anyway; there is no test for it here because none can fail.
	/// </para>
	/// <para>
	/// What the stylesheet reaches here is image width: the CSS-aware document
	/// is consulted for that and nothing else, so that is where a wrong viewport
	/// shows up, and that is what these tests measure.
	/// </para>
	/// </remarks>
	public class HtmlViewportTests : IDisposable
	{
		private readonly string _dir;

		public HtmlViewportTests()
		{
			_dir = Path.Combine(Path.GetTempPath(), "scui-viewport-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_dir);
		}

		public void Dispose()
		{
			try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
		}

		private string Write(string name, string content)
		{
			var path = Path.Combine(_dir, name);
			File.WriteAllText(path, content);
			return path;
		}

		/// <summary>Writes a decodable PNG so the image has real served pixels.</summary>
		private string WritePng(string name, int width, int height)
		{
			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					buffer.SetPixel(x, y, new ImagePixel(200, 40, 40));

			var path = Path.Combine(_dir, name);
			File.WriteAllBytes(path, KittyProtocol.EncodePng(buffer));
			return path;
		}

		private static string FileUrl(string path) => new Uri(path).AbsoluteUri;

		/// <summary>
		/// The width in columns that a stylesheet rule produces for the image,
		/// with external CSS resolved the way a fetched page resolves it.
		/// </summary>
		private static async Task<int> ImageColumns(string html, string url, int width)
		{
			var engine = new HtmlLayoutEngine();

			// Prime the document cache the way Layout does: LoadCssAsync keeps its
			// result only if the HTML it loaded still matches what was last laid
			// out, so the order here is the order the control uses.
			engine.Layout(html, width, Color.White, Color.Black,
				baseUrl: url, showImages: true);
			await engine.LoadCssAsync(html, url);
			var result = engine.Layout(html, width, Color.White, Color.Black,
				baseUrl: url, showImages: true);

			// The image is the widest run of non-blank cells on any line it drew.
			int widest = 0;
			foreach (var line in result.Lines)
			{
				int run = line.Cells.Count(c => c.Character.Value != ' ');
				if (run > widest) widest = run;
			}
			return widest;
		}

		/// <summary>
		/// A page whose image width is set inside a media query, plus a base rule
		/// the query is meant to override.
		/// </summary>
		private string PageWithQuery(string queryCondition, string insideQueryPx, string basePx)
		{
			var png = WritePng("img.png", 400, 100);
			var src = FileUrl(png);
			return "<html><head><style>"
				+ $"img {{ width: {basePx}; }}"
				+ $"@media {queryCondition} {{ img {{ width: {insideQueryPx}; }} }}"
				+ "</style></head><body>"
				+ $"<img src=\"{src}\">"
				+ "</body></html>";
		}

		[Fact]
		public async Task AMinWidthQueryAppliesAtTheNominalViewport()
		{
			// 1024 is wider than 700, so the query is true and the width inside it
			// is the one that counts: 1024px of a 1024px viewport is the whole
			// page, so the image fills the 80 columns available. With
			// ViewPortWidth at zero the query was false and the 128px base rule
			// won instead, giving a tenth of that.
			var page = Write("minwidth.html", PageWithQuery("(min-width: 700px)", "1024px", "128px"));
			int columns = await ImageColumns(File.ReadAllText(page), FileUrl(page), 80);

			Assert.True(columns > 60,
				$"a min-width query did not apply at the nominal viewport: image got {columns} of 80 columns");
		}

		[Fact]
		public async Task AMaxWidthQueryForAPhoneDoesNotApplyToAWideViewport()
		{
			// The mirror, and the half that silently DID apply: 1024 is not under
			// 500, so the phone rule must not win. A device reporting zero
			// satisfies every max-width query ever written, so a page served its
			// phone layout to a wide terminal and every image came out tiny.
			var page = Write("maxwidth.html", PageWithQuery("(max-width: 500px)", "64px", "1024px"));
			int columns = await ImageColumns(File.ReadAllText(page), FileUrl(page), 80);

			Assert.True(columns > 60,
				$"a phone-only max-width rule applied at a desktop viewport: image got {columns} of 80 columns");
		}

		[Fact]
		public void AMediaQueryAppliesToTextOnTheSynchronousPath()
		{
			// The synchronous path - SetContent, and every Layout before the
			// background CSS load returns - uses a context with no render device
			// registered, so the resolver falls back to one it builds itself.
			// That fallback had the same zero viewport, so a desktop query never
			// matched and a phone query always did.
			//
			// Measured on text colour rather than image width: a stylesheet image
			// width is only read from the separately loaded CSS document, which
			// this path does not have, but text styling goes through the resolver
			// on every layout.
			var wide = new Color(0x11, 0x99, 0x22);
			var html = "<html><head><style>"
				+ "p { color: #808080; }"
				+ "@media (min-width: 700px) { p { color: #119922; } }"
				+ "</style></head><body><p>Desktop text</p></body></html>";

			var result = new HtmlLayoutEngine().Layout(html, 80, Color.White, Color.Black);

			bool painted = result.Lines
				.SelectMany(l => l.Cells)
				.Any(c => c.Foreground == wide);

			Assert.True(painted,
				"a min-width query did not apply to text on the synchronous path");
		}

		[Fact]
		public void TheNominalViewportIsADeclaredConstant()
		{
			// The image maths divides by this to turn a declared width into a
			// fraction, so it is a unit of measure and must not drift with the
			// window: a thumbnail would stop being a thumbnail on resize. Pinned
			// deliberately; see the remarks on the constant.
			Assert.Equal(1024.0, HtmlConstants.CssViewportWidthPx);
			Assert.Equal(768.0, HtmlConstants.CssViewportHeightPx);
		}
	}
}
