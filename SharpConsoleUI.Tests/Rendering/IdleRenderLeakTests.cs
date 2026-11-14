// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Things that must not keep the render loop awake once the screen has stopped changing.
/// </summary>
/// <remarks>
/// <para>
/// The main loop renders when any term of its <c>shouldRender</c> decision is true, and sleeps
/// up to 100ms when none is. A term that is true forever costs a frame every 16ms for the life
/// of the process: a full layout and composite that writes nothing to the terminal, which is
/// invisible on screen and plain in a CPU graph.
/// </para>
/// <para>
/// Each test here pins one such leak. They are about the IDLE state, so they assert that a
/// signal has gone quiet, never that a frame was painted.
/// </para>
/// </remarks>
public class IdleRenderLeakTests
{
	// --- A spinner must not outlive the control that owns it ---

	[Fact]
	public void DisposingASpinningSpinner_StopsItsAnimation()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "Spin", Left = 0, Top = 0, Width = 30, Height = 10 };
		system.AddWindow(window);

		var spinner = new SpinnerControl { IsSpinning = true };
		window.AddControl(spinner);
		Assert.True(system.Animations.HasActiveAnimations);

		// Already true before this test existed, via Dispose clearing Container and the
		// Container setter stopping the animation. Pinned here because that is an indirect
		// route: a future refactor that stops nulling Container on dispose would leak the
		// animation of every closed window, and nothing else would notice.
		spinner.Dispose();

		Assert.False(system.Animations.HasActiveAnimations);
	}

	[Fact]
	public void HidingASpinningSpinner_StopsItsAnimation()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "Spin", Left = 0, Top = 0, Width = 30, Height = 10 };
		system.AddWindow(window);

		var spinner = new SpinnerControl { IsSpinning = true };
		window.AddControl(spinner);
		Assert.True(system.Animations.HasActiveAnimations);

		// An invisible spinner animates nothing a person can see.
		spinner.Visible = false;

		Assert.False(system.Animations.HasActiveAnimations);
	}

	[Fact]
	public void ShowingASpinnerAgain_ResumesItsAnimation()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "Spin", Left = 0, Top = 0, Width = 30, Height = 10 };
		system.AddWindow(window);

		var spinner = new SpinnerControl { IsSpinning = true };
		window.AddControl(spinner);
		spinner.Visible = false;
		Assert.False(system.Animations.HasActiveAnimations);

		// The regression guard: hiding must not be a one-way door.
		spinner.Visible = true;

		Assert.True(system.Animations.HasActiveAnimations);
	}

	[Fact]
	public void AStoppedSpinnerThatIsShown_StaysStopped()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "Spin", Left = 0, Top = 0, Width = 30, Height = 10 };
		system.AddWindow(window);

		var spinner = new SpinnerControl { IsSpinning = false };
		window.AddControl(spinner);

		spinner.Visible = false;
		spinner.Visible = true;

		// Visibility must not start a spinner the application never started.
		Assert.False(system.Animations.HasActiveAnimations);
	}

	// --- A hidden panel's periodic elements must not force frames ---

	[Fact]
	public void AHiddenPanel_DoesNotHoldARepaintSchedule()
	{
		// The default panels carry a ClockElement, whose schedule forces one frame a second.
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = false,
			ShowBottomPanel = false,
		});

		// Nothing on screen can show the time, so nothing should be asking for frames.
		Assert.Equal(0, system.RepaintScheduleCount);
	}

	[Fact]
	public void AVisiblePanel_KeepsItsRepaintSchedule()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = true,
			ShowBottomPanel = true,
		});

		// The regression guard: a shown clock must still tick.
		Assert.True(system.RepaintScheduleCount > 0);
	}

	[Fact]
	public void HidingAPanel_ReleasesItsRepaintSchedule()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = true,
			ShowBottomPanel = true,
		});
		int whenShown = system.RepaintScheduleCount;
		Assert.True(whenShown > 0);

		system.PanelStateService.ShowTopPanel = false;
		system.PanelStateService.ShowBottomPanel = false;

		Assert.Equal(0, system.RepaintScheduleCount);
	}

	[Fact]
	public void ShowingAPanelAgain_RestoresItsRepaintSchedule()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = false,
			ShowBottomPanel = false,
		});
		Assert.Equal(0, system.RepaintScheduleCount);

		system.PanelStateService.ShowTopPanel = true;

		Assert.True(system.RepaintScheduleCount > 0);
	}

	// --- Draining the UI action queue must not force a desktop render by itself ---

	[Fact]
	public void DrainingAUIActionThatChangesNothing_DoesNotForceADesktopRender()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "Queue", Left = 0, Top = 0, Width = 30, Height = 10 };
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.DesktopNeedsRender = false;

		// An action that touches nothing drawable - a counter, a log line, a callback.
		// The actions that DO change the screen invalidate what they changed, so the
		// queue itself has no business claiming the whole desktop needs repainting:
		// an application posting work every frame was pinned at the frame cap by this.
		int ran = 0;
		system.EnqueueOnUIThread(() => ran++);
		system.DrainPendingUIActionsForTest();

		Assert.Equal(1, ran);
		Assert.False(system.Render.DesktopNeedsRender);
	}

	[Fact]
	public void AUIActionThatInvalidatesAWindow_StillGetsItsFrame()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "Queue", Left = 0, Top = 0, Width = 30, Height = 10 };
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		// The regression guard: work that changes the screen must still be painted. It
		// travels as the window's own invalidation, which is the dirty signal that matters.
		system.EnqueueOnUIThread(() => window.Invalidate(Invalidation.Relayout));
		system.DrainPendingUIActionsForTest();

		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}
}
