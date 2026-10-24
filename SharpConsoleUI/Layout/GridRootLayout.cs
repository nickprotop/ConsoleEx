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
/// A window root layout that expresses <see cref="StickyPosition"/> as grid placement:
/// sticky-top children occupy <see cref="GridUnitType.Auto"/> rows at the top, sticky-bottom
/// children occupy Auto rows at the bottom, and everything else shares one
/// <see cref="GridUnitType.Star"/> row in the middle — the scrolling body.
/// </summary>
/// <remarks>
/// <para>
/// This is the experimental replacement for <see cref="WindowContentLayout"/>, the last
/// self-scrolling container in the codebase (added nine days into the project, 7 commits in 20
/// months). It exists to answer one question: can sticky be reproduced byte-identically as grid
/// placement? Until that is answered it runs <em>beside</em> the old layout, never instead of it.
/// </para>
/// <para>
/// It implements <see cref="IGridSource"/> and delegates to the real <see cref="GridLayout"/>
/// rather than reimplementing track sizing. That is the whole point: if the mapping is honest,
/// the shipped and tested grid algorithm does the work, and sticky stops being a separate
/// concept. Reimplementing the arithmetic here would prove nothing.
/// </para>
/// <para>
/// <b>Scrolling is not implemented yet.</b> The body row is laid out at its natural position; the
/// scroll offset belongs to a <see cref="ScrollLayout"/> that will own the star row. Comparisons
/// against the old layout are therefore only valid at <c>ScrollOffset == 0</c> until that lands.
/// </para>
/// </remarks>
public sealed class GridRootLayout : ILayoutContainer, IRegionClippingLayout, IGridSource
{
	private readonly GridLayout _grid;

	// Rebuilt on every measure/arrange from the live child list, because a child's StickyPosition
	// and Visible can both change between passes. Caching them would desync exactly the way the
	// pre-b310f0d5 ScrollablePanel did.
	private readonly List<GridLength> _rows = new();
	private readonly List<(IWindowControl Control, GridPlacement Placement)> _cells = new();

	/// <summary>Row index of the single star (body) row in the current track list.</summary>
	private int _bodyRowIndex;

	public GridRootLayout()
	{
		_grid = new GridLayout(this);
	}

	#region IGridSource

	public IReadOnlyList<GridLength> RowDefinitions => _rows;

	/// <summary>A window root is a single column: children stretch across the full width.</summary>
	public IReadOnlyList<GridLength> ColumnDefinitions { get; } = new[] { GridLength.Star(1) };

	/// <summary>
	/// No gaps. <see cref="WindowContentLayout"/> stacks children edge to edge, and any gap here
	/// would shift every row below it and fail the golden-buffer comparison immediately.
	/// </summary>
	public int RowGap => 0;

	public int ColumnGap => 0;

	public Margin Margin => new(0, 0, 0, 0);

	public Padding Padding => new(0, 0, 0, 0);

	public IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> OrderedCells => _cells;

	/// <summary>
	/// The window root fills its allocation; it never self-sizes the body row to content. The
	/// star row is the viewport, and a viewport that grew to fit its content would not scroll.
	/// </summary>
	public bool StarTracksSelfSizeToContentInMeasure => false;

	#endregion

	#region ILayoutContainer

