using System;
using System.Threading;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
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

	/// <summary>
	/// The watchdog must stop when the loop it watches stops.
	/// </summary>
	/// <remarks>
	/// It used to be released only by <c>Dispose()</c>, which a host driving frames itself never has
	/// to call. The timer then kept sampling a heartbeat nothing was beating any more, concluded the
	/// loop was hung, and escalated to the force-exit path — which called
	/// <see cref="Environment.Exit"/>. Inside a test host that killed the whole run: reported from a
	/// Windows box as "Test Run Aborted, ~50 tests never execute", with the abort point moving
	/// between runs because it was a leaked timer rather than any one test.
	/// </remarks>
	[Fact]
	public void Watchdog_StopsWithTheSession_AndDoesNotFireAfterwards()
	{
		var opts = new ConsoleWindowSystemOptions() with
		{
			Watchdog = new WatchdogOptions(
				StaleThresholdMs: 40, UnresponsiveThresholdMs: 80, PollIntervalMs: 20,
				ShowUnresponsiveBanner: false, AllowProcessExit: false)
		};
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(), options: opts) { BlockWhenIdle = false };

		int unresponsiveAfterStop = 0;
		using (var session = sys.BeginHosted())
			session.Tick();

		// The session is over. Anything the watchdog raises from here on concerns a loop that no
		// longer exists, and in the shipped code escalates to terminating the process.
		sys.Unresponsive += (s, e) => Interlocked.Increment(ref unresponsiveAfterStop);
		Thread.Sleep(400); // ~20 poll intervals, 5x the unresponsive threshold

		Assert.Equal(0, Volatile.Read(ref unresponsiveAfterStop));
	}

	/// <summary>
	/// A host that owns the process must be able to refuse having it terminated.
	/// </summary>
	/// <remarks>
	/// The force-exit path calls <see cref="Environment.Exit"/>, which is correct for a standalone
	/// terminal app — the process is the app — and unacceptable for a library embedded in a test
	/// runner, service, or GUI host. This asserts the option exists and defaults to preserving the
	/// historical behaviour; the effect itself cannot be asserted in-process, since the failing case
	/// would take the test host down with it.
	/// </remarks>
	[Fact]
	public void AllowProcessExit_DefaultsTrue_AndIsOptOut()
	{
		Assert.True(new WatchdogOptions().AllowProcessExit);
		Assert.False((new WatchdogOptions() with { AllowProcessExit = false }).AllowProcessExit);
	}

	[Fact]
	public void Unresponsive_Raised_WithDrainPhase_WhenLoopBlocks()
	{
		var opts = new ConsoleWindowSystemOptions() with
		{
			Watchdog = new WatchdogOptions(
				StaleThresholdMs: 50, UnresponsiveThresholdMs: 120, PollIntervalMs: 40,
				ShowUnresponsiveBanner: false,
				// These tests deliberately stall the loop past the stale threshold. Without this the
				// watchdog's force-exit path is live, and one queued Ctrl+Q/Ctrl+C would call
				// Environment.Exit(1) and take the test host down with it.
				AllowProcessExit: false)
		};
		// Drive a HEADLESS console: Run() renders into an in-memory buffer, not the real terminal.
		// A real NetConsoleDriver here writes screen renders to stdout; under CI (stdout is a pipe)
		// the ANSI volume stalls the test-host output pipe and trips --blame-hang. The watchdog logic
		// under test is driver-agnostic. (Headless shutdown verified: Run()+Shutdown() exits in ~230ms.)
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(), options: opts);

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

	[Fact]
	public void Unresponsive_BlockedIn_NamesLabelledQueuedAction()
	{
		var opts = new ConsoleWindowSystemOptions() with
		{
			Watchdog = new WatchdogOptions(
				StaleThresholdMs: 50, UnresponsiveThresholdMs: 120, PollIntervalMs: 40,
				ShowUnresponsiveBanner: false,
				// These tests deliberately stall the loop past the stale threshold. Without this the
				// watchdog's force-exit path is live, and one queued Ctrl+Q/Ctrl+C would call
				// Environment.Exit(1) and take the test host down with it.
				AllowProcessExit: false)
		};
		// Headless driver — see note in Unresponsive_Raised_WithDrainPhase_WhenLoopBlocks.
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(), options: opts);

		UnresponsiveEventArgs? captured = null;
		sys.Unresponsive += (s, e) => captured = e;

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();
		// Labelled queued action that blocks the Drain phase — label should surface in BlockedIn.
		sys.EnqueueOnUIThread(() => Thread.Sleep(400), label: "SaveTimer");

		var ok = WaitFor(() => captured != null, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(ok, "Unresponsive event did not fire");
		Assert.Equal(MainLoopPhase.Drain, captured!.Phase);
		Assert.Equal("UIAction: SaveTimer", captured.BlockedIn);
	}

	// ---- FormatCurrentCallback formatting matrix (internal, exercised directly) ----

	[Fact]
	public void FormatCurrentCallback_ReturnsNull_WhenNoFrameSet()
	{
		var sys = new ConsoleWindowSystem(new HeadlessConsoleDriver());
		Assert.Null(sys.FormatCurrentCallback());
	}

	[Fact]
	public void FormatCurrentCallback_FreeFormLabel_FormatsAsOpColonLabel()
	{
		var sys = new ConsoleWindowSystem(new HeadlessConsoleDriver());
		sys.SetFrameLabel("SaveTimer");
		Assert.Equal("UIAction: SaveTimer", sys.FormatCurrentCallback());
	}

	[Fact]
	public void FormatCurrentCallback_StructuredWindowAndControl_NamesBoth()
	{
		var sys = new ConsoleWindowSystem(new HeadlessConsoleDriver());
		var window = new Window(sys) { Title = "Editor" };
		var control = new SharpConsoleUI.Controls.MarkupControl(new System.Collections.Generic.List<string> { "x" });
		sys.SetFrame(window, control, UiOp.Click);
		Assert.Equal("Click on 'Editor' / MarkupControl", sys.FormatCurrentCallback());
	}

	[Fact]
	public void FormatCurrentCallback_WindowOnly_OmitsControlClause()
	{
		var sys = new ConsoleWindowSystem(new HeadlessConsoleDriver());
		var window = new Window(sys) { Title = "Dashboard" };
		sys.SetFrame(window, null, UiOp.Render);
		Assert.Equal("Render on 'Dashboard'", sys.FormatCurrentCallback());
	}

	// ---- UiCallbackScope restore semantics ----

	[Fact]
	public void UiCallbackScope_RestoresPreviousFrame_OnDispose()
	{
		var sys = new ConsoleWindowSystem(new HeadlessConsoleDriver());
		var window = new Window(sys) { Title = "Outer" };
		sys.SetFrame(window, null, UiOp.Key);

		using (new UiCallbackScope(sys, "Inner"))
		{
			Assert.Equal("UIAction: Inner", sys.FormatCurrentCallback());
		}

		// Outer frame restored after the scope exits.
		Assert.Equal("Key on 'Outer'", sys.FormatCurrentCallback());
	}

	[Fact]
	public void UiCallbackScope_Nested_InnermostWins_OuterRestored()
	{
		var sys = new ConsoleWindowSystem(new HeadlessConsoleDriver());

		using (new UiCallbackScope(sys, "A"))
		{
			Assert.Equal("UIAction: A", sys.FormatCurrentCallback());
			using (new UiCallbackScope(sys, "B"))
			{
				Assert.Equal("UIAction: B", sys.FormatCurrentCallback());
			}
			Assert.Equal("UIAction: A", sys.FormatCurrentCallback());
		}

		Assert.Null(sys.FormatCurrentCallback());
	}
}
