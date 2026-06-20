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

	[Fact]
	public void Unresponsive_Raised_WithDrainPhase_WhenLoopBlocks()
	{
		var opts = new ConsoleWindowSystemOptions() with
		{
			Watchdog = new WatchdogOptions(StaleThresholdMs: 50, UnresponsiveThresholdMs: 120, PollIntervalMs: 40, ShowUnresponsiveBanner: false)
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
			Watchdog = new WatchdogOptions(StaleThresholdMs: 50, UnresponsiveThresholdMs: 120, PollIntervalMs: 40, ShowUnresponsiveBanner: false)
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
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);
		Assert.Null(sys.FormatCurrentCallback());
	}

	[Fact]
	public void FormatCurrentCallback_FreeFormLabel_FormatsAsOpColonLabel()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);
		sys.SetFrameLabel("SaveTimer");
		Assert.Equal("UIAction: SaveTimer", sys.FormatCurrentCallback());
	}

	[Fact]
	public void FormatCurrentCallback_StructuredWindowAndControl_NamesBoth()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);
		var window = new Window(sys) { Title = "Editor" };
		var control = new SharpConsoleUI.Controls.MarkupControl(new System.Collections.Generic.List<string> { "x" });
		sys.SetFrame(window, control, UiOp.Click);
		Assert.Equal("Click on 'Editor' / MarkupControl", sys.FormatCurrentCallback());
	}

	[Fact]
	public void FormatCurrentCallback_WindowOnly_OmitsControlClause()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);
		var window = new Window(sys) { Title = "Dashboard" };
		sys.SetFrame(window, null, UiOp.Render);
		Assert.Equal("Render on 'Dashboard'", sys.FormatCurrentCallback());
	}

	// ---- UiCallbackScope restore semantics ----

	[Fact]
	public void UiCallbackScope_RestoresPreviousFrame_OnDispose()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);
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
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);

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
