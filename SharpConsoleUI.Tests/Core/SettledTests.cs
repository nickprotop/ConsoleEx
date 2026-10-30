// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Knowing when the system has finished reacting, without polling or sleeping.
/// </summary>
/// <remarks>
/// <para>
/// The existing <c>IsIdle</c> is a wall-clock timeout - "nobody typed for a
/// while" - so it can report idle while work is still queued, and it needs real
/// time to pass. Tests were polling for a window to have a height, then polling
/// again for the effect of an input. <c>IsSettled</c> answers the question they
/// were actually asking.
/// </para>
/// </remarks>
public class SettledTests
{
	private static ConsoleWindowSystem Headless(out HeadlessConsoleDriver driver)
	{
		driver = new HeadlessConsoleDriver(60, 20);
		return new ConsoleWindowSystem(driver);
	}

	[Fact]
	public void A_fresh_system_with_nothing_to_do_is_settled()
	{
		var system = Headless(out _);

		Assert.True(system.IsSettled);
	}

	[Fact]
	public void Queued_work_is_not_settled()
	{
		var system = Headless(out _);

		system.EnqueueOnUIThread(() => { });

		Assert.False(system.IsSettled);
	}

	[Fact]
	public void A_window_waiting_to_paint_is_not_settled()
	{
		var system = Headless(out _);

		new WindowBuilder(system)
			.WithTitle("W")
			.WithSize(30, 8)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("hello").Build())
			.BuildAndShow();

