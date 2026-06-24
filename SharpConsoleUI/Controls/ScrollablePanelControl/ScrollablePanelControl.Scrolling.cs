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
		/// <remarks>Horizontal scrolling is not applied for children (children typically fit the width).</remarks>
		public void ScrollChildIntoView(IWindowControl child)
		{
			// Skip if viewport hasn't been laid out yet (no dimensions to scroll within).
			// Mark as pending — PaintDOM will call ScrollChildIntoView on first valid render.
			if (_viewportWidth <= 0 || _viewportHeight <= 0)
			{
				_pendingScrollToFocused = true;
				return;
			}

			if (!TryGetChildSlotBounds(child, out int top, out int height))
				return; // Not our child (or not visible)

			ScrollRangeIntoView(top, height);
		}

		/// <summary>
		/// Scrolls so that a sub-region of <paramref name="child"/> is visible. The region is given in the
		/// child's own content coordinates: <paramref name="childRelativeTop"/> rows from the child's top,
		/// spanning <paramref name="regionHeight"/> rows. Mirrors <see cref="ScrollChildIntoView"/>'s clamp,
		/// applied to the region instead of the whole child — used to bring a focused element's row into view
		/// when the child itself does not scroll.
		/// </summary>
		/// <param name="child">The (direct) child whose sub-region should be made visible.</param>
		/// <param name="childRelativeTop">Row offset of the region from the child's top edge. A negative value is clamped to 0.</param>
		/// <param name="regionHeight">Height of the region in rows. A value less than 1 is treated as 1.</param>
		public void ScrollChildRegionIntoView(IWindowControl child, int childRelativeTop, int regionHeight)
		{
			// Mirror ScrollChildIntoView's pre-layout guard.
			if (_viewportWidth <= 0 || _viewportHeight <= 0)
			{
				_pendingScrollToFocused = true;
				return;
			}

			if (!TryGetChildSlotBounds(child, out int top, out int _))
				return; // Not our child (or not visible)

			ScrollRangeIntoView(top + Math.Max(0, childRelativeTop), Math.Max(1, regionHeight));
		}

		/// <summary>
		/// Finds the content-space top and height of a direct child in the current layout.
		/// Uses the shared layout so the Y/height agree with paint. Returns false if not present.
		/// </summary>
		private bool TryGetChildSlotBounds(IWindowControl child, out int top, out int height)
		{
			top = 0;
			height = 0;
			foreach (var slot in GetVisibleChildLayout(VisibleContentWidth))
			{
				if (slot.Control == child)
				{
					top = slot.Top;
					height = slot.Height;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Scrolls vertically so the content-space range [regionTop, regionTop+regionHeight) is visible.
		/// A region taller than the viewport aligns to its top (aligning its bottom would jump past content
		/// the user hasn't seen yet). Focus-driven scrolling detaches <see cref="AutoScroll"/>.
		/// </summary>
		private void ScrollRangeIntoView(int regionTop, int regionHeight)
		{
			if (regionTop < _verticalScrollOffset)
			{
				_autoScroll = false; // Detach: focus-driven scroll overrides autoScroll
				ScrollVerticalTo(regionTop);
			}
			else if (regionTop + regionHeight > _verticalScrollOffset + VisibleContentHeight)
			{
				_autoScroll = false; // Detach: focus-driven scroll overrides autoScroll
				int target = regionHeight > VisibleContentHeight
					? regionTop
					: regionTop + regionHeight - VisibleContentHeight;
				ScrollVerticalTo(target);
			}
			// If the range is already visible, don't scroll.
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
			// Re-sync viewport/content metrics from this panel's ARRANGED node bounds before the
			// clamp reads _viewportHeight/_contentHeight. A structural change (AddControl/ClearContents)
			// triggers a MEASURE pass that resolves metrics against the unbounded full-content box,
			// leaving _viewportHeight == content height; without this sync a programmatic scroll right
			// after mutating content would clamp against the stale viewport and reset to ~0. No-op when
			// the panel has no arranged node (detached/unit-test path).
			SyncMetricsFromArrangedBounds();

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
				Invalidate(Invalidation.Relayout);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Vertical, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		private void ScrollVerticalTo(int offset)
		{
			int oldOffset = _verticalScrollOffset;
			_verticalScrollOffset = Math.Clamp(offset, 0, Math.Max(0, _contentHeight - _viewportHeight));

			if (oldOffset != _verticalScrollOffset)
			{
				Invalidate(Invalidation.Relayout);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Vertical, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>
		/// Recomputes <see cref="_contentHeight"/> from the current children when the viewport
		/// is known. The cached height is otherwise only refreshed during <see cref="PaintDOM"/>,
		/// so callers that scroll right after mutating children (without an intervening paint)
		/// would otherwise compute their target from a stale height. Returns true if the viewport
		/// is laid out (height is now current); false if it is not yet known.
		/// </summary>
		private bool RefreshContentHeightIfLaidOut()
		{
			if (_viewportWidth <= 0 || _viewportHeight <= 0)
				return false;
			_contentHeight = CalculateContentHeight(_viewportWidth, VisibleContentHeight);
			return true;
		}

		/// <summary>
		/// Scrolls the content horizontally by the specified number of characters.
		/// Positive values scroll right, negative values scroll left.
		/// The offset is clamped to valid bounds automatically.
		/// </summary>
		/// <param name="chars">Number of characters to scroll (positive = right, negative = left).</param>
		/// <remarks>No-op when <see cref="HorizontalScrollMode"/> is <see cref="ScrollMode.None"/>,
		/// consistent with <see cref="ScrollToPosition"/>.</remarks>
		public void ScrollHorizontalBy(int chars)
		{
			if (_horizontalScrollMode != ScrollMode.Scroll)
				return;

			// Re-sync metrics from the arranged bounds before the clamp reads MaxHorizontalScrollOffset
			// (which derives from _viewportWidth/_contentWidth, also written by a MEASURE-pass
			// ResolveContentMetrics). No-op when the panel has no arranged node.
			SyncMetricsFromArrangedBounds();

			int oldOffset = _horizontalScrollOffset;
			_horizontalScrollOffset = Math.Clamp(_horizontalScrollOffset + chars, 0, MaxHorizontalScrollOffset);

			if (oldOffset != _horizontalScrollOffset)
			{
				Invalidate(Invalidation.Relayout);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Horizontal, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>
		/// Scrolls horizontally to an absolute character offset (clamped to valid bounds).
		/// No-op when <see cref="HorizontalScrollMode"/> is not <see cref="ScrollMode.Scroll"/>.
		/// </summary>
		private void ScrollHorizontalTo(int offset)
		{
			if (_horizontalScrollMode != ScrollMode.Scroll)
				return;

			int oldOffset = _horizontalScrollOffset;
			_horizontalScrollOffset = Math.Clamp(offset, 0, MaxHorizontalScrollOffset);

			if (oldOffset != _horizontalScrollOffset)
			{
				Invalidate(Invalidation.Relayout);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Horizontal, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>True when vertical content overflows and vertical scrolling is enabled.</summary>
		private bool VerticalIsScrollable =>
			_verticalScrollMode == ScrollMode.Scroll && _contentHeight > VisibleContentHeight;

		/// <summary>True when horizontal content overflows and horizontal scrolling is enabled.</summary>
		private bool HorizontalIsScrollable =>
			_horizontalScrollMode == ScrollMode.Scroll && _contentWidth > VisibleContentWidth;

		/// <summary>
		/// Scrolls to the top of the content.
		/// </summary>
		public void ScrollToTop() => ScrollVerticalTo(0);

		/// <summary>
		/// Scrolls to the bottom of the content.
		/// </summary>
		/// <remarks>
		/// This is a one-shot scroll — it does not enable <see cref="AutoScroll"/>. When called
		/// before the panel has been laid out (e.g. immediately after <c>AddWindow</c>), the scroll
		/// is deferred to the first paint. When called right after adding content, the content
		/// height is recomputed on demand so the new content is included in the target.
		/// </remarks>
		public void ScrollToBottom()
		{
			// Re-sync metrics from the arranged bounds so VisibleContentHeight reflects the real
			// on-screen viewport (not a MEASURE-pass content-sized value). No-op when detached.
			SyncMetricsFromArrangedBounds();

			if (!RefreshContentHeightIfLaidOut())
			{
				// Viewport not laid out yet — defer to the first valid paint.
				_pendingScrollToBottom = true;
				return;
			}
			ScrollVerticalTo(Math.Max(0, _contentHeight - VisibleContentHeight));
		}

		/// <summary>
		/// Scrolls to a specific position.
		/// </summary>
		public void ScrollToPosition(int vertical, int horizontal = 0)
		{
			// Re-sync metrics from the arranged bounds before both clamps (vertical via ScrollVerticalTo,
			// horizontal via MaxHorizontalScrollOffset) read potentially MEASURE-stale metrics. No-op when
			// detached.
			SyncMetricsFromArrangedBounds();

			ScrollVerticalTo(vertical);
			if (_horizontalScrollMode == ScrollMode.Scroll)
			{
				_horizontalScrollOffset = Math.Clamp(horizontal, 0, MaxHorizontalScrollOffset);
				Invalidate(Invalidation.Relayout);
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
