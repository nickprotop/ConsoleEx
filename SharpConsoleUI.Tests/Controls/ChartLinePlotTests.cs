// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// The chart drawn through the real render path, in a real window.
	/// </summary>
	public class ChartLinePlotTests
	{
		/// <summary>Builds a real window around a chart, the way an app would.</summary>
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 96, int height = 30)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system)
			{
				Left = 0,
				Top = 0,
				Width = width,
				Height = height,
			};
			var chart = new ChartControl
			{
				PlotType = ChartPlotType.Line,
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

		[Fact]
		public void ALineSeriesIsDrawnInItsOwnColour()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });

			Assert.True(AnyCellHas(Draw(system), Color.Red), "the series was not drawn");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TwoSeriesBothSurvive()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 8d, 2, 7, 1, 6 });

			var snap = Draw(system);

			Assert.True(AnyCellHas(snap, Color.Red), "series a vanished");
			Assert.True(AnyCellHas(snap, Color.Blue), "series b vanished");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AxisLabelsAppearOnScreen()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 50, 90 });

			Assert.Contains("100", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnEmptyChartRendersItsFrameWithoutThrowing()
		{
			// A dashboard builds its charts before the first sample arrives.
			var (system, _) = Build();

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void ASeriesWithOnePointDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 42d });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AFlatSeriesDoesNotDivideByZero()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 7d, 7, 7, 7 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheChartSurvivesABoundaryNarrowWindow()
		{
			// Narrow enough that the Y gutter alone could consume the whole width:
			// the plot must clamp to nothing rather than compute a negative extent.
			var (system, chart) = Build(width: 12, height: 6);
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheChartSurvivesAOneRowWindow()
		{
			var (system, chart) = Build(width: 40, height: 3);
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void DataAddedAfterTheFirstRenderShowsOnTheNext()
		{
			// The reactive contract: add a series, the UI updates. AddSeries
			// invalidates on the caller's behalf - no Invalidate call here.
			var (system, chart) = Build();
			Draw(system);

			chart.AddSeries("late", Color.Green).SetValues(new[] { 2d, 6, 3 });

			Assert.True(AnyCellHas(Draw(system), Color.Green),
				"a series added after the first paint never appeared");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AHiddenSeriesIsNotPlotted()
		{
			var (system, chart) = Build();
			var s = chart.AddSeries("ghost", Color.Magenta1);
			s.SetValues(new[] { 1d, 5, 2 });
			s.IsVisible = false;

			Assert.False(AnyCellHas(Draw(system), Color.Magenta1),
				"a hidden series was still plotted");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheChartDescribesItsStateForDiagnostics()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 3d, 4 });

			var recorder = new StateRecorder();
			chart.DescribeState(recorder);

			Assert.Equal(2L, recorder.Values["seriesCount"]);
			Assert.Equal(4L, recorder.Values["pointCount"]);
			GC.KeepAlive(system);
		}

		/// <summary>Collects described values so a test can assert on them.</summary>
		private sealed class StateRecorder : IStateWriter
		{
			public Dictionary<string, object?> Values { get; } = new();

			public void Write(string name, string? value) => Values[name] = value;
			public void Write(string name, long value) => Values[name] = value;
			public void Write(string name, double value) => Values[name] = value;
			public void Write(string name, bool value) => Values[name] = value;

			public void WriteIf(bool condition, string name, string? value)
			{
				if (condition) Values[name] = value;
			}
		}
	}
}
