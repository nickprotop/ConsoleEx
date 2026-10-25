// -----------------------------------------------------------------------
// SnakeGame — the renderer.
//
// Reads the Game and paints it into a PixelSurface, which is then presented
// into the CanvasControl as half-block cells. Nothing here mutates game
// state, so the renderer can run at a different rate from the simulation.
//
// The snake is drawn from INTERPOLATED positions: each segment slides from
// where it was on the previous grid tick towards where it is now, using the
// game's StepProgress. At eight ticks a second the difference between this
// and cell-by-cell teleporting is the difference between a modern game and
// a 1970s one.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Helpers;

namespace SnakeGame;

internal sealed class Renderer
{
	#region Palette

	private static readonly Color Void = new(6, 10, 16);
	private static readonly Color ArenaDark = new(12, 18, 28);
	private static readonly Color ArenaLight = new(16, 24, 36);
	private static readonly Color GridLine = new(22, 32, 48);
	private static readonly Color WallSolid = new(70, 90, 130);
	private static readonly Color WallPortal = new(90, 70, 150);

	private static readonly Color HeadCore = new(190, 255, 200);
	private static readonly Color BodyNear = new(70, 220, 130);
	private static readonly Color BodyFar = new(26, 120, 80);
	private static readonly Color ShieldRing = new(120, 210, 255);

	private static readonly Color FruitFlesh = new(240, 80, 70);
	private static readonly Color FruitShine = new(255, 200, 190);
	private static readonly Color FruitLeaf = new(90, 200, 90);
	private static readonly Color BonusFlesh = new(110, 200, 255);

	#endregion

	private readonly PixelSurface _surface = new();

	/// <summary>Paints one frame.</summary>
	public void Render(CanvasGraphics g, Game game, double now)
	{
		_surface.EnsureSize(g.Width, g.Height);
		_surface.ClearText();

		DrawArena(game, now);
		DrawFruit(game, game.Food, now, bonus: false);
		DrawFruit(game, game.Bonus, now, bonus: true);

		// While dying the body dissolves into the particles it shed, so the
		// snake is not left sitting intact next to its own death marker.
		double dissolve = game.Phase switch
		{
			Phase.Dying => Math.Clamp(game.PhaseProgress * 1.6, 0, 1),
			Phase.GameOver => 1,
			_ => 0,
		};
		if (dissolve < 1) DrawSnake(game, now, 1 - dissolve);

		DrawParticles(game);

		// After the snake, so the marker is not painted over by the head.
		if (game.Phase is Phase.Dying or Phase.GameOver) DrawDeath(game, dissolve);

		if (game.Phase == Phase.Paused) _surface.Dim(0.55);
		if (game.Phase == Phase.GameOver)
		{
			_surface.Desaturate(0.8);
			_surface.Dim(0.35);
		}

		DrawPopups(game);
		DrawOverlay(game, now);

		int shakeX = 0, shakeY = 0;
		if (game.Fx.Shake > 0.05f)
		{
			// Integer offsets only: a fractional shake would resample the whole
			// pixel grid every frame for a wobble nobody can see at this scale.
			double angle = now * 47;
			shakeX = (int)Math.Round(Math.Sin(angle) * game.Fx.Shake);
			shakeY = (int)Math.Round(Math.Cos(angle * 1.37) * game.Fx.Shake);
		}

		_surface.Present(g, shakeX, shakeY, Void);
	}

	#region Arena

	private void DrawArena(Game game, double now)
	{
		_surface.Fill(Void);

		int w = game.Columns * Game.CellSize;
		int h = game.Rows * Game.CellSize;

		// A subtle checkerboard, so the grid reads as a playfield rather than a
		// void, without competing with the snake for attention.
		for (int cy = 0; cy < game.Rows; cy++)
		{
			for (int cx = 0; cx < game.Columns; cx++)
			{
				var tint = (cx + cy) % 2 == 0 ? ArenaDark : ArenaLight;
				_surface.FillRect(
					game.OriginX + cx * Game.CellSize,
					game.OriginY + cy * Game.CellSize,
					Game.CellSize, Game.CellSize, tint);
			}
		}

		DrawWalls(game, w, h, now);
	}

