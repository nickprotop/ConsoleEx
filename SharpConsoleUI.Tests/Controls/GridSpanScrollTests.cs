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
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// Mirrors the DemoApp Grid Layout shape — 3 Star cols, [Auto, Star, Star] rows,
/// a Fill ScrollablePanel spanning two Star rows must get a bounded viewport and scroll on wheel.
public class GridSpanScrollTests
{
	private readonly ITestOutputHelper _out;
	public GridSpanScrollTests(ITestOutputHelper o) => _out = o;

	[Fact]
	public void FillSpc_SpanningTwoStarRows_GetsBoundedViewport()
	{
		var spc = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill, BorderStyle = BorderStyle.None };
		for (int i = 0; i < 30; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"log line {i}" }));

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

		spc.SyncMetricsFromArrangedBounds();
		var node = window.Renderer?.GetLayoutNode(spc);
		_out.WriteLine($"spc arranged bounds = {node?.AbsoluteBounds}  viewport={spc.ContentViewportHeight}  content=30 rows");

		// The two Star rows split the grid's remaining height (~28 minus Auto header). The SPC spans both
		// (~24 tall). Its viewport must be FAR less than its 30-row content so it can scroll. If the row(s)
		// grew to the SPC's full content height, viewport ~= content and scrolling is dead — the live bug.
		Assert.True(spc.ContentViewportHeight < 30,
			$"Fill SPC spanning Star rows should have a bounded viewport (<30), got {spc.ContentViewportHeight}");
	}

	[Fact]
	public void FillSpc_SpanningTwoStarRows_WheelScrolls()
	{
		var spc = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill, BorderStyle = BorderStyle.None };
		for (int i = 0; i < 30; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"log line {i}" }));

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

		spc.SyncMetricsFromArrangedBounds();
		int before = spc.VerticalScrollOffset;
		spc.ProcessMouseEvent(ContainerTestHelpers.CreateWheelDown(80, 15));
		int after = spc.VerticalScrollOffset;
		_out.WriteLine($"wheel: {before} -> {after}");

		Assert.True(after > before, $"Fill SPC in a spanning grid cell must scroll on wheel; {before} -> {after}");
	}
}
