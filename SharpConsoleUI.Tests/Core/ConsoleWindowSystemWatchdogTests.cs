using System;
using System.Threading;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

[Collection("TimingSensitive")]
public class ConsoleWindowSystemWatchdogTests
{
	private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline) { if (condition()) return true; Thread.Sleep(50); }
		return condition();
	}

	[Fact]
	public void Unresponsive_Raised_WithDrainPhase_WhenLoopBlocks()
	{
		var opts = new ConsoleWindowSystemOptions() with
		{
			Watchdog = new WatchdogOptions(StaleThresholdMs: 50, UnresponsiveThresholdMs: 120, PollIntervalMs: 40, ShowUnresponsiveBanner: false)
		};
		var sys = new ConsoleWindowSystem(RenderMode.Buffer, options: opts);

		UnresponsiveEventArgs? captured = null;
		sys.Unresponsive += (s, e) => captured = e;

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();
		sys.EnqueueOnUIThread(() => Thread.Sleep(400)); // blocks the Drain phase

		var ok = WaitFor(() => captured != null, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(ok, "Unresponsive event did not fire");
		Assert.Equal(MainLoopPhase.Drain, captured!.Phase);
	}
}
