// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using Xunit;

namespace SharpConsoleUI.Tests.Configuration;

[Collection("WheelStepDefault")]
public class ControlDefaultsTests
{
	[Fact]
	public void DefaultScrollWheelLines_DefaultsToOne()
	{
		Assert.Equal(1, ControlDefaults.DefaultScrollWheelLines);
	}

	[Fact]
	public void DefaultScrollWheelLines_IsSettable()
	{
		int original = ControlDefaults.DefaultScrollWheelLines;
		try
		{
			ControlDefaults.DefaultScrollWheelLines = 5;
			Assert.Equal(5, ControlDefaults.DefaultScrollWheelLines);
		}
		finally
		{
			ControlDefaults.DefaultScrollWheelLines = original;
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void DefaultScrollWheelLines_ClampsToAtLeastOne(int value)
	{
		int original = ControlDefaults.DefaultScrollWheelLines;
		try
		{
			ControlDefaults.DefaultScrollWheelLines = value;
			Assert.Equal(1, ControlDefaults.DefaultScrollWheelLines);
		}
		finally
		{
			ControlDefaults.DefaultScrollWheelLines = original;
		}
	}
}
