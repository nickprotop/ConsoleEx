// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Drawing;

/// <summary>
/// An RGB pixel grid at twice a terminal's vertical resolution, composited into a
/// canvas as half-block cells.
/// </summary>
/// <remarks>
/// <para>
/// A terminal cell is roughly twice as tall as it is wide. Treating each cell as two
/// stacked pixels and painting it with <c>▀</c> — foreground the top pixel,
/// background the bottom — doubles the vertical resolution and makes the pixels
/// nearly square. Circles look round, diagonals are smooth, and a chart gets two
/// rows of detail per row of cells.
/// </para>
/// <para>
/// This is a drawing target, not a control. Allocate one, draw into it, then
/// <see cref="Present(CanvasGraphics, Color)"/> it into a
/// <see cref="Controls.CanvasControl"/>. It sits beside <see cref="CanvasGraphics"/>
/// rather than replacing it: that one draws characters, this one draws pixels, and
/// a caller picks whichever the job wants.
/// </para>
/// <para>
/// A separate text layer floats glyphs over the pixels, taking the average of the
/// two pixels beneath as its background so lettering does not punch holes in what
/// is behind it. Text goes through the markup parser, so it handles wide
/// characters, combining marks and markup tags.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var surface = new PixelSurface();
/// surface.EnsureSize(g.Width, g.Height);
/// surface.Fill(background);
/// surface.FillCircle(20, 14, 6, Color.Red);
/// surface.TextCentered(2, "[bold]Chart[/]", Color.White);
/// surface.Present(g, background);
/// </code>
/// </example>
public sealed class PixelSurface
{
	/// <summary>The glyph that shows two pixels in one cell.</summary>
	private const char UpperHalf = '▀';

	private Color[] _pixels = Array.Empty<Color>();
	private TextCell[] _text = Array.Empty<TextCell>();

	/// <summary>Width in pixels, which equals the canvas width in columns.</summary>
	public int Width { get; private set; }

	/// <summary>Height in pixels: twice the canvas height in rows.</summary>
	public int Height { get; private set; }

	/// <summary>Height in canvas rows.</summary>
	public int Rows => Height / 2;

	/// <summary>One glyph of the text layer.</summary>
	private struct TextCell
	{
		public Cell Cell;
		public bool Set;
		public bool HasBg;
	}

	/// <summary>
	/// Resizes the surface to match a canvas, in cells. Cheap when nothing changed.
	/// </summary>
	/// <param name="columns">Canvas width in columns.</param>
	/// <param name="rows">Canvas height in rows.</param>
	/// <remarks>Contents are not preserved across a size change.</remarks>
	public void EnsureSize(int columns, int rows)
	{
		columns = Math.Max(0, columns);
		rows = Math.Max(0, rows);
		if (columns == Width && rows * 2 == Height) return;

		Width = columns;
		Height = rows * 2;
		_pixels = new Color[Width * Height];
		_text = new TextCell[Width * rows];
	}

	#region Pixels

	/// <summary>True when the coordinate is on the surface.</summary>
	public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

	/// <summary>Fills every pixel.</summary>
	public void Fill(Color c) => Array.Fill(_pixels, c);

	/// <summary>Reads a pixel, or black when out of bounds.</summary>
	public Color Get(int x, int y) => InBounds(x, y) ? _pixels[y * Width + x] : Color.Black;

	/// <summary>Sets a pixel. Out-of-bounds writes are ignored.</summary>
	public void Set(int x, int y, Color c)
	{
		if (InBounds(x, y)) _pixels[y * Width + x] = c;
	}

	/// <summary>Blends a colour over a pixel.</summary>
	/// <param name="x">Pixel X.</param>
	/// <param name="y">Pixel Y.</param>
	/// <param name="c">The colour to blend in.</param>
	/// <param name="alpha">0 leaves the pixel alone, 1 replaces it.</param>
	public void Blend(int x, int y, Color c, double alpha)
	{
		if (!InBounds(x, y) || alpha <= 0) return;
		if (alpha >= 1) { _pixels[y * Width + x] = c; return; }
		ref var p = ref _pixels[y * Width + x];
		p = Lerp(p, c, alpha);
	}

	/// <summary>Adds light to a pixel, for glows and sparks. Clamps at white.</summary>
	public void Add(int x, int y, Color c, double amount)
	{
		if (!InBounds(x, y) || amount <= 0) return;
		ref var p = ref _pixels[y * Width + x];
		p = new Color(
			(byte)Math.Min(255, p.R + c.R * amount),
			(byte)Math.Min(255, p.G + c.G * amount),
			(byte)Math.Min(255, p.B + c.B * amount));
	}

	/// <summary>Fills a rectangle.</summary>
	public void FillRect(int x, int y, int w, int h, Color c)
	{
		int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
		int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
		for (int py = y0; py < y1; py++)
			Array.Fill(_pixels, c, py * Width + x0, Math.Max(0, x1 - x0));
	}

