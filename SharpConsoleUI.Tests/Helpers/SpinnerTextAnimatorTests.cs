// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class SpinnerTextAnimatorTests
{
	private static ConsoleWindowSystem System() => TestWindowSystemBuilder.CreateTestSystem();

	[Fact]
	public void StartRegistersOneAnimation()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		int before = sys.Animations.ActiveCount;
		a.Start();
		Assert.Equal(before + 1, sys.Animations.ActiveCount);
	}

	[Fact]
	public void StartIsIdempotent()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		a.Start();
		int after1 = sys.Animations.ActiveCount;
		a.Start();
		Assert.Equal(after1, sys.Animations.ActiveCount);
	}

	[Fact]
	public void StopCancelsAnimation()
	{
		var sys = System();
		var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		a.Start();
		int withAnim = sys.Animations.ActiveCount;
		a.Stop();
		Assert.Equal(withAnim - 1, sys.Animations.ActiveCount);
	}

	[Fact]
	public void SetterReceivesFrameStrings()
	{
		var sys = System();
		var seen = new List<string>();
		var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, f => seen.Add(f));
		a.Start();
		// AnimationManager caps deltaTime at MaxFrameDeltaMs (33ms), so drive enough
		// ticks to accumulate >= SpinnerDefaultIntervalMs (100ms) in the animation.
		var tick = TimeSpan.FromMilliseconds(ControlDefaults.SpinnerDefaultIntervalMs);
		// AnimationManager caps each tick at MaxFrameDeltaMs (33ms), so compute how many
		// ticks are needed to accumulate >= SpinnerDefaultIntervalMs (+1 for safety margin).
		int ticksNeeded = (int)Math.Ceiling(
			(double)ControlDefaults.SpinnerDefaultIntervalMs / AnimationDefaults.MaxFrameDeltaMs) + 1;
		for (int i = 0; i < ticksNeeded; i++)
			sys.Animations.Update(tick);
		Assert.NotEmpty(seen);
		Assert.Contains(seen, s => Array.IndexOf(ControlDefaults.SpinnerCircleFrames, s) >= 0);
		a.Dispose();
	}

	[Fact]
	public void DoubleDisposeIsSafe()
	{
		var sys = System();
		var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		a.Start();
		a.Dispose();
		a.Dispose(); // must not throw
	}

	// --- Per-style default interval (Task 4) ---

	[Fact]
	public void StyleConstructorUsesPerStyleDefaultInterval()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Dots, _ => { });
		Assert.Equal(SpinnerControl.DefaultIntervalMs(SpinnerStyle.Dots), a.IntervalMs);
	}

	[Fact]
	public void ExplicitIntervalOverridesPerStyleDefault()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Dots, _ => { }, intervalMs: 50);
		Assert.Equal(50, a.IntervalMs);
	}

	[Fact]
	public void CustomFramesConstructorKeepsGlobalDefaultInterval()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, new[] { "a", "b" }, _ => { });
		Assert.Equal(ControlDefaults.SpinnerDefaultIntervalMs, a.IntervalMs);
	}

	// --- Visible toggle (Task 5, Issue #27) ---

	[Fact]
	public void VisibleDefaultsToTrue()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		Assert.True(a.Visible);
	}

	[Fact]
	public void HidingWhileStartedCancelsAnimationAndBlanksTarget()
	{
		var sys = System();
		string last = "x";
		var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, f => last = f);
		a.Start();
		int withAnim = sys.Animations.ActiveCount;
		a.Visible = false;
		Assert.Equal(withAnim - 1, sys.Animations.ActiveCount); // animation cancelled
		Assert.Equal("", last);                                 // target blanked
	}

	[Fact]
	public void ShowingAfterHideResumesAnimationWhenStarted()
	{
		var sys = System();
		var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		a.Start();
		int withAnim = sys.Animations.ActiveCount;
		a.Visible = false;
		a.Visible = true;
		Assert.Equal(withAnim, sys.Animations.ActiveCount); // re-registered
	}

	[Fact]
	public void TogglingVisibleWithoutStartNeverRegistersAnimation()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		int before = sys.Animations.ActiveCount;
		a.Visible = false;
		a.Visible = true;
		a.Visible = false;
		Assert.Equal(before, sys.Animations.ActiveCount); // never started → never animates
	}

	[Fact]
	public void HidingWhileNotStartedStillBlanksTarget()
	{
		var sys = System();
		string last = "x";
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, f => last = f);
		a.Visible = false;
		Assert.Equal("", last);
	}

	[Fact]
	public void StartWhileHiddenDoesNotRegisterUntilShown()
	{
		var sys = System();
		using var a = new SpinnerTextAnimator(sys, SpinnerStyle.Circle, _ => { });
		a.Visible = false;
		int before = sys.Animations.ActiveCount;
		a.Start();
		Assert.Equal(before, sys.Animations.ActiveCount); // hidden → not registered
		a.Visible = true;
		Assert.Equal(before + 1, sys.Animations.ActiveCount); // now shown → registered
	}
}
