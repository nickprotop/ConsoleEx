// -----------------------------------------------------------------------
// SnakeGame — input layer.
//
// Snake's input problem is not the one Space Invaders has. Invaders needs to
// know whether a key is still HELD, because the ship moves continuously.
// Snake only ever needs discrete turns — but it needs them not to be lost.
//
// The snake steps on a grid tick (~8/second at speed 1). A player rounding a
// corner presses Right then Down well inside one tick, and the naive "store
// the latest direction" approach silently eats the first press: both land in
// the same tick, Down overwrites Right, and the snake turns a corner the
// player never asked for. That is the single most common complaint about
// terminal Snake, and it is an input bug, not a timing one.
//
// TurnQueue fixes it by buffering: each press is appended, and every tick
// consumes exactly one. Two presses inside a tick become two turns on two
// consecutive ticks, which is what the player's fingers meant. The queue is
// deliberately shallow (3) — buffer any deeper and the snake keeps turning
// after the player has stopped, which feels like it is driving itself.
//
// Reversals are rejected at enqueue time against the last QUEUED direction
// rather than the current one, or Right→Down→... with a full queue could
// still fold the snake back into its own neck.
//
// Mouse input arrives on the driver's input thread (keys arrive on the UI
// thread), so MouseInput is the only lock-guarded state in the game.
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace SnakeGame;

/// <summary>Single monotonic clock shared by input handlers and the game loop.</summary>
internal static class GameClock
{
	private static readonly Stopwatch Watch = Stopwatch.StartNew();

	/// <summary>Seconds since start-up.</summary>
	public static double Now => Watch.Elapsed.TotalSeconds;
}

internal enum Direction
{
	Up,
	Down,
	Left,
	Right,
}

internal static class DirectionExtensions
{
	/// <summary>Unit step in grid cells.</summary>
	public static (int X, int Y) Delta(this Direction d) => d switch
	{
		Direction.Up => (0, -1),
		Direction.Down => (0, 1),
		Direction.Left => (-1, 0),
		_ => (1, 0),
	};

	public static Direction Opposite(this Direction d) => d switch
	{
		Direction.Up => Direction.Down,
		Direction.Down => Direction.Up,
		Direction.Left => Direction.Right,
		_ => Direction.Left,
	};

	/// <summary>True for a turn that would drive the head straight into the neck.</summary>
	public static bool Reverses(this Direction d, Direction current) => d == current.Opposite();
}

/// <summary>
/// A short buffer of pending turns, so two presses inside one tick both land.
/// </summary>
internal sealed class TurnQueue
{
	/// <summary>
	/// Three is enough for a corner plus a correction. Deeper and the snake keeps
	/// turning after the player has stopped pressing, which reads as lag.
	/// </summary>
	private const int Capacity = 3;

	private readonly Direction[] _queue = new Direction[Capacity];
	private int _count;

	/// <summary>The direction the last queued turn will leave the snake facing.</summary>
	private Direction _tail;

	/// <summary>Pending turns, for the HUD's input readout.</summary>
	public int Pending => _count;

	public void Reset(Direction facing)
	{
		_count = 0;
		_tail = facing;
	}

	/// <summary>
	/// Queues a turn. Returns false when it was rejected: a reversal, a repeat of
	/// where the snake is already heading, or a full buffer.
	/// </summary>
	public bool Enqueue(Direction d)
	{
		// Checked against the queue's tail, not the snake's current heading: with
		// Right already queued, a Left press is still a reversal even though the
		// snake has not turned yet.
		if (d == _tail || d.Reverses(_tail)) return false;
		if (_count >= Capacity) return false;

		_queue[_count++] = d;
		_tail = d;
		return true;
	}

	/// <summary>Takes the next turn, or returns <paramref name="current"/> unchanged.</summary>
	public Direction Dequeue(Direction current)
	{
		if (_count == 0) return current;

		var next = _queue[0];
		for (int i = 1; i < _count; i++) _queue[i - 1] = _queue[i];
		_count--;
		return next;
	}
}

/// <summary>
/// Thread-safe mouse state. Written on the input thread, read by the game loop.
/// </summary>
/// <remarks>
/// The mouse steers by direction rather than position: the snake turns towards
/// wherever the pointer is, which is the only scheme that works for a game whose
/// body cannot be teleported.
/// </remarks>
internal sealed class MouseInput
{
	private readonly object _lock = new();
	private int _cellX = -1;
	private int _cellY = -1;
	private double _lastMove = double.NegativeInfinity;
	private int _pendingClicks;

	public void Moved(int cellX, int cellY)
	{
		lock (_lock)
		{
			_cellX = cellX;
			_cellY = cellY;
			_lastMove = GameClock.Now;
		}
	}

	public void Pressed(int cellX, int cellY)
	{
		lock (_lock)
		{
			_cellX = cellX;
			_cellY = cellY;
			_lastMove = GameClock.Now;
			_pendingClicks++;
		}
	}

	/// <summary>Takes the pointer cell, last-move time and any clicks since the last call.</summary>
	public (int CellX, int CellY, double LastMove, int Clicks) Consume()
	{
		lock (_lock)
		{
			var result = (_cellX, _cellY, _lastMove, _pendingClicks);
			_pendingClicks = 0;
			return result;
		}
	}
}
