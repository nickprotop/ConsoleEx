// -----------------------------------------------------------------------
// SpaceInvaders — renderer.
//
// Draws one frame of the Game into the PixelSurface, back to front:
//   deep-space gradient → parallax starfield (warps between waves) →
//   threat glow → ground → shields → invaders → UFO → ship → shots →
//   particles → vignette → phase overlays (title, banners, pause, game
//   over) → text pop-ups, then presents it into the CanvasControl with
//   the current screen-shake offset.
// Purely visual state (stars, vignette mask) lives here, not in the Game.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;

namespace SpaceInvaders;

internal sealed class Renderer
{
	private static readonly Color SpaceTop = new(4, 4, 18);
	private static readonly Color SpaceBottom = new(16, 6, 34);
	private static readonly Color GroundColor = new(60, 220, 120);
	private static readonly Color ShieldColor = new(70, 235, 110);
	private static readonly Color ShipColor = new(150, 245, 255);
	private static readonly Color ShotColor = new(190, 255, 255);
	private static readonly Color HintColor = new(150, 160, 200);
	private static readonly ColorGradient TitleGradient =
		ColorGradient.FromColors(new Color(255, 70, 200), new Color(255, 200, 60), new Color(80, 255, 160), new Color(60, 180, 255), new Color(255, 70, 200));
	private static readonly ColorGradient RowGradient =
		ColorGradient.FromColors(new Color(255, 90, 220), new Color(120, 140, 255), new Color(60, 230, 255), new Color(90, 255, 150), new Color(200, 255, 90));

	private readonly PixelSurface _px = new();
	private readonly Random _rng = new();
	private Star[] _stars = Array.Empty<Star>();
	private float[] _vignette = Array.Empty<float>();
	private double _lastTime;
	private float _warp;

	private struct Star
	{
		public float X, Y;
		public int Layer;
		public float Phase;
	}

	public void Render(CanvasGraphics g, Game game, double now)
	{
		float dt = (float)Math.Clamp(now - _lastTime, 0, 0.1);
		_lastTime = now;

		_px.EnsureSize(g.Width, g.Height);
		if (_px.Width == 0 || _px.Height == 0) return;
		EnsureStars();
		EnsureVignette();
		_px.ClearText();

		if (game.TooSmall)
		{
			DrawTooSmall(g);
			return;
		}

		bool paused = game.Paused;
		float t = game.Time;

		DrawBackground(game);
		UpdateAndDrawStars(game, paused ? 0 : dt, (float)now);
		DrawGround();

		if (game.Phase != Phase.Title)
		{
			DrawShields(game);
			DrawInvaders(game);
			DrawUfo(game, t);
			DrawShip(game, (float)now);
			DrawShots(game, t);
		}
		else
		{
			DrawTitle(game, (float)now);
			DrawUfo(game, t);
		}

		DrawParticles(game);
		ApplyVignette();
		DrawOverlays(game, (float)now);
		DrawPopups(game);

		var (sx, sy) = ShakeOffset(game.Fx.Shake);
		_px.Present(g, sx, sy, SpaceTop);
	}

	#region Background

	private void DrawBackground(Game game)
	{
		// Vertical deep-space gradient, warmed towards red as the invaders descend.
		double threat = game.Phase == Phase.Playing ? game.Threat : 0;
		double pulse = threat > 0.55 ? (Math.Sin(game.Time * 6) * 0.5 + 0.5) * (threat - 0.55) * 1.6 : 0;
		var bottom = PixelSurface.Lerp(SpaceBottom, new Color(70, 8, 18), Math.Min(1, threat * 0.8 + pulse * 0.4));
		for (int y = 0; y < _px.Height; y++)
		{
			var row = PixelSurface.Lerp(SpaceTop, bottom, Math.Pow((double)y / _px.Height, 1.6));
			_px.FillRect(0, y, _px.Width, 1, row);
		}
	}

	private void EnsureStars()
	{
		int wanted = Math.Max(30, _px.Width * _px.Height / 90);
		if (_stars.Length == wanted) return;
		_stars = new Star[wanted];
		for (int i = 0; i < wanted; i++)
		{
			_stars[i] = new Star
			{
				X = (float)_rng.NextDouble() * _px.Width,
				Y = (float)_rng.NextDouble() * _px.Height,
				Layer = i % 7 == 0 ? 2 : i % 3 == 0 ? 1 : 0,
				Phase = (float)(_rng.NextDouble() * Math.PI * 2),
			};
		}
	}

