// -----------------------------------------------------------------------
// Tetris - the rules.
//
// Guideline behaviour: SRS rotation with kicks, a 7-bag randomiser, hold,
// ghost piece, lock delay, and the standard scoring. Simulation only: the
// board is a grid of colour indices and nothing here knows what a terminal
// is.
// -----------------------------------------------------------------------

using Games.Common;
using SharpConsoleUI;

namespace Tetris;

internal enum Phase { Title, Playing, Clearing, GameOver }

/// <summary>A piece in play: which kind, where, and how it is turned.</summary>
internal sealed class ActivePiece
{
	public PieceKind Kind;
	public int X, Y;
	public int Rotation;
}

internal sealed class Game
{
	public const int Columns = 10;
	public const int Rows = 20;

	/// <summary>Rows above the visible board, where a piece spawns before it enters view.</summary>
	public const int HiddenRows = 2;

	/// <summary>A cell's width in surface pixels, as the renderer last drew it.</summary>
	public int CellWidth { get; internal set; } = 4;

	/// <summary>
	/// A cell's height in surface pixels, as the renderer last drew it. Debris is spawned in
	/// well-pixel space, so it has to agree with what is on screen: a short terminal squashes
	/// the cells, and particles that kept assuming four pixels would erupt below the row that
	/// actually cleared.
	/// </summary>
	public int CellHeight { get; internal set; } = 4;

	private const float ClearFlashSeconds = 0.35f;
	private const float LockDelaySeconds = 0.5f;
	private const int LockResetCap = 15;
	private const int LinesPerLevel = 10;
	private const float SoftDropMultiplier = 20f;

	// Guideline scoring, before the level multiplier.
	private const int SingleScore = 100;
	private const int DoubleScore = 300;
	private const int TripleScore = 500;
	private const int TetrisScore = 800;

	private readonly Random _rng = new();
	private readonly int[,] _board = new int[Rows + HiddenRows, Columns];
	private readonly List<PieceKind> _bag = new();
	private readonly Queue<PieceKind> _next = new();
	private readonly Effects _fx;

	private float _fallTimer;
	private float _lockTimer;
	private int _lockResets;
	private bool _onFloor;
	private bool _holdUsed;
	private float _phaseTime;
	private int _softDrop;

	public Game(int highScore)
	{
		HighScore = highScore;
		_fx = new Effects(_rng, maxParticles: 1200);
	}

	public event EventHandler<string>? Notify;

	public Phase Phase { get; private set; } = Phase.Title;
	public int Score { get; private set; }
	public int HighScore { get; private set; }
	public int Lines { get; private set; }
	public int Level { get; private set; } = 1;
	public int Combo { get; private set; } = -1;
	public bool Paused { get; private set; }
	public bool BackToBack { get; private set; }
	public string LastClear { get; private set; } = string.Empty;

	public ActivePiece? Piece { get; private set; }
	public PieceKind? Held { get; private set; }
	public IReadOnlyCollection<PieceKind> Next => _next;
	public Effects Fx => _fx;

	/// <summary>Rows cleared and still flashing, for the renderer to light up.</summary>
	public IReadOnlyList<int> ClearingRows { get; private set; } = Array.Empty<int>();

	/// <summary>The colour index at a board cell, 0 for empty.</summary>
	public int CellAt(int row, int column) => _board[row + HiddenRows, column];

	/// <summary>Where the active piece would land if dropped now.</summary>
	public int GhostY
	{
		get
		{
			if (Piece is null) return 0;
			int y = Piece.Y;
			while (Fits(Piece.Kind, Piece.X, y + 1, Piece.Rotation)) y++;
			return y;
		}
	}

	public void StartGame()
	{
		Array.Clear(_board);
		_bag.Clear();
		_next.Clear();
		_fx.Clear();
		Score = 0;
		Lines = 0;
		Level = 1;
		Combo = -1;
		Held = null;
		BackToBack = false;
		LastClear = string.Empty;
		ClearingRows = Array.Empty<int>();
		Phase = Phase.Playing;
		_phaseTime = 0;

		for (int i = 0; i < 5; i++) _next.Enqueue(TakeFromBag());
		Spawn();
	}

	public void TogglePause()
	{
		if (Phase != Phase.Playing) return;
		Paused = !Paused;
	}

	/// <summary>Moves the piece sideways; -1 left, +1 right.</summary>
	public void Move(int direction)
	{
		if (Phase != Phase.Playing || Paused || Piece is null) return;
		if (!Fits(Piece.Kind, Piece.X + direction, Piece.Y, Piece.Rotation)) return;

		Piece.X += direction;
		OnPieceMoved();
	}

