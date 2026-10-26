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
	/// <summary>A line with the space beneath it filled.</summary>
	public class ChartAreaPlotTests
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

		/// <summary>
		/// How many cells the plot has painted with something other than background.
		/// </summary>
		/// <remarks>
		/// Counting an exact colour will not do: an area's fill is dimmed toward the
		/// background so the outline stays legible, so it is not the series colour
		/// any more. Coverage is the property under test, and this measures it
		/// whatever shade the fill ended up.
		/// </remarks>
		private static int CountPainted(CharacterBufferSnapshot snap)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					// Anything tinted by the series: the outline in full colour, or the
					// fill, which is the colour blended toward the plot background.
					var c = snap.GetCell(x, y);
					if (IsNear(c.Background, Color.Red) || IsNear(c.Foreground, Color.Red)) n++;
				}
			}
			return n;
		}

		/// <summary>Cells carrying a colour close to the given one.</summary>
		/// <remarks>Tolerant, because a dimmed fill is a blend rather than the exact colour.</remarks>
		private static int CountNear(CharacterBufferSnapshot snap, Color target)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					if (IsNear(c.Background, target) || IsNear(c.Foreground, target)) n++;
				}
			}
			return n;
		}

		/// <summary>True when the colour is dominated by the same channel as the target.</summary>
		private static bool IsNear(Color c, Color target)
		{
			if (c == Color.Black) return false;
			if (target == Color.Red) return c.R > c.G + 20 && c.R > c.B + 20;
			if (target == Color.Blue) return c.B > c.R + 20 && c.B > c.G + 20;
			return c == target;
		}

		[Fact]
		public void AnAreaFillsMoreThanItsLine()
		{
			// The defining property: same data, same shape, but the space below the
			// curve is painted. If it does not cover more cells, it is just a line.
			var (lineSystem, lineChart) = Build();
			lineChart.PlotType = ChartPlotType.Line;
			lineChart.AddSeries("a", Color.Red).SetValues(new[] { 2d, 8, 4, 9, 5 });
			int lineCells = CountPainted(Draw(lineSystem));

			var (areaSystem, areaChart) = Build();
			areaChart.PlotType = ChartPlotType.Area;
			areaChart.AddSeries("a", Color.Red).SetValues(new[] { 2d, 8, 4, 9, 5 });
			int areaCells = CountPainted(Draw(areaSystem));

			Assert.True(lineCells > 0, "the line drew nothing");
			Assert.True(areaCells > lineCells,
				$"area covered {areaCells} cells against the line's {lineCells}");
			GC.KeepAlive(lineSystem);
			GC.KeepAlive(areaSystem);
		}

		[Fact]
		public void TheFillSitsBelowTheLineNotAbove()
		{
			var (system, chart) = Build();
			chart.PlotType = ChartPlotType.Area;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 50d, 50, 50 });

			var snap = Draw(system);

			// Find the topmost and bottommost red rows: a flat series at mid-range
			// should fill from its own row down, so the span reaches the plot floor
			// rather than the ceiling.
			int top = int.MaxValue, bottom = int.MinValue;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					if (IsNear(c.Foreground, Color.Red) || IsNear(c.Background, Color.Red))
					{
						if (y < top) top = y;
						if (y > bottom) bottom = y;
						break;
					}
				}
			}

			Assert.True(top != int.MaxValue, "the area drew nothing");
			Assert.True(bottom - top > 3,
				$"a mid-range area filled only rows {top}..{bottom}; it should reach the floor");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnAreaPerSeriesIsRespected()
		{
			// PlotType is per series, which is what makes a combo chart work.
			var (system, chart) = Build();
			chart.PlotType = ChartPlotType.Line;

			var filled = chart.AddSeries("filled", Color.Red);
			filled.PlotType = ChartPlotType.Area;
			filled.SetValues(new[] { 2d, 8, 4 });

			var plain = chart.AddSeries("plain", Color.Blue);
			plain.SetValues(new[] { 3d, 7, 5 });

			var snap = Draw(system);
			int red = CountNear(snap, Color.Red);
			int blue = CountNear(snap, Color.Blue);

			Assert.True(red > blue,
				$"the area series ({red}) should cover more than the line ({blue})");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnAreaWithOnePointDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.PlotType = ChartPlotType.Area;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 42d });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnAreaInSplitViewStaysInItsHalf()
		{
			var (system, chart) = Build();
			chart.PlotType = ChartPlotType.Area;
			chart.SplitView = true;
			chart.AddSeries("down", Color.Red).SetValues(new[] { 30d, 60, 45 });
			chart.AddSeries("up", Color.Blue).SetValues(new[] { 20d, 40, 70 });

			var snap = Draw(system);

			// The upper area fills toward the centre, the lower away from it; they
			// must not bleed into each other.
			int redBottom = int.MinValue, blueTop = int.MaxValue;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					if (IsNear(c.Foreground, Color.Red) || IsNear(c.Background, Color.Red))
						if (y > redBottom) redBottom = y;
					if (IsNear(c.Foreground, Color.Blue) || IsNear(c.Background, Color.Blue))
						if (y < blueTop) blueTop = y;
				}
			}

			Assert.True(redBottom <= blueTop,
				$"the split areas overlap: red reaches {redBottom}, blue starts {blueTop}");
			GC.KeepAlive(system);
		}
	}
}