	private void UpdateAndDrawStars(Game game, float dt, float now)
	{
		// Warp between waves: stars accelerate into streaks, then settle.
		float targetWarp = game.Phase == Phase.WaveCleared && game.PhaseTime > 0.6f ? 1f : 0f;
		_warp += (targetWarp - _warp) * Math.Min(1f, dt * (targetWarp > _warp ? 2.5f : 4f));

		for (int i = 0; i < _stars.Length; i++)
		{
			ref var s = ref _stars[i];
			float speed = (s.Layer switch { 0 => 1.2f, 1 => 3.5f, _ => 8f }) * (1 + _warp * 22f);
			s.Y += speed * dt;
			if (s.Y >= _px.Height)
			{
				s.Y -= _px.Height;
				s.X = (float)_rng.NextDouble() * _px.Width;
			}

			double twinkle = 0.65 + 0.35 * Math.Sin(now * (1.5 + s.Layer) + s.Phase);
			double brightness = (s.Layer switch { 0 => 0.22, 1 => 0.45, _ => 0.9 }) * twinkle;
			var color = PixelSurface.Scale(s.Layer == 2 ? new Color(220, 230, 255) : new Color(170, 180, 230), brightness);

			int x = (int)s.X, y = (int)s.Y;
			int streak = (int)(_warp * (s.Layer + 1) * 3);
			for (int k = 0; k <= streak; k++)
				_px.Add(x, y - k, color, 1.0 - (double)k / (streak + 1));
		}
	}

	private void DrawGround()
	{
		int y = _px.Height - 2;
		for (int x = 0; x < _px.Width; x++)
		{
			double edge = Math.Abs(x - _px.Width / 2.0) / (_px.Width / 2.0);
			_px.Set(x, y, PixelSurface.Lerp(GroundColor, new Color(10, 60, 40), edge * 0.8));
		}
	}

	private void EnsureVignette()
	{
		if (_vignette.Length == _px.Width * _px.Height) return;
		_vignette = new float[_px.Width * _px.Height];
		double cx = _px.Width / 2.0, cy = _px.Height / 2.0;
		for (int y = 0; y < _px.Height; y++)
			for (int x = 0; x < _px.Width; x++)
			{
				double dx = (x - cx) / cx, dy = (y - cy) / cy;
				double d = Math.Sqrt(dx * dx * 0.7 + dy * dy * 0.6);
				_vignette[y * _px.Width + x] = (float)Math.Clamp((d - 0.55) * 0.75, 0, 0.45);
			}
	}

	private void ApplyVignette()
	{
		for (int y = 0; y < _px.Height; y++)
			for (int x = 0; x < _px.Width; x++)
			{
				float v = _vignette[y * _px.Width + x];
				if (v > 0) _px.Set(x, y, PixelSurface.Lerp(_px.Get(x, y), Color.Black, v));
			}
	}

	#endregion

	#region Actors

	private void DrawShields(Game game)
	{
		foreach (var s in game.Shields)
			for (int y = 0; y < s.Height; y++)
				for (int x = 0; x < s.Width; x++)
				{
					if (!s.Mask[x, y]) continue;
					// Lit top edge, darker body, so erosion reads as depth.
					bool exposed = y == 0 || !s.Mask[x, y - 1];
					var c = exposed ? PixelSurface.Lerp(ShieldColor, Color.White, 0.35) : PixelSurface.Scale(ShieldColor, 0.85 - y * 0.07);
					_px.Set(s.X + x, s.Y + y, c);
				}
	}

	private void DrawInvaders(Game game)
	{
		// Flash brighter on every march step: the formation visibly "beats".
		float sinceStep = game.Time - game.LastStepTime;
		double beat = game.Phase == Phase.Playing ? Math.Max(0, 1 - sinceStep / 0.12) * 0.35 : 0;

		foreach (var inv in game.Invaders)
		{
			var (x, y, arrival) = game.InvaderDrawPosition(inv);

			if (!inv.Alive)
			{
				if (inv.BurstTimer > 0)
					_px.Draw(Sprites.Burst, x, y, PixelSurface.Lerp(new Color(255, 160, 60), Color.White, inv.BurstTimer / 0.16));
				continue;
			}

			if (arrival <= 0) continue;

			var baseColor = RowGradient.Interpolate(inv.Row / 4.0);
			var color = PixelSurface.Scale(baseColor, 1 + beat);
			var sprite = inv.Frames[game.AnimFrame];
			_px.GlowMask(sprite, x, y, baseColor, 0.12 + beat * 0.3);
			_px.Draw(sprite, x, y, color, rowShade: 0.25);

			// A faint vapour trail while flying in.
			if (arrival < 1)
				for (int k = 1; k < 5; k++)
					_px.Add(x + 3, y - k * 2, baseColor, 0.35 * (1 - arrival) * (1 - k / 5.0));
		}
	}

