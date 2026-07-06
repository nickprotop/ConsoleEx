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
/// Coverage for <see cref="GridLayout.GetPaintClipRect"/>. Each cell child must be clipped to its
/// own track rectangle (converted from node-local to absolute coordinates) intersected with the
/// incoming parent clip. When no arrange has run, the parent clip passes through unchanged.
/// </summary>
public class GridClipTests
{
	/// <summary>
	/// Minimal <see cref="IGridSource"/> test double. Cells are correlated to layout-node children
	/// by index, mirroring the contract the real factory honours.
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
	/// Builds the container layout node plus a child node per cell, in OrderedCells order. The root
	/// node has no parent, so after arranging at origin (0,0) its AbsoluteBounds origin is (0,0) and
	/// each cell's absolute rectangle equals its node-local rectangle.
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

	private static MarkupControl Cell(int width)
	{
		return new MarkupControl(new List<string> { new string('X', width) });
	}

	/// <summary>
	/// Two 10-wide columns and one 5-tall row, no gaps/margin/padding. Cells therefore sit at
	/// (0,0,10,5) and (10,0,10,5) in both local and absolute space when arranged at origin (0,0).
	/// </summary>
	private static FakeGridSource TwoColumnSource(out MarkupControl c0, out MarkupControl c1)
	{
		c0 = Cell(4);
		c1 = Cell(4);
		return new FakeGridSource
		{
			ColumnDefinitions = new[] { GridLength.Cells(10), GridLength.Cells(10) },
			RowDefinitions = new[] { GridLength.Cells(5) },
			OrderedCells = new (IWindowControl, GridPlacement)[]
			{
				(c0, new GridPlacement(0, 0)),
				(c1, new GridPlacement(0, 1)),
			},
		};
	}

	[Fact]
	public void GetPaintClipRect_ReturnsCellIntersectParent()
	{
		var source = TwoColumnSource(out _, out _);
		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		layout.MeasureChildren(node, LayoutConstraints.Loose(40, 10));
		node.Arrange(new LayoutRect(0, 0, 40, 10));

		var child0 = node.Children[0];
		var child1 = node.Children[1];

		// Known cell rectangles (local == absolute because arranged at origin (0,0) with no parent).
		var cell0 = new LayoutRect(0, 0, 10, 5);
		var cell1 = new LayoutRect(10, 0, 10, 5);

		// Parent clip fully contains both cells → result equals the cell rectangle itself.
		var fullClip = new LayoutRect(0, 0, 40, 10);
		Assert.Equal(cell0, layout.GetPaintClipRect(child0, fullClip));
		Assert.Equal(cell1, layout.GetPaintClipRect(child1, fullClip));

		// Parent clip trims the cell → result is the cell intersected with the clip.
		var trimClip = new LayoutRect(3, 1, 12, 2);
		Assert.Equal(cell0.Intersect(trimClip), layout.GetPaintClipRect(child0, trimClip));
		Assert.Equal(cell1.Intersect(trimClip), layout.GetPaintClipRect(child1, trimClip));
	}

	[Fact]
	public void GetPaintClipRect_NoArrange_ReturnsParentClip()
	{
		var source = TwoColumnSource(out _, out _);
		var layout = new GridLayout(source);
		var node = BuildNode(layout, source);

		// No arrange has run, so _cellRects is empty and the parent clip must pass through unchanged.
		var parentClip = new LayoutRect(2, 3, 8, 4);
		Assert.Equal(parentClip, layout.GetPaintClipRect(node.Children[0], parentClip));
	}
}
