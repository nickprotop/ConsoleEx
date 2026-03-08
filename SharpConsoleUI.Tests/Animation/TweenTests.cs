using SharpConsoleUI.Animation;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

public class TweenTests
{
	[Fact]
	public void Tween_FromZeroToHundred_CompletesAfterDuration()
	{
		float lastValue = 0f;
		bool completed = false;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.FromSeconds(1),
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onUpdate: v => lastValue = v,
			onComplete: () => completed = true);

		tween.Update(TimeSpan.FromSeconds(1));

		Assert.Equal(100f, lastValue, precision: 1);
		Assert.True(tween.IsComplete);
		Assert.True(completed);
	}

	[Fact]
	public void Tween_AtHalfway_ReturnsInterpolatedValue()
	{
		float lastValue = 0f;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.FromSeconds(1),
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onUpdate: v => lastValue = v);

		tween.Update(TimeSpan.FromMilliseconds(500));

		Assert.Equal(50f, lastValue, precision: 1);
		Assert.False(tween.IsComplete);
	}

	[Fact]
	public void Tween_OnUpdateCalledEachUpdate()
	{
		int callCount = 0;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.FromSeconds(1),
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onUpdate: _ => callCount++);

		tween.Update(TimeSpan.FromMilliseconds(250));
		tween.Update(TimeSpan.FromMilliseconds(250));
		tween.Update(TimeSpan.FromMilliseconds(250));

		Assert.Equal(3, callCount);
	}

	[Fact]
	public void Tween_Cancel_StopsUpdates()
	{
		int callCount = 0;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.FromSeconds(1),
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onUpdate: _ => callCount++);

		tween.Update(TimeSpan.FromMilliseconds(250));
		tween.Cancel();
		tween.Update(TimeSpan.FromMilliseconds(250));

		Assert.Equal(1, callCount);
		Assert.True(tween.IsComplete);
	}

	[Fact]
	public void Tween_OvershootDuration_ClampsToOne()
	{
		float lastValue = 0f;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.FromSeconds(1),
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onUpdate: v => lastValue = v);

		tween.Update(TimeSpan.FromSeconds(2));

		Assert.Equal(100f, lastValue, precision: 1);
		Assert.True(tween.IsComplete);
	}

	[Fact]
	public void Tween_ZeroDuration_CompletesImmediately()
	{
		float lastValue = 0f;
		bool completed = false;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.Zero,
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onUpdate: v => lastValue = v,
			onComplete: () => completed = true);

		tween.Update(TimeSpan.FromMilliseconds(1));

		Assert.Equal(100f, lastValue, precision: 1);
		Assert.True(completed);
	}

	[Fact]
	public void Tween_DoesNotFireOnCompleteBeforeDone()
	{
		bool completed = false;

		var tween = new Tween<float>(
			from: 0f, to: 100f,
			duration: TimeSpan.FromSeconds(1),
			easing: EasingFunctions.Linear,
			interpolator: FloatInterpolator.Instance,
			onComplete: () => completed = true);

		tween.Update(TimeSpan.FromMilliseconds(500));

		Assert.False(completed);
	}
}
