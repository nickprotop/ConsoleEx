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
}
