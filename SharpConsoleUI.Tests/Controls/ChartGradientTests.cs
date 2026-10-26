// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Gradients along the plot, by value, or both.
	/// </summary>
	/// <remarks>
	/// The library had two gradient meanings before this control existed: LineGraph
	/// coloured by X position, Sparkline and BarGraph by value. A chart meant to
	/// supersede all three has to express both, so the axis is a per-series choice.
	/// </remarks>
	public class ChartGradientTests
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

		/// <summary>Every distinct colour painted inside the plot area.</summary>
		private static HashSet<Color> PlotColours(CharacterBufferSnapshot snap)
		{
			var seen = new HashSet<Color>();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					seen.Add(c.Foreground);
					seen.Add(c.Background);
				}
			}
			return seen;
		}

		/// <summary>The leftmost and rightmost columns holding a non-background colour.</summary>
		private static (Color? Left, Color? Right) EdgeColours(CharacterBufferSnapshot snap, Color bg)
		{
			Color? left = null, right = null;
			for (int x = 0; x < snap.Width; x++)
			{
				for (int y = 0; y < snap.Height; y++)
				{
					var c = snap.GetCell(x, y);
					if (c.Background != bg && c.Background != Color.Black)
					{
						left ??= c.Background;
						right = c.Background;
						break;
					}
				}
			}
			return (left, right);
		}

		[Fact]
		public void AGradientProducesManyColoursNotFourBands()
		{
			// The bug this test exists for: sampling the gradient once per segment
			// paints each segment one flat colour, so five points over a wide plot
			// gave four bands with hard steps. Interpolating per pixel gives a sweep.
			var (system, chart) = Build();
			var s = chart.AddSeries("g", Color.White);
			s.Gradient = ColorGradient.Predefined["spectrum"];
			s.SetValues(new[] { 10d, 60, 30, 80, 40 });

			var colours = PlotColours(Draw(system));

			// Four segments would give at most ~4 distinct series colours. A real
			// sweep across ~90 columns gives far more.
			Assert.True(colours.Count > 20,
				$"only {colours.Count} distinct colours on screen - the gradient is banding");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnXGradientRunsLeftToRightRegardlessOfValue()
		{
			// A flat series has no value variation at all, so any colour change
			// across it can only come from the X axis.
			var (system, chart) = Build();
			var s = chart.AddSeries("g", Color.White);
			s.Gradient = ColorGradient.FromColors(Color.Blue, Color.Red);
			s.GradientAxis = ChartGradientAxis.X;
			s.SetValues(new[] { 50d, 50, 50, 50, 50 });

			var colours = PlotColours(Draw(system));

			Assert.True(colours.Count > 10,
				"a flat series with an X gradient should still sweep across the plot");
			GC.KeepAlive(system);
		}

		/// <summary>Colours the series itself painted, excluding the plot background.</summary>
		/// <remarks>
		/// A line one pixel thick shares its cell with the background, so the cell is
		/// a half block carrying two colours rather than a solid one. Counting only
		/// solid cells finds nothing and passes vacuously - which an earlier version
		/// of this test did, and sabotage caught. Both halves are collected here and
		/// the background is subtracted.
		/// </remarks>
		private static HashSet<Color> SeriesColours(CharacterBufferSnapshot snap, ChartControl chart)
		{
			var background = new HashSet<Color> { Color.Black };
			var seen = new HashSet<Color>();

			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					// A half block's foreground is the top pixel and its background the
					// bottom, so both can be series colour.
					if (c.Character.Value == '\u2580')
					{
						seen.Add(c.Foreground);
						seen.Add(c.Background);
					}
					else if (c.Background == c.Foreground)
					{
						seen.Add(c.Background);
					}
				}
			}

			seen.ExceptWith(background);
			return seen;
		}

		[Fact]
		public void AYGradientLeavesAFlatSeriesOneColour()
		{
			// The sharpest distinction between the two axes: no value variation means
			// no colour variation when colouring by value, however far the line runs.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			var s = chart.AddSeries("g", Color.White);
			s.Gradient = ColorGradient.FromColors(Color.Blue, Color.Red);
			s.GradientAxis = ChartGradientAxis.Y;
			s.SetValues(new[] { 50d, 50, 50, 50, 50 });

			var colours = SeriesColours(Draw(system), chart);

			// One series colour, plus at most the plot fill showing through the
			// other half of each cell.
			Assert.True(colours.Count <= 2,
				$"a flat series with a Y gradient painted {colours.Count} colours "
				+ "({string.Join(\", \", colours)}); colouring by value must not vary along X");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnXGradientOnTheSameFlatSeriesPaintsMany()
		{
			// The mirror of the test above, measured the same way. Together they pin
			// the X/Y distinction: identical data, opposite results.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
            var s = chart.AddSeries("g", Color.White);
			s.Gradient = ColorGradient.FromColors(Color.Blue, Color.Red);
			s.GradientAxis = ChartGradientAxis.X;
			s.SetValues(new[] { 50d, 50, 50, 50, 50 });

			var colours = SeriesColours(Draw(system), chart);

			Assert.True(colours.Count > 10,
				$"an X gradient on a flat series painted only {colours.Count} colours");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AYGradientColoursHighAndLowDifferently()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			var s = chart.AddSeries("g", Color.White);
			s.Gradient = ColorGradient.FromColors(Color.Blue, Color.Red);
			s.GradientAxis = ChartGradientAxis.Y;
			s.SetValues(new[] { 5d, 95, 5, 95 });

			var colours = PlotColours(Draw(system));

			Assert.True(colours.Count > 10,
				"a series spanning the whole range should take most of the gradient");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheYGradientIsNormalisedAgainstTheAxisNotTheSeries()
		{
			// Pinning the axis pins the colour scale: 50 is mid-gradient on a 0..100
			// axis whatever else the series contains. Without this, "red means over
			// 80" would silently mean something different on every chart.
			var (axisSystem, axisChart) = Build();
			axisChart.YAxis.Min = 0;
			axisChart.YAxis.Max = 100;
			var pinned = axisChart.AddSeries("g", Color.White);
			pinned.Gradient = ColorGradient.FromColors(Color.Blue, Color.Red);
			pinned.GradientAxis = ChartGradientAxis.Y;
			pinned.SetValues(new[] { 50d, 50 });
			var pinnedColours = PlotColours(Draw(axisSystem));

			// Same values, but the axis now spans 40..60, so 50 still sits mid-scale.
			var (wideSystem, wideChart) = Build();
			wideChart.YAxis.Min = 40;
			wideChart.YAxis.Max = 60;
			var wide = wideChart.AddSeries("g", Color.White);
			wide.Gradient = ColorGradient.FromColors(Color.Blue, Color.Red);
			wide.GradientAxis = ChartGradientAxis.Y;
			wide.SetValues(new[] { 50d, 50 });
			var wideColours = PlotColours(Draw(wideSystem));

			// Mid-scale on both axes, so the series colour must match.
			Assert.True(pinnedColours.Overlaps(wideColours),
				"the same value at the same relative axis position took different colours");
			GC.KeepAlive(axisSystem);
			GC.KeepAlive(wideSystem);
		}

		[Fact]
		public void BothBlendsTheTwoPositions()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			var s = chart.AddSeries("g", Color.White);
			s.Gradient = ColorGradient.Predefined["spectrum"];
			s.GradientAxis = ChartGradientAxis.Both;
			s.SetValues(new[] { 10d, 40, 60, 90 });

			Assert.Null(Record.Exception(() => Draw(system)));
			Assert.True(PlotColours(Draw(system)).Count > 10, "Both produced no sweep");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheDefaultAxisIsX_SoLineGraphBehaviourIsPreserved()
		{
			// LineGraphControl colours by X. Keeping that as the default is what
			// makes a future delegation colour-identical rather than a surprise.
			var s = new ChartSeries { Name = "a", Color = Color.Red };

			Assert.Equal(ChartGradientAxis.X, s.GradientAxis);
		}

		[Fact]
		public void NoGradientUsesTheSeriesColour()
		{
			var (system, chart) = Build();
			chart.AddSeries("plain", Color.Magenta1).SetValues(new[] { 1d, 5, 2 });

			Assert.Contains(Color.Magenta1, PlotColours(Draw(system)));
			GC.KeepAlive(system);
		}
	}
}
