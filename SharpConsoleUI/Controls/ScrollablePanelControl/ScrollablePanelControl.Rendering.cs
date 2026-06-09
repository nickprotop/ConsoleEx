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
			var fgColor = _foregroundColor;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			int fullViewportHeight = targetHeight - BorderHeight - _padding.Top - _padding.Bottom;
			_viewportWidth = targetWidth - BorderWidth - _padding.Left - _padding.Right;

			// Content width is independent of viewport height, so resolve the horizontal scrollbar
			// first. It steals one row from the content viewport; reducing _viewportHeight BEFORE
			// measuring content means Fill children fill the reduced content area exactly (and so
			// don't spuriously overflow by the scrollbar row). _contentWidth must be known first.
			_contentWidth = CalculateContentWidth();
			_viewportHeight = fullViewportHeight;
			bool hScrollbar = NeedsHorizontalScrollbar;
			if (hScrollbar)
				_viewportHeight = Math.Max(1, _viewportHeight - HorizontalScrollbarRows);

			// Calculate content height against the (possibly reduced) viewport so Fill children
			// fill the content area, not the area the scrollbar occupies.
			_contentHeight = CalculateContentHeight(_viewportWidth, _viewportHeight);

			// Clamp scroll offsets to valid bounds after viewport/content recalculation
			// (viewport may have grown or content may have shrunk since last frame)
			int maxScrollOffset = Math.Max(0, _contentHeight - _viewportHeight);
			if (_verticalScrollOffset > maxScrollOffset)
				_verticalScrollOffset = maxScrollOffset;

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
				_verticalScrollOffset = Math.Max(0, _contentHeight - _viewportHeight);
			}

			// AutoScroll: scroll to bottom on any repaint when enabled
			if (_autoScroll)
			{
				int maxOffset = Math.Max(0, _contentHeight - _viewportHeight);
				if (_verticalScrollOffset < maxOffset)
				{
					_verticalScrollOffset = maxOffset;
				}
			}

			// Reserve space for the vertical scrollbar (2 columns: 1 gap + 1 bar). This keeps the
			// existing behavior: _viewportWidth stays the full inner width; the vertical bar is
			// drawn in the reserved columns and content is painted in `contentWidth`.
			int contentWidth = _viewportWidth;
			bool needsScrollbar = NeedsVerticalScrollbar;
			if (needsScrollbar)
			{
				contentWidth -= VerticalScrollbarColumns;
			}
			contentWidth = Math.Max(1, contentWidth);

			// Clamp the horizontal scroll offset against the content area width (what actually
			// shows). _contentWidth is the children's logical width; contentWidth is the visible
			// column count, so max offset is their difference.
			int maxHScrollOffset = Math.Max(0, _contentWidth - contentWidth);
			if (_horizontalScrollOffset > maxHScrollOffset)
				_horizontalScrollOffset = maxHScrollOffset;

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
				Color borderColor = _borderColor ?? fgColor;

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

			// Content area origin (inside border + padding)
			int contentOriginX = startX + ContentInsetLeft;
			int contentOriginY = startY + ContentInsetTop;

			// Render children with scroll offsets applied
			int currentY = -_verticalScrollOffset;

			// When horizontal scrolling is enabled, measure/arrange children at their full content
			// width so overflow exists to scroll through; the viewport clip below trims it. When it
			// is off, children are constrained to the visible width (the original behavior).
			bool horizontalScrollEnabled = _horizontalScrollMode == ScrollMode.Scroll;
			int childLayoutWidth = horizontalScrollEnabled ? Math.Max(contentWidth, _contentWidth) : contentWidth;

			// Get renderer for registering child bounds (needed for cursor position lookups)
			var parentWindow = this.GetParentWindow();
			var renderer = parentWindow?.Renderer;

			List<IWindowControl> paintSnapshot;
			lock (_childrenLock) { paintSnapshot = new List<IWindowControl>(_children); }

			// Two-pass Fill layout. The metrics (fixed height, fill count, per-Fill height) are
			// computed by the shared helper so paint, hit-testing and scroll-into-view agree.
			var (_, _, perFillHeight) = ComputeFillMetrics(paintSnapshot, contentWidth);

			foreach (var child in paintSnapshot)
			{
				if (!child.Visible) continue;

				// Build layout subtree (handles containers like TabControl, HorizontalGrid)
				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;

				// Measure using full layout pipeline.
				// Fill-aligned children: get remaining space after fixed children.
				// Content-sized children: measure unbounded for correct scroll positioning.
				bool isFillChild = _viewportHeight > 0 && child.VerticalAlignment == VerticalAlignment.Fill;
				int maxChildHeight = isFillChild ? perFillHeight : int.MaxValue;
				var constraints = new LayoutConstraints(1, childLayoutWidth, 1, maxChildHeight);
				childNode.Measure(constraints);
				// Fill children occupy their full allocated slot even if their content (DesiredSize)
				// is smaller — that is what VerticalAlignment.Fill means. Content-sized children use
				// their DesiredSize so they can overflow the viewport and be scrolled.
				int childHeight = isFillChild
					? Math.Max(childNode.DesiredSize.Height, perFillHeight)
					: childNode.DesiredSize.Height;

				// Respect explicit Width on child controls
				int childWidth = (child.Width.HasValue && child.Width.Value < childLayoutWidth)
					? child.Width.Value
					: childLayoutWidth;

				// Horizontal scroll offset shifts children left.
				int childX = contentOriginX - _horizontalScrollOffset;

				// Register child bounds for cursor position lookups (even if off-viewport)
				var childBoundsForCursor = new LayoutRect(
					childX,
					contentOriginY + currentY,
					childWidth,
					childHeight);
				renderer?.UpdateChildBounds(child, childBoundsForCursor);

				// Only render if in viewport
				if (currentY + childHeight > 0 && currentY < _viewportHeight)
				{
					var childBounds = new LayoutRect(
						childX,
						contentOriginY + currentY,
						childWidth,
						childHeight);

					// Arrange in screen coordinates (so AbsoluteBounds are correct)
					childNode.Arrange(childBounds);

					// Create clipped clipRect for child that excludes scrollbar area and clips to viewport
					var viewportRect = new LayoutRect(
						contentOriginX,
						contentOriginY,
						needsScrollbar ? contentWidth + 1 : contentWidth, // +1 for gap if scrollbar visible
						_viewportHeight);

					var childClipRect = clipRect.Intersect(viewportRect);

					if (needsScrollbar)
					{
						// Further restrict to exclude scrollbar columns
						int maxRight = contentOriginX + contentWidth + 1; // +1 for gap
						childClipRect = childClipRect.Intersect(new LayoutRect(
							childClipRect.X,
							childClipRect.Y,
							Math.Min(childClipRect.Width, maxRight - childClipRect.X),
							childClipRect.Height));
					}

					// Paint through layout pipeline (headers + children properly)
					childNode.Paint(buffer, childClipRect, fgColor, bgColor);
				}

				currentY += childHeight;
			}

			// Draw vertical scrollbar if content exceeds viewport and there's room
			if (NeedsVerticalScrollbar)
			{
				DrawVerticalScrollbar(buffer, bounds, fgColor, bgColor);
			}

			// Draw horizontal scrollbar on the reserved bottom row.
			if (hScrollbar)
			{
				DrawHorizontalScrollbar(buffer, bounds, contentWidth, fgColor, bgColor);
			}

			_isDirty = false;
		}

		#endregion

		#region Scrollbar Rendering

		private (int scrollbarRelX, int scrollbarTop, int scrollbarHeight, int thumbY, int thumbHeight) GetScrollbarGeometry()
		{
			// scrollbarRelX is control-relative (offset from bounds.X)
			// Position scrollbar inside the border if present
			int scrollbarRelX = _scrollbarPosition == ScrollbarPosition.Right
				? Margin.Left + ContentInsetLeft + _viewportWidth - 1
				: Margin.Left + ContentInsetLeft;
			int scrollbarTop = Margin.Top + ContentInsetTop;
			int scrollbarHeight = _viewportHeight;

			// Reserve arrow positions so thumb never overlaps them
			int arrowSlots = scrollbarHeight >= 3 ? 2 : 0;
			int thumbTrackHeight = Math.Max(1, scrollbarHeight - arrowSlots);
			double viewportRatio = (double)_viewportHeight / Math.Max(1, _contentHeight);
			int thumbHeight = Math.Clamp((int)(thumbTrackHeight * viewportRatio), 1, thumbTrackHeight);
			int thumbY = arrowSlots > 0 ? 1 : 0;
			if (_contentHeight > _viewportHeight)
			{
				double scrollRatio = (double)_verticalScrollOffset / (_contentHeight - _viewportHeight);
				int maxThumbPos = thumbTrackHeight - thumbHeight;
				thumbY += Math.Min((int)Math.Round(maxThumbPos * scrollRatio), maxThumbPos);
			}

			return (scrollbarRelX, scrollbarTop, scrollbarHeight, thumbY, thumbHeight);
		}

		private void DrawVerticalScrollbar(CharacterBuffer buffer, LayoutRect bounds, Color fgColor, Color bgColor)
		{
			var (scrollbarRelX, scrollbarTop, scrollbarHeight, thumbY, thumbHeight) = GetScrollbarGeometry();

			// Convert control-relative coordinates to buffer-absolute coordinates
			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarAbsTop = bounds.Y + scrollbarTop;

			// Colors
			Color thumbColor = HasFocus ? Color.Cyan1 : Color.Grey;
			Color trackColor = HasFocus ? Color.Grey : Color.Grey23;

			for (int y = 0; y < scrollbarHeight; y++)
			{
				Color color;
				char ch;

				if (y >= thumbY && y < thumbY + thumbHeight)
				{
					color = thumbColor;
					ch = '\u2588';
				}
				else
				{
					color = trackColor;
					ch = '\u2502';
				}

				buffer.SetNarrowCell(scrollbarX, scrollbarAbsTop + y, ch, color, bgColor);
			}

			// Always draw arrows at top/bottom with thumb color
			buffer.SetNarrowCell(scrollbarX, scrollbarAbsTop, '\u25b2', thumbColor, bgColor);
			buffer.SetNarrowCell(scrollbarX, scrollbarAbsTop + scrollbarHeight - 1, '\u25bc', thumbColor, bgColor);
		}

		/// <summary>
		/// Draws the horizontal scrollbar on the row reserved just below the content viewport.
		/// Mirrors <see cref="DrawVerticalScrollbar"/>: a left/right arrow at each end and a thumb
		/// sized from the viewport/content ratio and positioned from the horizontal scroll offset.
		/// <paramref name="trackWidth"/> is the visible content width (excludes the vertical
		/// scrollbar columns).
		/// </summary>
		private void DrawHorizontalScrollbar(CharacterBuffer buffer, LayoutRect bounds, int trackWidth, Color fgColor, Color bgColor)
		{
			if (trackWidth <= 0) return;

			// Row directly below the content viewport, inside the border/padding.
			int scrollbarRelY = Margin.Top + ContentInsetTop + _viewportHeight;
			int scrollbarRelX = Margin.Left + ContentInsetLeft;

			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarY = bounds.Y + scrollbarRelY;

			Color thumbColor = HasFocus ? Color.Cyan1 : Color.Grey;
			Color trackColor = HasFocus ? Color.Grey : Color.Grey23;

			// Reserve arrow positions so the thumb never overlaps them.
			int arrowSlots = trackWidth >= 3 ? 2 : 0;
			int thumbTrackWidth = Math.Max(1, trackWidth - arrowSlots);
			double viewportRatio = (double)trackWidth / Math.Max(1, _contentWidth);
			int thumbWidth = Math.Clamp((int)(thumbTrackWidth * viewportRatio), 1, thumbTrackWidth);
			int thumbX = arrowSlots > 0 ? 1 : 0;
			int maxOffset = Math.Max(0, _contentWidth - trackWidth);
			if (maxOffset > 0)
			{
				double scrollRatio = (double)_horizontalScrollOffset / maxOffset;
				int maxThumbPos = thumbTrackWidth - thumbWidth;
				thumbX += Math.Min((int)Math.Round(maxThumbPos * scrollRatio), maxThumbPos);
			}

			for (int x = 0; x < trackWidth; x++)
			{
				char ch;
				Color color;
				if (x >= thumbX && x < thumbX + thumbWidth)
				{
					color = thumbColor;
					ch = '█';
				}
				else
				{
					color = trackColor;
					ch = '─';
				}
				buffer.SetNarrowCell(scrollbarX + x, scrollbarY, ch, color, bgColor);
			}

			// Left/right arrows at the ends.
			buffer.SetNarrowCell(scrollbarX, scrollbarY, '◄', thumbColor, bgColor);
			buffer.SetNarrowCell(scrollbarX + trackWidth - 1, scrollbarY, '►', thumbColor, bgColor);
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
			// Measure at full width first, then re-measure at reduced width only if
			// the content actually overflows and a scrollbar will appear.
			int fullHeight = MeasureChildrenHeight(viewportWidth, maxHeight);

			int viewportH = maxHeight > 0 ? maxHeight : _viewportHeight;
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && fullHeight > viewportH)
			{
				int narrowWidth = Math.Max(1, viewportWidth - 2);
				if (narrowWidth != viewportWidth)
					return MeasureChildrenHeight(narrowWidth, maxHeight);
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
