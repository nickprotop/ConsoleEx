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
/// A window root that allocates edges: each docked child claims a strip from the remaining
/// rectangle in declaration order, and everything undocked shares a scrolling body in the middle.
/// </summary>
/// <remarks>
/// <para>
/// <b>This subsumes the banded model.</b> <see cref="StickyPosition.Top"/> is
/// <see cref="Dock.Top"/> and <see cref="StickyPosition.Bottom"/> is <see cref="Dock.Bottom"/> -
/// not approximately, but by the same allocation rule: sticky children already consumed rows off
/// the ends of a shrinking middle, in child order, which is precisely what docking does. The
/// difference is only that sticky could name two edges and this can name four.
/// </para>
/// <para>
/// Which is why there is one root here and not two. A separate grid-banded root and dock root
/// would be the same algorithm restricted to a subset of its edges.
/// </para>
/// <para>
/// The body is a synthetic node running a <see cref="ScrollLayout"/> with this instance as its
/// <see cref="IScrollOrigin"/>, for the same reason it was in the banded root: a docked region
/// clamps its child to the strip, while a scrolling body must let content overflow, because that
/// overflow is what there is to scroll.
/// </para>
/// </remarks>
public sealed class DockRootLayout : IWindowRootLayout, IScrollOrigin
{
	private readonly ScrollLayout _bodyLayout;
	private readonly LayoutNode _bodyNode;
	private readonly BodyMarker _bodyMarker = new();

	/// <summary>Creates a root with an empty body and no docked children.</summary>
	public DockRootLayout()
	{
		_bodyLayout = new ScrollLayout(this);
		_bodyNode = new LayoutNode(_bodyMarker, _bodyLayout);
	}

	/// <summary>
	/// Reads the edge a child claims. Defaults to deriving it from
	/// <see cref="IWindowControl.StickyPosition"/>, so existing applications keep working
	/// unchanged under this root.
	/// </summary>
	public static Func<IWindowControl, Dock> DockOf { get; set; } = FromSticky;

	private static Dock FromSticky(IWindowControl control) => control.StickyPosition switch
	{
		StickyPosition.Top => Dock.Top,
		StickyPosition.Bottom => Dock.Bottom,
		_ => Dock.None,
	};

	#region IScrollOrigin

	/// <summary>Rows the body is scrolled down by.</summary>
	public int ScrollOffset { get; set; }

	int IScrollOrigin.VerticalScrollOffset => ScrollOffset;

	/// <summary>The window root does not scroll horizontally.</summary>
	public int HorizontalScrollOffset => 0;

	/// <summary>False: the root clips horizontally rather than scrolling.</summary>
	public bool HorizontalScrollEnabled => false;

	/// <summary>Unused while <see cref="HorizontalScrollEnabled"/> is false.</summary>
	public int TotalContentWidth => 0;

	/// <summary>The visible height of the body region.</summary>
	public int ViewportHeight { get; private set; }

	#endregion

	#region Scroll range

	/// <summary>The body's full content height.</summary>
	public int ScrollableContentHeight => _bodyLayout.ContentHeight;

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

	#region Body region, recorded for clipping

	private LayoutRect _bodyRect;

	#endregion

	#region ILayoutContainer

	/// <summary>
	/// Measures docked children against the space left by the ones before them, then the body
	/// against what remains.
	/// </summary>
	public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
	{
		Reband(node);

		int remainingW = constraints.MaxWidth;
		int remainingH = constraints.MaxHeight;

		foreach (var child in node.Children)
		{
			if (ReferenceEquals(child, _bodyNode) || !child.IsVisible || child.Control == null)
				continue;

			var dock = DockOf(child.Control);
			var size = child.Measure(new LayoutConstraints(0, remainingW, 0, remainingH));

			switch (dock)
			{
				case Dock.Left:
				case Dock.Right:
					remainingW = Math.Max(0, remainingW - size.Width);
					break;
				case Dock.Top:
				case Dock.Bottom:
					remainingH = Math.Max(0, remainingH - size.Height);
					break;
			}
		}

		ViewportHeight = remainingH;
		_bodyNode.Measure(new LayoutConstraints(0, remainingW, 0, remainingH));

		return new LayoutSize(constraints.MaxWidth, constraints.MaxHeight);
	}

