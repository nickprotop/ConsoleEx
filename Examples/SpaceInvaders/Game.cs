// -----------------------------------------------------------------------
// SpaceInvaders — game simulation.
//
// Pure game state + rules in pixel space; no UI types beyond Color. The
// window's UI-thread loop (Program) feeds it key presses, mouse state and
// fixed time steps, and the Renderer draws whatever it finds here.
//
// Classic rules with modern feel: the formation marches in discrete steps
// that speed up as invaders die, shields erode pixel by pixel, a mystery
// UFO crosses the top, and a combo multiplier rewards unbroken accuracy.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace SpaceInvaders;

internal enum Phase { Title, WaveIntro, Playing, PlayerDown, WaveCleared, GameOver }

internal enum InvaderKind { Squid, Crab, Octopus }

internal enum GameEvent { WaveCleared, ExtraLife, NewHighScore, GameOver }

internal sealed class Invader
{
	public required int Col { get; init; }
	public required int Row { get; init; }
	public required InvaderKind Kind { get; init; }
	public bool Alive { get; set; } = true;
	public float EntryDelay { get; init; }

	/// <summary>Seconds the death burst sprite remains on screen.</summary>
	public float BurstTimer { get; set; }

	public int Points => Kind switch { InvaderKind.Squid => 30, InvaderKind.Crab => 20, _ => 10 };

	public Sprite[] Frames => Kind switch
	{
		InvaderKind.Squid => Sprites.Squid,
		InvaderKind.Crab => Sprites.Crab,
		_ => Sprites.Octopus,
	};
}

internal sealed class Shot
{
	public float X { get; set; }
	public float Y { get; set; }
	public float Vy { get; set; }
	public int Kind { get; set; }
}

internal sealed class Shield
{
	public int X { get; set; }
	public int Y { get; set; }
	public bool[,] Mask { get; } = Sprites.Shield.CloneMask();
	public int Width => Sprites.Shield.Width;
	public int Height => Sprites.Shield.Height;

	public bool Hit(int px, int py)
	{
		int lx = px - X, ly = py - Y;
		return (uint)lx < (uint)Width && (uint)ly < (uint)Height && Mask[lx, ly];
	}
}

internal sealed class Ufo
{
	public bool Active { get; set; }
	public float X { get; set; }
	public int Dir { get; set; }
	public int Points { get; set; }
	public const int Y = 2;
}

internal sealed class Game
{
	#region Tuning

	public const int MinColumns = 60;
	public const int MinRows = 24;

	private const int ColSpacing = 9;
	private const int RowSpacing = 6;
	private const int FormationRows = 5;
	private const float PlayerSpeed = 58f;
	private const float MouseFollowSpeed = 150f;
	private const float TapStep = 3f;
	private const float MaxStepLead = 6f;
	private const float PlayerShotSpeed = 125f;
	private const float FireCooldown = 0.24f;
	private const int MaxPlayerShots = 2;
	private const float EntryDuration = 0.75f;
	private const int FirstExtraLife = 1500;
	private const int ExtraLifeEvery = 5000;
	private const int MaxLives = 6;

	#endregion

	private readonly Random _rng = new();

	public Game(int hiScore)
	{
		HiScore = hiScore;
		Fx = new Effects(_rng);
		Repeat = new KeyRepeatModel();
		LeftKey = new HeldKey(Repeat);
		RightKey = new HeldKey(Repeat);
		FireKey = new HeldKey(Repeat);
	}

	/// <summary>Raised for HUD toasts and persistence.</summary>
	public event Action<GameEvent, string>? Notify;

	#region State

	public Phase Phase { get; private set; } = Phase.Title;
	public float PhaseTime { get; private set; }
	public bool Paused { get; private set; }

	/// <summary>Accumulated world time (slows during hit-stop, stops when paused).</summary>
	public float Time { get; private set; }

	public int Width { get; private set; }
	public int Height { get; private set; }
	public bool TooSmall => Width < MinColumns || Height < MinRows * 2;

