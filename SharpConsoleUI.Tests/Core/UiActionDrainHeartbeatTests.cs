// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// The main loop drains the whole UI-action queue in one iteration, and the heartbeat fires once
/// per iteration. A caller that floods <see cref="ConsoleWindowSystem.EnqueueOnUIThread(Action)"/>
/// faster than the loop iterates (e.g. the BenchmarkApp's frame loop, whose await-continuation
/// re-enqueues the next frame while the drain is still running) makes ONE drain run hundreds of
/// short actions back-to-back — and the watchdog falsely flags a stall even though the UI thread is
/// actively making progress. The fix heartbeats AS the drain makes progress, so a long-but-productive
/// drain is reported as alive. A single genuinely-blocking action must still trip the watchdog
/// (the heartbeat sits BETWEEN actions), which the existing ConsoleWindowSystemWatchdogTests assert.
/// </summary>
[Collection("TimingSensitive")]
public class UiActionDrainHeartbeatTests
{
	private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline) { if (condition()) return true; Thread.Sleep(20); }
		return condition();
	}

	[Fact]
	public void FloodOfShortActions_DoesNotTripWatchdog()
	{
		var opts = new ConsoleWindowSystemOptions() with
		{
			Watchdog = new WatchdogOptions(StaleThresholdMs: 60, UnresponsiveThresholdMs: 150, PollIntervalMs: 30, ShowUnresponsiveBanner: false)
		};
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(), options: opts);

		UnresponsiveEventArgs? captured = null;
		sys.Unresponsive += (s, e) => captured = e;

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		// Flood: each action does a little work and RE-ENQUEUES the next, so the drain loop never
		// sees an empty queue — mirroring the benchmark's self-feeding frame loop. The whole flood
		// runs far longer than the 60ms stale threshold, so without a mid-drain heartbeat the loop
		// would be flagged stale. We keep it self-sustaining for ~500ms of wall time.
		int remaining = 4000;
		var sw = Stopwatch.StartNew();
		void Pump()
		{
			// ~tiny busy work per action; the POINT is many actions in one uninterrupted drain.
			Thread.SpinWait(2000);
			if (--remaining > 0 && sw.ElapsedMilliseconds < 500)
				sys.EnqueueOnUIThread(Pump);
		}
		sys.EnqueueOnUIThread(Pump);

		// Give the flood time to run well past the stale threshold.
		WaitFor(() => remaining <= 0 || sw.ElapsedMilliseconds >= 500, timeoutMs: 3000);

		sys.Shutdown(0);
		t.Join(2000);

		Assert.Null(captured); // the UI thread was busy-but-alive; no false stall
	}
}
