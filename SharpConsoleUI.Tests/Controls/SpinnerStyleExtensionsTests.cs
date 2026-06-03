// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class SpinnerStyleExtensionsTests
{
	[Theory]
	[MemberData(nameof(AllStyles))]
	public void FrameWidthMatchesReservedWidth(SpinnerStyle style)
	{
		// The extension is thin sugar — it must agree with the single source of truth.
		Assert.Equal(MarkupSpinnerClock.ReservedWidth(style), style.FrameWidth());
	}

	[Theory]
	[MemberData(nameof(AllStyles))]
	public void FrameWidthIsAtLeastOne(SpinnerStyle style)
	{
		// Unlike the original switch (which returned 0 on a miss), every style reserves >= 1 column.
		Assert.True(style.FrameWidth() >= 1);
	}

	[Fact]
	public void FrameWidthWithRequestedMinimumClampsUp()
	{
		Assert.Equal(3, SpinnerStyle.Dots.FrameWidth());        // natural
		Assert.Equal(5, SpinnerStyle.Dots.FrameWidth(5));       // wider minimum honored
		Assert.Equal(3, SpinnerStyle.Dots.FrameWidth(2));       // narrower request clamped up
		Assert.Equal(3, SpinnerStyle.Dots.FrameWidth(0));       // non-positive → natural
	}

	public static TheoryData<SpinnerStyle> AllStyles()
	{
		var data = new TheoryData<SpinnerStyle>();
		foreach (SpinnerStyle style in System.Enum.GetValues(typeof(SpinnerStyle)))
			data.Add(style);
		return data;
	}
}
