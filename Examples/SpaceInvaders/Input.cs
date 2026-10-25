// -----------------------------------------------------------------------
// SpaceInvaders — input layer.
//
// Terminals only report key PRESSES; there is no key-release event. Holding
// an arrow key produces one press, then a pause (the OS auto-repeat delay,
// typically 250–600 ms), then a stream of repeats (~30 ms apart). A naive
// "move while a key event arrived recently" loop stutters through that
// pause — the classic terminal-game keyboard lag.
//
// Until the first auto-repeat arrives, a tap and the start of a hold are
// indistinguishable, so the game offers three keyboard modes:
//   Step  — every key event is one step: a tap moves one step, a hold keeps
//           stepping on each auto-repeat (step size is scaled by the learned
//           repeat interval so held speed is the same on every OS setting).
//   Glide — HeldKey keeps moving through the OS delay (no pause; a tap
//           glides for the learned delay).
//   Latch — a tap starts movement; the opposite key or Down/S stops it.
// Once repeats flow, a hold lasts only a few repeat intervals, so releasing
// stops the ship almost immediately. KeyRepeatModel learns the user's real
// delay/interval from observed repeats.
//
// Mouse input arrives on the driver's input thread (keys arrive on the UI
// thread), so MouseInput is the only lock-guarded state in the game.
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace SpaceInvaders;

/// <summary>Single monotonic clock shared by input handlers and the game loop.</summary>
internal static class GameClock
{
	private static readonly Stopwatch Watch = Stopwatch.StartNew();

	/// <summary>Seconds since start-up.</summary>
	public static double Now => Watch.Elapsed.TotalSeconds;
}

/// <summary>How held movement keys are interpreted.</summary>
internal enum KeyboardMode
{
	/// <summary>One key event, one step: taps step once, holds step on every auto-repeat.</summary>
	Step,

	/// <summary>Hold to move without pausing through the OS repeat delay; taps glide briefly.</summary>
	Glide,

	/// <summary>A direction key latches movement until the opposite key or Down/S. Zero lag.</summary>
	Latch,
}

/// <summary>
/// Learns the operating system's key auto-repeat timing from observed presses.
/// </summary>
internal sealed class KeyRepeatModel
{
	private const double DefaultDelay = 0.50;
	private const double DefaultInterval = 0.033;
	private const double MinDelay = 0.15;
	private const double MaxDelay = 0.90;

	/// <summary>Estimated delay before the first auto-repeat, in seconds.</summary>
	public double InitialDelay { get; private set; } = DefaultDelay;

	/// <summary>Estimated interval between auto-repeats, in seconds.</summary>
	public double Interval { get; private set; } = DefaultInterval;

	/// <summary>True once at least one genuine hold has been measured.</summary>
	public bool Calibrated { get; private set; }

	/// <summary>Records a measured press→first-repeat gap.</summary>
	public void ObserveDelay(double seconds)
	{
		if (seconds < MinDelay || seconds > MaxDelay) return;
		InitialDelay = Calibrated ? InitialDelay * 0.6 + seconds * 0.4 : seconds;
		Calibrated = true;
	}

	/// <summary>Records a measured repeat→repeat gap.</summary>
	public void ObserveInterval(double seconds)
	{
		if (seconds <= 0.005 || seconds > 0.12) return;
		Interval = Interval * 0.8 + seconds * 0.2;
	}
}

/// <summary>
/// Infers "is this key still held down" from a stream of press events.
/// </summary>
internal sealed class HeldKey
{
	// A press→press gap below this is a repeat, above it a human re-press.
	private const double RepeatGapCeiling = 0.12;

	private readonly KeyRepeatModel _model;
	private double _lastPress = double.NegativeInfinity;
	private double _holdUntil = double.NegativeInfinity;
	private double _pendingDelay = -1;
	private int _repeats;

	public HeldKey(KeyRepeatModel model) => _model = model;

	/// <summary>True when auto-repeat has confirmed the key is physically held.</summary>
	public bool Repeating => _repeats >= 1;

	/// <summary>Registers a press (initial or auto-repeat). Returns true for a fresh press.</summary>
	public bool Press(double now)
	{
		bool fresh = now > _holdUntil;
		double gap = now - _lastPress;

		if (fresh)
		{
			_repeats = 0;
			_pendingDelay = -1;
			_holdUntil = now + _model.InitialDelay + 0.04;
		}
		else
		{
			if (_repeats == 0 && _pendingDelay < 0)
			{
				// First event after the press: either the first auto-repeat or a
				// quick human double-tap. Only trust it once a fast repeat follows.
				_pendingDelay = gap;
			}
			else if (gap < RepeatGapCeiling)
			{
				if (_pendingDelay > 0)
				{
					_model.ObserveDelay(_pendingDelay);
					_pendingDelay = 0;
				}
				_model.ObserveInterval(gap);
			}

			_repeats++;
			_holdUntil = now + Math.Max(0.075, _model.Interval * 3.0 + 0.02);
		}

		_lastPress = now;
		return fresh;
	}

	/// <summary>Forgets the hold immediately (e.g. the opposite direction was pressed).</summary>
	public void Release()
	{
		_holdUntil = double.NegativeInfinity;
		_repeats = 0;
	}

	/// <summary>Whether the key should be treated as down at <paramref name="now"/>.</summary>
	public bool IsHeld(double now) => now <= _holdUntil;

}

/// <summary>
/// Thread-safe mouse state. Written on the input thread, read by the game loop.
/// </summary>
internal sealed class MouseInput
{
	private readonly object _lock = new();
	private int _cellX = -1;
	private double _lastMove = double.NegativeInfinity;
	private int _pendingClicks;

	public void Moved(int cellX)
	{
		lock (_lock)
		{
			_cellX = cellX;
			_lastMove = GameClock.Now;
		}
	}

	public void Pressed(int cellX)
	{
		lock (_lock)
		{
			_cellX = cellX;
			_lastMove = GameClock.Now;
			_pendingClicks++;
		}
	}

	/// <summary>Takes the current pointer column, last-move time and any clicks since the last call.</summary>
	public (int CellX, double LastMove, int Clicks) Consume()
	{
		lock (_lock)
		{
			var result = (_cellX, _lastMove, _pendingClicks);
			_pendingClicks = 0;
			return result;
		}
	}
}
