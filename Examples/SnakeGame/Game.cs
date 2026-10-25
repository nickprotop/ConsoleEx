// -----------------------------------------------------------------------
// SnakeGame — game simulation.
//
// Pure game state + rules; no UI types beyond Color. The window's UI-thread
// loop (Program) feeds it key presses, mouse state and fixed time steps, and
// the Renderer draws whatever it finds here.
//
// The snake lives on a coarse GRID (one cell per segment) but is DRAWN from
// an interpolated position: StepProgress runs 0→1 between grid ticks, and
// the renderer slides each segment from its previous cell towards its
// current one. Classic Snake teleports a whole cell at a time, which at 8
// ticks per second is a visible stutter; interpolating costs nothing and is
// the single biggest difference between this and the version it replaces.
//
// Modern rules on a classic skeleton: a combo multiplier for eating without
// dawdling, a timed bonus fruit, portal walls as an option, and a shield
// that forgives exactly one fatal mistake per run.
// -----------------------------------------------------------------------

using SharpConsoleUI;

namespace SnakeGame;

internal enum Phase
{
	Title,
	Playing,
	Paused,
	Dying,
	GameOver,
}

internal enum GameEvent
{
	Ate,
	BonusTaken,
	BonusMissed,
	ShieldUsed,
	LevelUp,
	NewHighScore,
	GameOver,
}

/// <summary>What happens when the snake reaches the edge of the arena.</summary>
internal enum WallMode
{
	/// <summary>Hitting a wall ends the run. The classic rule.</summary>
	Solid,

	/// <summary>The snake wraps to the opposite edge.</summary>
	Portal,
}

internal readonly record struct Cell(int X, int Y);

/// <summary>A piece of fruit on the board.</summary>
internal sealed class Fruit
{
	public required Cell At { get; init; }
	public required bool Bonus { get; init; }

	/// <summary>Seconds remaining before a bonus fruit vanishes; unused when not a bonus.</summary>
	public float Life { get; set; }
	public float MaxLife { get; init; } = 1;

	/// <summary>Seconds since it appeared, for the pop-in and pulse animations.</summary>
	public float Age { get; set; }

	public int Points => Bonus ? 50 : 10;
}

internal sealed class Game
{
	#region Tuning

	/// <summary>Grid cells per snake segment, horizontally. Segments are 5×5 pixels.</summary>
	/// <remarks>
	/// Five rather than four: a circle needs an odd diameter to have a centre
	/// pixel, and at four the "round" segments rasterise to plain squares. The
	/// cost is a slightly coarser board, which is the right trade — the snake is
	/// what the eye follows.
	/// </remarks>
	public const int CellSize = 5;

	/// <summary>Pixels of margin between the arena and the canvas edge.</summary>
	/// <remarks>
	/// The wall is drawn in this margin. Without it the border would sit at pixel
	/// -1 and never be visible, which is exactly the bug the first headless
	/// screenshot of this game showed.
	/// </remarks>
	public const int ArenaMargin = 2;

	private const int StartLength = 5;
	private const float BaseStepSeconds = 0.125f;      // level 1: eight steps a second
	private const float MinStepSeconds = 0.045f;       // the ceiling, around level 12
	private const float StepSpeedUpPerLevel = 0.007f;
	private const int FruitPerLevel = 5;

	/// <summary>Eat again within this long and the combo multiplier climbs.</summary>
	private const float ComboWindowSeconds = 4.0f;
	private const int MaxCombo = 5;

	/// <summary>A bonus fruit appears every so many normal ones.</summary>
	private const int BonusEveryNFruit = 4;
	private const float BonusLifeSeconds = 7f;

	private const float DyingSeconds = 1.4f;

	/// <summary>
	/// Extra time the snake is held still after a shield save, so the player can
	/// see what happened and steer before it moves again.
	/// </summary>
	private const float ShieldGraceSeconds = 0.6f;

	#endregion

	private readonly Random _rng = new();
	private readonly List<Cell> _body = new();
	private readonly List<Cell> _previousBody = new();
	private readonly TurnQueue _turns = new();

	private float _stepTimer;
	private float _phaseTimer;
	private int _fruitEatenThisLevel;
	private int _fruitEatenTotal;
	private double _lastEatTime = double.NegativeInfinity;

	public Game(int highScore)
	{
		HiScore = highScore;
		Fx = new Effects(_rng);
	}

	/// <summary>Raised for anything the shell should announce as a toast.</summary>
	public event Action<GameEvent, string>? Notify;

