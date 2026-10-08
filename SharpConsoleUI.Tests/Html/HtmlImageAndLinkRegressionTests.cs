// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Html;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	public class HtmlImageAndLinkRegressionTests
	{
		private const string Pixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";

		// A non-routable address: a synchronous fetch would hang until the HTTP timeout
		private const string UnreachableImage = "http://10.255.255.1/cat.png";

		private static LayoutResult Layout(string html) =>
			new HtmlLayoutEngine().Layout(html, 80, Color.White, Color.Black);

		[Fact]
		public void SetContent_WithShowImages_DoesNotBlockOnRemoteImages()
		{
			var html = new HtmlControl { ShowImages = true };

			var sw = Stopwatch.StartNew();
			html.SetContent($"<p>Text</p><img src=\"{UnreachableImage}\" alt=\"cat\">");
			sw.Stop();

			Assert.True(sw.Elapsed.TotalSeconds < 3, $"SetContent took {sw.Elapsed.TotalSeconds:F1}s");
		}

		[Fact]
		public void SetContent_WithShowImages_ShowsAltTextWhileRemoteImageLoads()
		{
			var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<p>Text</p><img src=\"{UnreachableImage}\" alt=\"cat\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("[cat]", text);
		}

		[Fact]
		public void SetContent_WithShowImages_RendersDataUriImageImmediately()
		{
			var html = new HtmlControl { ShowImages = true };
			html.SetContent($"<p>Text</p><img src=\"{Pixel}\" alt=\"pixel\">");

			var text = string.Join("\n", ContainerTestHelpers.RenderToLines(html, 80, 20));

			Assert.Contains("Text", text);
			Assert.DoesNotContain("[pixel]", text);
		}

		[Fact]
		public void LinkRegion_EndsWhereTheLinkEnds()
		{
			var result = Layout("<p>see <a href=\"https://example.com\">here</a> for more plain text</p>");

			var links = HtmlTestHelpers.GetAllLinks(result.Lines);

			var link = Assert.Single(links);
			Assert.Equal("here", link.Text);
			Assert.Equal(4, link.EndX - link.StartX);
		}

		[Fact]
		public void LinkIds_AreUniqueAcrossParagraphs()
		{
			var result = Layout(
				"<p><a href=\"https://a.example\">a</a> one</p>" +
				"<p><a href=\"https://b.example\">b</a> two</p>" +
				"<div><p><a href=\"https://c.example\">c</a> three</p></div>");

			var ids = HtmlTestHelpers.GetAllLinks(result.Lines).Select(l => l.LinkId).ToList();

			Assert.Equal(3, ids.Count);
			Assert.Equal(3, ids.Distinct().Count());
		}

		[Fact]
		public void WrappedLink_KeepsOneIdAcrossLines()
		{
			var longText = string.Join(" ", Enumerable.Repeat("word", 30));
			var result = new HtmlLayoutEngine().Layout(
				$"<p><a href=\"https://example.com\">{longText}</a> after</p>", 40, Color.White, Color.Black);

			var links = HtmlTestHelpers.GetAllLinks(result.Lines);

			Assert.True(links.Count > 1, "The link should wrap onto several lines");
			Assert.Single(links.Select(l => l.LinkId).Distinct());
		}
	}
}
