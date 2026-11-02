// -----------------------------------------------------------------------
// Breakout — the rules.
//
// Everything here is simulation: positions in pixel space, advanced by a
// fixed step. Nothing in this file draws, and nothing in it knows what a
// terminal is. The ball carries a sub-step sweep because at 120 steps a
// second a fast ball still moves far enough to tunnel through a one-pixel
// brick edge if it is only tested at step boundaries.
// -----------------------------------------------------------------------

using Games.Common;
using SharpConsoleUI;

namespace Breakout;

internal enum Phase
{
	Attract,
	Serving,
	Playing,
	LifeLost,
	Cleared,
	GameOver,
}

internal enum Drop
{
	None,
	Wide,
	Slow,
	Multi,
	Life,
}

/// <summary>A brick in the wall. Hits left at zero means broken.</summary>
internal sealed class Brick
{
	public int X, Y, Width, Height;
	public int Row;
	public int Hits;
	public float Flash;

	public bool Alive => Hits > 0;
}

internal sealed class Ball
{
	public float X, Y, Vx, Vy;
	public float Radius = 1.4f;
	public bool Held = true;
}

internal sealed class Falling
{
	public float X, Y, Vy;
	public Drop Kind;
}

/// <summary>
/// The whole game. Advanced by <see cref="Update"/> in fixed steps; the
/// caller decides how those steps are produced.
/// </summary>
internal sealed class Game
{
	public const int Columns = 10;
	public const int BrickRows = 6;

	private const float BaseBallSpeed = 46f;
	private const float SpeedPerLevel = 4f;
	private const float MaxBallSpeed = 96f;
	private const float PaddleSpeed = 78f;
	private const float DropSpeed = 22f;
	private const float ServeDelay = 0.9f;
	private const float ClearedDelay = 1.6f;
	private const float LifeLostDelay = 1.1f;
	private const float WideDuration = 12f;
	private const float SlowDuration = 9f;
	private const float SlowFactor = 0.62f;
	private const float PaddleBaseWidth = 16f;
	private const float PaddleWideWidth = 26f;
	private const float PaddleHeight = 2f;
	private const float PaddleBottomGap = 4f;
	private const float WallTop = 8f;
	private const float BrickHeight = 2f;
	private const float BrickGap = 1f;
	private const float MaxBounceAngle = 1.05f;
	private const int StartingLives = 3;
	private const int DropChancePercent = 14;

	private readonly Random _rng = new();
	private readonly List<Brick> _bricks = new();
	private readonly List<Ball> _balls = new();
	private readonly List<Falling> _drops = new();
	private readonly Effects _fx;

	private float _phaseTime;
	private float _paddleTarget;
	private int _paddleInput;

	/// <summary>
	/// Seconds since the last left/right press. The terminal reports no key release, so a held
	/// key is recognised by its auto-repeat still arriving: once the presses stop for longer
	/// than a repeat interval, the key is treated as let go and the paddle halts.
	/// </summary>
	private float _sinceSteerPress = float.PositiveInfinity;

	/// <summary>
	/// How long after a press the paddle keeps going. Comfortably longer than any OS
	/// auto-repeat interval, so a genuine hold never stutters, and short enough that letting
	/// go stops the paddle within a frame or two rather than letting it drift on.
	/// </summary>
	private const float SteerHeldSeconds = 0.14f;
	private float _wideTimer;
	private float _slowTimer;
	private bool _mouseControl;

	public Game(int highScore)
	{
		HighScore = highScore;
		_fx = new Effects(_rng, maxParticles: 1400);
	}

	/// <summary>Raised for anything the shell may want to toast or sound.</summary>
	public event EventHandler<string>? Notify;

	public int Width { get; private set; } = 120;
	public int Height { get; private set; } = 60;

	public Phase Phase { get; private set; } = Phase.Attract;
	public float PhaseTime => _phaseTime;
	public int Score { get; private set; }
	public int HighScore { get; private set; }
	public int Lives { get; private set; } = StartingLives;
	public int Level { get; private set; } = 1;
	public bool Paused { get; private set; }
	public int Combo { get; private set; }
	public int BestCombo { get; private set; }

	public float PaddleX { get; private set; }
	public float PaddleWidth => _wideTimer > 0 ? PaddleWideWidth : PaddleBaseWidth;
	public float PaddleY => Height - PaddleBottomGap;
	public float PaddleThickness => PaddleHeight;
	public bool WideActive => _wideTimer > 0;
	public bool SlowActive => _slowTimer > 0;