	public Effects Fx { get; }

	#region Board

	/// <summary>Arena width in grid cells.</summary>
	public int Columns { get; private set; }

	/// <summary>Arena height in grid cells.</summary>
	public int Rows { get; private set; }

	/// <summary>Pixel offset of the arena's top-left corner within the canvas.</summary>
	public int OriginX { get; private set; } = ArenaMargin;
	public int OriginY { get; private set; } = ArenaMargin;

	/// <summary>Centre of a grid cell, in surface pixels.</summary>
	public (float X, float Y) CellCenter(Cell c) => (
		OriginX + (c.X + 0.5f) * CellSize,
		OriginY + (c.Y + 0.5f) * CellSize);

	/// <summary>The snake, head first.</summary>
	public IReadOnlyList<Cell> Body => _body;

	/// <summary>Where each segment was on the previous tick, for interpolation.</summary>
	public IReadOnlyList<Cell> PreviousBody => _previousBody;

	/// <summary>How far through the current grid step we are, 0→1.</summary>
	public float StepProgress { get; private set; }

	public Fruit? Food { get; private set; }
	public Fruit? Bonus { get; private set; }

	#endregion

	#region Status

	public Phase Phase { get; private set; } = Phase.Title;
	public Direction Facing { get; private set; } = Direction.Right;
	public int Score { get; private set; }
	public int HiScore { get; private set; }
	public int Level { get; private set; } = 1;
	public int Combo { get; private set; } = 1;
	public bool BeatHighScore { get; private set; }
	public WallMode Walls { get; set; } = WallMode.Solid;

	/// <summary>One free death per run, spent automatically.</summary>
	public bool ShieldAvailable { get; private set; } = true;

	/// <summary>Where the snake died, for the skull marker.</summary>
	public Cell DeathCell { get; private set; }

	/// <summary>How far through the death animation, 0→1.</summary>
	public double PhaseProgress => Math.Clamp(_phaseTimer / DyingSeconds, 0, 1);

	public int Length => _body.Count;
	public int PendingTurns => _turns.Pending;

	/// <summary>Seconds per grid step at the current level.</summary>
	public float StepSeconds =>
		Math.Max(MinStepSeconds, BaseStepSeconds - (Level - 1) * StepSpeedUpPerLevel);

	/// <summary>Fraction of the combo window still open, 0→1, for the HUD meter.</summary>
	public double ComboRemaining
	{
		get
		{
			if (Combo <= 1 || double.IsNegativeInfinity(_lastEatTime)) return 0;
			double left = ComboWindowSeconds - (GameClock.Now - _lastEatTime);
			return Math.Clamp(left / ComboWindowSeconds, 0, 1);
		}
	}

	/// <summary>Progress towards the next level, 0→1.</summary>
	public double LevelProgress => (double)_fruitEatenThisLevel / FruitPerLevel;

	/// <summary>How full the board is, 0→1 — the HUD's "pressure" reading.</summary>
	public double Density => Columns * Rows == 0 ? 0 : (double)_body.Count / (Columns * Rows);

	#endregion

	/// <summary>Recomputes the grid when the canvas resizes.</summary>
	/// <remarks>
	/// A resize mid-run would otherwise leave segments outside the board. Rather
	/// than kill the player for resizing their terminal, the body is clamped back
	/// inside and the run continues.
	/// </remarks>
	public void Resize(int pixelWidth, int pixelHeight)
	{
		int usableWidth = pixelWidth - ArenaMargin * 2;
		int usableHeight = pixelHeight - ArenaMargin * 2;
		int columns = Math.Max(8, usableWidth / CellSize);
		int rows = Math.Max(6, usableHeight / CellSize);
		if (columns == Columns && rows == Rows) return;

		Columns = columns;
		Rows = rows;

		// Centre the board in whatever is left over, so the arena is not glued to
		// the top-left with a ragged gap on the other two sides.
		OriginX = ArenaMargin + Math.Max(0, usableWidth - columns * CellSize) / 2;
		OriginY = ArenaMargin + Math.Max(0, usableHeight - rows * CellSize) / 2;

		if (Phase == Phase.Title)
		{
			Reset();
			return;
		}

		for (int i = 0; i < _body.Count; i++)
			_body[i] = Clamp(_body[i]);
		for (int i = 0; i < _previousBody.Count; i++)
			_previousBody[i] = Clamp(_previousBody[i]);

		if (Food != null && !InBounds(Food.At)) Food = SpawnFruit(bonus: false);
		if (Bonus != null && !InBounds(Bonus.At)) Bonus = null;
	}

