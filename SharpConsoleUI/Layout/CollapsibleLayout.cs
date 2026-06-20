// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Layout algorithm for <see cref="Controls.CollapsiblePanel"/>. Reserves a header region
	/// at the top of the panel and stacks the visible body children vertically beneath it.
	/// When the panel is collapsed its children are invisible and contribute no height;
	/// when expanded the body height is capped by <see cref="Controls.CollapsiblePanel.MaxContentHeight"/>.
	/// </summary>
	public class CollapsibleLayout : ILayoutContainer
	{
		private static SharpConsoleUI.Controls.CollapsiblePanel? Panel(LayoutNode node)
			=> node.Control as SharpConsoleUI.Controls.CollapsiblePanel;

		/// <summary>
		/// Measures the visible body children below the reserved header region and returns the
		/// total desired size (header + capped body + margins).
		/// </summary>
		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
		{
			var panel = Panel(node);
			var margin = panel?.Margin ?? new SharpConsoleUI.Controls.Margin(0, 0, 0, 0);
			int headerH = panel?.HeaderHeight ?? 1;
			bool bordered = panel?.IsBordered ?? false;
			int marginH = margin.Top + margin.Bottom;
			int marginW = margin.Left + margin.Right;

			// Bug B: the bordered box adds a bottom-border row beneath the body and consumes one
			// column on each side. Account for both so the reported size matches the painted frame.
			int sideInset = bordered ? 1 : 0;
			int bottomBorderRows = bordered ? 1 : 0;
			// Body padding (default 0) insets content inside the border, on all four sides.
			var padding = panel?.Padding ?? new SharpConsoleUI.Layout.Padding(0, 0, 0, 0);
			int paddingW = padding.Left + padding.Right;
			int paddingH = padding.Top + padding.Bottom;
			int overhead = headerH + marginH + bottomBorderRows + paddingH;
			int chromeW = marginW + (sideInset * 2) + paddingW;

			int availW = Math.Max(0, constraints.MaxWidth - chromeW);
			int availH = Math.Max(0, constraints.MaxHeight - overhead);
			var childConstraints = new LayoutConstraints(
				Math.Max(0, constraints.MinWidth - chromeW), availW, 0, availH);

			// A collapsed panel contributes no body height — independent of each child's own Visible
			// (so the panel does NOT clobber a caller's child.Visible just to hide the body).
			bool expanded = panel?.IsExpanded ?? true;
			int maxW = 0, sumH = 0;
			if (expanded)
			{
				foreach (var child in node.Children)
				{
					var size = child.Measure(childConstraints); // a child's own Visible=false still measures to zero
					maxW = Math.Max(maxW, size.Width);
					sumH += size.Height;
				}
			}

			int cap = panel?.MaxContentHeight ?? int.MaxValue;
			int bodyH = Math.Min(sumH, cap);
			if (panel?.AnimatedBodyHeight is int animMeasure)
				bodyH = Math.Min(bodyH, animMeasure); // honor in-progress height animation
													  // When the panel has no explicit Width, it should occupy the width it is ALLOCATED by
													  // the host rather than shrinking to its (possibly invisible/collapsed) content. In a
													  // bounded-width slot (a flex/Fill grid column, a fixed-width container) MaxWidth is a
													  // real ceiling, so fill it — this keeps the collapsed header full-width and stops the
													  // grid from re-flexing on collapse. In an UNBOUNDED/auto-width host (MaxWidth is
													  // int.MaxValue or 0) there is no allocated width to fill, so fall back to content width.
			bool boundedWidth = constraints.MaxWidth > 0 && constraints.MaxWidth != int.MaxValue;
			int resolvedW = panel?.Width
				?? (boundedWidth
					? Math.Max(maxW + chromeW, constraints.MaxWidth)
					: Math.Max(maxW + chromeW, constraints.MinWidth));
			return new LayoutSize(resolvedW, overhead + bodyH);
		}

		/// <summary>
		/// Arranges the visible body children stacked below the reserved header region,
		/// clamped to the available (and optionally capped) body height. Invisible children
		/// are collapsed to an empty rectangle.
		/// </summary>
		public void ArrangeChildren(LayoutNode node, LayoutRect bounds)
		{
			var panel = Panel(node);
			var margin = panel?.Margin ?? new SharpConsoleUI.Controls.Margin(0, 0, 0, 0);
			int headerH = panel?.HeaderHeight ?? 1;
			bool bordered = panel?.IsBordered ?? false;

			// Bug B: in the bordered style the box frames the body — inset the body by the left
			// and right side borders (1 column each) and reserve one row at the bottom for the
			// bottom border. Borderless body is not inset (unchanged behavior).
			int sideInset = bordered ? 1 : 0;
			int bottomBorderRows = bordered ? 1 : 0;
			// Body padding (default 0) insets content inside the border, on all four sides.
			var padding = panel?.Padding ?? new SharpConsoleUI.Layout.Padding(0, 0, 0, 0);

			int contentTop = margin.Top + headerH + padding.Top;
			int contentLeft = bounds.X + margin.Left + sideInset + padding.Left;
			int contentWidth = Math.Max(0, bounds.Width - margin.Left - margin.Right - (sideInset * 2) - padding.Left - padding.Right);
			int cap = panel?.MaxContentHeight ?? int.MaxValue;

			int regionH = Math.Min(Math.Max(0, bounds.Height - contentTop - margin.Bottom - bottomBorderRows - padding.Bottom), cap);

			// Bug A: the incoming bounds is the panel's FULL arranged height (the window content
			// layout makes the whole panel scrollable and gives it its full desired height), so it
			// is NOT a screen bound. Clamp the body region to the real on-screen space — the root
			// layout node's arranged height (the window content viewport). This is the absolute
			// ceiling of visible rows; a body child taller than it gets a viewport < its content
			// and scrolls on its own. When the panel is nested inside an already-bounded container
			// (a Fill TabControl, an inner ScrollablePanel), bounds.Height is already smaller, so
			// the Math.Min below keeps the existing, tighter bound.
			int onScreenBodyCeiling = OnScreenBodyCeiling(node, contentTop, bottomBorderRows);
			if (onScreenBodyCeiling >= 0)
				regionH = Math.Min(regionH, onScreenBodyCeiling);

			if (panel?.AnimatedBodyHeight is int animRegion)
				regionH = Math.Min(regionH, animRegion); // clamp body to in-progress height animation

			// Publish the resolved body region height so children that query the container's
			// visible height (GetVisibleHeightForControl) get the real on-screen body viewport.
			panel?.SetArrangedBodyRegionHeight(regionH);

			int y = bounds.Y + contentTop;
			int bottomLimit = bounds.Y + contentTop + regionH;

			// Collapsed → arrange every body child to nothing (panel hides the body without touching
			// each child's own Visible). Expanded → a child's own Visible=false still hides just it.
			bool expanded = panel?.IsExpanded ?? true;
			foreach (var child in node.Children)
			{
				if (!expanded || !child.IsVisible)
				{
					child.Arrange(new LayoutRect(0, 0, 0, 0));
					continue;
				}

				int childH = child.DesiredSize.Height;
				int available = Math.Max(0, bottomLimit - y);
				int h = Math.Min(childH, available);
				child.Arrange(new LayoutRect(contentLeft, y, contentWidth, h));
				y += h;
			}
		}

		/// <summary>
		/// Computes the maximum number of on-screen rows available to the body, derived from the
		/// root layout node's arranged height (the window content viewport). Returns -1 when no
		/// usable ceiling can be determined (so the caller leaves the region unclamped).
		/// </summary>
		/// <remarks>
		/// The arrange pass runs top-down, so by the time this panel's children are arranged the
		/// root node already has its <see cref="LayoutNode.Bounds"/> set. The root's height is the
		/// visible content area of the window; the body cannot usefully exceed it. We subtract the
		/// header rows and any reserved bottom-border row so the returned value is a body ceiling.
		/// </remarks>
		private static int OnScreenBodyCeiling(LayoutNode node, int contentTop, int bottomBorderRows)
		{
			var root = node;
			while (root.Parent != null)
				root = root.Parent;

			int rootHeight = root.Bounds.Height;
			if (rootHeight <= 0)
				return -1; // root not arranged yet (e.g. detached measure) — leave unclamped

			return Math.Max(0, rootHeight - contentTop - bottomBorderRows);
		}
	}
}
