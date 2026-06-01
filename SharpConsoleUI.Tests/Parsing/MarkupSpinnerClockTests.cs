// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

[Collection("InlineSpinner")] // serialize: these mutate the static clock time-seam
public class MarkupSpinnerClockTests
{
	private static void WithClockTime(long startTick, System.Action<System.Action<long>> body)
	{
		try
		{
			MarkupSpinnerClock.SetTimeProviderForTests(() => startTick);
			body(delta => MarkupSpinnerClock.SetTimeProviderForTests(() => startTick + delta));
		}
		finally
		{
			MarkupSpinnerClock.ResetTimeProviderForTests();
			MarkupSpinnerClock.ResetForTests();
		}
	}

	[Theory]
	[InlineData(SpinnerStyle.Braille, 1)]
	[InlineData(SpinnerStyle.Circle, 1)]
	[InlineData(SpinnerStyle.Line, 1)]
	[InlineData(SpinnerStyle.Arc, 1)]
	[InlineData(SpinnerStyle.Bounce, 1)]
	[InlineData(SpinnerStyle.Dots, 3)]
	public void ReservedWidthIsMaxFrameWidth(SpinnerStyle style, int expected)
	{
		Assert.Equal(expected, MarkupSpinnerClock.ReservedWidth(style));
	}

	[Fact]
	public void ReservedWidthIsConstantAcrossCalls()
	{
		int a = MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots);
		int b = MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots);
		Assert.Equal(a, b);
		Assert.Equal(3, a);
	}

	[Fact]
	public void CurrentFrameAdvancesWithTimeAndWraps()
	{
		WithClockTime(1_000_000, set =>
		{
			Assert.Equal(0, MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Circle)); // 4 frames
			set(ControlDefaults.SpinnerDefaultIntervalMs);
			Assert.Equal(1, MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Circle));
			set(ControlDefaults.SpinnerDefaultIntervalMs * 4);
			Assert.Equal(0, MarkupSpinnerClock.CurrentFrame(SpinnerStyle.Circle));
		});
	}

	[Fact]
	public void CurrentGlyphIsReservedWidthForEveryFrame()
	{
		WithClockTime(2_000_000, set =>
		{
			int w = MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots);
			for (int f = 0; f < 6; f++)
			{
				set(ControlDefaults.SpinnerDefaultIntervalMs * f);
				string g = MarkupSpinnerClock.CurrentGlyph(SpinnerStyle.Dots);
				Assert.Equal(w, MarkupParser.StripLength(g));
			}
		});
	}

	[Fact]
	public void IsActiveTrueAfterMarkParsedAndDecays()
	{
		WithClockTime(3_000_000, set =>
		{
			Assert.False(MarkupSpinnerClock.IsActive);
			MarkupSpinnerClock.MarkParsedForTests();
			Assert.True(MarkupSpinnerClock.IsActive);
			set(ControlDefaults.InlineSpinnerKeepAliveMs + 50);
			Assert.False(MarkupSpinnerClock.IsActive);
		});
	}

	#region Render gate contract

	[Fact]
	public void ShouldKeepRenderingReflectsActiveAndEnabled()
	{
		WithClockTime(5_000_000, set =>
		{
			MarkupSpinnerClock.MarkParsedForTests();
			// enabled + active -> keep rendering
			Assert.True(MarkupSpinnerClock.ShouldKeepRendering(animationsEnabled: true));
			// disabled -> do not keep rendering even if active
			Assert.False(MarkupSpinnerClock.ShouldKeepRendering(animationsEnabled: false));
		});
	}

	#endregion
}
