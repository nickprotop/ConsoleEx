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
using SharpConsoleUI.Parsing;

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
	private bool _started;
	private bool _visible = true;

	/// <summary>Gets the resolved per-frame interval in milliseconds.</summary>
	public int IntervalMs => _intervalMs;

	/// <summary>Gets the display width (in columns) of the widest frame, with any markup stripped.
	/// Use this to pad the target to a fixed width so that toggling <see cref="Visible"/> (or showing
	/// an empty placeholder) does not shift the surrounding layout. For example:
	/// <code>if (!animator.Visible) label.Label = new string(' ', animator.FrameWidth);</code></summary>
	public int FrameWidth { get; }

	/// <summary>Creates an animator using a preset style. When <paramref name="intervalMs"/> is null,
	/// the style's per-style default interval is used.</summary>
	public SpinnerTextAnimator(ConsoleWindowSystem system, SpinnerStyle style, Action<string> setter, int? intervalMs = null)
		: this(system, SpinnerControl.FramesForStyle(style), setter, intervalMs ?? SpinnerControl.DefaultIntervalMs(style)) { }

	/// <summary>Creates an animator using custom frames (may contain markup).</summary>
	public SpinnerTextAnimator(ConsoleWindowSystem system, IReadOnlyList<string> frames, Action<string> setter, int? intervalMs = null)
	{
		_system = system ?? throw new ArgumentNullException(nameof(system));
		_setter = setter ?? throw new ArgumentNullException(nameof(setter));
		_frames = frames is { Count: > 0 } ? frames.ToArray() : ControlDefaults.SpinnerBrailleFrames;
		_intervalMs = Math.Max(ControlDefaults.AnimationMinIntervalMs, intervalMs ?? ControlDefaults.SpinnerDefaultIntervalMs);

		int frameWidth = 0;
		foreach (var f in _frames)
			frameWidth = Math.Max(frameWidth, MarkupParser.StripLength(f));
		FrameWidth = frameWidth;
	}

	/// <summary>Starts the animation. Idempotent. No visible effect while <see cref="Visible"/> is false;
	/// the animation begins when the animator is next shown.</summary>
	/// <remarks>Has no effect when the window system's animations are disabled
	/// or the concurrent-animation pool is full; in those cases the setter is never invoked.</remarks>
	public void Start()
	{
		_started = true;
		Apply();
	}

	/// <summary>Stops the animation and clears the started state. Safe to call when not started (idempotent).</summary>
	public void Stop()
	{
		_started = false;
		Apply();
	}

	/// <summary>Gets or sets whether the spinner is shown. When false, the animation is cancelled and the
	/// target setter receives an empty string; setting true resumes if the animator was started.
	/// Independent of <see cref="Start"/>/<see cref="Stop"/> — toggling visibility preserves started state.</summary>
	public bool Visible
	{
		get => _visible;
		set
		{
			if (_visible == value) return;
			_visible = value;
			if (!_visible) _setter(string.Empty); // blank the target when hidden
			Apply();
		}
	}

	/// <summary>Reconciles the registered animation with the current started/visible state.</summary>
	private void Apply()
	{
		bool shouldRun = _started && _visible && _system.Animations.IsEnabled;
		if (shouldRun && _animation == null)
		{
			_animation = new FrameCycleAnimation(
				_frames.Length,
				TimeSpan.FromMilliseconds(_intervalMs),
				i => _setter(_frames[i]));
			_system.Animations.Add(_animation);
		}
		else if (!shouldRun && _animation != null)
		{
			_system.Animations.Cancel(_animation);
			_animation = null;
		}
	}

	/// <inheritdoc/>
	public void Dispose() => Stop();
}
