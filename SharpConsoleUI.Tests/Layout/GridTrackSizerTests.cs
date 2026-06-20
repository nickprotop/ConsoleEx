// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

public class GridTrackSizerTests
{
	[Fact]
	public void Fixed_ExactSizes()
	{
		Assert.Equal(new[] { 10, 20 }, GridTrackSizer.Size(new[] { GridLength.Cells(10), GridLength.Cells(20) }, new[] { 0, 0 }, 100, 0));
	}

	[Fact]
	public void Star_SplitsRemainderByWeight()
	{
		Assert.Equal(new[] { 10, 30 }, GridTrackSizer.Size(new[] { GridLength.Star(1), GridLength.Star(3) }, new[] { 0, 0 }, 40, 0));
	}

	[Fact]
	public void Auto_UsesContentSize_ClampedToMax()
	{
		Assert.Equal(new[] { 5, 25 }, GridTrackSizer.Size(new[] { GridLength.Auto(max: 5), GridLength.Star(1) }, new[] { 12, 0 }, 30, 0));
	}

	[Fact]
	public void Gap_ReservedBeforeStarSplit()
	{
		Assert.Equal(new[] { 10, 10 }, GridTrackSizer.Size(new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { 0, 0 }, 21, 1));
	}

	[Fact]
	public void StarMin_ClampedSurplus_RedistributesToOtherStar()
	{
		Assert.Equal(new[] { 30, 10 }, GridTrackSizer.Size(new[] { GridLength.Star(1, min: 30), GridLength.Star(1) }, new[] { 0, 0 }, 40, 0));
	}

	[Fact]
	public void Mixed_FixedAutoStar()
	{
		Assert.Equal(new[] { 10, 8, 32 }, GridTrackSizer.Size(new[] { GridLength.Cells(10), GridLength.Auto(), GridLength.Star(1) }, new[] { 0, 8, 0 }, 50, 0));
	}

	[Fact]
	public void SingleTrack_NoGapReserved()
	{
		Assert.Equal(new[] { 40 }, GridTrackSizer.Size(new[] { GridLength.Star(1) }, new[] { 0 }, 40, 5));
	}

	[Fact]
	public void StarTotal_ExactWithRounding()
	{
		var s = GridTrackSizer.Size(new[] { GridLength.Star(1), GridLength.Star(1), GridLength.Star(1) }, new[] { 0, 0, 0 }, 10, 0);
		Assert.Equal(10, s[0] + s[1] + s[2]);
	}

	[Fact]
	public void FixedOverfills_StarsCollapseToZero()
	{
		Assert.Equal(new[] { 30, 0 }, GridTrackSizer.Size(new[] { GridLength.Cells(30), GridLength.Star(1) }, new[] { 0, 0 }, 20, 0));
	}

	[Fact]
	public void StarMinsOversubscribed_TerminatesAndPinsToMins()
	{
		Assert.Equal(new[] { 8, 8 }, GridTrackSizer.Size(new[] { GridLength.Star(1, min: 8), GridLength.Star(1, min: 8) }, new[] { 0, 0 }, 10, 0));
	}
}
