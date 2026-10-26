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
	/// Split halves scaling to their own peaks, and resampling dense history.
	/// </summary>
	/// <remarks>
	/// Both come from the cxnet network monitor, which plots 512 samples of
	/// download and upload into about seventy columns and rescales each side to
	/// its own rolling peak. On a shared scale a 2 MB/s upload beside a 40 MB/s
	/// download is a flat line along the centre and tells the reader nothing.
	/// </remarks>
	public class ChartPerSideScaleTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 80, int height = 24)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				SplitView = true,
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
		public void TheLowerHalfCanScaleToItsOwnPeak()
		{
			// A small second series on a shared scale hugs the centre line. Given
			// its own peak it uses its whole half, which is the entire point of a
			// throughput monitor's two directions.
			var (shared, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 40000;
			a.AddSeries("down", Color.Red).SetValues(new[] { 10000d, 40000, 20000 });
			a.AddSeries("up", Color.Blue).SetValues(new[] { 500d, 2000, 800 });
			var sharedUp = RowSpan(Draw(shared), Color.Blue);

			var (split, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 40000;
			b.LowerAxis.Min = 0;
			b.LowerAxis.Max = 2000;
			b.AddSeries("down", Color.Red).SetValues(new[] { 10000d, 40000, 20000 });
			b.AddSeries("up", Color.Blue).SetValues(new[] { 500d, 2000, 800 });
			var ownUp = RowSpan(Draw(split), Color.Blue);

			Assert.True(ownUp.Bottom - ownUp.Top > sharedUp.Bottom - sharedUp.Top,
				$"on its own scale the lower series spanned {ownUp.Bottom - ownUp.Top} rows, "
				+ $"shared {sharedUp.Bottom - sharedUp.Top}");
			GC.KeepAlive(shared);
			GC.KeepAlive(split);
		}

		[Fact]
		public void TheUpperHalfIsUnaffectedByTheLowerScale()
		{
			var (a, chartA) = Build();
			chartA.YAxis.Min = 0; chartA.YAxis.Max = 40000;
			chartA.AddSeries("down", Color.Red).SetValues(new[] { 10000d, 40000, 20000 });
			chartA.AddSeries("up", Color.Blue).SetValues(new[] { 500d, 2000, 800 });
			var withoutLower = RowSpan(Draw(a), Color.Red);

			var (b, chartB) = Build();
			chartB.YAxis.Min = 0; chartB.YAxis.Max = 40000;
			chartB.LowerAxis.Min = 0;
			chartB.LowerAxis.Max = 2000;
			chartB.AddSeries("down", Color.Red).SetValues(new[] { 10000d, 40000, 20000 });
			chartB.AddSeries("up", Color.Blue).SetValues(new[] { 500d, 2000, 800 });
			var withLower = RowSpan(Draw(b), Color.Red);

			Assert.Equal(withoutLower.Top, withLower.Top);
			GC.KeepAlive(a);
			GC.KeepAlive(b);
		}

		[Fact]
		public void AnUnsetLowerAxisFollowsThePrimary()
		{
			// Additive: a split chart that does not ask keeps the shared scale.
			var chart = new ChartControl();

			Assert.Null(chart.LowerAxis.Min);
			Assert.Null(chart.LowerAxis.Max);
		}

		[Fact]
		public void TheLowerAxisIsIgnoredWithoutSplitView()
		{
			var (system, chart) = Build();
			chart.SplitView = false;
			chart.YAxis.Min = 0; chart.YAxis.Max = 100;
			chart.LowerAxis.Min = 0;
			chart.LowerAxis.Max = 10;
			// Different values: identical ones make the second draw over the first
			// and the first cannot be found at all.
			chart.AddSeries("a", Color.Red).SetValues(new[] { 80d, 80 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 20d, 20 });

			var snap = Draw(system);
			var red = RowSpan(snap, Color.Red);
			var blue = RowSpan(snap, Color.Blue);

			Assert.True(red.Top != int.MaxValue && blue.Top != int.MaxValue, "a series did not draw");

			// Both measured against the PRIMARY axis: 20 of 100 sits low, 80 high.
			// Had LowerAxis (0..10) applied, the 20 would have been clamped off the
			// top of its band instead.
			Assert.True(blue.Top > red.Top,
				$"blue (20) should sit below red (80): red {red.Top}, blue {blue.Top}");
			GC.KeepAlive(system);
		}

		// --- resampling ---

		[Fact]
		public void DenseHistoryIsResampledToTheWidth()
		{
			// 512 samples into seventy columns: without resampling most points land
			// on the same column and the trace is a smear.
			var (system, chart) = Build();
			chart.ResampleToWidth = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var many = new double[512];
			for (int i = 0; i < many.Length; i++) many[i] = 50 + 40 * Math.Sin(i / 8.0);
			chart.AddSeries("dense", Color.Red).SetValues(many);

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void ResamplingKeepsThePeaks()
		{
			// Taking every nth sample would drop a one-sample spike, which in a
			// monitor is exactly the event worth seeing. The peak must survive.
			var (system, chart) = Build();
			chart.ResampleToWidth = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var samples = new double[512];
			for (int i = 0; i < samples.Length; i++) samples[i] = 10;
			samples[301] = 95;   // a single spike between two sample columns

			chart.AddSeries("spiky", Color.Red).SetValues(samples);

			var snap = Draw(system);
			var span = RowSpan(snap, Color.Red);

			// A 95 in a 0..100 range sits near the top; a dropped spike would leave
			// the whole trace flat at 10.
			Assert.True(span.Top < snap.Height / 3,
				$"the spike was resampled away: topmost row {span.Top} of {snap.Height}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AResampledTraceSpansThePlot()
		{
			// Resampling rewrites X to column indices, so the X RANGE has to follow
			// or the whole trace bunches into the left of the plot - which is what
			// it did the first time, with 512 samples crammed into a fifth of the
			// width while the axis still read 0..500.
			var (system, chart) = Build();
			chart.ResampleToWidth = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;

			var many = new double[512];
			for (int i = 0; i < many.Length; i++) many[i] = 50 + 40 * Math.Sin(i / 9.0);
			chart.AddSeries("dense", Color.Red).SetValues(many);

			var snap = Draw(system);

			int leftmost = -1, rightmost = -1;
			for (int x = 0; x < snap.Width; x++)
			{
				bool hit = false;
				for (int y = 0; y < snap.Height && !hit; y++)
				{
					var c = snap.GetCell(x, y);
					hit = c.Foreground == Color.Red || c.Background == Color.Red;
				}
				if (!hit) continue;
				if (leftmost < 0) leftmost = x;
				rightmost = x;
			}

			Assert.True(leftmost >= 0, "the resampled series did not draw");
			Assert.True(rightmost - leftmost > snap.Width / 2,
				$"the resampled trace spans columns {leftmost}..{rightmost} of {snap.Width}; "
				+ "it should fill the plot");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ResamplingIsOffByDefault()
		{
			Assert.False(new ChartControl().ResampleToWidth);
		}

		[Fact]
		public void ResamplingFewerPointsThanColumnsChangesNothing()
		{
			var (plain, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("s", Color.Red).SetValues(new[] { 10d, 90, 30, 70 });
			var without = RowSpan(Draw(plain), Color.Red);

			var (resampled, b) = Build();
			b.ResampleToWidth = true;
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			b.AddSeries("s", Color.Red).SetValues(new[] { 10d, 90, 30, 70 });
			var with = RowSpan(Draw(resampled), Color.Red);

			Assert.Equal(without.Top, with.Top);
			Assert.Equal(without.Bottom, with.Bottom);
			GC.KeepAlive(plain);
			GC.KeepAlive(resampled);
		}
	}
}
