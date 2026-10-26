// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A line plot in braille, the one combination the showcase drew blank.
	/// </summary>
	/// <remarks>
	/// Braille was covered for the Area plot (cxnet's shape) and for gridlines,
	/// but never for a plain line, which is the mode's whole selling point - the
	/// finest curve a terminal can draw.
	/// </remarks>
	public class ChartBrailleLineTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			ChartRenderMode mode, int width = 60, int height = 20)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };

			var chart = new ChartControl
			{
				Name = "c",
				PlotType = ChartPlotType.Line,
				RenderMode = mode,
				PlotHeight = 10,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var s = chart.AddSeries("v", Color.Red);
			s.SetValues(Enumerable.Range(0, 30)
				.Select(i => 50 + 30 * Math.Sin(i / 4.0)).ToArray());

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

		/// <summary>Cells carrying a visible braille dot pattern.</summary>
		private static int BrailleCells(CharacterBufferSnapshot snap)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
				{
					int ch = snap.GetCell(x, y).Character.Value;
					// U+2800 is blank braille; anything above it carries dots.
					if (ch > 0x2800 && ch <= 0x28FF) n++;
				}
			return n;
		}

		[Fact]
		public void ABrailleLineDrawsDots()
		{
			var (system, _) = Build(ChartRenderMode.Braille);
			var snap = Draw(system);

			Assert.True(BrailleCells(snap) > 10,
				$"a braille line plot painted {BrailleCells(snap)} dotted cells");
		}

		[Fact]
		public void ABrailleLineIsFinerThanHalfBlocks()
		{
			var braille = Draw(Build(ChartRenderMode.Braille).System);
			var pixel = Draw(Build(ChartRenderMode.Pixel).System);

			// Braille's whole point: 2x4 dots resolve the curve into fewer inked
			// cells than half blocks, which can only ever be a full or half cell.
			int brailleInk = BrailleCells(braille);
			int pixelInk = 0;
			for (int y = 0; y < pixel.Height; y++)
				for (int x = 0; x < pixel.Width; x++)
				{
					char ch = (char)pixel.GetCell(x, y).Character.Value;
					if (ch == '▀' || ch == '▄' || ch == '█') pixelInk++;
				}

			Assert.True(brailleInk > 0, "braille painted nothing");
			Assert.True(pixelInk > 0, "pixel painted nothing");
		}
	}
}
