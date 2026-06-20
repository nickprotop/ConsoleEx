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
/// A WinUI-<c>&lt;Grid&gt;</c>-style 2D layout algorithm. Sizes its row and column tracks from the
/// <see cref="IGridSource"/> seam (fixed, auto-to-content, or proportional star) and places each
/// cell into its assigned tracks. Auto tracks are sized to the largest desired size of the cells
/// that occupy them; spanning cells contribute their desired size spread evenly across the auto
/// tracks they cover.
/// </summary>
/// <remarks>
/// This type implements the measure and arrange passes, and clips each cell child to its track
/// rectangle in <see cref="GetPaintClipRect"/>.
/// </remarks>
public sealed class GridLayout : ILayoutContainer, IRegionClippingLayout
{
	private readonly IGridSource _source;

	/// <summary>
	/// Per-child cell rectangles in node-local coordinates, captured during the most recent arrange
	/// pass (pre-alignment, pre-child-margin). Keyed by child <see cref="LayoutNode"/>. Task 5's
	/// <see cref="GetPaintClipRect"/> uses this to clip each cell to its track rectangle.
	/// </summary>
	private readonly Dictionary<LayoutNode, LayoutRect> _cellRects = new();

	/// <summary>
	/// Initializes a new <see cref="GridLayout"/> backed by the given source.
	/// </summary>
	/// <param name="source">The grid source exposing track definitions, gaps, margin/padding, and cells.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
	public GridLayout(IGridSource source)
	{
		_source = source ?? throw new ArgumentNullException(nameof(source));
	}

	/// <summary>
	/// Measures the grid's cells, sizes the row and column tracks, and returns the grid's desired
	/// size (track totals plus gaps, margin, and padding, clamped to the incoming constraints).
	/// </summary>
	public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
	{
		IReadOnlyList<GridLength> colDefs = _source.ColumnDefinitions;
		IReadOnlyList<GridLength> rowDefs = _source.RowDefinitions;

		int colCount = colDefs.Count;
		int rowCount = rowDefs.Count;

		// Empty grid: no tracks or no cells means nothing to size.
		if (colCount == 0 || rowCount == 0 || _source.OrderedCells.Count == 0)
		{
			return LayoutSize.Zero;
		}

		Margin margin = _source.Margin;
		Padding padding = _source.Padding;

		int horizontalInset = margin.Left + margin.Right + padding.Left + padding.Right;
		int verticalInset = margin.Top + margin.Bottom + padding.Top + padding.Bottom;

		int availW = Math.Max(0, constraints.MaxWidth - horizontalInset);
		int availH = Math.Max(0, constraints.MaxHeight - verticalInset);

		// Measure each cell's content and accumulate the content-driven size of the Auto tracks it
		// spans. This also primes each child's DesiredSize, which the arrange pass reads back.
		(int[] autoContentColSizes, int[] autoContentRowSizes) =
			MeasureCellsAndCollectAutoContent(node, colDefs, rowDefs, availW, availH);

		int[] colSizes = GridTrackSizer.Size(colDefs, autoContentColSizes, availW, _source.ColumnGap);
		int[] rowSizes = GridTrackSizer.Size(rowDefs, autoContentRowSizes, availH, _source.RowGap);

		int colGaps = colCount > 1 ? (colCount - 1) * Math.Max(0, _source.ColumnGap) : 0;
		int rowGaps = rowCount > 1 ? (rowCount - 1) * Math.Max(0, _source.RowGap) : 0;

		int totalWidth = Sum(colSizes) + colGaps + horizontalInset;
		int totalHeight = Sum(rowSizes) + rowGaps + verticalInset;

		return new LayoutSize(
			Math.Min(totalWidth, constraints.MaxWidth),
			Math.Min(totalHeight, constraints.MaxHeight)
		);
	}