	public int Score { get; private set; }
	public int HiScore { get; private set; }
	public int Wave { get; private set; }
	public int Lives { get; private set; }
	public int Streak { get; private set; }
	public int Multiplier => 1 + Math.Min(Streak / 5, 4);
	public int ShotsFired { get; private set; }
	public int ShotsHit { get; private set; }
	public double Accuracy => ShotsFired == 0 ? 0 : (double)ShotsHit / ShotsFired;
	public bool BeatHighScore { get; private set; }
	public int LastWaveBonus { get; private set; }

	public float PlayerX { get; private set; }
	public int PlayerY => Height - 7;
	public float PlayerVelocity { get; private set; }
	public float Invulnerable { get; private set; }
	public bool PlayerAlive => Phase is Phase.WaveIntro or Phase.Playing or Phase.WaveCleared;

	public List<Invader> Invaders { get; } = new();
	public int Columns { get; private set; }
	public float FormX { get; private set; }
	public float FormY { get; private set; }
	public int AnimFrame { get; private set; }

	/// <summary>World time of the last formation step — the renderer pulses on the beat.</summary>
	public float LastStepTime { get; private set; }

	public int InvadersAlive { get; private set; }
	public int InvadersTotal => Invaders.Count;

	public List<Shot> PlayerShots { get; } = new();
	public List<Shot> Bombs { get; } = new();
	public List<Shield> Shields { get; } = new();
	public Ufo Ufo { get; } = new();
	public Effects Fx { get; }

	public KeyboardMode Mode { get; private set; } = KeyboardMode.Step;
	public KeyRepeatModel Repeat { get; }
	public HeldKey LeftKey { get; }
	public HeldKey RightKey { get; }
	public HeldKey FireKey { get; }

	/// <summary>Formation danger in [0,1]: how far the invaders have descended towards the ship.</summary>
	public double Threat { get; private set; }

	private int _formDir = 1;
	private float _formTargetY;
	private float _stepTimer;
	private float _bombTimer;
	private float _ufoTimer;
	private float _fireCooldown;
	private bool _fireRequested;
	private double _fireRequestTime;
	private int _latchDir;
	private float? _stepTarget;
	private bool _mouseFollow;
	private int _mouseCellX;
	private double _lastKeyMove = double.NegativeInfinity;
	private bool _invaded;
	private int _nextExtraLife;
	private int _waveShots, _waveHits;

	#endregion

	#region Lifecycle

	/// <summary>Adapts to the canvas size (in cells). Safe to call every frame.</summary>
	public void Resize(int columns, int rows)
	{
		int w = columns, h = rows * 2;
		if (w == Width && h == Height) return;

		bool first = Width == 0;
		Width = w;
		Height = h;
		if (first || TooSmall) return;

		// Keep the round going: re-anchor the ship, shields and formation.
		PlayerX = Math.Clamp(PlayerX, 1, Width - Sprites.Player.Width - 1);
		LayoutShields(keepMasks: true);
		ClampFormation();
	}

	public void StartGame()
	{
		Score = 0;
		Lives = 3;
		Wave = 0;
		Streak = 0;
		ShotsFired = ShotsHit = 0;
		BeatHighScore = false;
		_invaded = false;
		_nextExtraLife = FirstExtraLife;
		Fx.Clear();
		PlayerX = (Width - Sprites.Player.Width) / 2f;
		Paused = false;
		NextWave();
	}

	private void NextWave()
	{
		Wave++;
		_waveShots = _waveHits = 0;
		Columns = Math.Clamp((Width - 24) / ColSpacing, 5, 11);
		Invaders.Clear();
		for (int row = 0; row < FormationRows; row++)
		{
			var kind = row == 0 ? InvaderKind.Squid : row < 3 ? InvaderKind.Crab : InvaderKind.Octopus;
			for (int col = 0; col < Columns; col++)
			{
				Invaders.Add(new Invader
				{
					Col = col,
					Row = row,
					Kind = kind,
					// Rows land bottom-up, sweeping across each row.
					EntryDelay = (FormationRows - 1 - row) * 0.16f + col * 0.03f,
				});
			}
		}
		InvadersAlive = Invaders.Count;

		int formationWidth = (Columns - 1) * ColSpacing + Sprites.InvaderWidth;
		FormX = (Width - formationWidth) / 2f;
		_formTargetY = 9 + Math.Min(Wave - 1, 6) * 2;
		FormY = _formTargetY;
		_formDir = 1;
		_stepTimer = StepInterval();
		_bombTimer = 1.6f;
		_ufoTimer = 12f + (float)_rng.NextDouble() * 8f;
		Ufo.Active = false;
		PlayerShots.Clear();
		Bombs.Clear();
		LayoutShields(keepMasks: false);
		SetPhase(Phase.WaveIntro);
	}

