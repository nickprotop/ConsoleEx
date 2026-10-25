// -----------------------------------------------------------------------
// SnakeGame — half-block pixel surface.
//
// The arena is drawn into an RGB pixel grid twice as tall as the canvas
// (each terminal cell is two "pixels"), then presented with '▀' cells whose
// foreground is the top pixel and background the bottom. Terminal cells are
// roughly 1:2, so these pixels come out near-square — which is what lets a
// snake made of 4×4 pixel segments look square rather than stretched.
//
// A separate text layer holds glyph overlays (score pop-ups, prompts). Text
// cells take the averaged colour of the two pixels beneath as background,
// so lettering floats over the arena instead of punching holes in it.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drawing;

namespace SnakeGame;

internal sealed class PixelSurface
{
	private const char UpperHalf = '▀';

	private Color[] _pixels = Array.Empty<Color>();
	private TextCell[] _text = Array.Empty<TextCell>();

	/// <summary>Width in pixels (= canvas columns).</summary>
	public int Width { get; private set; }

	/// <summary>Height in pixels (= canvas rows × 2).</summary>
	public int Height { get; private set; }

	/// <summary>Canvas rows.</summary>
	public int Rows => Height / 2;

	private struct TextCell
	{
		public char Ch;
		public Color Fg;
		public Color Bg;
		public bool HasBg;
	}

	/// <summary>Ensures the surface matches the canvas size (in cells).</summary>
	public void EnsureSize(int columns, int rows)
	{
		if (columns == Width && rows * 2 == Height) return;
		Width = Math.Max(0, columns);
		Height = Math.Max(0, rows) * 2;
		_pixels = new Color[Width * Height];
		_text = new TextCell[Width * Math.Max(0, rows)];
	}

	#region Pixels

	public void Fill(Color c) => Array.Fill(_pixels, c);

	public void ClearText() => Array.Clear(_text);

	public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

	public void Set(int x, int y, Color c)
	{
		if (InBounds(x, y)) _pixels[y * Width + x] = c;
	}

	/// <summary>Alpha-blends <paramref name="c"/> over the pixel (alpha in [0,1]).</summary>
	public void Blend(int x, int y, Color c, double alpha)
	{
		if (!InBounds(x, y) || alpha <= 0) return;
		if (alpha >= 1) { _pixels[y * Width + x] = c; return; }
		ref var p = ref _pixels[y * Width + x];
		p = Lerp(p, c, alpha);
	}

	/// <summary>Additive light: brightens the pixel towards <paramref name="c"/> (glows, sparks).</summary>
	public void Add(int x, int y, Color c, double amount)
	{
		if (!InBounds(x, y) || amount <= 0) return;
		ref var p = ref _pixels[y * Width + x];
		p = new Color(
			(byte)Math.Min(255, p.R + c.R * amount),
			(byte)Math.Min(255, p.G + c.G * amount),
			(byte)Math.Min(255, p.B + c.B * amount));
	}

	public void FillRect(int x, int y, int w, int h, Color c)
	{
		int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
		int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
		for (int py = y0; py < y1; py++)
			Array.Fill(_pixels, c, py * Width + x0, Math.Max(0, x1 - x0));
	}

	/// <summary>Alpha-blends a rectangle, for soft shadows and glows.</summary>
	public void BlendRect(int x, int y, int w, int h, Color c, double alpha)
	{
		int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
		int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
		for (int py = y0; py < y1; py++)
			for (int px = x0; px < x1; px++)
				Blend(px, py, c, alpha);
	}

	/// <summary>Fills a circle, used for the food orb and round snake joints.</summary>
	public void FillCircle(double cx, double cy, double radius, Color c)
	{
		if (radius <= 0) return;
		int x0 = (int)Math.Floor(cx - radius), x1 = (int)Math.Ceiling(cx + radius);
		int y0 = (int)Math.Floor(cy - radius), y1 = (int)Math.Ceiling(cy + radius);
		double r2 = radius * radius;

		for (int py = y0; py <= y1; py++)
			for (int px = x0; px <= x1; px++)
			{
				double dx = px - cx, dy = py - cy;
				double d2 = dx * dx + dy * dy;
				if (d2 > r2) continue;

				// Soften the last half-pixel so the orb does not look like a staircase.
				double edge = radius - Math.Sqrt(d2);
				Blend(px, py, c, Math.Clamp(edge * 2, 0, 1));
			}
	}