		// Added but never rendered: the window has work pending.
		Assert.False(system.IsSettled);
	}

	[Fact]
	public async Task WaitForFrameAsync_returns_the_next_frame_number()
	{
		var system = Headless(out var driver);

		long before = driver.FrameNumber;
		var waiting = system.WaitForFrameAsync(CancellationToken.None);

		system.Render.UpdateDisplay();

		long got = await waiting.WaitAsync(System.TimeSpan.FromSeconds(5));

		Assert.True(got > before, $"frame did not advance: {before} -> {got}");
	}

	[Fact]
	public void SettleBlockers_names_what_is_holding_it()
	{
		var system = Headless(out _);

		Assert.Equal(string.Empty, system.SettleBlockers());

		system.EnqueueOnUIThread(() => { });

		// A bare false tells a timing-out caller nothing; the reason is the point.
		Assert.Contains("queued action", system.SettleBlockers());
	}

	/// <summary>
	/// The real thing: a window with a control, driven through the real input
	/// path, settling after the effect has actually been painted.
	/// </summary>
	[Fact]
	public async Task A_system_settles_after_the_work_it_was_given_is_painted()
	{
		var system = Headless(out var driver);

		var label = SharpConsoleUI.Builders.Controls.Markup().AddLine("before").Build();

		new WindowBuilder(system)
			.WithTitle("W")
			.WithSize(30, 8)
			.AddControl(label)
			.BuildAndShow();

		// Settling must include the first paint, not just the queue draining.
		await system.WaitUntilSettledAsync(CancellationToken.None)
			.WaitAsync(System.TimeSpan.FromSeconds(5));

		Assert.True(system.IsSettled);

		var text = string.Join("\n", driver.CaptureComposed()!.ToLines());
		Assert.Contains("before", text);

		// A change goes through the queue, and settling waits for its paint too.
		system.EnqueueOnUIThread(() => label.SetContent(["after"]));

		await system.WaitUntilSettledAsync(CancellationToken.None)
			.WaitAsync(System.TimeSpan.FromSeconds(5));

		text = string.Join("\n", driver.CaptureComposed()!.ToLines());
		Assert.Contains("after", text);

		// And it SURVIVES a re-render rather than being a one-frame accident.
		system.Render.UpdateDisplay();
		Assert.True(system.IsSettled);
	}

	[Fact]
	public async Task Waiting_after_a_mouse_event_waits_for_the_frame_it_caused()
	{
		// Reported from a real test suite: input sent, WaitUntilSettledAsync
		// awaited, and the effect not there yet, about eight runs in thirty.
		//
		// A mouse event is dispatched on the CALLING thread - it never enters
		// the input queue - so none of IsSettled's queue-based conditions was ever
		// false, and the wait returned without a single frame having been
		// composed. The while loop could not run even once.
		var driver = new HeadlessConsoleDriver(60, 20);
		var system = new ConsoleWindowSystem(driver);

		int clicks = 0;
		var button = new ButtonControl { Text = "Go" };
		button.Click += (_, _) => clicks++;

		new WindowBuilder(system).WithTitle("W").Maximized().AddControl(button).BuildAndShow();

		using var session = system.RunInBackground();
		await system.WaitUntilSettledAsync().WaitAsync(TimeSpan.FromSeconds(5));

		long before = driver.FrameNumber;

		// Straight through the driver, the way an input test drives one.
		driver.SimulateClick(new System.Drawing.Point(2, 2));

		await system.WaitUntilSettledAsync().WaitAsync(TimeSpan.FromSeconds(5));

		// The wait has to outlast the input: a frame composed after the event
		// is what proves the system saw it, and is the only thing a caller can
		// hang an assertion on.
		Assert.True(driver.FrameNumber > before,
			$"no frame was composed after the input: before={before} after={driver.FrameNumber}");
	}

	[Fact]
	public void A_system_is_not_settled_while_a_mouse_handler_is_running()
	{
		// The concurrent half of the same report. A mouse event is dispatched
		// synchronously on the thread that raised it and never enters a queue,
		// so without the mouse condition every "is anything queued" question
		// answers no while a handler is still running.
		//
		// Subscribed straight to the driver's own event: this is about the
		// dispatch window, not about hit-testing, and routing it through a
		// control would make a layout detail decide whether the test runs.
		var driver = new HeadlessConsoleDriver(60, 20);
		var system = new ConsoleWindowSystem(driver);

		new WindowBuilder(system).WithTitle("W").Maximized().BuildAndShow();
		system.Render.UpdateDisplay();

		int runs = 0;
		bool settledDuringHandler = true;

		driver.MouseEvent += (_, _, _) =>
		{
			runs++;
			settledDuringHandler = system.IsSettled;
		};

		driver.SimulateClick(new System.Drawing.Point(5, 5));

		Assert.True(runs > 0, "the mouse handler never ran, so nothing was measured");
		Assert.False(settledDuringHandler,
			"the system read as settled while a mouse handler was still running");
	}

	[Fact]
	public async Task Waiting_returns_on_a_system_with_nothing_to_do()
	{
		// The trap a frame floor must not fall into. Waiting for the UI to "go
		// quiet" costs the full timeout on anything with a moving part - a clock,
		// a spinner, a background simulation - because it is never quiet. The
		// floor is keepable instead: the counter advances on every composed frame,
		// changed or not, so an idle system satisfies it on the next frame.
		var driver = new HeadlessConsoleDriver(40, 10);
		var system = new ConsoleWindowSystem(driver);

		new WindowBuilder(system).WithTitle("W").Maximized().BuildAndShow();

		using var session = system.RunInBackground();

		// Settle once so nothing is outstanding, then wait again on a system that
		// has nothing whatever to do. The second wait is the one at risk of hanging.
		await system.WaitUntilSettledAsync().WaitAsync(TimeSpan.FromSeconds(5));

		long before = driver.FrameNumber;

		await system.WaitUntilSettledAsync().WaitAsync(TimeSpan.FromSeconds(5));

		// It returned, and it did wait for a frame rather than returning on the
		// spot - which is the whole of the guarantee.
		Assert.True(driver.FrameNumber > before,
			$"the wait returned without a frame: before={before} after={driver.FrameNumber}");
	}

	[Fact]
	public void A_system_is_not_settled_while_a_queued_action_is_running()
	{
		// Reported against 2.10.3: input sent, the wait returned, and the effect
		// landed just after. A queued action is taken OUT of the queue before it
		// runs, so between TryDequeue and the call the queue answers empty - and
		// the action, which is the work, is nowhere the check can see it.
		var driver = new HeadlessConsoleDriver(40, 10);
		var system = new ConsoleWindowSystem(driver);

		new WindowBuilder(system).WithTitle("W").Maximized().BuildAndShow();
		system.Render.UpdateDisplay();

		using var inside = new ManualResetEventSlim(false);
		using var release = new ManualResetEventSlim(false);
		bool settledWhileRunning = true;

		bool countedInside = false;
		// The REAL loop drains it. The test-only drain is a four line helper that
		// counts nothing, so driving through it would prove nothing about the
		// path that actually runs. Started BEFORE the action is queued, or the
		// action runs during startup and is finished before anything can ask.
		using var session = system.RunInBackground();

		system.EnqueueOnUIThread(() =>
		{
			countedInside = system.IsRunningUiAction;
			inside.Set();
			release.Wait(TimeSpan.FromSeconds(5));
		});

		Assert.True(inside.Wait(TimeSpan.FromSeconds(5)), "the action never started");
		// Read a few times: the action is blocked in release.Wait, so if the
		// counter is up at all it is up for this whole window.
		settledWhileRunning = system.IsSettled;
		release.Set();

		// Read from inside the action as well: it proves the count is around THIS
		// call, not merely up for some other reason.
		Assert.True(countedInside, "the action was not counted while it ran");

		Assert.False(settledWhileRunning,
			$"settled while a queued action ran. blockers said '{system.SettleBlockers()}'");
	}
}