	private void LayoutShields(bool keepMasks)
	{
		const int count = 4;
		if (!keepMasks || Shields.Count != count)
		{
			Shields.Clear();
			for (int i = 0; i < count; i++) Shields.Add(new Shield());
		}

		int span = Width - 8;
		for (int i = 0; i < count; i++)
		{
			var s = Shields[i];
			s.X = 4 + (int)((i + 0.5) * span / count) - s.Width / 2;
			s.Y = PlayerY - 11;
		}
	}

	private void ClampFormation()
	{
		if (InvadersAlive == 0) return;
		var (minX, maxX) = AliveBounds();
		if (maxX > Width - 1) FormX -= maxX - (Width - 1);
		if (minX < 1) FormX += 1 - minX;
	}

	private void SetPhase(Phase phase)
	{
		Phase = phase;
		PhaseTime = 0;
	}

	public float IntroDuration =>
		(FormationRows - 1) * 0.16f + (Columns - 1) * 0.03f + EntryDuration + 0.35f;

	#endregion

	#region Input (called on the UI thread from PreviewKeyPressed)

	public void PressLeft(double now)
	{
		RightKey.Release();
		bool fresh = LeftKey.Press(now);
		if (Mode == KeyboardMode.Step) StepBy(-1, fresh);
		_latchDir = -1;
		_mouseFollow = false;
		_lastKeyMove = now;
	}

	public void PressRight(double now)
	{
		LeftKey.Release();
		bool fresh = RightKey.Press(now);
		if (Mode == KeyboardMode.Step) StepBy(1, fresh);
		_latchDir = 1;
		_mouseFollow = false;
		_lastKeyMove = now;
	}

	public void PressStop()
	{
		LeftKey.Release();
		RightKey.Release();
		_latchDir = 0;
		_stepTarget = null;
	}

	/// <summary>
	/// Step mode: each key event moves the ship one step. A fresh press is a
	/// fixed nudge; auto-repeats step by speed × repeat interval, so a hold
	/// moves at the normal ship speed whatever the OS repeat rate is.
	/// </summary>
	private void StepBy(int dir, bool fresh)
	{
		float step = fresh
			? TapStep
			: Math.Clamp((float)(PlayerSpeed * Repeat.Interval), 1f, TapStep);

		// Keep stacking steps in the same direction; reversing starts from the ship.
		float from = _stepTarget is float t && Math.Sign(t - PlayerX) == dir ? t : PlayerX;
		float target = Math.Clamp(from + dir * step, PlayerX - MaxStepLead, PlayerX + MaxStepLead);
		_stepTarget = Math.Clamp(target, 1, Width - Sprites.Player.Width - 1);
	}

	public void PressFire(double now)
	{
		if (Phase == Phase.Title || (Phase == Phase.GameOver && PhaseTime > 1.2f))
		{
			StartGame();
			return;
		}

		if (FireKey.Press(now))
		{
			_fireRequested = true;
			_fireRequestTime = now;
		}
	}

	public void TogglePause()
	{
		if (Phase is Phase.Title or Phase.GameOver) return;
		Paused = !Paused;
	}

	public void ToggleKeyboardMode()
	{
		Mode = Mode switch
		{
			KeyboardMode.Step => KeyboardMode.Glide,
			KeyboardMode.Glide => KeyboardMode.Latch,
			_ => KeyboardMode.Step,
		};
		PressStop();
	}