	public IReadOnlyList<Brick> Bricks => _bricks;
	public IReadOnlyList<Ball> Balls => _balls;
	public IReadOnlyList<Falling> Drops => _drops;
	public Effects Fx => _fx;

	public int BricksLeft => _bricks.Count(b => b.Alive);

	/// <summary>The play field changed size; rebuild anything measured in pixels.</summary>
	public void Resize(int width, int height)
	{
		if (width <= 0 || height <= 0) return;
		if (width == Width && height == Height) return;

		float oldWidth = Width;
		Width = Math.Max(40, width);
		Height = Math.Max(24, height);

		// Keep everything proportionally where it was rather than snapping to a
		// corner: a terminal resize mid-rally should not lose the ball.
		float scale = Width / oldWidth;
		PaddleX = Math.Clamp(PaddleX * scale, PaddleWidth / 2, Width - PaddleWidth / 2);
		_paddleTarget = PaddleX;
		foreach (var ball in _balls)
		{
			ball.X *= scale;
			ball.Y = Math.Min(ball.Y, Height - PaddleBottomGap - 1);
		}
		LayOutWall();
	}

	public void StartGame()
	{
		Score = 0;
		Lives = StartingLives;
		Level = 1;
		Combo = 0;
		BestCombo = 0;
		_wideTimer = 0;
		_slowTimer = 0;
		_drops.Clear();
		BuildLevel();
		Notify?.Invoke(this, "Level 1");
	}

	public void TogglePause()
	{
		if (Phase is Phase.Attract or Phase.GameOver) return;
		Paused = !Paused;
	}

	/// <summary>Keyboard steering: -1 left, 0 none, +1 right.</summary>
	public void Steer(int direction)
	{
		_paddleInput = Math.Sign(direction);
		_sinceSteerPress = 0;
		if (_paddleInput != 0) _mouseControl = false;
	}

	/// <summary>Mouse steering: the paddle centre follows the pointer.</summary>
	public void PointAt(int pixelX)
	{
		_mouseControl = true;
		_paddleTarget = pixelX;
	}

	/// <summary>Space / click: start, serve, or restart depending on phase.</summary>
	public void Launch()
	{
		switch (Phase)
		{
			case Phase.Attract:
			case Phase.GameOver:
				StartGame();
				break;

			case Phase.Serving when _phaseTime >= ServeDelay:
				Serve();
				break;
		}
	}

	/// <summary>Advances the simulation by one fixed step.</summary>
	/// <param name="dt">Step length in seconds.</param>
	public void Update(float dt)
	{
		_phaseTime += dt;
		_fx.Update(dt * _fx.TimeScale, dt);

		if (Paused) return;

		switch (Phase)
		{
			case Phase.Attract:
				AttractStep(dt);
				return;

			case Phase.Serving:
				MovePaddle(dt);
				HoldBall();
				if (_phaseTime >= ServeDelay) Serve();
				return;

			case Phase.Playing:
				PlayStep(dt);
				return;

			case Phase.LifeLost:
				MovePaddle(dt);
				if (_phaseTime >= LifeLostDelay) NextLife();
				return;

			case Phase.Cleared:
				MovePaddle(dt);
				if (_phaseTime >= ClearedDelay) NextLevel();
				return;

			case Phase.GameOver:
				return;
		}
	}

	private void PlayStep(float dt)
	{
		if (_wideTimer > 0) _wideTimer = Math.Max(0, _wideTimer - dt);
		if (_slowTimer > 0) _slowTimer = Math.Max(0, _slowTimer - dt);

		MovePaddle(dt);

		float scale = _slowTimer > 0 ? SlowFactor : 1f;
		for (int i = _balls.Count - 1; i >= 0; i--)
		{
			if (!AdvanceBall(_balls[i], dt * scale))
			{
				_balls.RemoveAt(i);
			}
		}

		AdvanceDrops(dt);

		foreach (var brick in _bricks)
		{
			if (brick.Flash > 0) brick.Flash = Math.Max(0, brick.Flash - dt * 4f);
		}

		if (_balls.Count == 0)
		{
			LoseLife();
			return;
		}

		if (BricksLeft == 0)
		{
			Phase = Phase.Cleared;
			_phaseTime = 0;
			_fx.Flash(new Color(255, 255, 255), 0.5f);
			Notify?.Invoke(this, $"Level {Level} cleared");
		}
	}

