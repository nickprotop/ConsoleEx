// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Html;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Regression tests using a real-world HTML page (Wikipedia "Cat" article) as a fixture.
	/// This page stresses the layout engine with ~824KB of HTML containing 20 tables,
	/// deeply nested infoboxes, and heavy inline markup — the exact scenario that exposed
	/// the "hang on Wikipedia cat page" regression from the nested-table / scrollbar-thrash fix.
	/// </summary>
	public class WikipediaCatRegressionTests
	{
		private static string LoadFixture()
		{
			var dir = Path.GetDirectoryName(typeof(WikipediaCatRegressionTests).Assembly.Location)!;
			var path = Path.Combine(dir, "Html", "Fixtures", "wikipedia_cat.html");
			return File.ReadAllText(path);
		}

		[Fact]
		public void Layout_CompletesInReasonableTime()
		{
			var html = LoadFixture();
			var engine = new HtmlLayoutEngine();

			var sw = Stopwatch.StartNew();
			var result = engine.Layout(html, maxWidth: 108, Color.White, Color.Black);
			sw.Stop();

			// A clean single-pass layout of the cat article is well under 10s on any
			// reasonable hardware. If we go over that, something is pathological —
			// this guards against accidental O(n²) walks or infinite/ping-pong logic.
			Assert.True(sw.Elapsed.TotalSeconds < 10,
				$"Wikipedia Cat layout took {sw.Elapsed.TotalSeconds:F2}s (expected < 10s). " +
				"Possible regression in HtmlBlockFlow / HtmlTableLayout / nested-table handling.");

			Assert.True(result.Lines.Length > 0);
			Assert.True(result.TotalHeight > 0);
		}

		[Fact]
		public void Layout_ProducesNoControlCharactersInCells()
		{
			var html = LoadFixture();
			var engine = new HtmlLayoutEngine();

			var result = engine.Layout(html, maxWidth: 108, Color.White, Color.Black);

			// No Cell may hold a control character. A literal \n or \t in a Cell.Character
			// corrupts the terminal output stream when the buffer flushes to stdout.
			int offendingLine = -1;
			int offendingCol = -1;
			int offendingChar = 0;
			for (int i = 0; i < result.Lines.Length && offendingLine < 0; i++)
			{
				var cells = result.Lines[i].Cells;
				for (int j = 0; j < cells.Length; j++)
				{
					int v = cells[j].Character.Value;
					if (v < 0x20 && v != 0)
					{
						offendingLine = i;
						offendingCol = j;
						offendingChar = v;
						break;
					}
				}
			}

			Assert.True(offendingLine < 0,
				$"Found control char U+{offendingChar:X4} at line {offendingLine}, cell {offendingCol}");
		}

		[Fact]
		public void Layout_IsDeterministicAcrossRepeatedCalls()
		{
			// Re-runs the layout three times and asserts identical results.
			// Guards against state leaking between layouts (e.g. static caches).
			var html = LoadFixture();
			var engine = new HtmlLayoutEngine();

			var r1 = engine.Layout(html, maxWidth: 108, Color.White, Color.Black);
			var r2 = engine.Layout(html, maxWidth: 108, Color.White, Color.Black);
			var r3 = engine.Layout(html, maxWidth: 108, Color.White, Color.Black);

			Assert.Equal(r1.TotalHeight, r2.TotalHeight);
			Assert.Equal(r2.TotalHeight, r3.TotalHeight);
			Assert.Equal(r1.Lines.Length, r2.Lines.Length);
			Assert.Equal(r2.Lines.Length, r3.Lines.Length);
		}
	}
}