	/// <summary>Applies mouse state gathered on the input thread since the last frame.</summary>
	public void ApplyMouse(int cellX, double lastMove, int clicks, double now)
	{
		if (cellX >= 0 && lastMove > _lastKeyMove && now - lastMove < 3)
		{
			_mouseFollow = true;
			_mouseCellX = cellX;
			_latchDir = 0;
			_stepTarget = null;
		}

		for (int i = 0; i < clicks; i++)
		{
			if (Phase == Phase.Title || (Phase == Phase.GameOver && PhaseTime > 1.2f))
			{
				StartGame();
				return;
			}
			_fireRequested = true;
			_fireRequestTime = now;
		}
	}

	#endregion

	#region Simulation

	/// <summary>Advances the game by one fixed real-time step.</summary>
	public void Update(float dt, double now)
	{
		if (TooSmall || Paused) return;

		float worldDt = dt * Fx.TimeScale;
		Time += worldDt;
		PhaseTime += dt;
		Fx.Update(worldDt, dt);

		foreach (var inv in Invaders)
			if (inv.BurstTimer > 0) inv.BurstTimer -= worldDt;

		switch (Phase)
		{
			case Phase.Title:
				break;

			case Phase.WaveIntro:
				UpdatePlayer(worldDt, now);
				if (PhaseTime >= IntroDuration) SetPhase(Phase.Playing);
				break;

			case Phase.Playing:
				UpdatePlayer(worldDt, now);
				UpdateFiring(worldDt, now);
				UpdateFormation(worldDt);
				UpdateBombs(worldDt);
				UpdateUfo(worldDt);
				UpdatePlayerShots(worldDt);
				UpdateThreat();
				if (InvadersAlive == 0 && Phase == Phase.Playing) ClearWave();
				break;

			case Phase.PlayerDown:
				UpdatePlayerShots(worldDt);
				UpdateUfo(worldDt);
				if (PhaseTime >= 2.0f) Respawn();
				break;

			case Phase.WaveCleared:
				UpdatePlayer(worldDt, now);
				UpdatePlayerShots(worldDt);
				if (PhaseTime >= 2.8f) NextWave();
				break;

			case Phase.GameOver:
				UpdateBombs(worldDt);
				break;
		}
	}

	private void UpdatePlayer(float dt, double now)
	{
		if (Invulnerable > 0) Invulnerable -= dt;

		double dir = Mode switch
		{
			KeyboardMode.Glide => (RightKey.IsHeld(now) ? 1 : 0) - (LeftKey.IsHeld(now) ? 1 : 0),
			KeyboardMode.Latch => _latchDir,
			_ => 0,
		};

		float before = PlayerX;
		if (_stepTarget is float stepTarget)
		{
			// Glide quickly onto the step target: direct, but never a teleport.
			float delta = stepTarget - PlayerX;
			PlayerX += delta * Math.Min(1f, dt * 30f);
			if (Math.Abs(stepTarget - PlayerX) < 0.05f) { PlayerX = stepTarget; _stepTarget = null; }
		}
		else if (dir != 0)
		{
			PlayerX += (float)(dir * PlayerSpeed * dt);
		}
		else if (_mouseFollow)
		{
			float target = _mouseCellX - Sprites.Player.Width / 2f;
			float delta = target - PlayerX;
			float max = MouseFollowSpeed * dt;
			// Ease in on the target instead of snapping, capped to a max speed.
			PlayerX += Math.Clamp(delta * Math.Min(1f, dt * 14f), -max, max);
		}

		PlayerX = Math.Clamp(PlayerX, 1, Width - Sprites.Player.Width - 1);
		PlayerVelocity = dt > 0 ? (PlayerX - before) / dt : 0;
	}