	private void DrawWalls(Game game, int w, int h, double now)
	{
		bool portal = game.Walls == WallMode.Portal;
		var baseColor = portal ? WallPortal : WallSolid;

		// The wall sits in the arena margin, one pixel outside the playable grid.
		int left = game.OriginX - 1, right = game.OriginX + w;
		int top = game.OriginY - 1, bottom = game.OriginY + h;

		for (int x = left; x <= right; x++)
		{
			var c = portal ? Shimmer(baseColor, now, x) : baseColor;
			_surface.Set(x, top, c);
			_surface.Set(x, bottom, c);
		}
		for (int y = top; y <= bottom; y++)
		{
			var c = portal ? Shimmer(baseColor, now, y) : baseColor;
			_surface.Set(left, y, c);
			_surface.Set(right, y, c);
		}

		// A faint glow off a solid wall sells it as a physical edge; a portal
		// wall gets a stronger, cooler one so the two are unmistakable.
		var halo = portal ? new Color(120, 80, 200) : new Color(30, 45, 70);
		double strength = portal ? 0.35 : 0.18;
		for (int x = left; x <= right; x++)
		{
			_surface.Add(x, top - 1, halo, strength);
			_surface.Add(x, bottom + 1, halo, strength);
		}
		for (int y = top; y <= bottom; y++)
		{
			_surface.Add(left - 1, y, halo, strength);
			_surface.Add(right + 1, y, halo, strength);
		}
	}

	/// <summary>A travelling wave along a portal wall, so the rule reads at a glance.</summary>
	private static Color Shimmer(Color baseColor, double now, int along)
	{
		double t = (Math.Sin(now * 3 + along * 0.25) + 1) * 0.5;
		return PixelSurface.Lerp(baseColor, new Color(190, 140, 255), t * 0.8);
	}

	#endregion

	#region Snake

	private void DrawSnake(Game game, double now, double opacity = 1)
	{
		var body = game.Body;
		if (body.Count == 0) return;

		var previous = game.PreviousBody;
		float t = game.Phase == Phase.Playing ? game.StepProgress : 1f;

		// Back to front, so the head draws over its neighbour.
		for (int i = body.Count - 1; i >= 0; i--)
		{
			var (px, py) = Interpolate(game, body, previous, i, t);

			// Colour runs from bright at the head to dark at the tail, so the
			// snake reads as a direction even when coiled.
			double depth = body.Count == 1 ? 0 : (double)i / (body.Count - 1);
			var color = PixelSurface.Lerp(BodyNear, BodyFar, depth);

			// A gentle wave along the body, strongest at the tail — it makes a
			// straight-line snake look alive rather than extruded.
			double wave = Math.Sin(now * 7 - i * 0.45) * 0.35 * depth;

			if (opacity < 1) color = PixelSurface.Lerp(ArenaDark, color, opacity);
			DrawSegment(px, py + (float)wave, color, i == 0);
		}

		if (opacity > 0.35) DrawHead(game, body, previous, t, now);
	}

	/// <summary>Position in pixels, slid from the previous cell towards the current one.</summary>
	private static (float X, float Y) Interpolate(
		Game game, IReadOnlyList<Cell> body, IReadOnlyList<Cell> previous, int index, float t)
	{
		var current = body[index];

		// A segment with no history (the one just grown) simply appears.
		if (index >= previous.Count) return game.CellCenter(current);

		var was = previous[index];

		// A wrap teleports; previousBody[0] was rewritten to match so this stays
		// a zero-length slide rather than a sweep across the whole board.
		float x = was.X + (current.X - was.X) * t;
		float y = was.Y + (current.Y - was.Y) * t;
		return (game.OriginX + (x + 0.5f) * Game.CellSize,
			game.OriginY + (y + 0.5f) * Game.CellSize);
	}

	private void DrawSegment(float cx, float cy, Color color, bool head)
	{
		// Radius just under half a cell leaves a hairline between segments, which
		// is what makes a coiled snake legible instead of a solid blob.
		float radius = head ? Game.CellSize * 0.52f : Game.CellSize * 0.46f;
		_surface.FillCircle(cx, cy, radius, color);
	}

	private void DrawHead(Game game, IReadOnlyList<Cell> body, IReadOnlyList<Cell> previous,
		float t, double now)
	{
		var (hx, hy) = Interpolate(game, body, previous, 0, t);

		_surface.FillCircle(hx, hy, Game.CellSize * 0.52f, HeadCore);
		_surface.Glow(hx, hy, Game.CellSize * 1.6, new Color(40, 180, 110), 0.5);

		if (game.ShieldAvailable)
		{
			// A ring that breathes, so the one free life is visible on the board
			// and not only in the sidebar.
			double pulse = 0.55 + 0.45 * Math.Sin(now * 4);
			_surface.Glow(hx, hy, Game.CellSize * 2.1, ShieldRing, 0.28 * pulse);
		}

		DrawEyes(game.Facing, hx, hy);
	}

	private void DrawEyes(Direction facing, float hx, float hy)
	{
		// Eyes sit forward of centre and to either side of the direction of travel.
		var (dx, dy) = facing.Delta();
		float forward = Game.CellSize * 0.22f;
		float apart = Game.CellSize * 0.26f;

		// Perpendicular to the heading.
		float px = -dy, py = dx;

		for (int side = -1; side <= 1; side += 2)
		{
			float ex = hx + dx * forward + px * apart * side;
			float ey = hy + dy * forward + py * apart * side;
			_surface.Set((int)Math.Round(ex), (int)Math.Round(ey), new Color(20, 30, 25));
		}
	}

