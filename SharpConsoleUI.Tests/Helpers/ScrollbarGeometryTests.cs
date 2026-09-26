// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers.Scrollbar;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class ScrollbarGeometryTests
{
	private static ScrollbarMetrics M(int track, int content, int viewport, int offset, int minThumb = 1)
		=> new(track, content, viewport, offset, minThumb);

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 0)]
	[InlineData(2, 0)]
	[InlineData(3, 2)]
	[InlineData(20, 2)]
	public void ArrowSlots_ReservesTwoOnlyWhenTrackIsAtLeastThree(int trackLength, int expected)
	{
		Assert.Equal(expected, ScrollbarGeometry.ArrowSlots(trackLength));
	}

	[Fact]
	public void ThumbLength_IsProportionalToTheViewportRatio()
	{
		// Track 12 → 10 thumb cells after arrows; half the content visible → 5.
		Assert.Equal(5, ScrollbarGeometry.ThumbLength(M(track: 12, content: 100, viewport: 50, offset: 0)));
	}

	[Fact]
	public void ThumbLength_IsNeverBelowOne()
	{
		Assert.Equal(1, ScrollbarGeometry.ThumbLength(M(track: 12, content: 100000, viewport: 1, offset: 0)));
	}

	[Fact]
	public void ThumbLength_HonoursTheMinimum()
	{
		Assert.Equal(4, ScrollbarGeometry.ThumbLength(M(track: 12, content: 100000, viewport: 1, offset: 0, minThumb: 4)));
	}

	[Fact]
	public void ThumbLength_NeverExceedsTheThumbTrack()
	{
		// A minimum larger than the available track is clamped down to it (10 = 12 - 2 arrows).
		Assert.Equal(10, ScrollbarGeometry.ThumbLength(M(track: 12, content: 100, viewport: 50, offset: 0, minThumb: 99)));
	}

	[Fact]
	public void ThumbPos_StartsAfterTheLeadingArrow()
	{
		Assert.Equal(1, ScrollbarGeometry.ThumbPosForOffset(M(track: 12, content: 100, viewport: 50, offset: 0)));
	}

	[Fact]
	public void ThumbPos_StartsAtZeroWhenThereAreNoArrows()
	{
		Assert.Equal(0, ScrollbarGeometry.ThumbPosForOffset(M(track: 2, content: 100, viewport: 50, offset: 0)));
	}

	[Fact]
	public void ThumbPos_ReachesTheTrackEndAtMaxOffset()
	{
		var m = M(track: 12, content: 100, viewport: 50, offset: 50);
		int thumbLen = ScrollbarGeometry.ThumbLength(m);
		// arrowSlot(1) + thumbTrack(10) - thumbLen(5) = 6
		Assert.Equal(1 + 10 - thumbLen, ScrollbarGeometry.ThumbPosForOffset(m));
	}

	/// <summary>
	/// The round trip is the whole point of the shared engine: a drag to a thumb cell must map back
	/// to an offset whose forward map returns that same cell, or the thumb fights the cursor.
	/// </summary>
	[Theory]
	[InlineData(12, 100, 50)]
	[InlineData(20, 500, 20)]
	[InlineData(5, 30, 4)]
	[InlineData(3, 10, 1)]
	public void ForwardAndInverse_RoundTrip(int track, int content, int viewport)
	{
		int maxOffset = Math.Max(0, content - viewport);
		for (int offset = 0; offset <= maxOffset; offset++)
		{
			var m = M(track, content, viewport, offset);
			int pos = ScrollbarGeometry.ThumbPosForOffset(m);
			int back = ScrollbarGeometry.OffsetForThumbPos(m, pos);
			int posAgain = ScrollbarGeometry.ThumbPosForOffset(M(track, content, viewport, back));

			Assert.Equal(pos, posAgain);
		}
	}

	[Fact]
	public void OffsetForThumbPos_ClampsBeyondBothEnds()
	{
		var m = M(track: 12, content: 100, viewport: 50, offset: 0);
		Assert.Equal(0, ScrollbarGeometry.OffsetForThumbPos(m, -50));
		Assert.Equal(50, ScrollbarGeometry.OffsetForThumbPos(m, 999));
	}

	[Fact]
	public void ContentThatFits_HasNoScrollRange()
	{
		var m = M(track: 12, content: 10, viewport: 50, offset: 0);
		Assert.Equal(0, ScrollbarGeometry.MaxOffset(m));
		Assert.Equal(0, ScrollbarGeometry.OffsetForThumbPos(m, 5));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-3)]
	public void DegenerateTrack_DoesNotThrow(int track)
	{
		var m = M(track, content: 100, viewport: 50, offset: 10);
		ScrollbarGeometry.ThumbLength(m);
		ScrollbarGeometry.ThumbPosForOffset(m);
		ScrollbarGeometry.OffsetForThumbPos(m, 0);
	}
}