	private void UpdateFiring(float dt, double now)
	{
		if (_fireCooldown > 0) _fireCooldown -= dt;

		bool canFire = _fireCooldown <= 0 && PlayerShots.Count < MaxPlayerShots;
		if (!canFire) return;

		// A fresh press is buffered briefly so a tap during cooldown still fires;
		// a confirmed hold (auto-repeat) becomes autofire.
		bool buffered = _fireRequested && now - _fireRequestTime < 0.25;
		bool held = FireKey.Repeating && FireKey.IsHeld(now);
		if (!buffered && !held) return;

		_fireRequested = false;
		float x = PlayerX + Sprites.Player.Width / 2;
		PlayerShots.Add(new Shot { X = x, Y = PlayerY - 3, Vy = -PlayerShotSpeed });
		ShotsFired++;
		_waveShots++;
		_fireCooldown = FireCooldown;
		Fx.Jet(x, PlayerY - 1, 6, Effects.Plasma, -40f, 18f);
	}

	private float StepInterval()
	{
		double alive = Invaders.Count == 0 ? 1 : (double)InvadersAlive / Invaders.Count;
		double interval = 0.02 + 0.40 * Math.Pow(alive, 1.35);
		double waveFactor = Math.Max(0.5, 1 - 0.07 * (Wave - 1));
		return (float)(interval * waveFactor);
	}

	private void UpdateFormation(float dt)
	{
		_stepTimer -= dt;
		int guard = 0;
		while (_stepTimer <= 0 && guard++ < 3)
		{
			StepFormation();
			_stepTimer += StepInterval();
		}
		if (_stepTimer < 0) _stepTimer = 0;
	}

	private void StepFormation()
	{
		if (InvadersAlive == 0) return;

		var (minX, maxX) = AliveBounds();
		const int dx = 2;
		if (minX + dx * _formDir < 1 || maxX + dx * _formDir > Width - 1)
		{
			FormY += 3;
			_formDir = -_formDir;
		}
		else
		{
			FormX += dx * _formDir;
		}

		AnimFrame ^= 1;
		LastStepTime = Time;

		// Invaders plough through shields and, if they reach the ship's row, it's over.
		foreach (var inv in Invaders)
		{
			if (!inv.Alive) continue;
			var (ix, iy) = InvaderPosition(inv);
			foreach (var shield in Shields)
				for (int y = 0; y < Sprites.InvaderHeight; y++)
					for (int x = 0; x < Sprites.InvaderWidth; x++)
					{
						int lx = ix + x - shield.X, ly = iy + y - shield.Y;
						if ((uint)lx < (uint)shield.Width && (uint)ly < (uint)shield.Height)
							shield.Mask[lx, ly] = false;
					}

			if (iy + Sprites.InvaderHeight >= PlayerY + 1)
			{
				Invasion();
				return;
			}
		}
	}

	private (int MinX, int MaxX) AliveBounds()
	{
		int minCol = int.MaxValue, maxCol = int.MinValue;
		foreach (var inv in Invaders)
		{
			if (!inv.Alive) continue;
			minCol = Math.Min(minCol, inv.Col);
			maxCol = Math.Max(maxCol, inv.Col);
		}
		return ((int)FormX + minCol * ColSpacing, (int)FormX + maxCol * ColSpacing + Sprites.InvaderWidth);
	}

	/// <summary>Formation-space position of an invader (top-left, pixels).</summary>
	public (int X, int Y) InvaderPosition(Invader inv) =>
		((int)FormX + inv.Col * ColSpacing, (int)FormY + inv.Row * RowSpacing);

	/// <summary>Position including the wave-intro fly-in.</summary>
	public (int X, int Y, float Arrival) InvaderDrawPosition(Invader inv)
	{
		var (x, y) = InvaderPosition(inv);
		if (Phase != Phase.WaveIntro) return (x, y, 1);

		double t = Math.Clamp((PhaseTime - inv.EntryDelay) / EntryDuration, 0, 1);
		double eased = SharpConsoleUI.Animation.EasingFunctions.Bounce(t);
		int startY = -Sprites.InvaderHeight - 2 - inv.Row * 2;
		int sway = (int)Math.Round(Math.Sin(t * Math.PI) * (inv.Col - Columns / 2.0) * 1.5);
		return (x + sway, (int)Math.Round(startY + (y - startY) * eased), (float)t);
	}

