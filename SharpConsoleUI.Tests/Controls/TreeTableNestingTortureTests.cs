// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;
using Build = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// TreeTableControl inside real containers, at sizes that do not fit.
/// </summary>
/// <remarks>
/// A control that is the only child of a window large enough for it is the one arrangement where
/// the extent it is allotted and the viewport it paints into agree. Layout bugs live in the
/// mismatch, so every test here nests the control at least one container deep and gives it a
/// window smaller than the rows it holds.
/// </remarks>
public class TreeTableNestingTortureTests
{
	private const int Rows = 40;

	private static ConsoleWindowSystem System(int w = 100, int h = 30) =>
		new(new HeadlessConsoleDriver(w, h),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));

	/// <summary>A three-level backlog: 4 epics, 3 features each, 2 tasks each.</summary>
	private static TreeTableControl Backlog()
	{
		var tree = new TreeTableControl { FilteringEnabled = true, ReadOnly = false };
		tree.AddColumn("Item");
		tree.AddColumn("Owner");

		for (int e = 0; e < 4; e++)
		{
			var epic = tree.AddRootRow($"Epic {e}", $"owner{e}");
			for (int f = 0; f < 3; f++)
			{
				var feat = epic.AddChild($"Feature {e}.{f}", $"owner{f}");
				for (int t = 0; t < 2; t++)
					feat.AddChild($"Task {e}.{f}.{t}", $"owner{t}");
			}
		}
		return tree;
	}

	private static void Send(ConsoleWindowSystem system, ConsoleKeyInfo key)
	{
		system.InputStateService.EnqueueKey(key);
		system.Input.ProcessInput();
	}

	private static ConsoleKeyInfo Key(ConsoleKey k) => new('\0', k, false, false, false);
	private static ConsoleKeyInfo Ch(char c) => new(c, ConsoleKey.A, false, false, false);

	#region Inside a Grid cell, too small for the rows

	/// <summary>
	/// A Fill tree table in a star-sized grid cell, in a window far shorter than its rows. The
	/// painted viewport and the allotted extent differ here, which is where scroll-cap bugs live.
	/// </summary>
	[Fact]
	public void InAGridCell_TooShort_StillRendersAndCountsEveryRow()
	{
		var system = System(80, 14);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 10 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var grid = Build.Grid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.Place(tree, 0, 0)
			.Build();

		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.Equal(Rows, tree.RowCount);

		// Re-render and require the count to survive: a display map rebuilt per frame would
		// show up here even though the first render looked right.
		window.RenderAndGetVisibleContent();
		Assert.Equal(Rows, tree.RowCount);
	}

	/// <summary>Collapsing a root inside a grid cell must change the displayed count.</summary>
	[Fact]
	public void InAGridCell_CollapsingARoot_ChangesTheDisplayedCount()
	{
		var system = System(80, 14);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 10 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(tree, 0, 0).Build();
		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		tree.Collapse((TreeTableRow)tree.RootRows[0]);
		window.RenderAndGetVisibleContent();

		// Epic 0 hides 3 features + 6 tasks.
		Assert.Equal(Rows - 9, tree.RowCount);
	}

	#endregion

	#region Three containers deep

	/// <summary>
	/// Grid > ScrollablePanel > HorizontalGrid > TreeTable, in a small window. Each layer gets its
	/// own say over the extent, and the innermost control still has to arrange and count correctly.
	/// </summary>
	[Fact]
	public void ThreeContainersDeep_StillArrangesAndCounts()
	{
		var system = System(90, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 50, Height = 12 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var inner = Build.HorizontalGrid().Column(c => c.Add(tree)).Build();
		var panel = Build.ScrollablePanel().AddControl(inner)
			.WithVerticalAlignment(VerticalAlignment.Fill).Build();
		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(panel, 0, 0).Build();

		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.Equal(Rows, tree.RowCount);

		// Three layers of container each re-measure the child; the count must not drift.
		for (int i = 0; i < 3; i++) window.RenderAndGetVisibleContent();
		Assert.Equal(Rows, tree.RowCount);
	}

	/// <summary>Expansion state must survive several re-renders through all those layers.</summary>
	[Fact]
	public void ThreeContainersDeep_ExpansionSurvivesRerenders()
	{
		var system = System(90, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 50, Height = 12 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var inner = Build.HorizontalGrid().Column(c => c.Add(tree)).Build();
		var panel = Build.ScrollablePanel().AddControl(inner)
			.WithVerticalAlignment(VerticalAlignment.Fill).Build();
		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(panel, 0, 0).Build();
		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		tree.CollapseAll();
		int collapsed = tree.RowCount;
		for (int i = 0; i < 4; i++) window.RenderAndGetVisibleContent();

		Assert.Equal(4, collapsed);          // only the roots
		Assert.Equal(collapsed, tree.RowCount);
	}

	#endregion

	#region Inside a TabControl, including a hidden tab

	/// <summary>
	/// A tree table on a tab that is NOT selected is never painted. Its state must survive anyway,
	/// and must be right the moment the tab is shown.
	/// </summary>
	[Fact]
	public void OnAHiddenTab_StateSurvivesUntilTheTabIsShown()
	{
		var system = System(90, 24);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var tabs = Build.TabControl()
			.AddTab("First", Build.Markup("nothing here").Build())
			.AddTab("Tree", tree)
			.Fill()
			.Build();

		window.AddControl(tabs);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		tree.CollapseAll();
		window.RenderAndGetVisibleContent();

		Assert.Equal(4, tree.RowCount);
	}

	#endregion

	#region Filtering through the real key path, nested

	/// <summary>
	/// The filter typed as a user types it, with the control nested and the window too small.
	/// A match keeps the rows above it, so filtering a leaf must still show its ancestors.
	/// </summary>
	[Fact]
	public void FilteringNested_ThroughTheRealKeyPath_KeepsAncestors()
	{
		var system = System(90, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 55, Height = 11 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(tree, 0, 0).Build();
		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(tree);
		window.RenderAndGetVisibleContent();

		Send(system, Ch('/'));
		foreach (char c in "Task 1.2.1") Send(system, Ch(c));
		Send(system, Key(ConsoleKey.Enter));
		window.RenderAndGetVisibleContent();

		// The task, its feature and its epic: three rows.
		Assert.Equal(3, tree.RowCount);
	}

	/// <summary>And the filtered view must survive a re-render.</summary>
	[Fact]
	public void FilteringNested_SurvivesARerender()
	{
		var system = System(90, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 55, Height = 11 };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(tree, 0, 0).Build();
		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(tree);
		window.RenderAndGetVisibleContent();

		Send(system, Ch('/'));
		foreach (char c in "Task 1.2.1") Send(system, Ch(c));
		Send(system, Key(ConsoleKey.Enter));

		int filtered = tree.RowCount;
		for (int i = 0; i < 3; i++) window.RenderAndGetVisibleContent();

		Assert.Equal(3, filtered);
		Assert.Equal(filtered, tree.RowCount);
	}

	#endregion

	#region Degenerate sizes

	/// <summary>A window one row tall must not throw, however little fits.</summary>
	[Theory]
	[InlineData(20, 3)]
	[InlineData(10, 2)]
	[InlineData(6, 1)]
	public void AbsurdlySmallWindows_DoNotThrow(int width, int height)
	{
		var system = System(80, 24);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		var tree = Backlog();
		tree.VerticalAlignment = VerticalAlignment.Fill;

		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(tree, 0, 0).Build();
		window.AddControl(grid);
		system.AddWindow(window);

		var ex = Record.Exception(() =>
		{
			window.RenderAndGetVisibleContent();
			tree.CollapseAll();
			window.RenderAndGetVisibleContent();
			tree.ExpandAll();
			window.RenderAndGetVisibleContent();
		});

		Assert.Null(ex);
	}

	#endregion
}

/// <summary>
/// Mutating a nested tree table while it is sorted, filtered and collapsed. These combinations are
/// where the display map and the row data can fall out of step, so they are driven through real
/// containers rather than against the control alone.
/// </summary>
public class TreeTableMutationTortureTests
{
	private static ConsoleWindowSystem System(int w = 100, int h = 30) =>
		new(new HeadlessConsoleDriver(w, h),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));

	private static (TreeTableControl tree, Window window, ConsoleWindowSystem system) Nested(int height = 10)
	{
		var system = System(90, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 55, Height = height };
		var tree = new TreeTableControl { FilteringEnabled = true, SortingEnabled = true, ReadOnly = false };
		tree.AddColumn("Item");
		tree.AddColumn("Owner");
		tree.VerticalAlignment = VerticalAlignment.Fill;

		for (int e = 0; e < 3; e++)
		{
			var epic = tree.AddRootRow($"Epic {e}", $"o{e}");
			for (int f = 0; f < 2; f++)
				epic.AddChild($"Feature {e}.{f}", $"o{f}");
		}

		var grid = Build.Grid().Columns(GridLength.Star(1)).Rows(GridLength.Star(1))
			.Place(tree, 0, 0).Build();
		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		return (tree, window, system);
	}

	/// <summary>Adding a child while sorted must keep the sort, not silently drop it.</summary>
	[Fact]
	public void AddingARowWhileSorted_KeepsTheSort()
	{
		var (tree, window, _) = Nested();
		tree.SortByColumn(0);
		window.RenderAndGetVisibleContent();

		((TreeTableRow)tree.RootRows[0]).AddChild("Feature 0.9", "zz");
		window.RenderAndGetVisibleContent();

		Assert.Equal(SortDirection.Ascending, tree.CurrentSortDirection);
		Assert.Equal(10, tree.RowCount);
	}

	/// <summary>Removing a subtree while filtered must not leave a stale count or throw.</summary>
	[Fact]
	public void RemovingASubtreeWhileFiltered_IsConsistent()
	{
		var (tree, window, _) = Nested();
		tree.EnterFilterMode();
		tree.ApplyFilter("Feature");
		window.RenderAndGetVisibleContent();
		int before = tree.RowCount;

		tree.RemoveRow(tree.RootRows[0]);
		window.RenderAndGetVisibleContent();

		Assert.True(tree.RowCount < before, $"count did not shrink: {before} -> {tree.RowCount}");
		Assert.Null(Record.Exception(() => window.RenderAndGetVisibleContent()));
	}

	/// <summary>Sort, filter, collapse, mutate, clear — all of it, then the count must be sane.</summary>
	[Fact]
	public void EverythingAtOnce_LeavesAConsistentTable()
	{
		var (tree, window, _) = Nested();

		tree.SortByColumn(0);
		window.RenderAndGetVisibleContent();
		tree.EnterFilterMode();
		tree.ApplyFilter("Epic");
		window.RenderAndGetVisibleContent();
		tree.CollapseAll();
		window.RenderAndGetVisibleContent();
		((TreeTableRow)tree.RootRows[1]).AddChild("Feature 1.9", "zz");
		window.RenderAndGetVisibleContent();
		tree.ClearFilter();
		window.RenderAndGetVisibleContent();
		tree.ClearSort();
		window.RenderAndGetVisibleContent();
		tree.ExpandAll();
		window.RenderAndGetVisibleContent();

		// 3 epics + 6 original features + 1 added = 10
		Assert.Equal(10, tree.RowCount);
	}

	/// <summary>A collapsed parent's children must still be found by a filter.</summary>
	[Fact]
	public void FilteringFindsChildrenOfACollapsedParent()
	{
		var (tree, window, _) = Nested();
		tree.CollapseAll();
		window.RenderAndGetVisibleContent();
		Assert.Equal(3, tree.RowCount);

		tree.EnterFilterMode();
		tree.ApplyFilter("Feature 2.1");
		window.RenderAndGetVisibleContent();

		// Found inside a collapsed epic, and shown with its parent.
		Assert.Equal(2, tree.RowCount);
	}

	/// <summary>Clearing that filter restores the collapsed state, not an expanded tree.</summary>
	[Fact]
	public void ClearingAFilterRestoresTheCollapsedState()
	{
		var (tree, window, _) = Nested();
		tree.CollapseAll();
		window.RenderAndGetVisibleContent();

		tree.EnterFilterMode();
		tree.ApplyFilter("Feature 2.1");
		window.RenderAndGetVisibleContent();
		tree.ClearFilter();
		window.RenderAndGetVisibleContent();

		Assert.Equal(3, tree.RowCount);
	}
}