	/// <summary>
	/// Arranges each docked child against a shrinking rectangle, in declaration order, then gives
	/// the body whatever is left.
	/// </summary>
	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		Reband(node);

		int x = 0, y = 0;
		int w = finalRect.Width, h = finalRect.Height;

		// Bottom and right docks are arranged in REVERSE declaration order, so that the first
		// child declared ends up nearest the content rather than nearest the edge.
		//
		// This departs from WPF's DockPanel, deliberately. Under the strict rule each bottom child
		// consumes the bottom edge in turn, so declaring a rule and then a help line puts the help
		// line ABOVE the rule - correct by the algorithm, backwards to anyone writing a footer top
		// to bottom. WPF users know to declare footers in reverse; the 90 existing StickyBottom
		// call sites in this repository do not, and ImageViewerWindow is exactly this shape.
		//
		// Reversing the consumption order for the far edges makes author order read the way the
		// screen reads on both axes, and makes Dock.Bottom a drop-in for StickyPosition.Bottom.
		// Reversal applies WITHIN each far edge only - a run of Bottom children is reversed among
		// themselves and stays where it was declared relative to the Left and Top docks. Moving
		// every far-edge child to the end instead would make declaration order meaningless across
		// axes: a status bar declared before a sidebar would still be processed after it, and so
		// would stop spanning the full width.
		var ordered = new List<LayoutNode>(node.Children.Count);
		var run = new List<LayoutNode>();
		Dock runEdge = Dock.None;

		void FlushRun()
		{
			for (int i = run.Count - 1; i >= 0; i--)
				ordered.Add(run[i]);
			run.Clear();
			runEdge = Dock.None;
		}

		foreach (var child in node.Children)
		{
			if (ReferenceEquals(child, _bodyNode))
				continue;

			var d = child.Control == null ? Dock.None : DockOf(child.Control);
			bool isFar = d == Dock.Bottom || d == Dock.Right;

			if (isFar && d == runEdge)
			{
				run.Add(child);
				continue;
			}

			FlushRun();

			if (isFar)
			{
				runEdge = d;
				run.Add(child);
			}
			else
			{
				ordered.Add(child);
			}
		}
		FlushRun();

		foreach (var child in ordered)
		{

			if (!child.IsVisible || child.Control == null)
			{
				child.Arrange(LayoutRect.Empty);
				continue;
			}

			var dock = DockOf(child.Control);
			int cw = child.DesiredSize.Width;
			int ch = child.DesiredSize.Height;

			switch (dock)
			{
				case Dock.Left:
					cw = Math.Min(cw, w);
					child.Arrange(new LayoutRect(x, y, cw, h));
					x += cw; w -= cw;
					break;

				case Dock.Right:
					cw = Math.Min(cw, w);
					child.Arrange(new LayoutRect(x + w - cw, y, cw, h));
					w -= cw;
					break;

				case Dock.Top:
					ch = Math.Min(ch, h);
					// Full window width, matching the banded root, where a sticky child spanned the
					// single Star column rather than the rectangle left by side docks.
					child.Arrange(new LayoutRect(x, y, HorizontalExtent(child, w), ch));
					y += ch; h -= ch;
					break;

				case Dock.Bottom:
					ch = Math.Min(ch, h);
					child.Arrange(new LayoutRect(x, y + h - ch, HorizontalExtent(child, w), ch));
					h -= ch;
					break;
			}
		}

		ViewportHeight = h;
		ScrollOffset = Math.Clamp(ScrollOffset, 0, MaxScrollOffset);

