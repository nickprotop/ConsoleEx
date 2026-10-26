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
	/// Hover: the nearest point under the pointer, a crosshair, and a readout.
	/// </summary>
	/// <remarks>
	/// Every interactive feature is opt-in and off by default, so a chart used as
	/// decoration never takes the mouse from whatever is under it.
	/// </remarks>
	public class ChartHoverTests
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

		private static MouseEventArgs Mouse(MouseFlags flag, int x, int y)
		{
			var p = new Point(x, y);
			return new MouseEventArgs(new List<MouseFlags> { flag }, p, p, p);
		}

		/// <summary>Sends a move and reports whether the chart consumed it.</summary>
		private static bool Move(ChartControl chart, int x, int y)
		{
			var args = Mouse(MouseFlags.ReportMousePosition, x, y);
			((IMouseAwareControl)chart).ProcessMouseEvent(args);
			return args.Handled;
		}

		/// <summary>A cell inside the plot, found from a drawn point.</summary>
		private static (int X, int Y) APlottedCell(ChartControl chart)
		{
			var p = chart.PointPositionsForTest();
			Assert.NotEmpty(p);
			return (p[0].CellX, p[0].CellY);
		}

		[Fact]
		public void EveryInteractionIsOffByDefault()
		{
			// A chart used as decoration must not take the mouse or the keyboard
			// from whatever sits under it.
			var chart = new ChartControl();

			Assert.False(chart.EnableHover);
			Assert.False(chart.EnableSelection);
			Assert.False(chart.EnableZoom);
			Assert.False(((IMouseAwareControl)chart).WantsMouseEvents);
			Assert.False(((IFocusableControl)chart).CanReceiveFocus);
		}

		[Fact]
		public void EnablingHoverMakesTheChartWantTheMouse()
		{
			var chart = new ChartControl { EnableHover = true };

			Assert.True(((IMouseAwareControl)chart).WantsMouseEvents);
			GC.KeepAlive(chart);
		}

		[Fact]
		public void HoveringNearAPointReportsIt()
		{
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);

			var (x, y) = APlottedCell(chart);
			Move(chart, x, y);
			Draw(system);

			Assert.NotNull(chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void HoveringAnEmptyChartFindsNothingAndDoesNotThrow()
		{
			// Index-into-empty is the obvious crash, and a dashboard hovers a chart
			// that has not had its first sample all the time.
			var (system, chart) = Build();
			chart.EnableHover = true;
			Draw(system);

			var ex = Record.Exception(() =>
			{
				Move(chart, 10, 5);
				Draw(system);
			});

			Assert.Null(ex);
			Assert.Null(chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void HoveringWhenEverySeriesIsHiddenFindsNothing()
		{
			var (system, chart) = Build();
			chart.EnableHover = true;
			var s = chart.AddSeries("a", Color.Red);
			s.SetValues(new[] { 1d, 5, 2 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			s.IsVisible = false;
			Draw(system);

			Move(chart, x, y);
			Draw(system);

			Assert.Null(chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void LeavingTheChartClearsTheHover()
		{
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Move(chart, x, y);
			Draw(system);
			Assert.NotNull(chart.HoveredPoint);

			// Far outside the plot.
			Move(chart, 1000, 1000);
			Draw(system);

			Assert.Null(chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void HoverDoesNothingWhenNotEnabled()
		{
			var (system, chart) = Build();
			chart.EnableHover = false;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Move(chart, x, y);
			Draw(system);

			Assert.Null(chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void HoverStaysOffWhenAnotherFeatureIsOn()
		{
			// The toggles are independent. With hover off but zoom on the control
			// still wants the mouse, so the outer "does this control take input at
			// all" guard no longer covers hover - only hover's own check does.
			// Without this case, deleting that check breaks nothing in the suite.
			var (system, chart) = Build();
			chart.EnableHover = false;
			chart.EnableZoom = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

            Assert.True(((IMouseAwareControl)chart).WantsMouseEvents,
				"zoom should make the chart want the mouse");

			Move(chart, x, y);
			Draw(system);

			Assert.Null(chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnIgnoredMoveIsNotClaimed()
		{
			// Handled means consumed. A chart that swallows every motion breaks
			// window dragging for anything underneath it.
			var (system, chart) = Build();
			chart.EnableHover = false;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9 });
			Draw(system);

			Assert.False(Move(chart, 10, 10));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AMoveFarFromAnyPointIsNotClaimed()
		{
			// Hover enabled but nothing near the pointer: still not ours to consume.
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 1, 1 });
			Draw(system);

			Assert.False(Move(chart, 2000, 2000));
			GC.KeepAlive(system);
		}

		[Fact]
		public void HoveringDrawsACrosshair()
		{
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Move(chart, x, y);
			var snap = Draw(system);

			bool crosshair = false;
			for (int cy = 0; cy < snap.Height && !crosshair; cy++)
			{
				for (int cx = 0; cx < snap.Width && !crosshair; cx++)
				{
					int cp = snap.GetCell(cx, cy).Character.Value;
					if (cp == '│' || cp == '─') crosshair = true;
				}
			}

			Assert.True(crosshair, "hovering drew no crosshair");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheReadoutNamesTheSeriesAndItsValue()
		{
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("throughput", Color.Red).SetValues(new[] { 10d, 90, 10 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Move(chart, x, y);
			var snap = Draw(system);

			var sb = new System.Text.StringBuilder();
			for (int cy = 0; cy < snap.Height; cy++)
			{
				for (int cx = 0; cx < snap.Width; cx++) sb.Append(snap.GetCell(cx, cy).Character);
			}

			Assert.Contains("throughput", sb.ToString());
			GC.KeepAlive(system);
		}

		[Fact]
		public void HoverSurvivesARerender()
		{
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Move(chart, x, y);
			Draw(system);
			var held = chart.HoveredPoint;

			Draw(system);
			Draw(system);

			Assert.Equal(held, chart.HoveredPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TurningHoverOffClearsAnyExistingHover()
		{
			// Otherwise a stale crosshair and readout stay painted over a chart that
			// is no longer interactive.
			var (system, chart) = Build();
			chart.EnableHover = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);
			Move(chart, x, y);
			Draw(system);
			Assert.NotNull(chart.HoveredPoint);

			chart.EnableHover = false;
			Draw(system);

			Assert.Null(chart.HoveredPoint);
			GC.KeepAlive(system);
		}
	}
}