	private void MovePaddle(float dt)
	{
		float half = PaddleWidth / 2;

		if (_mouseControl)
		{
			// Ease toward the pointer rather than snapping: a teleporting paddle
			// can cross the ball without ever touching it.
			float delta = _paddleTarget - PaddleX;
			float maxStep = PaddleSpeed * 1.8f * dt;
			PaddleX += Math.Clamp(delta, -maxStep, maxStep);
		}
		else if (_paddleInput != 0)
		{
			// Let go of a direction whose auto-repeat has stopped arriving. Without this a
			// single tap latches the paddle and it slides on until the opposite key is
			// pressed, which reads as a stuck control rather than a deliberate one.
			_sinceSteerPress += dt;
			if (_sinceSteerPress > SteerHeldSeconds) _paddleInput = 0;
			else PaddleX += _paddleInput * PaddleSpeed * dt;
		}

		PaddleX = Math.Clamp(PaddleX, half, Width - half);
	}

	private void HoldBall()
	{
		foreach (var ball in _balls.Where(b => b.Held))
		{
			ball.X = PaddleX;
			ball.Y = PaddleY - ball.Radius - 0.6f;
		}
	}

	/// <summary>
	/// Moves one ball, resolving collisions along the way.
	/// </summary>
	/// <returns>False when the ball fell past the paddle and is gone.</returns>
	private bool AdvanceBall(Ball ball, float dt)
	{
		if (ball.Held)
		{
			HoldBall();
			return true;
		}

		// Sub-step so a fast ball cannot pass through a brick between frames:
		// never move more than half a brick's thickness at once.
		float distance = MathF.Sqrt(ball.Vx * ball.Vx + ball.Vy * ball.Vy) * dt;
		int slices = Math.Max(1, (int)MathF.Ceiling(distance / (BrickHeight * 0.5f)));
		float sliceDt = dt / slices;

		for (int s = 0; s < slices; s++)
		{
			ball.X += ball.Vx * sliceDt;
			ball.Y += ball.Vy * sliceDt;

			if (ball.X - ball.Radius <= 0)
			{
				ball.X = ball.Radius;
				ball.Vx = MathF.Abs(ball.Vx);
				_fx.AddShake(0.5f);
			}
			else if (ball.X + ball.Radius >= Width)
			{
				ball.X = Width - ball.Radius;
				ball.Vx = -MathF.Abs(ball.Vx);
				_fx.AddShake(0.5f);
			}

			if (ball.Y - ball.Radius <= 0)
			{
				ball.Y = ball.Radius;
				ball.Vy = MathF.Abs(ball.Vy);
				_fx.AddShake(0.5f);
			}

			HitBricks(ball);
			HitPaddle(ball);

			if (ball.Y - ball.Radius > Height)
			{
				return false;
			}
		}

		return true;
	}

	private void HitBricks(Ball ball)
	{
		foreach (var brick in _bricks)
		{
			if (!brick.Alive) continue;
			if (ball.X + ball.Radius < brick.X || ball.X - ball.Radius > brick.X + brick.Width) continue;
			if (ball.Y + ball.Radius < brick.Y || ball.Y - ball.Radius > brick.Y + brick.Height) continue;

			// Reflect on whichever axis the ball entered from, decided by which
			// overlap is shallower.
			float overlapX = Math.Min(
				ball.X + ball.Radius - brick.X,
				brick.X + brick.Width - (ball.X - ball.Radius));
			float overlapY = Math.Min(
				ball.Y + ball.Radius - brick.Y,
				brick.Y + brick.Height - (ball.Y - ball.Radius));

			if (overlapY <= overlapX)
			{
				ball.Vy = -ball.Vy;
				ball.Y += ball.Vy > 0 ? overlapY : -overlapY;
			}
			else
			{
				ball.Vx = -ball.Vx;
				ball.X += ball.Vx > 0 ? overlapX : -overlapX;
			}

			BreakBrick(brick, ball);
			return;
		}
	}