		_bodyRect = new LayoutRect(x, y, w, h);
		_bodyNode.Arrange(_bodyRect);
	}

	/// <summary>
	/// Width for a top- or bottom-docked child, honouring its horizontal alignment the way the
	/// window root always has: only a Stretch child without an explicit width fills the strip.
	/// </summary>
	private static int HorizontalExtent(LayoutNode child, int available)
	{
		var alignment = child.Control?.HorizontalAlignment ?? HorizontalAlignment.Stretch;
		if (alignment != HorizontalAlignment.Stretch)
			return child.DesiredSize.Width;
		if (child.Control?.Width.HasValue == true)
			return child.DesiredSize.Width;
		return available;
	}

	#endregion

	/// <summary>
	/// Clips a docked child to its own strip and the body to the body region, so body content
	/// cannot paint over a docked edge.
	/// </summary>
	public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
	{
		if (ReferenceEquals(child.Control, _bodyMarker))
		{
			int startY = Math.Max(parentClipRect.Y, _bodyRect.Y);
			int endY = Math.Min(parentClipRect.Bottom, _bodyRect.Bottom);
			return new LayoutRect(parentClipRect.X, startY, parentClipRect.Width, Math.Max(0, endY - startY));
		}

		// A docked child paints inside the strip it was arranged into.
		var b = child.Bounds;
		int cx = Math.Max(parentClipRect.X, b.X);
		int cy = Math.Max(parentClipRect.Y, b.Y);
		int cr = Math.Min(parentClipRect.Right, b.Right);
		int cb = Math.Min(parentClipRect.Bottom, b.Bottom);
		return new LayoutRect(cx, cy, Math.Max(0, cr - cx), Math.Max(0, cb - cy));
	}

	/// <summary>
	/// Moves every undocked child under the body node, leaving the root holding
	/// <c>[docked…, bodyNode]</c> in declaration order.
	/// </summary>
	/// <remarks>
	/// Only reshapes when the partition actually changed: <see cref="LayoutNode.ClearChildren"/>
	/// and <see cref="LayoutNode.AddChild"/> both invalidate measure, so rebuilding every pass
	/// would re-dirty the whole tree every frame and defeat paint-only invalidation.
	/// </remarks>
	private void Reband(LayoutNode node)
	{
		var docked = new List<LayoutNode>();
		var body = new List<LayoutNode>();

		foreach (var child in node.Children)
		{
			if (ReferenceEquals(child, _bodyNode))
			{
				foreach (var inner in child.Children)
					body.Add(inner);
				continue;
			}

			if (child.Control == null)
				continue;

			if (DockOf(child.Control) == Dock.None)
				body.Add(child);
			else
				docked.Add(child);
		}

		if (Unchanged(node, docked, body))
		{
			// Shape is right, but ClearChildren on an earlier pass may have severed a Parent link,
			// and Arrange derives AbsoluteBounds from Parent - a severed link silently places
			// content at the window origin. Reattach without touching the child lists.
			_bodyNode.Parent = node;
			foreach (var b in body)
				b.Parent = _bodyNode;
			return;
		}

		_bodyNode.ClearChildren();
		foreach (var b in body)
			_bodyNode.AddChild(b);

		node.ClearChildren();
		foreach (var d in docked)
			node.AddChild(d);
		node.AddChild(_bodyNode);
	}

	private bool Unchanged(LayoutNode node, List<LayoutNode> docked, List<LayoutNode> body)
	{
		if (node.Children.Count != docked.Count + 1)
			return false;

		for (int i = 0; i < docked.Count; i++)
		{
			if (!ReferenceEquals(node.Children[i], docked[i]))
				return false;
		}

		if (!ReferenceEquals(node.Children[docked.Count], _bodyNode))
			return false;

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
	/// Stands in for the body so the root has a node to arrange. Never painted, never in the
	/// window's control list.
	/// </summary>
	private sealed class BodyMarker : BaseControl
	{
		/// <summary>The body claims no intrinsic width; its children decide.</summary>
		public override int? ContentWidth => null;

		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
			=> new(constraints.MaxWidth, constraints.MaxHeight);

		/// <summary>Never paints; its children do.</summary>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color foreground, Color background)
		{
		}
	}
}
