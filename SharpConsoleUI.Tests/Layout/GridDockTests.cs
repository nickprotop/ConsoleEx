// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// <see cref="GridControl"/> gaining dock, so one container does both placements.
/// </summary>
/// <remarks>
/// <para>
/// Docks are allocated OUTSIDE the tracks: a docked child claims a strip off the grid's
/// rectangle and the track algorithm runs on what is left. That is what keeps cell coordinates
/// stable - the alternative, synthesising tracks from docks, would make <c>grid[1, 0]</c> mean
/// a different cell depending on how many children are docked.
/// </para>
/// <para>
/// This replaced a separate dock-panel control, which was deleted: a grid reproduced the same
/// shape and rendered it better (real column gaps, no indent leaking into the bottom band), so
/// a second container earned nothing.
/// </para>
/// </remarks>
[Collection("RootLayout")]
public class GridDockTests
{
	private readonly ITestOutputHelper _out;

	public GridDockTests(ITestOutputHelper output) => _out = output;

	private static string[] Render(Window window)
	{
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		return GoldenBuffer.Capture(window, "grid").ToText().Split('\n');
	}

	private static (ConsoleWindowSystem System, Window Window) Shell(int w = 40, int h = 9)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Grid", Width = w, Height = h, Left = 0, Top = 0 };
		return (system, window);
	}

	private static GridControl FillGrid()
	{
		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		return grid;
	}

	[Fact]
	public void TheIdeShape_TopBarSidebarStatusAndBody()
	{
		// The shape a dock panel would have existed for, in the grid itself.
		var (system, window) = Shell();

		var grid = FillGrid();
		grid.DockChild(Builder.Markup("TOOLBAR").Build(), Dock.Top);
		grid.DockChild(Builder.Markup("TREE").Build(), Dock.Left);
		grid.DockChild(Builder.Markup("STATUS").Build(), Dock.Bottom);
		grid.Place(Builder.Markup("editor").Build(), 0, 0);

		window.AddControl(grid);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.StartsWith("TOOLBAR", lines[0]);
		Assert.StartsWith("TREE", lines[1]);
		Assert.Contains("editor", lines[1]);

		int status = Array.FindIndex(lines, l => l.Contains("STATUS"));
		Assert.True(status > 1, "the bottom dock must be pinned low, not stacked under the body");

		// The top bar was declared first, so it spans the full width and the sidebar starts
		// below it. Declaration order IS the allocation order.
		Assert.False(lines[0].Contains("TREE"), "the top dock claimed the full width first");
	}

	[Fact]
	public void DockingDoesNotShiftCellCoordinates()
	{
		// The invariant that rules out deriving tracks from docks: grid[r, c] must mean the same
		// cell however many children are docked.
		var (system, window) = Shell(w: 40, h: 10);

		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		grid.Place(Builder.Markup("r0c0").Build(), 0, 0);
		grid.Place(Builder.Markup("r0c1").Build(), 0, 1);
		grid.Place(Builder.Markup("r1c0").Build(), 1, 0);
		grid.Place(Builder.Markup("r1c1").Build(), 1, 1);

		window.AddControl(grid);
		system.AddWindow(window);

		var before = Render(window);
		_out.WriteLine(string.Join("\n", before));

		// Dock something on every edge; the four cells must still be in reading order.
		grid.DockChild(Builder.Markup("T").Build(), Dock.Top);
		grid.DockChild(Builder.Markup("L").Build(), Dock.Left);
		grid.DockChild(Builder.Markup("B").Build(), Dock.Bottom);

		var after = Render(window);
		_out.WriteLine("--- after docking ---");
		_out.WriteLine(string.Join("\n", after));

		var text = string.Join("\n", after);
		foreach (var cell in new[] { "r0c0", "r0c1", "r1c0", "r1c1" })
			Assert.Contains(cell, text);

		// Still laid out as a 2x2: the two top cells share a row, above the two bottom ones.
		int r0 = Array.FindIndex(after, l => l.Contains("r0c0"));
		int r1 = Array.FindIndex(after, l => l.Contains("r1c0"));
		Assert.Equal(r0, Array.FindIndex(after, l => l.Contains("r0c1")));
		Assert.Equal(r1, Array.FindIndex(after, l => l.Contains("r1c1")));
		Assert.True(r1 > r0, "row 1 must still sit below row 0");
	}

	[Fact]
	public void TwoBandsOnTheSameEdgeKeepAuthorOrder()
	{
		// The far-edge reversal rule, shared with the window root: a rule declared before a help
		// line still paints above it.
		var (system, window) = Shell(w: 30, h: 8);

		var grid = FillGrid();
		grid.Place(Builder.Markup("body").Build(), 0, 0);
		grid.DockChild(Builder.Markup("RULE").Build(), Dock.Bottom);
		grid.DockChild(Builder.Markup("HELP").Build(), Dock.Bottom);

		window.AddControl(grid);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		int rule = Array.FindIndex(lines, l => l.Contains("RULE"));
		int help = Array.FindIndex(lines, l => l.Contains("HELP"));

		Assert.True(rule >= 0 && help >= 0, "both bottom docks must paint");
		Assert.True(rule < help, $"author order must hold: RULE ({rule}) above HELP ({help})");
	}

	[Fact]
	public void AGridWithOnlyDocksStillLaysThemOut()
	{
		// No tracks at all: the dock pass must not be skipped by the empty-grid short circuit.
		var (system, window) = Shell(w: 30, h: 6);

		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.DockChild(Builder.Markup("TOP").Build(), Dock.Top);
		grid.DockChild(Builder.Markup("BOTTOM").Build(), Dock.Bottom);

		window.AddControl(grid);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.Contains("TOP", lines[0]);
		int bottom = Array.FindIndex(lines, l => l.Contains("BOTTOM"));
		Assert.True(bottom > 0, "the bottom dock must be placed even with no tracks");
	}

	[Fact]
	public void UndockReturnsTheSpaceToTheTracks()
	{
		var (system, window) = Shell(w: 30, h: 8);

		var grid = FillGrid();
		grid.Place(Builder.Markup("body").Build(), 0, 0);
		var side = Builder.Markup("SIDE").Build();
		grid.DockChild(side, Dock.Left);

		window.AddControl(grid);
		system.AddWindow(window);

		var docked = Render(window);
		Assert.StartsWith("SIDE", docked[0]);

		Assert.True(grid.Undock(side));

		var undocked = Render(window);
		_out.WriteLine(string.Join("\n", undocked));

		Assert.DoesNotContain("SIDE", string.Join("\n", undocked));
		Assert.StartsWith("body", undocked[0]);
	}

	[Fact]
	public void DockRejectsNoneAndNull()
	{
		var grid = FillGrid();

		Assert.Throws<ArgumentNullException>(() => grid.DockChild(null!, Dock.Top));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => grid.DockChild(Builder.Markup("x").Build(), Dock.None));
	}

	[Fact]
	public void ANestedGridCanStillBeDockedItself()
	{
		// The method is DockChild, not Dock, because every control already HAS a Dock property -
		// the edge it claims in ITS parent. A method of the same name shadows that property and
		// makes `grid.Dock = Dock.Left` a compile error, so a nested grid could not be docked at
		// all. This test is the guard: it exercises both members on the same instance.
		var (system, window) = Shell(w: 40, h: 10);

		var outer = FillGrid();

		var inner = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		inner.ColumnDefinitions.Add(GridLength.Star(1));
		inner.RowDefinitions.Add(GridLength.Star(1));
		inner.Place(Builder.Markup("inner cell").Build(), 0, 0);

		// The property: the inner grid claims the outer grid's left edge.
		inner.Dock = Dock.Left;
		inner.Width = 14;
		outer.DockChild(inner, Dock.Left);

		outer.Place(Builder.Markup("outer body").Build(), 0, 0);

		window.AddControl(outer);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.Equal(Dock.Left, inner.Dock);
		Assert.Contains("inner cell", lines[0]);
		Assert.Contains("outer body", lines[0]);
	}

	[Fact]
	public void AGridWithNoDocksIsUnchanged()
	{
		// The regression guard: every existing grid docks nothing and must lay out exactly as before.
		var (system, window) = Shell(w: 30, h: 8);

		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.ColumnDefinitions.Add(GridLength.Auto());
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(Builder.Markup("left").Build(), 0, 0);
		grid.Place(Builder.Markup("right").Build(), 0, 1);

		window.AddControl(grid);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.Contains("left", lines[0]);
		Assert.Contains("right", lines[0]);
		Assert.Empty(grid.DockedChildren);
	}
}
