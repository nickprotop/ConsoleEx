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

/// <summary>
/// The frame-rate cap, which is a bandwidth control before it is a smoothness one.
/// </summary>
/// <remarks>
/// A terminal UI writes bytes to a pipe, and over SSH an uncapped redraw is the
/// difference between a usable session and a saturated link. The interval was
/// computed with integer division, so 144 fps asked for 6 ms and got 166, and
/// anything above 1000 asked for 0 ms and got no cap at all.
/// </remarks>
public class FrameRateCapTests
{
	[Theory]
	[InlineData(10, 100.0)]
	[InlineData(30, 33.333)]
	[InlineData(60, 16.667)]
	[InlineData(120, 8.333)]
	[InlineData(144, 6.944)]   // was 6, i.e. 166 fps
	[InlineData(240, 4.167)]
	public void The_interval_is_the_one_the_rate_asks_for(int fps, double expectedMs)
	{
		var options = ConsoleWindowSystemOptions.Create() with { TargetFPS = fps };

		Assert.Equal(expectedMs, options.MinFrameTime, 2);
	}

	[Fact]
	public void The_default_is_sixty()
	{
		var options = ConsoleWindowSystemOptions.Create();

		Assert.Equal(60, options.TargetFPS);
		Assert.Equal(16.667, options.MinFrameTime, 2);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-1000)]
	public void A_rate_of_zero_or_less_means_no_cap(int fps)
	{
		// Not an accidental 16 ms via a fallback: asking for no limit should BE
		// no limit, so the loop renders whenever something is dirty.
		var options = ConsoleWindowSystemOptions.Create() with { TargetFPS = fps };

		Assert.Equal(0.0, options.MinFrameTime);
	}

	[Fact]
	public void An_absurd_rate_is_clamped_rather_than_dividing_to_nothing()
	{
		// 1000/1001 was 0 in integer arithmetic, which silently removed the cap.
		var options = ConsoleWindowSystemOptions.Create() with { TargetFPS = 100_000 };

		Assert.True(options.MinFrameTime > 0,
			"an absurd rate should still cap something, not remove the cap");
		Assert.True(options.TargetFPS <= 1000,
			"the rate itself should be clamped, so what is reported is what is used");
	}

	[Fact]
	public void The_positional_constructor_clamps_too()
	{
		// The record's positional parameter and the clamping property share a
		// name. If the parameter bypassed the property, a caller who constructs
		// directly rather than through Create() would keep the raw value and
		// the uncapped interval that comes with it.
		var options = new ConsoleWindowSystemOptions(TargetFPS: 100_000);

		Assert.Equal(ConsoleWindowSystemOptions.MaxTargetFPS, options.TargetFPS);
		Assert.True(options.MinFrameTime > 0);
	}
}
