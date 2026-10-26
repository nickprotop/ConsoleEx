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
	/// A second vertical axis, so series in different units share a plot.
	/// </summary>
	/// <remarks>
	/// Requests per second against latency in milliseconds: plotted on one scale
	/// the smaller series is a flat line along the floor and says nothing.
	/// </remarks>
	public class ChartSecondaryAxisTests
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
		public void ASeriesOnTheSecondaryAxisUsesItsOwnScale()
		{
			// 0..1 against 0..1000: on one scale the small series flattens onto the
			// floor. On its own axis it uses the full height.
			var (shared, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 1000;
			a.AddSeries("big", Color.Red).SetValues(new[] { 200d, 800, 400 });
			a.AddSeries("small", Color.Blue).SetValues(new[] { 0.2d, 0.8, 0.4 });
			var sharedSmall = RowSpan(Draw(shared), Color.Blue);

			var (split, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 1000;
			b.SecondaryYAxis.Min = 0;
			b.SecondaryYAxis.Max = 1;
			b.AddSeries("big", Color.Red).SetValues(new[] { 200d, 800, 400 });
			var small = b.AddSeries("small", Color.Blue);
			small.UseSecondaryAxis = true;
			small.SetValues(new[] { 0.2d, 0.8, 0.4 });
			var splitSmall = RowSpan(Draw(split), Color.Blue);

			int sharedHeight = sharedSmall.Bottom - sharedSmall.Top;
			int splitHeight = splitSmall.Bottom - splitSmall.Top;

			Assert.True(splitHeight > sharedHeight + 3,
				$"on its own axis the series spanned {splitHeight} rows, shared {sharedHeight}");
			GC.KeepAlive(shared);
			GC.KeepAlive(split);
		}

		[Fact]
		public void TheSecondaryAxisIsLabelledOnTheRight()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0; chart.YAxis.Max = 100;
			chart.SecondaryYAxis.Min = 0;
			chart.SecondaryYAxis.Max = 5000;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 50 });
			var s = chart.AddSeries("b", Color.Blue);
			s.UseSecondaryAxis = true;
			s.SetValues(new[] { 1000d, 4000 });

			// Not "5000": nice-number ticks over this height land on 0/2000/4000,
			// so asserting the axis MAXIMUM tests the tick engine's arithmetic
			// rather than whether the labels were drawn. 4000 is a secondary-scale
			// value that cannot come from the primary 0..100 axis.
			Assert.Contains("4000", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void NoSecondaryLabelsWithoutASeriesUsingIt()
		{
			// The right-hand gutter is only worth its width when something needs it.
			var (system, chart) = Build();
			chart.YAxis.Min = 0; chart.YAxis.Max = 100;
			chart.SecondaryYAxis.Min = 0;
			chart.SecondaryYAxis.Max = 5000;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 50 });

			Assert.DoesNotContain("4000", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void SeriesDefaultToThePrimaryAxis()
		{
			var chart = new ChartControl();
			var s = chart.AddSeries("a", Color.Red);

			Assert.False(s.UseSecondaryAxis);
		}

		[Fact]
		public void ASecondaryAxisWithNoDataDoesNotThrow()
		{
			var (system, chart) = Build();
			var s = chart.AddSeries("a", Color.Red);
			s.UseSecondaryAxis = true;

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheSecondaryAxisAutoRangesFromItsOwnSeries()
		{
			// Unpinned, it must take its bounds from the series that use it, not
			// from the primary series it shares a plot with.
			var (system, chart) = Build();
			chart.YAxis.Min = 0; chart.YAxis.Max = 1000;
			chart.AddSeries("big", Color.Red).SetValues(new[] { 200d, 800 });
			var s = chart.AddSeries("small", Color.Blue);
			s.UseSecondaryAxis = true;
			s.SetValues(new[] { 1d, 9 });
			var autoSpan = RowSpan(Draw(system), Color.Blue);

			// The comparison that isolates auto-ranging: the SAME tiny series on the
			// primary 0..1000 scale is a flat line along the floor. Asserting an
			// absolute row count instead would be measuring the plot height.
			var (shared, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 1000;
			b.AddSeries("big", Color.Red).SetValues(new[] { 200d, 800 });
			b.AddSeries("small", Color.Blue).SetValues(new[] { 1d, 9 });
			var sharedSpan = RowSpan(Draw(shared), Color.Blue);

			Assert.True(autoSpan.Top != int.MaxValue, "the secondary series did not draw");
			Assert.True(autoSpan.Bottom - autoSpan.Top > sharedSpan.Bottom - sharedSpan.Top,
				$"auto-ranged span {autoSpan.Bottom - autoSpan.Top} was no better than "
				+ $"the shared-scale span {sharedSpan.Bottom - sharedSpan.Top}");

			GC.KeepAlive(shared);
			GC.KeepAlive(system);
		}
	}
}
