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
				_verticalScrollOffset = maxScrollOffset;
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

			// Deferred scroll-to-bottom: ScrollToBottom() was called before the viewport
			// was laid out. Metrics are now current, so complete the one-shot scroll.
			if (_pendingScrollToBottom && _viewportWidth > 0 && _viewportHeight > 0)
			{
				_pendingScrollToBottom = false;
				_verticalScrollOffset = Math.Max(0, _contentHeight - contentViewportHeight);
			}

			// AutoScroll: scroll to bottom on any repaint when enabled
			if (_autoScroll)
			{
				int maxOffset = Math.Max(0, _contentHeight - contentViewportHeight);
				if (_verticalScrollOffset < maxOffset)
				{
					_verticalScrollOffset = maxOffset;
				}
			}

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
				DrawVerticalScrollbar(buffer, bounds, fgColor, bgColor);
			}
			if (needsHScrollbar)
			{
				DrawHorizontalScrollbar(buffer, bounds, contentWidth, fgColor, bgColor);
			}

		}

		#endregion

		#region Scrollbar Rendering

		// Thumb sizing/positioning is shared by both axes and by the drag handlers, so the forward
		// map (offset → thumb pixel) and the inverse used while dragging round-trip cleanly (Bug D).

		/// <summary>The number of track cells reserved for arrows (2 when the track is long enough).</summary>
		private static int ArrowSlots(int trackLength) => trackLength >= 3 ? 2 : 0;

		/// <summary>The thumb length for a track, from the viewport/content ratio.</summary>
		private static int ThumbLength(int trackLength, int viewportExtent, int contentExtent)
		{
			int arrowSlots = ArrowSlots(trackLength);
			int thumbTrack = Math.Max(1, trackLength - arrowSlots);
			double ratio = (double)viewportExtent / Math.Max(1, contentExtent);
			return Math.Clamp((int)(thumbTrack * ratio), 1, thumbTrack);
		}

		/// <summary>
		/// The thumb's start position within the track (including the leading arrow slot) for a
		/// given scroll offset. Inverse of <see cref="OffsetForThumbPos"/>.
		/// </summary>
		private static int ThumbPosForOffset(int trackLength, int viewportExtent, int contentExtent, int scrollOffset)
		{
			int arrowSlots = ArrowSlots(trackLength);
			int thumbTrack = Math.Max(1, trackLength - arrowSlots);
			int thumbLen = ThumbLength(trackLength, viewportExtent, contentExtent);
			int pos = arrowSlots > 0 ? 1 : 0;
			int maxOffset = Math.Max(0, contentExtent - viewportExtent);
			if (maxOffset > 0)
			{
				double scrollRatio = (double)scrollOffset / maxOffset;
				int maxThumbPos = thumbTrack - thumbLen;
				pos += Math.Min((int)Math.Round(maxThumbPos * scrollRatio), maxThumbPos);
			}
			return pos;
		}

		/// <summary>
		/// The scroll offset that places the thumb start at <paramref name="thumbPos"/> within the
		/// track. Inverse of <see cref="ThumbPosForOffset"/> — uses the same rounding convention so
		/// a drag to a thumb pixel maps back to the offset whose forward map returns that pixel.
		/// </summary>
		private static int OffsetForThumbPos(int trackLength, int viewportExtent, int contentExtent, int thumbPos)
		{
			int arrowSlots = ArrowSlots(trackLength);
			int thumbTrack = Math.Max(1, trackLength - arrowSlots);
			int thumbLen = ThumbLength(trackLength, viewportExtent, contentExtent);
			int maxThumbPos = thumbTrack - thumbLen;
			int maxOffset = Math.Max(0, contentExtent - viewportExtent);
			if (maxThumbPos <= 0 || maxOffset <= 0) return 0;
			int rel = Math.Clamp((arrowSlots > 0 ? thumbPos - 1 : thumbPos), 0, maxThumbPos);
			double scrollRatio = (double)rel / maxThumbPos;
			return Math.Min((int)Math.Round(maxOffset * scrollRatio), maxOffset);
		}

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
			int scrollbarHeight = VisibleContentHeight;

			int thumbHeight = ThumbLength(scrollbarHeight, scrollbarHeight, _contentHeight);
			int thumbY = ThumbPosForOffset(scrollbarHeight, scrollbarHeight, _contentHeight, _verticalScrollOffset);

			return (scrollbarRelX, scrollbarTop, scrollbarHeight, thumbY, thumbHeight);
		}

		// Horizontal scrollbar geometry, mirroring GetScrollbarGeometry. The track sits on the row
		// directly below the content viewport and spans the visible content width.
		private (int scrollbarRelX, int scrollbarRelY, int trackWidth, int thumbX, int thumbWidth) GetHScrollbarGeometry()
		{
			int trackWidth = VisibleContentWidth;
			int scrollbarRelX = Margin.Left + ContentInsetLeft;
			// Overlay: paint on the bottom border row (no reserved row). Non-overlay: the reserved row
			// directly below the content viewport.
			int scrollbarRelY = OverlayActive
				? Margin.Top + (_viewportHeight + BorderHeight + _padding.Top + _padding.Bottom) - 1  // bottom border row
				: Margin.Top + ContentInsetTop + VisibleContentHeight;

			int thumbWidth = ThumbLength(trackWidth, trackWidth, _contentWidth);
			int thumbX = ThumbPosForOffset(trackWidth, trackWidth, _contentWidth, _horizontalScrollOffset);

			return (scrollbarRelX, scrollbarRelY, trackWidth, thumbX, thumbWidth);
		}

		/// <summary>Thumb color: the <see cref="ScrollbarThumbColor"/> override, else the control's ColorRole
		/// (the scrollbar is a scroll panel's defining chrome), else the theme's scrollbar thumb
		/// (focus-aware), else a hardcoded fallback.</summary>
		private Color ResolveScrollbarThumbColor()
		{
			if (_scrollbarThumbColor.HasValue) return _scrollbarThumbColor.Value;
			var roleState = !IsEnabled ? ColorRoleState.Disabled : (HasFocus ? ColorRoleState.Focused : ColorRoleState.Normal);
			var roleThumb = ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, roleState, mode: ColorRoleMode);
			if (roleThumb != null) return roleThumb.Value;
			var theme = GetConsoleWindowSystem?.Theme;
			if (theme != null)
				return (HasFocus ? theme.ScrollbarThumbColor : theme.ScrollbarThumbUnfocusedColor) ?? theme.WindowForegroundColor;
			return HasFocus ? Color.Cyan1 : Color.Grey;
		}

		/// <summary>Track color: the <see cref="ScrollbarColor"/> override, else the theme's scrollbar track (focus-aware), else a hardcoded fallback.</summary>
		private Color ResolveScrollbarTrackColor()
		{
			if (_scrollbarColor.HasValue) return _scrollbarColor.Value;
			var theme = GetConsoleWindowSystem?.Theme;
			if (theme != null)
				return (HasFocus ? theme.ScrollbarTrackColor : theme.ScrollbarTrackUnfocusedColor) ?? theme.WindowBackgroundColor;
			return HasFocus ? Color.Grey : Color.Grey23;
		}

		private void DrawVerticalScrollbar(CharacterBuffer buffer, LayoutRect bounds, Color fgColor, Color bgColor)
		{
			var (scrollbarRelX, scrollbarTop, scrollbarHeight, thumbY, thumbHeight) = GetScrollbarGeometry();

			// Convert control-relative coordinates to buffer-absolute coordinates
			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarAbsTop = bounds.Y + scrollbarTop;

			// Colors: honor the ScrollbarThumbColor/ScrollbarColor overrides; otherwise fall back to
			// the focus-aware defaults.
			Color thumbColor = ResolveScrollbarThumbColor();
			Color trackColor = ResolveScrollbarTrackColor();

			// In overlay mode the scrollbar shares the border column: the existing border line IS the
			// track, so paint ONLY the thumb cells (overriding the border where the thumb sits) and skip
			// the track fill + arrows, which would otherwise erase/overdraw the frame.
			bool overlay = OverlayActive;

			for (int y = 0; y < scrollbarHeight; y++)
			{
				bool isThumb = y >= thumbY && y < thumbY + thumbHeight;
				if (overlay && !isThumb)
					continue; // leave the border line intact as the track

				Color color = isThumb ? thumbColor : trackColor;
				char ch = isThumb ? '\u2588' : '\u2502';
				buffer.SetNarrowCell(scrollbarX, scrollbarAbsTop + y, ch, color, bgColor);
			}

			if (!overlay)
			{
				// Arrows at top/bottom (non-overlay only \u2014 in overlay they'd overwrite the border corners).
				buffer.SetNarrowCell(scrollbarX, scrollbarAbsTop, '\u25b2', thumbColor, bgColor);
				buffer.SetNarrowCell(scrollbarX, scrollbarAbsTop + scrollbarHeight - 1, '\u25bc', thumbColor, bgColor);
			}
		}

		/// <summary>
		/// Draws the horizontal scrollbar on the row reserved just below the content viewport.
		/// Mirrors <see cref="DrawVerticalScrollbar"/>: a left/right arrow at each end and a thumb
		/// sized/positioned from the shared geometry. <paramref name="trackWidth"/> is the visible
		/// content width (excludes the vertical scrollbar columns).
		/// </summary>
		private void DrawHorizontalScrollbar(CharacterBuffer buffer, LayoutRect bounds, int trackWidth, Color fgColor, Color bgColor)
		{
			if (trackWidth <= 0) return;

			var (scrollbarRelX, scrollbarRelY, _, thumbX, thumbWidth) = GetHScrollbarGeometry();
			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarY = bounds.Y + scrollbarRelY;

			Color thumbColor = ResolveScrollbarThumbColor();
			Color trackColor = ResolveScrollbarTrackColor();

			// In overlay mode the scrollbar shares the bottom border row: the border line IS the track,
			// so paint ONLY the thumb cells and skip the track fill + end arrows (which would erase the
			// frame / overwrite the corners).
			bool overlay = OverlayActive;

			for (int x = 0; x < trackWidth; x++)
			{
				bool isThumb = x >= thumbX && x < thumbX + thumbWidth;
				if (overlay && !isThumb)
					continue; // leave the bottom border intact as the track

				Color color = isThumb ? thumbColor : trackColor;
				char ch = isThumb ? '\u25ac' : '\u2500'; // U+25AC thumb / U+2500 track
				buffer.SetNarrowCell(scrollbarX + x, scrollbarY, ch, color, bgColor);
			}

			if (!overlay)
			{
				// Left/right arrows at the ends (non-overlay only \u2014 they'd overwrite the border corners).
				buffer.SetNarrowCell(scrollbarX, scrollbarY, '\u25c4', thumbColor, bgColor);                 // U+25C4
				buffer.SetNarrowCell(scrollbarX + trackWidth - 1, scrollbarY, '\u25ba', thumbColor, bgColor); // U+25BA
			}
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
