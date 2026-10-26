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
	/// Stacked series: each sits on the sum of those before it.
	/// </summary>
	/// <remarks>
	/// The standard way to show composition - how a total divides between its
	/// parts - and the one GUI chart staple the control had no answer for.
	/// </remarks>
	public class ChartStackingTests
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
		public void StackingIsOffByDefault()
		{
			Assert.False(new ChartControl().Stacked);
		}

		[Fact]
		public void AStackedSeriesSitsAboveTheOneBeforeIt()
		{
			// Unstacked, two equal series draw on top of each other. Stacked, the
			// second rides on the first and reaches twice as high.
			var (plain, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("one", Color.Red).SetValues(new[] { 30d, 30, 30 });
			a.AddSeries("two", Color.Blue).SetValues(new[] { 30d, 30, 30 });
			var plainBlue = RowSpan(Draw(plain), Color.Blue);

			var (stacked, b) = Build();
			b.Stacked = true;
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			b.AddSeries("one", Color.Red).SetValues(new[] { 30d, 30, 30 });
			b.AddSeries("two", Color.Blue).SetValues(new[] { 30d, 30, 30 });
			var stackedBlue = RowSpan(Draw(stacked), Color.Blue);

			// Higher on screen means a smaller row index.
			Assert.True(stackedBlue.Top < plainBlue.Top,
				$"stacked series sat at row {stackedBlue.Top}, unstacked at {plainBlue.Top}");
			GC.KeepAlive(plain);
			GC.KeepAlive(stacked);
		}

		[Fact]
		public void TheFirstSeriesIsUnaffectedByStacking()
		{
			// It has nothing beneath it, so stacking must not move it.
			//
			// The two series carry DIFFERENT values: given identical ones, the
			// second draws exactly over the first in the unstacked chart and the
			// first cannot be found at all, which is what an earlier version of
			// this test tripped over.
			var (plain, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("one", Color.Red).SetValues(new[] { 20d, 20, 20 });
			a.AddSeries("two", Color.Blue).SetValues(new[] { 50d, 50, 50 });
			var plainRed = RowSpan(Draw(plain), Color.Red);

			var (stacked, b) = Build();
			b.Stacked = true;
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			b.AddSeries("one", Color.Red).SetValues(new[] { 20d, 20, 20 });
			b.AddSeries("two", Color.Blue).SetValues(new[] { 50d, 50, 50 });
			var stackedRed = RowSpan(Draw(stacked), Color.Red);

			Assert.True(plainRed.Top != int.MaxValue, "the first series did not draw");
			Assert.Equal(plainRed.Top, stackedRed.Top);
			GC.KeepAlive(plain);
			GC.KeepAlive(stacked);
		}

		[Fact]
		public void AutoRangeAccountsForTheStackedTotal()
		{
			// Three series of 30 stack to 90, so an auto-ranged axis must reach 90
			// or the top of the stack is clipped away.
			var (system, chart) = Build();
			chart.Stacked = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 30d, 30 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 30d, 30 });
			chart.AddSeries("c", Color.Green).SetValues(new[] { 30d, 30 });

			var snap = Draw(system);

			Assert.True(RowSpan(snap, Color.Red).Top != int.MaxValue, "series a vanished");
			Assert.True(RowSpan(snap, Color.Blue).Top != int.MaxValue, "series b vanished");
			Assert.True(RowSpan(snap, Color.Green).Top != int.MaxValue, "series c vanished");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AHiddenSeriesDoesNotContributeToTheStack()
		{
			// Toggling a series off in the legend must restack the rest, not leave
			// a gap where it was.
			var (visible, a) = Build();
			a.Stacked = true;
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("one", Color.Red).SetValues(new[] { 30d, 30 });
			a.AddSeries("two", Color.Blue).SetValues(new[] { 30d, 30 });
			var bothTop = RowSpan(Draw(visible), Color.Blue).Top;

			var (hidden, b) = Build();
			b.Stacked = true;
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			var first = b.AddSeries("one", Color.Red);
			first.SetValues(new[] { 30d, 30 });
			first.IsVisible = false;
			b.AddSeries("two", Color.Blue).SetValues(new[] { 30d, 30 });
			var aloneTop = RowSpan(Draw(hidden), Color.Blue).Top;

			Assert.True(aloneTop > bothTop,
				$"with the series below hidden, blue should drop: {bothTop} then {aloneTop}");
			GC.KeepAlive(visible);
			GC.KeepAlive(hidden);
		}

		[Fact]
		public void EachStackedBandStaysVisible()
		{
			// Stacked AREAS fill downward, so a band drawn later covers the ones
			// beneath unless each fills only to the series below it. The row-span
			// tests cannot see this: the series are in the right PLACES and the
			// lower ones are simply painted over.
			var (system, chart) = Build();
			chart.Stacked = true;
			chart.PlotType = ChartPlotType.Area;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 200;
			chart.AddSeries("a", Color.Green).SetValues(new[] { 40d, 40, 40 });
			chart.AddSeries("b", Color.Cyan1).SetValues(new[] { 40d, 40, 40 });
			chart.AddSeries("c", Color.Magenta1).SetValues(new[] { 40d, 40, 40 });

			var snap = Draw(system);

			Assert.True(Visible(snap, Color.Green) > 5, "the bottom band was covered over");
			Assert.True(Visible(snap, Color.Cyan1) > 5, "the middle band was covered over");
			Assert.True(Visible(snap, Color.Magenta1) > 5, "the top band did not draw");
			GC.KeepAlive(system);
		}

		/// <summary>Cells carrying a colour, allowing for the area fill's dimming.</summary>
		private static int Visible(CharacterBufferSnapshot snap, Color target)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					if (Dominates(c.Background, target) || Dominates(c.Foreground, target)) n++;
				}
			}
			return n;
		}

		/// <summary>True when a colour is dominated by the same channels as the target.</summary>
		private static bool Dominates(Color c, Color target)
		{
			if (c == Color.Black) return false;
			if (target == Color.Green) return c.G > c.R + 15 && c.G > c.B + 15;
			if (target == Color.Cyan1) return c.G > c.R + 15 && c.B > c.R + 15;
			if (target == Color.Magenta1) return c.R > c.G + 15 && c.B > c.G + 15;
			return c == target;
		}

		[Fact]
		public void StackingHandlesUnevenSeriesLengths()
		{
			var (system, chart) = Build();
			chart.Stacked = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 20, 30, 40 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 5d, 5 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void StackingAnEmptyChartDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.Stacked = true;

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}
	}
}
