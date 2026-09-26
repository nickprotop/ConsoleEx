// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers.Scrollbar
{
	/// <summary>
	/// Shared scrollbar hit testing and the maps from a click or drag to a new scroll offset.
	/// </summary>
	/// <remarks>
	/// Zones come from the public <see cref="ScrollbarHitZone"/>, whose names are vertical because it
	/// predates horizontal support. On a horizontal bar read <c>UpArrow</c> as left, <c>DownArrow</c>
	/// as right, <c>TrackAbove</c> as before the thumb and <c>TrackBelow</c> as after it. The names
	/// are public API and are deliberately not changed.
	/// </remarks>
	internal static class ScrollbarInput
	{
		/// <summary>Which part of the bar a position falls on. Arrows win over the thumb.</summary>
		public static ScrollbarHitZone HitTest(in ScrollbarMetrics m, int relativePos)
		{
			if (relativePos < 0 || relativePos >= m.TrackLength)
				return ScrollbarHitZone.None;

			bool hasArrows = ScrollbarGeometry.ArrowSlots(m.TrackLength) > 0;

			if (hasArrows && relativePos == 0)
				return ScrollbarHitZone.UpArrow;
			if (hasArrows && relativePos == m.TrackLength - 1)
				return ScrollbarHitZone.DownArrow;

			int thumbPos = ScrollbarGeometry.ThumbPosForOffset(m);
			int thumbLen = ScrollbarGeometry.ThumbLength(m);

			if (relativePos >= thumbPos && relativePos < thumbPos + thumbLen)
				return ScrollbarHitZone.Thumb;

			return relativePos < thumbPos ? ScrollbarHitZone.TrackAbove : ScrollbarHitZone.TrackBelow;
		}

		/// <summary>
		/// The offset a click on <paramref name="zone"/> should produce. Arrows step by
		/// <paramref name="smallChange"/>, the track pages by <paramref name="largeChange"/>, and the
		/// thumb leaves the offset alone (a drag takes over from there).
		/// </summary>
		public static int OffsetForZone(in ScrollbarMetrics m, ScrollbarHitZone zone, int smallChange, int largeChange)
		{
			int max = ScrollbarGeometry.MaxOffset(m);

			int target = zone switch
			{
				ScrollbarHitZone.UpArrow => m.Offset - smallChange,
				ScrollbarHitZone.DownArrow => m.Offset + smallChange,
				ScrollbarHitZone.TrackAbove => m.Offset - largeChange,
				ScrollbarHitZone.TrackBelow => m.Offset + largeChange,
				_ => m.Offset,
			};

			return Math.Clamp(target, 0, max);
		}

		/// <summary>
		/// The offset for a thumb dragged <paramref name="delta"/> cells from
		/// <paramref name="dragStartThumbPos"/>. Anchoring on the thumb cell and inverting keeps the
		/// thumb under the cursor and lets it reach both extremes.
		/// </summary>
		public static int OffsetForDrag(in ScrollbarMetrics m, int dragStartThumbPos, int delta) =>
			ScrollbarGeometry.OffsetForThumbPos(m, dragStartThumbPos + delta);
	}
}
