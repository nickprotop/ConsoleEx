// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

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
			int availableWidth = Math.Max(1, width - Margin.Left - Margin.Right);

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
				height = contentHeight + Margin.Top + Margin.Bottom;
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

			_viewportHeight = bounds.Height - Margin.Top - Margin.Bottom;
			_viewportWidth = bounds.Width - Margin.Left - Margin.Right;

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
					bounds.X + Margin.Left,
					bounds.Y + Margin.Top + currentY,
					contentWidth,
					childHeight);
				renderer?.UpdateChildBounds(child, childBoundsForCursor);

				// Only render if in viewport
				if (currentY + childHeight > 0 && currentY < _viewportHeight)
				{
					var childBounds = new LayoutRect(
						bounds.X + Margin.Left,
						bounds.Y + Margin.Top + currentY,
						contentWidth,
						childHeight);

					// Arrange in screen coordinates (so AbsoluteBounds are correct)
					childNode.Arrange(childBounds);

					// Create clipped clipRect for child that excludes scrollbar area and clips to viewport
					var viewportRect = new LayoutRect(
						bounds.X + Margin.Left,
						bounds.Y + Margin.Top,
						needsScrollbar ? contentWidth + 1 : contentWidth, // +1 for gap if scrollbar visible
						_viewportHeight);

					var childClipRect = clipRect.Intersect(viewportRect);

					if (needsScrollbar)
					{
						// Further restrict to exclude scrollbar columns
						int maxRight = bounds.X + Margin.Left + contentWidth + 1; // +1 for gap
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

			// Draw vertical scrollbar if content exceeds viewport
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight)
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
			// For Right position: last column of the control = margin.Left + viewport + margin.Right - 1
			// This matches the old DrawVerticalScrollbar which used bounds.Right - 1 = bounds.X + bounds.Width - 1
			int scrollbarRelX = _scrollbarPosition == ScrollbarPosition.Right
				? Margin.Left + _viewportWidth + Margin.Right - 1
				: Margin.Left;
			int scrollbarTop = Margin.Top;
			int scrollbarHeight = _viewportHeight;

			double viewportRatio = (double)_viewportHeight / _contentHeight;
			int thumbHeight = Math.Max(1, (int)(scrollbarHeight * viewportRatio));
			double scrollRatio = _contentHeight > _viewportHeight
				? (double)_verticalScrollOffset / (_contentHeight - _viewportHeight)
				: 0;
			int thumbY = (int)((scrollbarHeight - thumbHeight) * scrollRatio);

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

				buffer.SetCell(scrollbarX, scrollbarAbsTop + y, ch, color, bgColor);
			}

			// Draw scroll indicators at top/bottom
			if (_verticalScrollOffset > 0)
			{
				buffer.SetCell(scrollbarX, scrollbarAbsTop, '\u25b2', thumbColor, bgColor);
			}
			if (_verticalScrollOffset < _contentHeight - _viewportHeight)
			{
				buffer.SetCell(scrollbarX, scrollbarAbsTop + scrollbarHeight - 1, '\u25bc', thumbColor, bgColor);
			}
		}

		#endregion

		#region Content Measurement

		private int CalculateContentHeight(int viewportWidth, int maxHeight = 0)
		{
			int availableWidth = viewportWidth;
			int maxH = maxHeight > 0 ? maxHeight : int.MaxValue;

			// Reserve space for scrollbar if we might need it
			// This is an approximation - we'll recalculate if needed
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll)
			{
				availableWidth = Math.Max(1, viewportWidth - 1);
			}

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
