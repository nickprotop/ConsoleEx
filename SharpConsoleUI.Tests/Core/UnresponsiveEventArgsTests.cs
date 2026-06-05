using System;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class UnresponsiveEventArgsTests
{
	[Fact]
	public void Unresponsive_ExposesDiagnostics_AndSettableBanner()
	{
		var ts = DateTime.UtcNow;
		var e = new UnresponsiveEventArgs(TimeSpan.FromSeconds(5), MainLoopPhase.Input, "key:ButtonControl", ts, showBanner: true);

		Assert.Equal(TimeSpan.FromSeconds(5), e.StalledFor);
		Assert.Equal(MainLoopPhase.Input, e.Phase);
		Assert.Equal("key:ButtonControl", e.BlockedIn);
		Assert.Equal(ts, e.TimestampUtc);
		Assert.True(e.ShowBanner);

		e.ShowBanner = false;
		Assert.False(e.ShowBanner);
	}

	[Fact]
	public void Recovered_ExposesDuration_AndSettableFullRefresh()
	{
		var e = new RecoveredEventArgs(TimeSpan.FromSeconds(7), DateTime.UtcNow, fullRefresh: true);

		Assert.Equal(TimeSpan.FromSeconds(7), e.WasStalledFor);
		Assert.True(e.FullRefresh);

		e.FullRefresh = false;
		Assert.False(e.FullRefresh);
	}
}
