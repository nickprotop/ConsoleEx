// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class GridControlGridlineTests
{
	private static (ConsoleWindowSystem system, Window window, GridControl grid) BuildGrid(
		GridLength[] cols, GridLength[] rows, int width = 60, int height = 16)
	{
		var grid = new GridControl { Width = width - 2, Height = height - 2 };
		foreach (var c in cols) grid.ColumnDefinitions.Add(c);
		foreach (var r in rows) grid.RowDefinitions.Add(r);
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

	private static string RenderedText(ConsoleWindowSystem system, Window window)
	{
		system.Render.UpdateDisplay();
		return ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
	}

	[Fact]
	public void ShowColumnGridlines_DrawsVerticalRule()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.ShowColumnGridlines = true;
		Assert.Contains('│', RenderedText(system, window));
	}

	[Fact]
	public void ShowRowGridlines_DrawsHorizontalRule()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1) }, new[] { GridLength.Star(1), GridLength.Star(1) });
		grid.ShowRowGridlines = true;
		Assert.Contains('─', RenderedText(system, window));
	}

	[Fact]
	public void NoGridlines_NoCrossGlyph()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) },
			new[] { GridLength.Star(1), GridLength.Star(1) });
		Assert.DoesNotContain('┼', RenderedText(system, window));
	}

	[Fact]
	public void PerBoundaryGridline_OnlyAddsThatBoundary()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.AddGridlineAfterColumn(0);
		Assert.True(grid.HasGridlineAfterColumn(0));
		Assert.Contains('│', RenderedText(system, window));
	}

	[Fact]
	public void BothAxes_DrawJunctionCross()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) },
			new[] { GridLength.Star(1), GridLength.Star(1) });
		grid.ShowColumnGridlines = true;
		grid.ShowRowGridlines = true;
		Assert.Contains('┼', RenderedText(system, window));
	}

	[Fact]
	public void SplitterBoundary_RendersSplitterGlyph_NotGridline()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1), GridLength.Star(1) },
			new[] { GridLength.Star(1) });
		grid.ShowColumnGridlines = true;
		grid.AddColumnSplitterAfter(0);
		var text = RenderedText(system, window);
		Assert.Contains('║', text);
		Assert.Contains('│', text);
	}

	[Fact]
	public void SpanningCell_SuppressesGridlineAcrossSpan()
	{
		int width = 60, height = 16;
		var grid = new GridControl { Width = width - 2, Height = height - 2 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Auto());
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "header" }), 0, 0, rowSpan: 1, colSpan: 2);
		grid.Place(new MarkupControl(new List<string> { "a" }), 1, 0);
		grid.Place(new MarkupControl(new List<string> { "b" }), 1, 1);
		grid.ShowColumnGridlines = true;

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var text = RenderedText(system, window);
		Assert.Contains('│', text);
		Assert.Contains("header", text);
	}

	[Fact]
	public void GridlineColor_Explicit_OverridesRoleDim()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.GridlineColor = new Color(10, 200, 30);
		Assert.Equal(new Color(10, 200, 30), grid.GetGridlineColorForTest());
	}

	[Fact]
	public void GridlineColor_RoleDriven_IsNonDefault()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.ColorRole = SharpConsoleUI.Themes.ColorRole.Primary;
		grid.ShowColumnGridlines = true;
		system.Render.UpdateDisplay();
		Assert.NotEqual(default(Color), grid.GetGridlineColorForTest());
	}

	[Fact]
	public void DoubleStyle_UsesDoubleGlyphs()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) },
			new[] { GridLength.Star(1), GridLength.Star(1) });
		grid.ShowColumnGridlines = true;
		grid.ShowRowGridlines = true;
		grid.GridlineStyle = BorderStyle.DoubleLine;
		var text = RenderedText(system, window);
		Assert.Contains('║', text);
		Assert.Contains('═', text);
		Assert.Contains('╬', text);
	}

	[Fact]
	public void OutOfRangeAndSingleTrack_DoNotThrow()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.ShowColumnGridlines = true;
		grid.ShowRowGridlines = true;
		grid.AddGridlineAfterColumn(99);
		var ex = Record.Exception(() => RenderedText(system, window));
		Assert.Null(ex);
	}

	[Fact]
	public void AutoGap_BumpsGapToOne_WhenGridlinesOnWithZeroGap()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(10), GridLength.Cells(10) }, new[] { GridLength.Star(1) });
		grid.ColumnGap = 0;
		grid.ShowColumnGridlines = true;
		Assert.Contains('│', RenderedText(system, window));
	}

	[Fact]
	public void RealWindow_GridlinedTable_RendersAndSurvivesRerender()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1), GridLength.Star(1) },
			new[] { GridLength.Star(1), GridLength.Star(1) },
			width: 72, height: 16);
		grid.ShowColumnGridlines = true;
		grid.ShowRowGridlines = true;
		var first = RenderedText(system, window);
		Assert.Contains('│', first);
		Assert.Contains('─', first);
		Assert.Contains('┼', first);
		var second = RenderedText(system, window);
		Assert.Contains('┼', second);
	}
}