	private void UpdateBombs(float dt)
	{
		if (Phase == Phase.Playing)
		{
			_bombTimer -= dt;
			int maxBombs = Math.Min(3 + Wave / 2, 7);
			if (_bombTimer <= 0 && Bombs.Count < maxBombs && InvadersAlive > 0)
			{
				DropBomb();
				double alive = (double)InvadersAlive / Invaders.Count;
				double waveFactor = Math.Max(0.4, 1 - 0.08 * (Wave - 1));
				_bombTimer = (float)((0.35 + _rng.NextDouble() * 0.8) * waveFactor * (0.55 + 0.45 * alive));
			}
		}

		for (int i = Bombs.Count - 1; i >= 0; i--)
		{
			var b = Bombs[i];
			b.Y += b.Vy * dt;
			int bx = (int)b.X, tip = (int)b.Y + 2;

			var shield = Shields.FirstOrDefault(s => s.Hit(bx, tip));
			if (shield != null)
			{
				DamageShield(shield, bx, tip, downward: true);
				Bombs.RemoveAt(i);
				continue;
			}

			if (Phase == Phase.Playing && Invulnerable <= 0 && HitsPlayer(bx, (int)b.Y, 3))
			{
				Bombs.RemoveAt(i);
				KillPlayer();
				continue;
			}

			if (tip >= Height - 2)
			{
				Fx.Jet(b.X, Height - 3, 5, Effects.Fire, -22f, 30f);
				Bombs.RemoveAt(i);
			}
		}
	}

	private void DropBomb()
	{
		// The lowest living invader in each column can fire; favour the column above the ship.
		var shooters = Invaders.Where(v => v.Alive)
			.GroupBy(v => v.Col)
			.Select(g => g.MaxBy(v => v.Row)!)
			.ToList();
		if (shooters.Count == 0) return;

		Invader shooter;
		float shipCenter = PlayerX + Sprites.Player.Width / 2f;
		if (_rng.NextDouble() < 0.45)
			shooter = shooters.MinBy(v => Math.Abs(InvaderPosition(v).X + 3 - shipCenter))!;
		else
			shooter = shooters[_rng.Next(shooters.Count)];

		var (x, y) = InvaderPosition(shooter);
		int kind = _rng.Next(3);
		float speed = 30f + Wave * 2.5f;
		Bombs.Add(new Shot { X = x + 3, Y = y + Sprites.InvaderHeight, Vy = kind == 1 ? speed * 1.35f : speed, Kind = kind });
	}

	private bool HitsPlayer(int x, int y, int length)
	{
		int px = (int)PlayerX;
		for (int i = 0; i < length; i++)
			if (Sprites.Player[x - px, y + i - PlayerY]) return true;
		return false;
	}

	private void UpdateUfo(float dt)
	{
		if (!Ufo.Active)
		{
			if (Phase != Phase.Playing) return;
			_ufoTimer -= dt;
			if (_ufoTimer <= 0 && InvadersAlive >= 8)
			{
				Ufo.Active = true;
				Ufo.Dir = _rng.Next(2) == 0 ? 1 : -1;
				Ufo.X = Ufo.Dir > 0 ? -Sprites.Ufo.Width : Width;
				Ufo.Points = new[] { 50, 100, 150, 300 }[_rng.Next(4)];
				_ufoTimer = 16f + (float)_rng.NextDouble() * 10f;
			}
			return;
		}

		Ufo.X += Ufo.Dir * 24f * dt;
		if (Ufo.X < -Sprites.Ufo.Width - 2 || Ufo.X > Width + 2) Ufo.Active = false;
	}

	private void UpdatePlayerShots(float dt)
	{
		for (int i = PlayerShots.Count - 1; i >= 0; i--)
		{
			var s = PlayerShots[i];
			float prevY = s.Y;
			s.Y += s.Vy * dt;
			int sx = (int)s.X;

			if (s.Y < 0)
			{
				PlayerShots.RemoveAt(i);
				Miss();
				continue;
			}

			if (ShotHitsSomething(s, sx, (int)s.Y, (int)prevY))
				PlayerShots.RemoveAt(i);
		}
	}

