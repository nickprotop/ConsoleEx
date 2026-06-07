using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class CursorBlinkClockTests
{
	[Fact]
	public void StartsOn_TogglesAfterFullRate_ResetReturnsOn()
	{
		var clock = new CursorBlinkClock();
		Assert.True(clock.IsOn(rateMs: 500));     // solid initially

		clock.Advance(300);
		Assert.True(clock.IsOn(500));             // still within first on-phase

		clock.Advance(300);                        // total 600 > 500 -> off phase
		Assert.False(clock.IsOn(500));

		clock.Advance(500);                        // into next on-phase (total 1100 -> cycle 2)
		Assert.True(clock.IsOn(500));

		clock.Advance(400);                        // total 1500 -> cycle 3 -> off
		Assert.False(clock.IsOn(500));
		clock.Reset();
		Assert.True(clock.IsOn(500));             // reset forces solid on
	}

	[Fact]
	public void NonPositiveRate_AlwaysOn()
	{
		var clock = new CursorBlinkClock();
		clock.Advance(99999);
		Assert.True(clock.IsOn(0));
	}
}