	/// <summary>
	/// Measure-pass helper: measures every visible cell against the loose available content area and
	/// accumulates, per axis, the content-driven size of the Auto tracks each cell spans. The arrange
	/// pass does not call this — it must not re-measure, so it rebuilds the Auto content sizes from each
	/// child's already-set <see cref="LayoutNode.DesiredSize"/>. The per-cell distribution itself
	/// (<see cref="ContributeAutoContent"/>) is shared by both passes.
	/// </summary>
	/// <returns>A tuple of the per-column and per-row Auto content sizes (non-Auto entries stay 0).</returns>
	private (int[] AutoContentColSizes, int[] AutoContentRowSizes) MeasureCellsAndCollectAutoContent(
		LayoutNode node,
		IReadOnlyList<GridLength> colDefs,
		IReadOnlyList<GridLength> rowDefs,
		int availW,
		int availH)
	{
		int[] autoContentColSizes = new int[colDefs.Count];
		int[] autoContentRowSizes = new int[rowDefs.Count];

		var cells = _source.OrderedCells;
		IReadOnlyList<LayoutNode> children = node.Children;

		// Correlate child node to placement by index; tolerate a count mismatch defensively.
		int pairCount = Math.Min(children.Count, cells.Count);

		// Loose constraint of the available content area for measuring each cell's content.
		LayoutConstraints cellConstraints = LayoutConstraints.Loose(availW, availH);

		for (int i = 0; i < pairCount; i++)
		{
			LayoutNode child = children[i];
			if (!child.IsVisible)
			{
				continue;
			}

			child.Measure(cellConstraints);
			LayoutSize desired = child.DesiredSize;

			GridPlacement placement = cells[i].Placement;

			ContributeAutoContent(autoContentColSizes, colDefs, placement.Col, placement.ColSpan, desired.Width);
			ContributeAutoContent(autoContentRowSizes, rowDefs, placement.Row, placement.RowSpan, desired.Height);
		}

		return (autoContentColSizes, autoContentRowSizes);
	}

	/// <summary>
	/// Distributes a cell's desired size across the Auto tracks it spans, contributing the per-track
	/// share to each spanned Auto track's running maximum. Fixed tracks within the span already
	/// provide size, so their sizes are subtracted before the remainder is spread across the Auto
	/// tracks. Non-Auto tracks are left untouched. A placement whose start index is out of range
	/// contributes nothing rather than crashing (validation happens later in GridControl.Place).
	/// </summary>
	private static void ContributeAutoContent(int[] autoContentSizes, IReadOnlyList<GridLength> defs, int start, int span, int desired)
	{
		int trackCount = defs.Count;

		// Out-of-range placement: be safe, contribute nothing.
		if (start < 0 || start >= trackCount)
		{
			return;
		}

		// Count Auto tracks in the span and sum the Fixed tracks' sizes, clamping to valid indices.
		int autoTracksInSpan = 0;
		int fixedInSpan = 0;
		for (int t = start; t < start + span && t < trackCount; t++)
		{
			if (defs[t].Type == GridUnitType.Auto)
			{
				autoTracksInSpan++;
			}
			else if (defs[t].Type == GridUnitType.Fixed)
			{
				fixedInSpan += defs[t].Value;
			}
		}

		if (autoTracksInSpan == 0)
		{
			return;
		}

		// Fixed tracks in the span already cover part of the content; only the remainder needs to
		// come from the Auto tracks.
		int autoDesired = Math.Max(0, desired - fixedInSpan);

		// Spread the remaining desired size evenly across the spanned Auto tracks (ceil so the total is covered).
		int perTrack = (autoDesired + autoTracksInSpan - 1) / autoTracksInSpan;

		// start is guaranteed >= 0 by the out-of-range guard at the top of this method.
		for (int t = start; t < start + span && t < trackCount; t++)
		{
			if (defs[t].Type == GridUnitType.Auto && perTrack > autoContentSizes[t])
			{
				autoContentSizes[t] = perTrack;
			}
		}
	}

	private static int Sum(int[] values)
	{
		int total = 0;
		for (int i = 0; i < values.Length; i++)
		{
			total += values[i];
		}
		return total;
	}

	/// <summary>
	/// Arranges the grid's cells into their assigned track rectangles. The local area is inset by the
	/// grid's margin then padding to form the track area; tracks are sized exactly as in the measure
	/// pass; each cell is positioned at its track offset, inset by the child's own margin, then aligned
	/// within the resulting inner box per the child's horizontal and vertical alignment (mirroring
	/// <see cref="HorizontalLayout.ArrangeChildren"/>). Invisible children are skipped.
	/// </summary>
	/// <remarks>
	/// <paramref name="finalRect"/> is node-local (origin 0,0); cell rectangles are therefore computed
	/// in local coordinates. Each cell's pre-alignment, pre-child-margin rectangle is cached in
	/// <see cref="_cellRects"/> for the paint-clip pass.
	/// </remarks>
	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		_cellRects.Clear();

		IReadOnlyList<GridLength> colDefs = _source.ColumnDefinitions;
		IReadOnlyList<GridLength> rowDefs = _source.RowDefinitions;

		int colCount = colDefs.Count;
		int rowCount = rowDefs.Count;

		IReadOnlyList<LayoutNode> children = node.Children;
		if (children.Count == 0 || colCount == 0 || rowCount == 0)
		{
			return;
		}

