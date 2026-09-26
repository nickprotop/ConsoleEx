// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Identifies which part of a scrollbar was hit by a click.
	/// </summary>
#pragma warning disable CS1591
	public enum ScrollbarHitZone
	{
		None,
		UpArrow,
		DownArrow,
		Thumb,
		TrackAbove,
		TrackBelow
	}
#pragma warning restore CS1591

	/// <summary>
	/// Shared vertical scrollbar geometry, drawing and hit testing.
	/// </summary>
	/// <remarks>
	/// Kept as public API; the implementation delegates to the internal scrollbar engine in
	/// <see cref="Scrollbar"/>, which also serves the horizontal axis and the standalone
	/// <c>ScrollbarControl</c>. Used by ListControl, TreeControl and HtmlControl — TableControl and
	/// ScrollablePanelControl use the engine directly.
	/// </remarks>
	public static class ScrollbarHelper
	{
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
			if (contentAreaHeight <= 0) return (0, 0, 0, 0);

			var metrics = new Scrollbar.ScrollbarMetrics(
				contentAreaHeight, totalItems, visibleItems, scrollOffset);

			// Content that fits keeps the historical shape: the thumb fills the whole track.
			if (totalItems <= visibleItems)
				return (0, contentAreaHeight, 0, contentAreaHeight);

			return (0, contentAreaHeight,
				Scrollbar.ScrollbarGeometry.ThumbPosForOffset(metrics),
				Scrollbar.ScrollbarGeometry.ThumbLength(metrics));
		}

		/// <summary>
		/// Draws a vertical scrollbar into the character buffer.
		/// </summary>
		public static void DrawVerticalScrollbar(
			CharacterBuffer buffer, int x, int startY, int height,
			int totalItems, int visibleItems, int scrollOffset,
			Color thumbColor, Color trackColor, Color bgColor)
		{
			if (height <= 0) return;

			var metrics = new Scrollbar.ScrollbarMetrics(height, totalItems, visibleItems, scrollOffset);
			Scrollbar.ScrollbarRenderer.Draw(
				buffer, Scrollbar.ScrollbarAxis.Vertical, x, startY, metrics,
				new Scrollbar.ScrollbarPalette(thumbColor, trackColor, bgColor));
		}

		/// <summary>
		/// Determines which zone of the scrollbar was clicked.
		/// </summary>
		/// <param name="relativeY">Y position relative to the scrollbar top.</param>
		/// <param name="trackHeight">Total scrollbar track height.</param>
		/// <param name="thumbY">Thumb start position within the track.</param>
		/// <param name="thumbHeight">Thumb height.</param>
		/// <returns>The hit zone.</returns>
		/// <remarks>
		/// Kept as its own implementation rather than delegating: its four scalar parameters cannot
		/// rebuild the full <see cref="Scrollbar.ScrollbarMetrics"/> the engine needs. This logic is
		/// mirrored in <see cref="Scrollbar.ScrollbarInput.HitTest"/> — the two must stay in step.
		/// </remarks>
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
			var metrics = new Scrollbar.ScrollbarMetrics(
				contentAreaHeight, totalItems, visibleItems, dragStartOffset);

			int startThumbPos = Scrollbar.ScrollbarGeometry.ThumbPosForOffset(metrics);
			return Scrollbar.ScrollbarInput.OffsetForDrag(metrics, startThumbPos, dragDeltaY);
		}
	}
}