	/// <summary>Turns the piece; +1 clockwise, -1 anticlockwise.</summary>
	public void Rotate(int direction)
	{
		if (Phase != Phase.Playing || Paused || Piece is null) return;

		int from = Piece.Rotation;
		int to = ((from + direction) % 4 + 4) % 4;

		// The first kick that fits wins; none fitting means the rotation is simply refused,
		// which is what lets a player wedge a piece against a wall without it jumping.
		foreach (var (dx, dy) in Pieces.Kicks(Piece.Kind, from, to))
		{
			if (!Fits(Piece.Kind, Piece.X + dx, Piece.Y + dy, to)) continue;

			Piece.X += dx;
			Piece.Y += dy;
			Piece.Rotation = to;
			OnPieceMoved();
			return;
		}
	}

	/// <summary>Holds the current piece, swapping it for whatever was held.</summary>
	public void Hold()
	{
		if (Phase != Phase.Playing || Paused || Piece is null || _holdUsed) return;

		var swap = Held;
		Held = Piece.Kind;
		_holdUsed = true;

		if (swap is { } kind) StartPiece(kind);
		else Spawn();
	}

	/// <summary>Drops the piece all the way and locks it.</summary>
	public void HardDrop()
	{
		if (Phase != Phase.Playing || Paused || Piece is null) return;

		int from = Piece.Y;
		Piece.Y = GhostY;
		Score += (Piece.Y - from) * 2;   // guideline: two points a cell
		_fx.AddShake(Math.Min(3f, 0.6f + (Piece.Y - from) * 0.12f));
		Lock();
	}

	/// <summary>Holds the piece falling fast while the key is down.</summary>
	public void SoftDrop(bool active) => _softDrop = active ? 1 : 0;

	public void Update(float dt, double now)
	{
		_phaseTime += dt;
		_fx.Update(dt * _fx.TimeScale, dt);

		if (Paused || Phase is Phase.Title or Phase.GameOver) return;

		if (Phase == Phase.Clearing)
		{
			if (_phaseTime >= ClearFlashSeconds) FinishClear();
			return;
		}

		if (Piece is null) return;

		// Gravity. Soft drop multiplies it rather than replacing it, so a held key speeds the
		// piece up at every level instead of flattening the curve.
		float speed = 1f / FallSeconds() * (_softDrop > 0 ? SoftDropMultiplier : 1f);
		_fallTimer += dt * speed;

		while (_fallTimer >= 1f)
		{
			_fallTimer -= 1f;
			if (Fits(Piece.Kind, Piece.X, Piece.Y + 1, Piece.Rotation))
			{
				Piece.Y++;
				if (_softDrop > 0) Score++;
				_onFloor = false;
				_lockResets = 0;
			}
			else
			{
				_onFloor = true;
			}
		}

		// Lock delay: a piece resting on the floor gets half a second to be moved, and each
		// move buys more, up to a cap. Without the cap a player could stall forever.
		if (_onFloor)
		{
			_lockTimer += dt;
			if (_lockTimer >= LockDelaySeconds) Lock();
		}
		else
		{
			_lockTimer = 0;
		}
	}

	/// <summary>Seconds a piece takes to fall one row at the current level.</summary>
	private float FallSeconds() => MathF.Max(0.05f, 0.8f - (Level - 1) * 0.07f);

	private void OnPieceMoved()
	{
		if (!_onFloor) return;

		// Still resting, but the player did something: give the lock timer back, up to the cap.
		if (_lockResets++ < LockResetCap) _lockTimer = 0;
		_onFloor = !Fits(Piece!.Kind, Piece.X, Piece.Y + 1, Piece.Rotation);
	}

	private void Spawn() => StartPiece(_next.Dequeue());

	private void StartPiece(PieceKind kind)
	{
		if (_next.Count < 5) _next.Enqueue(TakeFromBag());

		var piece = new ActivePiece
		{
			Kind = kind,
			X = kind == PieceKind.O ? 4 : 3,
			Y = 0,
			Rotation = 0,
		};

		_fallTimer = 0;
		_lockTimer = 0;
		_lockResets = 0;
		_onFloor = false;

		// Blocked at spawn is the end: the stack reached the ceiling.
		if (!Fits(piece.Kind, piece.X, piece.Y, piece.Rotation))
		{
			Piece = piece;
			Phase = Phase.GameOver;
			_phaseTime = 0;
			_fx.AddShake(4f);
			_fx.Flash(new Color(255, 80, 80), 0.5f);
			if (Score > HighScore) HighScore = Score;
			Notify?.Invoke(this, $"Game over - {Score:N0}");
			return;
		}

		Piece = piece;
	}