	/// <summary>Additive halo around a point, for the food's pulse and the head's lamp.</summary>
	public void Glow(double cx, double cy, double radius, Color c, double amount)
	{
		if (radius <= 0 || amount <= 0) return;
		int x0 = (int)Math.Floor(cx - radius), x1 = (int)Math.Ceiling(cx + radius);
		int y0 = (int)Math.Floor(cy - radius), y1 = (int)Math.Ceiling(cy + radius);

		for (int py = y0; py <= y1; py++)
			for (int px = x0; px <= x1; px++)
			{
				double dx = px - cx, dy = py - cy;
				double d = Math.Sqrt(dx * dx + dy * dy);
				if (d > radius) continue;
				double falloff = 1 - d / radius;
				Add(px, py, c, amount * falloff * falloff);
			}
	}

	/// <summary>Draws a sprite mask; set pixels take <paramref name="c"/>.</summary>
	public void DrawSprite(Sprite sprite, int x, int y, Color c)
	{
		for (int sy = 0; sy < sprite.Height; sy++)
			for (int sx = 0; sx < sprite.Width; sx++)
				if (sprite[sx, sy]) Set(x + sx, y + sy, c);
	}

	/// <summary>Multiplies every pixel towards black (pause dim, fades).</summary>
	public void Dim(double amount)
	{
		if (amount <= 0) return;
		for (int i = 0; i < _pixels.Length; i++)
			_pixels[i] = Lerp(_pixels[i], Color.Black, amount);
	}

	/// <summary>Pulls every pixel towards its own luminance (game-over desaturation).</summary>
	public void Desaturate(double amount)
	{
		if (amount <= 0) return;
		for (int i = 0; i < _pixels.Length; i++)
		{
			var p = _pixels[i];
			byte l = (byte)(p.R * 0.3 + p.G * 0.59 + p.B * 0.11);
			_pixels[i] = Lerp(p, new Color(l, l, l), amount);
		}
	}

	#endregion

	#region Text layer

	/// <summary>Writes text at a cell position. Without <paramref name="bg"/> the pixels show through.</summary>
	public void Text(int col, int row, string s, Color fg, Color? bg = null)
	{
		if ((uint)row >= (uint)Rows) return;
		for (int i = 0; i < s.Length; i++)
		{
			int cx = col + i;
			if ((uint)cx >= (uint)Width) continue;
			ref var cell = ref _text[row * Width + cx];
			cell.Ch = s[i];
			cell.Fg = fg;
			cell.HasBg = bg.HasValue;
			cell.Bg = bg ?? default;
		}
	}

	/// <summary>Writes text centred on a row.</summary>
	public void TextCentered(int row, string s, Color fg, Color? bg = null) =>
		Text((Width - s.Length) / 2, row, s, fg, bg);

	#endregion

	/// <summary>
	/// Presents pixels + text into the canvas, optionally displaced by a screen-shake offset.
	/// </summary>
	public void Present(CanvasGraphics g, int shakeX, int shakeY, Color voidColor)
	{
		int rows = Math.Min(Rows, g.Height);
		int cols = Math.Min(Width, g.Width);

		for (int cy = 0; cy < rows; cy++)
		{
			for (int cx = 0; cx < cols; cx++)
			{
				int sx = cx - shakeX;
				int topY = cy * 2 - shakeY;
				var top = InBounds(sx, topY) ? _pixels[topY * Width + sx] : voidColor;
				var bottom = InBounds(sx, topY + 1) ? _pixels[(topY + 1) * Width + sx] : voidColor;

				var text = _text[cy * Width + cx];
				if (text.Ch != '\0')
				{
					var bg = text.HasBg ? text.Bg : Lerp(top, bottom, 0.5);
					g.SetNarrowCell(cx, cy, text.Ch, text.Fg, bg);
				}
				else if (top == bottom)
				{
					g.SetNarrowCell(cx, cy, ' ', top, top);
				}
				else
				{
					g.SetNarrowCell(cx, cy, UpperHalf, top, bottom);
				}
			}
		}
	}

	/// <summary>Linear RGB blend, always opaque.</summary>
	public static Color Lerp(Color a, Color b, double t)
	{
		t = Math.Clamp(t, 0, 1);
		return new Color(
			(byte)(a.R + (b.R - a.R) * t),
			(byte)(a.G + (b.G - a.G) * t),
			(byte)(a.B + (b.B - a.B) * t));
	}

	/// <summary>Scales brightness (values above 1 brighten, clamped).</summary>
	public static Color Scale(Color c, double k) => new(
		(byte)Math.Clamp(c.R * k, 0, 255),
		(byte)Math.Clamp(c.G * k, 0, 255),
		(byte)Math.Clamp(c.B * k, 0, 255));
}
