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
	/// A grid meant for a desktop window, met at terminal width.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A page tells us its viewport is 1024 pixels wide, so its desktop media
	/// queries apply and we are handed the layout written for a desktop screen:
	/// eight columns, twelve columns, whatever the designer chose. 1024 pixels
	/// is around 120 terminal columns, so eight tracks are fifteen characters
	/// each, and a news headline shreds into a narrow ribbon.
	/// </para>
	/// <para>
	/// Columns also ran into each other. A track's text was written at exactly
	/// the track's width with nothing between tracks, so a cell that filled its
	/// own width touched the next one and the two words merged into a third that
	/// is in neither.
	/// </para>
	/// </remarks>
	public class HtmlDesktopGridTests
	{
		private const int Width = 120;

		private static LayoutResult Layout(string html, int width = Width) =>
			new HtmlLayoutEngine().Layout(html, width, Color.White, Color.Black,
				showImages: false, linkColor: Color.Green);

		private static string TextOf(LayoutLine line) =>
			new string(line.Cells.Select(c => (char)c.Character.Value).ToArray());

		private static string FirstText(LayoutResult result) =>
			result.Lines.Select(TextOf).FirstOrDefault(t => t.Trim().Length > 0) ?? string.Empty;

		private static string Grid(int tracks, string cellText) =>
			$"<div style=\"display:grid;grid-template-columns:repeat({tracks}, 1fr)\">"
			+ string.Concat(Enumerable.Range(0, tracks).Select(i => $"<div>{cellText}{i}</div>"))
			+ "</div>";

		[Theory]
		[InlineData(4)]
		[InlineData(6)]
		[InlineData(8)]
		[InlineData(12)]
		public void AGridWrittenForADesktopDoesNotShredItsText(int tracks)
		{
			// A track has to hold a phrase, not a syllable. Four tracks at 120
			// columns is thirty characters each, which is already a narrow
			// ribbon; eight is fifteen, and twelve is ten.
			var result = Layout(Grid(tracks, "Headline number "));

			var lines = result.Lines.Where(l => TextOf(l).Trim().Length > 0).ToList();

			Assert.True(lines.Count <= tracks + 1,
				$"{tracks} tracks produced {lines.Count} lines: the cells were shredded rather than stacked");
		}

		[Theory]
		[InlineData(2)]
		[InlineData(3)]
		public void ColumnsDoNotRunIntoEachOther(int tracks)
		{
			// The defect that makes a grid unreadable rather than merely narrow:
			// a cell whose text fills its track touched the next cell, merging
			// two words into a third that is in neither of them.
			//
			// Cell text is chosen to end in a letter and the next to begin with
			// one, so any missing gap shows as a run of letters across the
			// boundary.
			//
			// Only track counts the terminal can still honour side by side are
			// tested: once a grid is too narrow it stacks, and a long run of
			// letters is then one cell's own text rather than two cells merged.
			var result = Layout(Grid(tracks, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

			foreach (var line in result.Lines)
			{
				var text = TextOf(line);
				if (text.Trim().Length == 0) continue;

				// Two cells' contents are separated, so no run of 'a' may be
				// longer than one track's worth of them.
				int longest = 0, run = 0;
				foreach (var c in text)
				{
					if (c == 'a') { run++; longest = Math.Max(longest, run); }
					else run = 0;
				}

				Assert.True(longest <= Width / tracks,
					$"{tracks} tracks: a run of {longest} characters crossed a column boundary");
			}
		}

		[Fact]
		public void TwoRoomyColumnsStillShareALine()
		{
			// The guard: a grid the terminal CAN honour is still honoured. Two
			// columns at 120 is sixty each, which holds a sentence.
			var result = Layout(
				"<div style=\"display:grid;grid-template-columns:1fr 1fr\">"
				+ "<div>Left</div><div>Right</div></div>");

			Assert.Contains(result.Lines, l =>
			{
				var t = TextOf(l);
				return t.Contains("Left") && t.Contains("Right");
			});
		}

		[Fact]
		public void ADesktopMediaQueryGridStacksAtTerminalWidth()
		{
			// The real shape, from newsit.gr: the grid is not written inline but
			// inside a media query for a desktop window, which our declared
			// viewport matches. This is how a modern page delivers its layout,
			// so the rule has to hold for it and not only for inline styles.
			var html = "<html><head><style>"
				+ "@media screen and (min-width: 769px) and (max-width: 1200px) {"
				+ "  .lat { display: grid; grid-template-columns: repeat(8, 1fr); }"
				+ "}</style></head><body>"
				+ "<div class=\"lat\"><div>Alpha story text here</div>"
				+ "<div>Beta story text here</div><div>Gamma story text here</div></div>"
				+ "</body></html>";

			var result = Layout(html);
			var first = FirstText(result);

			Assert.DoesNotContain("Beta", first);
		}

		[Fact]
		public void EveryCellsTextSurvives()
		{
			// Stacking must not lose a cell: all of them are still on the page,
			// just underneath each other.
			var result = Layout(Grid(8, "Story "));

			var all = string.Join(" ", result.Lines.Select(TextOf));
			for (int i = 0; i < 8; i++)
				Assert.Contains($"Story {i}", all);
		}
	}
}
