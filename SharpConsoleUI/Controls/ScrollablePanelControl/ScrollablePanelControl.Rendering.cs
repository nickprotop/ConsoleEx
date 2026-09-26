// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Calculate available width from constraints, not from stale _viewportWidth
			int width = Width ?? constraints.MaxWidth;
			int availableWidth = Math.Max(1, width - Margin.Left - Margin.Right - BorderWidth - _padding.Left - _padding.Right);

			// Determine height
			int height;
			if (_height.HasValue)
			{
				// Explicit height set - use it directly
				height = _height.Value;
			}
			else
			{
				// No explicit height - calculate from content
				int contentHeight = CalculateContentHeight(availableWidth);

				// When measured with an effectively-unbounded height, the host is giving us no viewport to
				// fill (e.g. nested in an auto-sizing container). Auto-sizing to full content would make us
				// balloon and hand scrolling to the host, defeating the point of a scroll viewport. Cap to a
				// sane default so we stay bounded and self-scrolling; shorter content still shrinks to fit.
				// In a normal window the panel is handed a bounded viewport instead, so this never triggers.
				if (constraints.IsHeightEffectivelyUnbounded)
					contentHeight = Math.Min(contentHeight, Configuration.ControlDefaults.ScrollablePanelDefaultUnboundedHeight);

				height = contentHeight + Margin.Top + Margin.Bottom + BorderHeight + _padding.Top + _padding.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(width + Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			// Unset foreground follows the theme (so a theme switch recolors panel text), falling
			// back to the painter-supplied default; an explicit ForegroundColor still pins it.
			var fgColor = ColorResolver.ResolveForeground(_foregroundColor, Container, defaultFg);

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			// _viewportHeight / _viewportWidth are the FULL inner content box (border + padding
			// removed). They are NOT reduced for the scrollbars — the reduced values are
			// VisibleContentHeight / VisibleContentWidth, derived from the single-source-of-truth
			// NeedsVerticalScrollbar / NeedsHorizontalScrollbar. This keeps those predicates (which
			// read _viewport*) self-consistent and avoids the double-subtraction trap.
			_viewportHeight = targetHeight - BorderHeight - _padding.Top - _padding.Bottom;
			_viewportWidth = targetWidth - BorderWidth - _padding.Left - _padding.Right;

			// Content width is independent of viewport height; resolve it first so the horizontal
			// scrollbar decision (which steals a content row) can be made before measuring height.
			_contentWidth = CalculateContentWidth();

			// Reserve the horizontal scrollbar row BEFORE measuring content height, so Fill children
			// fill the reduced content area exactly instead of overflowing by the scrollbar row.
			int contentViewportHeight = VisibleContentHeight;
			_contentHeight = CalculateContentHeight(_viewportWidth, contentViewportHeight);

			// Clamp scroll offsets to valid bounds after viewport/content recalculation
			// (viewport may have grown or content may have shrunk since last frame).
			int maxScrollOffset = Math.Max(0, _contentHeight - contentViewportHeight);
			if (_verticalScrollOffset > maxScrollOffset)
				SetVerticalScrollOffset(maxScrollOffset, "paint-clamp-max");
			if (_horizontalScrollOffset > MaxHorizontalScrollOffset)
				_horizontalScrollOffset = MaxHorizontalScrollOffset;

			// Deferred scroll-to-focused: triggered when focus was set before viewport was ready
			if (_pendingScrollToFocused && _viewportWidth > 0 && _viewportHeight > 0)
			{
				_pendingScrollToFocused = false;
				var pendingFocusedChild = GetFocusedChildFromCoordinator();
				if (pendingFocusedChild is IWindowControl pendingFw)
					ScrollChildIntoView(pendingFw);
			}

			// PaintDOM runs in the PAINT phase, AFTER ScrollLayout.ArrangeChildren has already
			// positioned the children using the offset as it stood at arrange time. Any offset change
			// made below (deferred scroll-to-bottom or AutoScroll) therefore only takes effect on the
			// NEXT arrange. Track whether we change it so we can schedule that arrange explicitly —
			// otherwise the panel stays one frame stale and only settles when some unrelated event
			// (e.g. a mouse move) forces another repaint (#62; and the "extra line until the mouse
			// moves" report on #61).
			int offsetBeforeAutoAdjust = _verticalScrollOffset;

			// Deferred scroll-to-bottom: ScrollToBottom() was called before the viewport
			// was laid out. Metrics are now current, so complete the one-shot scroll.
			if (_pendingScrollToBottom && _viewportWidth > 0 && _viewportHeight > 0)
			{
				_pendingScrollToBottom = false;
				SetVerticalScrollOffset(Math.Max(0, _contentHeight - contentViewportHeight), "pending-scroll-bottom");
			}

			// AutoScroll: scroll to bottom on any repaint when enabled
			if (_autoScroll)
			{
				int maxOffset = Math.Max(0, _contentHeight - contentViewportHeight);
				if (_verticalScrollOffset < maxOffset)
				{
					SetVerticalScrollOffset(maxOffset, "autoscroll-bottom");
				}
			}

			// The offset moved during paint, so the children arranged this frame are now at a stale
			// position. Request a relayout so the next frame re-arranges them at the new offset and
			// settles immediately (matching ScrollToBottom()/ScrollVerticalTo, which Invalidate too).
			// Idempotent: once at the bottom the blocks above stop changing the offset, so no further
			// relayout is scheduled and rendering goes idle.
			if (_verticalScrollOffset != offsetBeforeAutoAdjust)
				Invalidate(Invalidation.Relayout);

			// Reserve space for the vertical scrollbar (single source of truth). contentWidth is the
			// VISIBLE content width children are painted into; horizontal overflow beyond it is
			// reached by scrolling, not by widening the paint area.
			bool needsScrollbar = NeedsVerticalScrollbar;
			bool needsHScrollbar = NeedsHorizontalScrollbar;
			int contentWidth = VisibleContentWidth;

			// Draw border if needed
			bool hasBorder = _borderStyle != BorderStyle.None;
			var effectiveBg = _backgroundColorValue == null ? Color.Transparent : bgColor;
			if (!hasBorder && _backgroundColorValue != null)
			{
				// No border but explicit background — fill the entire panel area
				var fillRect = clipRect.Intersect(new LayoutRect(startX, startY, targetWidth, targetHeight));
				if (fillRect.Width > 0 && fillRect.Height > 0)
					Helpers.ControlRenderingHelpers.FillRect(buffer, fillRect, fgColor, effectiveBg);
			}
			if (hasBorder)
			{
				var box = BoxChars.FromBorderStyle(_borderStyle);
				// Border follows the control's ColorRole when set (like the Panel family), else the explicit
				// override, else the foreground. ColorRole.Default makes ColorRoleBorder null → unchanged.
				Color borderColor = _borderColor
					?? ColorResolver.ColorRoleBorder(ColorRole, Container, Outline, mode: ColorRoleMode)
					?? fgColor;

				// Fill the interior of the border with background color
				// This covers padding areas, empty space below children, and scrollbar track
				int innerX = startX + 1;
				int innerY = startY + 1;
				int innerWidth = targetWidth - 2;
				int innerHeight = targetHeight - 2;
				if (innerWidth > 0 && innerHeight > 0)
				{
					var innerRect = new LayoutRect(innerX, innerY, innerWidth, innerHeight);
					var fillRect = clipRect.Intersect(innerRect);
					if (fillRect.Width > 0 && fillRect.Height > 0)
					{
						Helpers.ControlRenderingHelpers.FillRect(buffer, fillRect, fgColor, effectiveBg);
					}
				}

				// Top border with optional header
				DrawTopBorder(buffer, startX, startY, targetWidth, clipRect, box, borderColor, effectiveBg);

				// Left and right vertical border chars for middle rows
				for (int row = 1; row < targetHeight - 1; row++)
				{
					int y = startY + row;
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					if (startX >= clipRect.X && startX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(startX, y, box.Vertical, borderColor, cellBg);
					}
					int rightX = startX + targetWidth - 1;
					if (rightX >= clipRect.X && rightX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(rightX, y, box.Vertical, borderColor, cellBg);
					}
				}

				// Bottom border
				DrawBottomBorder(buffer, startX, startY + targetHeight - 1, targetWidth, clipRect, box, borderColor, effectiveBg);
			}

			// NOTE: SPC no longer paints its children here. As of the ScrollLayout refactor, the
			// panel participates in the layout tree (LayoutNodeFactory.ResolveLayout returns a
			// ScrollLayout + the panel's children), so the engine measures, arranges and paints the
			// children — applying the scroll offset via AbsoluteBounds and clipping each child to the
			// content viewport through ScrollLayout.GetPaintClipRect (scrollbar chrome excluded).
			// PaintDOM is now chrome-only: it computes the viewport/content metrics the scrollbars
			// need (the prelude above), paints the border, and draws the scrollbars BELOW. Because
			// LayoutNode.Paint runs PaintControl (this method) BEFORE painting child nodes, the
			// chrome is drawn first and children — clipped to the viewport — never overdraw it.

			// Draw the scrollbars (single source of truth for visibility).
			if (needsScrollbar)
			{
				DrawVerticalScrollbar(buffer, bounds, clipRect, fgColor, bgColor);
			}
			if (needsHScrollbar)
			{
				DrawHorizontalScrollbar(buffer, bounds, clipRect, contentWidth, fgColor, bgColor);
			}

		}

		#endregion

		#region Scrollbar Rendering

		/// <summary>The shared engine's view of the vertical scrollbar's geometry inputs.</summary>
		private ScrollbarMetrics VerticalScrollbarMetrics =>
			new(VisibleContentHeight, _contentHeight, VisibleContentHeight, _verticalScrollOffset);

		/// <summary>The shared engine's view of the horizontal scrollbar's geometry inputs.</summary>
		private ScrollbarMetrics HorizontalScrollbarMetrics =>
			new(VisibleContentWidth, _contentWidth, VisibleContentWidth, _horizontalScrollOffset);

		private (int scrollbarRelX, int scrollbarTop, int scrollbarHeight, int thumbY, int thumbHeight) GetScrollbarGeometry()
		{
			// scrollbarRelX is control-relative (offset from bounds.X).
			int scrollbarRelX;
			if (OverlayActive)
			{
				// Overlay: paint the thumb ON the border line (no interior column reserved). The border
				// columns are the panel edges: left at Margin.Left, right at Margin.Left+targetWidth-1.
				// targetWidth is the panel's drawn width = viewport + border + horizontal padding.
				int panelWidth = _viewportWidth + BorderWidth + _padding.Left + _padding.Right;
				scrollbarRelX = _scrollbarPosition == ScrollbarPosition.Right
					? Margin.Left + panelWidth - 1   // right border column
					: Margin.Left;                    // left border column
			}
			else
			{
				// Normal: position the scrollbar inside the border, in its reserved interior column.
				scrollbarRelX = _scrollbarPosition == ScrollbarPosition.Right
					? Margin.Left + ContentInsetLeft + _viewportWidth - 1
					: Margin.Left + ContentInsetLeft;
			}
			int scrollbarTop = Margin.Top + ContentInsetTop;
			// The vertical track spans the content height (the H-scrollbar row, when shown, is below it).
			var metrics = VerticalScrollbarMetrics;

			int thumbHeight = ScrollbarGeometry.ThumbLength(metrics);
			int thumbY = ScrollbarGeometry.ThumbPosForOffset(metrics);

			return (scrollbarRelX, scrollbarTop, metrics.TrackLength, thumbY, thumbHeight);
		}

		// Horizontal scrollbar geometry, mirroring GetScrollbarGeometry. The track sits on the row
		// directly below the content viewport and spans the visible content width.
		private (int scrollbarRelX, int scrollbarRelY, int trackWidth, int thumbX, int thumbWidth) GetHScrollbarGeometry()
		{
			var metrics = HorizontalScrollbarMetrics;
			int scrollbarRelX = Margin.Left + ContentInsetLeft;
			// Overlay: paint on the bottom border row (no reserved row). Non-overlay: the reserved row
			// directly below the content viewport.
			int scrollbarRelY = OverlayActive
				? Margin.Top + (_viewportHeight + BorderHeight + _padding.Top + _padding.Bottom) - 1  // bottom border row
				: Margin.Top + ContentInsetTop + VisibleContentHeight;

			int thumbWidth = ScrollbarGeometry.ThumbLength(metrics);
			int thumbX = ScrollbarGeometry.ThumbPosForOffset(metrics);

			return (scrollbarRelX, scrollbarRelY, metrics.TrackLength, thumbX, thumbWidth);
		}

		/// <summary>
		/// Resolves the scrollbar palette. SPC is the only scrollbar-bearing control whose colour
		/// cascade includes a ColorRole tier, so that check runs AHEAD of the shared engine resolver:
		/// an explicit <see cref="ScrollbarThumbColor"/> override wins outright, then the control's
		/// ColorRole (the scrollbar is a scroll panel's defining chrome), and only then the engine's
		/// theme/fallback tiers (shared with every other control).
		/// </summary>
		private ScrollbarPalette ResolveScrollbarPalette(Color bgColor)
		{
			var roleState = !IsEnabled ? ColorRoleState.Disabled : (HasFocus ? ColorRoleState.Focused : ColorRoleState.Normal);
			Color? roleThumb = _scrollbarThumbColor == null
				? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, roleState, mode: ColorRoleMode)
				: null;

			var resolved = ScrollbarPaletteResolver.Resolve(new ScrollbarPaletteRequest(
				ThumbOverride: _scrollbarThumbColor ?? roleThumb,
				TrackOverride: _scrollbarColor,
				Theme: GetConsoleWindowSystem?.Theme,
				HasFocus: HasFocus,
				IsEnabled: IsEnabled,
				Background: bgColor));

			return resolved;
		}

		private void DrawVerticalScrollbar(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fgColor, Color bgColor)
		{
			var (scrollbarRelX, scrollbarTop, _, _, _) = GetScrollbarGeometry();

			// Convert control-relative coordinates to buffer-absolute coordinates
			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarAbsTop = bounds.Y + scrollbarTop;

			var palette = ResolveScrollbarPalette(bgColor);

			// In overlay mode the scrollbar shares the border column: the existing border line IS the
			// track, so paint ONLY the thumb cells (overriding the border where the thumb sits) and skip
			// the track fill + arrows, which would otherwise erase/overdraw the frame.
			bool overlay = OverlayActive;

			ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, scrollbarX, scrollbarAbsTop,
				VerticalScrollbarMetrics, palette, clip: clipRect, drawArrows: !overlay, drawTrack: !overlay);
		}

		/// <summary>
		/// Draws the horizontal scrollbar on the row reserved just below the content viewport.
		/// Mirrors <see cref="DrawVerticalScrollbar"/>: a left/right arrow at each end and a thumb
		/// sized/positioned from the shared geometry. <paramref name="trackWidth"/> is the visible
		/// content width (excludes the vertical scrollbar columns).
		/// </summary>
		private void DrawHorizontalScrollbar(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, int trackWidth, Color fgColor, Color bgColor)
		{
			if (trackWidth <= 0) return;

			var (scrollbarRelX, scrollbarRelY, _, _, _) = GetHScrollbarGeometry();
			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarY = bounds.Y + scrollbarRelY;

			var palette = ResolveScrollbarPalette(bgColor);

			// In overlay mode the scrollbar shares the bottom border row: the border line IS the track,
			// so paint ONLY the thumb cells and skip the track fill + end arrows (which would erase the
			// frame / overwrite the corners).
			bool overlay = OverlayActive;

			ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Horizontal, scrollbarX, scrollbarY,
				HorizontalScrollbarMetrics, palette, clip: clipRect, drawArrows: !overlay, drawTrack: !overlay);
		}

		#endregion

		#region Border Drawing

		private void DrawTopBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int innerWidth = width - 2;

			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.TopLeft, borderColor, cellBg);
			}

			if (string.IsNullOrEmpty(_header) || innerWidth < 4)
			{
				for (int i = 0; i < innerWidth; i++)
				{
					int px = x + 1 + i;
					if (px >= clipRect.X && px < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
					}
				}
			}
			else
			{
				var headerCells = MarkupParser.Parse(_header, borderColor, bgColor);
				int headerLen = headerCells.Count;
				int headerWithSpaces = headerLen + 2;

				if (headerWithSpaces > innerWidth)
				{
					for (int i = 0; i < innerWidth; i++)
					{
						int px = x + 1 + i;
						if (px >= clipRect.X && px < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
						}
					}
				}
				else
				{
					int dashSpace = innerWidth - headerWithSpaces;
					int leftDashes, rightDashes;

					switch (_headerAlignment)
					{
						case TextJustification.Center:
							leftDashes = dashSpace / 2;
							rightDashes = dashSpace - leftDashes;
							break;
						case TextJustification.Right:
							leftDashes = dashSpace - 1;
							rightDashes = 1;
							break;
						default:
							leftDashes = 1;
							rightDashes = dashSpace - 1;
							break;
					}

					int writeX = x + 1;

					for (int i = 0; i < leftDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}

					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					foreach (var cell in headerCells)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							buffer.SetCell(writeX, y, cell);
						}
						writeX++;
					}

					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					for (int i = 0; i < rightDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}
				}
			}

			int rightCornerX = x + width - 1;
			if (rightCornerX >= clipRect.X && rightCornerX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightCornerX, y, box.TopRight, borderColor, cellBg);
			}
		}

		private void DrawBottomBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.BottomLeft, borderColor, cellBg);
			}

			int innerWidth = width - 2;
			for (int i = 0; i < innerWidth; i++)
			{
				int px = x + 1 + i;
				if (px >= clipRect.X && px < clipRect.Right)
				{
					var cellBg = bgColor;
					buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
				}
			}

			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, box.BottomRight, borderColor, cellBg);
			}
		}

		#endregion

		#region Content Measurement

		/// <summary>
		/// Computes the total natural height (in rows) of this panel's content when laid out at the given
		/// width, reflowing wrapped text. Pure measurement: it does NOT change the panel's arranged size or
		/// scroll state. Intended for callers that need a content-fit height (e.g. auto-sizing a host window)
		/// without running a full layout pass.
		/// </summary>
		/// <param name="viewportWidth">The width, in cells, to lay the content out against.</param>
		/// <returns>The content's natural height in rows.</returns>
		public int MeasureContentHeight(int viewportWidth) => CalculateContentHeight(viewportWidth);

		private int CalculateContentHeight(int viewportWidth, int maxHeight = 0)
		{
			// Measure at full width first, then re-measure at the reduced (scrollbar) width if the content
			// overflows and a scrollbar will appear.
			int fullHeight = MeasureChildrenHeight(viewportWidth, maxHeight);

			int viewportH = maxHeight > 0 ? maxHeight : _viewportHeight;
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll)
			{
				int narrowWidth = Math.Max(1, viewportWidth - 2);
				if (narrowWidth != viewportWidth)
				{
					int narrowHeight = MeasureChildrenHeight(narrowWidth, maxHeight);

					// Stability fix: the overflow decision must be a FIXED POINT, not dependent on the
					// borderline full-width height. If EITHER measurement overflows the viewport, the
					// scrollbar shows, so the content height is the NARROW (scrollbar-present) height.
					// Deciding on fullHeight alone made content height oscillate between the wrapped and
					// unwrapped values across re-measures (e.g. between a wheel tick and the ScrollVerticalBy
					// clamp), capping the scroll partway through wrapping content (issue: log cell stopped
					// scrolling at ~1/3). Using the narrow height whenever either overflows is stable.
					if (narrowHeight > viewportH || fullHeight > viewportH)
						return narrowHeight;
				}
			}

			return fullHeight;
		}

		private int MeasureChildrenHeight(int availableWidth, int maxHeight)
		{
			List<IWindowControl> calcSnapshot;
			lock (_childrenLock) { calcSnapshot = new List<IWindowControl>(_children); }

			// Use the shared Fill metrics + per-child height so the panel's own content-height
			// measurement agrees with how PaintDOM and hit-testing size each child. A Fill child
			// contributes the height it is actually painted into (its allocated slot), not just
			// its content size. maxHeight is the height to distribute Fill children across
			// (the viewport, possibly re-evaluated at a reduced width for the scrollbar).
			var (_, _, perFillHeight) = ComputeFillMetrics(calcSnapshot, availableWidth, maxHeight);

			int totalHeight = 0;
			foreach (var child in calcSnapshot)
			{
				if (!child.Visible) continue;
				totalHeight += ComputeChildHeight(child, availableWidth, perFillHeight, maxHeight);
			}

			return totalHeight;
		}

		private int CalculateContentWidth()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }
			int maxWidth = 0;
			foreach (var c in snapshot)
			{
				if (c.Visible)
				{
					int w = c.GetLogicalContentSize().Width;
					if (w > maxWidth) maxWidth = w;
				}
			}
			return maxWidth;
		}

		#endregion
	}
}
