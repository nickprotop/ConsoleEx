// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// First end-to-end render coverage for <see cref="GridControl"/>: proves the layout engine builds
/// the grid's cells into the DOM tree (via <see cref="LayoutNodeFactory"/>) and paints each cell's
/// content within its arranged bounds using <see cref="GridLayout"/>.
/// </summary>
public class GridRenderTests
{
	[Fact]
	public void Grid_RendersEachCellContent_InItsCell()
	{
		var grid = new GridControl { Width = 40, Height = 6 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "LEFTCELL" }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "RIGHTCELL" }), 0, 1);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);

		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("LEFTCELL", stripped);
		Assert.Contains("RIGHTCELL", stripped);
	}

	[Fact]
	public void Grid_ClipsOverflowingCellContent_DoesNotBleedIntoNeighbor()
	{
		var grid = new GridControl { Width = 16, Height = 4 };
		grid.ColumnDefinitions.Add(GridLength.Cells(8));
		grid.ColumnDefinitions.Add(GridLength.Cells(8));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { new string('X', 16) }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "RIGHT" }), 0, 1);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);

		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		// The right cell's content survives — the left cell's overflow did not overwrite it.
		Assert.Contains("RIGHT", stripped);

		// The left cell's 16-char run is clipped to its 8-cell column: no run of >8 X's appears.
		Assert.DoesNotMatch(new Regex("X{9,}"), stripped);
	}

	[Fact]
	public void AutoFlow_Renders()
	{
		// Star tracks need a bounded extent to divide, so a top-level grid is given a concrete size
		// (mirrors how ScrollablePanelControl is sized in the panel tests).
		var grid = new GridControl { Width = 40, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.AddControl(new MarkupControl(new List<string> { "ALPHA" }));
		grid.AddControl(new MarkupControl(new List<string> { "BETA" }));
		grid.AddControl(new MarkupControl(new List<string> { "GAMMA" }));

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);

		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("ALPHA", stripped);
		Assert.Contains("BETA", stripped);
		Assert.Contains("GAMMA", stripped);
	}
}
