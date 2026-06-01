// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Animation;

/// <summary>
/// A looping animation that advances a frame index on a fixed interval and never
/// self-completes. Used by spinner controls and helpers. Ticked on the UI thread
/// via <see cref="AnimationManager"/>; invokes the callback only when the index changes.
/// </summary>
public sealed class FrameCycleAnimation : IAnimation
{
	private readonly int _frameCount;
	private readonly TimeSpan _interval;
	private readonly Action<int> _onFrame;
	private TimeSpan _accum;
	private bool _cancelled;

	/// <summary>The current frame index (0-based).</summary>
	public int CurrentFrame { get; private set; }

	/// <inheritdoc/>
	public bool IsComplete => _cancelled;

	/// <summary>Creates a looping frame-cycle animation.</summary>
	/// <param name="frameCount">Number of frames to cycle through.</param>
	/// <param name="interval">Time between frame advances.</param>
	/// <param name="onFrame">Invoked with the new frame index whenever it changes.</param>
	public FrameCycleAnimation(int frameCount, TimeSpan interval, Action<int> onFrame)
	{
		_frameCount = frameCount;
		_interval = interval <= TimeSpan.Zero
			? TimeSpan.FromMilliseconds(ControlDefaults.AnimationMinIntervalMs)
			: interval;
		_onFrame = onFrame ?? throw new ArgumentNullException(nameof(onFrame));
	}

	/// <inheritdoc/>
	public void Update(TimeSpan deltaTime)
	{
		if (_cancelled || _frameCount <= 1) return;

		_accum += deltaTime;
		bool changed = false;
		while (_accum >= _interval)
		{
			_accum -= _interval;
			CurrentFrame = (CurrentFrame + 1) % _frameCount;
			changed = true;
		}
		if (changed) _onFrame(CurrentFrame);
	}

	/// <inheritdoc/>
	public void Cancel() => _cancelled = true;
}
