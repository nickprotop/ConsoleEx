// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

public class LayoutConstraintsTests
{
	// A hosting cell collapsing to zero (e.g. a grid track animated to 0 width) produces a degenerate
	// constraint like Min=1, Max=0. LayoutConstraints must normalize Max up to Min so that every consumer's
	// Math.Clamp(value, MinWidth, MaxWidth) is safe (Math.Clamp throws when min > max). Regression for the
	// crash found driving the grid-animation demo (BarGraphControl.MeasureDOM threw "'1' cannot be > 0").
	[Fact]
	public void Constructor_DegenerateMaxBelowMin_ClampsMaxUpToMin()
	{
		var c = new LayoutConstraints(1, 0, 1, 0);
		Assert.Equal(1, c.MinWidth);
		Assert.Equal(1, c.MaxWidth);   // normalized: was 0
		Assert.Equal(1, c.MinHeight);
		Assert.Equal(1, c.MaxHeight);  // normalized: was 0
	}

	[Fact]
	public void Constructor_NormalizedConstraints_AreSafeForMathClamp()
	{
		var c = new LayoutConstraints(5, 0, 3, 0);
		// Would throw if Max < Min slipped through.
		var w = Math.Clamp(10, c.MinWidth, c.MaxWidth);
		var h = Math.Clamp(10, c.MinHeight, c.MaxHeight);
		Assert.Equal(5, w);
		Assert.Equal(3, h);
	}

	[Fact]
	public void Constructor_ValidConstraints_LeftUnchanged()
	{
		var c = new LayoutConstraints(2, 40, 1, 24);
		Assert.Equal(2, c.MinWidth);
		Assert.Equal(40, c.MaxWidth);
		Assert.Equal(1, c.MinHeight);
		Assert.Equal(24, c.MaxHeight);
	}

	[Fact]
	public void NamedArguments_StillCompileAndNormalize()
	{
		// Source-compat: callers used named args matching the old positional record params.
		var c = new LayoutConstraints(MinWidth: 1, MaxWidth: 0, MinHeight: 1, MaxHeight: 0);
		Assert.Equal(1, c.MaxWidth);
		Assert.Equal(1, c.MaxHeight);
	}
}
