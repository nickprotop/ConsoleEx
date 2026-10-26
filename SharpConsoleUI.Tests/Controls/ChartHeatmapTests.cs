// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
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
	/// A grid of values where colour carries the magnitude.
	/// </summary>
	/// <remarks>
	/// Every other plot type answers "what is the value at X", mapping one
	/// dimension of data onto one of screen, with colour as decoration. A
	/// heatmap answers "what is the value at X,Y" - the one question the
	/// control could not express. Colour stops being decoration and becomes
	/// the value channel.
	/// </remarks>
	public class ChartHeatmapTests
	{
		private static double[,] Ramp(int cols, int rows)
		{
			var g = new double[rows, cols];
			for (int y = 0; y < rows; y++)
				for (int x = 0; x < cols; x++)
					g[y, x] = (double)(x + y) / (cols + rows - 2) * 100;
			return g;
		}

		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			ChartRenderMode mode = ChartRenderMode.Pixel, int cols = 16, int rows = 8)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 20);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };

			var chart = new ChartControl
			{
				Name = "h",
				PlotType = ChartPlotType.Heatmap,
				RenderMode = mode,
				PlotHeight = 10,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var s = chart.AddSeries("grid", Color.White);
			s.Gradient = ColorGradient.Predefined["spectrum"];
			s.SetGrid(Ramp(cols, rows));

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
		/// Colours inside the plot only.
		/// </summary>
		/// <remarks>
		/// Counting the whole buffer sweeps in the window background, the default
		/// foreground and the axis labels, which are the same three colours on
		/// every chart - a flat one-colour heatmap then reads as four and the
		/// count says nothing about the data.
		/// </remarks>
		private static int DistinctColours(CharacterBufferSnapshot snap, ChartControl chart)
		{
			var chrome = new HashSet<(byte, byte, byte)>
			{
				(38, 38, 38), (255, 255, 255), (238, 238, 238), (0, 0, 0),
			};

			var seen = new HashSet<(byte, byte, byte)>();
			int bottom = Math.Min(snap.Height, chart.PlotHeight!.Value + 1);
			for (int y = 0; y < bottom; y++)
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					foreach (var col in new[] { c.Foreground, c.Background })
					{
						var k = (col.R, col.G, col.B);
						if (!chrome.Contains(k)) seen.Add(k);
					}
				}
			return seen.Count;
		}

		[Fact]
		public void AGridIsStoredWithoutDisturbingThePoints()
		{
			// The grid is its own storage, so the line/area/bar paths never see it
			// and cannot be destabilised by a heatmap series sitting beside them.
			var s = new ChartControl { Name = "c" }.AddSeries("g", Color.White);
			s.SetGrid(Ramp(4, 3));

			Assert.Empty(s.Points);
			Assert.Equal(4, s.GridWidth);
			Assert.Equal(3, s.GridHeight);
			Assert.True(s.HasGrid);
		}

		[Fact]
		public void ASparseGridFillsOnlyTheCellsGiven()
		{
			var s = new ChartControl { Name = "c" }.AddSeries("g", Color.White);
			s.SetGrid(new[] { (0, 0, 5d), (2, 1, 9d) });

			Assert.Equal(3, s.GridWidth);
			Assert.Equal(2, s.GridHeight);
			Assert.Equal(5d, s.GridValue(0, 0));
			Assert.Equal(9d, s.GridValue(2, 1));
			Assert.Null(s.GridValue(1, 0));
		}

		[Fact]
		public void AHeatmapPaintsManyColoursAcrossTheGrid()
		{
			var (system, chart) = Build();
			var snap = Draw(system);

			// A ramp across a spectrum gradient should produce a broad spread, not
			// the two or three colours a line plot would.
			int n = DistinctColours(snap, chart);
			Assert.True(n > 8, $"a heatmap painted only {n} distinct colours");
		}

		[Fact]
		public void AHeatmapColoursByValueNotPosition()
		{
			// Equal values must take equal colours wherever they sit, which is what
			// makes the colour readable as a number.
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 20);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };
			var chart = new ChartControl
			{
				Name = "h",
				PlotType = ChartPlotType.Heatmap,
				PlotHeight = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var s = chart.AddSeries("flat", Color.White);
			s.Gradient = ColorGradient.Predefined["spectrum"];
			var flat = new double[4, 8];
			for (int y = 0; y < 4; y++)
				for (int x = 0; x < 8; x++) flat[y, x] = 50;
			s.SetGrid(flat);

			window.AddControl(chart);
			system.AddWindow(window);

			var snap = Draw(system);

			// One value everywhere means one colour everywhere: a position-coloured
			// grid would fan out instead.
			int n = DistinctColours(snap, chart);
			Assert.True(n <= 2, $"a flat grid produced {n} colours");
		}

		[Fact]
		public void AHeatmapIgnoresBrailleAndDrawsInPixels()
		{
			// Braille resolves a LINE finely by packing 2x4 dots into a cell, but
			// it can only colour them one foreground at a time. A heatmap has no
			// line to trace and nothing but colour to say, so the dots buy it
			// nothing while the single foreground would cost it half the picture.
			// It takes the pixel path whatever the mode says.
			var (pixSys, pixChart) = Build(ChartRenderMode.Pixel);
			var (brSys, brChart) = Build(ChartRenderMode.Braille);

			int pixel = DistinctColours(Draw(pixSys), pixChart);
			int braille = DistinctColours(Draw(brSys), brChart);

			Assert.True(braille > 8,
				$"a braille-mode heatmap painted only {braille} colours");
			Assert.Equal(pixel, braille);
		}

		[Fact]
		public void DegenerateGridsDoNotThrow()
		{
			// A single cell, a single row, an empty sparse set: all reachable from
			// real data (one reading so far) and all easy to divide by zero on.
			var chart = new ChartControl { Name = "c" };

			var one = chart.AddSeries("one", Color.White);
			one.SetGrid(new double[,] { { 42 } });
			Assert.Equal(1, one.GridWidth);
			Assert.Equal(1, one.GridHeight);

			var row = chart.AddSeries("row", Color.White);
			row.SetGrid(new double[,] { { 1, 2, 3 } });
			Assert.Equal(3, row.GridWidth);
			Assert.Equal(1, row.GridHeight);

			var none = chart.AddSeries("none", Color.White);
			none.SetGrid(Array.Empty<(int, int, double)>());
			Assert.False(none.HasGrid);
		}

		[Fact]
		public void NonFiniteValuesBecomeHolesRatherThanCrashing()
		{
			var s = new ChartControl { Name = "c" }.AddSeries("g", Color.White);
			s.SetGrid(new[,] { { 1d, double.NaN }, { double.PositiveInfinity, 4d } });

			Assert.Equal(1d, s.GridValue(0, 0));
			Assert.Null(s.GridValue(1, 0));
			Assert.Null(s.GridValue(0, 1));
			Assert.Equal(4d, s.GridValue(1, 1));
		}

		[Fact]
		public void AHeatmapWithNoGridFallsBackToTheEmptyState()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 16);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };
			var chart = new ChartControl
			{
				Name = "h",
				PlotType = ChartPlotType.Heatmap,
				PlotHeight = 8,
				EmptyText = "no grid",
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			window.AddControl(chart);
			system.AddWindow(window);

			var snap = Draw(system);
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
					sb.Append((char)snap.GetCell(x, y).Character.Value);

			Assert.Contains("no grid", sb.ToString());
		}
	}
}
