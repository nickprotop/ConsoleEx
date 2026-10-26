// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A legend for a series whose colour carries a value.
	/// </summary>
	/// <remarks>
	/// A swatch legend shows ONE colour per series. A gradient series spans
	/// twenty, so the swatch picks one of them and implies the rest do not
	/// exist - the legend is mildly lying. Where colour is the value channel,
	/// the legend has to be a scale.
	/// </remarks>
	public class ChartColorBarLegendTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			ChartLegendStyle style)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(70, 20);
			var window = new Window(system) { Left = 0, Top = 0, Width = 70, Height = 20 };

			var chart = new ChartControl
			{
				Name = "c",
				PlotType = ChartPlotType.Area,
				PlotHeight = 10,
				ShowLegend = true,
				LegendStyle = style,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var s = chart.AddSeries("load", Color.White);
			s.Gradient = ColorGradient.Predefined["spectrum"];
			s.GradientAxis = ChartGradientAxis.Y;
			s.SetValues(Enumerable.Range(0, 20).Select(i => (double)(i * 5)).ToArray());

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

		/// <summary>
		/// The legend row: directly under the plot and its X labels, which is not
		/// the bottom of the buffer - the control is shorter than the window.
		/// </summary>
		/// <remarks>
		/// With no title the plot starts at the top row, so the legend lands one
		/// row past the plot plus its X-label row.
		/// </remarks>
		private static int LegendRow(ChartControl chart) =>
			chart.PlotHeight!.Value + 1;

		/// <summary>Distinct colours across the legend row.</summary>
		private static int LegendColours(CharacterBufferSnapshot snap, int row)
		{
			var seen = new HashSet<(byte, byte, byte)>();
			for (int x = 0; x < snap.Width; x++)
			{
				var c = snap.GetCell(x, row);
				foreach (var col in new[] { c.Foreground, c.Background })
					if (col.R + col.G + col.B > 60) seen.Add((col.R, col.G, col.B));
			}
			return seen.Count;
		}

		private static string RowText(CharacterBufferSnapshot snap, int row)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < snap.Width; x++)
				sb.Append((char)snap.GetCell(x, row).Character.Value);
			return sb.ToString();
		}

		[Fact]
		public void AColorBarShowsTheWholeScaleNotOneSwatch()
		{
			var (swatchSys, swatchChart) = Build(ChartLegendStyle.Swatches);
			var (barSys, barChart) = Build(ChartLegendStyle.ColorBar);

			int swatches = LegendColours(Draw(swatchSys), LegendRow(swatchChart));
			int bar = LegendColours(Draw(barSys), LegendRow(barChart));

			Assert.True(bar > swatches + 3,
				$"colour bar showed {bar} colours, swatch legend showed {swatches}");
		}

		[Fact]
		public void AColorBarIsLabelledWithItsRange()
		{
			var (system, chart) = Build(ChartLegendStyle.ColorBar);
			string text = RowText(Draw(system), LegendRow(chart));

			// Without its ends a colour bar is decoration: the reader cannot turn
			// a colour back into a number.
			Assert.Contains("0", text);
			Assert.Contains("100", text);
		}

		[Fact]
		public void SwatchesRemainTheDefault()
		{
			var chart = new ChartControl { Name = "d" };
			Assert.Equal(ChartLegendStyle.Swatches, chart.LegendStyle);
		}
	}
}
