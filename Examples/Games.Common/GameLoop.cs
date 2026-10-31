// -----------------------------------------------------------------------
// The example games' loop, as an animation.
//
// A game needs time advanced in fixed steps, every frame, on the UI thread.
// ConsoleWindowSystem.Animations already does exactly that for every
// animation it holds: the render loop calls Animations.Update once per
// composed frame, and HasActiveAnimations keeps it drawing. A game is
// simply something that never finishes, which FrameCycleAnimation (the
// spinner clock) already demonstrates is a legitimate shape for IAnimation.
//
// Registering here instead of running a Task.Delay loop buys three things:
//
//   * One clock. No Stopwatch, no second thread of time, no deadline
//     arithmetic that has to stay in step with the renderer.
//   * The animation clock is one the diagnostics socket can freeze and
//     advance by hand, so the whole game becomes steppable from outside:
//         ask '{"cmd":"clock","target":"animation","fake":true}'
//         ask '{"cmd":"clock","target":"animation","advance":1000}'
//     At a 1/120 step that advance is exactly 120 steps, every time.
//   * It is testable for the same reason: deterministic, headless, and
//     frame-exact, with no sleeping.
//
// WantsUncappedDelta is what makes it work. AnimationManager clamps every
// delta to AnimationDefaults.MaxFrameDeltaMs so a tween cannot jump to its
// end after an idle period; a simulation would rather drop steps than have
// its clock stretched, and a hand-advance of a second has to arrive as a
// second.
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;

namespace Games.Common;

/// <summary>
/// Advances a game in fixed steps from whatever delta the animation manager
/// supplies. Never completes until cancelled.
/// </summary>
public sealed class GameLoop : IAnimation
{
	/// <summary>
	/// Longest run of simulated time one frame may catch up on when time is
	/// arriving from a running clock. Past this the loop drops steps rather
	/// than spending the frame simulating, which is what stops a stall from
	/// becoming a spiral it never recovers from.
	/// </summary>
	private const double MaxCatchUpSeconds = 0.25;

	private readonly double _stepSeconds;
	private readonly Action<float, double> _step;
	private readonly Action<double> _afterSteps;
	private readonly Func<bool>? _timeIsDeliberate;

	private double _accumulator;
	private bool _cancelled;

	/// <param name="stepSeconds">Fixed simulation step, in seconds.</param>
	/// <param name="step">
	/// Advances the game by exactly one step. Receives the step length and the
	/// game's own elapsed time, which this loop accumulates rather than reading
	/// a wall clock: a frozen clock must stop that time too, or anything keyed
	/// to it keeps animating while the simulation stands still.
	/// </param>
	/// <param name="afterSteps">
	/// Runs once per frame after the steps, with the same elapsed time: paint,
	/// HUD, and anything else that reads state rather than advancing it.
	/// </param>
	/// <param name="timeIsDeliberate">
	/// True while the delta is supplied on purpose rather than by a running
	/// clock - a frozen clock being advanced by hand. Such a delta is never a
	/// stall, so it is simulated in full: "advance by a second" has to mean a
	/// second, or stepping from outside quietly lies about how far it went.
	/// Null means time is always real.
	/// </param>
	public GameLoop(
		double stepSeconds,
		Action<float, double> step,
		Action<double> afterSteps,
		Func<bool>? timeIsDeliberate = null)
	{
		if (stepSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepSeconds));

		_stepSeconds = stepSeconds;
		_step = step ?? throw new ArgumentNullException(nameof(step));
		_afterSteps = afterSteps ?? throw new ArgumentNullException(nameof(afterSteps));
		_timeIsDeliberate = timeIsDeliberate;
	}

	/// <summary>Simulation steps taken since the loop started.</summary>
	public long Steps { get; private set; }

	/// <summary>Frames the loop has been advanced on.</summary>
	public long Frames { get; private set; }

	/// <summary>
	/// Seconds of simulated time elapsed: the sum of every step taken, not a
	/// wall-clock reading. Freezing the clock freezes this too.
	/// </summary>
	public double Now { get; private set; }

	/// <summary>Frames per second, averaged over the last second of real frames.</summary>
	public double Fps { get; private set; }

	private double _fpsWindowStart;
	private long _fpsWindowFrames;

	/// <inheritdoc/>
	public bool IsComplete => _cancelled;

	/// <inheritdoc/>
	/// <remarks>
	/// The loop clamps its own catch-up rather than taking the manager's cap,
	/// which would stretch simulated time whenever a frame ran long and make
	/// the game visibly slow down under load.
	/// </remarks>
	public bool WantsUncappedDelta => true;

	/// <inheritdoc/>
	public void Update(TimeSpan deltaTime)
	{
		if (_cancelled) return;

		Frames++;

		double seconds = Math.Max(0, deltaTime.TotalSeconds);
		if (_timeIsDeliberate?.Invoke() != true)
		{
			seconds = Math.Min(MaxCatchUpSeconds, seconds);
		}

		_accumulator += seconds;

		while (_accumulator >= _stepSeconds)
		{
			Now += _stepSeconds;
			_step((float)_stepSeconds, Now);
			_accumulator -= _stepSeconds;
			Steps++;
		}

		// Frame rate is measured against simulated time, so a frozen clock
		// reports the rate it had rather than decaying toward zero.
		_fpsWindowFrames++;
		if (Now - _fpsWindowStart >= 1)
		{
			Fps = _fpsWindowFrames / (Now - _fpsWindowStart);
			_fpsWindowFrames = 0;
			_fpsWindowStart = Now;
		}

		_afterSteps(Now);
	}

	/// <inheritdoc/>
	public void Cancel() => _cancelled = true;
}
