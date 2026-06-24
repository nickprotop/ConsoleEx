using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Xunit;
// The WindowResized event carries SharpConsoleUI.Helpers.Size (the framework alias),
// NOT System.Drawing.Size. Alias it to match the framework.
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Tests for the framework-level <see cref="ConsoleWindowSystem.WindowResized"/> hook: it must fire on the
/// UI thread after the framework's pass-1 reposition/invalidate, and the framework must re-invalidate all
/// windows (pass 2) AFTER the handler returns — so consumers no longer need to invalidate manually.
/// </summary>
public class WindowResizedTests
{
	private const int InitialWidth = 80;
	private const int InitialHeight = 25;
	private const int ResizedWidth = 100;
	private const int ResizedHeight = 30;

	/// <summary>
	/// Builds a headless system with the resize path wired (as Run() would), plus one registered window.
	/// Returns the driver so the test can drive <see cref="HeadlessConsoleDriver.SimulateScreenResize"/>.
	/// </summary>
	private static (ConsoleWindowSystem system, HeadlessConsoleDriver driver, Window window) BuildSystem()
	{
		var driver = new HeadlessConsoleDriver(InitialWidth, InitialHeight);
		var system = new ConsoleWindowSystem(driver);
		// Wire the resize handler the same way Run() does, without the blocking main loop.
		system.WireScreenResizeForTest();

		var window = new Window(system) { Title = "Resize Test", Width = 40, Height = 12 };
		system.AddWindow(window);

		return (system, driver, window);
	}

	/// <summary>
	/// Renders the window over a non-empty visible region, which runs the real layout pipeline and
	/// consumes the window's pending work back to <see cref="FrameWork.None"/>.
	/// </summary>
	private static void RenderToDrainPendingWork(Window window)
	{
		window.RenderAndGetVisibleContent(new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
	}

	[Fact]
	public void WindowResized_FiresOnUiThread_AfterResize()
	{
		var (system, driver, _) = BuildSystem();

		int callCount = 0;
		Size observed = default;
		system.WindowResized += (_, size) =>
		{
			callCount++;
			observed = size;
		};

		driver.SimulateScreenResize(ResizedWidth, ResizedHeight);
		// Nothing should have run yet — the reflow is enqueued onto the UI queue.
		Assert.Equal(0, callCount);

		system.DrainPendingUIActionsForTest();

		Assert.Equal(1, callCount);
		Assert.Equal(ResizedWidth, observed.Width);
		Assert.Equal(ResizedHeight, observed.Height);
	}

	[Fact]
	public void WindowResized_FrameworkReinvalidatesAfterHandler_NoManualInvalidate()
	{
		var (system, driver, window) = BuildSystem();

		// Drain the window's pending work to None first.
		RenderToDrainPendingWork(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Handler mutates app state but calls NO Invalidate.
		bool handlerRan = false;
		system.WindowResized += (_, _) => handlerRan = true;

		driver.SimulateScreenResize(ResizedWidth, ResizedHeight);
		system.DrainPendingUIActionsForTest();

		Assert.True(handlerRan);
		// Pass 2 (framework-owned) re-invalidated the window at Relayout, despite the handler not invalidating.
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void WindowResized_ReflowRunsBeforeReinvalidate()
	{
		var (system, driver, window) = BuildSystem();

		RenderToDrainPendingWork(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Handler appends a control as its reflow work (no manual Invalidate of the window).
		var added = new MarkupControl(new List<string> { "reflowed" });
		system.WindowResized += (_, _) => window.AddControl(added);

		driver.SimulateScreenResize(ResizedWidth, ResizedHeight);
		system.DrainPendingUIActionsForTest();

		// The reflow's control mutation is present...
		Assert.Contains(added, window.GetControls());
		// ...AND the window is invalidated (pass 2 ran AFTER the reflow — FIFO queue ordering).
		Assert.NotEqual(FrameWork.None, window.PendingWork);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void NoSubscriber_NoSecondInvalidatePass()
	{
		var (system, driver, window) = BuildSystem();

		// No WindowResized subscriber.
		RenderToDrainPendingWork(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		var ex = Record.Exception(() =>
		{
			driver.SimulateScreenResize(ResizedWidth, ResizedHeight);
			system.DrainPendingUIActionsForTest();
		});
		Assert.Null(ex);

		// Pass 1 (existing behavior, under the render lock) invalidated the window during the resize.
		// It was Relayout; the key point is NO pass-2 churn was enqueued. After a fresh render+drain the
		// window settles back to None and stays there (no extra re-invalidate appears from a phantom pass 2).
		RenderToDrainPendingWork(window);
		system.DrainPendingUIActionsForTest();
		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void ThrowingHandler_DoesNotBreakResize_AndPass2StillRuns()
	{
		var (system, driver, window) = BuildSystem();

		RenderToDrainPendingWork(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		bool secondHandlerRan = false;
		system.WindowResized += (_, _) => throw new System.InvalidOperationException("boom");
		system.WindowResized += (_, _) => secondHandlerRan = true;

		var ex = Record.Exception(() =>
		{
			driver.SimulateScreenResize(ResizedWidth, ResizedHeight);
			system.DrainPendingUIActionsForTest();
		});

		// One throwing handler must not break resize.
		Assert.Null(ex);
		// The second handler still ran.
		Assert.True(secondHandlerRan);
		// Pass 2 still re-invalidated despite the first handler throwing.
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void ScreenResized_StillFires_BackwardCompat()
	{
		var (_, driver, _) = BuildSystem();

		int rawCount = 0;
		Size observed = default;
		driver.ScreenResized += (_, size) =>
		{
			rawCount++;
			observed = size;
		};

		driver.SimulateScreenResize(ResizedWidth, ResizedHeight);

		Assert.Equal(1, rawCount);
		Assert.Equal(ResizedWidth, observed.Width);
		Assert.Equal(ResizedHeight, observed.Height);
	}
}
