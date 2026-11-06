// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Html;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Loading pages and images from <c>file:</c> URLs, the way a browser does.
	/// </summary>
	/// <remarks>
	/// Both paths used to accept only <c>http</c>, <c>https</c> and <c>data:</c>.
	/// A page went straight to HttpClient, which throws on a file URI, and an
	/// image fell through every scheme check to a bare return-null and showed
	/// its alt text. Opening an HTML file on disk - the first thing anyone tries
	/// with an HTML widget - did nothing at all.
	/// </remarks>
	public class HtmlLocalFileTests : IDisposable
	{
		private readonly string _dir;

		public HtmlLocalFileTests()
		{
			_dir = Path.Combine(Path.GetTempPath(), "scui-html-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_dir);
		}

		public void Dispose()
		{
			try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
		}

		/// <summary>Writes a decodable PNG of the given size and returns its path.</summary>
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

		private string WriteText(string name, string content)
		{
			var path = Path.Combine(_dir, name);
			File.WriteAllText(path, content);
			return path;
		}

		private static string FileUrl(string path) => new Uri(path).AbsoluteUri;

		[Fact]
		public void AFileUrlIsRecognisedAsLocal()
		{
			Assert.True(HtmlImageLoader.IsLocalFileUrl(FileUrl(WritePng("a.png", 8, 8))));
			Assert.False(HtmlImageLoader.IsLocalFileUrl("https://example.com/a.png"));
			Assert.False(HtmlImageLoader.IsLocalFileUrl("data:image/png;base64,AAAA"));

			// A bare path is deliberately NOT local: a relative src on a fetched
			// page resolves against that page's host, and must not turn into a
			// read of this machine's disk.
			Assert.False(HtmlImageLoader.IsLocalFileUrl("/etc/hostname"));
			Assert.False(HtmlImageLoader.IsLocalFileUrl(null));
		}

		[Fact]
		public void AnImageLoadsFromAFileUrl()
		{
			var url = FileUrl(WritePng("photo.png", 64, 32));

			using var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<p>Before</p><img src=\"{url}\" alt=\"photo\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			// The alt text is what shows when an image does not load, so its
			// ABSENCE is the assertion: the image itself was drawn instead.
			Assert.Contains("Before", text);
			Assert.DoesNotContain("[photo]", text);
		}

		[Fact]
		public void AMissingFileFallsBackToAltText()
		{
			var url = FileUrl(Path.Combine(_dir, "nope.png"));

			using var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<img src=\"{url}\" alt=\"gone\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("[gone]", text);
		}

		[Fact]
		public void ANonImageFileFallsBackToAltText()
		{
			// A readable file that is not an image: it must fail to decode and
			// show alt text, not throw out of layout.
			var url = FileUrl(WriteText("notes.txt", "this is not a png"));

			using var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<img src=\"{url}\" alt=\"text\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("[text]", text);
		}

		[Fact]
		public void ADirectoryFallsBackToAltText()
		{
			var url = FileUrl(_dir) + "/";

			using var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<img src=\"{url}\" alt=\"dir\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("[dir]", text);
		}

		[Fact]
		public async Task APageLoadsFromAFileUrl()
		{
			var page = WriteText("page.html",
				"<h1>Local heading</h1><p>Local paragraph body.</p>");

			using var html = new HtmlControl();
			await html.LoadUrlAsync(FileUrl(page));

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("Local heading", text);
			Assert.Contains("Local paragraph", text);
		}

		[Fact]
		public async Task APageResolvesItsRelativeImagesAgainstItsOwnDirectory()
		{
			// The real reason local pages matter: a saved page sits beside its
			// images and refers to them by name. The base URL has to carry the
			// directory, or every image on the page is a broken one.
			WritePng("beside.png", 64, 32);
			var page = WriteText("withimage.html",
				"<p>Shown</p><img src=\"beside.png\" alt=\"beside\">");

			using var html = new HtmlControl { ShowImages = true };
			await html.LoadUrlAsync(FileUrl(page));

			// The image load is a background phase; give it a moment to land.
			for (int i = 0; i < 50; i++)
			{
				var probe = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));
				if (!probe.Contains("[beside]")) break;
				await Task.Delay(20);
			}

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("Shown", text);
			Assert.DoesNotContain("[beside]", text);
		}

		[Fact]
		public async Task ARemotePageMayNotReadLocalFiles()
		{
			// The cross-origin rule every browser enforces: a page fetched over
			// http(s) cannot pull in file:// subresources. Without it, any site
			// you visit could probe this machine's disk and read the result back
			// off the screen.
			//
			// Driven through the real layout engine with a remote base URL, which
			// is the state a fetched page is laid out in.
			var image = FileUrl(WritePng("secret.png", 64, 32));

			var result = new HtmlLayoutEngine().Layout(
				$"<img src=\"{image}\" alt=\"blocked\">",
				80, Color.White, Color.Black,
				baseUrl: "https://example.com/page.html",
				showImages: true,
				imageCache: new System.Collections.Generic.Dictionary<string, PixelBuffer?>());

			var text = HtmlTestHelpers.LinesToText(result.Lines);

			// Alt text means the image was not loaded, which is the whole point.
			Assert.Contains("[blocked]", text);
		}

		[Fact]
		public void ALocalPageMayStillReadLocalFiles()
		{
			// The rule is about crossing origins, not about file:// itself: a
			// local page keeps full access to the folder it lives in, or the
			// feature would be pointless.
			var image = FileUrl(WritePng("ok.png", 64, 32));
			var page = WriteText("host.html", $"<img src=\"{image}\" alt=\"allowed\">");

			var result = new HtmlLayoutEngine().Layout(
				$"<img src=\"{image}\" alt=\"allowed\">",
				80, Color.White, Color.Black,
				baseUrl: FileUrl(page),
				showImages: true,
				imageCache: new System.Collections.Generic.Dictionary<string, PixelBuffer?>());

			var text = HtmlTestHelpers.LinesToText(result.Lines);

			Assert.DoesNotContain("[allowed]", text);
		}

		[Fact]
		public void ADocumentWithNoOriginMayReadLocalFiles()
		{
			// SetContent with no base URL: the content came from the application
			// itself, not from a remote server, so there is no origin to cross.
			// Blocking here would break the ordinary case of an app showing a
			// local image in HTML it wrote.
			var image = FileUrl(WritePng("own.png", 64, 32));

			using var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<img src=\"{image}\" alt=\"own\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.DoesNotContain("[own]", text);
		}

		[Fact]
		public async Task ALocalImageSurvivesAnEarlierSetContent()
		{
			// The real usage path, and the one the other tests missed. A browser
			// window shows a home page with SetContent and only then navigates,
			// which is a different sequence from loading a URL into a fresh
			// control: the first paint of a URL load deliberately runs with
			// images off, and it is the background image phase that turns them
			// back on. That phase collected http and https URLs only, so a page
			// whose only images are local had nothing to collect, returned
			// early, and never re-laid the page out with images enabled.
			//
			// Every isolated test passed throughout, because laying out directly
			// never goes through that phase at all.
			WritePng("beside.png", 64, 32);
			var page = WriteText("after-setcontent.html",
				"<p>Shown</p><img src=\"beside.png\" alt=\"beside\">");

			using var html = new HtmlControl { ShowImages = true };

			html.SetContent("<h1>Home</h1><p>a home page first</p>");
			_ = ContainerTestHelpers.RenderToLines(html, 120, 30);

			await html.LoadUrlAsync(FileUrl(page));

			for (int i = 0; i < 60; i++)
			{
				var probe = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 120, 30));
				if (!probe.Contains("[beside]")) break;
				await Task.Delay(25);
			}

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 120, 30));

			Assert.Contains("Shown", text);
			Assert.DoesNotContain("[beside]", text);
		}

		[Fact]
		public void SetContentWithARemoteBaseUrlMayNotReadLocalFiles()
		{
			// The same-origin rule has to follow the ORIGIN, not the entry point.
			// Content handed over with SetContent is normally trusted, because it
			// came from the application - but an application that passes a remote
			// base URL is telling us the markup came from that server, and markup
			// from a server gets a server's permissions.
			var image = FileUrl(WritePng("private.png", 64, 32));

			using var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<img src=\"{image}\" alt=\"nope\">", "https://example.com/page.html");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("[nope]", text);
		}

		[Fact]
		public async Task AMissingPageReportsAnError()
		{
			var url = FileUrl(Path.Combine(_dir, "absent.html"));

			using var html = new HtmlControl();
			bool errored = false;
			html.LoadError += (_, _) => errored = true;

			await html.LoadUrlAsync(url);

			Assert.True(errored, "loading a missing local page reported no error");
		}
	}
}
