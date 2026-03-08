namespace SharpConsoleUI.Animation;

/// <summary>
/// A generic tween that interpolates a value from start to end over a duration,
/// applying an easing function and invoking callbacks on update and completion.
/// </summary>
public sealed class Tween<T> : IAnimation
{
	private readonly T _from;
	private readonly T _to;
	private readonly TimeSpan _duration;
	private readonly EasingFunction _easing;
	private readonly IInterpolator<T> _interpolator;
	private readonly Action<T>? _onUpdate;
	private readonly Action? _onComplete;
	private TimeSpan _elapsed;
	private bool _cancelled;

	/// <summary>
	/// Creates a new tween interpolating from <paramref name="from"/> to <paramref name="to"/>.
	/// </summary>
	public Tween(
		T from,
		T to,
		TimeSpan duration,
		EasingFunction easing,
		IInterpolator<T> interpolator,
		Action<T>? onUpdate = null,
		Action? onComplete = null)
	{
		_from = from;
		_to = to;
		_duration = duration;
		_easing = easing;
		_interpolator = interpolator;
		_onUpdate = onUpdate;
		_onComplete = onComplete;
		_elapsed = TimeSpan.Zero;
	}

	/// <inheritdoc />
	public bool IsComplete { get; private set; }

	/// <inheritdoc />
	public void Update(TimeSpan deltaTime)
	{
		if (IsComplete || _cancelled)
			return;

		_elapsed += deltaTime;

		double rawProgress = _duration.TotalMilliseconds > 0
			? Math.Clamp(_elapsed / _duration, 0.0, 1.0)
			: 1.0;

		double easedProgress = _easing(rawProgress);
		T value = _interpolator.Interpolate(_from, _to, easedProgress);
		_onUpdate?.Invoke(value);

		if (rawProgress >= 1.0)
		{
			IsComplete = true;
			_onComplete?.Invoke();
		}
	}

	/// <inheritdoc />
	public void Cancel()
	{
		if (IsComplete)
			return;

		_cancelled = true;
		IsComplete = true;
	}
}
