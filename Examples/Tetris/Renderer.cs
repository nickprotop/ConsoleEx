// -----------------------------------------------------------------------
// Tetris - the renderer.
//
// Reads the Game and paints it into a PixelSurface, presented into the
// CanvasControl as half-block cells. Nothing here mutates game state.
//
// A board cell is drawn two pixels square, which is one terminal cell wide
// and one tall: the well then has the proportions players expect rather
// than being squashed flat by the terminal's own 1:2 aspect.
// -----------------------------------------------------------------------

using Games.Common;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Parsing;

namespace Tetris;

internal sealed class Renderer
{
	/// <summary>
	/// The shape of one board cell, in surface pixels. A pixel here is a whole terminal column
	/// wide and half a row tall, so a square cell needs twice as many pixels across as down:
	/// this 2:1 pair is the unit the board is scaled in, and scaling it as a unit is what keeps
	/// cells square. Equal numbers would draw the board squashed to half its height.
	/// </summary>
	private const int CellUnitW = 2;
	private const int CellUnitH = 1;

	/// <summary>
	/// How many units a cell may grow to. Four is 8x4 pixels, an eight-column cell, which is
	/// as large as the board reads well at; beyond that it is a wall of colour.
	/// </summary>
	private const int MaxCellScale = 4;

	/// <summary>
	/// Pixels the well's own frame needs beyond the playfield itself: the wall runs one pixel
	/// above the first row, the floor is two pixels thick below the last, and a little air is
	/// left above and below so neither sits flush against the window edge.
	/// </summary>
	private const int WellFramePixels = 8;

	/// <summary>Columns the side walls take, two on each side of the playfield.</summary>
	private const int WellFrameColumns = 4;

	private readonly PixelSurface _surface = new();

	/// <summary>Where the well was last drawn, so overlays can centre on it.</summary>
	private int _wellLeft;
	private int _wellWidth;

	/// <summary>Centre of the well in pixels, for overlay text and art.</summary>
	private int WellCentre => _wellLeft + _wellWidth / 2;

	/// <summary>Writes text centred on the well rather than on the whole surface.</summary>
	private int TextOnWell(int row, string markup, Color fg)
	{
		int width = MarkupParser.StripLength(markup);
		return _surface.Text(Math.Max(0, WellCentre - width / 2), row, markup, fg);
	}

	public void Render(CanvasGraphics g, Game game, double now)
	{
		_surface.EnsureSize(g.Width, g.Height);
		_surface.ClearText();
		_surface.Fill(new Color(8, 9, 18));

		var fx = game.Fx;
		int shakeX = 0, shakeY = 0;
		if (fx.Shake > 0.05f)
		{
			double phase = now * 47;
			shakeX = (int)Math.Round(Math.Sin(phase) * fx.Shake);
			shakeY = (int)Math.Round(Math.Cos(phase * 1.3) * fx.Shake * 0.6);
		}

		// Grow the cell to fill the terminal rather than drawing a fixed small board in the
		// middle of it: the 2:1 unit is multiplied by whatever both axes can afford, so a tall
		// window gets a tall well instead of the same postage stamp with empty space under it.
		// Whichever axis runs out first decides, and the pair scales together so cells stay
		// square - growing or shrinking one alone would squash the board.
		// The canvas is its own column in the layout grid, with the HUD in the next one, so the
		// width here is already the well's to use: subtracting the HUD again would reserve it
		// twice and squeeze the board into a corner.
		int scale = Math.Min(
			(_surface.Height - WellFramePixels) / (Game.Rows * CellUnitH),
			(_surface.Width - WellFrameColumns) / (Game.Columns * CellUnitW));

		scale = Math.Clamp(scale, 1, MaxCellScale);

		int cellW = CellUnitW * scale;
		int cellH = CellUnitH * scale;

		game.CellWidth = cellW;
		game.CellHeight = cellH;
		int wellWidth = Game.Columns * cellW;
		int wellHeight = Game.Rows * cellH;
		// The frame sits outside the playfield, so centre the framed box and then step in by
		// the wall's own width: centring the playfield alone leaves the left wall clipped
		// against the window edge.
		int framedWidth = wellWidth + WellFrameColumns;
		int originX = Math.Max(WellFrameColumns / 2,
			(_surface.Width - framedWidth) / 2 + WellFrameColumns / 2);
		int originY = Math.Max(2, (_surface.Height - wellHeight) / 2);

		// Overlays belong over the board, not over the whole surface: the HUD owns the right
		// columns, so centring a title on the surface would hang it off the well's edge.
		_wellLeft = originX;
		_wellWidth = wellWidth;

		DrawWell(originX, originY, wellWidth, wellHeight, cellH);
		DrawStack(game, originX, originY, cellW, cellH, now);
		DrawGhost(game, originX, originY, cellW, cellH);
		DrawPiece(game, originX, originY, cellW, cellH);
		DrawParticles(game, originX, originY);
		DrawFlash(game);
		DrawOverlay(game, now);

		_surface.Present(g, shakeX, shakeY, new Color(8, 9, 18));
	}

	private void DrawWell(int x, int y, int w, int h, int cellH)
	{
		_surface.FillRect(x, y, w, h, Palettes.Empty);

		// A faint grid, so an empty well still reads as a playfield rather than a void.
		for (int row = 0; row <= Game.Rows; row++)
			for (int px = 0; px < w; px += 2)
				_surface.Blend(x + px, y + row * cellH, Palettes.Grid, 0.55);

		// Walls and floor.
		for (int py = -1; py <= h; py++)
		{
			_surface.FillRect(x - 2, y + py, 2, 1, Palettes.Wall);
			_surface.FillRect(x + w, y + py, 2, 1, Palettes.Wall);
		}
		_surface.FillRect(x - 2, y + h, w + 4, 2, Palettes.Wall);
	}

