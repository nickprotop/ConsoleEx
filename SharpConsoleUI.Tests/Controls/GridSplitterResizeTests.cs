// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class GridSplitterResizeTests
{
	[Fact]
	public void StarStar_RedistributesWeight_SumConserved()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Star(1), GridLength.Star(1), deltaCells: 5, sizeA: 20, sizeB: 20);
		Assert.Equal(GridUnitType.Star, a.Type);
		Assert.Equal(GridUnitType.Star, b.Type);
		Assert.Equal(2.0, a.Weight + b.Weight, 3);
		Assert.True(a.Weight > b.Weight);
		Assert.Equal(1.25, a.Weight, 3);
		Assert.Equal(0.75, b.Weight, 3);
	}

	[Fact]
	public void FixedFixed_MovesCells()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Cells(20), GridLength.Cells(20), deltaCells: 5, sizeA: 20, sizeB: 20);
		Assert.Equal(GridUnitType.Fixed, a.Type);
		Assert.Equal(GridUnitType.Fixed, b.Type);
		Assert.Equal(25, a.Value);
		Assert.Equal(15, b.Value);
	}

	[Fact]
	public void StarFixed_StarAbsorbs_FixedHeld()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Star(1), GridLength.Cells(20), deltaCells: 5, sizeA: 20, sizeB: 20);
		Assert.Equal(GridUnitType.Star, a.Type);
		Assert.Equal(GridUnitType.Fixed, b.Type);
		Assert.Equal(20, b.Value);
		Assert.Equal(1.25, a.Weight, 3);
	}

	[Fact]
	public void FixedStar_StarAbsorbs_FixedHeld()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Cells(20), GridLength.Star(1), deltaCells: 5, sizeA: 20, sizeB: 20);
		Assert.Equal(GridUnitType.Fixed, a.Type);
		Assert.Equal(GridUnitType.Star, b.Type);
		Assert.Equal(20, a.Value);
		Assert.Equal(0.75, b.Weight, 3);
	}

	[Fact]
	public void AutoLeft_BakesToFixed_ThenResizes()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Auto(), GridLength.Cells(20), deltaCells: 5, sizeA: 20, sizeB: 20);
		Assert.Equal(GridUnitType.Fixed, a.Type);
		Assert.Equal(25, a.Value);
		Assert.Equal(GridUnitType.Fixed, b.Type);
		Assert.Equal(15, b.Value);
	}

	[Fact]
	public void RespectsMin_BoundaryStopsAtNeighborMin()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Cells(20), GridLength.Cells(20, min: 18), deltaCells: 5, sizeA: 20, sizeB: 20);
		Assert.Equal(18, b.Value);
		Assert.Equal(22, a.Value);
	}

	[Fact]
	public void ZeroDelta_ReturnsUnchanged()
	{
		var (a, b) = GridSplitterResize.ApplyResize(
			GridLength.Star(1), GridLength.Star(2), deltaCells: 0, sizeA: 10, sizeB: 20);
		Assert.Equal(1.0, a.Weight, 3);
		Assert.Equal(2.0, b.Weight, 3);
	}
}
