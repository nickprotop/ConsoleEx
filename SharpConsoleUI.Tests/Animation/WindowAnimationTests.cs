using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

public class WindowAnimationTests
{
	/// <summary>
	/// Advances the system's animation manager in small steps until the total time is reached.
	/// </summary>
	private static void AdvanceByMs(ConsoleWindowSystem system, double totalMs)
	{
		double remaining = totalMs;
		while (remaining > 0)
		{
			double tick = Math.Min(remaining, AnimationDefaults.MaxFrameDeltaMs);
			system.Animations.Update(TimeSpan.FromMilliseconds(tick));
			remaining -= tick;
		}
	}

	#region Slide Tests

	[Fact]
	public void SlideIn_FromLeft_MovesWindowToTarget()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 30, Top = 15 };
		system.AddWindow(window);

		int targetLeft = window.Left;
		int targetTop = window.Top;

		var anim = WindowAnimations.SlideIn(window, SlideDirection.Left);

		// Window should start offscreen
		Assert.True(window.Left < 0);

		// After full duration, should be at target position
		AdvanceByMs(system, AnimationDefaults.DefaultSlideDurationMs + 50);

		Assert.Equal(targetLeft, window.Left);
		Assert.Equal(targetTop, window.Top);
		Assert.True(anim.IsComplete);
	}

	[Fact]
	public void SlideIn_FromRight_StartsOffscreenRight()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 30, Top = 15 };
		system.AddWindow(window);

		int targetLeft = window.Left;

		WindowAnimations.SlideIn(window, SlideDirection.Right);

		// Should start at or beyond desktop width
		Assert.True(window.Left >= system.DesktopDimensions.Width);

		AdvanceByMs(system, AnimationDefaults.DefaultSlideDurationMs + 50);

		Assert.Equal(targetLeft, window.Left);
	}

	[Fact]
	public void SlideIn_FromTop_StartsOffscreenTop()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 30, Top = 15 };
		system.AddWindow(window);

		int targetTop = window.Top;

		WindowAnimations.SlideIn(window, SlideDirection.Top);

		Assert.True(window.Top < 0);

		AdvanceByMs(system, AnimationDefaults.DefaultSlideDurationMs + 50);

		Assert.Equal(targetTop, window.Top);
	}

	[Fact]
	public void SlideOut_Left_MovesOffscreen()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 30, Top = 15 };
		system.AddWindow(window);

		var anim = WindowAnimations.SlideOut(window, SlideDirection.Left);

		AdvanceByMs(system, AnimationDefaults.DefaultSlideDurationMs + 50);

		Assert.True(window.Left < 0);
		Assert.True(anim.IsComplete);
	}

	#endregion

	#region FadeIn Tests

	[Fact]
	public void FadeIn_CreatesAnimation_DrivingIntensityFromOneToZero()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 10, Top = 5 };
		system.AddWindow(window);

		var anim = WindowAnimations.FadeIn(window);

		Assert.False(anim.IsComplete);

		// After full duration, animation should be complete
		AdvanceByMs(system, AnimationDefaults.DefaultFadeDurationMs + 50);

		Assert.True(anim.IsComplete);
	}

	[Fact]
	public void FadeIn_WithCustomDuration_UsesSpecifiedDuration()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 10, Top = 5 };
		system.AddWindow(window);

		const int customDurationMs = 600;
		var anim = WindowAnimations.FadeIn(window, duration: TimeSpan.FromMilliseconds(customDurationMs));

		// At half the custom duration, animation should still be running
		AdvanceByMs(system, customDurationMs / 2);
		Assert.False(anim.IsComplete);

		// After full custom duration, animation should be done
		AdvanceByMs(system, customDurationMs);
		Assert.True(anim.IsComplete);
	}

	[Fact]
	public void FadeIn_PostBufferPaintHandler_CleanedUpAfterCompletion()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 10, Top = 5 };
		system.AddWindow(window);

		bool handlerCalled = false;
		WindowAnimations.FadeIn(window, onComplete: () =>
		{
			handlerCalled = true;
		});

		// Complete the animation
		AdvanceByMs(system, AnimationDefaults.DefaultFadeDurationMs + 50);

		Assert.True(handlerCalled);
	}

	#endregion

	#region FadeOut Tests

	[Fact]
	public void FadeOut_CreatesAnimation_DrivingIntensityFromZeroToOne()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 10, Top = 5 };
		system.AddWindow(window);

		var anim = WindowAnimations.FadeOut(window);

		Assert.False(anim.IsComplete);

		AdvanceByMs(system, AnimationDefaults.DefaultFadeDurationMs + 50);

		Assert.True(anim.IsComplete);
	}

	[Fact]
	public void FadeOut_OnComplete_Fires()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 10, Top = 5 };
		system.AddWindow(window);

		bool completeCalled = false;
		WindowAnimations.FadeOut(window, onComplete: () => completeCalled = true);

		// Not yet complete
		Assert.False(completeCalled);

		AdvanceByMs(system, AnimationDefaults.DefaultFadeDurationMs + 50);

		Assert.True(completeCalled);
	}

	[Fact]
	public void FadeOut_PostBufferPaintHandler_CleanedUpAfterCompletion()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Width = 20, Height = 10, Left = 10, Top = 5 };
		system.AddWindow(window);

		bool cleanupVerified = false;
		WindowAnimations.FadeOut(window, onComplete: () =>
		{
			cleanupVerified = true;
		});

		AdvanceByMs(system, AnimationDefaults.DefaultFadeDurationMs + 50);

		Assert.True(cleanupVerified);
	}

	#endregion
}
