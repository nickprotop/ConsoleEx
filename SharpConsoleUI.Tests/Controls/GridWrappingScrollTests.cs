// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression for the "scrolls but not to the end" bug. A ScrollablePanel whose child text WRAPS, hosted
/// in a narrow grid cell, must scroll all the way to its real end. The root cause was the grid's pass-1
/// measure sizing each cell against the FULL grid width instead of its column width: a wrapping child then
/// reported (and cached) a no-wrap content height that was too small, capping the scroll partway. Pass-1
/// now measures each cell at its estimated column width, so the content height is consistent and the
/// scroll reaches the end (finalOffset == contentHeight - viewport).
/// </summary>
public class GridWrappingScrollTests
{
	private static (ScrollablePanelControl spc, Window window) BuildWrappingLogGrid()
	{
		var spc = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		// Lines long enough to wrap in a narrow (~1/3 of 90) column.
		for (int i = 0; i < 45; i++)
			spc.AddControl(Builders.Controls.Markup($"{i} WARN this is a longish log line that wraps").WithMargin(1, 0, 1, 0).Build());

		var grid = new GridControl { Width = 90, Height = 28 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Auto());
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "header" }), 0, 0, colSpan: 3);
		grid.Place(spc, 1, 2, rowSpan: 2);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		return (spc, window);
	}

	[Fact]
	public void WrappingContent_InNarrowCell_ScrollsToTheEnd()
	{
		var (spc, window) = BuildWrappingLogGrid();

		// Drive scroll to the end, re-rendering each step like the live loop does, until it stops moving.
		int last = -1, stuck = 0;
		for (int i = 0; i < 300 && stuck < 6; i++)
		{
			spc.ScrollVerticalBy(1);
			window.RenderAndGetVisibleContent();
			int off = spc.VerticalScrollOffset;
			if (off == last) stuck++; else stuck = 0;
			last = off;
		}

		int expectedMax = System.Math.Max(0, spc.TotalContentHeightInternal - spc.ContentViewportHeight);
		Assert.True(expectedMax > 0, "the wrapping content must overflow its cell (so there is something to scroll)");
		Assert.Equal(expectedMax, spc.VerticalScrollOffset);
	}

	[Fact]
	public void WrappingContent_ContentHeight_ReflectsWrappedRows()
	{
		var (spc, _) = BuildWrappingLogGrid();
		spc.SyncMetricsFromArrangedBounds();

		// 45 lines that each wrap to 2 rows in the narrow column → content height well above 45.
		Assert.True(spc.TotalContentHeightInternal > 45,
			$"wrapped content height should exceed the {45} logical lines, got {spc.TotalContentHeightInternal}");
	}
}
