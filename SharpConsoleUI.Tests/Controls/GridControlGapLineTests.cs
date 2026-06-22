// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

// Tests for the centered gap-line feature: splitter/gridline boundary glyphs draw CENTERED in
// their gap (offset[n]+size[n]+gap/2), the splitter hit-test (s.Bounds) tracks the glyph, and
// gridline crossings render edge-aware junctions (┼ interior, ┬ where a span/edge removes an arm).
public class GridControlGapLineTests
{
	private static (ConsoleWindowSystem system, Window window, GridControl grid) BuildGrid(
		GridLength[] cols, GridLength[] rows, int width = 60, int height = 16, int colGap = 0, int rowGap = 0)
	{
		var grid = new GridControl { Width = width - 2, Height = height - 2 };
		foreach (var c in cols) grid.ColumnDefinitions.Add(c);
		foreach (var r in rows) grid.RowDefinitions.Add(r);
		grid.ColumnGap = colGap;
		grid.RowGap = rowGap;
		for (int r = 0; r < rows.Length; r++)
			for (int c = 0; c < cols.Length; c++)
				grid.Place(new MarkupControl(new List<string> { $"r{r}c{c}" }), r, c);

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		return (system, window, grid);
	}

	private static string RenderedText(Window window)
		=> ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());

	// 1) With a gap of 2, the column splitter glyph centres at offset0+size0+gap/2. colOffset0 is 0,
	//    so the control-relative Bounds.X == arranged size0 + ColumnGap/2 (= +1).
	[Fact]
	public void Gap2_ColumnSplitter_BoundsX_IsCentred()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(10), GridLength.Cells(10) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 2);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		var s = grid.GetColumnSplitterForTest(0);
		int expected = grid.GetColumnArrangedSizeForTest(0) + grid.ColumnGap / 2; // colOffset0 == 0
		Assert.Equal(expected, s.Bounds.X);
	}

	// 2) Regression: with a gap of 1 the glyph is unchanged (gap/2 == 0), so Bounds.X == arranged size0.
	[Fact]
	public void Gap1_ColumnSplitter_BoundsX_Unchanged()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(10), GridLength.Cells(10) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		int expected = grid.GetColumnArrangedSizeForTest(0) + grid.ColumnGap / 2; // gap/2 == 0
		Assert.Equal(expected, grid.GetColumnSplitterForTest(0).Bounds.X);
	}

	// 3) A centred column gridline (gap 2) still renders its vertical glyph.
	[Fact]
	public void Gap2_Gridline_Renders()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(10), GridLength.Cells(10) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 2);
		grid.ShowColumnGridlines = true;
		system.Render.UpdateDisplay();

		Assert.Contains('│', RenderedText(window));
	}

	// 4) Edge-aware junctions: a header that spans both columns blocks the column line above the
	//    body, so where the column line meets the first row boundary it is a top-tee (┬). Between the
	//    two body rows the same column line crosses a full row line → interior cross (┼). No splitter
	//    is present, so the splitter-crossing glyph (╬) must not appear.
	[Fact]
	public void Junction_TopTee_AtHeaderSpanBoundary()
	{
		var grid = new GridControl { Width = 60 - 2, Height = 16 - 2 };
		grid.ColumnDefinitions.Add(GridLength.Cells(12));
		grid.ColumnDefinitions.Add(GridLength.Cells(12));
		grid.RowDefinitions.Add(GridLength.Auto());
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.ColumnGap = 1;
		grid.RowGap = 1;

		grid.Place(new MarkupControl(new List<string> { "header" }), 0, 0, colSpan: 2);
		grid.Place(new MarkupControl(new List<string> { "a" }), 1, 0);
		grid.Place(new MarkupControl(new List<string> { "b" }), 1, 1);
		grid.Place(new MarkupControl(new List<string> { "c" }), 2, 0);
		grid.Place(new MarkupControl(new List<string> { "d" }), 2, 1);
		grid.ShowColumnGridlines = true;
		grid.ShowRowGridlines = true;

		var system = TestWindowSystemBuilder.CreateTestSystem(60, 16);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		string text = RenderedText(window);
		Assert.Contains('┬', text);          // header boundary blocks the up arm → top-tee
		Assert.Contains('┼', text);          // interior crossing between body rows 1 and 2
		Assert.DoesNotContain('╬', text);    // no splitter present
	}

	// 5) Key proof that paint == hit-test after centering: pressing exactly on the CENTRED handle's
	//    screen X (derived from s.Bounds.X) starts the drag and the resize takes effect.
	[Fact]
	public void Gap2_RealDrag_CentredColumnSplitter_Resizes()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(12), GridLength.Cells(12) }, new[] { GridLength.Star(1) },
			width: 48, height: 8, colGap: 2);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		int w0 = grid.ColumnDefinitions[0].Value;
		int w1 = grid.ColumnDefinitions[1].Value;

		int sx = grid.GetColumnSplitterScreenX(0, window);
		int sy = window.Top + 1 + grid.ActualY + 2;

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(sx, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged }, new Point(sx + 4, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(sx + 4, sy));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		Assert.True(grid.ColumnDefinitions[0].Value > w0, "col0 should have grown (press hit the centred handle)");
		Assert.True(grid.ColumnDefinitions[1].Value < w1, "col1 should have shrunk");
	}
}
