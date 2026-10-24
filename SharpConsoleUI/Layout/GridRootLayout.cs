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
/// children occupy Auto rows at the bottom, and every other child lives in a scrolling body that
/// fills one <see cref="GridUnitType.Star"/> row between them.
/// </summary>
/// <remarks>
/// <para>
/// Experimental replacement for <see cref="WindowContentLayout"/>, the last self-scrolling
/// container in the codebase. It exists to answer one question: can sticky be reproduced
/// byte-identically as grid placement? Until that is answered it runs <em>beside</em> the old
/// layout, never instead of it.
/// </para>
/// <para>
/// <b>This is the only <see cref="ILayoutContainer"/> that owns tree shape.</b> Every other layout
/// arranges the children it is handed; this one inserts a synthetic body node between the root and
/// its non-sticky children. That is deliberate and is what the banding requires:
/// </para>
/// <list type="bullet">
/// <item>A grid cell clamps its content to the track and does not stack several controls. A
/// scrolling body must do the opposite — stack its children and let them overflow, because that
/// overflow is precisely what there is to scroll through.</item>
/// <item><see cref="GridLayout"/> pairs <c>node.Children[i]</c> to <c>OrderedCells[i]</c> by index
/// (see <see cref="IGridSource"/>). Relaxing that to pair by control identity does not help: the
/// body node is synthetic and has no control, so no identity could resolve it. Owning the tree is
/// the honest fix; changing the grid contract would only move the special case.</item>
/// </list>
/// <para>
/// The body node is structural and invisible to callers. <c>Window.Controls</c> still returns the
/// flat author-order list, and <see cref="LayoutNode.FindByControl"/> recurses, so a control inside
/// the body is found exactly as before.
/// </para>
/// </remarks>
public sealed class GridRootLayout : IWindowRootLayout, IGridSource, IScrollOrigin
{
	private readonly GridLayout _grid;

	/// <summary>The scroll layout driving the body, with this instance as its scroll origin.</summary>
	private readonly ScrollLayout _bodyLayout;

	/// <summary>
	/// The synthetic node occupying the star row. Persistent across passes so the body's children
	/// keep their measured state; its child list is rebuilt whenever the banding changes.
	/// </summary>
	private readonly LayoutNode _bodyNode;

	/// <summary>
	/// A placeholder control standing in for the body in <see cref="OrderedCells"/>. The grid
	/// requires a control per cell to correlate against; the body is structural, so it carries a
	/// marker that is never rendered and never appears in the window's control list.
	/// </summary>
	private readonly BodyMarker _bodyMarker = new();

	// Rebuilt on every measure and arrange from the live child list: a child's StickyPosition and
	// Visible can both change between passes, and caching them would desync exactly the way the
	// pre-b310f0d5 ScrollablePanel did.
	private readonly List<GridLength> _rows = new();
	private readonly List<(IWindowControl Control, GridPlacement Placement)> _cells = new();

	/// <summary>Creates a root layout with an empty body and no sticky bands.</summary>
	public GridRootLayout()
	{
		_grid = new GridLayout(this);
		_bodyLayout = new ScrollLayout(this);
		_bodyNode = new LayoutNode(_bodyMarker, _bodyLayout);
	}

	#region IGridSource

	/// <summary>One Auto row per sticky child, with a single Star row for the body between them.</summary>
	public IReadOnlyList<GridLength> RowDefinitions => _rows;

	/// <summary>A window root is one column: children stretch across the full width.</summary>
	public IReadOnlyList<GridLength> ColumnDefinitions { get; } = new[] { GridLength.Star(1) };

	/// <summary>
	/// No gaps. <see cref="WindowContentLayout"/> stacks children edge to edge; any gap would
	/// shift every row below it and fail the parity comparison immediately.
	/// </summary>
	public int RowGap => 0;

	/// <summary>Zero: a single column has nothing to gap against.</summary>
	public int ColumnGap => 0;

	/// <summary>None. The window frame already insets the content area.</summary>
	public Margin Margin => new(0, 0, 0, 0);

	/// <summary>None, matching the old layout, which stacked children against the content edge.</summary>
	public Padding Padding => new(0, 0, 0, 0);

	/// <summary>The banded cells, in grid-row order: sticky top, then the body, then sticky bottom.</summary>
	public IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> OrderedCells => _cells;

	/// <summary>
	/// The root fills its allocation and never self-sizes the body to content: the star row is the
	/// viewport, and a viewport that grew to fit its content would not scroll.
	/// </summary>
	public bool StarTracksSelfSizeToContentInMeasure => false;

