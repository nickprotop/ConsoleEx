// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>Narrowing the visible X range, and getting back out again.</summary>
	public class ChartZoomTests
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

		private static void Send(ChartControl chart, MouseFlags flag, int x, int y)
		{
			var p = new Point(x, y);
			var args = new MouseEventArgs(new List<MouseFlags> { flag }, p, p, p);
			((IMouseAwareControl)chart).ProcessMouseEvent(args);
		}

		/// <summary>Press, drag, release across the plot.</summary>
		private static void Drag(ChartControl chart, int fromX, int toX, int y)
		{
			Send(chart, MouseFlags.Button1Pressed, fromX, y);
			Send(chart, MouseFlags.Button1Pressed, toX, y);     // SGR resends press on motion
			Send(chart, MouseFlags.Button1Released, toX, y);
		}

		private static bool Key(ChartControl chart, ConsoleKey key) =>
			((IInteractiveControl)chart).ProcessKey(new ConsoleKeyInfo('\0', key, false, false, false));

		[Fact]
		public void ZoomToNarrowsTheVisibleRange()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4 });
			Draw(system);

			chart.ZoomTo(1, 3);
			Draw(system);

			Assert.True(chart.IsZoomed);
			Assert.Equal(1, chart.VisibleXMin);
			Assert.Equal(3, chart.VisibleXMax);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ResetZoomRestoresTheFullRange()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);

			chart.ZoomTo(1, 3);
			Draw(system);
			chart.ResetZoom();
			Draw(system);

			Assert.False(chart.IsZoomed);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ZoomingRaisesViewportChangedOnce()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);

			int fired = 0;
			chart.ViewportChanged += (_, _) => fired++;

			chart.ZoomTo(1, 3);
			chart.ZoomTo(1, 3);   // same range again: nothing changed

			Assert.Equal(1, fired);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ABackwardsRangeIsOrderedNotRecentred()
		{
			// ZoomTo(10, 2) must mean the same as ZoomTo(2, 10). Asserting only that
			// the result is non-inverted is not enough: without the swap the span
			// comes out negative, the minimum-span guard recentres it, and the range
			// is valid but WRONG - a different window from the one asked for. That
			// is why this pins the exact bounds.
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 0d, 5, 2, 8, 3, 7, 4, 9, 1, 6, 2 });
			Draw(system);

			chart.ZoomTo(8, 2);

			Assert.Equal(2, chart.VisibleXMin);
			Assert.Equal(8, chart.VisibleXMax);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ABackwardsDragZoomsToTheSameRangeAsAForwardOne()
		{
			// Dragging right-to-left is how half of people select a range.
			var (forward, a) = Build();
			a.EnableZoom = true;
			a.AddSeries("s", Color.Red).SetValues(new[] { 0d, 5, 2, 8, 3, 7, 4, 9 });
			Draw(forward);
			Drag(a, fromX: 20, toX: 60, y: 12);
			Draw(forward);

			var (backward, b) = Build();
			b.EnableZoom = true;
			b.AddSeries("s", Color.Red).SetValues(new[] { 0d, 5, 2, 8, 3, 7, 4, 9 });
			Draw(backward);
			Drag(b, fromX: 60, toX: 20, y: 12);
			Draw(backward);

			Assert.Equal(a.VisibleXMin, b.VisibleXMin, 6);
			Assert.Equal(a.VisibleXMax, b.VisibleXMax, 6);
			GC.KeepAlive(forward);
			GC.KeepAlive(backward);
		}

		[Fact]
		public void AZeroWidthZoomIsRefused()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);

			chart.ZoomTo(2, 2);

			Assert.True(chart.VisibleXMax > chart.VisibleXMin,
				"a zero-width range was accepted, which divides by zero downstream");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ZoomSurvivesARerender()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);

			chart.ZoomTo(1, 3);
			Draw(system);
			double min = chart.VisibleXMin, max = chart.VisibleXMax;

			Draw(system);
			Draw(system);

			Assert.Equal(min, chart.VisibleXMin);
			Assert.Equal(max, chart.VisibleXMax);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ADragAcrossThePlotZoomsToThatRange()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4, 9 });
			Draw(system);

			Drag(chart, fromX: 20, toX: 60, y: 12);
			Draw(system);

			Assert.True(chart.IsZoomed, "a drag across the plot did not zoom");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AZeroWidthDragIsAClickNotAZoom()
		{
			// Press and release in one cell is how every click arrives. If that
			// zooms, the chart becomes unusable the first time someone clicks it.
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);

			Drag(chart, fromX: 20, toX: 20, y: 12);
			Draw(system);

			Assert.False(chart.IsZoomed, "a click zoomed the chart");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ZoomIsIgnoredWhenNotEnabled()
		{
			var (system, chart) = Build();
			chart.EnableZoom = false;
			chart.EnableHover = true;   // something else on, so the chart takes the mouse
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);

			Drag(chart, fromX: 20, toX: 60, y: 12);
			Draw(system);

			Assert.False(chart.IsZoomed);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheWheelZoomsIn()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3, 7, 4, 9 });
			Draw(system);

			Send(chart, MouseFlags.WheeledUp, 40, 12);
			Draw(system);

			Assert.True(chart.IsZoomed, "the wheel did not zoom");
			GC.KeepAlive(system);
		}

		[Fact]
		public void EscapeResetsTheZoom()
		{
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);
			chart.ZoomTo(1, 3);
			Draw(system);

			Key(chart, ConsoleKey.Escape);
			Draw(system);

			Assert.False(chart.IsZoomed);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ZoomingChangesWhatIsDrawn()
		{
			// The point of the feature: a narrower range must redraw, not just
			// record a number.
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9, 1, 9, 1, 9 });
			string before = TextOf(Draw(system));

			chart.ZoomTo(2, 4);
			string after = TextOf(Draw(system));

			Assert.NotEqual(before, after);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TurningZoomOffResetsTheView()
		{
			// Otherwise a chart that is no longer zoomable stays stuck at whatever
			// range it was left on, with no way back.
			var (system, chart) = Build();
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });
			Draw(system);
			chart.ZoomTo(1, 3);
			Draw(system);
			Assert.True(chart.IsZoomed);

			chart.EnableZoom = false;
			Draw(system);

			Assert.False(chart.IsZoomed);
			GC.KeepAlive(system);
		}

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var c = snap.GetCell(x, y);
					sb.Append(c.Character).Append(c.Background.R);
				}
			}
			return sb.ToString();
		}
	}
}
