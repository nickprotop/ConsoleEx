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
/// This type only implements the measure pass. <see cref="ArrangeChildren"/> (Task 4) and
/// <see cref="GetPaintClipRect"/> (Task 5) are stubbed.
/// </remarks>
public sealed class GridLayout : ILayoutContainer, IRegionClippingLayout
{
	private readonly IGridSource _source;

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

		// Accumulate the content-driven size of Auto tracks. Non-Auto tracks stay 0.
		int[] autoContentColSizes = new int[colCount];
		int[] autoContentRowSizes = new int[rowCount];

		var cells = _source.OrderedCells;
		IReadOnlyList<LayoutNode> children = node.Children;

		// Correlate child node to placement by index; tolerate a count mismatch defensively.
		int pairCount = Math.Min(children.Count, cells.Count);

		// Loose constraint of the available content area for measuring each cell's content.
		// TODO Task 4: measure cells against their own track extents once tracks are sized (currently loose-available, can over-report content size).
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

		for (int t = Math.Max(0, start); t < start + span && t < trackCount; t++)
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
	/// Arranges the grid's cells into their assigned tracks. Not yet implemented.
	/// </summary>
	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		// TODO Task 4: implemented next; no-op until then so an arranged grid node degrades rather than crashes.
		return;
	}

	/// <summary>
	/// Gets the paint clip rectangle for a child cell. Not yet implemented; returns the parent clip.
	/// </summary>
	public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
	{
		// TODO: Task 5 — clip each cell to its track rectangle (and honour spanning).
		return parentClipRect;
	}
}