	private void DrawUfo(Game game, float t)
	{
		var ufo = game.Ufo;
		if (!ufo.Active) return;
		int x = (int)ufo.X;
		var s = Sprites.Ufo;
		for (int sy = 0; sy < s.Height; sy++)
			for (int sx = 0; sx < s.Width; sx++)
			{
				if (!s[sx, sy]) continue;
				var c = Effects.Rainbow.Interpolate(((t * 0.6 + sx / (double)s.Width) % 1 + 1) % 1);
				_px.Set(x + sx, Ufo.Y + sy, c);
				_px.Add(x + sx, Ufo.Y + sy + 1, c, 0.15);
			}

		// Blinking underside lights.
		bool blink = (int)(t * 6) % 2 == 0;
		for (int sx = 1; sx < s.Width; sx += 3)
			_px.Add(x + sx, Ufo.Y + s.Height, blink ? Color.White : new Color(255, 80, 80), 0.6);
	}

	private void DrawShip(Game game, float now)
	{
		if (!game.PlayerAlive) return;
		if (game.Invulnerable > 0 && (int)(now * 10) % 2 == 0) return;

		int x = (int)game.PlayerX, y = game.PlayerY;
		_px.GlowMask(Sprites.Player, x, y, ShipColor, 0.12);
		_px.Draw(Sprites.Player, x, y, ShipColor, rowShade: -0.0);
		_px.Set(x + 4, y, Color.White);

		// Engine flicker under the hull; brighter while moving.
		double thrust = Math.Min(1, Math.Abs(game.PlayerVelocity) / 50.0);
		for (int i = 2; i <= 6; i += 2)
		{
			double flicker = 0.25 + _rng.NextDouble() * 0.35 + thrust * 0.4;
			_px.Add(x + i, y + Sprites.Player.Height, new Color(255, 140, 40), flicker);
		}
	}

	private void DrawShots(Game game, float t)
	{
		foreach (var s in game.PlayerShots)
		{
			int x = (int)s.X, y = (int)s.Y;
			for (int k = 0; k < 3; k++) _px.Set(x, y + k, ShotColor);
			for (int k = 3; k < 9; k++) _px.Add(x, y + k, new Color(60, 160, 255), 0.6 * (1 - (k - 3) / 6.0));
			_px.Add(x - 1, y + 1, ShotColor, 0.2);
			_px.Add(x + 1, y + 1, ShotColor, 0.2);
		}

		foreach (var b in game.Bombs)
		{
			int x = (int)b.X, y = (int)b.Y;
			switch (b.Kind)
			{
				case 0: // wriggler
					int phase = (int)(t * 12 + x) % 2;
					for (int k = 0; k < 3; k++)
						_px.Set(x + ((k + phase) % 2 == 0 ? 0 : 1), y + k, new Color(255, 170, 60));
					break;
				case 1: // fast lance
					for (int k = 0; k < 3; k++) _px.Set(x, y + k, new Color(255, 70, 70));
					_px.Add(x, y - 1, new Color(255, 70, 70), 0.4);
					break;
				default: // plunger
					_px.Set(x, y, new Color(255, 240, 120));
					_px.Set(x - 1, y + 1, new Color(255, 240, 120));
					_px.Set(x + 1, y + 1, new Color(255, 240, 120));
					_px.Set(x, y + 1, new Color(255, 240, 120));
					_px.Set(x, y + 2, new Color(255, 240, 120));
					break;
			}
			_px.Add(x, y + 3, new Color(255, 120, 40), 0.3);
		}
	}

	private void DrawParticles(Game game)
	{
		foreach (ref readonly var p in game.Fx.Particles)
		{
			double life = p.Life / p.MaxLife;
			var c = p.Palette.Interpolate(life);
			int x = (int)p.X, y = (int)p.Y;
			if (p.Glow)
			{
				_px.Add(x, y, c, 1.0);
				double halo = 0.25 * (1 - life);
				_px.Add(x - 1, y, c, halo);
				_px.Add(x + 1, y, c, halo);
				_px.Add(x, y - 1, c, halo);
				_px.Add(x, y + 1, c, halo);
			}
			else
			{
				_px.Blend(x, y, c, 1 - life * 0.6);
			}
		}
	}

	#endregion

	#region Overlays

