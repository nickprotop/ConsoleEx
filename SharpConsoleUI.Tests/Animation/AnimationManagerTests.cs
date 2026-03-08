using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

public class AnimationManagerTests
{
	/// <summary>
	/// Advances the manager in small steps until the total time is reached.
	/// Respects the MaxFrameDeltaMs cap used by AnimationManager.Update.
	/// </summary>
	private static void AdvanceByMs(AnimationManager manager, double totalMs)
	{
		var step = TimeSpan.FromMilliseconds(AnimationDefaults.MaxFrameDeltaMs);
		double remaining = totalMs;
		while (remaining > 0)
		{
			double tick = Math.Min(remaining, AnimationDefaults.MaxFrameDeltaMs);
			manager.Update(TimeSpan.FromMilliseconds(tick));
			remaining -= tick;
		}
	}

	[Fact]
	public void ActiveCount_InitiallyZero()
	{
		var manager = new AnimationManager();
		Assert.Equal(0, manager.ActiveCount);
	}

	[Fact]
	public void Animate_IncrementsActiveCount()
	{
		var manager = new AnimationManager();

		manager.Animate(0f, 100f, TimeSpan.FromSeconds(1));

		Assert.Equal(1, manager.ActiveCount);
	}

	[Fact]
	public void Update_AdvancesTweens()
	{
		var manager = new AnimationManager();
		float lastValue = 0f;

		manager.Animate(0f, 100f, TimeSpan.FromSeconds(1),
			onUpdate: v => lastValue = v);

		manager.Update(TimeSpan.FromMilliseconds(30));

		Assert.True(lastValue > 0f);
	}

	[Fact]
	public void Update_RemovesCompletedAnimations()
	{
		var manager = new AnimationManager();

		manager.Animate(0f, 100f, TimeSpan.FromSeconds(1));

		AdvanceByMs(manager, 1100);

		Assert.Equal(0, manager.ActiveCount);
	}

	[Fact]
	public void Cancel_StopsSpecificAnimation()
	{
		var manager = new AnimationManager();
		int updateCount = 0;

		var anim = manager.Animate(0f, 100f, TimeSpan.FromSeconds(1),
			onUpdate: _ => updateCount++);

		manager.Update(TimeSpan.FromMilliseconds(30));
		int countBeforeCancel = updateCount;

		manager.Cancel(anim);
		manager.Update(TimeSpan.FromMilliseconds(30));

		Assert.Equal(countBeforeCancel, updateCount);
	}

	[Fact]
	public void CancelAll_ClearsAllAnimations()
	{
		var manager = new AnimationManager();

		manager.Animate(0f, 100f, TimeSpan.FromSeconds(1));
		manager.Animate(0f, 100f, TimeSpan.FromSeconds(2));
		manager.Animate(0f, 100f, TimeSpan.FromSeconds(3));

		manager.CancelAll();

		Assert.Equal(0, manager.ActiveCount);
	}

	[Fact]
	public void Animate_RespectsMaxConcurrentLimit()
	{
		var manager = new AnimationManager();

		for (int i = 0; i < AnimationDefaults.MaxConcurrentAnimations + 10; i++)
		{
			manager.Animate(0f, 100f, TimeSpan.FromSeconds(10));
		}

		Assert.Equal(AnimationDefaults.MaxConcurrentAnimations, manager.ActiveCount);
	}

	[Fact]
	public void HasActiveAnimations_ReflectsState()
	{
		var manager = new AnimationManager();

		Assert.False(manager.HasActiveAnimations);

		manager.Animate(0f, 100f, TimeSpan.FromSeconds(1));
		Assert.True(manager.HasActiveAnimations);

		manager.CancelAll();
		Assert.False(manager.HasActiveAnimations);
	}

	[Fact]
	public void Animate_ByteOverload_Works()
	{
		var manager = new AnimationManager();
		byte lastValue = 0;

		manager.Animate((byte)0, (byte)255, TimeSpan.FromMilliseconds(200),
			onUpdate: v => lastValue = v);

		AdvanceByMs(manager, 100);

		Assert.True(lastValue > 0);
	}

	[Fact]
	public void Animate_IntOverload_Works()
	{
		var manager = new AnimationManager();
		int lastValue = 0;

		manager.Animate(0, 100, TimeSpan.FromMilliseconds(200),
			onUpdate: v => lastValue = v);

		AdvanceByMs(manager, 100);

		Assert.True(lastValue > 0);
	}

	[Fact]
	public void Animate_ColorOverload_Works()
	{
		var manager = new AnimationManager();
		Color lastValue = new Color(0, 0, 0);

		manager.Animate(
			new Color(0, 0, 0),
			new Color(255, 255, 255),
			TimeSpan.FromMilliseconds(200),
			onUpdate: v => lastValue = v);

		AdvanceByMs(manager, 100);

		Assert.True(lastValue.R > 0);
	}

	[Fact]
	public void Animate_OnComplete_Fires()
	{
		var manager = new AnimationManager();
		bool completed = false;

		manager.Animate(0f, 100f, TimeSpan.FromSeconds(1),
			onComplete: () => completed = true);

		AdvanceByMs(manager, 1100);

		Assert.True(completed);
	}
}
