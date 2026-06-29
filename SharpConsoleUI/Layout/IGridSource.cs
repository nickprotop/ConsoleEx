// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Layout;

/// <summary>
/// The seam between a grid container and the <see cref="GridLayout"/> algorithm that measures and
/// arranges it. A grid control implements this interface to expose exactly what the layout needs:
/// the row and column track definitions, the inter-track gaps, the grid's own margin and padding,
/// and the ordered set of cells with their placements.
/// </summary>
/// <remarks>
/// The order of <see cref="OrderedCells"/> is significant: it must match the order in which the
/// layout-node factory turns cells into child <see cref="LayoutNode"/>s. The layout correlates each
/// child node to its placement <em>by index</em>, so <c>node.Children[i]</c> corresponds to
/// <c>OrderedCells[i]</c>.
/// </remarks>
public interface IGridSource
{
	/// <summary>Gets the row track definitions, top to bottom.</summary>
	IReadOnlyList<GridLength> RowDefinitions { get; }

	/// <summary>Gets the column track definitions, left to right.</summary>
	IReadOnlyList<GridLength> ColumnDefinitions { get; }

	/// <summary>Gets the gap, in cells, between adjacent rows.</summary>
	int RowGap { get; }

	/// <summary>Gets the gap, in cells, between adjacent columns.</summary>
	int ColumnGap { get; }

	/// <summary>Gets the grid's own outer margin.</summary>
	Margin Margin { get; }

	/// <summary>Gets the grid's own inner padding.</summary>
	Padding Padding { get; }

	/// <summary>
	/// Gets the <b>content-bearing</b> cells in placement order, each paired with where it sits in the
	/// grid. The <see cref="IWindowControl"/> is the same instance the layout-node factory turns into a
	/// <see cref="LayoutNode"/>, so the layout correlates node to placement by index (the order of child
	/// nodes matches the order of this list). Content-less styled cells (a background/border applied to an
	/// empty cell) are intentionally excluded so this index-correlation invariant holds; such cells affect
	/// paint chrome only, never layout-tree children.
	/// </summary>
	IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> OrderedCells { get; }

	/// <summary>
	/// When <c>true</c>, a <see cref="GridUnitType.Star"/> track measured on an <em>effectively
	/// unbounded</em> axis self-sizes to its cells' content (like an Auto track) instead of collapsing to
	/// 0. This affects the MEASURE pass only — arrange still distributes Star tracks across the real,
	/// bounded extent — so the grid reports a content-based natural size to a parent that measures it
	/// unbounded (to discover its desired height/width), yet still fills when arranged larger.
	/// </summary>
	/// <remarks>
	/// The default is <c>false</c>, preserving the WinUI "Star-in-unbounded collapses to 0" contract for
	/// ordinary grids. <see cref="Controls.HorizontalGridControl"/> opts in so its single-row, flush layout
	/// self-sizes exactly as the retired HorizontalLayout did (content size at measure, fill at arrange).
	/// </remarks>
	bool StarTracksSelfSizeToContentInMeasure => false;
}
