// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using SharpConsoleUI.Animation;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

public class FrameCycleAnimationTests
{
	private static FrameCycleAnimation Make(int frames, int intervalMs, Action<int> onFrame)
		=> new FrameCycleAnimation(frames, TimeSpan.FromMilliseconds(intervalMs), onFrame);

	[Fact]
	public void DoesNotAdvanceBeforeInterval()
	{
		int calls = 0, last = -1;
		var a = Make(4, 100, i => { calls++; last = i; });
		a.Update(TimeSpan.FromMilliseconds(50));
		Assert.Equal(0, a.CurrentFrame);
		Assert.Equal(0, calls);
		Assert.Equal(-1, last);
	}

	[Fact]
	public void AdvancesOneFrameAtIntervalBoundary()
	{
		int last = -1;
		var a = Make(4, 100, i => last = i);
		a.Update(TimeSpan.FromMilliseconds(100));
		Assert.Equal(1, a.CurrentFrame);
		Assert.Equal(1, last);
	}

	[Fact]
	public void AccumulatorCarriesRemainder()
	{
		int last = -1;
		var a = Make(4, 100, i => last = i);
		a.Update(TimeSpan.FromMilliseconds(150)); // advance 1, 50 left
		Assert.Equal(1, a.CurrentFrame);
		a.Update(TimeSpan.FromMilliseconds(50));  // now hits 100 total -> advance to 2
		Assert.Equal(2, a.CurrentFrame);
		Assert.Equal(2, last);
	}

	[Fact]
	public void MultipleIntervalsInOneUpdateAdvanceMultiple()
	{
		var a = Make(4, 100, _ => { });
		a.Update(TimeSpan.FromMilliseconds(250)); // 2 full intervals
		Assert.Equal(2, a.CurrentFrame);
	}

	[Fact]
	public void WrapsModulo()
	{
		var a = Make(3, 100, _ => { });
		a.Update(TimeSpan.FromMilliseconds(300)); // 3 advances -> back to 0
		Assert.Equal(0, a.CurrentFrame);
	}

	[Fact]
	public void CallbackFiresOnlyOnChange()
	{
		int calls = 0;
		var a = Make(4, 100, _ => calls++);
		a.Update(TimeSpan.FromMilliseconds(50)); // no change
		a.Update(TimeSpan.FromMilliseconds(50)); // total 100 -> 1 change
		Assert.Equal(1, calls);
	}

	[Fact]
	public void NeverSelfCompletes()
	{
		var a = Make(4, 100, _ => { });
		for (int i = 0; i < 100; i++) a.Update(TimeSpan.FromMilliseconds(100));
		Assert.False(a.IsComplete);
	}

	[Fact]
	public void CancelStopsAndCompletes()
	{
		int calls = 0;
		var a = Make(4, 100, _ => calls++);
		a.Cancel();
		Assert.True(a.IsComplete);
		a.Update(TimeSpan.FromMilliseconds(500));
		Assert.Equal(0, calls);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void SingleOrZeroFrameNoOps(int frameCount)
	{
		int calls = 0;
		var a = Make(frameCount, 100, _ => calls++);
		a.Update(TimeSpan.FromMilliseconds(500));
		Assert.Equal(0, a.CurrentFrame);
		Assert.Equal(0, calls);
	}
}
