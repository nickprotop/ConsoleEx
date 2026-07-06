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

/// <summary>
/// Parity suite for <see cref="GridLayout.ArrangeChildren"/>. These tests assert that grid
/// margin/padding insetting, per-cell child-margin insetting, and horizontal/vertical alignment
/// behave identically to the established <see cref="HorizontalLayout"/> arrange contract.
/// </summary>
public class GridArrangeTests
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
		public bool StarTracksSelfSizeToContentInMeasure { get; init; }
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

	/// <summary>
	/// Creates a single-line markup cell of the given display width, with explicit alignment so the
	/// arrange behaviour is deterministic (BaseControl defaults to Left/Top).
	/// </summary>
	private static MarkupControl Cell(
		int width,
		HorizontalAlignment hAlign = HorizontalAlignment.Stretch,
		VerticalAlignment vAlign = VerticalAlignment.Fill,
		Margin margin = default)
	{
		return new MarkupControl(new List<string> { new string('X', width) })
		{
			HorizontalAlignment = hAlign,
			VerticalAlignment = vAlign,
			Margin = margin,
		};
	}

	private static LayoutSize MeasureArrange(GridLayout layout, LayoutNode node, int width, int height)
	{
		var desired = layout.MeasureChildren(node, LayoutConstraints.Loose(width, height));
		node.Arrange(new LayoutRect(0, 0, width, height));
		return desired;
	}

	[Fact]
	public void GridMargin_InsetsTrackArea()
	{
		var c0 = Cell(4);
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Star() },
			RowDefinitions = new[] { GridLength.Star() },
			Margin = new Margin(2, 1, 2, 1),
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);

		var child = node.Children[0];
		Assert.Equal(2, child.Bounds.X);
		Assert.Equal(1, child.Bounds.Y);
		Assert.Equal(40 - 4, child.Bounds.Width);
		Assert.Equal(10 - 2, child.Bounds.Height);
	}

	[Fact]
	public void GridPadding_InsetsInsideMargin()
	{
		var c0 = Cell(4);
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Star() },
			RowDefinitions = new[] { GridLength.Star() },
			Padding = new Padding(1, 1, 1, 1),
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);

		var child = node.Children[0];
		Assert.Equal(1, child.Bounds.X);
		Assert.Equal(1, child.Bounds.Y);
		Assert.Equal(40 - 2, child.Bounds.Width);
		Assert.Equal(10 - 2, child.Bounds.Height);
	}

	[Fact]
	public void CellChildMargin_InsetsWithinCell()
	{
		var c0 = Cell(4, margin: new Margin(1, 1, 1, 1));
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Star() },
			RowDefinitions = new[] { GridLength.Star() },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);

		var child = node.Children[0];
		// The grid arranges the child into the FULL cell content box and does NOT pre-inset by the child's
		// own margin — the control applies its margin itself during paint (its leftInset/topInset). This
		// matches VerticalStackLayout/WindowContentLayout (the framework convention) and avoids the
		// double-subtraction that truncated tight Auto-cell labels. So the child's bounds span the whole
		// cell; the margin shows up as the control's own internal inset, not a smaller arranged rect.
		Assert.Equal(0, child.Bounds.X);
		Assert.Equal(0, child.Bounds.Y);
		Assert.Equal(40, child.Bounds.Width);
		Assert.Equal(10, child.Bounds.Height);
	}

	[Fact]
	public void HAlign_Left_PinsToInnerStart()
	{
		var child = ArrangeSingleCellHAlign(HorizontalAlignment.Left);
		Assert.Equal(0, child.Bounds.X);
		Assert.Equal(4, child.Bounds.Width);
	}

	[Fact]
	public void HAlign_Center_Floats()
	{
		var child = ArrangeSingleCellHAlign(HorizontalAlignment.Center);
		// cell width 20, child width 4 → x = (20 - 4) / 2 = 8.
		Assert.Equal(8, child.Bounds.X);
		Assert.Equal(4, child.Bounds.Width);
	}

	[Fact]
	public void HAlign_Right_PinsToInnerEnd()
	{
		var child = ArrangeSingleCellHAlign(HorizontalAlignment.Right);
		// x = 20 - 4 = 16.
		Assert.Equal(16, child.Bounds.X);
		Assert.Equal(4, child.Bounds.Width);
	}

	[Fact]
	public void HAlign_Stretch_FillsCell()
	{
		var child = ArrangeSingleCellHAlign(HorizontalAlignment.Stretch);
		Assert.Equal(0, child.Bounds.X);
		Assert.Equal(20, child.Bounds.Width);
	}

	private LayoutNode ArrangeSingleCellHAlign(HorizontalAlignment hAlign)
	{
		var c0 = Cell(4, hAlign: hAlign, vAlign: VerticalAlignment.Top);
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(20) },
			RowDefinitions = new[] { GridLength.Cells(3) },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);
		return node.Children[0];
	}

	[Fact]
	public void VAlign_Top_PinsToInnerStart()
	{
		var child = ArrangeSingleCellVAlign(VerticalAlignment.Top);
		Assert.Equal(0, child.Bounds.Y);
		Assert.Equal(1, child.Bounds.Height);
	}

	[Fact]
	public void VAlign_Center_Floats()
	{
		var child = ArrangeSingleCellVAlign(VerticalAlignment.Center);
		// cell height 10, child height 1 → y = (10 - 1) / 2 = 4.
		Assert.Equal(4, child.Bounds.Y);
		Assert.Equal(1, child.Bounds.Height);
	}

	[Fact]
	public void VAlign_Bottom_PinsToInnerEnd()
	{
		var child = ArrangeSingleCellVAlign(VerticalAlignment.Bottom);
		// y = 10 - 1 = 9.
		Assert.Equal(9, child.Bounds.Y);
		Assert.Equal(1, child.Bounds.Height);
	}

	[Fact]
	public void VAlign_Fill_FillsCell()
	{
		var child = ArrangeSingleCellVAlign(VerticalAlignment.Fill);
		Assert.Equal(0, child.Bounds.Y);
		Assert.Equal(10, child.Bounds.Height);
	}

	private LayoutNode ArrangeSingleCellVAlign(VerticalAlignment vAlign)
	{
		// Single-line cell measures height 1; cell is the full 10-row grid.
		var c0 = Cell(4, hAlign: HorizontalAlignment.Left, vAlign: vAlign);
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(20) },
			RowDefinitions = new[] { GridLength.Cells(10) },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);
		return node.Children[0];
	}

	[Fact]
	public void ColSpan_CoversTwoColumnsPlusGap()
	{
		var c0 = Cell(4);
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Star(), GridLength.Star() },
			RowDefinitions = new[] { GridLength.Star() },
			ColumnGap = 2,
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0, 1, 2)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		// width 22: two equal star cols of 10 each, plus gap of 2 → cols (10, 10), offsets (0, 12).
		MeasureArrange(layout, node, 22, 5);

		var child = node.Children[0];
		// Spanning cell covers col0 (10) + gap (2) + col1 (10) = 22.
		Assert.Equal(0, child.Bounds.X);
		Assert.Equal(22, child.Bounds.Width);
	}

	[Fact]
	public void HiddenChild_NotArranged()
	{
		var visible = Cell(4);
		var hidden = Cell(4);
		hidden.Visible = false;

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10), GridLength.Cells(10) },
			RowDefinitions = new[] { GridLength.Star() },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(visible, new GridPlacement(0, 0)),
				(hidden, new GridPlacement(0, 1)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);

		var visibleNode = node.Children[0];
		var hiddenNode = node.Children[1];

		// Visible sibling keeps its cell at column 0.
		Assert.Equal(0, visibleNode.Bounds.X);
		Assert.Equal(10, visibleNode.Bounds.Width);

		// Hidden child is not arranged.
		Assert.False(hiddenNode.IsVisible);
		Assert.True(hiddenNode.Bounds.IsEmpty);
	}

	[Fact]
	public void ExplicitSize_PlusCenter_Floats()
	{
		var c0 = new MarkupControl(new List<string> { new string('X', 20) })
		{
			Width = 4,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
		};

		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(20) },
			RowDefinitions = new[] { GridLength.Cells(3) },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);

		var child = node.Children[0];
		// Explicit width 4 honoured even though content is 20 wide; centered → x = (20 - 4) / 2 = 8.
		Assert.Equal(4, child.Bounds.Width);
		Assert.Equal(8, child.Bounds.X);
	}

	[Fact]
	public void OversizedChild_Center_DoesNotGoNegative()
	{
		// Child measures 30 wide but its column is only 10 → centering must not push the start before
		// the cell's inner edge (no negative X / painting outside the cell).
		var c0 = Cell(30, hAlign: HorizontalAlignment.Center, vAlign: VerticalAlignment.Top);
		var source = new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10) },
			RowDefinitions = new[] { GridLength.Cells(3) },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
			},
		};

		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);
		MeasureArrange(layout, node, 40, 10);

		var child = node.Children[0];
		// Cell inner start is 0 (no grid margin/padding, no child margin); X must not go negative.
		Assert.True(child.Bounds.X >= 0, $"expected X >= 0 but was {child.Bounds.X}");
	}
}
