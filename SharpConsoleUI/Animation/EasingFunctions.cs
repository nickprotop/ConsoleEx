namespace SharpConsoleUI.Animation;

/// <summary>
/// Delegate for easing functions that map normalized time [0,1] to progress [0,1].
/// </summary>
public delegate double EasingFunction(double t);

/// <summary>
/// Standard easing functions for animations.
/// All functions accept t in [0,1] and return a value where f(0)=0 and f(1)=1.
/// </summary>
public static class EasingFunctions
{
	/// <inheritdoc cref="EasingFunction"/>
	public static double Linear(double t) => t;

	/// <inheritdoc cref="EasingFunction"/>
	public static double EaseIn(double t) => t * t;

	/// <inheritdoc cref="EasingFunction"/>
	public static double EaseOut(double t) => t * (2.0 - t);

	/// <inheritdoc cref="EasingFunction"/>
	public static double EaseInOut(double t) =>
		t < 0.5 ? 2.0 * t * t : -1.0 + (4.0 - 2.0 * t) * t;

	/// <summary>
	/// Bounce easing: simulates a bouncing ball effect.
	/// </summary>
	public static double Bounce(double t)
	{
		const double n1 = 7.5625;
		const double d1 = 2.75;

		if (t < 1.0 / d1)
			return n1 * t * t;
		if (t < 2.0 / d1)
		{
			t -= 1.5 / d1;
			return n1 * t * t + 0.75;
		}
		if (t < 2.5 / d1)
		{
			t -= 2.25 / d1;
			return n1 * t * t + 0.9375;
		}
		t -= 2.625 / d1;
		return n1 * t * t + 0.984375;
	}

	/// <summary>
	/// Sine pulse: rises from 0 to 1 at midpoint, then falls back to 0.
	/// Useful for flash/pulse effects where the value should peak and return.
	/// </summary>
	public static double SinePulse(double t) => Math.Sin(t * Math.PI);

	/// <summary>
	/// Elastic easing: overshoots and oscillates like a spring.
	/// </summary>
	public static double Elastic(double t)
	{
		if (t <= 0.0) return 0.0;
		if (t >= 1.0) return 1.0;

		const double c4 = (2.0 * Math.PI) / 3.0;
		return Math.Pow(2.0, -10.0 * t) * Math.Sin((t * 10.0 - 0.75) * c4) + 1.0;
	}
}
