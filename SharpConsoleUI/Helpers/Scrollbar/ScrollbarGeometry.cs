// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Helpers.Scrollbar
{
	/// <summary>
	/// Shared scrollbar maths for both axes.
	/// </summary>
	/// <remarks>
	/// Generalised from ScrollablePanelControl, whose forward map (offset → thumb cell) and inverse
	/// (thumb cell → offset) round-trip cleanly. The older per-control maths used a lossy ratio for
	/// dragging whose range wrongly included the two arrow cells, so the thumb lagged the cursor and
	/// could not reach the extremes on a short track.
	/// </remarks>
	internal static class ScrollbarGeometry
	{
		/// <summary>Track cells reserved for arrows: 2 when the track is long enough, otherwise 0.</summary>
		public static int ArrowSlots(int trackLength) =>
			trackLength >= ControlDefaults.MinArrowTrackLength ? 2 : 0;

		/// <summary>The scrollable range: content beyond the viewport, never negative.</summary>
		public static int MaxOffset(in ScrollbarMetrics m) =>
			Math.Max(0, m.ContentExtent - m.ViewportExtent);

		/// <summary>The thumb's length, from the viewport/content ratio.</summary>
		public static int ThumbLength(in ScrollbarMetrics m)
		{
			int thumbTrack = ThumbTrack(m.TrackLength);
			double ratio = (double)m.ViewportExtent / Math.Max(1, m.ContentExtent);
			int min = Math.Clamp(m.MinThumbLength, 1, thumbTrack);
			return Math.Clamp((int)(thumbTrack * ratio), min, thumbTrack);
		}

		/// <summary>
		/// The thumb's start cell within the track, including the leading arrow slot.
		/// Inverse of <see cref="OffsetForThumbPos"/>.
		/// </summary>
		public static int ThumbPosForOffset(in ScrollbarMetrics m)
		{
			int pos = ArrowSlots(m.TrackLength) > 0 ? 1 : 0;
			int maxOffset = MaxOffset(m);
			if (maxOffset <= 0) return pos;

			int maxThumbPos = ThumbTrack(m.TrackLength) - ThumbLength(m);
			if (maxThumbPos <= 0) return pos;

			double scrollRatio = (double)m.Offset / maxOffset;
			return pos + Math.Min((int)Math.Round(maxThumbPos * scrollRatio), maxThumbPos);
		}

		/// <summary>
		/// The scroll offset that puts the thumb at <paramref name="thumbPos"/>.
		/// Inverse of <see cref="ThumbPosForOffset"/>, using the same rounding so the pair round-trips.
		/// </summary>
		public static int OffsetForThumbPos(in ScrollbarMetrics m, int thumbPos)
		{
			int maxThumbPos = ThumbTrack(m.TrackLength) - ThumbLength(m);
			int maxOffset = MaxOffset(m);
			if (maxThumbPos <= 0 || maxOffset <= 0) return 0;

			int rel = Math.Clamp(ArrowSlots(m.TrackLength) > 0 ? thumbPos - 1 : thumbPos, 0, maxThumbPos);
			double scrollRatio = (double)rel / maxThumbPos;
			return Math.Min((int)Math.Round(maxOffset * scrollRatio), maxOffset);
		}

		private static int ThumbTrack(int trackLength) =>
			Math.Max(1, trackLength - ArrowSlots(trackLength));
	}
}