	public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
	{
		BuildTracks(node);
		return _grid.MeasureChildren(node, constraints);
	}

	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		BuildTracks(node);
		_grid.ArrangeChildren(node, finalRect);
		RecordBands(node, finalRect);
	}

	#endregion

	#region Sticky band geometry (for clipping)

	/// <summary>Total height of the sticky-top band, as last arranged.</summary>
	public int StickyTopHeight { get; private set; }

	/// <summary>Total height of the sticky-bottom band, as last arranged.</summary>
	public int StickyBottomHeight { get; private set; }

	/// <summary>Top edge of the scrolling body region, as last arranged.</summary>
	public int ScrollableTop { get; private set; }

	/// <summary>Bottom edge of the scrolling body region, as last arranged.</summary>
	public int ScrollableBottom { get; private set; }

	/// <summary>
	/// Clips each child to its own band, so body content cannot paint over a sticky header or
	/// footer. Deliberately mirrors <see cref="WindowContentLayout.GetPaintClipRect"/> rather than
	/// deriving the rectangles from the grid's own track bounds: the comparison this layout has to
	/// pass is against that method's output, so any difference in rounding or clamping would show
	/// up as a colour bleed in the golden buffers.
	/// </summary>
	public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
	{
		var sticky = child.Control?.StickyPosition ?? StickyPosition.None;

		switch (sticky)
		{
			case StickyPosition.Top:
				return new LayoutRect(
					parentClipRect.X,
					parentClipRect.Y,
					parentClipRect.Width,
					Math.Min(StickyTopHeight, parentClipRect.Height));

			case StickyPosition.Bottom:
				int bottomStartY = Math.Max(parentClipRect.Y, ScrollableBottom);
				return new LayoutRect(
					parentClipRect.X,
					bottomStartY,
					parentClipRect.Width,
					Math.Max(0, parentClipRect.Bottom - bottomStartY));

			default:
				int startY = Math.Max(parentClipRect.Y, ScrollableTop);
				int endY = Math.Min(parentClipRect.Bottom, ScrollableBottom);
				return new LayoutRect(
					parentClipRect.X,
					startY,
					parentClipRect.Width,
					Math.Max(0, endY - startY));
		}
	}

	#endregion

	/// <summary>
	/// Derives the row tracks and cell placements from the current children.
	/// </summary>
	/// <remarks>
	/// One Auto row per sticky child, in child order, so a band of several controls keeps its
	/// internal ordering and each sizes to its own content — which is what the old layout's
	/// running <c>currentStickyTopY</c> accumulator produced. A single shared Auto row would size
	/// every control in the band to the tallest, which is a different layout.
	///
	/// All non-sticky children share the one star row and are stacked within it. The grid places
	/// them in the same cell; their vertical stacking inside that cell is the body's own concern
	/// and is what the ScrollLayout will own.
	/// </remarks>
	private void BuildTracks(LayoutNode node)
	{
		_rows.Clear();
		_cells.Clear();

		var top = new List<IWindowControl>();
		var body = new List<IWindowControl>();
		var bottom = new List<IWindowControl>();

		foreach (var child in node.Children)
		{
			if (!child.IsVisible || child.Control == null)
				continue;

			switch (child.Control.StickyPosition)
			{
				case StickyPosition.Top: top.Add(child.Control); break;
				case StickyPosition.Bottom: bottom.Add(child.Control); break;
				default: body.Add(child.Control); break;
			}
		}

		foreach (var c in top)
		{
			_cells.Add((c, new GridPlacement(_rows.Count, 0)));
			_rows.Add(GridLength.Auto());
		}

		_bodyRowIndex = _rows.Count;
		_rows.Add(GridLength.Star(1));
		foreach (var c in body)
			_cells.Add((c, new GridPlacement(_bodyRowIndex, 0)));

		foreach (var c in bottom)
		{
			_cells.Add((c, new GridPlacement(_rows.Count, 0)));
			_rows.Add(GridLength.Auto());
		}
	}

	/// <summary>
	/// Reads the arranged band geometry back off the children, for <see cref="GetPaintClipRect"/>.
	/// Taken from the children's real arranged bounds rather than from the track sizes, so the
	/// clip rectangles can never disagree with where the controls actually landed.
	/// </summary>
	private void RecordBands(LayoutNode node, LayoutRect finalRect)
	{
		int topHeight = 0;
		int bottomHeight = 0;

		foreach (var child in node.Children)
		{
			if (!child.IsVisible || child.Control == null)
				continue;

			switch (child.Control.StickyPosition)
			{
				case StickyPosition.Top: topHeight += child.DesiredSize.Height; break;
				case StickyPosition.Bottom: bottomHeight += child.DesiredSize.Height; break;
			}
		}

		StickyTopHeight = topHeight;
		StickyBottomHeight = bottomHeight;
		ScrollableTop = topHeight;
		ScrollableBottom = finalRect.Height - bottomHeight;
	}
}
