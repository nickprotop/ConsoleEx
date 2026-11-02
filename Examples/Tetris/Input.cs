// -----------------------------------------------------------------------
// Tetris - delayed auto shift.
//
// Terminals report key PRESSES only; there is no release. Holding left gives
// one press, a pause of 250-600ms while the OS decides it is a hold, then a
// stream of repeats. Moving the piece once per press through that pause is
// the stutter every terminal Tetris has.
//
// So the game does its own repeating: a press moves once and arms a timer,
// the timer takes over after the delay and repeats at a fixed rate, and the
// OS repeats only refresh a deadline that would otherwise let go. Holding
// left slides smoothly from the first cell, which is what DAS means.
// -----------------------------------------------------------------------

namespace Tetris;

internal sealed class AutoShift
{
	/// <summary>Before a held key starts repeating.</summary>
	private const float DelaySeconds = 0.17f;

	/// <summary>Between repeats once it has.</summary>
	private const float RepeatSeconds = 0.035f;

	/// <summary>
	/// How long after the last press a key is still considered held. Longer than any OS
	/// repeat interval, so a real hold never lapses, and short enough that a release is
	/// noticed within a frame or two.
	/// </summary>
	private const float HeldSeconds = 0.09f;

	private int _direction;
	private float _sinceLastPress;
	private float _sinceStart;
	private float _repeatTimer;

	private float _softDropFor;

	/// <summary>A left or right key arrived.</summary>
	public void Press(int direction, Game game)
	{
		if (direction != _direction)
		{
			// A new direction starts over: pressing right while holding left should move
			// right immediately, not inherit the left key's repeat.
			_direction = direction;
			_sinceStart = 0;
			_repeatTimer = 0;
			game.Move(direction);
		}
		else if (_sinceLastPress > HeldSeconds)
		{
			// The key had lapsed, so this press is a fresh tap rather than part of a hold.
			// Without this a player tapping left at their own pace moves once and then
			// stops: the press refreshed the hold but nothing moved the piece, because
			// repeat only engages after DelaySeconds of CONTINUOUS holding, which a tap
			// never reaches. Taps are how a piece gets nudged one cell, so they must
			// always move.
			_sinceStart = 0;
			_repeatTimer = 0;
			game.Move(direction);
		}

		_sinceLastPress = 0;
	}

	/// <summary>The soft drop key arrived.</summary>
	public void SoftDrop(Game game)
	{
		_softDropFor = HeldSeconds;
		game.SoftDrop(true);
	}

	/// <summary>Advances the repeat, and lets go of keys that stopped arriving.</summary>
	public void Update(float dt, Game game)
	{
		if (_softDropFor > 0)
		{
			_softDropFor -= dt;
			if (_softDropFor <= 0) game.SoftDrop(false);
		}

		if (_direction == 0) return;

		_sinceLastPress += dt;
		if (_sinceLastPress > HeldSeconds)
		{
			_direction = 0;
			return;
		}

		_sinceStart += dt;
		if (_sinceStart < DelaySeconds) return;

		_repeatTimer += dt;
		while (_repeatTimer >= RepeatSeconds)
		{
			_repeatTimer -= RepeatSeconds;
			game.Move(_direction);
		}
	}
}
