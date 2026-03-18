// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region IScrollableContainer Implementation

		/// <summary>
		/// Automatically scrolls to bring a child control into view when it receives focus.
		/// This is called by the focus system when a child within this panel gains focus.
		/// </summary>
		public void ScrollChildIntoView(IWindowControl child)
		{
			// Skip if viewport hasn't been laid out yet (no dimensions to scroll within)
			if (_viewportWidth <= 0 || _viewportHeight <= 0)
				return;

			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			if (!snapshot.Contains(child))
				return; // Not our child

			// Calculate child's position within our content
			int childContentY = 0;
			int contentWidth = _viewportWidth;
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight)
				contentWidth -= 2;

			// Find child's Y position by measuring all children before it
			foreach (var c in snapshot.Where(c => c.Visible))
			{
				if (c == child)
					break;

				childContentY += MeasureChildHeight(c, contentWidth);
			}

			int childHeight = MeasureChildHeight(child, contentWidth);

			// Scroll vertically if child is outside viewport
			if (childContentY < _verticalScrollOffset)
			{
				// Child is above viewport - scroll up to show it at top
				_autoScroll = false; // Detach: focus-driven scroll overrides autoScroll
				ScrollVerticalTo(childContentY);
			}
			else if (childContentY + childHeight > _verticalScrollOffset + _viewportHeight)
			{
				// Child is below viewport - scroll down to show it at bottom
				_autoScroll = false; // Detach: focus-driven scroll overrides autoScroll
				ScrollVerticalTo(childContentY + childHeight - _viewportHeight);
			}
			// If child is already visible, don't scroll

			// Note: Horizontal scrolling not implemented for children (children typically fit width)
		}

		#endregion

		#region Scrolling Methods

		/// <summary>
		/// Scrolls the content vertically by the specified number of lines.
		/// Positive values scroll down, negative values scroll up.
		/// The offset is clamped to valid bounds automatically.
		/// </summary>
		/// <param name="lines">Number of lines to scroll (positive = down, negative = up).</param>
		public void ScrollVerticalBy(int lines)
		{
			int oldOffset = _verticalScrollOffset;
			int maxOffset = Math.Max(0, _contentHeight - _viewportHeight);
			_verticalScrollOffset = Math.Clamp(_verticalScrollOffset + lines, 0, maxOffset);

			// AutoScroll state tracking
			if (_autoScroll && lines < 0 && _verticalScrollOffset < maxOffset)
			{
				_autoScroll = false;  // Detach: user scrolled up
			}
			else if (!_autoScroll && lines > 0 && _verticalScrollOffset >= maxOffset)
			{
				_autoScroll = true;   // Re-attach: user scrolled to bottom
			}

			if (oldOffset != _verticalScrollOffset)
			{
				Invalidate(true);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Vertical, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		private void ScrollVerticalTo(int offset)
		{
			int oldOffset = _verticalScrollOffset;
			_verticalScrollOffset = Math.Clamp(offset, 0, Math.Max(0, _contentHeight - _viewportHeight));

			if (oldOffset != _verticalScrollOffset)
			{
				Invalidate(true);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Vertical, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>
		/// Scrolls the content horizontally by the specified number of characters.
		/// Positive values scroll right, negative values scroll left.
		/// The offset is clamped to valid bounds automatically.
		/// </summary>
		/// <param name="chars">Number of characters to scroll (positive = right, negative = left).</param>
		public void ScrollHorizontalBy(int chars)
		{
			int oldOffset = _horizontalScrollOffset;
			_horizontalScrollOffset = Math.Clamp(_horizontalScrollOffset + chars, 0, Math.Max(0, _contentWidth - _viewportWidth));

			if (oldOffset != _horizontalScrollOffset)
			{
				Invalidate(true);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Horizontal, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>
		/// Scrolls to the top of the content.
		/// </summary>
		public void ScrollToTop() => ScrollVerticalTo(0);

		/// <summary>
		/// Scrolls to the bottom of the content.
		/// </summary>
		public void ScrollToBottom() => ScrollVerticalTo(Math.Max(0, _contentHeight - _viewportHeight));

		/// <summary>
		/// Scrolls to a specific position.
		/// </summary>
		public void ScrollToPosition(int vertical, int horizontal = 0)
		{
			ScrollVerticalTo(vertical);
			if (_horizontalScrollMode == ScrollMode.Scroll)
			{
				_horizontalScrollOffset = Math.Clamp(horizontal, 0, Math.Max(0, _contentWidth - _viewportWidth));
				Invalidate(true);
			}
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Checks if scrolling is needed based on current or safe default dimensions.
		/// </summary>
		private bool NeedsScrolling()
		{
			// Use safe dimension checking with defaults
			int contentH = _contentHeight > 0 ? _contentHeight : CalculateContentHeightSafe();
			int viewportH = _viewportHeight > 0 ? _viewportHeight : 1;
			int contentW = _contentWidth > 0 ? _contentWidth : CalculateContentWidthSafe();
			int viewportW = _viewportWidth > 0 ? _viewportWidth : 1;

			bool needsVertical = _verticalScrollMode == ScrollMode.Scroll && contentH > viewportH;
			bool needsHorizontal = _horizontalScrollMode == ScrollMode.Scroll && contentW > viewportW;

			return needsVertical || needsHorizontal;
		}

		/// <summary>
		/// Checks if any child control can receive focus, including controls
		/// nested inside containers like HorizontalGrid.
		/// </summary>
		private bool HasFocusableChildren()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }
			return snapshot.Any(c => c.Visible && c is IInteractiveControl && CanChildReceiveFocus(c));
		}

		/// <summary>
		/// Safe version of CalculateContentHeight that doesn't rely on _viewportWidth being set.
		/// Uses a default width or last known good value.
		/// </summary>
		private int CalculateContentHeightSafe()
		{
			return CalculateContentHeight(_viewportWidth > 0 ? _viewportWidth : 100);
		}

		/// <summary>
		/// Safe version of CalculateContentWidth.
		/// </summary>
		private int CalculateContentWidthSafe()
		{
			return CalculateContentWidth();
		}

		#endregion
	}
}