	#endregion

	#region IScrollOrigin

	/// <summary>
	/// Rows the body is scrolled down by. <see cref="IScrollOrigin.VerticalScrollOffset"/> reads
	/// this; the name <c>ScrollOffset</c> is what the window and its renderer have always called it.
	/// </summary>
	public int ScrollOffset { get; set; }

	int IScrollOrigin.VerticalScrollOffset => ScrollOffset;

	/// <summary>The window root does not scroll horizontally, matching the old layout.</summary>
	public int HorizontalScrollOffset => 0;

	/// <summary>False: the window root clips horizontally rather than scrolling.</summary>
	public bool HorizontalScrollEnabled => false;

	/// <summary>Unused while <see cref="HorizontalScrollEnabled"/> is false.</summary>
	public int TotalContentWidth => 0;

	#endregion

	#region Window scroll metrics

	/// <summary>The body's full content height — what the old layout called ScrollableContentHeight.</summary>
	public int ScrollableContentHeight => _bodyLayout.ContentHeight;

	/// <summary>The visible height of the body region.</summary>
	public int ViewportHeight => Math.Max(0, ScrollableBottom - ScrollableTop);

	/// <summary>The largest valid <see cref="ScrollOffset"/>.</summary>
	public int MaxScrollOffset => Math.Max(0, ScrollableContentHeight - ViewportHeight);

	/// <summary>Whether the body overflows its viewport.</summary>
	public bool CanScroll => ScrollableContentHeight > ViewportHeight;

	/// <summary>Scrolls by <paramref name="delta"/> rows, clamped to the valid range.</summary>
	public void ScrollBy(int delta) => ScrollOffset = Math.Clamp(ScrollOffset + delta, 0, MaxScrollOffset);

	/// <summary>Scrolls to the first row of the body.</summary>
	public void ScrollToTop() => ScrollOffset = 0;

	/// <summary>Scrolls to the last row of the body.</summary>
	public void ScrollToBottom() => ScrollOffset = MaxScrollOffset;

	/// <summary>Scrolls up by one viewport height.</summary>
	public void PageUp() => ScrollBy(-ViewportHeight);

	/// <summary>Scrolls down by one viewport height.</summary>
	public void PageDown() => ScrollBy(ViewportHeight);

	#endregion

	#region ILayoutContainer

	/// <summary>Rebands the children, then measures them as grid cells.</summary>
	public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
	{
		Reband(node);
		return _grid.MeasureChildren(node, constraints);
	}

