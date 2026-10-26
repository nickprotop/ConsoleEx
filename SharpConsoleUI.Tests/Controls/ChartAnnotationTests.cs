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
	/// <summary>
	/// Marks on a chart that are not data: targets, limits, peaks, thresholds.
	/// </summary>
	/// <remarks>
	/// A chart of numbers alone rarely answers the question being asked. "Is this
	/// over the limit" needs the limit drawn; "when did it peak" needs the peak
	/// labelled. LineGraphControl has all of these and ChartControl did not.
	/// </remarks>
	public class ChartAnnotationTests
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

		private static int CountWith(CharacterBufferSnapshot snap, Color c)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) n++;
				}
			}
			return n;
		}

		// --- reference lines ---

		[Fact]
		public void AHorizontalReferenceLineIsDrawnAcrossThePlot()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40, 20 });
			chart.AddReferenceLine(80, Color.Yellow, "limit");

			var snap = Draw(system);

			Assert.True(CountWith(snap, Color.Yellow) > 10,
				"the reference line did not span the plot");
			Assert.Contains("limit", TextOf(snap));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AVerticalReferenceLineMarksAnX()
		{
			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 10;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40, 20, 50, 30 });
			chart.AddReferenceLine(5, Color.Cyan1, "deploy", ChartReferenceAxis.X);

			Assert.True(CountWith(Draw(system), Color.Cyan1) > 5,
				"the vertical reference line did not draw");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ReferenceLinesCanBeCleared()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40 });
			chart.AddReferenceLine(80, Color.Yellow, "limit");
			Draw(system);

			chart.ClearReferenceLines();

			Assert.Equal(0, CountWith(Draw(system), Color.Yellow));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AReferenceLineOutsideTheRangeIsNotDrawn()
		{
			// Clamping it to the edge would claim a limit sits at the top of the
			// chart when it is actually off the scale.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40 });
			chart.AddReferenceLine(500, Color.Yellow, "way up");

			Assert.Equal(0, CountWith(Draw(system), Color.Yellow));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AReferenceLineSitsUnderTheData()
		{
			// It is context, not content. Drawn after the plot, a line would erase
			// the series wherever the two cross - so this runs a series straight
			// THROUGH a line and counts what is left of it.
			var (clean, a) = Build();
			a.YAxis.Min = 0;
			a.YAxis.Max = 100;
			a.AddSeries("s", Color.Red).SetValues(new[] { 10d, 90, 10, 90, 10 });
			int without = CountWith(Draw(clean), Color.Red);

			var (lined, b) = Build();
			b.YAxis.Min = 0;
			b.YAxis.Max = 100;
			b.AddSeries("s", Color.Red).SetValues(new[] { 10d, 90, 10, 90, 10 });
			// Mid-range, so the zig-zag crosses it repeatedly.
			b.AddReferenceLine(50, Color.Yellow, null);
			int with = CountWith(Draw(lined), Color.Red);

			Assert.True(without > 0, "the series did not draw at all");
			Assert.Equal(without, with);

			GC.KeepAlive(clean);
			GC.KeepAlive(lined);
		}

		[Fact]
		public void AReferenceLineIsHiddenWhereTheDataCoversIt()
		{
			// The other half of the same rule: a line under a flat series at the
			// same value is completely hidden, and that is correct - the data wins
			// the cell.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("s", Color.Red).SetValues(new[] { 50d, 50, 50, 50 });
			chart.AddReferenceLine(50, Color.Yellow, null);

			var snap = Draw(system);

			Assert.True(CountWith(snap, Color.Red) > 0, "the series vanished");
			Assert.Equal(0, CountWith(snap, Color.Yellow));
			GC.KeepAlive(system);
		}

		// --- colour thresholds ---

		[Fact]
		public void AThresholdChangesTheSeriesColour()
		{
			var (low, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			var sa = a.AddSeries("v", Color.Green);
			sa.SetThresholds(new ChartThreshold(70, Color.Yellow), new ChartThreshold(90, Color.Red));
			sa.SetValues(new[] { 20d, 25, 20 });
			var lowSnap = Draw(low);

			var (high, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			var sb = b.AddSeries("v", Color.Green);
			sb.SetThresholds(new ChartThreshold(70, Color.Yellow), new ChartThreshold(90, Color.Red));
			sb.SetValues(new[] { 95d, 96, 95 });
			var highSnap = Draw(high);

			Assert.True(CountWith(lowSnap, Color.Green) > 0, "a low series should stay green");
			Assert.True(CountWith(highSnap, Color.Red) > 0, "a high series should go red");
			GC.KeepAlive(low);
			GC.KeepAlive(high);
		}

		[Fact]
		public void ThresholdsApplyPerPointNotPerSeries()
		{
			// A series crossing a limit must change colour AT the crossing, which is
			// the whole point: one colour for the whole line says nothing new.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			var s = chart.AddSeries("v", Color.Green);
			s.SetThresholds(new ChartThreshold(80, Color.Red));
			s.SetValues(new[] { 10d, 20, 95, 15, 10 });

			var snap = Draw(system);

			Assert.True(CountWith(snap, Color.Green) > 0, "the below-threshold part lost its colour");
			Assert.True(CountWith(snap, Color.Red) > 0, "the above-threshold part did not change");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AGradientWinsOverThresholds()
		{
			// Both set is a contradiction; the gradient is the more specific request.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			var s = chart.AddSeries("v", Color.Green);
			s.Gradient = SharpConsoleUI.Helpers.ColorGradient.FromColors(Color.Blue, Color.Magenta1);
			s.SetThresholds(new ChartThreshold(10, Color.Red));
			s.SetValues(new[] { 50d, 60, 70 });

			Assert.Equal(0, CountWith(Draw(system), Color.Red));
			GC.KeepAlive(system);
		}

		// --- high/low labels ---

		[Fact]
		public void HighAndLowLabelsMarkThePeakAndTrough()
		{
			var (system, chart) = Build();
			chart.ShowHighLowLabels = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 30d, 95, 40, 12, 60 });

			string text = TextOf(Draw(system));

			Assert.Contains("95", text);
			Assert.Contains("12", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void HighLowLabelsAreOffByDefault()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 30d, 95, 40 });

			Assert.False(chart.ShowHighLowLabels);
			GC.KeepAlive(system);
		}

		[Fact]
		public void HighLowOnAnEmptySeriesDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.ShowHighLowLabels = true;
			chart.AddSeries("a", Color.Red);

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		// --- value markers ---

		[Fact]
		public void AValueMarkerPointsAtAValue()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40, 20 });
			chart.AddValueMarker(60, "target", Color.Magenta1);

			string text = TextOf(Draw(system));

			Assert.Contains("target", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void MarkersCanBeCleared()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40 });
			chart.AddValueMarker(60, "target", Color.Magenta1);
			Draw(system);

			chart.ClearValueMarkers();

			Assert.DoesNotContain("target", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnnotationsSurviveARerender()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 40 });
			chart.AddReferenceLine(80, Color.Yellow, "limit");

			Draw(system);
			int first = CountWith(Draw(system), Color.Yellow);
			int second = CountWith(Draw(system), Color.Yellow);

			Assert.Equal(first, second);
			Assert.True(first > 0);
			GC.KeepAlive(system);
		}
	}
}
