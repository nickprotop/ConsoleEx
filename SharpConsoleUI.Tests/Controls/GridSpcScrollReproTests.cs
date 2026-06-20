// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression for the live "SPC inside a grid cell won't scroll to the end" bug. Replicates the
/// DemoApp Grid Layout page in a NARROW, SHORT window so each star-sized cell is allotted more
/// height during the grid's MEASURE pass than the window can actually paint, then drives the
/// focused panel to the bottom with real DownArrow KEY events (the live input path:
/// EnqueueKey + Input.ProcessInput, re-rendering each tick).
///
/// Root cause: ScrollLayout.MeasureChildren clamped the panel's persisted scroll offset against the
/// cell's measure-time extent (e.g. 20 rows) instead of the real painted viewport (e.g. 8 rows),
/// pinning the offset to (content − cellExtent) on every frame and capping the scroll partway.
/// The clamp now happens only in the arrange/paint passes, which use the true on-screen box, so the
/// scroll reaches its real end.
/// </summary>
public class GridSpcScrollReproTests
{
	// Deliberately narrow AND short: the 3 star columns are ~1/3 of the window (content overflows
	// horizontally so it would wrap), and the window is too short to show a full star-row cell.
	private const int WinW = 60;
	private const int WinH = 22;

	private static ScrollablePanelControl MakeScrollLog(string prefix, int lines, bool wrap)
	{
		var spc = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		for (int i = 0; i < lines; i++)
		{
			string text = wrap
				? $"{i:00} {prefix} this is a longish log line that should wrap in a narrow column"
				: $"{i:00} {prefix} ok";
			spc.AddControl(Builders.Controls.Markup(text).WithMargin(1, 0, 1, 0).Build());
		}
		return spc;
	}

	private static (ConsoleWindowSystem system, Window window, List<ScrollablePanelControl> panels, string[] names)
		BuildNarrowGridAllCellsScrolling(bool wrap)
	{
		var panels = new List<ScrollablePanelControl>();

		// One scrolling SPC per body cell (rows 1 & 2, cols 0..2) plus the row-spanning alerts cell.
		var cpu = MakeScrollLog("CPU", 40, wrap); panels.Add(cpu);
		var res = MakeScrollLog("RES", 40, wrap); panels.Add(res);
		var alerts = MakeScrollLog("ALERT", 45, wrap); panels.Add(alerts);
		var svc = MakeScrollLog("SVC", 40, wrap); panels.Add(svc);
		var cmd = MakeScrollLog("CMD", 40, wrap); panels.Add(cmd);

		var header = new MarkupControl(new List<string> { "System Dashboard" }) { Margin = new Margin(1, 0, 1, 0) };

		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Auto());
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowGap = 1;
		grid.ColumnGap = 2;

		grid.Place(header, 0, 0, colSpan: 3);
		grid.Place(cpu, 1, 0);
		grid.Place(res, 1, 1);
		grid.Place(alerts, 1, 2, rowSpan: 2);
		grid.Place(svc, 2, 0);
		grid.Place(cmd, 2, 1);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment(
			sysW: WinW + 10, sysH: WinH + 6, winW: WinW, winH: WinH);
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.RenderAndGetVisibleContent();

		string[] names = { "CPU", "RES", "ALERTS(span)", "SVC", "CMD" };
		return (system, window, panels, names);
	}

	/// <summary>
	/// Drives a focused SPC to its end with DownArrow key events (the live path), re-rendering each
	/// tick, then returns (reachedOffset, trueMaxOffset). The true max is read from the panel's own
	/// arranged metrics so the assertion is independent of the exact viewport math.
	/// </summary>
	private static (int reached, int trueMax) DriveToEnd(ConsoleWindowSystem system, Window window, ScrollablePanelControl spc)
	{
		window.FocusManager.SetFocus(spc, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var down = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);

		int last = -1, stuck = 0;
		for (int i = 0; i < 400 && stuck < 8; i++)
		{
			system.InputStateService.EnqueueKey(down);
			system.Input.ProcessInput();
			window.RenderAndGetVisibleContent();
			int off = spc.VerticalScrollOffset;
			if (off == last) stuck++; else stuck = 0;
			last = off;
		}

		spc.SyncMetricsFromArrangedBounds();
		int trueMax = Math.Max(0, spc.TotalContentHeightInternal - spc.ContentViewportHeight);
		return (spc.VerticalScrollOffset, trueMax);
	}

	[Fact]
	public void AllCells_KeyDriven_NonWrapping_ScrollToEnd()
	{
		var (system, window, panels, names) = BuildNarrowGridAllCellsScrolling(wrap: false);
		for (int i = 0; i < panels.Count; i++)
		{
			var (reached, trueMax) = DriveToEnd(system, window, panels[i]);
			Assert.True(trueMax > 0, $"[{names[i]}] content must overflow its cell (something to scroll)");
			Assert.True(reached == trueMax,
				$"[{names[i]}] key-driven scroll capped at {reached}, expected end {trueMax}");
		}
	}

	[Fact]
	public void AllCells_KeyDriven_Wrapping_ScrollToEnd()
	{
		var (system, window, panels, names) = BuildNarrowGridAllCellsScrolling(wrap: true);
		for (int i = 0; i < panels.Count; i++)
		{
			var (reached, trueMax) = DriveToEnd(system, window, panels[i]);
			Assert.True(trueMax > 0, $"[{names[i]}] wrapped content must overflow its cell");
			Assert.True(reached == trueMax,
				$"[{names[i]}] key-driven scroll capped at {reached}, expected end {trueMax}");
		}
	}
}
