using SharpConsoleUI.Configuration;
using Xunit;

namespace SharpConsoleUI.Tests.Configuration;

public class WatchdogOptionsTests
{
	[Fact]
	public void InstallSynchronizationContext_DefaultsFalse_AndIsConfigurable()
	{
		// Default is false to preserve legacy behavior: awaited continuations resume on the thread
		// pool, so a handler that blocks on async work (.Result/.Wait()/.GetAwaiter().GetResult())
		// freezes-then-recovers instead of deadlocking. Installing a UI SynchronizationContext
		// (true) would capture those continuations onto the UI queue, which a blocked UI thread
		// never drains -> permanent deadlock. Confirmed external users (dotnet-skills, Cratis CLI)
		// block on async from UI handlers, so this MUST default to false to avoid breaking them.
		Assert.False(new ConsoleWindowSystemOptions().InstallSynchronizationContext);

		var opted = new ConsoleWindowSystemOptions() with { InstallSynchronizationContext = true };
		Assert.True(opted.InstallSynchronizationContext);
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
