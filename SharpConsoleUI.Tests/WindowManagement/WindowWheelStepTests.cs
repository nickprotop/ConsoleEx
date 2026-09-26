// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Issue #84: window scrolling used a hardcoded 3 while controls used DefaultScrollWheelLines (1),
/// so the same wheel moved three times faster outside a scroll panel than inside one.
/// </summary>
/// <remarks>
/// <see cref="ControlDefaults.DefaultWindowScrollWheelLines"/> has no way to express "unset" through
/// its public setter — any concrete value pins it and stops it following
/// <see cref="ControlDefaults.DefaultScrollWheelLines"/>. So restoring it to a captured "original"
/// value is not enough to undo a pin: it leaves the field pinned to that same value instead of
/// following again, which broke <see cref="WindowDefault_FollowsTheControlDefaultWhenTheAppSetsIt"/>
/// whenever an earlier test in this collection had already pinned it. Every test here unpins via
/// <c>ResetDefaultWindowScrollWheelLinesForTests</c> in its <c>finally</c> instead of reassigning a
/// captured value, so tests are independent of run order.
/// </remarks>
[Collection("WheelStepDefault")]
public class WindowWheelStepTests
{
	[Fact]
	public void WindowDefault_IsThree()
	{
		ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		try
		{
			Assert.Equal(3, ControlDefaults.DefaultWindowScrollWheelLines);
		}
		finally
		{
			ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		}
	}

	[Fact]
	public void WindowDefault_FollowsTheControlDefaultWhenTheAppSetsIt()
	{
		int originalControl = ControlDefaults.DefaultScrollWheelLines;
		ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		try
		{
			ControlDefaults.DefaultScrollWheelLines = 5;
			Assert.Equal(5, ControlDefaults.DefaultWindowScrollWheelLines);
		}
		finally
		{
			ControlDefaults.DefaultScrollWheelLines = originalControl;
			ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		}
	}

	[Fact]
	public void WindowDefault_CanBeSetIndependently()
	{
		try
		{
			ControlDefaults.DefaultWindowScrollWheelLines = 7;
			Assert.Equal(7, ControlDefaults.DefaultWindowScrollWheelLines);
		}
		finally
		{
			ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-2)]
	public void WindowDefault_ClampsToAtLeastOne(int value)
	{
		try
		{
			ControlDefaults.DefaultWindowScrollWheelLines = value;
			Assert.Equal(1, ControlDefaults.DefaultWindowScrollWheelLines);
		}
		finally
		{
			ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		}
	}
}
