// -----------------------------------------------------------------------
// Breakout — the renderer.
//
// Reads the Game and paints it into a PixelSurface, presented into the
// CanvasControl as half-block cells. Nothing here mutates game state.
// -----------------------------------------------------------------------

using Games.Common;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;

namespace Breakout;

internal sealed class Renderer
{
	#region Palette

	private static readonly Color Void = new(6, 7, 16);
	private static readonly Color FieldTop = new(14, 16, 34);
	private static readonly Color FieldBottom = new(8, 9, 20);
	private static readonly Color SideRail = new(44, 50, 86);
	private static readonly Color PaddleCore = new(230, 240, 255);
	private static readonly Color PaddleEdge = new(110, 150, 230);
	private static readonly Color PaddleWide = new(255, 220, 130);
	private static readonly Color BallCore = new(255, 255, 255);
	private static readonly Color BallHalo = new(130, 200, 255);
	private static readonly Color BallSlow = new(150, 220, 255);

	#endregion

	private readonly PixelSurface _surface = new();

	public void Render(CanvasGraphics g, Game game, double now)
	{
		_surface.EnsureSize(g.Width, g.Height);
		_surface.ClearText();

		// Shake moves the whole field, so it is applied once at Present time
		// rather than to every drawn thing.
		var fx = game.Fx;
		int shakeX = 0, shakeY = 0;
		if (fx.Shake > 0.05f)
		{
			double phase = now * 47;
			shakeX = (int)Math.Round(Math.Sin(phase) * fx.Shake);
			shakeY = (int)Math.Round(Math.Cos(phase * 1.3) * fx.Shake * 0.6);
		}

		DrawField(game, now);
		DrawBricks(game, now);
		DrawDrops(game, now);
		DrawPaddle(game, now);
		DrawBalls(game, now);
		DrawParticles(game);
		DrawFlash(game);
		DrawOverlay(game, now);

		_surface.Present(g, shakeX, shakeY, Void);
	}

	private void DrawField(Game game, double now)
	{
		int w = _surface.Width;
		int h = _surface.Height;

		for (int y = 0; y < h; y++)
		{
			double t = h <= 1 ? 0 : (double)y / (h - 1);
			_surface.FillRect(0, y, w, 1, PixelSurface.Lerp(FieldTop, FieldBottom, t));
		}

		// A faint rail down each side so the bounce walls are visible even with
		// no ball near them.
		for (int y = 0; y < h; y++)
		{
			_surface.Blend(0, y, SideRail, 0.5);
			_surface.Blend(w - 1, y, SideRail, 0.5);
		}
		for (int x = 0; x < w; x++)
		{
			_surface.Blend(x, 0, SideRail, 0.5);
		}

		// The floor glows red when a ball is close to being lost.
		float nearest = float.MaxValue;
		foreach (var ball in game.Balls)
		{
			if (!ball.Held) nearest = Math.Min(nearest, game.Height - ball.Y);
		}
		if (nearest < 12)
		{
			double danger = 1 - nearest / 12.0;
			for (int x = 0; x < w; x++)
			{
				_surface.Add(x, h - 1, new Color(255, 60, 60), danger * 0.5);
				_surface.Add(x, h - 2, new Color(255, 60, 60), danger * 0.25);
			}
		}
	}

	private void DrawBricks(Game game, double now)
	{
		foreach (var brick in game.Bricks)
		{
			if (!brick.Alive) continue;

			var baseColor = Sprites.RowColors[brick.Row % Sprites.RowColors.Length];

			// A two-hit brick reads as armoured: darker, with a bright rim.
			var body = brick.Hits > 1
				? PixelSurface.Scale(baseColor, 0.55)
				: baseColor;

			if (brick.Flash > 0)
			{
				body = PixelSurface.Lerp(body, new Color(255, 255, 255), brick.Flash * 0.8);
			}

			_surface.FillRect(brick.X, brick.Y, brick.Width, brick.Height, body);

			// Top highlight and bottom shade give the wall depth at two pixels tall.
			for (int x = brick.X; x < brick.X + brick.Width; x++)
			{
				_surface.Blend(x, brick.Y, new Color(255, 255, 255), 0.22);
				_surface.Blend(x, brick.Y + brick.Height - 1, new Color(0, 0, 0), 0.25);
			}

			if (brick.Hits > 1)
			{
				for (int x = brick.X; x < brick.X + brick.Width; x++)
				{
					_surface.Blend(x, brick.Y, baseColor, 0.6);
				}
			}
		}
	}

	private void DrawPaddle(Game game, double now)
	{
		float half = game.PaddleWidth / 2;
		int x0 = (int)MathF.Round(game.PaddleX - half);
		int width = (int)MathF.Round(game.PaddleWidth);
		int y = (int)game.PaddleY;
		int thickness = (int)game.PaddleThickness;

		var core = game.WideActive ? PaddleWide : PaddleCore;
		var edge = game.WideActive ? PixelSurface.Scale(PaddleWide, 0.55) : PaddleEdge;

		_surface.FillRect(x0, y, width, thickness, edge);

		// A bright centre band, narrowing toward the ends, so the steering zone
		// the bounce angle depends on is something the player can see.
		int inset = Math.Max(1, width / 6);
		_surface.FillRect(x0 + inset, y, width - inset * 2, 1, core);

		_surface.Glow(game.PaddleX, y, width * 0.45, core, 0.1);
	}

