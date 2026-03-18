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

			var bgColor = BackgroundColor;
			var fgColor = _foregroundColor;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			_viewportHeight = targetHeight - BorderHeight - _padding.Top - _padding.Bottom;
			_viewportWidth = targetWidth - BorderWidth - _padding.Left - _padding.Right;

			// Calculate content dimensions from children
			_contentHeight = CalculateContentHeight(_viewportWidth, _viewportHeight);
			_contentWidth = CalculateContentWidth();

			// AutoScroll: scroll to bottom on any repaint when enabled
			if (_autoScroll)
			{
				int maxOffset = Math.Max(0, _contentHeight - _viewportHeight);
				if (_verticalScrollOffset < maxOffset)
				{
					_verticalScrollOffset = maxOffset;
				}
			}

			// Reserve space for scrollbar(s)
			int contentWidth = _viewportWidth;
			bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
			if (needsScrollbar)
			{
				contentWidth -= 2;  // Reserve 2 columns: 1 for gap, 1 for scrollbar
			}
			contentWidth = Math.Max(1, contentWidth);

			// Draw border if needed
			bool hasBorder = _borderStyle != BorderStyle.None;
			bool preserveBg = _backgroundColorValue == null && (Container?.HasGradientBackground ?? false);
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
						Helpers.ControlRenderingHelpers.FillRect(buffer, fillRect, fgColor, bgColor, preserveBg);
					}
				}

				// Top border with optional header
				DrawTopBorder(buffer, startX, startY, targetWidth, clipRect, box, borderColor, bgColor, preserveBg);

				// Left and right vertical border chars for middle rows
				for (int row = 1; row < targetHeight - 1; row++)
				{
					int y = startY + row;
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					if (startX >= clipRect.X && startX < clipRect.Right)
					{
						var cellBg = preserveBg ? buffer.GetCell(startX, y).Background : bgColor;
						buffer.SetNarrowCell(startX, y, box.Vertical, borderColor, cellBg);
					}
					int rightX = startX + targetWidth - 1;
					if (rightX >= clipRect.X && rightX < clipRect.Right)
					{
						var cellBg = preserveBg ? buffer.GetCell(rightX, y).Background : bgColor;
						buffer.SetNarrowCell(rightX, y, box.Vertical, borderColor, cellBg);
					}
				}

				// Bottom border
				DrawBottomBorder(buffer, startX, startY + targetHeight - 1, targetWidth, clipRect, box, borderColor, bgColor, preserveBg);
			}

			// Content area origin (inside border + padding)
			int contentOriginX = startX + ContentInsetLeft;
			int contentOriginY = startY + ContentInsetTop;

			// Render children with scroll offsets applied
			int currentY = -_verticalScrollOffset;

			// Get renderer for registering child bounds (needed for cursor position lookups)
			var parentWindow = this.GetParentWindow();
			var renderer = parentWindow?.Renderer;

			List<IWindowControl> paintSnapshot;
			lock (_childrenLock) { paintSnapshot = new List<IWindowControl>(_children); }

			// Two-pass: measure non-Fill children first to determine remaining space for Fill children.
			int fixedHeight = 0;
			if (_viewportHeight > 0)
			{
				foreach (var child in paintSnapshot)
				{
					if (!child.Visible || child.VerticalAlignment == VerticalAlignment.Fill) continue;
					var node = LayoutNodeFactory.CreateSubtree(child);
					node.IsVisible = true;
					node.Measure(new LayoutConstraints(1, contentWidth, 1, int.MaxValue));
					fixedHeight += node.DesiredSize.Height;
				}
			}
			int fillCount = 0;
			foreach (var c in paintSnapshot)
			{
				if (c.Visible && c.VerticalAlignment == VerticalAlignment.Fill)
					fillCount++;
			}
			int perFillHeight = (_viewportHeight > 0 && fillCount > 0)
				? Math.Max(0, (_viewportHeight - fixedHeight) / fillCount) : _viewportHeight;

			foreach (var child in paintSnapshot)
			{
				if (!child.Visible) continue;

				// Build layout subtree (handles containers like TabControl, HorizontalGrid)
				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;

				// Measure using full layout pipeline.
				// Fill-aligned children: get remaining space after fixed children.
				// Content-sized children: measure unbounded for correct scroll positioning.
				int maxChildHeight = (_viewportHeight > 0 && child.VerticalAlignment == VerticalAlignment.Fill)
					? perFillHeight : int.MaxValue;
				var constraints = new LayoutConstraints(1, contentWidth, 1, maxChildHeight);
				childNode.Measure(constraints);
				int childHeight = childNode.DesiredSize.Height;

				// Register child bounds for cursor position lookups (even if off-viewport)
				var childBoundsForCursor = new LayoutRect(
					contentOriginX,
					contentOriginY + currentY,
					contentWidth,
					childHeight);
				renderer?.UpdateChildBounds(child, childBoundsForCursor);

				// Only render if in viewport
				if (currentY + childHeight > 0 && currentY < _viewportHeight)
				{
					var childBounds = new LayoutRect(
						contentOriginX,
						contentOriginY + currentY,
						contentWidth,
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
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight && _viewportHeight > 0)
			{
				DrawVerticalScrollbar(buffer, bounds, fgColor, bgColor);
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
			Color thumbColor = _hasFocus ? Color.Cyan1 : Color.Grey;
			Color trackColor = _hasFocus ? Color.Grey : Color.Grey23;

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

		#endregion

		#region Border Drawing

		private void DrawTopBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, bool preserveBg = false)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int innerWidth = width - 2;

			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = preserveBg ? buffer.GetCell(x, y).Background : bgColor;
				buffer.SetNarrowCell(x, y, box.TopLeft, borderColor, cellBg);
			}

			if (string.IsNullOrEmpty(_header) || innerWidth < 4)
			{
				for (int i = 0; i < innerWidth; i++)
				{
					int px = x + 1 + i;
					if (px >= clipRect.X && px < clipRect.Right)
					{
						var cellBg = preserveBg ? buffer.GetCell(px, y).Background : bgColor;
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
							var cellBg = preserveBg ? buffer.GetCell(px, y).Background : bgColor;
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
							var cellBg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}

					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					foreach (var cell in headerCells)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							if (preserveBg)
							{
								var cellBg = buffer.GetCell(writeX, y).Background;
								buffer.SetCell(writeX, y, new Cell(cell.Character, cell.Foreground, cellBg, cell.Decorations)
								{
									IsWideContinuation = cell.IsWideContinuation,
									Combiners = cell.Combiners
								});
							}
							else
							{
								buffer.SetCell(writeX, y, cell);
							}
						}
						writeX++;
					}

					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					for (int i = 0; i < rightDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}
				}
			}

			int rightCornerX = x + width - 1;
			if (rightCornerX >= clipRect.X && rightCornerX < clipRect.Right)
			{
				var cellBg = preserveBg ? buffer.GetCell(rightCornerX, y).Background : bgColor;
				buffer.SetNarrowCell(rightCornerX, y, box.TopRight, borderColor, cellBg);
			}
		}

		private void DrawBottomBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, bool preserveBg = false)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = preserveBg ? buffer.GetCell(x, y).Background : bgColor;
				buffer.SetNarrowCell(x, y, box.BottomLeft, borderColor, cellBg);
			}

			int innerWidth = width - 2;
			for (int i = 0; i < innerWidth; i++)
			{
				int px = x + 1 + i;
				if (px >= clipRect.X && px < clipRect.Right)
				{
					var cellBg = preserveBg ? buffer.GetCell(px, y).Background : bgColor;
					buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
				}
			}

			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = preserveBg ? buffer.GetCell(rightX, y).Background : bgColor;
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
			int maxH = maxHeight > 0 ? maxHeight : int.MaxValue;

			List<IWindowControl> calcSnapshot;
			lock (_childrenLock) { calcSnapshot = new List<IWindowControl>(_children); }

			// Two-pass measurement: fixed children first, then Fill children get remaining space.
			// Pass 1: measure non-Fill children to determine fixed height.
			int fixedHeight = 0;
			int fillCount = 0;
			foreach (var child in calcSnapshot)
			{
				if (!child.Visible) continue;
				if (child.VerticalAlignment == VerticalAlignment.Fill)
				{
					fillCount++;
					continue;
				}
				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;
				var constraints = new LayoutConstraints(1, availableWidth, 1, int.MaxValue);
				childNode.Measure(constraints);
				fixedHeight += childNode.DesiredSize.Height;
			}

			// Pass 2: measure Fill children with remaining space.
			int remainingHeight = (maxH < int.MaxValue) ? Math.Max(0, maxH - fixedHeight) : int.MaxValue;
			int perFillHeight = (fillCount > 0 && remainingHeight < int.MaxValue)
				? Math.Max(0, remainingHeight / fillCount) : int.MaxValue;

			int totalHeight = fixedHeight;
			foreach (var child in calcSnapshot)
			{
				if (!child.Visible || child.VerticalAlignment != VerticalAlignment.Fill) continue;
				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;
				var constraints = new LayoutConstraints(1, availableWidth, 1, perFillHeight);
				childNode.Measure(constraints);
				totalHeight += childNode.DesiredSize.Height;
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
