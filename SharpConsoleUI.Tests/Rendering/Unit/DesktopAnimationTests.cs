// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Core;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit;

/// <summary>
/// An animated desktop background advances on the animation clock.
/// </summary>
/// <remarks>
/// It used to own a timer, and then, briefly, a per-frame repaint schedule.
/// The schedule was wrong in a way only a running application showed: dragging
/// a window renders at full rate, and the background redrew on every one of
/// those frames, so the animation visibly sped up while the mouse moved.
/// AnimationManager exists for exactly this - it advances by a CAPPED delta,
/// so a busy loop cannot make an animation jump and an idle one cannot make it
/// lurch.
/// </remarks>
public class DesktopAnimationTests
{
	private static DesktopBackgroundService Animated(
		AnimationManager animations, out TimeSpan lastElapsed)
	{
		TimeSpan seen = TimeSpan.Zero;

		var service = new DesktopBackgroundService(
			() => new ModernGrayTheme(),
			() => { },
			animations);

		service.Render(10, 5);
		service.Config = new DesktopBackgroundConfig
		{
			PaintCallback = (buffer, width, height, elapsed) => seen = elapsed,
			AnimationIntervalMs = 70,
		};

		lastElapsed = seen;
		return service;
	}

	[Fact]
	public void It_registers_with_the_animation_manager()
	{
		var animations = new AnimationManager();

		Assert.False(animations.HasActiveAnimations);

		var service = Animated(animations, out _);

		Assert.True(animations.HasActiveAnimations,
			"an animated background should keep the loop rendering");

		service.Dispose();

		Assert.False(animations.HasActiveAnimations,
			"disposing should remove it");
	}

	[Fact]
	public void A_fast_loop_does_not_make_it_run_fast()
	{
		// THE REGRESSION. Sixty frames of 1 ms each is 60 ms of real time. The
		// animation must advance by about that, not by sixty intervals' worth.
		var animations = new AnimationManager();
		var service = Animated(animations, out _);

		for (int i = 0; i < 60; i++)
			animations.Update(TimeSpan.FromMilliseconds(1));

		TimeSpan elapsed = Elapsed(service);

		Assert.InRange(elapsed.TotalMilliseconds, 50, 70);

		service.Dispose();
	}

	[Fact]
	public void A_stalled_loop_does_not_make_it_jump()
	{
		// The other half: one frame after a two second stall must not advance the
		// animation two seconds, or it lurches. AnimationManager caps the delta.
		var animations = new AnimationManager();
		var service = Animated(animations, out _);

		animations.Update(TimeSpan.FromSeconds(2));

		Assert.True(Elapsed(service).TotalMilliseconds <= 40,
			"a long delta should be capped, not applied whole");

		service.Dispose();
	}

	[Fact]
	public void Turning_animations_off_stops_it()
	{
		var animations = new AnimationManager { IsEnabled = false };
		var service = Animated(animations, out _);

		animations.Update(TimeSpan.FromMilliseconds(100));

		Assert.Equal(TimeSpan.Zero, Elapsed(service));

		service.Dispose();
	}

	private static TimeSpan Elapsed(DesktopBackgroundService service)
	{
		service.Render(10, 5);
		return service.LastAnimationElapsed;
	}
}
