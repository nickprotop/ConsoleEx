// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>The legend: which series are on the chart, and their colours.</summary>
	public class ChartLegendTests
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

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
				sb.Append('\n');
			}
			return sb.ToString();
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

		[Fact]
		public void EverySeriesNameAppears()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.AddSeries("alpha", Color.Red).SetValues(new[] { 1d, 5 });
			chart.AddSeries("beta", Color.Blue).SetValues(new[] { 2d, 4 });

			string text = TextOf(Draw(system));

			Assert.Contains("alpha", text);
			Assert.Contains("beta", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void NoLegendWhenNotAsked()
		{
			var (system, chart) = Build();
			chart.AddSeries("alpha", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.DoesNotContain("alpha", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void PositionNoneSuppressesItEvenWhenShown()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.LegendPosition = ChartLegendPosition.None;
			chart.AddSeries("alpha", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.DoesNotContain("alpha", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AHiddenSeriesStaysInTheLegendButLeavesThePlot()
		{
			// The legend is how a series gets turned back on, so it must list one
			// that is off - otherwise hiding it makes it unreachable.
			var (system, chart) = Build();
			chart.ShowLegend = true;
			var s = chart.AddSeries("ghost", Color.Magenta1);
			s.SetValues(new[] { 1d, 5, 2 });
			s.IsVisible = false;

			var snap = Draw(system);

			Assert.Contains("ghost", TextOf(snap));
			Assert.False(AnyCellHas(snap, Color.Magenta1), "a hidden series was still plotted");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheLegendDoesNotOverflowANarrowChart()
		{
			var (system, chart) = Build(width: 24, height: 12);
			chart.ShowLegend = true;
			chart.AddSeries("a-very-long-series-name-indeed", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheLegendDoesNotEatThePlot()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 2, 8 });

			Assert.True(AnyCellHas(Draw(system), Color.Red),
				"turning on the legend lost the series");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ALegendWithNoSeriesDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}
		// --- side legends ---

		/// <summary>
		/// The leftmost and rightmost columns the PLOT draws the series in.
		/// </summary>
		/// <remarks>
		/// Skips the legend swatch, which carries the same colour: counting it
		/// would put the trace's left edge at the legend and make a side legend
		/// look like it overlapped the plot it sits beside.
		/// </remarks>
		private static (int Left, int Right) PlotExtent(CharacterBufferSnapshot snap, Color c)
		{
			int left = int.MaxValue, right = -1;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground != c && cell.Background != c) continue;
					if (cell.Character.Value == '\u25a0') continue;   // the legend swatch
					if (x < left) left = x;
					if (x > right) right = x;
				}
			}
			return (left == int.MaxValue ? -1 : left, right);
		}

		/// <summary>The row a text run sits on, or -1.</summary>
		private static int RowOf(CharacterBufferSnapshot snap, string text)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x + text.Length <= snap.Width; x++)
				{
					bool hit = true;
					for (int i = 0; i < text.Length && hit; i++)
						hit = snap.GetCell(x + i, y).Character.ToString() == text[i].ToString();
					if (hit) return y;
				}
			}
			return -1;
		}

		/// <summary>The column a text run starts at, or -1.</summary>
		private static int ColOf(CharacterBufferSnapshot snap, string text)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x + text.Length <= snap.Width; x++)
				{
					bool hit = true;
					for (int i = 0; i < text.Length && hit; i++)
						hit = snap.GetCell(x + i, y).Character.ToString() == text[i].ToString();
					if (hit) return x;
				}
			}
			return -1;
		}

		[Theory]
		[InlineData(ChartLegendPosition.Left)]
		[InlineData(ChartLegendPosition.Right)]
		public void ASideLegendListsEverySeriesOnItsOwnRow(ChartLegendPosition position)
		{
			// Left and Right were named in the enum and reserved no space, and
			// PaintLegend only ever drew Top or Bottom - so both silently fell
			// through to Bottom and a caller asking for a side legend got a
			// bottom one. A side legend stacks its entries vertically, one row
			// each, which is the whole reason to want one.
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.LegendPosition = position;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("alpha", Color.Red).SetValues(new[] { 10d, 50, 30 });
			chart.AddSeries("beta", Color.Blue).SetValues(new[] { 20d, 40, 60 });

			var snap = Draw(system);

			int alphaRow = RowOf(snap, "alpha");
			int betaRow = RowOf(snap, "beta");

			Assert.True(alphaRow >= 0, "the first series is not listed");
			Assert.True(betaRow >= 0, "the second series is not listed");
			Assert.True(betaRow == alphaRow + 1,
				$"a side legend stacks its entries: alpha on row {alphaRow}, beta on {betaRow}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ALeftLegendSitsLeftOfThePlot()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.LegendPosition = ChartLegendPosition.Left;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("alpha", Color.Red).SetValues(new[] { 10d, 50, 30 });

			var snap = Draw(system);

			// The entry text starts left of where the trace is drawn.
			int label = ColOf(snap, "alpha");
			var (traceLeft, _) = PlotExtent(snap, Color.Red);

			Assert.True(label >= 0, "the series is not listed");
			Assert.True(label < traceLeft,
				$"the legend is at column {label} but the plot starts at {traceLeft}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ARightLegendSitsRightOfThePlot()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.LegendPosition = ChartLegendPosition.Right;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("alpha", Color.Red).SetValues(new[] { 10d, 50, 30 });

			var snap = Draw(system);

			int label = ColOf(snap, "alpha");
			var (_, traceRight) = PlotExtent(snap, Color.Red);

			Assert.True(label >= 0, "the series is not listed");
			Assert.True(label > traceRight,
				$"the legend is at column {label} but the plot ends at {traceRight}");
			GC.KeepAlive(system);
		}

		[Theory]
		[InlineData(ChartLegendPosition.Left)]
		[InlineData(ChartLegendPosition.Right)]
		public void ASideLegendTakesColumnsFromThePlotNotRows(ChartLegendPosition position)
		{
			// The point of a side legend: it costs width, not height. A chart that
			// reserved a row for it as well would lose a row of plot for nothing.
			var (withSide, a) = Build();
			a.ShowLegend = true;
			a.LegendPosition = position;
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("alpha", Color.Red).SetValues(new[] { 0d, 100 });

			var (noLegend, b) = Build();
			b.ShowLegend = false;
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			b.AddSeries("alpha", Color.Red).SetValues(new[] { 0d, 100 });

			var sideSnap = Draw(withSide);
			var plainSnap = Draw(noLegend);

			Assert.Equal(PlotRows(plainSnap, Color.Red), PlotRows(sideSnap, Color.Red));

			// ...and it DID cost width, or it is not drawn beside the plot at all.
			var (sl, sr) = PlotExtent(sideSnap, Color.Red);
			var (pl, pr) = PlotExtent(plainSnap, Color.Red);
			Assert.True((sr - sl) < (pr - pl),
				$"the plot is {sr - sl} wide with a side legend and {pr - pl} without");
			GC.KeepAlive(withSide);
			GC.KeepAlive(noLegend);
		}

		[Fact]
		public void ALeftLegendDoesNotCollideWithTheYLabels()
		{
			// The Y labels right-align against plot.X, and a Left legend sits in
			// the columns immediately before it - so the labels were written over
			// the legend text and the two ran together ("requests0").
			var (system, chart) = Build();
			chart.ShowLegend = true;
			chart.LegendPosition = ChartLegendPosition.Left;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.YAxis.ShowLabels = true;
			chart.AddSeries("requests", Color.Red).SetValues(new[] { 10d, 50, 30 });

			string text = TextOf(Draw(system));

			Assert.Contains("requests", text);
			Assert.DoesNotContain("requests0", text);
			Assert.DoesNotContain("requests1", text);
			GC.KeepAlive(system);
		}

		private static int PlotRows(CharacterBufferSnapshot snap, Color c)
		{
			int rows = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) { rows++; break; }
				}
			}
			return rows;
		}


	}
}
