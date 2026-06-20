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

/// A ScrollablePanel hosted in a grid cell must get a bounded viewport (the cell size), not its full
/// content height — so it can scroll inside the cell. Regression guard for the AlignAxis clamp (a child
/// is never arranged larger than its cell).
public class GridNestedScrollTests
{
	[Fact]
	public void Spc_InGridCell_HasBoundedViewport_NotFullContentHeight()
	{
		// A grid cell sized smaller than its SPC content. The SPC must end up with a viewport SMALLER than
		// its content (so it can scroll), not a viewport equal to content height.
		var spc = new ScrollablePanelControl { BorderStyle = BorderStyle.None };
		for (int i = 0; i < 40; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"row {i}" }));

		var grid = new GridControl { Width = 30, Height = 12 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1)); // one cell, ~12 tall, holding 40 rows of content
		grid.Place(spc, 0, 0);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		spc.SyncMetricsFromArrangedBounds();

		// The SPC's visible viewport must be much less than its content (40 rows) so it can scroll.
		// If this fails (viewport ~= content), maxScroll == 0 and the wheel is a no-op — the bug.
		Assert.True(spc.ContentViewportHeight > 0, "viewport height should be positive");
		Assert.True(spc.ContentViewportHeight < 40,
			$"SPC in a grid cell should have a CLIPPED viewport (<40), got {spc.ContentViewportHeight} — " +
			"if it equals content height, the grid arranged the cell at full content height and scrolling is dead");
	}

	[Fact]
	public void Spc_InGridCell_WheelDown_ChangesScrollOffset()
	{
		var spc = new ScrollablePanelControl { BorderStyle = BorderStyle.None };
		for (int i = 0; i < 40; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"row {i}" }));

		var grid = new GridControl { Width = 30, Height = 12 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(spc, 0, 0);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		int before = spc.VerticalScrollOffset;
		// Wheel down inside the cell (a point well within the grid/cell area).
		spc.SyncMetricsFromArrangedBounds();
		var wheel = ContainerTestHelpers.CreateWheelDown(5, 5);
		spc.ProcessMouseEvent(wheel);
		int after = spc.VerticalScrollOffset;

		Assert.True(after > before,
			$"wheel-down on an SPC in a grid cell should scroll; offset went {before} -> {after}");
	}
}
