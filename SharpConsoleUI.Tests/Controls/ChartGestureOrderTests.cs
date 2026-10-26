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
	/// <summary>
	/// Gesture precedence when zoom and selection are both on.
	/// </summary>
	/// <remarks>
	/// A click arrives carrying Pressed AND Clicked together, so a handler that
	/// tests the press first sees every click as the start of a drag. Rule 14 in
	/// CLAUDE.md names that as the rejected pattern; these pin the behaviour it
	/// protects.
	/// </remarks>
	public class ChartGestureOrderTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(96, 30);
			var window = new Window(system) { Left = 0, Top = 0, Width = 96, Height = 30 };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				VerticalAlignment = VerticalAlignment.Fill,
				EnableSelection = true,
				EnableZoom = true,
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

		/// <summary>A real click: the press and the click flag arrive together.</summary>
		private static void ClickAt(ChartControl chart, int x, int y)
		{
			var p = new Point(x, y);
			var args = new MouseEventArgs(
				new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Clicked }, p, p, p);
			((IMouseAwareControl)chart).ProcessMouseEvent(args);
		}

		[Fact]
		public void AClickSelectsEvenWhenZoomIsAlsoEnabled()
		{
			// The case the ordering protects: with both toggles on, a click carries
			// Pressed as well, and must still reach selection rather than being
			// eaten as the start of a drag.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);

			var positions = chart.PointPositionsForTest();
			Assert.NotEmpty(positions);
			ClickAt(chart, positions[0].CellX, positions[0].CellY);
			Draw(system);

			Assert.NotNull(chart.SelectedPoint);
			Assert.False(chart.IsZoomed, "a click started a zoom");
			GC.KeepAlive(system);
		}

		[Fact]
		public void AClickOnALegendEntryStillTogglesWithZoomOn()
		{
			var (system, chart) = Build();
			chart.ShowLegend = true;
			var s = chart.AddSeries("alpha", Color.Red);
			s.SetValues(new[] { 1d, 9, 1 });
			Draw(system);

			var regions = chart.LegendRegionsForTest();
			Assert.NotEmpty(regions);

			ClickAt(chart, regions[0].Rect.X, regions[0].Rect.Y);
			Draw(system);

			Assert.False(s.IsVisible, "the legend click was swallowed by the drag handler");
			GC.KeepAlive(system);
		}
	}
}
