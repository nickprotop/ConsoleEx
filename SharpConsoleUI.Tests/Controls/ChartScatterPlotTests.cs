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
	/// <summary>Unconnected marks, one per point.</summary>
	public class ChartScatterPlotTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 96, int height = 30)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				PlotType = ChartPlotType.Scatter,
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

		private static bool IsNear(Color c, Color target)
		{
			if (c == Color.Black) return false;
			if (target == Color.Red) return c.R > c.G + 20 && c.R > c.B + 20;
			return c == target;
		}

		private static bool CellHas(CharacterBufferSnapshot snap, int x, int y, Color t)
		{
			if ((uint)x >= (uint)snap.Width || (uint)y >= (uint)snap.Height) return false;
			var c = snap.GetCell(x, y);
			return IsNear(c.Foreground, t) || IsNear(c.Background, t);
		}

		private static int CountColumnsWith(CharacterBufferSnapshot snap, Color t)
		{
			int n = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				for (int y = 0; y < snap.Height; y++)
				{
					if (CellHas(snap, x, y, t)) { n++; break; }
				}
			}
			return n;
		}

		[Fact]
		public void ScatterLeavesTheSpaceBetweenPointsEmpty()
		{
			// The whole difference from a line plot. If the gap fills in, scatter is
			// a line with extra steps.
			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 100;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetPoints(new[]
			{
				new ChartPoint(0, 0),
				new ChartPoint(100, 100),
			});

			var snap = Draw(system);

			// Two marks over a wide plot should touch only a handful of columns; a
			// connecting line would touch nearly all of them.
			int columns = CountColumnsWith(snap, Color.Red);

			Assert.True(columns > 0, "scatter drew nothing");
			Assert.True(columns < snap.Width / 3,
				$"scatter touched {columns} of {snap.Width} columns; its points are connected");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ALineOverTheSameDataTouchesFarMoreColumns()
		{
			// The contrast, measured: same two points, drawn as a line.
			var (scatterSystem, scatterChart) = Build();
			scatterChart.XAxis.Min = 0; scatterChart.XAxis.Max = 100;
			scatterChart.YAxis.Min = 0; scatterChart.YAxis.Max = 100;
			scatterChart.AddSeries("a", Color.Red).SetPoints(new[]
			{
				new ChartPoint(0, 0), new ChartPoint(100, 100),
			});
			int scatterCols = CountColumnsWith(Draw(scatterSystem), Color.Red);

			var (lineSystem, lineChart) = Build();
			lineChart.PlotType = ChartPlotType.Line;
			lineChart.XAxis.Min = 0; lineChart.XAxis.Max = 100;
			lineChart.YAxis.Min = 0; lineChart.YAxis.Max = 100;
			lineChart.AddSeries("a", Color.Red).SetPoints(new[]
			{
				new ChartPoint(0, 0), new ChartPoint(100, 100),
			});
			int lineCols = CountColumnsWith(Draw(lineSystem), Color.Red);

			Assert.True(lineCols > scatterCols * 3,
				$"a line touched {lineCols} columns and scatter {scatterCols}; too close to distinguish");
			GC.KeepAlive(scatterSystem);
			GC.KeepAlive(lineSystem);
		}

		[Fact]
		public void IrregularXSpacingPositionsByValue()
		{
			// Scatter's reason to exist: X carries information, so points bunch where
			// the data bunches rather than spreading evenly by index.
			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetPoints(new[]
			{
				new ChartPoint(1, 50),
				new ChartPoint(2, 50),
				new ChartPoint(3, 50),
				new ChartPoint(99, 50),
			});

			var snap = Draw(system);

			// Three points crowd the left edge and one sits far right, so the marks
			// must be lopsided rather than evenly spread.
			int leftHalf = 0, rightHalf = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				bool hit = false;
				for (int y = 0; y < snap.Height && !hit; y++) hit = CellHas(snap, x, y, Color.Red);
				if (!hit) continue;
				if (x < snap.Width / 2) leftHalf++; else rightHalf++;
			}

			Assert.True(leftHalf > rightHalf,
				$"points bunched at low X should crowd the left: {leftHalf} left, {rightHalf} right");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TwoPointsAtTheSamePlaceDoNotThrow()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetPoints(new[]
			{
				new ChartPoint(5, 5), new ChartPoint(5, 5),
			});

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AMarkAtThePlotEdgeDoesNotThrow()
		{
			// The mark is a small plus, so a point on the boundary writes outside it.
			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 10;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 10;
			chart.AddSeries("a", Color.Red).SetPoints(new[]
			{
				new ChartPoint(0, 0), new ChartPoint(10, 10),
			});

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}
	}
}
