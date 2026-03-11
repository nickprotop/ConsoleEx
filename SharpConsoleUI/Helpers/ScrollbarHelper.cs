// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Identifies which part of a scrollbar was hit by a click.
	/// </summary>
	public enum ScrollbarHitZone
	{
		None,
		UpArrow,
		DownArrow,
		Thumb,
		TrackAbove,
		TrackBelow
	}

	/// <summary>
	/// Shared scrollbar geometry, drawing, and hit testing logic.
	/// Used by ListControl, TreeControl, and TableControl.
	/// </summary>
	public static class ScrollbarHelper
	{
		private const char ThumbChar = '\u2588';    // █
		private const char TrackChar = '\u2502';    // │
		private const char UpArrowChar = '\u25b2';  // ▲
		private const char DownArrowChar = '\u25bc'; // ▼
		private const int MinArrowTrackHeight = 3;

		/// <summary>
		/// Calculates vertical scrollbar geometry relative to the content area.
		/// </summary>
		/// <param name="contentAreaHeight">Total height available for the scrollbar in rows.</param>
		/// <param name="totalItems">Total number of items/rows in the content.</param>
		/// <param name="visibleItems">Number of items/rows visible in the viewport.</param>
		/// <param name="scrollOffset">Current scroll offset (first visible item index).</param>
		/// <returns>Geometry tuple: trackTop, trackHeight, thumbY, thumbHeight.</returns>
		public static (int trackTop, int trackHeight, int thumbY, int thumbHeight)
			GetVerticalGeometry(int contentAreaHeight, int totalItems, int visibleItems, int scrollOffset)
		{
			int trackTop = 0;
			int trackHeight = contentAreaHeight;
			if (trackHeight <= 0) return (0, 0, 0, 0);

			if (totalItems <= visibleItems) return (trackTop, trackHeight, 0, trackHeight);

			// Reserve first and last positions for arrows
			int arrowSlots = trackHeight >= MinArrowTrackHeight ? 2 : 0;
			int thumbTrackHeight = trackHeight - arrowSlots;
			if (thumbTrackHeight <= 0) return (trackTop, trackHeight, 0, trackHeight);

			double viewportRatio = (double)visibleItems / totalItems;
			int thumbHeight = Math.Clamp((int)(thumbTrackHeight * viewportRatio), 1, thumbTrackHeight);
			double scrollRatio = (double)scrollOffset / Math.Max(1, totalItems - visibleItems);
			int thumbY = arrowSlots > 0 ? 1 : 0; // start after top arrow
			int maxThumbPos = thumbTrackHeight - thumbHeight;
			thumbY += Math.Min((int)(maxThumbPos * scrollRatio), maxThumbPos);

			return (trackTop, trackHeight, thumbY, thumbHeight);
		}

		/// <summary>
		/// Draws a vertical scrollbar into the character buffer.
		/// </summary>
		public static void DrawVerticalScrollbar(
			CharacterBuffer buffer, int x, int startY, int height,
			int totalItems, int visibleItems, int scrollOffset,
			Color thumbColor, Color trackColor, Color bgColor)
		{
			var (trackTop, trackHeight, thumbY, thumbHeight) =
				GetVerticalGeometry(height, totalItems, visibleItems, scrollOffset);
			if (trackHeight <= 0) return;

			bool hasArrows = trackHeight >= MinArrowTrackHeight;

			for (int y = 0; y < trackHeight; y++)
			{
				int absY = startY + trackTop + y;
				if (y >= thumbY && y < thumbY + thumbHeight)
				{
					buffer.SetCell(x, absY, ThumbChar, thumbColor, bgColor);
				}
				else
				{
					buffer.SetCell(x, absY, TrackChar, trackColor, bgColor);
				}
			}

			// Arrow indicators at fixed positions (first and last)
			if (hasArrows)
			{
				buffer.SetCell(x, startY + trackTop, UpArrowChar, thumbColor, bgColor);
				buffer.SetCell(x, startY + trackTop + trackHeight - 1, DownArrowChar, thumbColor, bgColor);
			}
		}

		/// <summary>
		/// Determines which zone of the scrollbar was clicked.
		/// </summary>
		/// <param name="relativeY">Y position relative to the scrollbar top.</param>
		/// <param name="trackHeight">Total scrollbar track height.</param>
		/// <param name="thumbY">Thumb start position within the track.</param>
		/// <param name="thumbHeight">Thumb height.</param>
		/// <returns>The hit zone.</returns>
		public static ScrollbarHitZone HitTest(int relativeY, int trackHeight, int thumbY, int thumbHeight)
		{
			if (relativeY < 0 || relativeY >= trackHeight)
				return ScrollbarHitZone.None;

			bool hasArrows = trackHeight >= MinArrowTrackHeight;

			if (hasArrows && relativeY == 0)
				return ScrollbarHitZone.UpArrow;

			if (hasArrows && relativeY == trackHeight - 1)
				return ScrollbarHitZone.DownArrow;

			if (relativeY >= thumbY && relativeY < thumbY + thumbHeight)
				return ScrollbarHitZone.Thumb;

			if (relativeY < thumbY)
				return ScrollbarHitZone.TrackAbove;

			return ScrollbarHitZone.TrackBelow;
		}

		/// <summary>
		/// Calculates a new scroll offset from a thumb drag operation.
		/// </summary>
		/// <param name="dragDeltaY">Pixels dragged from start position.</param>
		/// <param name="dragStartOffset">Scroll offset when drag began.</param>
		/// <param name="contentAreaHeight">Total scrollbar height.</param>
		/// <param name="thumbHeight">Current thumb height.</param>
		/// <param name="totalItems">Total number of items.</param>
		/// <param name="visibleItems">Number of visible items.</param>
		/// <returns>New scroll offset, clamped to valid range.</returns>
		public static int CalculateDragOffset(
			int dragDeltaY, int dragStartOffset,
			int contentAreaHeight, int thumbHeight,
			int totalItems, int visibleItems)
		{
			int maxOffset = Math.Max(0, totalItems - visibleItems);
			int trackRange = Math.Max(1, contentAreaHeight - thumbHeight);

			if (maxOffset <= 0) return 0;

			int newOffset = dragStartOffset + (int)(dragDeltaY * (double)maxOffset / trackRange);
			return Math.Clamp(newOffset, 0, maxOffset);
		}
	}
}
