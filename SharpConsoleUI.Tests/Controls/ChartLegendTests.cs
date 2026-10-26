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
	}
}
