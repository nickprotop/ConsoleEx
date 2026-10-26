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
	/// <summary>Picking a point with the mouse or the keyboard.</summary>
	public class ChartSelectionTests
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

		private static bool Send(ChartControl chart, MouseFlags flag, int x, int y)
		{
			var p = new Point(x, y);
			var args = new MouseEventArgs(new List<MouseFlags> { flag }, p, p, p);
			((IMouseAwareControl)chart).ProcessMouseEvent(args);
			return args.Handled;
		}

		private static bool Key(ChartControl chart, ConsoleKey key) =>
			((IInteractiveControl)chart).ProcessKey(new ConsoleKeyInfo('\0', key, false, false, false));

		private static (int X, int Y) APlottedCell(ChartControl chart, int index = 0)
		{
			var p = chart.PointPositionsForTest();
			Assert.True(p.Count > index, "not enough drawn points");
			return (p[index].CellX, p[index].CellY);
		}

		[Fact]
		public void ClickingAPointSelectsIt()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Send(chart, MouseFlags.Button1Clicked, x, y);
			Draw(system);

			Assert.NotNull(chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void SelectingTheSamePointTwiceRaisesTheEventOnce()
		{
			// Guard before firing: a dashboard that redraws on SelectionChanged
			// would otherwise loop on a steady mouse.
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			int fired = 0;
			chart.SelectionChanged += (_, _) => fired++;

			Send(chart, MouseFlags.Button1Clicked, x, y);
			Draw(system);
			Send(chart, MouseFlags.Button1Clicked, x, y);
			Draw(system);

			Assert.Equal(1, fired);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheSelectionSurvivesARerender()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Send(chart, MouseFlags.Button1Clicked, x, y);
			Draw(system);
			var selected = chart.SelectedPoint;

			Draw(system);
			Draw(system);

			Assert.Equal(selected, chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void SelectionIsOffUnlessEnabled()
		{
			var (system, chart) = Build();
			chart.EnableSelection = false;
			chart.EnableHover = true;   // something else on, so the chart takes the mouse
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);

			Send(chart, MouseFlags.Button1Clicked, x, y);
			Draw(system);

			Assert.Null(chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ClickingAwayFromAnyPointClearsTheSelection()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1 });
			Draw(system);
			var (x, y) = APlottedCell(chart);
			Send(chart, MouseFlags.Button1Clicked, x, y);
			Draw(system);
			Assert.NotNull(chart.SelectedPoint);

			Send(chart, MouseFlags.Button1Clicked, 2000, 2000);
			Draw(system);

			Assert.Null(chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ClickingALegendEntryTogglesItsSeries()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.ShowLegend = true;
			var s = chart.AddSeries("alpha", Color.Red);
			s.SetValues(new[] { 1d, 9, 1 });
			Draw(system);

			var region = chart.LegendRegionsForTest();
			Assert.NotEmpty(region);
			var rect = region[0].Rect;

			Assert.True(s.IsVisible);
			Send(chart, MouseFlags.Button1Clicked, rect.X, rect.Y);
			Draw(system);

			Assert.False(s.IsVisible);
			GC.KeepAlive(system);
		}

		[Fact]
		public void RightArrowMovesToTheNextPoint()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			Draw(system);

			Key(chart, ConsoleKey.Home);
			Draw(system);
			var first = chart.SelectedPoint;

			Key(chart, ConsoleKey.RightArrow);
			Draw(system);

			Assert.NotNull(first);
			Assert.NotEqual(first, chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ArrowingPastTheEndStopsRatherThanWrapping()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2, 3 });
			Draw(system);
			Key(chart, ConsoleKey.End);
			Draw(system);
			var last = chart.SelectedPoint;

			for (int i = 0; i < 5; i++) { Key(chart, ConsoleKey.RightArrow); Draw(system); }

			Assert.Equal(last, chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void EnterActivatesTheSelectedPoint()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2, 3 });
			Draw(system);
			Key(chart, ConsoleKey.Home);
			Draw(system);

			ChartPoint? activated = null;
			chart.PointActivated += (_, e) => activated = e.Point;

			Key(chart, ConsoleKey.Enter);

			Assert.NotNull(activated);
			GC.KeepAlive(system);
		}

		[Fact]
		public void EscapeClearsTheSelection()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2, 3 });
			Draw(system);
			Key(chart, ConsoleKey.Home);
			Draw(system);
			Assert.NotNull(chart.SelectedPoint);

			Key(chart, ConsoleKey.Escape);
			Draw(system);

			Assert.Null(chart.SelectedPoint);
			GC.KeepAlive(system);
		}

		[Fact]
		public void KeysAreIgnoredOnAnEmptyChart()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			Draw(system);

			var ex = Record.Exception(() =>
			{
				Key(chart, ConsoleKey.RightArrow);
				Key(chart, ConsoleKey.End);
				Key(chart, ConsoleKey.Enter);
				Draw(system);
			});

			Assert.Null(ex);
			GC.KeepAlive(system);
		}

		[Fact]
		public void KeysAreDeclinedWhenSelectionIsOff()
		{
			// Declining lets the key bubble to whatever else wanted it.
			var (system, chart) = Build();
			chart.EnableSelection = false;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2, 3 });
			Draw(system);

			Assert.False(Key(chart, ConsoleKey.RightArrow));
			Assert.False(Key(chart, ConsoleKey.Home));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AnUnrelatedKeyIsDeclined()
		{
			// A chart that swallows every key breaks the window's own shortcuts.
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2, 3 });
			Draw(system);

			Assert.False(Key(chart, ConsoleKey.F5));
			Assert.False(Key(chart, ConsoleKey.Tab));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheChartIsFocusableOnlyWhenSelectionIsOn()
		{
			var passive = new ChartControl();
			var interactive = new ChartControl { EnableSelection = true };

			Assert.False(((IFocusableControl)passive).CanReceiveFocus);
			Assert.True(((IFocusableControl)interactive).CanReceiveFocus);
		}

		[Fact]
		public void TheSelectedPointIsMarkedOnScreen()
		{
			// A selection nobody can see is not a selection. The user has to know
			// which point the keyboard is sitting on.
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 1, 9 });
			var before = Draw(system);
			string plain = TextOf(before);

			Key(chart, ConsoleKey.Home);
			string marked = TextOf(Draw(system));

			Assert.NotEqual(plain, marked);
			GC.KeepAlive(system);
		}

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
			}
			return sb.ToString();
		}

		[Fact]
		public void TurningSelectionOffClearsIt()
		{
			var (system, chart) = Build();
			chart.EnableSelection = true;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 2, 3 });
			Draw(system);
			Key(chart, ConsoleKey.Home);
			Draw(system);
			Assert.NotNull(chart.SelectedPoint);

			chart.EnableSelection = false;
			Draw(system);

			Assert.Null(chart.SelectedPoint);
			GC.KeepAlive(system);
		}
	}
}
