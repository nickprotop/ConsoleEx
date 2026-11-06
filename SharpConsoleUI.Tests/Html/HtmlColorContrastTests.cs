// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Html;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// A colour the page asks for has to be readable on the surface it lands on.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A page picks its colours for its own background. Hacker News sets
	/// <c>a:link { color:#000000 }</c>, which is right on its cream page and
	/// invisible on a dark terminal: every story title rendered black on dark
	/// and the front page looked empty, while the domain beside each title
	/// survived only because HN colours that separately.
	/// </para>
	/// <para>
	/// Honouring the page is still the default - the colours carry meaning - but
	/// a colour that cannot be read carries none. Each resolved foreground is
	/// lifted to the contrast floor against the background it is actually drawn
	/// on, the same treatment the tab strip's accent gets.
	/// </para>
	/// </remarks>
	public class HtmlColorContrastTests
	{
		private static LayoutResult Layout(string html, Color fg, Color bg) =>
			new HtmlLayoutEngine().Layout(html, 60, fg, bg);

		private static LayoutResult LayoutHonouringPage(string html, Color fg, Color bg) =>
			new HtmlLayoutEngine().Layout(html, 60, fg, bg, usePageBackground: true);

		/// <summary>The worst contrast ratio among the letters drawn.</summary>
		private static double WorstLetterContrast(LayoutResult result, Color bg)
		{
			double worst = double.MaxValue;
			foreach (var line in result.Lines)
			{
				foreach (var cell in line.Cells)
				{
					int ch = cell.Character.Value;
					if (ch is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z')) continue;
					double r = PaletteColors.ContrastRatio(cell.Foreground, bg);
					if (r < worst) worst = r;
				}
			}
			return worst == double.MaxValue ? 0 : worst;
		}

		[Fact]
		public void ABlackLinkOnADarkPageIsLifted()
		{
			// The Hacker News case, exactly.
			var bg = new Color(15, 23, 42); // a dark slate surface
			var result = Layout(
				"<html><head><style>a:link{color:#000000}</style></head>"
					+ "<body><p><a href=\"https://a.com\">StoryTitle</a></p></body></html>",
				Color.White, bg);

			double worst = WorstLetterContrast(result, bg);

			Assert.True(worst >= PaletteColors.TextContrast,
				$"the page's black link read {worst:0.00}:1 against the terminal background");
		}

		[Fact]
		public void AWhiteParagraphOnALightPageIsDarkened()
		{
			// The mirror case: a page that assumes a dark background, shown on a
			// light theme. Lifting has to work in both directions, or the fix only
			// helps half the users.
			var bg = new Color(246, 246, 239); // a cream surface
			var result = Layout(
				"<p style=\"color:#ffffff\">Some body text</p>", Color.Black, bg);

			double worst = WorstLetterContrast(result, bg);

			Assert.True(worst >= PaletteColors.TextContrast,
				$"the page's white text read {worst:0.00}:1 against a light background");
		}

		[Fact]
		public void AReadablecolourIsLeftAlone()
		{
			// The rule only rescues what cannot be read. A colour that already
			// clears the floor must arrive exactly as the page asked, or every
			// page would be repainted in approximations of itself.
			var bg = new Color(15, 23, 42);
			var result = Layout("<p style=\"color:#ff6b9d\">Pink text</p>", Color.White, bg);

			var asked = new Color(0xff, 0x6b, 0x9d);
			Assert.True(PaletteColors.ContrastRatio(asked, bg) >= PaletteColors.TextContrast,
				"test precondition: the chosen colour should already be readable");

			bool exact = result.Lines
				.SelectMany(l => l.Cells)
				.Where(c => c.Character.Value == 'P')
				.Any(c => c.Foreground == asked);

			Assert.True(exact, "a readable page colour was changed anyway");
		}

		[Fact]
		public void ThePageBackgroundIsIgnoredUnlessAskedFor()
		{
			// Default behaviour, and the reason this is opt-in: the control sits in
			// a themed window, so a page painting its own surface in the middle of
			// it has to be a deliberate choice, not a surprise on upgrade.
			var bg = new Color(15, 23, 42);
			var result = Layout(
				"<body bgcolor=\"#f6f6ef\"><p>Body text</p></body>", Color.White, bg);

			bool anyCream = result.Lines
				.SelectMany(l => l.Cells)
				.Any(c => c.Background == new Color(0xf6, 0xf6, 0xef));

			Assert.False(anyCream, "the page background was adopted without being asked for");
		}

		[Fact]
		public void ThePageBackgroundIsUsedWhenAskedFor()
		{
			// bgcolor, not CSS: it is how the pages that still use a page colour
			// actually set it. Hacker News's cream and textfiles.com's black are
			// both this attribute.
			var bg = new Color(15, 23, 42);
			var result = LayoutHonouringPage(
				"<body bgcolor=\"#f6f6ef\"><p>Body text</p></body>", Color.White, bg);

			bool anyCream = result.Lines
				.SelectMany(l => l.Cells)
				.Any(c => c.Background == new Color(0xf6, 0xf6, 0xef));

			Assert.True(anyCream, "the page background was not used");
		}

		[Fact]
		public void TextIsReadableAgainstThePageBackgroundItLandsOn()
		{
			// The two halves have to agree. Adopting the cream surface without
			// checking text against IT would just move the problem: the terminal's
			// own light foreground would now be the invisible one.
			var pageBg = new Color(0xf6, 0xf6, 0xef);
			var result = LayoutHonouringPage(
				"<body bgcolor=\"#f6f6ef\"><p>Body text</p></body>",
				Color.White, new Color(15, 23, 42));

			double worst = WorstLetterContrast(result, pageBg);

			Assert.True(worst >= PaletteColors.TextContrast,
				$"text read {worst:0.00}:1 against the page background it was drawn on");
		}

		[Fact]
		public void ALinkOnAnAdoptedPageBackgroundIsReadable()
		{
			// Hacker News's black-on-cream, with the cream adopted. The link is
			// drawn in the theme's link colour rather than the page's black -
			// anchors always are, because a user-agent stylesheet gives every
			// anchor a colour and there is no reliable way to tell that from a
			// choice the page made. What must hold either way is that the result
			// can be read on the surface it landed on.
			var pageBg = new Color(0xf6, 0xf6, 0xef);
			var result = LayoutHonouringPage(
				"<html><head><style>a:link{color:#000000}</style></head>"
					+ "<body bgcolor=\"#f6f6ef\"><p><a href=\"https://a.com\">StoryTitle</a></p></body></html>",
				Color.White, new Color(15, 23, 42));

			double worst = WorstLetterContrast(result, pageBg);

			Assert.True(worst >= PaletteColors.TextContrast,
				$"the link read {worst:0.00}:1 against the page background it was drawn on");
		}

		[Fact]
		public void ThePageBackgroundIsFoundThroughAWrapper()
		{
			// Where a classic page actually puts it: body > center > table, with a
			// trailing <script> that must not be mistaken for a second child.
			// Looking only at the body, or counting the script, finds nothing -
			// which is exactly how the real Hacker News page kept its dark
			// terminal surface with the option switched on.
			var result = LayoutHonouringPage(
				"<body><center><table bgcolor=\"#f6f6ef\"><tr><td>Story</td></tr></table></center>"
					+ "<script>var x = 1;</script></body>",
				Color.White, new Color(15, 23, 42));

			bool anyCream = result.Lines
				.SelectMany(l => l.Cells)
				.Any(c => c.Background == new Color(0xf6, 0xf6, 0xef));

			Assert.True(anyCream, "the page background inside a wrapper was not found");
		}

		[Fact]
		public void ContentDeepInThePageIsNotMistakenForTheBackground()
		{
			// The other side of that walk: once an element has real siblings it is
			// layout, not the page, and its colour belongs to it alone.
			var terminalBg = new Color(15, 23, 42);
			var result = LayoutHonouringPage(
				"<body><p>First</p><div bgcolor=\"#ff0000\">Second</div></body>",
				Color.White, terminalBg);

			bool allTerminal = result.Lines
				.SelectMany(l => l.Cells)
				.Where(c => c.Character.Value == 'F')
				.All(c => c.Background == terminalBg);

			Assert.True(allTerminal, "a nested element's colour was taken as the page background");
		}

		[Fact]
		public void TheLiftedcolourKeepsTheHueItWasGiven()
		{
			// Lifting is a rescue, not a repaint: a page's red link should come
			// back as a readable red, not as the theme's accent. Checked by hue
			// rather than by exact value, since the lift necessarily moves it.
			var bg = new Color(15, 23, 42);
			var result = Layout(
				"<p style=\"color:#400000\">Dark red words</p>", Color.White, bg);

			var lifted = result.Lines
				.SelectMany(l => l.Cells)
				.FirstOrDefault(c => c.Character.Value == 'D')
				.Foreground;

			Assert.True(lifted.R > lifted.G && lifted.R > lifted.B,
				$"a dark red was lifted to something that is no longer red: {lifted}");
		}
	}
}