	private void BreakBrick(Brick brick, Ball ball)
	{
		brick.Hits--;
		brick.Flash = 1f;

		var color = Sprites.RowColors[brick.Row % Sprites.RowColors.Length];

		if (brick.Alive)
		{
			_fx.Explode(ball.X, ball.Y, 6, Palettes.Spark, 14f, 0.3f, 6f);
			_fx.AddShake(0.6f);
			return;
		}

		Combo++;
		BestCombo = Math.Max(BestCombo, Combo);

		int tier = BrickRows - brick.Row;
		int gained = tier * 10 * Math.Max(1, Combo / 4 + 1);
		Score += gained;
		if (Score > HighScore) HighScore = Score;

		_fx.Explode(
			brick.X + brick.Width / 2f,
			brick.Y + brick.Height / 2f,
			18, Palettes.FromColor(color), 26f, 0.6f, 18f);
		_fx.AddPopup($"+{gained}", brick.X + brick.Width / 2f, brick.Y, new Color(255, 245, 200));
		_fx.AddShake(1.1f);

		if (Combo > 0 && Combo % 8 == 0)
		{
			_fx.Flash(color, 0.22f);
			Notify?.Invoke(this, $"{Combo} in a row");
		}

		MaybeDrop(brick);
	}

	private void MaybeDrop(Brick brick)
	{
		if (_rng.Next(100) >= DropChancePercent) return;

		var kind = _rng.Next(4) switch
		{
			0 => Drop.Wide,
			1 => Drop.Slow,
			2 => Drop.Multi,
			_ => Drop.Life,
		};

		_drops.Add(new Falling
		{
			X = brick.X + brick.Width / 2f,
			Y = brick.Y + brick.Height,
			Vy = DropSpeed,
			Kind = kind,
		});
	}

	private void AdvanceDrops(float dt)
	{
		for (int i = _drops.Count - 1; i >= 0; i--)
		{
			var drop = _drops[i];
			drop.Y += drop.Vy * dt;

			bool caught =
				drop.Y >= PaddleY - 1 &&
				drop.Y <= PaddleY + PaddleHeight + 1 &&
				MathF.Abs(drop.X - PaddleX) <= PaddleWidth / 2 + 2;

			if (caught)
			{
				Collect(drop.Kind);
				_drops.RemoveAt(i);
			}
			else if (drop.Y > Height)
			{
				_drops.RemoveAt(i);
			}
		}
	}

	private void Collect(Drop kind)
	{
		switch (kind)
		{
			case Drop.Wide:
				_wideTimer = WideDuration;
				_fx.AddPopup("WIDE", PaddleX, PaddleY - 3, new Color(255, 210, 110));
				Notify?.Invoke(this, "Wide paddle");
				break;

			case Drop.Slow:
				_slowTimer = SlowDuration;
				_fx.AddPopup("SLOW", PaddleX, PaddleY - 3, new Color(130, 200, 255));
				Notify?.Invoke(this, "Slow ball");
				break;

			case Drop.Multi:
				SplitBalls();
				_fx.AddPopup("MULTI", PaddleX, PaddleY - 3, new Color(190, 140, 255));
				Notify?.Invoke(this, "Multi ball");
				break;

			case Drop.Life:
				Lives++;
				_fx.AddPopup("LIFE", PaddleX, PaddleY - 3, new Color(120, 235, 150));
				Notify?.Invoke(this, "Extra life");
				break;
		}

		_fx.Flash(new Color(255, 255, 210), 0.18f);
	}

	private void SplitBalls()
	{
		var seeds = _balls.Where(b => !b.Held).ToList();
		if (seeds.Count == 0) return;

		foreach (var seed in seeds)
		{
			if (_balls.Count >= 6) break;

			for (int i = -1; i <= 1; i += 2)
			{
				if (_balls.Count >= 6) break;

				float speed = MathF.Sqrt(seed.Vx * seed.Vx + seed.Vy * seed.Vy);
				float angle = MathF.Atan2(seed.Vy, seed.Vx) + i * 0.4f;
				_balls.Add(new Ball
				{
					X = seed.X,
					Y = seed.Y,
					Vx = MathF.Cos(angle) * speed,
					Vy = MathF.Sin(angle) * speed,
					Radius = seed.Radius,
					Held = false,
				});
			}
		}
	}

	private void HitPaddle(Ball ball)
	{
		if (ball.Vy <= 0) return;
		if (ball.Y + ball.Radius < PaddleY) return;
		if (ball.Y - ball.Radius > PaddleY + PaddleHeight) return;

		float half = PaddleWidth / 2;
		if (ball.X < PaddleX - half - 1 || ball.X > PaddleX + half + 1) return;

		// Where it landed decides the angle: edges steer, the middle goes straight
		// up. This is the whole game's steering, so it must not be symmetrical
		// around a dead centre that produces an unwinnable vertical rally.
		float offset = Math.Clamp((ball.X - PaddleX) / half, -1f, 1f);
		float angle = offset * MaxBounceAngle;
		float speed = Math.Min(MaxBallSpeed, CurrentSpeed() * 1.012f);

		ball.Vx = MathF.Sin(angle) * speed;
		ball.Vy = -MathF.Cos(angle) * speed;
		ball.Y = PaddleY - ball.Radius - 0.1f;

		Combo = 0;
		_fx.AddShake(0.7f);
		_fx.Explode(ball.X, PaddleY, 5, Palettes.Spark, 12f, 0.25f, 4f);
	}