	/// <summary>Blends a rectangle, for soft shadows and tints.</summary>
	public void BlendRect(int x, int y, int w, int h, Color c, double alpha)
	{
		int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
		int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
		for (int py = y0; py < y1; py++)
			for (int px = x0; px < x1; px++)
				Blend(px, py, c, alpha);
	}

	/// <summary>
	/// Fills a circle with a softened edge.
	/// </summary>
	/// <remarks>
	/// The last half-pixel is blended rather than cut. At the small radii this
	/// surface is used for, a hard edge reads as a staircase rather than a curve.
	/// </remarks>
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

				double edge = radius - Math.Sqrt(d2);
				Blend(px, py, c, Math.Clamp(edge * 2, 0, 1));
			}
	}

	/// <summary>Adds a soft halo of light around a point.</summary>
	/// <param name="cx">Centre X, in pixels.</param>
	/// <param name="cy">Centre Y, in pixels.</param>
	/// <param name="radius">Radius, in pixels.</param>
	/// <param name="c">The light's colour.</param>
	/// <param name="amount">Brightness at the centre, falling off to nothing at the edge.</param>
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

	/// <summary>Draws a line, Bresenham.</summary>
	public void DrawLine(int x0, int y0, int x1, int y1, Color c)
	{
		int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
		int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
		int err = dx + dy;

		while (true)
		{
			Set(x0, y0, c);
			if (x0 == x1 && y0 == y1) break;
			int e2 = err * 2;
			if (e2 >= dy) { err += dy; x0 += sx; }
			if (e2 <= dx) { err += dx; y0 += sy; }
		}
	}

	/// <summary>
	/// Draws a <see cref="PixelMask"/>; lit pixels take <paramref name="c"/>.
	/// </summary>
	/// <param name="mask">The shape.</param>
	/// <param name="x">Left edge, in pixels.</param>
	/// <param name="y">Top edge, in pixels.</param>
	/// <param name="c">The colour for lit pixels.</param>
	/// <param name="rowShade">
	/// 0 draws flat. Above 0, each row is darkened a little more than the one above
	/// it, up to this fraction at the bottom - enough to suggest a light source
	/// without needing a second colour.
	/// </param>
	public void Draw(PixelMask mask, int x, int y, Color c, double rowShade = 0)
	{
		ArgumentNullException.ThrowIfNull(mask);

		for (int my = 0; my < mask.Height; my++)
		{
			var rowColor = rowShade > 0
				? Lerp(c, Color.Black, rowShade * my / Math.Max(1, mask.Height - 1))
				: c;

			for (int mx = 0; mx < mask.Width; mx++)
				if (mask[mx, my]) Set(x + mx, y + my, rowColor);
		}
	}

	/// <summary>
	/// Adds a soft halo around a mask's lit pixels.
	/// </summary>
	/// <param name="mask">The shape to light up.</param>
	/// <param name="x">Left edge, in pixels.</param>
	/// <param name="y">Top edge, in pixels.</param>
	/// <param name="c">The light's colour.</param>
	/// <param name="amount">Brightness added to each neighbouring pixel.</param>
	/// <remarks>
	/// Lights the four neighbours of every lit pixel rather than computing a real
	/// distance field: at these sizes the difference is invisible and the cost is a
	/// fraction of it.
	/// </remarks>
	public void GlowMask(PixelMask mask, int x, int y, Color c, double amount)
	{
		ArgumentNullException.ThrowIfNull(mask);
		if (amount <= 0) return;

		for (int my = 0; my < mask.Height; my++)
			for (int mx = 0; mx < mask.Width; mx++)
			{
				if (!mask[mx, my]) continue;
				Add(x + mx - 1, y + my, c, amount);
				Add(x + mx + 1, y + my, c, amount);
				Add(x + mx, y + my - 1, c, amount);
				Add(x + mx, y + my + 1, c, amount);
			}
	}

	/// <summary>Darkens every pixel towards black.</summary>
	/// <param name="amount">0 changes nothing, 1 goes fully black.</param>
	public void Dim(double amount)
	{
		if (amount <= 0) return;
		for (int i = 0; i < _pixels.Length; i++)
			_pixels[i] = Lerp(_pixels[i], Color.Black, amount);
	}

	/// <summary>Pulls every pixel towards its own luminance.</summary>
	/// <param name="amount">0 changes nothing, 1 is fully grey.</param>
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

	/// <summary>Clears the text layer, leaving the pixels alone.</summary>
	public void ClearText() => Array.Clear(_text);

	/// <summary>
	/// Writes text over the pixels, at a cell position.
	/// </summary>
	/// <param name="col">Column, in cells.</param>
	/// <param name="row">Row, in cells.</param>
	/// <param name="markup">The text. Markup tags are honoured.</param>
	/// <param name="fg">Default foreground, for text that sets no colour of its own.</param>
	/// <param name="bg">
	/// A background, or null to let the pixels beneath show through.
	/// </param>
	/// <returns>The width written, in columns.</returns>
	/// <remarks>
	/// Goes through the markup parser rather than writing characters one at a time,
	/// so a wide glyph takes the two columns it needs, a combining mark stays with
	/// its letter, and markup works. Writing per <c>char</c> would split surrogate
	/// pairs and mis-measure every non-Latin label.
	/// </remarks>
	public int Text(int col, int row, string markup, Color fg, Color? bg = null)
	{
		if (string.IsNullOrEmpty(markup) || (uint)row >= (uint)Rows) return 0;

		var cells = MarkupParser.Parse(markup, fg, bg ?? Color.Black);
		for (int i = 0; i < cells.Count; i++)
		{
			int cx = col + i;
			if ((uint)cx >= (uint)Width) continue;

			_text[row * Width + cx] = new TextCell
			{
				Cell = cells[i],
				Set = true,
				HasBg = bg.HasValue,
			};
		}

		// Already in display columns: Parse emits a continuation cell per wide glyph.
		return cells.Count;
	}

	/// <summary>Writes text centred on a row, measured in display columns.</summary>
	/// <returns>The width written, in columns.</returns>
	public int TextCentered(int row, string markup, Color fg, Color? bg = null)
	{
		if (string.IsNullOrEmpty(markup)) return 0;

		// StripLength measures what will be DRAWN: markup tags removed, wide glyphs
		// counted twice. Using markup.Length would centre on the tags as well.
		int width = MarkupParser.StripLength(markup);
		return Text((Width - width) / 2, row, markup, fg, bg);
	}

	#endregion

	#region Present

	/// <summary>
	/// Composites the surface into a canvas.
	/// </summary>
	/// <param name="g">The canvas to draw into.</param>
	/// <param name="voidColor">What to show where the surface does not reach.</param>
	public void Present(CanvasGraphics g, Color voidColor) => Present(g, 0, 0, voidColor);

	/// <summary>
	/// Composites the surface into a canvas, displaced by an offset.
	/// </summary>
	/// <param name="g">The canvas to draw into.</param>
	/// <param name="offsetX">Horizontal displacement, in pixels.</param>
	/// <param name="offsetY">Vertical displacement, in pixels.</param>
	/// <param name="voidColor">What to show where the surface does not reach.</param>
	/// <remarks>
	/// The offset shifts where each cell READS from, so nothing is moved or
	/// resampled and pixels shifted off the surface come back as
	/// <paramref name="voidColor"/>. That is what a screen shake is: a sampling
	/// offset, not a transform.
	/// </remarks>
	public void Present(CanvasGraphics g, int offsetX, int offsetY, Color voidColor)
	{
		ArgumentNullException.ThrowIfNull(g);

		int rows = Math.Min(Rows, g.Height);
		int cols = Math.Min(Width, g.Width);

		for (int cy = 0; cy < rows; cy++)
		{
			for (int cx = 0; cx < cols; cx++)
			{
				int sx = cx - offsetX;
				int topY = cy * 2 - offsetY;

				var top = InBounds(sx, topY) ? _pixels[topY * Width + sx] : voidColor;
				var bottom = InBounds(sx, topY + 1) ? _pixels[(topY + 1) * Width + sx] : voidColor;

				var text = _text[cy * Width + cx];
				if (text.Set)
				{
					// A glyph with no background of its own floats on the average of
					// the two pixels under it, so it sits in the scene rather than on
					// a rectangle cut out of it.
					var cell = text.Cell;
					if (!text.HasBg) cell.Background = Lerp(top, bottom, 0.5);

					// SetCell, not SetNarrowCell: the parser's wide-continuation flag
					// and combining marks have to survive the write.
					g.SetCell(cx, cy, cell);
				}
				else if (top == bottom)
				{
					// One colour: a space costs the same and avoids a glyph the font
					// might render fractionally differently.
					g.SetNarrowCell(cx, cy, ' ', top, top);
				}
				else
				{
					g.SetNarrowCell(cx, cy, UpperHalf, top, bottom);
				}
			}
		}
	}

	#endregion

	#region Colour helpers

	/// <summary>Blends two colours. <paramref name="t"/> is clamped to [0,1].</summary>
	public static Color Lerp(Color a, Color b, double t)
	{
		t = Math.Clamp(t, 0, 1);
		return new Color(
			(byte)(a.R + (b.R - a.R) * t),
			(byte)(a.G + (b.G - a.G) * t),
			(byte)(a.B + (b.B - a.B) * t));
	}

	/// <summary>Scales brightness. Above 1 brightens, clamped at white.</summary>
	public static Color Scale(Color c, double k) => new(
		(byte)Math.Clamp(c.R * k, 0, 255),
		(byte)Math.Clamp(c.G * k, 0, 255),
		(byte)Math.Clamp(c.B * k, 0, 255));

	#endregion
}