	private void DrawOverlays(Game game, float now)
	{
		switch (game.Phase)
		{
			case Phase.WaveIntro:
				{
					// "WAVE n" fades in, holds, fades out while the formation lands.
					float d = game.IntroDuration;
					double a = Math.Min(1, game.PhaseTime / 0.3) * Math.Min(1, (d - game.PhaseTime) / 0.4);
					DrawBanner($"WAVE {game.Wave}", _px.Height / 2 - 4, now, a);
					break;
				}
			case Phase.WaveCleared:
				{
					double a = Math.Min(1, game.PhaseTime / 0.3) * Math.Min(1, (2.8 - game.PhaseTime) / 0.5);
					int y = _px.Height / 2 - 8;
					DrawBanner("CLEARED", y, now, a);
					if (a > 0.3)
					{
						int row = (y + BigHeight("CLEARED")) / 2 + 2;
						_px.TextCentered(row, $"WAVE BONUS  +{game.LastWaveBonus:N0}", new Color(255, 220, 120));
					}
					break;
				}
			case Phase.PlayerDown when game.PhaseTime > 0.8f && game.Lives > 0:
				_px.TextCentered(_px.Rows / 2, game.Lives == 1 ? "LAST SHIP" : $"{game.Lives} SHIPS LEFT", new Color(255, 200, 200));
				break;
			case Phase.GameOver:
				DrawGameOver(game, now);
				break;
		}

		if (game.Paused)
		{
			_px.Dim(0.6);
			DrawBanner("PAUSED", _px.Height / 2 - 6, now, 1);
			_px.TextCentered(_px.Rows / 2 + 4, "P / Esc to resume  ·  R restart  ·  Q quit", HintColor);
		}
	}

	private void DrawTitle(Game game, float now)
	{
		int y = Math.Max(2, _px.Height / 2 - 30);
		var font = PickFont("INVADERS");

		// Title rows drift in from above on start-up.
		double intro = EasingFunctions.EaseOut(Math.Min(1, now / 1.2));
		int yOffset = (int)((1 - intro) * -20);

		DrawBig("SPACE", font, y + yOffset, now, 1);
		y += BigText.Render("SPACE", font).Height + 1;
		DrawBig("INVADERS", font, y + yOffset, now + 0.6f, 1);
		y += BigText.Render("INVADERS", font).Height + 4;

		// Score table with the real sprites, revealed one line at a time.
		var entries = new (PixelMask PixelMask, Color Color, string Label)[]
		{
			(Sprites.Ufo, Effects.Rainbow.Interpolate((now * 0.3) % 1), "= ? MYSTERY"),
			(Sprites.Squid[(int)(now * 2) % 2], RowGradient.Interpolate(0), "= 30 POINTS"),
			(Sprites.Crab[(int)(now * 2) % 2], RowGradient.Interpolate(0.4), "= 20 POINTS"),
			(Sprites.Octopus[(int)(now * 2) % 2], RowGradient.Interpolate(0.9), "= 10 POINTS"),
		};
		int left = _px.Width / 2 - 10;
		for (int i = 0; i < entries.Length; i++)
		{
			double reveal = Math.Clamp((now - 1.0 - i * 0.35) / 0.3, 0, 1);
			if (reveal <= 0) break;
			var (sprite, color, label) = entries[i];
			int sx = left - sprite.Width / 2;
			_px.Draw(sprite, sx, y, PixelSurface.Scale(color, reveal));
			int chars = (int)Math.Round(label.Length * reveal);
			_px.Text(left + 7, y / 2 + 1, label[..chars], PixelSurface.Scale(new Color(220, 225, 255), reveal));
			y += 6;
		}

		int row = y / 2 + 2;
		if (now > 2.6 && (int)(now * 2.2) % 3 != 0)
			_px.TextCentered(row, "PRESS SPACE OR CLICK TO START", new Color(255, 230, 120));
		_px.TextCentered(row + 2, $"HI-SCORE  {game.HiScore:N0}", new Color(150, 220, 255));
		_px.TextCentered(_px.Rows - 2, "←/→ or mouse to steer  ·  SPACE fire  ·  K keyboard mode", HintColor);
	}