	private void DrawStack(Game game, int originX, int originY, int cellW, int cellH, double now)
	{
		// A clearing row pulses white before it collapses, so the clear is something that
		// happens rather than something that already happened.
		double pulse = 0.5 + 0.5 * Math.Sin(now * 26);

		for (int row = 0; row < Game.Rows; row++)
		{
			bool clearing = game.ClearingRows.Contains(row + Game.HiddenRows);

			for (int column = 0; column < Game.Columns; column++)
			{
				int cell = game.CellAt(row, column);
				if (cell == 0) continue;

				var color = Palettes.ForCell(cell);
				if (clearing) color = PixelSurface.Lerp(color, Color.White, pulse);

				DrawBlock(originX + column * cellW, originY + row * cellH, color, cellW, cellH);
			}
		}
	}

	private void DrawGhost(Game game, int originX, int originY, int cellW, int cellH)
	{
		if (game.Piece is not { } piece || game.Phase != Phase.Playing) return;

		int ghostY = game.GhostY;
		if (ghostY == piece.Y) return;

		foreach (var (cx, cy) in Pieces.Cells(piece.Kind, piece.Rotation))
		{
			int row = ghostY + cy;
			if (row < 0) continue;

			int x = originX + (piece.X + cx) * cellW;
			int y = originY + row * cellH;

			// Outline only: a filled ghost competes with the piece itself for attention.
			for (int i = 0; i < cellW; i++)
			{
				_surface.Blend(x + i, y, Palettes.Ghost, 0.8);
				_surface.Blend(x + i, y + cellH - 1, Palettes.Ghost, 0.8);
			}
			for (int i = 0; i < cellH; i++)
			{
				_surface.Blend(x, y + i, Palettes.Ghost, 0.8);
				_surface.Blend(x + cellW - 1, y + i, Palettes.Ghost, 0.8);
			}
		}
	}

	private void DrawPiece(Game game, int originX, int originY, int cellW, int cellH)
	{
		if (game.Piece is not { } piece) return;

		var color = Palettes.For(piece.Kind);
		foreach (var (cx, cy) in Pieces.Cells(piece.Kind, piece.Rotation))
		{
			int row = piece.Y + cy;
			if (row < 0) continue;   // still above the ceiling

			DrawBlock(originX + (piece.X + cx) * cellW, originY + row * cellH, color, cellW, cellH);
		}
	}

	/// <summary>One board cell: a lit square with a highlight, so the stack has depth.</summary>
	private void DrawBlock(int x, int y, Color color, int cellW, int cellH)
	{
		_surface.FillRect(x, y, cellW, cellH, color);

		// A lit top edge and a dark bottom one: at two rows a cell this is the whole of the
		// depth cue, and without it the stack reads as a flat colour field.
		for (int i = 0; i < cellW; i++)
		{
			_surface.Blend(x + i, y, Color.White, 0.28);
			_surface.Blend(x + i, y + cellH - 1, Color.Black, 0.22);
		}
	}

	private void DrawParticles(Game game, int originX, int originY)
	{
		foreach (ref readonly var p in game.Fx.Particles)
		{
			double age = p.MaxLife <= 0 ? 1 : Math.Clamp(p.Life / p.MaxLife, 0, 1);
			double fade = 1 - age;
			if (fade <= 0) continue;

			var tint = p.Palette.Interpolate(age);
			int x = originX + (int)MathF.Round(p.X);
			int y = originY + (int)MathF.Round(p.Y);

			if (p.Glow)
			{
				_surface.Add(x, y, tint, fade * 0.9);
				_surface.Glow(x, y, 2.2, tint, fade * 0.3);
			}
			else
			{
				_surface.Blend(x, y, tint, fade);
			}
		}
	}

	private void DrawFlash(Game game)
	{
		float intensity = game.Fx.FlashIntensity;
		if (intensity <= 0.004f) return;

		_surface.BlendRect(0, 0, _surface.Width, _surface.Height,
			game.Fx.FlashColor, Math.Min(0.7, intensity));
	}

	private void DrawOverlay(Game game, double now)
	{
		switch (game.Phase)
		{
			case Phase.Title:
				_surface.Dim(0.5);
				DrawBanner("TETRIS", "Space or click to play", now);
				break;

			case Phase.GameOver:
				_surface.Dim(0.6);
				DrawBanner("GAME OVER", $"{game.Score:N0} points", now);
				break;
		}

		if (game.Paused)
		{
			_surface.Dim(0.55);
			TextOnWell(_surface.Rows / 2, "[bold]PAUSED[/]", new Color(255, 230, 150));
		}
	}

	private void DrawBanner(string title, string subtitle, double now)
	{
		var mask = BigText.Render(title);
		int x = Math.Max(0, WellCentre - mask.Width / 2);
		int y = Math.Max(2, _surface.Height / 2 - mask.Height);

		double pulse = 0.65 + 0.35 * Math.Sin(now * 2.4);
		_surface.GlowMask(mask, x, y, new Color(120, 200, 255), pulse * 0.4);
		_surface.Draw(mask, x, y, new Color(235, 245, 255), 0.25);

		TextOnWell((y + mask.Height) / 2 + 2, subtitle,
			PixelSurface.Scale(new Color(190, 210, 255), pulse));
	}
}
