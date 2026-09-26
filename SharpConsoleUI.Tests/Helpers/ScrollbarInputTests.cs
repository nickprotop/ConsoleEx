// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class ScrollbarInputTests
{
	// Track 12, content 100, viewport 50 → arrows at 0 and 11, thumb length 5 starting at 1.
	private static ScrollbarMetrics M(int offset = 0, int minThumb = 1)
		=> new(TrackLength: 12, ContentExtent: 100, ViewportExtent: 50, Offset: offset, MinThumbLength: minThumb);

	[Theory]
	[InlineData(-1, ScrollbarHitZone.None)]
	[InlineData(12, ScrollbarHitZone.None)]
	[InlineData(0, ScrollbarHitZone.UpArrow)]
	[InlineData(11, ScrollbarHitZone.DownArrow)]
	[InlineData(1, ScrollbarHitZone.Thumb)]
	[InlineData(5, ScrollbarHitZone.Thumb)]
	[InlineData(6, ScrollbarHitZone.TrackBelow)]
	[InlineData(10, ScrollbarHitZone.TrackBelow)]
	public void HitTest_MapsEveryCell(int relativePos, ScrollbarHitZone expected)
	{
		Assert.Equal(expected, ScrollbarInput.HitTest(M(), relativePos));
	}

	[Fact]
	public void HitTest_ReportsTrackAboveWhenTheThumbHasMovedDown()
	{
		// At max offset the thumb sits at the bottom, so a cell just under the top arrow is above it.
		Assert.Equal(ScrollbarHitZone.TrackAbove, ScrollbarInput.HitTest(M(offset: 50), 2));
	}

	[Fact]
	public void HitTest_PrefersArrowsOverTheThumb()
	{
		// A minimum thumb long enough to cover the whole thumb-track: the end cells are still arrows.
		var m = M(minThumb: 99);
		Assert.Equal(ScrollbarHitZone.UpArrow, ScrollbarInput.HitTest(m, 0));
		Assert.Equal(ScrollbarHitZone.DownArrow, ScrollbarInput.HitTest(m, 11));
	}

	[Fact]
	public void OffsetForZone_ArrowsStepBySmallChange()
	{
		Assert.Equal(7, ScrollbarInput.OffsetForZone(M(offset: 10), ScrollbarHitZone.UpArrow, smallChange: 3, largeChange: 50));
		Assert.Equal(13, ScrollbarInput.OffsetForZone(M(offset: 10), ScrollbarHitZone.DownArrow, smallChange: 3, largeChange: 50));
	}

	[Fact]
	public void OffsetForZone_TrackPagesByLargeChange()
	{
		Assert.Equal(0, ScrollbarInput.OffsetForZone(M(offset: 30), ScrollbarHitZone.TrackAbove, smallChange: 1, largeChange: 50));
		Assert.Equal(50, ScrollbarInput.OffsetForZone(M(offset: 30), ScrollbarHitZone.TrackBelow, smallChange: 1, largeChange: 50));
	}

	[Fact]
	public void OffsetForZone_ClampsToTheScrollRange()
	{
		Assert.Equal(0, ScrollbarInput.OffsetForZone(M(offset: 0), ScrollbarHitZone.UpArrow, smallChange: 5, largeChange: 50));
		Assert.Equal(50, ScrollbarInput.OffsetForZone(M(offset: 50), ScrollbarHitZone.DownArrow, smallChange: 5, largeChange: 50));
	}

	[Fact]
	public void OffsetForZone_LeavesTheOffsetAloneForThumbAndNone()
	{
		Assert.Equal(30, ScrollbarInput.OffsetForZone(M(offset: 30), ScrollbarHitZone.Thumb, 1, 50));
		Assert.Equal(30, ScrollbarInput.OffsetForZone(M(offset: 30), ScrollbarHitZone.None, 1, 50));
	}

	[Fact]
	public void OffsetForDrag_ReachesBothExtremes()
	{
		var m = M(offset: 0);
		int startPos = ScrollbarGeometry.ThumbPosForOffset(m);

		Assert.Equal(50, ScrollbarInput.OffsetForDrag(m, startPos, delta: 99));
		Assert.Equal(0, ScrollbarInput.OffsetForDrag(m, startPos, delta: -99));
	}

	[Fact]
	public void OffsetForDrag_OneCellDownMovesOneThumbStep()
	{
		var m = M(offset: 0);
		int startPos = ScrollbarGeometry.ThumbPosForOffset(m);
		int moved = ScrollbarInput.OffsetForDrag(m, startPos, delta: 1);

		// The thumb must actually follow: the new offset maps back to the next thumb cell.
		Assert.Equal(startPos + 1, ScrollbarGeometry.ThumbPosForOffset(M(offset: moved)));
	}
}