	private void DrawGameOver(Game game, float now)
	{
		// The battlefield drains of colour while the letters drop in.
		_px.Desaturate(Math.Min(0.85, game.PhaseTime / 1.5));
		_px.Dim(Math.Min(0.35, game.PhaseTime / 3));

		var font = PickFont("GAME OVER");
		const string text = "GAME OVER";
		int total = 0;
		var glyphs = new List<(PixelMask PixelMask, int Offset)>();
		foreach (char c in text)
		{
			if (c == ' ') { total += 4; continue; }
			var s = BigText.Render(c.ToString(), font);
			glyphs.Add((s, total));
			total += s.Width + 1;
		}

		int left = (_px.Width - total) / 2;
		int baseY = _px.Height / 2 - 12;
		for (int i = 0; i < glyphs.Count; i++)
		{
			double t = Math.Clamp((game.PhaseTime - 0.3 - i * 0.09) / 0.7, 0, 1);
			if (t <= 0) continue;
			double eased = EasingFunctions.Bounce(t);
			int y = (int)Math.Round(-20 + (baseY + 20) * eased);
			var (sprite, offset) = glyphs[i];
			DrawSpriteGradient(sprite, left + offset, y, (gx, gy) =>
				PixelSurface.Lerp(new Color(255, 60, 60), new Color(255, 200, 80), (double)gy / sprite.Height), 1);
		}

		if (game.PhaseTime > 1.4)
		{
			int row = (baseY + BigText.Render("G", font).Height) / 2 + 2;
			_px.TextCentered(row, $"FINAL SCORE  {game.Score:N0}", Color.White);
			_px.TextCentered(row + 1, $"WAVE {game.Wave}  ·  ACCURACY {game.Accuracy:P0}", HintColor);
			if (game.BeatHighScore)
			{
				var c = Effects.Rainbow.Interpolate((now * 0.5) % 1);
				_px.TextCentered(row + 3, "★  NEW HIGH SCORE  ★", c);
			}
			if ((int)(now * 2.2) % 3 != 0)
				_px.TextCentered(row + 5, "SPACE / CLICK to play again  ·  Q to quit", new Color(255, 230, 120));
		}
	}

	private void DrawTooSmall(CanvasGraphics g)
	{
		_px.Fill(SpaceTop);
		_px.TextCentered(_px.Rows / 2 - 1, "TERMINAL TOO SMALL", new Color(255, 120, 120));
		_px.TextCentered(_px.Rows / 2 + 1, $"Play field needs {Game.MinColumns}×{Game.MinRows} cells", HintColor);
		_px.Present(g, 0, 0, SpaceTop);
	}

	#endregion

	#region Big text helpers

	private string PickFont(string widest) =>
		BigText.Render(widest, "standard").Width <= _px.Width - 4 ? "standard" : "small";

	private int BigHeight(string text) => BigText.Render(text, PickFont(text)).Height;

	/// <summary>Big centred text with a drop shadow and a sweeping gradient.</summary>
	private void DrawBanner(string text, int y, float now, double alpha) =>
		DrawBig(text, PickFont(text), y, now, alpha);

	private void DrawBig(string text, string font, int y, float now, double alpha)
	{
		if (alpha <= 0) return;
		var sprite = BigText.Render(text, font);
		int x = (_px.Width - sprite.Width) / 2;
		DrawSpriteGradient(sprite, x, y, (gx, gy) =>
		{
			double u = ((gx / (double)Math.Max(1, sprite.Width)) * 0.8 - now * 0.25) % 1;
			var c = TitleGradient.Interpolate(u < 0 ? u + 1 : u);
			// Vertical sheen: brighter top, deeper bottom.
			return PixelSurface.Scale(c, 1.15 - 0.45 * gy / Math.Max(1, sprite.Height));
		}, alpha);
	}

	private void DrawSpriteGradient(PixelMask sprite, int x, int y, Func<int, int, Color> color, double alpha)
	{
		for (int sy = 0; sy < sprite.Height; sy++)
			for (int sx = 0; sx < sprite.Width; sx++)
			{
				if (!sprite[sx, sy]) continue;
				_px.Blend(x + sx + 1, y + sy + 1, new Color(0, 0, 0), alpha * 0.7);
			}
		for (int sy = 0; sy < sprite.Height; sy++)
			for (int sx = 0; sx < sprite.Width; sx++)
				if (sprite[sx, sy]) _px.Blend(x + sx, y + sy, color(sx, sy), alpha);
	}

	#endregion

	private void DrawPopups(Game game)
	{
		foreach (var pop in game.Fx.Popups)
		{
			double life = pop.Life / pop.MaxLife;
			var c = PixelSurface.Lerp(pop.Color, new Color(40, 30, 70), Math.Max(0, life - 0.4) / 0.6);
			_px.Text((int)pop.X - pop.Text.Length / 2, (int)pop.Y / 2, pop.Text, c);
		}
	}

	private (int X, int Y) ShakeOffset(float amount)
	{
		if (amount < 0.3f) return (0, 0);
		return ((int)Math.Round((_rng.NextDouble() * 2 - 1) * amount),
			(int)Math.Round((_rng.NextDouble() * 2 - 1) * amount * 0.6));
	}
}
