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
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// The gauge: one row, length is the value, colour reads the scale.
	/// </summary>
	public class ChartGaugeTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 70, int height = 10)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				PlotType = ChartPlotType.Gauge,
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

		private static int PaintedRows(CharacterBufferSnapshot snap)
		{
			int rows = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y).Background;
					if (c != Color.Black && c.R + c.G + c.B > 120) { rows++; break; }
				}
			}
			return rows;
		}

		private static int BarCells(CharacterBufferSnapshot snap)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y).Background;
					if (c != Color.Black && c.R + c.G + c.B > 120) n++;
				}
			}
			return n;
		}

		[Fact]
		public void AGaugeOccupiesOneRow()
		{
			// The defining shape: a gauge is an inline readout, not a chart that
			// eats a window to say one number.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("cpu", Color.Green).SetValues(new[] { 72d });

			Assert.Equal(1, PaintedRows(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheLengthTracksTheValue()
		{
			var (low, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			a.AddSeries("cpu", Color.Green).SetValues(new[] { 20d });
			int shortBar = BarCells(Draw(low));

			var (high, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			b.AddSeries("cpu", Color.Green).SetValues(new[] { 90d });
			int longBar = BarCells(Draw(high));

			Assert.True(longBar > shortBar * 2,
				$"90% drew {longBar} cells and 20% drew {shortBar}; length should track value");
			GC.KeepAlive(low);
			GC.KeepAlive(high);
		}

		[Fact]
		public void TheLabelAndValueAreShown()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("cpu", Color.Green).SetValues(new[] { 72d });

			string text = TextOf(Draw(system));

			Assert.Contains("cpu", text);
			Assert.Contains("72", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheLabelCanBeHidden()
		{
			var (system, chart) = Build();
			chart.ShowGaugeLabel = false;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("cpu", Color.Green).SetValues(new[] { 72d });

			Assert.DoesNotContain("cpu", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheValueCanBeHidden()
		{
			var (system, chart) = Build();
			chart.ShowGaugeValue = false;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("cpu", Color.Green).SetValues(new[] { 72d });

			Assert.DoesNotContain("72", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheValueUsesTheAxisFormatter()
		{
			// So units and percentages work without a second formatting concept.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.YAxis.LabelFormatter = v => $"{v:0}%";
			chart.AddSeries("cpu", Color.Green).SetValues(new[] { 72d });

			Assert.Contains("72%", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AGradientReadsTheScaleNotTheBarLength()
		{
			// The tip colour is the reading. Sampled along the bar's own length
			// every gauge would end the same colour and say nothing.
			var (low, a) = Build();
			a.YAxis.Min = 0; a.YAxis.Max = 100;
			var sa = a.AddSeries("cpu", Color.White);
			sa.Gradient = ColorGradient.FromColors(Color.Green, Color.Red);
			sa.SetValues(new[] { 15d });
			int lowRed = MaxRed(Draw(low));

			var (high, b) = Build();
			b.YAxis.Min = 0; b.YAxis.Max = 100;
			var sb = b.AddSeries("cpu", Color.White);
			sb.Gradient = ColorGradient.FromColors(Color.Green, Color.Red);
			sb.SetValues(new[] { 95d });
			int highRed = MaxRed(Draw(high));

			Assert.True(highRed > lowRed + 40,
				$"a 95% gauge reached red {highRed} and a 15% one {lowRed}");
			GC.KeepAlive(low);
			GC.KeepAlive(high);
		}

		[Fact]
		public void SeveralGaugesStack()
		{
			// A stack of readings is the common use; each series gets its own row.
			var (system, chart) = Build(width: 70, height: 12);
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("cpu", Color.Green).SetValues(new[] { 72d });
			chart.AddSeries("ram", Color.Cyan1).SetValues(new[] { 45d });
			chart.AddSeries("disk", Color.Yellow).SetValues(new[] { 88d });

			string text = TextOf(Draw(system));

			Assert.Contains("cpu", text);
			Assert.Contains("ram", text);
			Assert.Contains("disk", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnEmptyGaugeDoesNotThrow()
		{
			var (system, _) = Build();

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AVeryNarrowGaugeDoesNotThrow()
		{
			var (system, chart) = Build(width: 12, height: 6);
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a-long-name", Color.Green).SetValues(new[] { 72d });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		private static int MaxRed(CharacterBufferSnapshot snap)
		{
			int max = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y).Background;
					if (c != Color.Black && c.R > max) max = c.R;
				}
			}
			return max;
		}
	}
}