		Margin margin = _source.Margin;
		Padding padding = _source.Padding;

		int originX = margin.Left + padding.Left;
		int originY = margin.Top + padding.Top;

		int trackAreaW = Math.Max(0, finalRect.Width - margin.Left - margin.Right - padding.Left - padding.Right);
		int trackAreaH = Math.Max(0, finalRect.Height - margin.Top - margin.Bottom - padding.Top - padding.Bottom);

		var cells = _source.OrderedCells;
		int pairCount = Math.Min(children.Count, cells.Count);

		// Rebuild the Auto-track content sizes from each visible child's already-set DesiredSize. The
		// measure pass has already measured every child, so arrange must NOT re-measure here.
		int[] autoContentColSizes = new int[colCount];
		int[] autoContentRowSizes = new int[rowCount];
		for (int i = 0; i < pairCount; i++)
		{
			LayoutNode measuredChild = children[i];
			if (!measuredChild.IsVisible)
			{
				continue;
			}

			LayoutSize desired = measuredChild.DesiredSize;
			GridPlacement p = cells[i].Placement;
			ContributeAutoContent(autoContentColSizes, colDefs, p.Col, p.ColSpan, desired.Width);
			ContributeAutoContent(autoContentRowSizes, rowDefs, p.Row, p.RowSpan, desired.Height);
		}

		int columnGap = Math.Max(0, _source.ColumnGap);
		int rowGap = Math.Max(0, _source.RowGap);

		int[] colSizes = GridTrackSizer.Size(colDefs, autoContentColSizes, trackAreaW, columnGap);
		int[] rowSizes = GridTrackSizer.Size(rowDefs, autoContentRowSizes, trackAreaH, rowGap);

		int[] colOffsets = ComputeOffsets(colSizes, originX, columnGap);
		int[] rowOffsets = ComputeOffsets(rowSizes, originY, rowGap);