	#endregion

	#region Fruit

	private void DrawFruit(Game game, Fruit? fruit, double now, bool bonus)
	{
		if (fruit == null) return;

		var (cx, cy) = game.CellCenter(fruit.At);

		// Pop in rather than appear, using the library's easing. Elastic
		// overshoots slightly, so the fruit lands with a bounce.
		double grow = Math.Clamp(fruit.Age / 0.3, 0, 1);
		double scale = grow >= 1 ? 1 : EasingFunctions.Elastic(grow);

		double pulse = 1 + Math.Sin(now * 5) * 0.08;
		double radius = Game.CellSize * 0.42 * scale * pulse;
		if (radius <= 0.2) return;

		var flesh = bonus ? BonusFlesh : FruitFlesh;

		if (bonus)
		{
			// A bonus about to expire flashes faster and faster, which is the
			// only honest way to show a timer on the board itself.
			double urgency = 1 - Math.Clamp(fruit.Life / fruit.MaxLife, 0, 1);
			double blink = Math.Sin(now * (4 + urgency * 22));
			if (urgency > 0.55 && blink < -0.2) return;
			flesh = PixelSurface.Lerp(flesh, new Color(255, 255, 255), urgency * 0.4);
		}

		_surface.Glow(cx, cy, Game.CellSize * (bonus ? 2.4 : 1.7),
			flesh, bonus ? 0.55 : 0.32);
		_surface.FillCircle(cx, cy, radius, flesh);

		// A single bright pixel reads as a specular highlight at this scale.
		_surface.Set((int)(cx - radius * 0.35), (int)(cy - radius * 0.35),
			bonus ? Color.White : FruitShine);

		if (!bonus)
			_surface.Draw(Sprites.FruitStalk,
				(int)(cx - 1), (int)(cy - radius - 1.5f), FruitLeaf);
	}

	#endregion

	#region Effects

	private void DrawParticles(Game game)
	{
		foreach (ref readonly var p in game.Fx.Particles)
		{
			float life = p.MaxLife <= 0 ? 1 : p.Life / p.MaxLife;
			var color = p.Palette.Interpolate(Math.Clamp(life, 0, 1));

			int x = (int)p.X, y = (int)p.Y;
			if (p.Glow)
				_surface.Add(x, y, color, 0.9 * (1 - life));
			else
				_surface.Blend(x, y, color, 1 - life * 0.6);
		}
	}

	private void DrawPopups(Game game)
	{
		foreach (var pop in game.Fx.Popups)
		{
			float fade = 1 - pop.Life / pop.MaxLife;
			if (fade <= 0.05f) continue;

			int row = (int)(pop.Y / 2);
			int col = (int)pop.X - pop.Text.Length / 2;
			var color = PixelSurface.Lerp(Void, pop.Color, fade);
			_surface.Text(col, row, pop.Text, color);
		}
	}

	private void DrawDeath(Game game, double fadeIn)
	{
		if (fadeIn <= 0.05) return;

		var (cx, cy) = game.CellCenter(game.DeathCell);
		var color = PixelSurface.Lerp(ArenaDark, new Color(245, 225, 225), fadeIn);

		_surface.Glow(cx, cy, Game.CellSize * 2.2, new Color(120, 40, 40), 0.5 * fadeIn);
		_surface.Draw(Sprites.Skull,
			(int)cx - Sprites.Skull.Width / 2,
			(int)cy - Sprites.Skull.Height / 2,
			color);
	}

	#endregion

	#region Overlay

	private void DrawOverlay(Game game, double now)
	{
		switch (game.Phase)
		{
			case Phase.Title:
				Banner(game, "S N A K E", "Arrows or WASD to start", now);
				break;

			case Phase.Paused:
				Banner(game, "P A U S E D", "P or Space to resume", now);
				break;

			case Phase.GameOver:
				Banner(game, "G A M E   O V E R",
					$"{game.Score} points  ·  length {game.Length}  ·  R to retry", now);
				break;
		}
	}

	private void Banner(Game game, string title, string subtitle, double now)
	{
		int midRow = _surface.Rows / 2;

		// Cycle the title through the rainbow, a letter at a time.
		int start = (_surface.Width - title.Length) / 2;
		for (int i = 0; i < title.Length; i++)
		{
			double phase = (now * 0.35 + i * 0.045) % 1.0;
			var color = Effects.Rainbow.Interpolate(phase);
			_surface.Text(start + i, midRow - 1, title[i].ToString(), color);
		}

		_surface.TextCentered(midRow + 1, subtitle, new Color(170, 190, 210));
	}

	#endregion
}