	private Cell Clamp(Cell c) =>
		new(Math.Clamp(c.X, 0, Columns - 1), Math.Clamp(c.Y, 0, Rows - 1));

	private bool InBounds(Cell c) =>
		(uint)c.X < (uint)Columns && (uint)c.Y < (uint)Rows;

	#region Lifecycle

	public void StartGame()
	{
		Reset();
		Phase = Phase.Playing;
		Fx.Clear();
	}

	private void Reset()
	{
		_body.Clear();
		_previousBody.Clear();

		int cy = Rows / 2;
		int cx = Math.Max(2, Columns / 4);
		for (int i = 0; i < StartLength; i++)
			_body.Add(new Cell(Math.Max(0, cx - i), cy));
		_previousBody.AddRange(_body);

		Facing = Direction.Right;
		_turns.Reset(Facing);

		Score = 0;
		Level = 1;
		Combo = 1;
		ShieldAvailable = true;
		BeatHighScore = false;
		_fruitEatenThisLevel = 0;
		_fruitEatenTotal = 0;
		_stepTimer = 0;
		_phaseTimer = 0;
		StepProgress = 0;
		_lastEatTime = double.NegativeInfinity;

		Bonus = null;
		Food = SpawnFruit(bonus: false);
	}

	public void TogglePause()
	{
		Phase = Phase switch
		{
			Phase.Playing => Phase.Paused,
			Phase.Paused => Phase.Playing,
			Phase.Title or Phase.GameOver => Phase.Playing,
			_ => Phase,
		};

		if (Phase == Phase.Playing && _body.Count == 0) StartGame();
	}

	public void ToggleWalls()
	{
		Walls = Walls == WallMode.Solid ? WallMode.Portal : WallMode.Solid;
	}

	#endregion

	#region Input

	/// <summary>Queues a turn. Called from the key handler on the UI thread.</summary>
	public void Turn(Direction d)
	{
		if (Phase == Phase.Title || Phase == Phase.GameOver)
		{
			StartGame();
			return;
		}

		if (Phase != Phase.Playing) return;
		_turns.Enqueue(d);
	}

	/// <summary>
	/// Steers towards the pointer. The snake turns on the axis where the pointer
	/// is furthest away, which is the only mouse scheme that suits a body that
	/// cannot be teleported.
	/// </summary>
	public void ApplyMouse(int cellX, int cellY, double lastMove, int clicks, double now)
	{
		if (clicks > 0 && Phase is Phase.Title or Phase.GameOver)
		{
			StartGame();
			return;
		}

		if (Phase != Phase.Playing || _body.Count == 0) return;

		// Only steer from a pointer that moved recently; a parked cursor should
		// not fight the keyboard for control.
		if (now - lastMove > 0.4) return;
		if (cellX < 0 || cellY < 0) return;

		var head = _body[0];
		int dx = cellX - head.X, dy = cellY - head.Y;
		if (dx == 0 && dy == 0) return;

		var wanted = Math.Abs(dx) >= Math.Abs(dy)
			? (dx < 0 ? Direction.Left : Direction.Right)
			: (dy < 0 ? Direction.Up : Direction.Down);

		_turns.Enqueue(wanted);
	}

	#endregion

	#region Simulation

	/// <summary>Advances the world by one fixed step.</summary>
	public void Update(float dt, double now)
	{
		float worldDt = dt * Fx.TimeScale;
		Fx.Update(worldDt, dt);

		switch (Phase)
		{
			case Phase.Playing:
				UpdatePlaying(worldDt, now);
				break;

			case Phase.Dying:
				_phaseTimer += dt;
				if (_phaseTimer >= DyingSeconds)
				{
					Phase = Phase.GameOver;
					Notify?.Invoke(GameEvent.GameOver,
						BeatHighScore
							? $"New best: {Score}. Press R to go again."
							: $"Game over — {Score} points, length {_body.Count}.");
				}
				break;
		}

		AgeFruit(worldDt, now);
	}

	private void UpdatePlaying(float dt, double now)
	{
		_stepTimer += dt;
		float step = StepSeconds;

		// Progress feeds the renderer's interpolation; it must stay inside [0,1]
		// even on a long frame, or segments would overshoot their target cell.
		StepProgress = Math.Clamp(_stepTimer / step, 0, 1);

		if (_stepTimer < step) return;
		_stepTimer -= step;
		StepProgress = 0;

		Step(now);
	}