	private bool ShotHitsSomething(Shot s, int sx, int top, int prevTop)
	{
		// Sweep from the previous position so nothing is skipped at high speed.
		for (int y = prevTop + 2; y >= top; y--)
		{
			if (Ufo.Active && sx >= (int)Ufo.X && sx < (int)Ufo.X + Sprites.Ufo.Width
				&& y >= Ufo.Y && y < Ufo.Y + Sprites.Ufo.Height)
			{
				KillUfo();
				return true;
			}

			foreach (var inv in Invaders)
			{
				if (!inv.Alive) continue;
				var (ix, iy) = InvaderPosition(inv);
				if (inv.Frames[AnimFrame][sx - ix, y - iy])
				{
					KillInvader(inv, ix, iy);
					return true;
				}
			}

			var shield = Shields.FirstOrDefault(sh => sh.Hit(sx, y));
			if (shield != null)
			{
				DamageShield(shield, sx, y, downward: false);
				Miss();
				return true;
			}

			for (int b = Bombs.Count - 1; b >= 0; b--)
			{
				var bomb = Bombs[b];
				if (Math.Abs((int)bomb.X - sx) <= 1 && y >= (int)bomb.Y - 1 && y <= (int)bomb.Y + 3)
				{
					Bombs.RemoveAt(b);
					Fx.Explode(sx, y, 10, Effects.Plasma, 30f, 0.4f, 0f);
					AddScore(5, sx, y, quiet: true);
					return true;
				}
			}
		}
		return false;
	}

	private void Miss() => Streak = 0;

	private void KillInvader(Invader inv, int x, int y)
	{
		inv.Alive = false;
		inv.BurstTimer = 0.16f;
		InvadersAlive--;
		ShotsHit++;
		_waveHits++;
		Streak++;

		int mult = Multiplier;
		AddScore(inv.Points * mult, x + 3, y, quiet: false, mult);

		var palette = InvaderPalette(inv.Kind);
		Fx.Shatter(inv.Frames[AnimFrame], x, y, palette, 26f);
		Fx.Explode(x + 3, y + 2, 8, Effects.Fire, 34f, 0.5f);
		Fx.AddShake(0.9f);

		if (InvadersAlive == 0)
		{
			Fx.SlowMotion(0.7f, 0.25f);
			Fx.Flash(Color.White, 0.35f);
			Fx.AddShake(3f);
		}
	}

	private void KillUfo()
	{
		int x = (int)Ufo.X, y = Ufo.Y;
		Ufo.Active = false;
		ShotsHit++;
		_waveHits++;
		Streak++;
		AddScore(Ufo.Points, x + 5, y + 4, quiet: false);
		Fx.Shatter(Sprites.Ufo, x, y, Effects.Rainbow, 40f);
		Fx.Explode(x + 5, y + 2, 40, Effects.Fire, 55f, 0.9f);
		Fx.AddShake(2.5f);
		Fx.Flash(new Color(255, 80, 200), 0.25f);
	}

	private void KillPlayer()
	{
		Lives--;
		Streak = 0;
		int px = (int)PlayerX, py = PlayerY;
		Fx.Shatter(Sprites.Player, px, py, Effects.Plasma, 45f);
		Fx.Explode(px + 4, py + 2, 70, Effects.Fire, 65f, 1.2f);
		Fx.AddShake(5f);
		Fx.Flash(new Color(255, 30, 30), 0.55f);
		Fx.SlowMotion(0.6f, 0.3f);
		PlayerShots.Clear();
		SetPhase(Phase.PlayerDown);
	}

	private void Invasion()
	{
		_invaded = true;
		Lives = 0;
		int px = (int)PlayerX, py = PlayerY;
		Fx.Shatter(Sprites.Player, px, py, Effects.Plasma, 45f);
		Fx.Explode(px + 4, py + 2, 90, Effects.Fire, 70f, 1.4f);
		Fx.AddShake(6f);
		Fx.Flash(new Color(255, 30, 30), 0.7f);
		EndGame();
	}

	private void Respawn()
	{
		Bombs.Clear();
		if (Lives <= 0)
		{
			EndGame();
			return;
		}
		PlayerX = (Width - Sprites.Player.Width) / 2f;
		Invulnerable = 2.2f;
		SetPhase(Phase.Playing);
	}

