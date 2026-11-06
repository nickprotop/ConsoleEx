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
	/// An image that did not arrive stands in for itself only if it has
	/// something to say.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Alt text is what a page author writes for a reader who cannot see the
	/// picture, and showing it in place of one is right. An image with no alt
	/// text has told us nothing, and the renderer answered by inventing the word
	/// "image": a page of logos, icons and spacers became a column of
	/// <c>[image]</c>, each one occupying a line and saying less than the blank
	/// line it replaced.
	/// </para>
	/// <para>
	/// On a real news site 63 of 134 images carried no alt attribute. That is
	/// not unusual markup - a decorative image is SUPPOSED to have an empty alt,
	/// and it is how a page says "there is nothing here worth describing".
	/// </para>
	/// </remarks>
	public class HtmlDecorativeImageTests
	{
		private static LayoutResult Layout(string html, int width = 80) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string AllText(LayoutResult result) =>
			string.Join("\n", result.Lines.Select(l =>
				new string(l.Cells.Select(c => (char)c.Character.Value).ToArray()).Trim()));

		[Fact]
		public void AnImageWithNoAltTextSaysNothing()
		{
			// The defect: the renderer invented a word the page never wrote.
			var result = Layout("<p>before</p><img src=\"logo.png\"><p>after</p>");

			Assert.DoesNotContain("image", AllText(result), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void AnImageWithEmptyAltTextSaysNothing()
		{
			// alt="" is how a page explicitly marks a picture as decorative. It
			// is a statement, not an omission, and the statement is "skip me".
			var result = Layout("<p>before</p><img src=\"spacer.gif\" alt=\"\"><p>after</p>");

			Assert.DoesNotContain("image", AllText(result), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void TheTextAroundADecorativeImageSurvives()
		{
			// The guard: dropping the placeholder must not drop its neighbours.
			var result = Layout("<p>before</p><img src=\"logo.png\"><p>after</p>");

			var text = AllText(result);
			Assert.Contains("before", text);
			Assert.Contains("after", text);
		}

		[Fact]
		public void AnImageWithAltTextStillShowsIt()
		{
			// The case that must not regress: an author who described their
			// picture gets that description shown.
			var result = Layout("<img src=\"cat.png\" alt=\"a sleeping cat\">");

			Assert.Contains("a sleeping cat", AllText(result));
		}

		[Fact]
		public void ALinkedDecorativeImageKeepsSomethingToClick()
		{
			// A picture wrapped in a link is the one case where a placeholder
			// earns its line: drop it and the link has nothing to land on, so a
			// reader cannot follow it at all. The URL is better than a word the
			// page never wrote.
			var result = Layout(
				"<a href=\"https://example.com\"><img src=\"logo.png\"></a>");

			var links = result.Lines
				.Where(l => l.Links != null)
				.SelectMany(l => l.Links!)
				.ToArray();

			Assert.NotEmpty(links);
			Assert.Equal("https://example.com", links[0].Url);
		}

		[Fact]
		public void APageOfDecorativeImagesIsNotAColumnOfPlaceholders()
		{
			// The symptom as a reader meets it: a page whose images are logos,
			// icons and spacers should read as its text, not as a list of the
			// word "image".
			var html = "<p>Real content here.</p>"
				+ string.Concat(Enumerable.Repeat("<img src=\"icon.png\">", 20))
				+ "<p>More real content.</p>";

			var result = Layout(html);
			var text = AllText(result);

			Assert.DoesNotContain("image", text, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("Real content here.", text);
			Assert.Contains("More real content.", text);
		}
	}
}
