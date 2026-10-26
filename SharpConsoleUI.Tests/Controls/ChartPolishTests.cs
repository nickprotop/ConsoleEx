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
	/// <summary>The smaller things a chart needs to feel finished.</summary>
	public class ChartPolishTests
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

		[Fact]
		public void AnEmptyChartSaysSo()
		{
			// A blank frame is indistinguishable from a broken one. Saying "no data"
			// tells the reader the chart is working and waiting.
			var (system, chart) = Build();
			chart.EmptyText = "no data yet";

			Assert.Contains("no data yet", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheEmptyMessageGoesOnceDataArrives()
		{
			var (system, chart) = Build();
			chart.EmptyText = "no data yet";
			Draw(system);

			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.DoesNotContain("no data yet", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AChartWithOnlyHiddenSeriesIsStillEmpty()
		{
			// Every series toggled off looks the same as no data, so it should read
			// the same way rather than showing an unexplained blank plot.
			var (system, chart) = Build();
			chart.EmptyText = "nothing to show";
			var s = chart.AddSeries("a", Color.Red);
			s.SetValues(new[] { 1d, 5 });
			s.IsVisible = false;

			Assert.Contains("nothing to show", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void NoEmptyTextByDefault()
		{
			Assert.Null(new ChartControl().EmptyText);
		}

		[Fact]
		public void ToggleSeriesFlipsVisibility()
		{
			// So an app can drive legend behaviour without reaching into the series
			// collection itself.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.True(chart.Series[0].IsVisible);
			Assert.True(chart.ToggleSeries("a"));
			Assert.False(chart.Series[0].IsVisible);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TogglingAnUnknownSeriesReportsFailure()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.False(chart.ToggleSeries("nope"));
			GC.KeepAlive(system);
		}

		[Fact]
		public void ShowOnlyIsolatesOneSeries()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 2d, 6 });
			chart.AddSeries("c", Color.Green).SetValues(new[] { 3d, 7 });

			chart.ShowOnlySeries("b");

			Assert.False(chart.Series[0].IsVisible);
			Assert.True(chart.Series[1].IsVisible);
			Assert.False(chart.Series[2].IsVisible);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ShowAllSeriesRestoresEverything()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 2d, 6 });
			chart.ShowOnlySeries("a");

			chart.ShowAllSeries();

			Assert.True(chart.Series[0].IsVisible);
			Assert.True(chart.Series[1].IsVisible);
			GC.KeepAlive(system);
		}

		[Fact]
		public void KeyboardSelectionShowsTheReadout()
		{
			// Hover is mouse-only; someone navigating by keyboard needs the same
			// answer to "what is this point".
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.EnableHover = true;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("throughput", Color.Red).SetValues(new[] { 10d, 90, 10 });
			Draw(system);

			((IInteractiveControl)chart).ProcessKey(
				new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false));

			Assert.Contains("throughput", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheReadoutIsNotShownWithoutHoverEnabled()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.EnableHover = false;
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("throughput", Color.Red).SetValues(new[] { 10d, 90, 10 });
			Draw(system);

			((IInteractiveControl)chart).ProcessKey(
				new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false));

			Assert.DoesNotContain("throughput", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}
	}
}
