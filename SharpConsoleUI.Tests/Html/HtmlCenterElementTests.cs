// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Html;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// <c>&lt;center&gt;</c> as a block element.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It was missing from the block-tag set, so it fell through to the inline
	/// path and everything inside it was buffered as inline content. A table has
	/// no inline representation, so it was dropped - and with it the whole page,
	/// silently, with no error and no alt text.
	/// </para>
	/// <para>
	/// Found on Hacker News, which wraps its entire body in
	/// <c>&lt;center&gt;&lt;table&gt;</c> and rendered as three blank rows. The tag
	/// is deprecated in HTML but extremely common on the older and lighter pages
	/// a terminal browser is most likely to be pointed at.
	/// </para>
	/// </remarks>
	public class HtmlCenterElementTests
	{
		private static string Render(string html) =>
			HtmlTestHelpers.LinesToText(
				new HtmlLayoutEngine().Layout(html, 120, Color.White, Color.Black).Lines);

		[Fact]
		public void ATableInsideCenterIsNotDropped()
		{
			// The exact shape of the Hacker News page body.
			var text = Render(
				"<center><table width=\"85%\"><tr><td>HELLO</td></tr></table></center>");

			Assert.Contains("HELLO", text);
		}

		[Fact]
		public void ANestedTableInsideCenterIsNotDropped()
		{
			// HN nests several tables deep inside that outer one.
			var text = Render(
				"<center><table width=\"85%\"><tr><td>"
					+ "<table><tr><td>INNER</td></tr></table>"
					+ "</td></tr></table></center>");

			Assert.Contains("INNER", text);
		}

		[Fact]
		public void BlockContentInsideCenterSurvives()
		{
			// Not only tables: any block child was going through the inline path.
			var text = Render(
				"<center><h1>Heading</h1><p>Paragraph body.</p><ul><li>Item</li></ul></center>");

			Assert.Contains("Heading", text);
			Assert.Contains("Paragraph body.", text);
			Assert.Contains("Item", text);
		}

		[Fact]
		public void PlainTextInsideCenterStillRenders()
		{
			// The case that always worked, pinned so making <center> a block does
			// not lose its simplest use.
			var text = Render("<center>Just some centered words</center>");

			Assert.Contains("Just some centered words", text);
		}

		[Fact]
		public async Task AnUnresponsiveHostFailsRatherThanHangs()
		{
			// A host that accepts the connection and then says nothing. On
			// HttpClient's 100-second default this left the control spinning for a
			// minute and a half with no error, which reads as the application
			// hanging rather than as a site being down - what textfiles.com over
			// https does, since it serves no TLS.
			using var html = new HtmlControl();
			bool errored = false;
			html.LoadError += (_, _) => errored = true;

			var sw = Stopwatch.StartNew();
			await html.LoadUrlAsync("http://10.255.255.1/page.html");
			sw.Stop();

			Assert.True(errored, "an unreachable host reported no error");
			Assert.True(sw.Elapsed.TotalSeconds < 40,
				$"the fetch took {sw.Elapsed.TotalSeconds:F0}s before giving up");
		}

		[Fact]
		public void CenterDoesNotSwallowWhatFollowsIt()
		{
			// A block element closes the inline run before it and starts a new one
			// after; content after the close tag must still appear.
			var text = Render("<center><p>Inside</p></center><p>After</p>");

			Assert.Contains("Inside", text);
			Assert.Contains("After", text);
		}
	}
}