	private void DrawBalls(Game game, double now)
	{
		foreach (var ball in game.Balls)
		{
			var core = game.SlowActive ? BallSlow : BallCore;
			var halo = game.SlowActive ? new Color(80, 160, 230) : BallHalo;

			_surface.Glow(ball.X, ball.Y, ball.Radius * 4.5, halo, 0.5);
			_surface.FillCircle(ball.X, ball.Y, ball.Radius, core);

			if (ball.Held)
			{
				// Held on the paddle: a pulse so it is obvious the serve is waiting.
				double pulse = 0.5 + 0.5 * Math.Sin(now * 6);
				_surface.Glow(ball.X, ball.Y, ball.Radius * 6, core, pulse * 0.35);
			}
		}
	}

	private void DrawDrops(Game game, double now)
	{
		foreach (var drop in game.Drops)
		{
			var color = drop.Kind switch
			{
				Drop.Wide => new Color(255, 210, 110),
				Drop.Slow => new Color(130, 200, 255),
				Drop.Multi => new Color(190, 140, 255),
				_ => new Color(120, 235, 150),
			};

			double bob = Math.Sin(now * 8 + drop.X) * 0.4;
			int x = (int)MathF.Round(drop.X) - Sprites.Capsule.Width / 2;
			int y = (int)MathF.Round(drop.Y + (float)bob);

			_surface.Glow(drop.X, drop.Y, 4, color, 0.4);
			_surface.Draw(Sprites.Capsule, x, y, color);
		}
	}

	private void DrawParticles(Game game)
	{
		foreach (ref readonly var p in game.Fx.Particles)
		{
			// Life counts UP toward MaxLife, so age is the gradient position and
			// what is left of it is the fade.
			double age = p.MaxLife <= 0 ? 1 : Math.Clamp(p.Life / p.MaxLife, 0, 1);
			double fade = 1 - age;
			if (fade <= 0) continue;

			var tint = p.Palette.Interpolate(age);
			int x = (int)MathF.Round(p.X);
			int y = (int)MathF.Round(p.Y);

			if (p.Glow)
			{
				_surface.Add(x, y, tint, fade * 0.9);
				_surface.Glow(p.X, p.Y, 2.2, tint, fade * 0.3);
			}
			else
			{
				_surface.Blend(x, y, tint, fade);
			}
		}

		foreach (var popup in game.Fx.Popups)
		{
			float fade = popup.MaxLife <= 0 ? 0 : 1 - popup.Life / popup.MaxLife;
			if (fade <= 0.05f) continue;

			var color = PixelSurface.Scale(popup.Color, fade);
			int col = (int)MathF.Round(popup.X) - popup.Text.Length / 2;
			_surface.Text(col, (int)(popup.Y / 2), popup.Text, color);
		}
	}

	private void DrawFlash(Game game)
	{
		float intensity = game.Fx.FlashIntensity;
		if (intensity <= 0.004f) return;

		_surface.BlendRect(
			0, 0, _surface.Width, _surface.Height,
			game.Fx.FlashColor, Math.Min(0.75, intensity));
	}

	private void DrawOverlay(Game game, double now)
	{
		switch (game.Phase)
		{
			case Phase.Attract:
				_surface.Dim(0.45);
				DrawBanner(game, "BREAKOUT", "Space or click to play", now);
				break;

			case Phase.Serving:
				_surface.TextCentered(
					_surface.Rows - 6,
					"[bold]Space[/] to serve",
					new Color(200, 215, 255));
				break;

			case Phase.Cleared:
				_surface.Dim(0.3);
				DrawBanner(game, "CLEARED", $"Level {game.Level + 1}", now);
				break;

			case Phase.GameOver:
				_surface.Dim(0.55);
				DrawBanner(game, "GAME OVER", $"{game.Score} points", now);
				break;
		}

		if (game.Paused)
		{
			_surface.Dim(0.5);
			_surface.TextCentered(_surface.Rows / 2, "[bold]PAUSED[/]", new Color(255, 230, 150));
		}
	}

	private void DrawBanner(Game game, string title, string subtitle, double now)
	{
		var mask = BigText.Render(title);
		int x = (_surface.Width - mask.Width) / 2;
		int y = Math.Max(2, _surface.Height / 2 - mask.Height);

		double pulse = 0.65 + 0.35 * Math.Sin(now * 2.4);
		_surface.GlowMask(mask, x, y, new Color(120, 180, 255), pulse * 0.4);
		_surface.Draw(mask, x, y, new Color(235, 245, 255), 0.25);

		_surface.TextCentered(
			(y + mask.Height) / 2 + 2,
			subtitle,
			PixelSurface.Scale(new Color(190, 210, 255), pulse));
	}
}
