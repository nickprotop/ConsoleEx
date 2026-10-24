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
public sealed class GridRootLayout : ILayoutContainer, IRegionClippingLayout, IGridSource, IScrollOrigin
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

	public GridRootLayout()
	{
		_grid = new GridLayout(this);
		_bodyLayout = new ScrollLayout(this);
		_bodyNode = new LayoutNode(_bodyMarker, _bodyLayout);
	}

	#region IGridSource

	public IReadOnlyList<GridLength> RowDefinitions => _rows;

	/// <summary>A window root is one column: children stretch across the full width.</summary>
	public IReadOnlyList<GridLength> ColumnDefinitions { get; } = new[] { GridLength.Star(1) };

	/// <summary>
	/// No gaps. <see cref="WindowContentLayout"/> stacks children edge to edge; any gap would
	/// shift every row below it and fail the parity comparison immediately.
	/// </summary>
	public int RowGap => 0;

	public int ColumnGap => 0;

	public Margin Margin => new(0, 0, 0, 0);

	public Padding Padding => new(0, 0, 0, 0);

	public IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> OrderedCells => _cells;

	/// <summary>
	/// The root fills its allocation and never self-sizes the body to content: the star row is the
	/// viewport, and a viewport that grew to fit its content would not scroll.
	/// </summary>
	public bool StarTracksSelfSizeToContentInMeasure => false;

	#endregion

	#region IScrollOrigin

	/// <summary>Rows the body is scrolled down by — the window's scroll offset.</summary>
	public int VerticalScrollOffset { get; set; }

	/// <summary>The window root does not scroll horizontally, matching the old layout.</summary>
	public int HorizontalScrollOffset => 0;

	public bool HorizontalScrollEnabled => false;

	public int TotalContentWidth => 0;

	#endregion

	#region Window scroll metrics

	/// <summary>The body's full content height — what the old layout called ScrollableContentHeight.</summary>
	public int ScrollableContentHeight => _bodyLayout.ContentHeight;

	/// <summary>The visible height of the body region.</summary>
	public int ViewportHeight => Math.Max(0, ScrollableBottom - ScrollableTop);

	/// <summary>The largest valid <see cref="VerticalScrollOffset"/>.</summary>
	public int MaxScrollOffset => Math.Max(0, ScrollableContentHeight - ViewportHeight);

	public bool CanScroll => ScrollableContentHeight > ViewportHeight;

	#endregion

	#region ILayoutContainer

	public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
	{
		Reband(node);
		return _grid.MeasureChildren(node, constraints);
	}

	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		Reband(node);
		VerticalScrollOffset = Math.Clamp(VerticalScrollOffset, 0, MaxScrollOffset);
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
