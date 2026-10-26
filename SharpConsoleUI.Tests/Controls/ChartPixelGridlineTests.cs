// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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
	/// Gridlines in the pixel render path.
	/// </summary>
	/// <remarks>
	/// The glyph path dots its gridlines; the pixel path inked every pixel along
	/// the line, so the same chart drew a SOLID bar across the plot. At the top
	/// of the range that bar sits one space from the axis label, which reads as
	/// the label running into the plot. Context should not out-weigh content.
	/// </remarks>
	public class ChartPixelGridlineTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(70, 22);
			var window = new Window(system) { Left = 0, Top = 0, Width = 70, Height = 22 };

			var chart = new ChartControl
			{
				Name = "c",
				PlotType = ChartPlotType.Line,
				RenderMode = ChartRenderMode.Pixel,
				PlotHeight = 12,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.YAxis.ShowGridlines = true;

			chart.AddSeries("v", Color.Green)
				.SetValues(Enumerable.Repeat(50d, 20).ToArray());

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

		/// <summary>The longest unbroken run of gridline-coloured cells in any row.</summary>
		private static int LongestRun(CharacterBufferSnapshot snap)
		{
			int best = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				int run = 0;
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					bool grid = c.Foreground == ControlDefaults.ChartGridlineColor
						|| c.Background == ControlDefaults.ChartGridlineColor;
					run = grid ? run + 1 : 0;
					if (run > best) best = run;
				}
			}
			return best;
		}

		[Fact]
		public void APixelGridlineIsBrokenRatherThanSolid()
		{
			var snap = Draw(Build().System);

			// A dashed line still spans the plot, but never as one unbroken block:
			// a run longer than a few cells means it went back to a solid bar.
			Assert.True(LongestRun(snap) <= ControlDefaults.ChartGridlineDashRun + 1,
				$"a pixel gridline painted an unbroken run of {LongestRun(snap)} cells");
		}

		[Fact]
		public void APixelGridlineIsStillDrawn()
		{
			var snap = Draw(Build().System);

			int cells = 0;
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					if (c.Foreground == ControlDefaults.ChartGridlineColor
						|| c.Background == ControlDefaults.ChartGridlineColor) cells++;
				}

			Assert.True(cells > 5, $"gridlines painted only {cells} cells");
		}
	}
}
