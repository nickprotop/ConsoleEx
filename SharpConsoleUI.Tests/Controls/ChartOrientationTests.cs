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
	/// Bars running across instead of up.
	/// </summary>
	/// <remarks>
	/// With a plot one row tall this is the inline gauge shape BarGraphControl has:
	/// a bar filling left to right, shading along its own length.
	/// </remarks>
	public class ChartOrientationTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 96, int height = 30)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				PlotType = ChartPlotType.Bar,
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

		/// <summary>The widest run of the colour on any single row.</summary>
		private static int WidestRow(CharacterBufferSnapshot snap, Color t)
		{
			int widest = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				int n = 0;
				for (int x = 0; x < snap.Width; x++) if (CellHas(snap, x, y, t)) n++;
				widest = Math.Max(widest, n);
			}
			return widest;
		}

		/// <summary>The tallest run of the colour in any single column.</summary>
		private static int TallestColumn(CharacterBufferSnapshot snap, Color t)
		{
			int tallest = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				int n = 0;
				for (int y = 0; y < snap.Height; y++) if (CellHas(snap, x, y, t)) n++;
				tallest = Math.Max(tallest, n);
			}
			return tallest;
		}

		[Fact]
		public void VerticalIsTheDefault()
		{
			Assert.Equal(ChartOrientation.Vertical, new ChartControl().Orientation);
		}

		[Fact]
		public void TheValueIsEncodedOnTheOtherAxisWhenHorizontal()
		{
			// Not "tall versus wide": a bar is as wide as its slot, so on a wide
			// plot a vertical bar can be wider than it is tall and that proves
			// nothing. The real distinction is WHICH axis carries the value, so
			// this changes one value and sees which direction the bar grows in.
			double[] low = { 20d, 20, 20, 20 };
			double[] high = { 90d, 90, 90, 90 };

			var (vLow, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("s", Color.Red).SetValues(low);
			var vLowSnap = Draw(vLow);

			var (vHigh, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			b.AddSeries("s", Color.Red).SetValues(high);
			var vHighSnap = Draw(vHigh);

			// Vertical: a bigger value makes TALLER bars, same width.
			Assert.True(TallestColumn(vHighSnap, Color.Red) > TallestColumn(vLowSnap, Color.Red),
				"a vertical bar did not grow taller with its value");
			Assert.Equal(WidestRow(vLowSnap, Color.Red), WidestRow(vHighSnap, Color.Red));

			var (hLow, c) = Build();
			c.Orientation = ChartOrientation.Horizontal;
			c.YAxis.Min = 0; c.YAxis.Max = 100;
			c.AddSeries("s", Color.Red).SetValues(low);
			var hLowSnap = Draw(hLow);

			var (hHigh, d) = Build();
			d.Orientation = ChartOrientation.Horizontal;
			d.YAxis.Min = 0; d.YAxis.Max = 100;
			d.AddSeries("s", Color.Red).SetValues(high);
			var hHighSnap = Draw(hHigh);

			// Horizontal: a bigger value makes LONGER bars, same height.
			Assert.True(WidestRow(hHighSnap, Color.Red) > WidestRow(hLowSnap, Color.Red),
				"a horizontal bar did not grow longer with its value");
			Assert.Equal(TallestColumn(hLowSnap, Color.Red), TallestColumn(hHighSnap, Color.Red));

			GC.KeepAlive(vLow); GC.KeepAlive(vHigh);
			GC.KeepAlive(hLow); GC.KeepAlive(hHigh);
		}

		[Fact]
		public void ALargerValueMakesALongerHorizontalBar()
		{
			var (small, a) = Build();
			a.Orientation = ChartOrientation.Horizontal;
			a.YAxis.Min = 0;
			a.YAxis.Max = 100;
			a.AddSeries("s", Color.Red).SetValues(new[] { 20d });
			int shortBar = WidestRow(Draw(small), Color.Red);

			var (big, b) = Build();
			b.Orientation = ChartOrientation.Horizontal;
			b.YAxis.Min = 0;
			b.YAxis.Max = 100;
			b.AddSeries("s", Color.Red).SetValues(new[] { 90d });
			int longBar = WidestRow(Draw(big), Color.Red);

			Assert.True(longBar > shortBar,
				$"value 90 drew {longBar} cells against value 20's {shortBar}");
			GC.KeepAlive(small);
			GC.KeepAlive(big);
		}

		[Fact]
		public void AOneRowPlotIsTheInlineGaugeShape()
		{
			// What BarGraphControl is: one row, a bar running across it.
			var (system, chart) = Build(width: 60, height: 8);
			chart.Orientation = ChartOrientation.Horizontal;
			chart.PlotHeight = 1;
			chart.XAxis.ShowLabels = false;
			chart.YAxis.ShowLabels = false;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("s", Color.Red).SetValues(new[] { 72d });

			var snap = Draw(system);

			Assert.True(WidestRow(snap, Color.Red) > 10, "the gauge bar is too short to read");
			Assert.True(TallestColumn(snap, Color.Red) <= 2,
				"a one-row plot drew more than one row of bar");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AHorizontalGradientShadesAlongTheBarsLength()
		{
			// BarGraphControl's SmoothGradient runs along the bar, not across the
			// value scale: the fill shades from its start to its tip.
			var (system, chart) = Build(width: 60, height: 8);
			chart.Orientation = ChartOrientation.Horizontal;
			chart.PlotHeight = 1;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			var s = chart.AddSeries("s", Color.White);
			s.Gradient = SharpConsoleUI.Helpers.ColorGradient.FromColors(Color.Blue, Color.Red);
			s.GradientAxis = ChartGradientAxis.X;
			s.SetValues(new[] { 90d });

			var snap = Draw(system);

			var seen = new System.Collections.Generic.HashSet<Color>();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					if (c.Background != Color.Black) seen.Add(c.Background);
				}
			}

			Assert.True(seen.Count > 8,
				$"a gradient along the bar produced only {seen.Count} colours");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AShortHorizontalBarEndsEarlyInTheGradient()
		{
			// In a one-row gauge there is no vertical dimension left, so LENGTH is
			// the value and the colour at the bar's tip says where that value sits
			// on the scale. That only works if the gradient is sampled against the
			// VALUE SCALE: sampled against the bar's own length, every bar would
			// run the full gradient and a 20% reading would look like a 100% one.
			var (small, a) = Build(width: 60, height: 8);
			a.Orientation = ChartOrientation.Horizontal;
			a.PlotHeight = 1;
			a.YAxis.Min = 0;
			a.YAxis.Max = 100;
			var sa = a.AddSeries("s", Color.White);
			sa.Gradient = SharpConsoleUI.Helpers.ColorGradient.FromColors(Color.Blue, Color.Red);
			sa.SetValues(new[] { 20d });
			var smallColours = BarColours(Draw(small), Color.Red);

			var (big, b) = Build(width: 60, height: 8);
			b.Orientation = ChartOrientation.Horizontal;
			b.PlotHeight = 1;
			b.YAxis.Min = 0;
			b.YAxis.Max = 100;
			var sb = b.AddSeries("s", Color.White);
			sb.Gradient = SharpConsoleUI.Helpers.ColorGradient.FromColors(Color.Blue, Color.Red);
			sb.SetValues(new[] { 95d });
			var bigColours = BarColours(Draw(big), Color.Red);

			// The long bar must reach redder than the short one: that difference IS
			// the reading.
			Assert.True(bigColours.MaxRed > smallColours.MaxRed + 40,
				$"a 95% bar reached red {bigColours.MaxRed} and a 20% bar {smallColours.MaxRed}; "
				+ "the tip colour does not track the value");

			GC.KeepAlive(small);
			GC.KeepAlive(big);
		}

		/// <summary>The reddest colour anywhere in the plot.</summary>
		private static (int MaxRed, int Count) BarColours(CharacterBufferSnapshot snap, Color _)
		{
			int maxRed = 0, count = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y).Background;
					if (c == Color.Black) continue;
					if (c.R > maxRed) maxRed = c.R;
					count++;
				}
			}
			return (maxRed, count);
		}

		[Fact]
		public void HorizontalSurvivesMultipleCategories()
		{
			var (system, chart) = Build();
			chart.Orientation = ChartOrientation.Horizontal;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.XAxis.Kind = ChartAxisKind.Category;
			chart.XAxis.SetCategories(new[] { "cpu", "gpu", "ram" });
			chart.AddSeries("s", Color.Red).SetCategories(
				new[] { ("cpu", 40d), ("gpu", 70d), ("ram", 55d) });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void HorizontalWithNoDataDoesNotThrow()
		{
			var (system, chart) = Build();
			chart.Orientation = ChartOrientation.Horizontal;

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}
	}
}
