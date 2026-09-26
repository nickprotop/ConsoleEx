// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class ScrollbarHelperTests
{
	[Fact]
	public void GetVerticalGeometry_PlacesTheThumbAfterTheTopArrow()
	{
		var (trackTop, trackHeight, thumbY, thumbHeight) =
			ScrollbarHelper.GetVerticalGeometry(contentAreaHeight: 12, totalItems: 100, visibleItems: 50, scrollOffset: 0);

		Assert.Equal(0, trackTop);
		Assert.Equal(12, trackHeight);
		Assert.Equal(1, thumbY);
		Assert.Equal(5, thumbHeight);
	}

	[Fact]
	public void GetVerticalGeometry_ContentThatFitsFillsTheTrack()
	{
		var (_, trackHeight, thumbY, thumbHeight) =
			ScrollbarHelper.GetVerticalGeometry(12, totalItems: 10, visibleItems: 50, scrollOffset: 0);

		Assert.Equal(12, trackHeight);
		Assert.Equal(0, thumbY);
		Assert.Equal(trackHeight, thumbHeight);
	}

	[Fact]
	public void GetVerticalGeometry_ZeroHeightIsAllZeroes()
	{
		Assert.Equal((0, 0, 0, 0), ScrollbarHelper.GetVerticalGeometry(0, 100, 50, 0));
	}

	[Theory]
	[InlineData(-1, ScrollbarHitZone.None)]
	[InlineData(0, ScrollbarHitZone.UpArrow)]
	[InlineData(11, ScrollbarHitZone.DownArrow)]
	[InlineData(1, ScrollbarHitZone.Thumb)]
	[InlineData(8, ScrollbarHitZone.TrackBelow)]
	[InlineData(12, ScrollbarHitZone.None)]
	public void HitTest_IsUnchanged(int relativeY, ScrollbarHitZone expected)
	{
		Assert.Equal(expected, ScrollbarHelper.HitTest(relativeY, trackHeight: 12, thumbY: 1, thumbHeight: 5));
	}

	/// <summary>
	/// The accepted behaviour change: the drag now reaches the last offset. The old ratio maths
	/// divided by the full track including both arrow cells, so it always fell short.
	/// </summary>
	[Fact]
	public void CalculateDragOffset_ReachesTheLastItem()
	{
		int atEnd = ScrollbarHelper.CalculateDragOffset(
			dragDeltaY: 99, dragStartOffset: 0,
			contentAreaHeight: 12, thumbHeight: 5,
			totalItems: 100, visibleItems: 50);

		Assert.Equal(50, atEnd);
	}

	/// <summary>
	/// The real shape of the bug this fix addresses, which the extreme-delta test above does NOT
	/// catch. Track 12, content 100, viewport 50 → the thumb is 5 cells long and has exactly 5 cells
	/// of travel. Dragging it those 5 cells is the user dragging the thumb to the bottom of the
	/// track, so it must land on the last item (offset 50).
	/// <para>
	/// The old ratio maths divided by <c>trackHeight - thumbHeight</c> = 7, a range that wrongly
	/// included the two arrow cells, so a full-travel drag returned 5 * 50 / 7 = 35 — the thumb was
	/// visually at the bottom while the content sat 30% short of the end. Only a delta large enough
	/// to overshoot the clamp (like 99) hid the defect.
	/// </para>
	/// </summary>
	[Fact]
	public void CalculateDragOffset_FullThumbTravel_LandsOnTheLastItem()
	{
		int atEnd = ScrollbarHelper.CalculateDragOffset(
			dragDeltaY: 5, dragStartOffset: 0,
			contentAreaHeight: 12, thumbHeight: 5,
			totalItems: 100, visibleItems: 50);

		Assert.Equal(50, atEnd);
	}

	/// <summary>
	/// Mid-drag positions track the thumb proportionally over its real travel, not over a range
	/// inflated by the arrow cells. Half the thumb's travel is half the content.
	/// </summary>
	[Theory]
	[InlineData(1, 10)]
	[InlineData(2, 20)]
	[InlineData(3, 30)]
	[InlineData(4, 40)]
	public void CalculateDragOffset_TracksTheThumbProportionally(int deltaY, int expectedOffset)
	{
		int offset = ScrollbarHelper.CalculateDragOffset(
			dragDeltaY: deltaY, dragStartOffset: 0,
			contentAreaHeight: 12, thumbHeight: 5,
			totalItems: 100, visibleItems: 50);

		Assert.Equal(expectedOffset, offset);
	}

	[Fact]
	public void CalculateDragOffset_ClampsAtZero()
	{
		Assert.Equal(0, ScrollbarHelper.CalculateDragOffset(-99, 10, 12, 5, 100, 50));
	}

	[Fact]
	public void CalculateDragOffset_IsZeroWhenNothingScrolls()
	{
		Assert.Equal(0, ScrollbarHelper.CalculateDragOffset(5, 0, 12, 12, 10, 50));
	}
}
