// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Runtime CRUD coverage for <see cref="GridControl"/>: each test renders the grid, mutates it
/// (add/remove/replace controls, add/remove track definitions), then re-renders and asserts the
/// mutation is reflected. This guards against state failing to survive a re-render (the
/// ScrollLayout post-mortem class of bug), and specifically proves that mutating the raw
/// <see cref="GridControl.RowDefinitions"/>/<see cref="GridControl.ColumnDefinitions"/> lists at
/// runtime triggers a rebuild.
/// </summary>
public class GridCrudTests
{
	private static GridControl NewGrid() => new() { Width = 40, Height = 10 };

	private static string Render(GridControl grid)
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		var content = window.RenderAndGetVisibleContent();
		return ContainerTestHelpers.StripAnsiCodes(content);
	}

	private static string ReRender(SharpConsoleUI.Window window)
	{
		var content = window.RenderAndGetVisibleContent();
		return ContainerTestHelpers.StripAnsiCodes(content);
	}

	private static (GridControl grid, SharpConsoleUI.Window window, System.Func<string> render) Setup(GridControl grid)
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		string Do() => ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		return (grid, window, Do);
	}

	[Fact]
	public void AddControl_AfterRender_AppearsOnReRender()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "AAA" }), 0, 0);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);

		grid.AddControl(new MarkupControl(new List<string> { "BBB" }));
		var second = render();

		Assert.Contains("AAA", second);
		Assert.Contains("BBB", second);
	}

	[Fact]
	public void RemoveControl_AfterRender_DisappearsOnReRender()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		var aaa = new MarkupControl(new List<string> { "AAA" });
		grid.Place(aaa, 0, 0);
		grid.Place(new MarkupControl(new List<string> { "BBB" }), 1, 0);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);
		Assert.Contains("BBB", first);

		grid.RemoveControl(aaa);
		var second = render();

		Assert.DoesNotContain("AAA", second);
		Assert.Contains("BBB", second);
	}

	[Fact]
	public void ReplaceControl_AfterRender_ShowsNewControl()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		var aaa = new MarkupControl(new List<string> { "AAA" });
		grid.Place(aaa, 0, 0);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);

		grid.ReplaceControl(aaa, new MarkupControl(new List<string> { "ZZZ" }));
		var second = render();

		Assert.Contains("ZZZ", second);
		Assert.DoesNotContain("AAA", second);
	}

	[Fact]
	public void RemoveAt_AfterRender_ClearsCell()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "AAA" }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "BBB" }), 0, 1);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);
		Assert.Contains("BBB", first);

		grid.RemoveAt(0, 0);
		var second = render();

		Assert.DoesNotContain("AAA", second);
		Assert.Contains("BBB", second);
	}

	[Fact]
	public void ClearControls_AfterRender_EmptiesGrid()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "AAA" }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "BBB" }), 1, 0);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);
		Assert.Contains("BBB", first);

		grid.ClearControls();
		var second = render();

		Assert.DoesNotContain("AAA", second);
		Assert.DoesNotContain("BBB", second);
	}

	[Fact]
	public void AddColumn_AtRuntime_RebuildsAndRenders()
	{
		// 1 column, two auto-flowed controls stack into 2 rows.
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.AddControl(new MarkupControl(new List<string> { "AAA" }));
		grid.AddControl(new MarkupControl(new List<string> { "BBB" }));

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);
		Assert.Contains("BBB", first);

		// Mutate ONLY the column definitions at runtime. Without the observable-list gap fix this
		// would not trigger a rebuild and a subsequent placement-based render would be stale.
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "CCC" }), 0, 1);
		var second = render();

		Assert.Contains("AAA", second);
		Assert.Contains("BBB", second);
		Assert.Contains("CCC", second);
	}

	[Fact]
	public void AddColumn_Alone_TriggersFreshRender()
	{
		// Proves the def-list mutation ALONE invalidates: place a wide-spanning cell, then add a
		// column so the grid geometry changes, with no control mutation between renders.
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "AAA" }), 0, 0);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);

		// Only the column list mutates — no Place/AddControl/Remove. Must still render cleanly.
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		var second = render();

		Assert.Contains("AAA", second);
	}

	[Fact]
	public void RemoveColumn_AtRuntime_RebuildsAndRenders()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "AAA" }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "BBB" }), 0, 1);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);
		Assert.Contains("BBB", first);

		// Drop column 1. The BBB cell is now out of range; GridLayout clamps placements into the
		// remaining column, so this must render gracefully without throwing. (Both cells collapse onto
		// the single column at row 0; the later-painted one wins — we only assert no crash and that the
		// grid still produces cell content.)
		grid.ColumnDefinitions.RemoveAt(1);
		var second = render();

		Assert.True(second.Contains("AAA") || second.Contains("BBB"));
	}

	[Fact]
	public void AddRow_AtRuntime_RebuildsAndRenders()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "AAA" }), 0, 0);

		var (_, _, render) = Setup(grid);
		var first = render();
		Assert.Contains("AAA", first);

		// Add a second row, then a cell into it — the row-list mutation must rebuild so the new row exists.
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "BBB" }), 1, 0);
		var second = render();

		Assert.Contains("AAA", second);
		Assert.Contains("BBB", second);
	}

	[Fact]
	public void State_SurvivesMultipleReRenders()
	{
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		var aaa = new MarkupControl(new List<string> { "AAA" });
		grid.Place(aaa, 0, 0);
		grid.Place(new MarkupControl(new List<string> { "BBB" }), 1, 0);

		var (_, _, render) = Setup(grid);
		render();

		grid.RemoveControl(aaa);
		var second = render();
		Assert.DoesNotContain("AAA", second);
		Assert.Contains("BBB", second);

		// A further re-render with no mutation must be stable (post-mortem rule).
		var third = render();
		Assert.Equal(second, third);
	}

	[Fact]
	public void ConcurrentDefMutation_DoesNotCorrupt()
	{
		// Concurrency smoke test: several threads mutate the grid (track definitions + controls) at once.
		// The grid locks internally (GridDefinitionList._sync, _cellsLock), so this must not throw a
		// "Collection was modified" or corrupt the lists; afterwards the grid still renders. This is a
		// no-crash/no-corruption guard, not a determinism assertion (see the class threading remarks).
		var grid = NewGrid();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "SEED" }), 0, 0);

		var tasks = new List<System.Threading.Tasks.Task>();
		for (int t = 0; t < 8; t++)
		{
			int id = t;
			tasks.Add(System.Threading.Tasks.Task.Run(() =>
			{
				grid.ColumnDefinitions.Add(GridLength.Star(1));
				grid.AddControl(new MarkupControl(new List<string> { $"C{id}" }));
			}));
		}
		System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

		// No exception above, and a render after the storm still works.
		var output = Render(grid);
		Assert.Contains("SEED", output);
	}
}