	/// <summary>
	/// Seven-bag: every piece appears once before any repeats. Pure random gives droughts
	/// long enough to feel broken.
	/// </summary>
	private PieceKind TakeFromBag()
	{
		if (_bag.Count == 0)
		{
			_bag.AddRange(Enum.GetValues<PieceKind>());
			for (int i = _bag.Count - 1; i > 0; i--)
			{
				int j = _rng.Next(i + 1);
				(_bag[i], _bag[j]) = (_bag[j], _bag[i]);
			}
		}

		var kind = _bag[^1];
		_bag.RemoveAt(_bag.Count - 1);
		return kind;
	}

	/// <summary>Whether a piece fits at a position: inside the walls and clear of the stack.</summary>
	private bool Fits(PieceKind kind, int x, int y, int rotation)
	{
		foreach (var (cx, cy) in Pieces.Cells(kind, rotation))
		{
			int bx = x + cx;
			int by = y + cy + HiddenRows;

			if (bx < 0 || bx >= Columns) return false;
			if (by >= Rows + HiddenRows) return false;
			if (by >= 0 && _board[by, bx] != 0) return false;
		}

		return true;
	}

	private void Lock()
	{
		if (Piece is null) return;

		foreach (var (cx, cy) in Pieces.Cells(Piece.Kind, Piece.Rotation))
		{
			int by = Piece.Y + cy + HiddenRows;
			int bx = Piece.X + cx;
			if (by >= 0 && by < Rows + HiddenRows && bx >= 0 && bx < Columns)
				_board[by, bx] = (int)Piece.Kind + 1;
		}

		var full = new List<int>();
		for (int row = 0; row < Rows + HiddenRows; row++)
		{
			bool complete = true;
			for (int column = 0; column < Columns; column++)
			{
				if (_board[row, column] == 0) { complete = false; break; }
			}
			if (complete) full.Add(row);
		}

		Piece = null;
		_holdUsed = false;

		if (full.Count == 0)
		{
			Combo = -1;
			Spawn();
			return;
		}

		ClearingRows = full;
		Phase = Phase.Clearing;
		_phaseTime = 0;
		ScoreClear(full.Count);
	}

	private void ScoreClear(int count)
	{
		int baseScore = count switch
		{
			1 => SingleScore,
			2 => DoubleScore,
			3 => TripleScore,
			_ => TetrisScore,
		};

		// Back-to-back: consecutive tetrises are worth half again, which is what makes
		// stacking for them a real decision rather than a flourish.
		bool difficult = count == 4;
		if (difficult && BackToBack) baseScore = baseScore * 3 / 2;
		BackToBack = difficult;

		Combo++;
		int comboBonus = Combo > 0 ? 50 * Combo * Level : 0;

		Score += baseScore * Level + comboBonus;
		if (Score > HighScore) HighScore = Score;

		Lines += count;
		Level = Lines / LinesPerLevel + 1;

		LastClear = count switch { 1 => "SINGLE", 2 => "DOUBLE", 3 => "TRIPLE", _ => "TETRIS" };

		_fx.AddShake(count >= 4 ? 3.2f : 1.2f + count * 0.4f);
		if (count >= 4) _fx.Flash(new Color(120, 220, 255), 0.4f);

		foreach (int row in ClearingRows)
		{
			for (int column = 0; column < Columns; column++)
			{
				_fx.Explode(
					column * CellWidth + CellWidth / 2,
					(row - HiddenRows) * CellHeight + CellHeight / 2,
					count >= 4 ? 5 : 3,
					Palettes.DebrisFor(_board[row, column]),
					count >= 4 ? 26f : 16f, 0.55f, 22f);
			}
		}

		if (Combo > 0) Notify?.Invoke(this, $"{LastClear} - combo {Combo}");
		else Notify?.Invoke(this, LastClear);
	}

	/// <summary>Drops everything above each cleared row down into it.</summary>
	private void FinishClear()
	{
		foreach (int row in ClearingRows.OrderBy(r => r))
		{
			for (int y = row; y > 0; y--)
			{
				for (int column = 0; column < Columns; column++)
					_board[y, column] = _board[y - 1, column];
			}
			for (int column = 0; column < Columns; column++) _board[0, column] = 0;
		}

		ClearingRows = Array.Empty<int>();
		Phase = Phase.Playing;
		_phaseTime = 0;
		Spawn();
	}
}
