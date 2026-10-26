// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Surfaces;
using Xunit;

namespace SharpConsoleUI.Tests.DiagnosticsFonts
{
	/// <summary>
	/// Braille in a deterministic screenshot.
	/// </summary>
	/// <remarks>
	/// The embedded face is DejaVu Sans Mono, which has none of the 256 braille
	/// patterns - the one monospace face in its family that omits them. Embedded
	/// mode consults no system fonts on purpose, because machine-dependent
	/// fallbacks are what it exists to remove, so a braille chart photographed
	/// blank while the app drew it correctly.
	/// <para>
	/// A terminal solves this by composing several faces. So does this: braille
	/// is drawn from geometry rather than from a font, which keeps the output
	/// identical on every machine and costs no bytes in the package.
	/// </para>
	/// </remarks>
	public class EmbeddedBrailleGlyphTests
	{
		private const int CellWidth = 8;
		private const int CellHeight = 16;
		private const int BrailleFirst = 0x2800;
		private const int BrailleLast = 0x28FF;

		private static DiagnosticsFont Embedded()
		{
			var font = DiagnosticsFont.Load(null, CellHeight, CellWidth,
				out string? error, FontSource.Embedded);
			Assert.Null(error);
			Assert.NotNull(font);
			return font!;
		}

		/// <summary>Draws one rune and counts the pixels it inked.</summary>
		private static int InkedPixels(DiagnosticsFont font, Rune rune)
		{
			var rgb = new byte[CellWidth * CellHeight * 3];
			font.DrawRune(rgb, CellWidth, CellHeight, 0, 0, rune, Color.White);
			int inked = 0;
			for (int i = 0; i < rgb.Length; i += 3)
				if (rgb[i] != 0 || rgb[i + 1] != 0 || rgb[i + 2] != 0) inked++;
			return inked;
		}

		[Fact]
		public void EveryBraillePatternDrawsSomething()
		{
			using var font = Embedded();

			// U+2800 is blank by definition; every other pattern has at least one dot.
			var blank = new System.Collections.Generic.List<string>();
			for (int cp = 0x2801; cp <= 0x28FF; cp++)
			{
				if (InkedPixels(font, new Rune(cp)) == 0)
					blank.Add($"U+{cp:X4}");
			}

			Assert.True(blank.Count == 0,
				$"{blank.Count} braille patterns drew nothing: {string.Join(", ", blank.Take(8))}");
		}

		[Fact]
		public void BrailleIsNotReportedMissing()
		{
			using var font = Embedded();

			foreach (int cp in new[] { 0x2801, 0x28FF, 0x2847, 0x28C0 })
				InkedPixels(font, new Rune(cp));

			Assert.Empty(font.MissingCodepoints.Where(cp => cp >= 0x2800 && cp <= 0x28FF));
		}

		[Fact]
		public void MoreDotsInkMorePixels()
		{
			// Guards against a stub that draws the same blob for every pattern: the
			// whole point is that the DOTS carry the data.
			using var font = Embedded();

			int one = InkedPixels(font, new Rune(0x2801));    // a single dot
			int all = InkedPixels(font, new Rune(0x28FF));    // all eight

			Assert.True(all > one * 4,
				$"one dot inked {one} pixels, eight inked {all}");
		}

		[Fact]
		public void EveryPatternLooksDifferent()
		{
			// "Draws something" is not enough: a stub that stamps one dot for every
			// codepoint passes that, and passes the missing-glyph check too, while
			// rendering every braille chart as the same meaningless texture. The
			// patterns must be DISTINCT, because the dots are the data.
			using var font = Embedded();

			var seen = new System.Collections.Generic.Dictionary<string, int>();
			for (int cp = BrailleFirst; cp <= BrailleLast; cp++)
			{
				var rgb = new byte[CellWidth * CellHeight * 3];
				font.DrawRune(rgb, CellWidth, CellHeight, 0, 0, new Rune(cp), Color.White);
				string key = System.Convert.ToBase64String(rgb);
				if (seen.TryGetValue(key, out int other))
					Assert.Fail($"U+{cp:X4} renders identically to U+{other:X4}");
				seen[key] = cp;
			}

			Assert.Equal(BrailleLast - BrailleFirst + 1, seen.Count);
		}

		[Theory]
		// dot number, column (0 = left), row (0 = top)
		[InlineData(1, 0, 0)]
		[InlineData(2, 0, 1)]
		[InlineData(3, 0, 2)]
		[InlineData(4, 1, 0)]
		[InlineData(5, 1, 1)]
		[InlineData(6, 1, 2)]
		[InlineData(7, 0, 3)]
		[InlineData(8, 1, 3)]
		public void EachDotSitsWhereUnicodeSaysItDoes(int dot, int column, int row)
		{
			// Dots 1-3 run DOWN the left column and 4-6 down the right; 7 and 8 are
			// the eight-dot extension and sit at the bottom of each column rather
			// than continuing the numbering. Reading the block as a plain row-major
			// grid swaps dots 2 through 5 and still leaves U+2801 top-left and
			// U+2880 bottom-right - so sampling only those two proves nothing.
			using var font = Embedded();

			var at = Centroid(font, new Rune(BrailleFirst + (1 << (dot - 1))));

			double expectedX = (column + 0.5) * (CellWidth / 2.0);
			double expectedY = (row + 0.5) * (CellHeight / 4.0);

			Assert.True(Math.Abs(at.X - expectedX) < CellWidth / 4.0,
				$"dot {dot} sat at x={at.X:0.0}, expected about {expectedX:0.0}");
			Assert.True(Math.Abs(at.Y - expectedY) < CellHeight / 8.0,
				$"dot {dot} sat at y={at.Y:0.0}, expected about {expectedY:0.0}");
		}

		/// <summary>The centre of mass of whatever a rune inked.</summary>
		private static (double X, double Y) Centroid(DiagnosticsFont font, Rune rune)
		{
			var rgb = new byte[CellWidth * CellHeight * 3];
			font.DrawRune(rgb, CellWidth, CellHeight, 0, 0, rune, Color.White);

			double sx = 0, sy = 0, total = 0;
			for (int y = 0; y < CellHeight; y++)
				for (int x = 0; x < CellWidth; x++)
				{
					int w = rgb[(y * CellWidth + x) * 3];
					if (w == 0) continue;
					sx += x * w; sy += y * w; total += w;
				}

			Assert.True(total > 0, "the rune inked nothing");
			return (sx / total, sy / total);
		}

		[Fact]
		public void TheBlankPatternStaysBlank()
		{
			using var font = Embedded();
			Assert.Equal(0, InkedPixels(font, new Rune(0x2800)));
		}
	}
}
