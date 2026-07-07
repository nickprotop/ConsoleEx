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
	/// Per-cell OUTER rectangles in node-local coordinates, keyed by the cell's start (row, col). Captured
	/// during the most recent arrange pass for EVERY styled cell — content-bearing and content-less
	/// styled-empty cells alike — so <see cref="Controls.GridControl.PaintDOM"/> can paint per-cell chrome
	/// (background fill and border) over the right region. Unlike <see cref="_cellRects"/> (inner, for
	/// child clipping) these are the full cell rectangle including any border/padding inset.
	/// </summary>
	private readonly Dictionary<(int Row, int Col), LayoutRect> _cellRectsByCoord = new();

	// Per-track arranged sizes/offsets from the last ArrangeChildren pass. Read by GridControl's splitter
	// logic to render handles and translate a cell-drag into a track resize. Window-content relative offsets.
	private int[] _lastColSizes = System.Array.Empty<int>();
	private int[] _lastRowSizes = System.Array.Empty<int>();
	private int[] _lastColOffsets = System.Array.Empty<int>();
	private int[] _lastRowOffsets = System.Array.Empty<int>();
	private int _lastColumnGap;
	private int _lastRowGap;

	/// <summary>Per-track arranged metrics from the last arrange, for GridControl splitter hit-testing/resize.</summary>
	internal (int[] ColSizes, int[] RowSizes, int[] ColOffsets, int[] RowOffsets, int ColumnGap, int RowGap) LastArrangeMetrics
		=> (_lastColSizes, _lastRowSizes, _lastColOffsets, _lastRowOffsets, _lastColumnGap, _lastRowGap);

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

		// When an axis is effectively unbounded (e.g. a Star-track grid measured for vertical
		// stacking), there is no finite extent for Star tracks to divide. Dividing ~int.MaxValue
		// among the stars would push later cells billions of cells off-screen (silent content
		// loss). Following the WinUI contract, Star-in-unbounded collapses to 0: pass 0 as the
		// track-sizer available so Stars get no remainder, while Fixed/Auto tracks (which size from
		// their own definitions, independent of available) are unaffected.
		int starAvailW = constraints.IsWidthEffectivelyUnbounded ? 0 : availW;
		int starAvailH = constraints.IsHeightEffectivelyUnbounded ? 0 : availH;

		int columnGap = Math.Max(0, _source.ColumnGap);
		int rowGap = Math.Max(0, _source.RowGap);

		// Opt-in (ContentSizedStars): a Star track on an effectively-unbounded axis normally collapses to 0
		// (WinUI contract, above). When the source opts in, instead give each single-track Star cell a content
		// FLOOR so the track self-sizes to the child that was already measured at its provisional allocation:
		//   - the child's DesiredSize on this axis WAS measured above (at the estimated per-track width, and at
		//     the unbounded extent along this axis — a stable content measure that does NOT depend on any Star
		//     division, so it is a fixed point across re-renders);
		//   - the floor is fed as the Star track's Min while starAvail stays 0, so GridTrackSizer clamps the
		//     Star track up to exactly that content (no division of ~int.MaxValue, no oscillation).
		// ARRANGE (below) still distributes Star across the real, bounded extent. The bounded axis is untouched
		// (its existing Star split already sizes correctly), so this branch is measure-only and unbounded-only.
		IReadOnlyList<GridLength> measureColDefs = colDefs;
		IReadOnlyList<GridLength> measureRowDefs = rowDefs;
		if (_source.StarTracksSelfSizeToContentInMeasure)
		{
			// Report the content-PACKED size on both axes (mirroring master's HorizontalGridControl.MeasureDOM,
			// which always sums column content regardless of alignment or the parent's finite width) so a
			// content-tight parent - the window root for a Left/Center/Right grid - packs the cluster, while a
			// wider parent (a Stretch slot or a ScrollablePanel viewport) still lets the flex columns fan out at
			// ARRANGE. Each single-track Star cell gets a content FLOOR (its already-measured DesiredSize, a
			// stable fixed point that does not depend on any Star division) AND the Star pool is forced to 0
			// below, so GridTrackSizer pins every Star to exactly its floor. This runs on the bounded axis too
			// (needed for the window-root Left case), not only the unbounded axis of the original WinUI
			// contract. ARRANGE is untouched and still fans Star out across the real arranged extent.
			measureColDefs = ApplyStarContentFloor(colDefs, node, availW, columnGap, isColumn: true);
			measureRowDefs = ApplyStarContentFloor(rowDefs, node, availH, rowGap, isColumn: false);
			starAvailW = 0;
			starAvailH = 0;
		}

		int[] colSizes = GridTrackSizer.Size(measureColDefs, autoContentColSizes, starAvailW, _source.ColumnGap);
		int[] rowSizes = GridTrackSizer.Size(measureRowDefs, autoContentRowSizes, starAvailH, _source.RowGap);

		// PASS 2: re-measure each content cell against its REAL cell extent (the actual column width
		// computed in pass 1) so wrapping controls reflow to their column rather than reporting a single
		// no-wrap line that later gets clipped. Re-priming each child's DesiredSize against the narrower
		// inner width makes a Wrap=true control report a taller height; the Auto row sizes are then
		// recomputed from those wrapped heights below. Column widths drove the wrap, so they are not
		// re-derived (wrapping does not change how wide a column is, only how tall the cell becomes).
		bool anyReflow = RemeasureCellsAgainstCellExtent(node, colDefs, rowDefs, colSizes, rowSizes, columnGap, rowGap);

		if (anyReflow)
		{
			// Recompute ROW auto-content from the NEW (wrapped) DesiredSize heights so Auto rows grow to
			// fit the reflowed text. Columns are unaffected, so colSizes is reused as-is.
			int[] autoContentRowSizes2 = new int[measureRowDefs.Count];
			var cells = _source.OrderedCells;
			IReadOnlyList<LayoutNode> children = node.Children;
			int pairCount = Math.Min(children.Count, cells.Count);
			for (int i = 0; i < pairCount; i++)
			{
				LayoutNode child = children[i];
				if (!child.IsVisible)
				{
					continue;
				}

				GridPlacement p = cells[i].Placement;
				ContributeAutoContent(autoContentRowSizes2, measureRowDefs, p.Row, p.RowSpan, child.DesiredSize.Height);
			}

			rowSizes = GridTrackSizer.Size(measureRowDefs, autoContentRowSizes2, starAvailH, _source.RowGap);
		}

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

		// Pre-estimate each column's width so a cell is measured at (close to) its real column width rather
		// than the full grid width. Measuring a wrapping/scrolling cell at the full width makes it report a
		// no-wrap content size that is wrong for its narrow column AND pollutes the child's cached metrics
		// (e.g. a ScrollablePanel caches a too-small content height, capping its scroll). Auto columns have
		// no width yet (they size FROM this measure), so an Auto cell is still measured at the full available
		// width — correct, since Auto means "size to content". Fixed columns use their exact width; Star
		// columns use their proportional share of the space left after Fixed/Auto.
		int[] estColWidths = EstimateColumnWidths(colDefs, availW, columnGapForMeasure: _source.ColumnGap);

		for (int i = 0; i < pairCount; i++)
		{
			LayoutNode child = children[i];
			if (!child.IsVisible)
			{
				continue;
			}

			GridPlacement placement = cells[i].Placement;

			// Width to measure this cell at: the sum of its spanned columns' estimated widths (+ crossed
			// gaps), or the full available width if any spanned column is Auto (Auto must measure at content
			// width). Height is always loose-available (rows are sized after).
			int measureW = EstimatedCellMeasureWidth(placement, colDefs, estColWidths, availW, _source.ColumnGap);

			child.Measure(LayoutConstraints.Loose(measureW, availH));
			LayoutSize desired = child.DesiredSize;

			ContributeAutoContent(autoContentColSizes, colDefs, placement.Col, placement.ColSpan, desired.Width);
			ContributeAutoContent(autoContentRowSizes, rowDefs, placement.Row, placement.RowSpan, desired.Height);
		}

		return (autoContentColSizes, autoContentRowSizes);
	}

	/// <summary>
	/// Pass-1 estimate of each column's width, used only to measure cells at (close to) their real column
	/// width instead of the full grid width. Fixed columns get their exact value; Star columns split the
	/// space left after Fixed (and after reserving gaps) by weight; Auto columns return -1 (unknown — an
	/// Auto cell is measured at the full available width, since Auto sizes to content).
	/// </summary>
	private static int[] EstimateColumnWidths(IReadOnlyList<GridLength> colDefs, int availW, int columnGapForMeasure)
	{
		int count = colDefs.Count;
		int[] widths = new int[count];
		int gap = Math.Max(0, columnGapForMeasure);
		int totalGap = count > 1 ? (count - 1) * gap : 0;

		int fixedSum = 0;
		double starWeight = 0;
		bool anyAuto = false;
		for (int c = 0; c < count; c++)
		{
			switch (colDefs[c].Type)
			{
				case GridUnitType.Fixed:
					fixedSum += colDefs[c].Value;
					break;
				case GridUnitType.Star:
					starWeight += colDefs[c].Weight;
					break;
				default:
					anyAuto = true;
					break;
			}
		}

		// Space available to Star columns after fixed tracks and gaps. Auto columns are unknown here; if
		// any exist we cannot estimate Star precisely, so we leave a conservative remainder.
		int starPool = Math.Max(0, availW - totalGap - fixedSum);
		for (int c = 0; c < count; c++)
		{
			switch (colDefs[c].Type)
			{
				case GridUnitType.Fixed:
					widths[c] = colDefs[c].Value;
					break;
				case GridUnitType.Star:
					widths[c] = starWeight > 0
						? (int)System.Math.Round(starPool * (colDefs[c].Weight / starWeight))
						: 0;
					break;
				default:
					widths[c] = -1; // Auto: measure at content width
					break;
			}
		}

		_ = anyAuto; // estimate is best-effort; an Auto neighbour just makes Star slightly generous
		return widths;
	}

	/// <summary>
	/// The width to measure a cell at in pass 1: the sum of its spanned columns' estimated widths plus the
	/// gaps it crosses. Returns the full available width if any spanned column is Auto (Auto must measure at
	/// content width) or if the estimate is non-positive.
	/// </summary>
	private static int EstimatedCellMeasureWidth(
		GridPlacement placement, IReadOnlyList<GridLength> colDefs, int[] estColWidths, int availW, int columnGap)
	{
		int count = colDefs.Count;
		int start = placement.Col;
		if (start < 0 || start >= count)
		{
			return availW;
		}

		int span = Math.Clamp(placement.ColSpan, 1, count - start);
		int gap = Math.Max(0, columnGap);
		int width = 0;
		int traversed = 0;
		for (int c = start; c < start + span && c < count; c++)
		{
			if (estColWidths[c] < 0)
			{
				return availW; // spans an Auto column → measure at content width
			}
			width += estColWidths[c];
			traversed++;
		}
		if (traversed > 1)
		{
			width += (traversed - 1) * gap;
		}

		return width > 0 ? width : availW;
	}

	/// <summary>
	/// Pass-2 measure helper: re-measures each visible content cell against the inner extent of the cell
	/// it was placed in (the column width resolved by pass 1) so wrapping controls reflow to their column.
	/// Re-measuring is a measure-pass operation and is both cheap and correct for non-wrapping controls
	/// (they report the same size). A cell with no finite inner width (e.g. a Star column collapsed to 0 in
	/// an unbounded axis) is skipped: wrapping to width 0 is degenerate, so its pass-1 DesiredSize is kept.
	/// </summary>
	/// <returns><c>true</c> if at least one cell was re-measured against a finite inner extent; otherwise <c>false</c>.</returns>
	private bool RemeasureCellsAgainstCellExtent(
		LayoutNode node,
		IReadOnlyList<GridLength> colDefs,
		IReadOnlyList<GridLength> rowDefs,
		int[] colSizes,
		int[] rowSizes,
		int columnGap,
		int rowGap)
	{
		var cells = _source.OrderedCells;
		IReadOnlyList<LayoutNode> children = node.Children;
		int pairCount = Math.Min(children.Count, cells.Count);

		bool anyReflow = false;
		for (int i = 0; i < pairCount; i++)
		{
			LayoutNode child = children[i];
			if (!child.IsVisible)
			{
				continue;
			}

			(int innerW, int _) = CellInnerExtent(
				cells[i].Placement, colSizes, rowSizes, colDefs.Count, rowDefs.Count, columnGap, rowGap, child);

			// No meaningful width to wrap into (e.g. Star collapsed to 0 in an unbounded axis): keep the
			// pass-1 DesiredSize rather than reflowing to width 0.
			if (innerW <= 0)
			{
				continue;
			}

			// Add the child's own margin back onto the measure width. CellInnerExtent already subtracted
			// the child margin (it is the alignment box used by arrange), but a control's MeasureDOM treats
			// constraints.MaxWidth as its TOTAL allocation and subtracts its margin again internally. Passing
			// innerW directly would double-subtract the margin, starving the content by 2*margin and forcing
			// a single-line label (e.g. "Animation" in an Auto column) to wrap. Re-add it so the control's
			// content gets exactly cellWidth - border - cellPadding - margin, matching pass 1 and arrange.
			Margin childMargin = child.Control?.Margin ?? default;
			int measureW = innerW + childMargin.Left + childMargin.Right;

			// Constrain WIDTH to the cell's inner width so a wrapping control reflows to its column, but
			// leave HEIGHT unbounded: the goal is to discover the wrapped height. Clamping height to the
			// pass-1 row size would cap the reported height at the single-line measure and prevent an Auto
			// row from growing. Fixed/Star rows ignore the reported height (only Auto rows size to content),
			// and arrange clips each cell to its track, so an over-tall report is harmless for those rows.
			child.Measure(LayoutConstraints.Loose(measureW, int.MaxValue));
			anyReflow = true;
		}

		return anyReflow;
	}

	/// <summary>
	/// Computes the inner content extent (width and height available to the child) of the cell at the given
	/// placement, mirroring the exact inset order the arrange pass uses: the spanned track extent, minus the
	/// cell border (1 each side when bordered), minus the cell padding, minus the child's own margin, floored
	/// at 0. Shared by the pass-2 re-measure and <see cref="ArrangeChildren"/> so the wrap width a control is
	/// measured against is identical to the box it is later arranged into.
	/// </summary>
	private static (int InnerW, int InnerH) CellInnerExtent(
		GridPlacement placement,
		int[] colSizes,
		int[] rowSizes,
		int colCount,
		int rowCount,
		int columnGap,
		int rowGap,
		LayoutNode child)
	{
		int col = Math.Clamp(placement.Col, 0, colCount - 1);
		int row = Math.Clamp(placement.Row, 0, rowCount - 1);
		int colSpan = Math.Clamp(placement.ColSpan, 1, colCount - col);
		int rowSpan = Math.Clamp(placement.RowSpan, 1, rowCount - row);

		int cellW = SpanExtent(colSizes, col, colSpan, columnGap);
		int cellH = SpanExtent(rowSizes, row, rowSpan, rowGap);

		int border = placement.Border != BorderStyle.None ? 1 : 0;
		Padding cellPadding = placement.CellPadding;
		Margin childMargin = child.Control?.Margin ?? default;

		int innerW = Math.Max(0, cellW - 2 * border - cellPadding.Left - cellPadding.Right - childMargin.Left - childMargin.Right);
		int innerH = Math.Max(0, cellH - 2 * border - cellPadding.Top - cellPadding.Bottom - childMargin.Top - childMargin.Bottom);

		return (innerW, innerH);
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
	/// Measure-pass helper for the <see cref="IGridSource.StarTracksSelfSizeToContentInMeasure"/> opt-in on an
	/// effectively-unbounded axis. Returns a copy of <paramref name="defs"/> in which each single-track
	/// <see cref="GridUnitType.Star"/> track's <see cref="GridLength.Min"/> is raised to its cell's already-measured
	/// content size along the given axis. Because the caller passes <c>starAvail = 0</c> to
	/// <see cref="GridTrackSizer"/> on an unbounded axis, the floor is what actually sizes the Star track: the
	/// track-sizer clamps it up from 0 to exactly the content size — so the grid reports a content-based desired
	/// size instead of collapsing the Star track to 0, WITHOUT dividing ~int.MaxValue among the stars.
	/// <para>
	/// The content size is read from each child's <see cref="LayoutNode.DesiredSize"/>, which the measure pass
	/// already computed at the child's provisional per-track allocation and at the axis's unbounded extent. That
	/// measure does not depend on any Star division, so it is a stable fixed point: measure width/height equals
	/// the width/height the child is later arranged at, so a content-dependent child (e.g. a table) converges
	/// across re-renders rather than oscillating.
	/// </para>
	/// <para>
	/// Over-subscription guard: if the Star content floors plus the Fixed/gap consumption on this axis would
	/// exceed <paramref name="available"/>, the original (un-floored) defs are returned so the plain even Star
	/// split runs instead. Only single-track (non-spanning) Star cells contribute a floor, since a spanning
	/// cell's content is shared across its tracks and must not pin any one of them.
	/// </para>
	/// </summary>
	private IReadOnlyList<GridLength> ApplyStarContentFloor(
		IReadOnlyList<GridLength> defs,
		LayoutNode node,
		int available,
		int gap,
		bool isColumn)
	{
		int count = defs.Count;
		int[] floor = new int[count];

		var cells = _source.OrderedCells;
		IReadOnlyList<LayoutNode> children = node.Children;
		int pairCount = Math.Min(children.Count, cells.Count);

		for (int i = 0; i < pairCount; i++)
		{
			LayoutNode child = children[i];
			if (!child.IsVisible)
			{
				continue;
			}

			GridPlacement p = cells[i].Placement;
			int start = isColumn ? p.Col : p.Row;
			int span = isColumn ? p.ColSpan : p.RowSpan;
			// Only single-track cells contribute a per-track floor; a spanning cell's content is shared across
			// its tracks and must not pin any one of them to the whole content size.
			if (span != 1 || start < 0 || start >= count || defs[start].Type != GridUnitType.Star)
			{
				continue;
			}

			int content = isColumn ? child.DesiredSize.Width : child.DesiredSize.Height;
			if (content > floor[start])
			{
				floor[start] = content;
			}
		}

		// Over-subscription guard: sum Fixed consumption + gaps + Star floors. If they exceed the available
		// extent, the floors cannot all be honoured inside the allocation, so keep the plain even Star split.
		int gapTotal = count > 1 ? (count - 1) * Math.Max(0, gap) : 0;
		int consumedPlusFloors = gapTotal;
		for (int i = 0; i < count; i++)
		{
			GridLength def = defs[i];
			if (def.Type == GridUnitType.Star)
			{
				consumedPlusFloors += floor[i];
			}
			else if (def.Type == GridUnitType.Fixed)
			{
				consumedPlusFloors += ClampTrack(def.Value, def.Min, def.Max);
			}
		}
		if (consumedPlusFloors > available)
		{
			return defs;
		}

		var result = new GridLength[count];
		for (int i = 0; i < count; i++)
		{
			GridLength def = defs[i];
			if (def.Type == GridUnitType.Star && floor[i] > 0)
			{
				int min = def.Min.HasValue ? Math.Max(def.Min.Value, floor[i]) : floor[i];
				result[i] = GridLength.Star(def.Weight, min, def.Max);
			}
			else
			{
				result[i] = def;
			}
		}
		return result;
	}

	/// <summary>Clamps a track size to its optional Min/Max and floors it at 0.</summary>
	private static int ClampTrack(int value, int? min, int? max)
	{
		if (min.HasValue && value < min.Value) value = min.Value;
		if (max.HasValue && value > max.Value) value = max.Value;
		return Math.Max(0, value);
	}

	/// <summary>
	/// Arranges the grid's cells into their assigned track rectangles. The local area is inset by the
	/// grid's margin then padding to form the track area; tracks are sized exactly as in the measure
	/// pass; each cell is positioned at its track offset, inset by the child's own margin, then aligned
	/// within the resulting inner box per the child's horizontal and vertical alignment (mirroring
	/// <c>HorizontalLayout.ArrangeChildren</c>). Invisible children are skipped.
	/// </summary>
	/// <remarks>
	/// <paramref name="finalRect"/> is node-local (origin 0,0); cell rectangles are therefore computed
	/// in local coordinates. Each cell's pre-alignment, pre-child-margin rectangle is cached in
	/// <see cref="_cellRects"/> for the paint-clip pass.
	/// </remarks>
	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		_cellRects.Clear();
		_cellRectsByCoord.Clear();

		IReadOnlyList<GridLength> colDefs = _source.ColumnDefinitions;
		IReadOnlyList<GridLength> rowDefs = _source.RowDefinitions;

		int colCount = colDefs.Count;
		int rowCount = rowDefs.Count;

		IReadOnlyList<LayoutNode> children = node.Children;
		// No tracks means nothing to lay out. A grid with no content children can still have content-less
		// styled cells whose chrome rectangles must be recorded, so an empty child list does NOT short-circuit.
		if (colCount == 0 || rowCount == 0)
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

		_lastColSizes = colSizes;
		_lastRowSizes = rowSizes;
		_lastColOffsets = colOffsets;
		_lastRowOffsets = rowOffsets;
		_lastColumnGap = columnGap;
		_lastRowGap = rowGap;

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

			// OUTER cell rectangle (full cell, including any border + padding): recorded by coordinate
			// for the grid's per-cell chrome painting.
			var outerRect = new LayoutRect(cellX, cellY, cellW, cellH);
			_cellRectsByCoord[(placement.Row, placement.Col)] = outerRect;

			// Inset the content area by the cell's border (1 on each side when bordered) then its padding,
			// so content sits inside the border and padding. The remaining rectangle is the INNER cell
			// area: it is both the child clip (stored in _cellRects) and the box the child aligns within.
			int border = placement.Border != BorderStyle.None ? 1 : 0;
			Padding cellPadding = placement.CellPadding;
			int contentX = cellX + border + cellPadding.Left;
			int contentY = cellY + border + cellPadding.Top;
			int contentW = Math.Max(0, cellW - 2 * border - cellPadding.Left - cellPadding.Right);
			int contentH = Math.Max(0, cellH - 2 * border - cellPadding.Top - cellPadding.Bottom);

			var cellRect = new LayoutRect(contentX, contentY, contentW, contentH);
			_cellRects[child] = cellRect;

			// Align the child within the cell's CONTENT box (border + cell-padding already removed). Do NOT
			// pre-subtract the child's own margin here: the control's own paint (its leftInset/topInset)
			// already accounts for its margin, and its DesiredSize already includes it. Subtracting the
			// margin to form the alignment box AND letting the control subtract it again in paint would
			// double-subtract — starving the content by 2*margin and truncating a tight Auto-cell label
			// (e.g. "Animation" -> "Animati"). The grid reserves border + cell-padding; the control owns
			// its margin.
			HorizontalAlignment hAlign = child.Control?.HorizontalAlignment ?? HorizontalAlignment.Stretch;
			VerticalAlignment vAlign = child.Control?.VerticalAlignment ?? VerticalAlignment.Fill;

			(int childX, int childW) = AlignHorizontal(hAlign, contentX, contentW, child.DesiredSize.Width);
			(int childY, int childH) = AlignVertical(vAlign, contentY, contentH, child.DesiredSize.Height);

			child.Arrange(new LayoutRect(childX, childY, childW, childH));
		}

		// Record OUTER rectangles for content-less styled cells (styled empty cells). They have no child
		// node to arrange, so their geometry is derived directly from the track offsets/sizes; the grid's
		// PaintDOM reads these to paint their chrome. Only GridControl exposes these; other IGridSource
		// implementations (if any) simply contribute no content-less cells.
		if (_source is Controls.GridControl gridControl)
		{
			var allCells = gridControl.AllOrderedCells();
			for (int i = 0; i < allCells.Count; i++)
			{
				if (allCells[i].Control != null)
				{
					continue; // content cells already recorded in the loop above
				}

				GridPlacement placement = allCells[i].Placement;
				int col = Math.Clamp(placement.Col, 0, colCount - 1);
				int row = Math.Clamp(placement.Row, 0, rowCount - 1);
				int colSpan = Math.Clamp(placement.ColSpan, 1, colCount - col);
				int rowSpan = Math.Clamp(placement.RowSpan, 1, rowCount - row);

				int cellX = colOffsets[col];
				int cellY = rowOffsets[row];
				int cellW = SpanExtent(colSizes, col, colSpan, columnGap);
				int cellH = SpanExtent(rowSizes, row, rowSpan, rowGap);
				_cellRectsByCoord[(placement.Row, placement.Col)] = new LayoutRect(cellX, cellY, cellW, cellH);
			}
		}
	}

	/// <summary>
	/// Returns the OUTER (full) cell rectangle in node-local coordinates for the cell whose top-left is
	/// <paramref name="row"/>/<paramref name="col"/>, as recorded by the most recent arrange pass. Used by
	/// <see cref="Controls.GridControl.PaintDOM"/> to paint per-cell chrome (background and border).
	/// </summary>
	/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
	/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
	/// <param name="localRect">The cell's local outer rectangle when found.</param>
	/// <returns><c>true</c> when a rectangle was recorded for the cell; otherwise <c>false</c>.</returns>
	internal bool TryGetCellRect(int row, int col, out LayoutRect localRect) =>
		_cellRectsByCoord.TryGetValue((row, col), out localRect);

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
		// A non-stretch child is never arranged LARGER than its cell: a child whose desired size exceeds
		// the cell is clamped to the cell extent (and the cell clip trims any overflow). This is essential
		// for scroll-capable children (e.g. a ScrollablePanel in a cell) — if the child were arranged at
		// its full content height, its node bounds would equal its content and it would believe it has
		// nothing to scroll. Clamping gives it a bounded viewport so it can scroll inside the cell.
		int extent = Math.Min(desiredExtent, innerExtent);
		switch (anchor)
		{
			case AxisAnchor.Start:
				return (innerStart, extent);
			case AxisAnchor.Center:
				// Clamp so an oversized child never starts before the cell's inner edge.
				return (Math.Max(innerStart, innerStart + (innerExtent - extent) / 2), extent);
			case AxisAnchor.End:
				return (Math.Max(innerStart, innerStart + innerExtent - extent), extent);
			case AxisAnchor.Stretch:
			default:
				return (innerStart, innerExtent);
		}
	}

	/// <summary>
	/// Resolves a child's horizontal start and extent, mirroring the switch shape in
	/// <c>HorizontalLayout.ArrangeChildren</c> (Stretch fills; Left/Center/Right float).
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
	/// <c>HorizontalLayout.ArrangeChildren</c> (Fill fills; Top/Center/Bottom float).
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
