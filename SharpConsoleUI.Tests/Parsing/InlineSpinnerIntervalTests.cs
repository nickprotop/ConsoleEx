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

namespace SharpConsoleUI.Tests.Parsing;

[Collection("InlineSpinner")] // shares the global MarkupSpinnerClock time seam
public class InlineSpinnerIntervalTests
{
	[Fact]
	public void CurrentFrameUsesSuppliedInterval()
	{
		try
		{
			long t = 0;
			MarkupSpinnerClock.SetTimeProviderForTests(() => t);
			int frameCount = SpinnerControl.FramesForStyle(SpinnerStyle.Dots).Length;

			// At interval 100ms, t=250 → frame (250/100)%count = 2.
			t = 250;
			Assert.Equal(2 % frameCount, MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Dots, 100));

			// At interval 250ms, same t=250 → frame (250/250)%count = 1.
			Assert.Equal(1 % frameCount, MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Dots, 250));
		}
		finally
		{
			MarkupSpinnerClock.ResetTimeProviderForTests();
		}
	}

	[Fact]
	public void TwoIntervalsAdvanceIndependentlyFromSameClock()
	{
		try
		{
			long t = 600;
			MarkupSpinnerClock.SetTimeProviderForTests(() => t);
			int frameCount = SpinnerControl.FramesForStyle(SpinnerStyle.Dots).Length;
			int fast = MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Dots, 200); // (600/200)=3
			int slow = MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Dots, 600); // (600/600)=1
			Assert.Equal(3 % frameCount, fast);
			Assert.Equal(1 % frameCount, slow);
		}
		finally
		{
			MarkupSpinnerClock.ResetTimeProviderForTests();
		}
	}

	[Fact]
	public void ReservedWidthIsIndependentOfInterval()
	{
		// Width is keyed by style only — ". .. ..." Dots reserves 3 columns regardless of interval.
		Assert.Equal(3, MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots));
	}

	[Theory]
	[InlineData("spinner dots 250", SpinnerStyle.Dots, 250)]
	[InlineData("spinner dots", SpinnerStyle.Dots, 360)]          // per-style default
	[InlineData("spinner", SpinnerStyle.Braille, 100)]            // bare → braille default
	[InlineData("spinner star 30", SpinnerStyle.Star, 30)]
	[InlineData("spinner dots foo", SpinnerStyle.Dots, 360)]      // bad token → style default
	[InlineData("spinner dots -5", SpinnerStyle.Dots, 360)]       // non-positive → style default
	[InlineData("spinner dots 0", SpinnerStyle.Dots, 360)]        // zero → style default
	public void TryParseSpinnerTagYieldsStyleAndInterval(string tag, SpinnerStyle expectedStyle, int expectedInterval)
	{
		bool ok = MarkupParser.TryParseSpinnerTagForTests(tag, out var style, out int interval);
		Assert.True(ok);
		Assert.Equal(expectedStyle, style);
		Assert.Equal(expectedInterval, interval);
	}

	[Fact]
	public void NonSpinnerTagReturnsFalse()
	{
		Assert.False(MarkupParser.TryParseSpinnerTagForTests("bold", out _, out _));
	}
}
