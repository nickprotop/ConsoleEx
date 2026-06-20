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

public class GridLengthTests
{
	[Fact]
	public void Cells_IsFixed_WithValue()
	{
		var g = GridLength.Cells(20);
		Assert.Equal(GridUnitType.Fixed, g.Type);
		Assert.Equal(20, g.Value);
	}

	[Fact]
	public void Auto_HasType()
	{
		Assert.Equal(GridUnitType.Auto, GridLength.Auto().Type);
	}

	[Fact]
	public void Star_DefaultWeightOne()
	{
		var g = GridLength.Star();
		Assert.Equal(GridUnitType.Star, g.Type);
		Assert.Equal(1.0, g.Weight);
	}

	[Fact]
	public void Star_CustomWeight()
	{
		Assert.Equal(2.0, GridLength.Star(2).Weight);
	}

	[Fact]
	public void MinMax_Captured()
	{
		var g = GridLength.Star(1, min: 4, max: 40);
		Assert.Equal(4, g.Min);
		Assert.Equal(40, g.Max);
	}

	[Fact]
	public void Star_NegativeWeightClampsToOne()
	{
		Assert.Equal(1.0, GridLength.Star(-5).Weight);
	}

	[Fact]
	public void Star_ZeroWeightClampsToOne()
	{
		Assert.Equal(1.0, GridLength.Star(0).Weight);
	}

	[Fact]
	public void Cells_MinMax_Captured()
	{
		var g = GridLength.Cells(10, min: 4, max: 40);
		Assert.Equal(4, g.Min);
		Assert.Equal(40, g.Max);
	}

	[Fact]
	public void MinGreaterThanMax_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => GridLength.Star(1, min: 40, max: 4));
	}
}
