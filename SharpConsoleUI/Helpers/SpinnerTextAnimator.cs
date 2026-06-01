// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Drives an arbitrary text setter from a looping spinner frame cycle, using the
/// window system's animation manager. Useful for animating a status-bar label or
/// window title without a dedicated control. Dispose or Stop to end the animation.
/// </summary>
public sealed class SpinnerTextAnimator : IDisposable
{
	private readonly ConsoleWindowSystem _system;
	private readonly string[] _frames;
	private readonly int _intervalMs;
	private readonly Action<string> _setter;
	private FrameCycleAnimation? _animation;

	/// <summary>Creates an animator using a preset style.</summary>
	public SpinnerTextAnimator(ConsoleWindowSystem system, SpinnerStyle style, Action<string> setter, int? intervalMs = null)
		: this(system, SpinnerControl.FramesForStyle(style), setter, intervalMs) { }

	/// <summary>Creates an animator using custom frames (may contain markup).</summary>
	public SpinnerTextAnimator(ConsoleWindowSystem system, IReadOnlyList<string> frames, Action<string> setter, int? intervalMs = null)
	{
		_system = system ?? throw new ArgumentNullException(nameof(system));
		_setter = setter ?? throw new ArgumentNullException(nameof(setter));
		_frames = frames is { Count: > 0 } ? frames.ToArray() : ControlDefaults.SpinnerBrailleFrames;
		_intervalMs = Math.Max(ControlDefaults.AnimationMinIntervalMs, intervalMs ?? ControlDefaults.SpinnerDefaultIntervalMs);
	}

	/// <summary>Starts the animation. Idempotent.</summary>
	/// <remarks>Has no effect when the window system's animations are disabled
	/// or the concurrent-animation pool is full; in those cases the setter is never invoked.</remarks>
	public void Start()
	{
		if (_animation != null) return;
		if (!_system.Animations.IsEnabled) return;
		_animation = new FrameCycleAnimation(
			_frames.Length,
			TimeSpan.FromMilliseconds(_intervalMs),
			i => _setter(_frames[i]));
		_system.Animations.Add(_animation);
	}

	/// <summary>Stops the animation. Safe to call when not started (idempotent).</summary>
	public void Stop()
	{
		if (_animation == null) return;
		_system.Animations.Cancel(_animation);
		_animation = null;
	}

	/// <inheritdoc/>
	public void Dispose() => Stop();
}
