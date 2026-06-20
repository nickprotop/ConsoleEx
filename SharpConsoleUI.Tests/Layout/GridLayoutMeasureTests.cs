// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

public class GridLayoutMeasureTests
{
	/// <summary>
	/// Minimal <see cref="IGridSource"/> test double. Cells are correlated to layout-node children
	/// by index, mirroring the contract the real factory will honour.
	/// </summary>
	private sealed class FakeGridSource : IGridSource
	{
		public IReadOnlyList<GridLength> RowDefinitions { get; init; } = Array.Empty<GridLength>();
		public IReadOnlyList<GridLength> ColumnDefinitions { get; init; } = Array.Empty<GridLength>();
		public int RowGap { get; init; }
		public int ColumnGap { get; init; }
		public Margin Margin { get; init; }
		public Padding Padding { get; init; }
		public IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> OrderedCells { get; init; }
			= Array.Empty<(IWindowControl, GridPlacement)>();
	}

	/// <summary>
	/// Builds the container layout node plus a child node per cell, in OrderedCells order.
	/// </summary>
	private static LayoutNode BuildNode(GridLayout layout, FakeGridSource source)
	{
		var node = new LayoutNode((IWindowControl?)null, layout);
		foreach (var (control, _) in source.OrderedCells)
		{
			node.AddChild(new LayoutNode(control));
		}
		return node;
	}

	private static MarkupControl Cell(params string[] lines) => new(new List<string>(lines));

	[Fact]
	public void AllFixed_DesiredSizeSumsTracksPlusGapsPaddingMargin()
	{
		var c0 = Cell("a");
		var c1 = Cell("b");

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10), GridLength.Cells(20) },
			RowDefinitions = new[] { GridLength.Cells(3) },
			ColumnGap = 2,
			Padding = new Padding(1, 1, 1, 1),
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
				(c1, new GridPlacement(0, 1)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		var desired = layout.MeasureChildren(node, LayoutConstraints.Unbounded);

		// width = 10 + 20 + 2(gap) + 1+1(padding LR); height = 3 + 1+1(padding TB)
		Assert.Equal(34, desired.Width);
		Assert.Equal(5, desired.Height);
	}

	[Fact]
	public void AutoRow_SizesToTallestCell()
	{
		var shortCell = Cell("one");
		var tallCell = Cell("line1", "line2", "line3");

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10), GridLength.Cells(10) },
			RowDefinitions = new[] { GridLength.Auto() },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(shortCell, new GridPlacement(0, 0)),
				(tallCell, new GridPlacement(0, 1)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		var desired = layout.MeasureChildren(node, LayoutConstraints.Unbounded);

		// Auto row sizes to the taller cell (3 lines). No gaps, no padding, no margin.
		Assert.Equal(3, desired.Height);
		// Two fixed Cells(10) columns, no gap → width 20.
		Assert.Equal(20, desired.Width);
	}

	[Fact]
	public void ColSpan_DistributesAcrossSpannedAutoColumns()
	{
		// Single cell spanning two Auto columns, content measures width 14.
		var wide = Cell(new string('X', 14));

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Auto(), GridLength.Auto() },
			RowDefinitions = new[] { GridLength.Cells(1) },
			ColumnGap = 0,
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(wide, new GridPlacement(0, 0, 1, 2)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		var desired = layout.MeasureChildren(node, LayoutConstraints.Unbounded);

		// 14 split across 2 Auto columns → 7 each, total 14 (no gap, no inset).
		Assert.Equal(14, desired.Width);
	}

	[Fact]
	public void Span_OverFixedPlusAuto_SubtractsFixed()
	{
		// Cell spanning [Cells(10), Auto()] with content width 30. The Fixed column already
		// provides 10, so the Auto column should absorb the remaining 20.
		var wide = Cell(new string('X', 30));

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10), GridLength.Auto() },
			RowDefinitions = new[] { GridLength.Cells(1) },
			ColumnGap = 0,
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(wide, new GridPlacement(0, 0, 1, 2)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		var desired = layout.MeasureChildren(node, LayoutConstraints.Unbounded);

		// Fixed 10 + Auto 20 = 30 total.
		Assert.Equal(30, desired.Width);
	}

	[Fact]
	public void UnboundedHeight_StarRowsCollapseToZero()
	{
		// A grid measured for vertical stacking sees an effectively-unbounded height. Star rows have
		// no finite extent to divide, so they collapse to 0 (WinUI contract) rather than blowing up to
		// ~int.MaxValue and pushing later cells off-screen. The Auto row keeps its content height.
		var autoCell = Cell("line1", "line2");
		var starCell = Cell("x");

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10) },
			RowDefinitions = new[] { GridLength.Auto(), GridLength.Star(1) },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(autoCell, new GridPlacement(0, 0)),
				(starCell, new GridPlacement(1, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		var desired = layout.MeasureChildren(node, LayoutConstraints.Unbounded);

		// Desired height is the Auto row's content (2 lines) only; the Star row contributed 0 — no
		// int.MaxValue blowup.
		Assert.Equal(2, desired.Height);
	}

	[Fact]
	public void EmptyGrid_ReturnsZero()
	{
		var source = new FakeGridSource();
		var layout = new GridLayout(source);
		var node = new LayoutNode((IWindowControl?)null, layout);

		var desired = layout.MeasureChildren(node, LayoutConstraints.Unbounded);

		Assert.Equal(LayoutSize.Zero, desired);
	}
}
