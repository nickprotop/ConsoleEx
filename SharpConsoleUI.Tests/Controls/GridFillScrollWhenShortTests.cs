// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// "Real-thing" regression for the GridControl-won't-scroll-when-short bug: a GridControl with
/// VerticalAlignment.Fill, hosted as the single child of a ScrollablePanelControl, in a window
/// SHORTER than the grid's natural fixed-row content.
///
/// Observed today (the bug): the Fill grid is measured against the panel's per-Fill slot and then
/// pinned to it (ScrollablePanelControl.ComputeChildHeight does Math.Max(measured, perFillHeight)),
/// so the panel sees content == viewport, TotalContentHeight collapses to the viewport height, and
/// CanScrollDown is false — the fixed rows squash instead of the panel scrolling. (A NON-Fill Cells
/// grid already reports its true content height and scrolls correctly; only the Fill case is broken.)
///
/// Desired (the assertion below): a Fill grid still fills when there is room, but reports a real
/// minimum content height (sum of fixed Cells rows + gaps) when the viewport is shorter than that
/// minimum, so the hosting panel shows a scrollbar and can scroll to reveal the clipped rows.
///
/// NOTE: this test currently FAILS — it pins the bug. No fix is applied yet.
/// </summary>
public class GridFillScrollWhenShortTests
{
	// Four fixed rows totaling 28 cells — far taller than the 10-row window below.
	private static readonly int[] RowHeights = { 7, 7, 6, 8 };
	private const int NaturalContentHeight = 28; // sum of RowHeights, no gaps

	private static GridControl MakeFillCellsGrid()
	{
		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		foreach (var h in RowHeights)
			grid.RowDefinitions.Add(GridLength.Cells(h));
		for (int r = 0; r < RowHeights.Length; r++)
			grid.Place(new MarkupControl(new List<string> { $"row {r}" }), r, 0);
		return grid;
	}

	private static (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window) HostInShortPanel(GridControl grid, int winHeight)
	{
		var panel = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		Assert.Equal(ScrollMode.Scroll, panel.VerticalScrollMode); // default is scroll
		panel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment(
			sysW: 50, sysH: winHeight + 6, winW: 40, winH: winHeight);
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.RenderAndGetVisibleContent();
		return (panel, system, window);
	}

	[Fact]
	public void FillGrid_TallerThanViewport_PanelCanScroll()
	{
		var grid = MakeFillCellsGrid();
		var (panel, _, _) = HostInShortPanel(grid, winHeight: 10); // window shorter than 28-row content

		// The panel must see the grid's real content (>= its fixed-row minimum), not a viewport-clamped value,
		// so it can scroll to reveal the rows that don't fit.
		Assert.True(
			panel.TotalContentHeight >= NaturalContentHeight,
			$"expected panel content >= {NaturalContentHeight} (grid's fixed-row minimum), got {panel.TotalContentHeight}");
		Assert.True(panel.ShowScrollbar, "panel should show a scrollbar when the Fill grid overflows the viewport");
		Assert.True(panel.CanScrollDown, "panel should be able to scroll down to reveal the clipped grid rows");
	}

	[Fact]
	public void FillGrid_TallerThanViewport_FillStillFillsWhenRoomAvailable()
	{
		// Regression guard for the fix: when the window is TALLER than the content, a Fill grid must still
		// fill the available space (no scrollbar). This pins the "no regression to Fill" acceptance criterion.
		var grid = MakeFillCellsGrid();
		var (panel, _, _) = HostInShortPanel(grid, winHeight: NaturalContentHeight + 12); // plenty of room

		Assert.False(panel.CanScrollDown, "a Fill grid with room to spare should not need to scroll");
	}
}
