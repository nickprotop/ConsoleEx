// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Layout algorithm for <see cref="Controls.ScrollablePanelControl"/>. Stacks the panel's
	/// visible children vertically inside a clipped content viewport and offsets them by the
	/// panel's scroll position, so the scroll offset flows into each child's
	/// <see cref="LayoutNode.AbsoluteBounds"/> via the standard <see cref="LayoutNode.Arrange"/>.
	/// </summary>
	/// <remarks>
	/// This layout DOES NOT own scroll state — the panel remains the single source of truth for the
	/// scroll offset, viewport size and scrollbar reservation. ScrollLayout only READS those values
	/// (via internal accessors) and reuses the panel's shared Fill helpers
	/// (<c>ComputeFillMetrics</c>/<c>ComputeChildHeight</c>) so the arranged layout is byte-identical
	/// to the panel's existing self-paint. It mirrors <see cref="CollapsibleLayout"/>'s shape: a
	/// tree-participating container that reads its control's state and arranges a clipped region.
	///
	/// The crux: <see cref="ArrangeChildren"/> arranges each child at <c>y = -verticalOffset + cumH</c>;
	/// because <see cref="LayoutNode.Arrange"/> computes
	/// <c>AbsoluteBounds.Y = Parent.AbsoluteBounds.Y + finalRect.Y</c>, the negative scroll offset
	/// flows straight into the child's absolute bounds — no SPC-specific hit-test/cursor override
	/// is needed.
	/// </remarks>
	public class ScrollLayout : ILayoutContainer, IRegionClippingLayout
	{
		private static SharpConsoleUI.Controls.ScrollablePanelControl? Panel(LayoutNode node)
			=> node.Control as SharpConsoleUI.Controls.ScrollablePanelControl;

		/// <summary>
		/// Measures the visible children at the content width and returns the VIEWPORT size as the
		/// node's desired size (NOT the full content extent). The panel computes the full content
		/// extent internally for scrollbar math; the layout slot only ever occupies the viewport.
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{
			var panel = Panel(node);
			if (panel == null)
				return LayoutSize.Zero;

			// Register the persistent child nodes so the panel's content-height/Fill math reuses the
			// already-built subtrees (node.Children) instead of rebuilding throwaway subtrees per call.
			// Restored in finally so nested/overlapping passes are not disturbed.
			var previousResolver = panel.SetChildNodeResolver(BuildChildNodeResolver(node));
			try
			{
				return MeasureChildrenCore(node, constraints, panel);
			}
			finally
			{
				panel.SetChildNodeResolver(previousResolver);
			}
		}

		private LayoutSize MeasureChildrenCore(LayoutNode node, LayoutConstraints constraints, SharpConsoleUI.Controls.ScrollablePanelControl panel)
		{
			// Determine the panel's OUTER box. The window layout measures the panel with bounded
			// constraints when it dictates the slot (e.g. an explicit window region), but with an
			// UNBOUNDED MaxHeight/MaxWidth when the panel should auto-size to its content (the common
			// case for a panel with no explicit Height). In the unbounded case we must reproduce the
			// panel's own content-based auto-sizing — delegating to its MeasureDOM, which is the single
			// source of truth for that calculation — otherwise the panel would measure to zero height
			// (constraints.MinHeight) and never be arranged or painted.
			//
			// PERF: MeasureDOM runs the full O(children) content-height traversal. Its result is ONLY
			// consumed by the unbounded branches below (the bounded branches use the constraint extents
			// verbatim). So call it ONLY when a dimension is actually unbounded — in the common bounded
			// case (the window dictated a finite slot) it is pure waste that doubles the measure cost
			// (ResolveContentMetrics below performs the authoritative content measurement either way).
			bool widthUnbounded = constraints.IsWidthEffectivelyUnbounded;
			bool heightUnbounded = constraints.IsHeightEffectivelyUnbounded;

			// CRITICAL: when the incoming constraint is UNBOUNDED (int.MaxValue), the panel falls
			// back to its content-based desired size. That desired size must NEVER be allowed to
			// carry an unbounded/near-int.MaxValue value into the node's arranged bounds: a child
			// measured at int.MaxValue (e.g. a Fill child inside a nested ColumnContainer receiving
			// MaxHeight - fixed ≈ 2 billion) would otherwise make this node's Bottom hundreds of
			// millions, and an ancestor's per-row background-fill loop would iterate that many times
			// (an effective hang). The returned size IS the node's slot; clamp the unbounded fallback
			// to a finite, on-screen-sane bound. Bounded constraints are used verbatim (the window
			// layout already dictated a real finite slot).
			int outerW, outerH;
			if (widthUnbounded || heightUnbounded)
			{
				var desired = panel.MeasureDOM(constraints);
				outerW = widthUnbounded
					? Math.Min(desired.Width, LayoutDefaults.MaxSafeRenderWidth)
					: constraints.MaxWidth;
				outerH = heightUnbounded
					? Math.Min(desired.Height, LayoutDefaults.MaxUnboundedMeasureHeight)
					: constraints.MaxHeight;
			}
			else
			{
				outerW = constraints.MaxWidth;
				outerH = constraints.MaxHeight;
			}

			// Resolve the content region for the allocated outer box. This populates the panel's
			// viewport/content fields so the Fill helpers below and the arrange pass see consistent metrics.
			//
			// CRITICAL: this is a MEASURE pass, so it must NEVER clamp the persisted scroll offset — only
			// the arrange/paint passes (which receive the panel's REAL on-screen box) may clamp. The outer
			// box handed in here is whatever extent the parent allocates while sizing, which is NOT
			// guaranteed to be the visible viewport:
			//   • Unbounded constraint → the panel auto-sized to its content, so the derived viewport equals
			//     the content extent and the offset-max collapses to 0 — clamping would wipe the user's
			//     scroll on every re-layout (the original wheel-doesn't-scroll bug).
			//   • Bounded constraint from a container (e.g. a GridControl cell allotted its star-row height)
			//     can still exceed the window-clipped on-screen viewport. Clamping against that taller box
			//     caps the scroll partway: maxOffset = content − cellExtent instead of content − viewport,
			//     so the panel stops short of its real end (the SPC-in-grid-cell scroll-cap bug).
			// Arrange (ArrangeChildrenCore → ResolveContentMetrics(finalRect)) clamps against the true box,
			// which is both correct and sufficient, so suppress the clamp here unconditionally.
			var content = panel.ResolveContentMetrics(new LayoutRect(0, 0, outerW, outerH), clampOffsets: false);

			int contentWidth = content.Width;

			// Measure each visible child so its DesiredSize is current (the panel's content-height
			// math already ran inside ResolveContentMetrics; this primes the child nodes that the
			// arrange pass reads). Fill children measure against their per-Fill slot.
			var (_, _, perFillHeight) = panel.ComputeFillMetrics(panel.Children, contentWidth, content.Height);
			int childLayoutWidth = ChildLayoutWidth(panel, contentWidth);

			foreach (var child in node.Children)
			{
				if (!child.IsVisible)
				{
					child.Measure(LayoutConstraints.Fixed(0, 0));
					continue;
				}

				bool isFill = content.Height > 0 && child.Control?.VerticalAlignment == VerticalAlignment.Fill;
				int maxChildHeight = isFill ? perFillHeight : int.MaxValue;
				child.Measure(new LayoutConstraints(1, childLayoutWidth, 1, maxChildHeight));
			}

			// The slot the panel occupies is the full outer box (viewport + chrome). The window
			// layout engine sized it; we report that size back so the node is not resized smaller
			// than its allocation (matching the panel's own MeasureDOM, which returns the outer box).
			return new LayoutSize(outerW, outerH);
		}

		/// <summary>
		/// Arranges the visible children stacked vertically from <c>y = -verticalScrollOffset</c>
		/// (and <c>x = -horizontalScrollOffset</c>), each at its full measured height, within the
		/// content region. Off-viewport children are arranged too (correct bounds; paint culls them).
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
		{
			var panel = Panel(node);
			if (panel == null)
				return;

			var previousResolver = panel.SetChildNodeResolver(BuildChildNodeResolver(node));
			try
			{
				ArrangeChildrenCore(node, finalRect, panel);
			}
			finally
			{
				panel.SetChildNodeResolver(previousResolver);
			}
		}

		private void ArrangeChildrenCore(LayoutNode node, LayoutRect finalRect, SharpConsoleUI.Controls.ScrollablePanelControl panel)
		{
			var content = panel.ResolveContentMetrics(finalRect);
			int contentWidth = content.Width;
			int contentViewportHeight = content.Height;
			int contentOriginX = content.X;
			int contentOriginY = content.Y;

			var (_, _, perFillHeight) = panel.ComputeFillMetrics(panel.Children, contentWidth, content.Height);
			int childLayoutWidth = ChildLayoutWidth(panel, contentWidth);

			int childX = contentOriginX - panel.HorizontalScrollOffsetInternal;
			int currentY = contentOriginY - panel.VerticalScrollOffsetInternal;

			foreach (var child in node.Children)
			{
				if (!child.IsVisible || child.Control == null)
				{
					child.Arrange(LayoutRect.Empty);
					continue;
				}

				bool isFill = contentViewportHeight > 0 && child.Control.VerticalAlignment == VerticalAlignment.Fill;
				int childHeight = isFill
					? Math.Max(child.DesiredSize.Height, perFillHeight)
					: child.DesiredSize.Height;

				// Respect explicit Width on child controls (mirrors PaintDOM).
				int childWidth = (child.Control.Width.HasValue && child.Control.Width.Value < childLayoutWidth)
					? child.Control.Width.Value
					: childLayoutWidth;

				// Arrange relative to the node (finalRect is node-local; LayoutNode.Arrange adds the
				// node's AbsoluteBounds), so the negative scroll offset flows into AbsoluteBounds.
				child.Arrange(new LayoutRect(childX, currentY, childWidth, childHeight));

				currentY += childHeight;
			}
		}

		/// <summary>
		/// Restricts a child's paint area to the panel's content viewport (inside the scrollbar
		/// chrome), intersected with the parent clip. Scrolled-out rows are clipped away.
		/// </summary>
		public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
		{
			var panel = child.Parent != null ? Panel(child.Parent) : null;
			if (panel == null)
				return parentClipRect;

			var node = child.Parent!;
			var content = panel.ResolveContentMetrics(node.AbsoluteBounds.WithPosition(0, 0));

			// content.X / content.Y are relative to the node's outer box; offset by the node's
			// absolute origin to get the screen-space viewport rect.
			var viewportRect = new LayoutRect(
				node.AbsoluteBounds.X + content.X,
				node.AbsoluteBounds.Y + content.Y,
				content.Width,
				content.Height);

			return parentClipRect.Intersect(viewportRect);
		}

		/// <summary>
		/// The width children are laid out at. With horizontal scrolling enabled, children use their
		/// full content width so the overflow exists to scroll through (the viewport clip trims it);
		/// otherwise they are constrained to the visible content width. Mirrors PaintDOM.
		/// </summary>
		private static int ChildLayoutWidth(SharpConsoleUI.Controls.ScrollablePanelControl panel, int contentWidth)
		{
			return panel.HorizontalScrollEnabledInternal
				? Math.Max(contentWidth, panel.TotalContentWidthInternal)
				: contentWidth;
		}

		/// <summary>
		/// Builds a control→node lookup over the layout node's persistent children, so the panel's
		/// content-height/Fill helpers can measure the already-built child subtree instead of rebuilding
		/// a throwaway one. Each child control maps to its real <see cref="LayoutNode"/>.
		/// </summary>
		private static Func<SharpConsoleUI.Controls.IWindowControl, LayoutNode?> BuildChildNodeResolver(LayoutNode node)
		{
			Dictionary<SharpConsoleUI.Controls.IWindowControl, LayoutNode>? map = null;
			foreach (var child in node.Children)
			{
				if (child.Control == null)
					continue;
				map ??= new Dictionary<SharpConsoleUI.Controls.IWindowControl, LayoutNode>();
				map[child.Control] = child;
			}

			return ctrl => (map != null && map.TryGetValue(ctrl, out var n)) ? n : null;
		}
	}
}
