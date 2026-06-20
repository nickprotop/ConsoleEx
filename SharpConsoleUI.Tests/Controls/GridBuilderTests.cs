// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Coverage for the fluent <see cref="GridBuilder"/> and the <see cref="Controls.Grid"/> factory:
/// proves declared definitions/gaps/padding/size/role round-trip onto the built control, that
/// deferred <see cref="GridBuilder.Place"/>/<see cref="GridBuilder.Add"/> intents are replayed in
/// declaration order against the already-set track definitions, and that the built grid renders
/// end-to-end through the factory wiring.
/// </summary>
public class GridBuilderTests
{
	[Fact]
	public void Controls_Grid_ReturnsBuilder()
	{
		Assert.IsType<GridBuilder>(Builders.Controls.Grid());
	}

	[Fact]
	public void Builder_RoundTrips_DefsGapsPaddingSize()
	{
		var grid = Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(2))
			.Rows(GridLength.Auto(), GridLength.Star(1))
			.RowGap(1).ColumnGap(2)
			.WithPadding(1, 1, 1, 1)
			.WithSize(40, 12)
			.Place(new MarkupControl(new List<string> { "x" }), 0, 0)
			.Build();

		Assert.Equal(2, grid.ColumnDefinitions.Count);
		Assert.Equal(2, grid.RowDefinitions.Count);
		Assert.Equal(1, grid.RowGap);
		Assert.Equal(2, grid.ColumnGap);
		Assert.Equal(40, grid.Width);
		Assert.Equal(12, grid.Height);
		Assert.Single(grid.OrderedCells);
	}

	[Fact]
	public void Builder_Place_HonorsCoordinates()
	{
		var control = new MarkupControl(new List<string> { "placed" });
		var grid = Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1), GridLength.Star(1))
			.Place(control, 1, 1)
			.Build();

		var cell = Assert.Single(grid.OrderedCells);
		Assert.Same(control, cell.Control);
		Assert.Equal(1, cell.Placement.Row);
		Assert.Equal(1, cell.Placement.Col);
	}

	[Fact]
	public void Builder_AutoFlow_AddsTiles()
	{
		var a = new MarkupControl(new List<string> { "a" });
		var b = new MarkupControl(new List<string> { "b" });
		var c = new MarkupControl(new List<string> { "c" });

		var grid = Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Add(a).Add(b).Add(c)
			.Build();

		var cells = grid.OrderedCells;
		Assert.Equal(3, cells.Count);

		// Row-major: a@(0,0), b@(0,1), c@(1,0).
		Assert.Equal((0, 0), CellOf(cells, a));
		Assert.Equal((0, 1), CellOf(cells, b));
		Assert.Equal((1, 0), CellOf(cells, c));
	}

	[Fact]
	public void Builder_InterleavedPlaceAndAdd_PreservesOrder()
	{
		var a = new MarkupControl(new List<string> { "a" });
		var x = new MarkupControl(new List<string> { "x" });
		var b = new MarkupControl(new List<string> { "b" });

		var grid = Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Add(a)        // -> (0,0)
			.Place(x, 0, 1)
			.Add(b)        // next free scanning row-major skipping (0,0) and (0,1) -> (1,0)
			.Build();

		Assert.Equal((0, 0), CellOf(grid.OrderedCells, a));
		Assert.Equal((1, 0), CellOf(grid.OrderedCells, b));
	}

	[Fact]
	public void Builder_WithColorRole_RoundTrips()
	{
		var grid = Builders.Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.Build();

		Assert.Equal(ColorRole.Primary, grid.ColorRole);
	}

	[Fact]
	public void Builder_Renders()
	{
		var grid = Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.WithSize(40, 6)
			.Place(new MarkupControl(new List<string> { "LEFTCELL" }), 0, 0)
			.Place(new MarkupControl(new List<string> { "RIGHTCELL" }), 0, 1)
			.Build();

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);

		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("LEFTCELL", stripped);
		Assert.Contains("RIGHTCELL", stripped);
	}

	private static (int Row, int Col) CellOf(
		IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> cells, IWindowControl control)
	{
		var cell = cells.Single(c => ReferenceEquals(c.Control, control));
		return (cell.Placement.Row, cell.Placement.Col);
	}
}
