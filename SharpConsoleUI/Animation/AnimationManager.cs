using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Animation;

/// <summary>
/// Manages active animations, advancing them each frame and removing completed ones.
/// Owned by ConsoleWindowSystem.
/// </summary>
public sealed class AnimationManager
{
	private readonly List<IAnimation> _animations = new();
	private readonly object _lock = new();
	private bool _isEnabled = true;

	/// <summary>
	/// Gets or sets whether animations are enabled. When disabled, new animations
	/// complete instantly (onUpdate called with final value, onComplete called).
	/// </summary>
	public bool IsEnabled
	{
		get => _isEnabled;
		set
		{
			_isEnabled = value;
			if (!value)
				CancelAll();
		}
	}

	/// <summary>Number of currently running animations.</summary>
	public int ActiveCount
	{
		get
		{
			lock (_lock) return _animations.Count;
		}
	}

	/// <summary>Whether any animations are currently running.</summary>
	public bool HasActiveAnimations
	{
		get
		{
			lock (_lock) return _animations.Count > 0;
		}
	}

	/// <summary>
	/// Advances all active animations by deltaTime and removes completed ones.
	/// Delta time is capped to prevent animations completing instantly after idle periods.
	/// </summary>
	public void Update(TimeSpan deltaTime)
	{
		var capped = deltaTime.TotalMilliseconds > AnimationDefaults.MaxFrameDeltaMs
			? TimeSpan.FromMilliseconds(AnimationDefaults.MaxFrameDeltaMs)
			: deltaTime;

		lock (_lock)
		{
			for (int i = _animations.Count - 1; i >= 0; i--)
			{
				_animations[i].Update(capped);
				if (_animations[i].IsComplete)
					_animations.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// Creates and starts a float tween animation.
	/// </summary>
	public IAnimation Animate(
		float from,
		float to,
		TimeSpan duration,
		EasingFunction? easing = null,
		Action<float>? onUpdate = null,
		Action? onComplete = null)
	{
		return AddTween(from, to, duration, easing, FloatInterpolator.Instance, onUpdate, onComplete);
	}

	/// <summary>
	/// Creates and starts an int tween animation.
	/// </summary>
	public IAnimation Animate(
		int from,
		int to,
		TimeSpan duration,
		EasingFunction? easing = null,
		Action<int>? onUpdate = null,
		Action? onComplete = null)
	{
		return AddTween(from, to, duration, easing, IntInterpolator.Instance, onUpdate, onComplete);
	}

	/// <summary>
	/// Creates and starts a byte tween animation.
	/// </summary>
	public IAnimation Animate(
		byte from,
		byte to,
		TimeSpan duration,
		EasingFunction? easing = null,
		Action<byte>? onUpdate = null,
		Action? onComplete = null)
	{
		return AddTween(from, to, duration, easing, ByteInterpolator.Instance, onUpdate, onComplete);
	}

	/// <summary>
	/// Creates and starts a Color tween animation.
	/// </summary>
	public IAnimation Animate(
		Color from,
		Color to,
		TimeSpan duration,
		EasingFunction? easing = null,
		Action<Color>? onUpdate = null,
		Action? onComplete = null)
	{
		return AddTween(from, to, duration, easing, ColorInterpolator.Instance, onUpdate, onComplete);
	}

	/// <summary>
	/// Creates and starts a generic tween animation with a custom interpolator.
	/// </summary>
	public IAnimation Animate<T>(
		T from,
		T to,
		TimeSpan duration,
		IInterpolator<T> interpolator,
		EasingFunction? easing = null,
		Action<T>? onUpdate = null,
		Action? onComplete = null)
	{
		return AddTween(from, to, duration, easing, interpolator, onUpdate, onComplete);
	}

	/// <summary>
	/// Adds a pre-built animation to the manager.
	/// </summary>
	public void Add(IAnimation animation)
	{
		lock (_lock)
		{
			if (_animations.Count >= AnimationDefaults.MaxConcurrentAnimations)
				return;
			_animations.Add(animation);
		}
	}

	/// <summary>
	/// Cancels a specific animation.
	/// </summary>
	public void Cancel(IAnimation animation)
	{
		lock (_lock)
		{
			animation.Cancel();
		}
	}

	/// <summary>
	/// Cancels all active animations.
	/// </summary>
	public void CancelAll()
	{
		lock (_lock)
		{
			foreach (var anim in _animations)
				anim.Cancel();
			_animations.Clear();
		}
	}

	private IAnimation AddTween<T>(
		T from,
		T to,
		TimeSpan duration,
		EasingFunction? easing,
		IInterpolator<T> interpolator,
		Action<T>? onUpdate,
		Action? onComplete)
	{
		// When disabled, call onUpdate with final value and onComplete immediately
		if (!_isEnabled)
		{
			onUpdate?.Invoke(to);
			onComplete?.Invoke();
			var noOp = new Tween<T>(from, to, TimeSpan.Zero, EasingFunctions.Linear, interpolator, null, null);
			noOp.Cancel();
			return noOp;
		}

		var tween = new Tween<T>(
			from, to, duration,
			easing ?? EasingFunctions.EaseInOut,
			interpolator,
			onUpdate, onComplete);

		lock (_lock)
		{
			if (_animations.Count >= AnimationDefaults.MaxConcurrentAnimations)
			{
				tween.Cancel();
				return tween;
			}
			_animations.Add(tween);
		}

		return tween;
	}
}
