// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Gridlines behind the data, and the render modes that draw them differently.
	/// </summary>
	public class ChartGridlineTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 96, int height = 30)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				VerticalAlignment = VerticalAlignment.Fill,
			};
			window.AddControl(chart);
			system.AddWindow(window);
			return (system, chart);
		}

		private static CharacterBufferSnapshot Draw(ConsoleWindowSystem system)
		{
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			return system.RenderingDiagnostics!.LastBufferSnapshot!;
		}

		private static bool AnyCellHas(CharacterBufferSnapshot snap, Color c)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) return true;
				}
			}
			return false;
		}

		private static int CountCellsWith(CharacterBufferSnapshot snap, Color c)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) n++;
				}
			}
			return n;
		}

		/// <summary>True when any cell inside the control shows something drawn.</summary>
		private static bool AnyNonBlankCellInPlot(CharacterBufferSnapshot snap, Color background)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					int cp = c.Character.Value;

					// Each mode signals "drawn" differently: pixel mode fills a cell's
					// background, braille sets a glyph above U+2800, ascii sets a
					// box-drawing character.
					bool glyph = cp != 0 && cp != ' ' && cp != 0x2800;
					bool filled = c.Background != background && c.Background != Color.Black;

					if (glyph || filled) return true;
				}
			}
			return false;
		}

		[Fact]
		public void GridlinesAppearOnlyWhenAskedFor()
		{
			var (noGrid, chartA) = Build();
			chartA.YAxis.ShowGridlines = false;
			chartA.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });
			int without = CountCellsWith(Draw(noGrid), ControlDefaults.ChartGridlineColor);

			var (grid, chartB) = Build();
			chartB.YAxis.ShowGridlines = true;
			chartB.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });
			int with = CountCellsWith(Draw(grid), ControlDefaults.ChartGridlineColor);

			Assert.Equal(0, without);
			Assert.True(with > 0, "ShowGridlines produced no gridline cells");
			GC.KeepAlive(noGrid);
			GC.KeepAlive(grid);
		}

		[Fact]
		public void AGridlineNeverHidesTheData()
		{
			// Paint order: grid first, plot second. If that inverts, a chart with
			// gridlines on quietly loses points wherever a line crosses one.
			//
			// Asserting the series appears SOMEWHERE is not enough - it survives
			// either order, since gridlines only cover the cells they cross. This
			// counts the series cells with gridlines off and on: inverting the order
			// strictly reduces the count, because every crossing is overpainted.
			var (clean, a) = Build();
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4 });
			int withoutGrid = CountCellsWith(Draw(clean), Color.Red);

			var (gridded, b) = Build();
			b.YAxis.ShowGridlines = true;
			b.XAxis.ShowGridlines = true;
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4 });
			int withGrid = CountCellsWith(Draw(gridded), Color.Red);

			Assert.True(withoutGrid > 0, "the series did not draw at all");
			Assert.Equal(withoutGrid, withGrid);

			GC.KeepAlive(clean);
			GC.KeepAlive(gridded);
		}

		[Fact]
		public void BothAxesCanDrawGridlines()
		{
			var (yOnly, a) = Build();
			a.YAxis.ShowGridlines = true;
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2 });
			int horizontal = CountCellsWith(Draw(yOnly), ControlDefaults.ChartGridlineColor);

			var (both, b) = Build();
			b.YAxis.ShowGridlines = true;
			b.XAxis.ShowGridlines = true;
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2 });
			int crossed = CountCellsWith(Draw(both), ControlDefaults.ChartGridlineColor);

			Assert.True(crossed > horizontal,
				$"adding X gridlines drew no more: {horizontal} then {crossed}");
			GC.KeepAlive(yOnly);
			GC.KeepAlive(both);
		}

		[Theory]
		[InlineData(ChartRenderMode.Pixel)]
		[InlineData(ChartRenderMode.Braille)]
		[InlineData(ChartRenderMode.Ascii)]
		public void EveryRenderModeDrawsTheSeries(ChartRenderMode mode)
		{
			// A declared mode that paints nothing is worse than no mode at all: it
			// type-checks, it is documented, and it silently shows an empty chart.
			var (system, chart) = Build();
			chart.RenderMode = mode;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });

			var snap = Draw(system);

			Assert.True(AnyNonBlankCellInPlot(snap, Color.Black), $"{mode} rendered an empty plot");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AxisLinesAreDrawnWhenAsked()
		{
			// The rules along the left and bottom edges. Without them the plot reads
			// as data floating in space rather than sitting in a frame.
			var (system, chart) = Build();
			chart.ShowAxisLines = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			var snap = Draw(system);
			string text = TextOf(snap);

			Assert.Contains("\u2502", text);   // vertical rule, the Y axis
			Assert.Contains("\u2500", text);   // horizontal rule, the X axis
			GC.KeepAlive(system);
		}

		[Fact]
		public void AxisLinesMeetAtACorner()
		{
			var (system, chart) = Build();
			chart.ShowAxisLines = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Contains("\u2514", TextOf(Draw(system)));   // the bottom-left corner
			GC.KeepAlive(system);
		}

		[Fact]
		public void AxisLinesAreOffByDefault()
		{
			// Additive and opt-in: a chart that did not ask keeps the look it had.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.DoesNotContain("\u2514", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AxisLinesDoNotEatThePlot()
		{
			// The rules take a column and a row; the series must still be drawn, not
			// pushed off the edge or overpainted.
			var (system, chart) = Build();
			chart.ShowAxisLines = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 2, 8, 3 });

			Assert.True(AnyCellHas(Draw(system), Color.Red),
				"turning on axis lines lost the series");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AxisLinesSurviveATinyChart()
		{
			var (system, chart) = Build(width: 10, height: 5);
			chart.ShowAxisLines = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
				sb.Append('\n');
			}
			return sb.ToString();
		}

		[Fact]
		public void EachRenderModeUsesItsOwnGlyphs()
		{
			// EveryRenderModeDrawsTheSeries only asks whether SOMETHING appeared, and
			// every mode draws something, so it passed while RenderMode was being
			// ignored entirely. What distinguishes the modes is WHICH glyphs they
			// use, so that is what this checks.
			var (pixel, a) = Build();
			a.RenderMode = ChartRenderMode.Pixel;
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			var pixelGlyphs = GlyphsOf(Draw(pixel));

			var (braille, b) = Build();
			b.RenderMode = ChartRenderMode.Braille;
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			var brailleGlyphs = GlyphsOf(Draw(braille));

			// Half blocks are U+2580; braille lives in U+2800..U+28FF. A mode that
			// paints the other's glyphs is not that mode.
			Assert.Contains(0x2580, pixelGlyphs);
			Assert.True(brailleGlyphs.Any(cp => cp > 0x2800 && cp <= 0x28FF),
				"braille mode drew no braille glyphs");
			Assert.DoesNotContain(0x2580, brailleGlyphs);

			GC.KeepAlive(pixel);
			GC.KeepAlive(braille);
		}

		/// <summary>Every distinct codepoint on screen.</summary>
		private static System.Collections.Generic.HashSet<int> GlyphsOf(CharacterBufferSnapshot snap)
		{
			var seen = new System.Collections.Generic.HashSet<int>();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) seen.Add(snap.GetCell(x, y).Character.Value);
			}
			return seen;
		}

		[Fact]
		public void ABrailleAreaFillIsSolidNotStriped()
		{
			// An area fill must cover every column under the curve. Filling only at
			// each data point's own dot column leaves the columns between them
			// empty, which renders as vertical stripes - the line looks right and
			// the fill is full of holes.
			var (system, chart) = Build();
			chart.RenderMode = ChartRenderMode.Braille;
			chart.PlotType = ChartPlotType.Area;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("s", Color.Cyan1).SetValues(
				System.Linq.Enumerable.Range(0, 40).Select(i => 20.0 + i * 1.5).ToArray());

			var snap = Draw(system);

			// Walk the row just above the floor: under a rising ramp it should be
			// filled from the left edge of the plot to the right with no gaps.
			int deepRow = -1, filled = 0, gaps = 0;
			for (int y = snap.Height - 1; y >= 0 && deepRow < 0; y--)
			{
				int n = 0;
				for (int x = 0; x < snap.Width; x++)
				{
					int cp = snap.GetCell(x, y).Character.Value;
					if (cp > 0x2800 && cp <= 0x28FF) n++;
				}
				if (n > snap.Width / 3) deepRow = y;
			}

			Assert.True(deepRow >= 0, "the area drew no well-filled row");

			bool started = false;
			for (int x = 0; x < snap.Width; x++)
			{
				int cp = snap.GetCell(x, deepRow).Character.Value;
				bool ink = cp > 0x2800 && cp <= 0x28FF;
				if (ink) { started = true; filled++; }
				else if (started) gaps++;
			}

			Assert.True(gaps * 4 < filled,
				$"row {deepRow} has {gaps} empty columns against {filled} filled; "
				+ "the area fill is striped rather than solid");
			GC.KeepAlive(system);
		}

		[Fact]
		public void GridlinesWithNoTicksDoNotThrow()
		{
			// A plot too small for a single tick still has to paint.
			var (system, chart) = Build(width: 14, height: 5);
			chart.YAxis.ShowGridlines = true;
			chart.XAxis.ShowGridlines = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}
	}
}