	private void EndGame()
	{
		SetPhase(Phase.GameOver);
		Notify?.Invoke(GameEvent.GameOver, BeatHighScore
			? $"New high score: {Score:N0}!"
			: $"Game over — {Score:N0} points");
	}

	private void ClearWave()
	{
		double waveAccuracy = _waveShots == 0 ? 0 : (double)_waveHits / _waveShots;
		LastWaveBonus = (int)Math.Round((100 * Wave + waveAccuracy * 500) / 10) * 10;
		AddScore(LastWaveBonus, Width / 2, Height / 2, quiet: true);
		Bombs.Clear();
		Ufo.Active = false;
		SetPhase(Phase.WaveCleared);
		Notify?.Invoke(GameEvent.WaveCleared, $"Wave {Wave} cleared · +{LastWaveBonus:N0} bonus · {waveAccuracy:P0} accuracy");
	}

	private void AddScore(int points, float x, float y, bool quiet, int mult = 1)
	{
		Score += points;

		if (!quiet)
		{
			var color = mult > 1 ? new Color(255, 220, 90) : new Color(220, 230, 255);
			Fx.AddPopup(mult > 1 ? $"{points} ×{mult}" : points.ToString(), x, y, color);
		}

		while (Score >= _nextExtraLife)
		{
			_nextExtraLife += ExtraLifeEvery;
			if (Lives < MaxLives)
			{
				Lives++;
				Notify?.Invoke(GameEvent.ExtraLife, "Extra life!");
			}
		}

		if (Score > HiScore)
		{
			bool hadRecord = HiScore > 0;
			HiScore = Score;
			if (!BeatHighScore)
			{
				BeatHighScore = true;
				if (hadRecord) Notify?.Invoke(GameEvent.NewHighScore, "New high score!");
			}
		}
	}

	private void DamageShield(Shield shield, int px, int py, bool downward)
	{
		// Carve a ragged crater; it bites deeper in the direction of travel.
		double radius = 1.6 + _rng.NextDouble() * 0.9;
		int depth = downward ? 1 : -1;
		for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
			{
				double dist = Math.Sqrt(dx * dx + (dy - depth) * (dy - depth) * 0.8);
				if (dist > radius + _rng.NextDouble() * 0.8) continue;
				int lx = px + dx - shield.X, ly = py + dy - shield.Y;
				if ((uint)lx < (uint)shield.Width && (uint)ly < (uint)shield.Height)
					shield.Mask[lx, ly] = false;
			}

		Fx.Explode(px, py, 7, Effects.Toxic, 22f, 0.45f, downward ? 30f : -10f, glow: false);
	}

	private void UpdateThreat()
	{
		int lowest = 0;
		foreach (var inv in Invaders)
			if (inv.Alive) lowest = Math.Max(lowest, InvaderPosition(inv).Y + Sprites.InvaderHeight);

		float start = _formTargetY + (FormationRows - 1) * RowSpacing + Sprites.InvaderHeight;
		Threat = InvadersAlive == 0 ? 0 : Math.Clamp((lowest - start) / Math.Max(1f, PlayerY - start), 0, 1);
	}

	private static readonly ColorGradient SquidPalette = ColorGradient.FromColors(new Color(255, 230, 255), new Color(255, 90, 220), new Color(90, 20, 90));
	private static readonly ColorGradient CrabPalette = ColorGradient.FromColors(new Color(230, 255, 255), new Color(80, 220, 255), new Color(20, 50, 110));
	private static readonly ColorGradient OctopusPalette = ColorGradient.FromColors(new Color(240, 255, 220), new Color(140, 255, 90), new Color(30, 90, 30));

	/// <summary>Debris palette per invader type (bright core → its hue → dark).</summary>
	public static ColorGradient InvaderPalette(InvaderKind kind) => kind switch
	{
		InvaderKind.Squid => SquidPalette,
		InvaderKind.Crab => CrabPalette,
		_ => OctopusPalette,
	};

	#endregion
}
