// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests;

[Collection("TimingSensitive")]
public class WindowThreadOnUITests
{
	// Poll a condition to a deadline (mirrors SynchronizationContextInstalledTests.WaitFor).
	private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline) { if (condition()) return true; Thread.Sleep(25); }
		return condition();
	}

	// A background (default) window thread must NOT run on the UI thread.
	[Fact]
	public async Task AsyncWindowThread_RunsOffUIThread()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);

		var tcs = new TaskCompletionSource<bool>();
		var window = new Window(system, async (win, ct) =>
		{
			// Before any await: background thread, so not the UI thread.
			tcs.TrySetResult(system.IsOnUIThread);
			await Task.CompletedTask;
		});
		system.AddWindow(window);

		bool onUiThread = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.False(onUiThread); // background Task.Run -> not UI thread
	}

	// A UI-affine window thread (onUIThread: true) runs on the UI thread both BEFORE and AFTER an await.
	// Driven via a real Run() loop on a background thread: IsOnUIThread only resolves true against the
	// thread captured inside Run(), so the affinity proof requires the live loop, not a bare queue pump.
	[Fact]
	public void UIWindowThread_RunsOnUIThread_BeforeAndAfterAwait()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer); // headless buffer render

		bool? beforeAwaitOnUi = null;
		bool? afterAwaitOnUi = null;

		// Constructing the window with onUIThread: true enqueues its start action; the loop runs it
		// under a ConsoleUISynchronizationContext so the continuation resumes on the UI thread too.
		var window = new Window(sys, async (win, ct) =>
		{
			beforeAwaitOnUi = sys.IsOnUIThread; // synchronous prologue on the UI thread
			await Task.Delay(10, ct);
			afterAwaitOnUi = sys.IsOnUIThread;  // continuation Posted back to the UI thread
		}, onUIThread: true);

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		// Add the window on the UI thread (z-order/focus mutations belong on the loop thread).
		sys.EnqueueOnUIThread(() => sys.AddWindow(window));

		var ok = WaitFor(() => beforeAwaitOnUi.HasValue && afterAwaitOnUi.HasValue, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(ok, "UI-affine window delegate did not run both before and after the await");
		Assert.True(beforeAwaitOnUi == true, "prologue should be on the UI thread");
		Assert.True(afterAwaitOnUi == true, "continuation should be on the UI thread after the await");
	}

	// Companion (non-breaking proof): the DEFAULT delegate ctor runs OFF the UI thread even under the
	// same live Run() harness, so the additive UI-affine path did not change existing behavior.
	[Fact]
	public void DefaultWindowThread_RunsOffUIThread_UnderRunLoop()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer); // headless buffer render

		bool? observedOnUi = null;
		var window = new Window(sys, async (win, ct) =>
		{
			observedOnUi = sys.IsOnUIThread; // background Task.Run -> not the UI thread
			await Task.CompletedTask;
		}); // default ctor -> onUIThread: false

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		sys.EnqueueOnUIThread(() => sys.AddWindow(window));

		var ok = WaitFor(() => observedOnUi.HasValue, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(ok, "default window delegate never ran");
		Assert.False(observedOnUi!.Value, "default delegate must run off the UI thread");
	}

	// Setting BOTH thread methods on the builder is a configuration error caught at Build().
	[Fact]
	public void Builder_BothThreadMethods_ThrowsOnBuild()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);

		var builder = new SharpConsoleUI.Builders.WindowBuilder(system)
			.WithAsyncWindowThread((win, ct) => Task.CompletedTask)
			.WithWindowThreadOnUI((win, ct) => Task.CompletedTask);

		var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
		Assert.Contains("only one window thread", ex.Message);
	}

	// The builder's WithWindowThreadOnUI must produce a UI-affine window whose delegate runs on the UI
	// thread. Driven via a real Run() loop on a background thread because IsOnUIThread only resolves true
	// against the thread captured inside Run() (see UIWindowThread_RunsOnUIThread_BeforeAndAfterAwait).
	[Fact]
	public void Builder_WithWindowThreadOnUI_RunsOnUIThread()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer); // headless buffer render

		bool? observedOnUi = null;
		var window = new SharpConsoleUI.Builders.WindowBuilder(sys)
			.WithTitle("UI Thread")
			.WithWindowThreadOnUI(async (win, ct) =>
			{
				await Task.Delay(10, ct);
				observedOnUi = sys.IsOnUIThread; // continuation Posted back to the UI thread
			})
			.Build();

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		sys.EnqueueOnUIThread(() => sys.AddWindow(window));

		var ok = WaitFor(() => observedOnUi.HasValue, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(ok, "builder UI-affine window delegate never ran");
		Assert.True(observedOnUi!.Value, "builder WithWindowThreadOnUI delegate must run on the UI thread");
	}

	// Real-thing end-to-end: a UI-affine window thread mutates a REAL control (MarkupControl) DIRECTLY,
	// AFTER an await, with NO EnqueueOnUIThread. This is the whole point of the per-window opt-in — on a
	// UI-affine thread you may touch controls without marshalling. We then prove the mutation actually
	// PAINTS (the sentinel appears in the window's rendered lines) and SURVIVES a re-render, with no
	// data-race exception.
	//
	// Harness: driven via a real Run() loop on a background thread (IsOnUIThread and the whole render
	// path only resolve correctly against the thread captured inside Run()). RenderAndGetVisibleContent
	// is captured ON the loop thread via EnqueueOnUIThread and marshalled back — the test thread never
	// touches the window's render state concurrently with the loop.
	[Fact]
	public void UIWindowThread_DirectMutation_RendersAndSurvives()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer); // headless buffer render

		var markup = new MarkupControl(new List<string> { "initial" });
		Exception? delegateError = null;
		bool mutated = false;

		var window = new SharpConsoleUI.Builders.WindowBuilder(sys)
			.WithTitle("Direct")
			.WithSize(60, 10)
			.AddControl(markup)
			.WithWindowThreadOnUI(async (win, ct) =>
			{
				try
				{
					await Task.Delay(10, ct);
					// Direct mutation — NO EnqueueOnUIThread. Safe: we are on the UI thread.
					markup.SetContent(new List<string> { "UNIQUE_FROM_UI_THREAD" });
					mutated = true;
				}
				catch (Exception ex) { delegateError = ex; }
			})
			.Build();

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		sys.EnqueueOnUIThread(() => sys.AddWindow(window));

		// Wait for the direct mutation to have run on the UI thread.
		var mutatedOk = WaitFor(() => mutated || delegateError != null, timeoutMs: 3000);

		// Capture #1: render on the loop thread and marshal the lines back.
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		List<string>? captured1 = null;
		sys.EnqueueOnUIThread(() => captured1 = window.RenderAndGetVisibleContent(region));
		var cap1Ok = WaitFor(() => captured1 != null, timeoutMs: 3000);

		// Capture #2: a second, independent render proves the mutation survives a re-render.
		List<string>? captured2 = null;
		sys.EnqueueOnUIThread(() => captured2 = window.RenderAndGetVisibleContent(region));
		var cap2Ok = WaitFor(() => captured2 != null, timeoutMs: 3000);

		sys.Shutdown(0);
		t.Join(2000);

		Assert.Null(delegateError);
		Assert.True(mutatedOk, "the UI-affine delegate never performed the direct mutation");
		Assert.True(cap1Ok, "first render capture never completed");
		Assert.True(cap2Ok, "second render capture never completed");
		Assert.Contains(captured1!, l => l.Contains("UNIQUE_FROM_UI_THREAD"));
		Assert.Contains(captured2!, l => l.Contains("UNIQUE_FROM_UI_THREAD"));
	}

	// Regression (close-during-gap): a UI-affine window sets _windowThreadCts immediately but assigns
	// _windowTask LATER, inside the queued "WindowThreadOnUI:start" action. If Close() runs in that gap
	// (cts set, task still null), the Close guard (_windowThreadCts != null && _windowTask != null) is
	// false and Close falls through to set _isClosing = true WITHOUT cancelling the CTS. The start action
	// must then bail out and NOT run the delegate on the already-closed window.
	//
	// We force the ordering by closing the window BEFORE starting the Run() loop: constructing the
	// UI-affine window enqueues its start action, and Close() on the test thread (window not yet added,
	// _windowTask still null) trips the orphan path and sets _isClosing = true. When the loop then drains
	// the queued start action, the new guard (if (_isClosing || token.IsCancellationRequested) return;)
	// must prevent the delegate from ever running.
	[Fact]
	public void UIWindowThread_ClosedDuringStartGap_DoesNotRunDelegate()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer); // headless buffer render

		bool delegateRan = false;
		var window = new SharpConsoleUI.Builders.WindowBuilder(sys)
			.WithTitle("Closed In Gap")
			.WithWindowThreadOnUI(async (win, ct) =>
			{
				delegateRan = true; // set at the very start of the delegate
				await Task.CompletedTask;
			})
			.Build();

		// Close on the test thread BEFORE the loop drains the queued start action. The window is not
		// added yet and _windowTask is still null, so Close() takes the orphan path and sets _isClosing.
		window.Close();

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		// Let the loop start and drain the queued "WindowThreadOnUI:start" action, then settle.
		WaitFor(() => delegateRan, timeoutMs: 1000);

		sys.Shutdown(0);
		t.Join(2000);

		Assert.False(delegateRan, "the UI-affine delegate must NOT run on a window closed during the start gap");
	}

	// Affinity holds regardless of the global InstallSynchronizationContext setting. The per-window
	// opt-in is the whole point: IsOnUIThread must be true inside the UI-affine delegate even with
	// InstallSynchronizationContext == false (the default). Driven via a real Run() loop.
	[Fact]
	public void UIWindowThread_UIAffinity_IndependentOfGlobalSyncContext()
	{
		var options = new ConsoleWindowSystemOptions() with { InstallSynchronizationContext = false };
		var sys = new ConsoleWindowSystem(RenderMode.Buffer, options); // headless buffer render

		bool? observedOnUi = null;
		var window = new SharpConsoleUI.Builders.WindowBuilder(sys)
			.WithWindowThreadOnUI(async (win, ct) =>
			{
				await Task.Delay(10, ct);
				observedOnUi = sys.IsOnUIThread; // must be true despite global sync-context OFF
			})
			.Build();

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		sys.EnqueueOnUIThread(() => sys.AddWindow(window));

		var ok = WaitFor(() => observedOnUi.HasValue, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.False(options.InstallSynchronizationContext, "guard: this test must run with the global sync-context OFF");
		Assert.True(ok, "UI-affine window delegate never ran");
		Assert.True(observedOnUi!.Value, "UI affinity must hold even with InstallSynchronizationContext == false");
	}
}
