using SharpConsoleUI.Configuration;
using Xunit;

namespace SharpConsoleUI.Tests.Configuration;

public class WatchdogOptionsTests
{
	[Fact]
	public void InstallSynchronizationContext_DefaultsTrue_AndIsConfigurable()
	{
		Assert.True(new ConsoleWindowSystemOptions().InstallSynchronizationContext);

		var opted = new ConsoleWindowSystemOptions() with { InstallSynchronizationContext = false };
		Assert.False(opted.InstallSynchronizationContext);
	}

	[Fact]
	public void Defaults_MatchSystemDefaults()
	{
		var o = new WatchdogOptions();

		Assert.True(o.Enabled);
		Assert.Equal(SystemDefaults.WatchdogStaleThresholdMs, o.StaleThresholdMs);
		Assert.Equal(SystemDefaults.WatchdogUnresponsiveThresholdMs, o.UnresponsiveThresholdMs);
		Assert.Equal(SystemDefaults.WatchdogPollIntervalMs, o.PollIntervalMs);
		Assert.True(o.ShowUnresponsiveBanner);
		Assert.True(o.FullRefreshOnRecovery);
	}

	[Fact]
	public void Supports_NonDestructiveMutation()
	{
		var o = new WatchdogOptions() with { Enabled = false, StaleThresholdMs = 100 };

		Assert.False(o.Enabled);
		Assert.Equal(100, o.StaleThresholdMs);
		Assert.Equal(SystemDefaults.WatchdogUnresponsiveThresholdMs, o.UnresponsiveThresholdMs);
	}

	[Fact]
	public void ConsoleWindowSystemOptions_Watchdog_DefaultsToNull()
	{
		var o = new ConsoleWindowSystemOptions();
		Assert.Null(o.Watchdog);
	}

	[Fact]
	public void ConsoleWindowSystemOptions_AcceptsWatchdog()
	{
		var wd = new WatchdogOptions(Enabled: false);
		var o = new ConsoleWindowSystemOptions() with { Watchdog = wd };
		Assert.Same(wd, o.Watchdog);
	}
}
