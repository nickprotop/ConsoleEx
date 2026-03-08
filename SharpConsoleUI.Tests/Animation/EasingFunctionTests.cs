using SharpConsoleUI.Animation;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

public class EasingFunctionTests
{
	private static readonly EasingFunction[] AllFunctions =
	[
		EasingFunctions.Linear,
		EasingFunctions.EaseIn,
		EasingFunctions.EaseOut,
		EasingFunctions.EaseInOut,
		EasingFunctions.Bounce,
		EasingFunctions.Elastic
	];

	[Theory]
	[MemberData(nameof(GetAllFunctions))]
	public void AllFunctions_AtZero_ReturnZero(EasingFunction func)
	{
		Assert.Equal(0.0, func(0.0), precision: 10);
	}

	[Theory]
	[MemberData(nameof(GetAllFunctions))]
	public void AllFunctions_AtOne_ReturnOne(EasingFunction func)
	{
		Assert.Equal(1.0, func(1.0), precision: 10);
	}

	[Fact]
	public void Linear_AtHalf_ReturnsHalf()
	{
		Assert.Equal(0.5, EasingFunctions.Linear(0.5), precision: 10);
	}

	[Fact]
	public void EaseIn_StartsSlowly()
	{
		// At t=0.25, EaseIn (t*t) should return 0.0625, less than linear (0.25)
		double result = EasingFunctions.EaseIn(0.25);
		Assert.True(result < 0.25, $"EaseIn(0.25) = {result} should be < 0.25");
	}

	[Fact]
	public void EaseOut_StartsFast()
	{
		// At t=0.25, EaseOut should return more than linear
		double result = EasingFunctions.EaseOut(0.25);
		Assert.True(result > 0.25, $"EaseOut(0.25) = {result} should be > 0.25");
	}

	[Fact]
	public void EaseInOut_AtHalf_ReturnsHalf()
	{
		Assert.Equal(0.5, EasingFunctions.EaseInOut(0.5), precision: 10);
	}

	[Theory]
	[MemberData(nameof(GetMonotonicFunctions))]
	public void MonotonicFunctions_AreNonDecreasing(EasingFunction func)
	{
		const int steps = 100;
		double previous = 0.0;
		for (int i = 1; i <= steps; i++)
		{
			double t = i / (double)steps;
			double current = func(t);
			Assert.True(current >= previous - 1e-10,
				$"Function is not monotonic at t={t}: {current} < {previous}");
			previous = current;
		}
	}

	[Fact]
	public void Bounce_ProducesValuesInRange()
	{
		for (int i = 0; i <= 100; i++)
		{
			double t = i / 100.0;
			double result = EasingFunctions.Bounce(t);
			Assert.InRange(result, -0.01, 1.01);
		}
	}

	[Fact]
	public void Elastic_ProducesValuesNearOne_AtEnd()
	{
		double result = EasingFunctions.Elastic(0.95);
		Assert.InRange(result, 0.9, 1.1);
	}

	public static TheoryData<EasingFunction> GetAllFunctions()
	{
		var data = new TheoryData<EasingFunction>();
		foreach (var f in AllFunctions) data.Add(f);
		return data;
	}

	public static TheoryData<EasingFunction> GetMonotonicFunctions()
	{
		return new TheoryData<EasingFunction>
		{
			EasingFunctions.Linear,
			EasingFunctions.EaseIn,
			EasingFunctions.EaseOut,
			EasingFunctions.EaseInOut
		};
	}
}
