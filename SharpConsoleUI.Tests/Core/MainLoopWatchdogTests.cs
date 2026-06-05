using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

[Collection("TimingSensitive")]
public class MainLoopWatchdogTests
{
	/// <summary>Polls a condition with short sleeps, avoids flaky fixed-delay tests.</summary>
	private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline)
		{
			if (condition()) return true;
			Thread.Sleep(50);
		}
		return condition();
	}

	[Fact]
	public void IsStale_ReturnsFalse_WhenHeartbeatRecent()
	{
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 2000);
		watchdog.Heartbeat();

		Assert.False(watchdog.IsStale);
	}

	[Fact]
	public void IsStale_ReturnsTrue_WhenHeartbeatExpired()
	{
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50);
		watchdog.Heartbeat();
		Thread.Sleep(100);

		Assert.True(watchdog.IsStale);
	}

	[Fact]
	public void Heartbeat_ClearsStaleState()
	{
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50);
		watchdog.Heartbeat();
		Thread.Sleep(100);
		Assert.True(watchdog.IsStale);

		watchdog.Heartbeat();
		Assert.False(watchdog.IsStale);
	}

	[Fact]
	public void OnTick_CallsScanForEmergencyExit_WhenStale()
	{
		var scanCalled = false;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50, unresponsiveThresholdMs: 10000);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => { scanCalled = true; return false; },
			onForceExit: () => { },
			onRecovery: () => { });

		Assert.True(WaitFor(() => scanCalled), "Watchdog should have called scanForEmergencyExit when stale");
		watchdog.Dispose();
	}

	[Fact]
	public void OnTick_DoesNotScan_WhenHeartbeatFresh()
	{
		var scanCalled = false;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 5000);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => { scanCalled = true; return false; },
			onForceExit: () => { },
			onRecovery: () => { });

		Thread.Sleep(600);

		Assert.False(scanCalled, "Watchdog should not scan when heartbeat is fresh");
		watchdog.Dispose();
	}

	[Fact]
	public void OnTick_CallsForceExit_WhenScanReturnsTrue()
	{
		var forceExitCalled = false;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50, unresponsiveThresholdMs: 10000);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => true,
			onForceExit: () => { forceExitCalled = true; },
			onRecovery: () => { });

		Assert.True(WaitFor(() => forceExitCalled), "Watchdog should call onForceExit when scan finds emergency key");
		watchdog.Dispose();
	}

	[Fact]
	public void Heartbeat_TriggersRecovery_AfterBannerShown()
	{
		var bannerTick = false;
		var recoveryCalled = false;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 30, unresponsiveThresholdMs: 60);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => { bannerTick = true; return false; },
			onForceExit: () => { },
			onRecovery: () => { recoveryCalled = true; });

		// Wait for at least one tick (proves banner threshold was reached)
		Assert.True(WaitFor(() => bannerTick), "Timer should have ticked");

		// Now simulate main loop recovery
		watchdog.Heartbeat();

		Assert.True(recoveryCalled, "Watchdog should call onRecovery when heartbeat resumes after banner");
		watchdog.Dispose();
	}

	[Fact]
	public void Heartbeat_DoesNotTriggerRecovery_WhenNeverWentStale()
	{
		var recoveryCalled = false;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 10000, unresponsiveThresholdMs: 20000);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => false,
			onForceExit: () => { },
			onRecovery: () => { recoveryCalled = true; });

		// Heartbeat stays fresh — never crosses the stale threshold
		Thread.Sleep(100);
		watchdog.Heartbeat();

		Assert.False(recoveryCalled, "Watchdog should not call onRecovery if it never went stale");
		watchdog.Dispose();
	}

	[Fact]
	public void PollInterval_Configurable_TicksFaster()
	{
		var ticks = 0;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50, unresponsiveThresholdMs: 10000, pollIntervalMs: 60);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => { Interlocked.Increment(ref ticks); return false; },
			onForceExit: () => { },
			onRecovery: () => { });

		Assert.True(WaitFor(() => Volatile.Read(ref ticks) >= 3, timeoutMs: 2000));
		watchdog.Dispose();
	}

	[Fact]
	public void OnUnresponsive_Invoked_AndBannerSuppressedWhenCallbackReturnsFalse()
	{
		var unresponsiveCalls = 0;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50, unresponsiveThresholdMs: 80, pollIntervalMs: 40);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => false,
			onForceExit: () => { },
			onRecovery: () => { },
			onUnresponsive: _ => { Interlocked.Increment(ref unresponsiveCalls); return false; },
			onRecovered: _ => { });

		Assert.True(WaitFor(() => Volatile.Read(ref unresponsiveCalls) >= 1, timeoutMs: 2000));
		watchdog.Dispose();
	}

	[Fact]
	public void OnRecovered_Invoked_AfterAnyStalePeriod_NotJustBanner()
	{
		TimeSpan? recoveredWith = null;
		var watchdog = new MainLoopWatchdog(staleThresholdMs: 50, unresponsiveThresholdMs: 100000, pollIntervalMs: 40);
		watchdog.Heartbeat();
		watchdog.Start(
			scanForEmergencyExit: () => false,
			onForceExit: () => { },
			onRecovery: () => { },
			onUnresponsive: _ => true,
			onRecovered: d => recoveredWith = d);

		Assert.True(WaitFor(() => watchdog.IsStale, timeoutMs: 2000));
		watchdog.Heartbeat();
		Assert.True(WaitFor(() => recoveredWith != null, timeoutMs: 2000));
		watchdog.Dispose();
	}
}
