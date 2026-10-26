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
	/// How the plot uses its width: stretched to fit, or a fixed step per point.
	/// </summary>
	/// <remarks>
	/// Stretching rescales the whole chart whenever a sample arrives, which is
	/// wrong for a live feed: the trace should march rightward with old samples
	/// staying where they were. SparklineControl works the second way by default
	/// and that behaviour is worth having here.
	/// </remarks>
	public class ChartSizingTests
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

		/// <summary>The rightmost column carrying the series colour.</summary>
		private static int RightEdge(CharacterBufferSnapshot snap, Color c)
		{
			for (int x = snap.Width - 1; x >= 0; x--)
			{
				for (int y = 0; y < snap.Height; y++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) return x;
				}
			}
			return -1;
		}

		[Fact]
		public void StretchIsTheDefault()
		{
			var chart = new ChartControl();

			Assert.Equal(ChartSizing.Stretch, chart.Sizing);
		}

		[Fact]
		public void StretchedPointsSpanTheWholePlot()
		{
			var (few, a) = Build();
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2 });
			int fewEdge = RightEdge(Draw(few), Color.Red);

			var (many, b) = Build();
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4, 9, 2, 6 });
			int manyEdge = RightEdge(Draw(many), Color.Red);

			// Both reach the right edge: the data is rescaled to the width.
			Assert.True(fewEdge > 0 && manyEdge > 0, "nothing drew");
			Assert.True(Math.Abs(fewEdge - manyEdge) <= 2,
				$"stretched plots should both fill the width: {fewEdge} against {manyEdge}");
			GC.KeepAlive(few);
			GC.KeepAlive(many);
		}

		[Fact]
		public void AFixedStepGrowsWithTheData()
		{
			// The live-feed shape: three samples occupy three steps, ten occupy ten,
			// and the chart grows rightward instead of rescaling.
			var (few, a) = Build();
			a.Sizing = ChartSizing.FixedStep;
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2 });
			int fewEdge = RightEdge(Draw(few), Color.Red);

			var (many, b) = Build();
			b.Sizing = ChartSizing.FixedStep;
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4, 9, 2, 6 });
			int manyEdge = RightEdge(Draw(many), Color.Red);

			Assert.True(fewEdge > 0 && manyEdge > 0, "nothing drew");
			Assert.True(manyEdge > fewEdge + 3,
				$"more samples should reach further right: {fewEdge} then {manyEdge}");
			GC.KeepAlive(few);
			GC.KeepAlive(many);
		}

		[Fact]
		public void AppendingASampleExtendsTheTraceInsteadOfRescalingIt()
		{
			// The property that makes a fixed step usable for a live feed. Measured
			// at the RIGHT EDGE, because a line series fills every column between
			// its points, so "which columns are occupied" cannot tell the two
			// sizings apart - an earlier version of this test compared exactly that
			// and passed with the feature switched off.
			var (fixedStep, a) = Build();
			a.Sizing = ChartSizing.FixedStep;
			a.YAxis.Min = 0;
			a.YAxis.Max = 100;
			var sa = a.AddSeries("s", Color.Red);
			sa.SetValues(new[] { 10d, 90, 10, 90 });
			int stepEdgeBefore = RightEdge(Draw(fixedStep), Color.Red);

			sa.AddPoint(new ChartPoint(4, 50));
			sa.AddPoint(new ChartPoint(5, 70));
			a.Invalidate(Invalidation.Relayout);
			int stepEdgeAfter = RightEdge(Draw(fixedStep), Color.Red);

			Assert.True(stepEdgeAfter > stepEdgeBefore,
				$"a fixed step must extend rightward: {stepEdgeBefore} then {stepEdgeAfter}");

			// The contrast: stretched, the same append moves nothing at the edge
			// because the trace already filled the width - it rescales instead.
			var (stretched, b) = Build();
			b.YAxis.Min = 0;
			b.YAxis.Max = 100;
			var sb = b.AddSeries("s", Color.Red);
			sb.SetValues(new[] { 10d, 90, 10, 90 });
			int stretchEdgeBefore = RightEdge(Draw(stretched), Color.Red);

			sb.AddPoint(new ChartPoint(4, 50));
			sb.AddPoint(new ChartPoint(5, 70));
			b.Invalidate(Invalidation.Relayout);
			int stretchEdgeAfter = RightEdge(Draw(stretched), Color.Red);

			Assert.Equal(stretchEdgeBefore, stretchEdgeAfter);

			GC.KeepAlive(fixedStep);
			GC.KeepAlive(stretched);
		}

		[Fact]
		public void TheStepWidthIsConfigurable()
		{
			var (narrow, a) = Build();
			a.Sizing = ChartSizing.FixedStep;
			a.StepWidth = 1;
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8 });
			int narrowEdge = RightEdge(Draw(narrow), Color.Red);

			var (wide, b) = Build();
			b.Sizing = ChartSizing.FixedStep;
			b.StepWidth = 4;
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5, 2, 8 });
			int wideEdge = RightEdge(Draw(wide), Color.Red);

			Assert.True(wideEdge > narrowEdge,
				$"a wider step should reach further: {narrowEdge} against {wideEdge}");
			GC.KeepAlive(narrow);
			GC.KeepAlive(wide);
		}

		[Fact]
		public void FixedStepBarsStaySeparate()
		{
			// Bars size themselves from the slot each point gets. In fixed-step mode
			// that slot is the step, not the plot width divided by the point count -
			// otherwise eight samples on a wide plot each sprawl across a huge slot
			// and merge into one solid block with no sample boundaries visible.
			var (system, chart) = Build();
			chart.Sizing = ChartSizing.FixedStep;
			chart.StepWidth = 3;
			chart.PlotType = ChartPlotType.Bar;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("s", Color.Red).SetValues(new[] { 20d, 60, 40, 80, 50, 90, 45, 70 });

			var snap = Draw(system);

			// Count separate runs of bar columns: eight samples must not read as one.
			int runs = 0;
			bool inBar = false;
			for (int x = 0; x < snap.Width; x++)
			{
				bool here = false;
				for (int y = 0; y < snap.Height && !here; y++)
				{
					var c = snap.GetCell(x, y);
					here = c.Foreground == Color.Red || c.Background == Color.Red
						|| (c.Background.R > c.Background.G + 20 && c.Background.R > c.Background.B + 20);
				}
				if (here && !inBar) runs++;
				inBar = here;
			}

			Assert.True(runs >= 4,
				$"eight fixed-step bars produced {runs} separate runs; they merged into a block");
			GC.KeepAlive(system);
		}

		[Fact]
		public void MoreDataThanWidthDoesNotOverflow()
		{
			// Past the right edge the chart cannot grow further; it must clip
			// rather than paint outside itself or throw.
			var (system, chart) = Build(width: 40, height: 14);
			chart.Sizing = ChartSizing.FixedStep;
			var many = new double[500];
			for (int i = 0; i < many.Length; i++) many[i] = i % 90;
			chart.AddSeries("s", Color.Red).SetValues(many);

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnEmptyFixedStepChartDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.Sizing = ChartSizing.FixedStep;

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		/// <summary>Every column carrying the colour, left to right.</summary>
		private static System.Collections.Generic.List<int> ColumnProfile(
			CharacterBufferSnapshot snap, Color c)
		{
			var columns = new System.Collections.Generic.List<int>();
			for (int x = 0; x < snap.Width; x++)
			{
				for (int y = 0; y < snap.Height; y++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) { columns.Add(x); break; }
				}
			}
			return columns;
		}

		private static int FirstColumnWith(CharacterBufferSnapshot snap, Color c)
		{
			for (int x = 0; x < snap.Width; x++)
			{
				for (int y = 0; y < snap.Height; y++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) return x;
				}
			}
			return -1;
		}
	}
}