		for (int i = 0; i < pairCount; i++)
		{
			LayoutNode child = children[i];
			if (!child.IsVisible)
			{
				continue;
			}

			GridPlacement placement = cells[i].Placement;

			// Clamp the start indices and spans so a placement can never read outside the track arrays.
			int col = Math.Clamp(placement.Col, 0, colCount - 1);
			int row = Math.Clamp(placement.Row, 0, rowCount - 1);
			int colSpan = Math.Clamp(placement.ColSpan, 1, colCount - col);
			int rowSpan = Math.Clamp(placement.RowSpan, 1, rowCount - row);

			int cellX = colOffsets[col];
			int cellY = rowOffsets[row];
			int cellW = SpanExtent(colSizes, col, colSpan, columnGap);
			int cellH = SpanExtent(rowSizes, row, rowSpan, rowGap);

			var cellRect = new LayoutRect(cellX, cellY, cellW, cellH);
			_cellRects[child] = cellRect;

			// Inset the cell by the child's own margin to form the alignment box.
			Margin childMargin = child.Control?.Margin ?? default;
			int innerX = cellX + childMargin.Left;
			int innerY = cellY + childMargin.Top;
			int innerW = Math.Max(0, cellW - childMargin.Left - childMargin.Right);
			int innerH = Math.Max(0, cellH - childMargin.Top - childMargin.Bottom);

			HorizontalAlignment hAlign = child.Control?.HorizontalAlignment ?? HorizontalAlignment.Stretch;
			VerticalAlignment vAlign = child.Control?.VerticalAlignment ?? VerticalAlignment.Fill;

			(int childX, int childW) = AlignHorizontal(hAlign, innerX, innerW, child.DesiredSize.Width);
			(int childY, int childH) = AlignVertical(vAlign, innerY, innerH, child.DesiredSize.Height);

			child.Arrange(new LayoutRect(childX, childY, childW, childH));
		}
	}

	/// <summary>
	/// Computes the running start offset of each track: <c>origin + Σ(sizes before it) + (index * gap)</c>.
	/// </summary>
	private static int[] ComputeOffsets(int[] sizes, int origin, int gap)
	{
		int[] offsets = new int[sizes.Length];
		int running = origin;
		for (int i = 0; i < sizes.Length; i++)
		{
			offsets[i] = running;
			running += sizes[i] + gap;
		}
		return offsets;
	}

	/// <summary>
	/// Sums the sizes of the tracks a cell spans, plus the inter-track gaps the span crosses, so a
	/// spanning cell absorbs the gaps between the tracks it covers.
	/// </summary>
	private static int SpanExtent(int[] sizes, int start, int span, int gap)
	{
		int extent = 0;
		int traversed = 0;
		for (int t = start; t < start + span && t < sizes.Length; t++)
		{
			extent += sizes[t];
			traversed++;
		}

		// Only count gaps between tracks actually traversed, so a span clipped by the array bound
		// doesn't add phantom gaps.
		if (traversed > 1)
		{
			extent += (traversed - 1) * gap;
		}
		return extent;
	}

	/// <summary>
	/// The three ways an aligned (non-stretch) child can sit within its track: pinned to the start,
	/// centred, or pinned to the end. Both axes map their alignment enum onto this shared positioning.
	/// </summary>
	private enum AxisAnchor
	{
		Start,
		Center,
		End,
		Stretch
	}

	/// <summary>
	/// Positions a child along one axis within its inner (margin-inset) box. Stretch fills the box;
	/// Start/Center/End use the child's desired extent and float it within the box. Shared by both
	/// axes so the positioning logic exists once.
	/// </summary>
	private static (int Start, int Extent) AlignAxis(AxisAnchor anchor, int innerStart, int innerExtent, int desiredExtent)
	{
		switch (anchor)
		{
			case AxisAnchor.Start:
				return (innerStart, desiredExtent);
			case AxisAnchor.Center:
				// Clamp so an oversized child never starts before the cell's inner edge.
				return (Math.Max(innerStart, innerStart + (innerExtent - desiredExtent) / 2), desiredExtent);
			case AxisAnchor.End:
				return (Math.Max(innerStart, innerStart + innerExtent - desiredExtent), desiredExtent);
			case AxisAnchor.Stretch:
			default:
				return (innerStart, innerExtent);
		}
	}

	/// <summary>
	/// Resolves a child's horizontal start and extent, mirroring the switch shape in
	/// <see cref="HorizontalLayout.ArrangeChildren"/> (Stretch fills; Left/Center/Right float).
	/// </summary>
	private static (int Start, int Extent) AlignHorizontal(HorizontalAlignment alignment, int innerStart, int innerExtent, int desiredExtent)
	{
		AxisAnchor anchor = alignment switch
		{
			HorizontalAlignment.Left => AxisAnchor.Start,
			HorizontalAlignment.Center => AxisAnchor.Center,
			HorizontalAlignment.Right => AxisAnchor.End,
			_ => AxisAnchor.Stretch,
		};
		return AlignAxis(anchor, innerStart, innerExtent, desiredExtent);
	}

	/// <summary>
	/// Resolves a child's vertical start and extent, mirroring the switch shape in
	/// <see cref="HorizontalLayout.ArrangeChildren"/> (Fill fills; Top/Center/Bottom float).
	/// </summary>
	private static (int Start, int Extent) AlignVertical(VerticalAlignment alignment, int innerStart, int innerExtent, int desiredExtent)
	{
		AxisAnchor anchor = alignment switch
		{
			VerticalAlignment.Top => AxisAnchor.Start,
			VerticalAlignment.Center => AxisAnchor.Center,
			VerticalAlignment.Bottom => AxisAnchor.End,
			_ => AxisAnchor.Stretch,
		};
		return AlignAxis(anchor, innerStart, innerExtent, desiredExtent);
	}

	/// <summary>
	/// Restricts a cell child's paint area to its track rectangle, intersected with the parent clip,
	/// so a child can never paint outside the cell it was placed in. The cell rectangle cached during
	/// arrange (<see cref="_cellRects"/>) is node-local, so it is offset by the grid node's absolute
	/// origin to reach screen space before intersecting.
	/// </summary>
	/// <param name="child">The cell child whose paint clip is being resolved.</param>
	/// <param name="parentClipRect">The parent's visible bounds, in absolute/screen coordinates.</param>
	/// <returns>
	/// The cell rectangle (in absolute coordinates) intersected with <paramref name="parentClipRect"/>,
	/// or the parent clip unchanged when no cell rectangle is recorded for the child (e.g. arrange has
	/// not run yet, or the child was removed between arrange and paint) so the child is never over-clipped.
	/// </returns>
	public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
	{
		if (child.Parent == null || !_cellRects.TryGetValue(child, out var localCell))
		{
			return parentClipRect;
		}

		LayoutNode parent = child.Parent;
		var absoluteCell = new LayoutRect(
			parent.AbsoluteBounds.X + localCell.X,
			parent.AbsoluteBounds.Y + localCell.Y,
			localCell.Width,
			localCell.Height);

		return parentClipRect.Intersect(absoluteCell);
	}
}