	/// <summary>One grid tick: the snake advances exactly one cell.</summary>
	private void Step(double now)
	{
		_previousBody.Clear();
		_previousBody.AddRange(_body);

		Facing = _turns.Dequeue(Facing);

		var head = _body[0];
		var (dx, dy) = Facing.Delta();
		var next = new Cell(head.X + dx, head.Y + dy);

		if (!InBounds(next))
		{
			if (Walls == WallMode.Solid)
			{
				Die(next, "wall");
				return;
			}

			next = new Cell((next.X + Columns) % Columns, (next.Y + Rows) % Rows);

			// The wrap is a teleport, so the interpolation has to be told not to
			// slide the head across the whole board to get there.
			_previousBody[0] = next;
		}

		// The tail cell is about to be vacated, so moving into it is legal —
		// unless the snake is about to grow into it.
		bool eating = Food != null && Food.At == next
			|| Bonus != null && Bonus.At == next;

		int selfCheckLength = eating ? _body.Count : _body.Count - 1;
		for (int i = 0; i < selfCheckLength; i++)
		{
			if (_body[i] != next) continue;
			Die(next, "itself");
			return;
		}

		_body.Insert(0, next);

		if (Food != null && Food.At == next)
		{
			Eat(Food, now);
			Food = SpawnFruit(bonus: false);
		}
		else if (Bonus != null && Bonus.At == next)
		{
			Eat(Bonus, now);
			Bonus = null;
		}
		else
		{
			_body.RemoveAt(_body.Count - 1);
		}
	}

	private void Eat(Fruit fruit, double now)
	{
		bool comboHeld = now - _lastEatTime <= ComboWindowSeconds;
		Combo = comboHeld ? Math.Min(MaxCombo, Combo + 1) : 1;
		_lastEatTime = now;

		int points = fruit.Points * Combo;
		Score += points;

		if (Score > HiScore)
		{
			bool firstTime = !BeatHighScore;
			HiScore = Score;
			if (firstTime)
			{
				BeatHighScore = true;
				Notify?.Invoke(GameEvent.NewHighScore, "New high score!");
			}
		}

		var (px, py) = CellCenter(fruit.At);

		if (fruit.Bonus)
		{
			Fx.Explode(px, py, 90, Effects.Spark, 55f, life: 0.9f, gravity: 10f);
			Fx.AddShake(2.4f);
			Fx.Flash(new Color(120, 200, 255), 0.42f);
			Fx.SlowMotion(0.16f, 0.35f);
			Notify?.Invoke(GameEvent.BonusTaken, $"Bonus fruit +{points}");
		}
		else
		{
			Fx.Explode(px, py, 34, Effects.Juice, 34f, life: 0.6f, gravity: 30f);
			Fx.AddShake(0.9f);
		}

		Fx.AddPopup(
			Combo > 1 ? $"+{points} ×{Combo}" : $"+{points}",
			px, py,
			Combo > 1 ? new Color(255, 215, 90) : new Color(230, 255, 230));

		_fruitEatenThisLevel++;
		_fruitEatenTotal++;

		if (_fruitEatenThisLevel >= FruitPerLevel)
		{
			_fruitEatenThisLevel = 0;
			Level++;
			Fx.Flash(new Color(150, 255, 190), 0.3f);
			Notify?.Invoke(GameEvent.LevelUp, $"Level {Level} — faster now.");
		}

		if (Bonus == null && _fruitEatenTotal % BonusEveryNFruit == 0)
		{
			Bonus = SpawnFruit(bonus: true);
		}
	}