	/// <summary>Rebands the children, clamps the scroll offset, then arranges the grid.</summary>
	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		Reband(node);
		ScrollOffset = Math.Clamp(ScrollOffset, 0, MaxScrollOffset);
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
	/// Clips each band to its own region so body content cannot paint over a sticky header or
	/// footer. Deliberately mirrors <see cref="WindowContentLayout.GetPaintClipRect"/> rather than
	/// deriving rectangles from the grid's track bounds: the comparison this layout must pass is
	/// against that method's output, so a difference in rounding or clamping would surface as a
	/// colour bleed in the golden buffers.
	/// </summary>
	public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
	{
		// The body node carries the marker, so its clip is the scrollable region; its own children
		// are clipped by the body's ScrollLayout.
		var sticky = ReferenceEquals(child.Control, _bodyMarker)
			? StickyPosition.None
			: child.Control?.StickyPosition ?? StickyPosition.None;

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
	/// Rebuilds the row tracks, the cell list and the body node's children from the root's current
	/// children.
	/// </summary>
	/// <remarks>
	/// One Auto row per sticky child, in child order, so a band of several controls keeps its
	/// ordering and each sizes to its own content — which is what the old layout's running
	/// <c>currentStickyTopY</c> accumulator produced. One shared Auto row per band would instead
	/// size every control in it to the tallest, a different layout.
	///
	/// Non-sticky children are moved under the body node rather than placed in the grid directly,
	/// and the root's own child list is reshaped to
	/// <c>[sticky-top…, bodyNode, sticky-bottom…]</c> so the grid's index correlation holds.
	/// </remarks>
	private void Reband(LayoutNode node)
	{
		var top = new List<LayoutNode>();
		var body = new List<LayoutNode>();
		var bottom = new List<LayoutNode>();

		foreach (var child in node.Children)
		{
			if (ReferenceEquals(child, _bodyNode))
			{
				// Already-banded tree: recover the body's children rather than losing them.
				foreach (var inner in child.Children)
					body.Add(inner);
				continue;
			}

			if (child.Control == null)
				continue;

			switch (child.Control.StickyPosition)
			{
				case StickyPosition.Top: top.Add(child); break;
				case StickyPosition.Bottom: bottom.Add(child); break;
				default: body.Add(child); break;
			}
		}

		// The track and cell lists are rebuilt every pass - they are plain data the grid reads, and
		// rebuilding them invalidates nothing.
		_rows.Clear();
		_cells.Clear();

		foreach (var c in top)
		{
			_cells.Add((c.Control!, new GridPlacement(_rows.Count, 0)));
			_rows.Add(GridLength.Auto());
		}

		int bodyRow = _rows.Count;
		_rows.Add(GridLength.Star(1));
		_cells.Add((_bodyMarker, new GridPlacement(bodyRow, 0)));

		foreach (var c in bottom)
		{
			_cells.Add((c.Control!, new GridPlacement(_rows.Count, 0)));
			_rows.Add(GridLength.Auto());
		}

		// The TREE, by contrast, is only rebuilt when the banding actually changed. ClearChildren
		// and AddChild each call InvalidateMeasure, so reshaping unconditionally on every measure
		// AND every arrange re-dirties the whole tree every frame and defeats paint-only
		// invalidation entirely - a real per-frame cost, not just a failing assertion. Caught by
		// PaintOnlyInvalidationTests, which assert a repaint performs exactly zero measures.
		if (BandingUnchanged(node, top, body, bottom))
		{
			// Shape is right, but a node's Parent may have been severed by a ClearChildren on an
			// earlier pass (ClearChildren nulls each child's Parent, and re-adding under the same
			// parent is what restores it). Arrange derives AbsoluteBounds from Parent, so a severed
			// link silently places the body's content at the window origin instead of below the
			// sticky band. Reattach without touching the child lists, so nothing is invalidated.
			_bodyNode.Parent = node;
			foreach (var b in body)
				b.Parent = _bodyNode;
			return;
		}

		_bodyNode.ClearChildren();
		foreach (var b in body)
			_bodyNode.AddChild(b);

		node.ClearChildren();
		foreach (var t in top)
			node.AddChild(t);
		node.AddChild(_bodyNode);
		foreach (var b in bottom)
			node.AddChild(b);
	}

	/// <summary>
	/// Records band geometry for <see cref="GetPaintClipRect"/>, read from the children's own
	/// desired sizes so the clip rectangles cannot disagree with where the controls landed.
	/// </summary>
	private void RecordBands(LayoutNode node, LayoutRect finalRect)
	{
		int topHeight = 0;
		int bottomHeight = 0;

		foreach (var child in node.Children)
		{
			if (!child.IsVisible || child.Control == null || ReferenceEquals(child.Control, _bodyMarker))
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

	/// <summary>
	/// Whether the root already holds exactly <c>[top…, bodyNode, bottom…]</c> with the body holding
	/// exactly <paramref name="body"/>, in order. Compared by node identity, so a child that merely
	/// changed size is unchanged here and keeps its measured state.
	/// </summary>
	private bool BandingUnchanged(
		LayoutNode node, List<LayoutNode> top, List<LayoutNode> body, List<LayoutNode> bottom)
	{
		if (node.Children.Count != top.Count + 1 + bottom.Count)
			return false;

		for (int i = 0; i < top.Count; i++)
		{
			if (!ReferenceEquals(node.Children[i], top[i]))
				return false;
		}

		if (!ReferenceEquals(node.Children[top.Count], _bodyNode))
			return false;

		for (int i = 0; i < bottom.Count; i++)
		{
			if (!ReferenceEquals(node.Children[top.Count + 1 + i], bottom[i]))
				return false;
		}

		if (_bodyNode.Children.Count != body.Count)
			return false;

		for (int i = 0; i < body.Count; i++)
		{
			if (!ReferenceEquals(_bodyNode.Children[i], body[i]))
				return false;
		}

		return true;
	}

	/// <summary>
	/// A control that exists only so the grid has something to correlate the body cell against.
	/// Never added to a window, never painted, never visible to callers.
	/// </summary>
	private sealed class BodyMarker : BaseControl
	{
		/// <summary>The body claims no intrinsic width; its children decide.</summary>
		public override int? ContentWidth => null;

		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
			=> new(constraints.MaxWidth, constraints.MaxHeight);

		/// <summary>
		/// Never paints. The body is a layout region; its children paint themselves, and anything
		/// drawn here would land underneath them.
		/// </summary>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color foreground, Color background)
		{
		}
	}
}
