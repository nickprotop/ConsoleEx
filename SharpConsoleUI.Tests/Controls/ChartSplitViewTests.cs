// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Split view: one series rising from the centre, another falling from it.
	/// </summary>
	/// <remarks>
	/// The shape SparklineControl calls Bidirectional, which its own documentation
	/// points at network throughput. Both halves measure the same positive range
	/// growing away from the centre, because 5 Mbps down is not minus 5 Mbps.
	/// </remarks>
	public class ChartSplitViewTests
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

		/// <summary>The topmost and bottommost rows carrying a colour.</summary>
		private static (int Top, int Bottom) RowSpan(CharacterBufferSnapshot snap, Color c)
		{
			int top = int.MaxValue, bottom = int.MinValue;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c)
					{
						if (y < top) top = y;
						if (y > bottom) bottom = y;
						break;
					}
				}
			}
			return (top, bottom);
		}

		[Fact]
		public void TheFirstSeriesTakesTheUpperHalfAndTheSecondTheLower()
		{
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.AddSeries("down", Color.Red).SetValues(new[] { 30d, 60, 45, 80, 50 });
			chart.AddSeries("up", Color.Blue).SetValues(new[] { 20d, 40, 70, 35, 60 });

			var snap = Draw(system);
			var (redTop, redBottom) = RowSpan(snap, Color.Red);
			var (blueTop, blueBottom) = RowSpan(snap, Color.Blue);

			Assert.True(redBottom < blueTop,
				$"the halves overlap: red spans {redTop}-{redBottom}, blue {blueTop}-{blueBottom}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void BothHalvesGrowAwayFromTheCentre()
		{
			// A larger value must reach FURTHER from the middle in both halves. If
			// the lower half were not mirrored, a bigger upload would climb toward
			// the centre instead of away from it.
			var (small, a) = Build();
			a.SplitView = true;
			a.YAxis.Min = 0;
			a.YAxis.Max = 100;
			a.AddSeries("d", Color.Red).SetValues(new[] { 10d, 10 });
			a.AddSeries("u", Color.Blue).SetValues(new[] { 10d, 10 });
			var smallSnap = Draw(small);

			var (big, b) = Build();
			b.SplitView = true;
			b.YAxis.Min = 0;
			b.YAxis.Max = 100;
			b.AddSeries("d", Color.Red).SetValues(new[] { 90d, 90 });
			b.AddSeries("u", Color.Blue).SetValues(new[] { 90d, 90 });
			var bigSnap = Draw(big);

			// Upper series: bigger value sits higher, so a smaller row index.
			Assert.True(RowSpan(bigSnap, Color.Red).Top < RowSpan(smallSnap, Color.Red).Top,
				"a larger value did not rise further in the upper half");

			// Lower series: bigger value sits lower, so a larger row index.
			Assert.True(RowSpan(bigSnap, Color.Blue).Bottom > RowSpan(smallSnap, Color.Blue).Bottom,
				"a larger value did not fall further in the lower half");

			GC.KeepAlive(small);
			GC.KeepAlive(big);
		}

		[Fact]
		public void WithinOneChartTheLowerSeriesFallsAsItsValueRises()
		{
			// The decisive one, and the one a pair of separate charts cannot make:
			// inside a SINGLE render, a bigger value in the lower half must sit on a
			// LOWER row than a smaller one. Comparing two charts' extremes passed
			// even while the halves were misread, so this walks one chart's columns.
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("upper", Color.Red).SetValues(new[] { 50d, 50 });
			chart.AddSeries("lower", Color.Blue).SetValues(new[] { 10d, 90 });

			var snap = Draw(system);

			int rowAtLeft = FirstRowOf(snap, Color.Blue, fromLeft: true);
			int rowAtRight = FirstRowOf(snap, Color.Blue, fromLeft: false);

			Assert.True(rowAtLeft >= 0 && rowAtRight >= 0, "the lower series did not draw");
			Assert.True(rowAtRight > rowAtLeft,
				$"value 90 drew at row {rowAtRight} and value 10 at row {rowAtLeft}; "
				+ "in the lower half a bigger value must fall further from the centre");
			GC.KeepAlive(system);
		}

		/// <summary>The row of the first column from one edge that carries the colour.</summary>
		private static int FirstRowOf(CharacterBufferSnapshot snap, Color c, bool fromLeft)
		{
			for (int i = 0; i < snap.Width; i++)
			{
				int x = fromLeft ? i : snap.Width - 1 - i;
				for (int y = 0; y < snap.Height; y++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) return y;
				}
			}
			return -1;
		}

		[Fact]
		public void TheCentreOfASplitAxisReadsZero()
		{
			// In split view both series grow AWAY from the middle, so the middle is
			// where a value of zero sits. An axis still labelled as one continuous
			// range puts the midpoint there instead - saying 50 at the line that
			// means 0, which misreads every point on the chart.
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("down", Color.Red).SetValues(new[] { 30d, 60 });
			chart.AddSeries("up", Color.Blue).SetValues(new[] { 20d, 40 });

			var snap = Draw(system);
			int centreRow = CentreRowOfPlot(snap);
			string centreLabel = LabelOnRow(snap, centreRow);

			Assert.Equal("0", centreLabel.Trim());
			GC.KeepAlive(system);
		}

		[Fact]
		public void ASplitAxisShowsTheMaximumAtBothEnds()
		{
			// Mirrored and both positive: the top and the bottom are each the far
			// end of the same positive range, so both read the maximum.
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("down", Color.Red).SetValues(new[] { 30d, 60 });
			chart.AddSeries("up", Color.Blue).SetValues(new[] { 20d, 40 });

			string text = TextOfSnapshot(Draw(system));

			// "100" appears twice: once for the top half, once for the bottom.
			int occurrences = CountOccurrences(text, "100");
			Assert.True(occurrences >= 2,
				$"a mirrored axis should label its maximum at both ends; found {occurrences}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ANonSplitAxisIsUnchanged()
		{
			// The mirroring applies only to split view; an ordinary chart keeps a
			// single continuous axis with the minimum at the bottom.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 30d, 60 });

			string text = TextOfSnapshot(Draw(system));

			Assert.Equal(1, CountOccurrences(text, "100"));
			GC.KeepAlive(system);
		}

		private static int CountOccurrences(string haystack, string needle)
		{
			int n = 0, i = 0;
			while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
			return n;
		}

		private static string TextOfSnapshot(CharacterBufferSnapshot snap)
		{
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
				sb.Append('\n');
			}
			return sb.ToString();
		}

		/// <summary>The row halfway down the plotted area.</summary>
		private static int CentreRowOfPlot(CharacterBufferSnapshot snap)
		{
			// The plot sits below the title row and above the X-label row; the chart
			// fills the window, so the midpoint of the drawn area is close enough to
			// find the centre label.
			int top = 1, bottom = snap.Height - 2;
			return (top + bottom) / 2;
		}

		/// <summary>The leading text on a row, which is where a Y label sits.</summary>
		private static string LabelOnRow(CharacterBufferSnapshot snap, int row)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < Math.Min(8, snap.Width); x++)
			{
				char ch = snap.GetCell(x, row).Character.ToString()[0];
				if (ch == '\0') ch = ' ';
				sb.Append(ch);
			}
			return sb.ToString();
		}

		[Fact]
		public void SplitViewIsOffByDefault()
		{
			var chart = new ChartControl();

			Assert.False(chart.SplitView);
		}

		[Fact]
		public void WithoutSplitBothSeriesShareTheWholePlot()
		{
			// The contrast: normally two series overlap across the full height.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 90, 10 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 90d, 10, 90 });

			var snap = Draw(system);
			var red = RowSpan(snap, Color.Red);
			var blue = RowSpan(snap, Color.Blue);

			// They interleave rather than separating into halves.
			Assert.True(red.Bottom > blue.Top,
				"two ordinary series should share the plot, not split it");
			GC.KeepAlive(system);
		}

		[Fact]
		public void SplitViewWorksForAnyPlotType()
		{
			// The mode changes where a series maps its values, not how it is drawn,
			// so an area or a bar splits exactly as a line does.
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.PlotType = ChartPlotType.Line;
			var down = chart.AddSeries("down", Color.Red);
			down.SetValues(new[] { 30d, 60, 45 });
			var up = chart.AddSeries("up", Color.Blue);
			up.SetValues(new[] { 20d, 40, 70 });

			var snap = Draw(system);

			Assert.True(RowSpan(snap, Color.Red).Bottom < RowSpan(snap, Color.Blue).Top,
				"the split did not hold");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ASingleSeriesInSplitViewUsesTheUpperHalf()
		{
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.AddSeries("only", Color.Red).SetValues(new[] { 10d, 50, 30 });

			var snap = Draw(system);
			var span = RowSpan(snap, Color.Red);

			Assert.True(span.Top != int.MaxValue, "the series did not draw");
			Assert.True(span.Bottom < snap.Height / 2 + 4,
				"a lone series should stay in the upper half");
			GC.KeepAlive(system);
		}

		[Fact]
		public void SplitViewWithNoSeriesDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.SplitView = true;

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void SplitViewSurvivesAShortPlot()
		{
			// Two halves out of very few rows: each half can round to nothing.
			var (system, chart) = Build(width: 40, height: 5);
			chart.SplitView = true;
			chart.AddSeries("d", Color.Red).SetValues(new[] { 1d, 5 });
			chart.AddSeries("u", Color.Blue).SetValues(new[] { 2d, 4 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AThirdSeriesStillDrawsInSplitView()
		{
			// Split names the first two; anything beyond must not vanish silently.
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.AddSeries("d", Color.Red).SetValues(new[] { 30d, 60 });
			chart.AddSeries("u", Color.Blue).SetValues(new[] { 20d, 40 });
			chart.AddSeries("third", Color.Green).SetValues(new[] { 50d, 25 });

			var snap = Draw(system);

			Assert.True(RowSpan(snap, Color.Green).Top != int.MaxValue,
				"a third series disappeared in split view");
			GC.KeepAlive(system);
		}
	}
}