	private void Die(Cell at, string cause)
	{
		if (ShieldAvailable)
		{
			// The shield has to buy a MOMENT, not a tick. Simply cancelling the
			// move leaves the head pinned against the wall still facing into it,
			// so the next tick repeats the same death and the shield bought
			// nothing — which is exactly what the first play-through showed.
			//
			// So: cancel the move, turn the snake to the first heading that is
			// actually survivable, and freeze it there long enough for the player
			// to register what happened and steer.
			ShieldAvailable = false;
			Combo = 1;

			var escape = FindEscape();
			if (escape == null)
			{
				// Boxed in with no legal move. A shield cannot save that, and
				// pretending otherwise would just stall for one more tick.
				ShieldAvailable = false;
			}
			else
			{
				Facing = escape.Value;
				_turns.Reset(Facing);
				_stepTimer = -ShieldGraceSeconds;
				StepProgress = 0;

				Fx.Flash(new Color(120, 220, 255), 0.6f);
				Fx.AddShake(3.5f);
				Fx.SlowMotion(0.35f, 0.3f);

				var (hx, hy) = CellCenter(_body[0]);
				Fx.Explode(hx, hy, 70, Effects.Spark, 45f, life: 0.8f, gravity: 0f);
				Fx.AddPopup("SHIELD", hx, hy, new Color(140, 220, 255), 1.4f);

				Notify?.Invoke(GameEvent.ShieldUsed,
					$"Shield absorbed the {cause} — steer! No more left.");
				return;
			}
		}

		DeathCell = Clamp(at);
		Phase = Phase.Dying;
		_phaseTimer = 0;
		Combo = 1;

		Fx.Flash(new Color(255, 80, 80), 0.75f);
		Fx.AddShake(6f);
		Fx.SlowMotion(0.5f, 0.25f);

		// Shed the whole body, so the snake visibly comes apart.
		foreach (var segment in _body)
		{
			var (sx, sy) = CellCenter(segment);
			Fx.Explode(sx, sy, 6, Effects.Scale, 26f, life: 1.1f, gravity: 42f);
		}

		if (Score >= HiScore && Score > 0) BeatHighScore = true;
	}

	/// <summary>
	/// The first heading that does not kill the snake immediately, preferring to
	/// carry on straight and only then turning.
	/// </summary>
	private Direction? FindEscape()
	{
		var head = _body[0];
		Span<Direction> order = stackalloc Direction[4];
		int n = 0;

		// Straight on first (it may have been a self-collision, not a wall), then
		// the two turns, never the reversal.
		order[n++] = Facing;
		foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
			if (d != Facing && !d.Reverses(Facing))
				order[n++] = d;

		for (int i = 0; i < n; i++)
		{
			var (dx, dy) = order[i].Delta();
			var next = new Cell(head.X + dx, head.Y + dy);

			if (!InBounds(next))
			{
				if (Walls == WallMode.Solid) continue;
				next = new Cell((next.X + Columns) % Columns, (next.Y + Rows) % Rows);
			}

			bool blocked = false;
			for (int b = 0; b < _body.Count - 1 && !blocked; b++)
				blocked = _body[b] == next;

			if (!blocked) return order[i];
		}

		return null;
	}

	private void AgeFruit(float dt, double now)
	{
		if (Food != null) Food.Age += dt;

		if (Bonus == null) return;

		Bonus.Age += dt;
		Bonus.Life -= dt;
		if (Bonus.Life > 0) return;

		var (px, py) = CellCenter(Bonus.At);
		Fx.Explode(px, py, 18, Effects.Spark, 16f, life: 0.5f, gravity: -8f, glow: false);
		Bonus = null;
		Notify?.Invoke(GameEvent.BonusMissed, "Bonus fruit got away.");
	}

	/// <summary>Finds a free cell, preferring one that is not immediately in front of the head.</summary>
	private Fruit? SpawnFruit(bool bonus)
	{
		if (Columns <= 0 || Rows <= 0) return null;

		// A board this full has few free cells; scanning beats guessing.
		int free = Columns * Rows - _body.Count;
		if (free <= 0) return null;

		for (int attempt = 0; attempt < 200; attempt++)
		{
			var candidate = new Cell(_rng.Next(Columns), _rng.Next(Rows));
			if (Occupied(candidate)) continue;
			return Make(candidate);
		}

		// Fallback: walk the board and take the first free cell, so a nearly full
		// board still spawns rather than silently dropping the fruit.
		for (int y = 0; y < Rows; y++)
			for (int x = 0; x < Columns; x++)
			{
				var candidate = new Cell(x, y);
				if (!Occupied(candidate)) return Make(candidate);
			}

		return null;

		Fruit Make(Cell at) => new()
		{
			At = at,
			Bonus = bonus,
			Life = bonus ? BonusLifeSeconds : 0,
			MaxLife = bonus ? BonusLifeSeconds : 1,
		};
	}

	private bool Occupied(Cell c)
	{
		if (Food != null && Food.At == c) return true;
		if (Bonus != null && Bonus.At == c) return true;
		foreach (var segment in _body)
			if (segment == c)
				return true;
		return false;
	}

	#endregion
}