	private float CurrentSpeed()
	{
		float fastest = 0;
		foreach (var ball in _balls)
		{
			fastest = Math.Max(fastest, MathF.Sqrt(ball.Vx * ball.Vx + ball.Vy * ball.Vy));
		}
		return fastest > 0 ? fastest : BaseBallSpeed + (Level - 1) * SpeedPerLevel;
	}

	private void AttractStep(float dt)
	{
		// The demo paddle tracks a ball that bounces around on its own, so the
		// title screen is the game playing itself rather than a static picture.
		if (_balls.Count == 0)
		{
			_balls.Add(new Ball
			{
				X = Width * 0.5f,
				Y = Height * 0.45f,
				Vx = BaseBallSpeed * 0.6f,
				Vy = BaseBallSpeed * 0.5f,
				Held = false,
			});
		}

		var ball = _balls[0];
		ball.X += ball.Vx * dt;
		ball.Y += ball.Vy * dt;

		if (ball.X < ball.Radius || ball.X > Width - ball.Radius) ball.Vx = -ball.Vx;
		if (ball.Y < ball.Radius || ball.Y > Height - PaddleBottomGap - 2) ball.Vy = -ball.Vy;
		ball.X = Math.Clamp(ball.X, ball.Radius, Width - ball.Radius);

		_paddleTarget = ball.X;
		_mouseControl = true;
		MovePaddle(dt);
	}

	private void BuildLevel()
	{
		_balls.Clear();
		_drops.Clear();
		Combo = 0;
		LayOutWall();
		PaddleX = Width / 2f;
		_paddleTarget = PaddleX;
		BeginServe();
	}

	private void LayOutWall()
	{
		_bricks.Clear();

		float margin = 3f;
		float usable = Width - margin * 2;
		float cellWidth = usable / Columns;

		for (int row = 0; row < BrickRows; row++)
		{
			for (int col = 0; col < Columns; col++)
			{
				// From level 3 on, every other brick in the top two rows takes two
				// hits, so a level is not simply wider but denser.
				int hits = Level >= 3 && row < 2 && (col + row) % 2 == 0 ? 2 : 1;

				_bricks.Add(new Brick
				{
					X = (int)(margin + col * cellWidth),
					Y = (int)(WallTop + row * (BrickHeight + BrickGap)),
					Width = (int)(cellWidth - BrickGap),
					Height = (int)BrickHeight,
					Row = row,
					Hits = hits,
				});
			}
		}
	}

	private void BeginServe()
	{
		Phase = Phase.Serving;
		_phaseTime = 0;
		_balls.Clear();
		_balls.Add(new Ball { Held = true, X = PaddleX, Y = PaddleY - 2 });
	}

	private void Serve()
	{
		float speed = BaseBallSpeed + (Level - 1) * SpeedPerLevel;
		float angle = (_rng.NextSingle() - 0.5f) * 0.6f;

		foreach (var ball in _balls)
		{
			ball.Held = false;
			ball.Vx = MathF.Sin(angle) * speed;
			ball.Vy = -MathF.Cos(angle) * speed;
		}

		Phase = Phase.Playing;
		_phaseTime = 0;
	}

	private void LoseLife()
	{
		Lives--;
		Combo = 0;
		_wideTimer = 0;
		_slowTimer = 0;
		_drops.Clear();
		_fx.AddShake(3.2f);
		_fx.Flash(new Color(255, 70, 70), 0.42f);

		if (Lives <= 0)
		{
			Phase = Phase.GameOver;
			_phaseTime = 0;
			Notify?.Invoke(this, $"Game over - {Score}");
			return;
		}

		Phase = Phase.LifeLost;
		_phaseTime = 0;
		Notify?.Invoke(this, $"{Lives} left");
	}

	private void NextLife()
	{
		PaddleX = Width / 2f;
		_paddleTarget = PaddleX;
		BeginServe();
	}

	private void NextLevel()
	{
		Level++;
		BuildLevel();
		Notify?.Invoke(this, $"Level {Level}");
	}
}
