// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using StbTrueTypeSharp;

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// Rasterises glyphs from a monospace TrueType font, for the PNG screenshot verb.
/// </summary>
/// <remarks>
/// <para>
/// A screenshot without text is not a screenshot. Drawing real glyphs means finding a real
/// font: a hand-rolled ASCII atlas would misrender exactly the cases this library works
/// hardest to get right - CJK and other wide characters, combining marks, and Nerd Font
/// glyphs - and a picture that quietly draws those wrongly is worse than no picture.
/// </para>
/// <para>
/// Rasterised glyphs are cached per codepoint, because a screen repeats a small alphabet
/// thousands of times and re-rasterising each cell would dominate the capture.
/// </para>
/// </remarks>
internal sealed class DiagnosticsFont : IDisposable
{
	/// <summary>Where a monospace font usually lives, most specific first.</summary>
	/// <remarks>
	/// A Nerd Font comes first: it covers the powerline and icon glyphs a TUI actually
	/// draws, so it is the one most likely to render a real screen faithfully.
	/// </remarks>
	private static readonly string[] CandidateFonts =
	{
		"/usr/share/fonts/UbuntuMonoNerd/UbuntuMonoNerdFontMono-Regular.ttf",
		"/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
		"/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
		"/usr/share/fonts/liberation/LiberationMono-Regular.ttf",
		"/usr/share/fonts/TTF/DejaVuSansMono.ttf",
		"/Library/Fonts/Menlo.ttc",
		"/System/Library/Fonts/Menlo.ttc",
		"C:\\Windows\\Fonts\\consola.ttf",
	};

	private readonly byte[] _ttf;
	private readonly StbTrueType.stbtt_fontinfo _font;
	private readonly Dictionary<int, Glyph> _cache = new();
	private readonly float _scaleX;
	private readonly float _scaleY;
	private readonly int _baseline;
	private bool _disposed;

	/// <summary>A rasterised glyph: 8-bit coverage, and where to place it in the cell.</summary>
	private readonly record struct Glyph(byte[] Alpha, int W, int H, int OffX, int OffY);

	internal int CellWidth { get; }

	internal int CellHeight { get; }

	internal string Path { get; }

	private DiagnosticsFont(string path, byte[] ttf, StbTrueType.stbtt_fontinfo font,
		int cellWidth, int cellHeight, float scaleX, float scaleY, int baseline)
	{
		Path = path;
		_ttf = ttf;
		_font = font;
		CellWidth = cellWidth;
		CellHeight = cellHeight;
		_scaleX = scaleX;
		_scaleY = scaleY;
		_baseline = baseline;
	}

	/// <summary>
	/// Loads a font, sized so a cell is <paramref name="cellHeight"/> pixels tall.
	/// </summary>
	/// <param name="explicitPath">A caller-supplied .ttf, or null to search known locations.</param>
	/// <param name="cellHeight">Target cell height in pixels.</param>
	/// <param name="cellWidth">Target cell width, or 0 to take the font's advance.</param>
	/// <param name="error">Why no font could be loaded.</param>
	/// <returns>The font, or null.</returns>
	internal static DiagnosticsFont? Load(string? explicitPath, int cellHeight, int cellWidth, out string? error)
	{
		error = null;

		string? path = explicitPath;
		if (!string.IsNullOrWhiteSpace(path))
		{
			if (!File.Exists(path))
			{
				error = $"font not found: {path}";
				return null;
			}
		}
		else
		{
			path = CandidateFonts.FirstOrDefault(File.Exists);
			if (path == null)
			{
				error = "no monospace font found; pass \"font\":\"/path/to/mono.ttf\", "
					  + "or use \"glyphs\":false for a colour-only image";
				return null;
			}
		}

		byte[] ttf;
		try { ttf = File.ReadAllBytes(path); }
		catch (Exception ex) { error = $"cannot read font {path}: {ex.Message}"; return null; }

		var font = new StbTrueType.stbtt_fontinfo();
		float scaleY, scaleX;
		int baseline, advance;

		unsafe
		{
			fixed (byte* p = ttf)
			{
				if (StbTrueType.stbtt_InitFont(font, p, 0) == 0)
				{
					error = $"not a usable TrueType font: {path}";
					return null;
				}

				scaleY = StbTrueType.stbtt_ScaleForPixelHeight(font, cellHeight);

				int asc, desc, gap;
				StbTrueType.stbtt_GetFontVMetrics(font, &asc, &desc, &gap);
				baseline = (int)Math.Round(asc * scaleY);

				// 'M' is the conventional width probe, and in a monospace face every
				// glyph shares its advance.
				int adv, lsb;
				StbTrueType.stbtt_GetCodepointHMetrics(font, 'M', &adv, &lsb);
				advance = Math.Max(1, (int)Math.Round(adv * scaleY));

				// Cells sit at INTEGER pixel offsets, but the font's natural advance is
				// fractional (8.275 at 16px for DejaVu). Rounding it down and stepping by
				// the rounded value leaves a sub-pixel hole at every cell boundary - which
				// is invisible in letters but breaks box-drawing: U+2500 is drawn 10px wide
				// from x=-1 precisely so it overhangs and FUSES with its neighbours, and a
				// short step turns a continuous rule into a dashed one.
				//
				// So squeeze the horizontal scale until the advance lands exactly on the
				// integer cell width. The glyph keeps its designed overhang, the joins
				// close, and the <4% horizontal squeeze is imperceptible in text.
				int cellW = cellWidth > 0 ? cellWidth : advance;
				scaleX = adv > 0 ? (float)cellW / adv : scaleY;
			}
		}

		int cw = cellWidth > 0 ? cellWidth : advance;
		return new DiagnosticsFont(path, ttf, font, cw, cellHeight, scaleX, scaleY, baseline);
	}

	/// <summary>
	/// Blends a rune into an RGB buffer at a cell origin.
	/// </summary>
	/// <remarks>
	/// Coverage is used as an alpha between the cell's background and foreground, so
	/// antialiasing survives and text stays legible at small cell sizes.
	/// </remarks>
	internal void DrawRune(byte[] rgb, int imgW, int imgH, int originX, int originY,
		Rune rune, Color fg)
	{
		if (_disposed) return;

		int cp = rune.Value;
		if (cp == ' ' || cp == 0) return;

		if (!_cache.TryGetValue(cp, out var glyph))
		{
			glyph = Rasterise(cp);
			_cache[cp] = glyph;
		}

		if (glyph.Alpha.Length == 0) return;

		for (int gy = 0; gy < glyph.H; gy++)
		{
			int py = originY + _baseline + glyph.OffY + gy;
			if (py < 0 || py >= imgH) continue;

			for (int gx = 0; gx < glyph.W; gx++)
			{
				int px = originX + glyph.OffX + gx;
				if (px < 0 || px >= imgW) continue;

				byte a = glyph.Alpha[gy * glyph.W + gx];
				if (a == 0) continue;

				// Blend against what is ALREADY in the buffer, not against this cell's
				// background. Box-drawing glyphs are designed to overhang (U+2500 spans
				// 10px from x=-1 at an 8px pitch) so adjacent cells fuse into a continuous
				// rule. Compositing over the cell's own bg would repaint the neighbour's
				// pixels with this cell's colour and chop the join into dashes.
				int i = (py * imgW + px) * 3;
				rgb[i] = Blend(rgb[i], fg.R, a);
				rgb[i + 1] = Blend(rgb[i + 1], fg.G, a);
				rgb[i + 2] = Blend(rgb[i + 2], fg.B, a);
			}
		}
	}

	private static byte Blend(byte from, byte to, byte alpha) =>
		(byte)((from * (255 - alpha) + to * alpha) / 255);

	private Glyph Rasterise(int codepoint)
	{
		unsafe
		{
			fixed (byte* p = _ttf)
			{
				// Re-point the font at the pinned buffer: stbtt stores a raw pointer, and
				// the managed array may have moved since Load pinned it.
				_font.data = p;

				int w, h, xoff, yoff;
				byte* bmp = StbTrueType.stbtt_GetCodepointBitmap(
					_font, _scaleX, _scaleY, codepoint, &w, &h, &xoff, &yoff);

				if (bmp == null || w <= 0 || h <= 0)
					return new Glyph(Array.Empty<byte>(), 0, 0, 0, 0);

				var alpha = new byte[w * h];
				new ReadOnlySpan<byte>(bmp, w * h).CopyTo(alpha);
				StbTrueType.stbtt_FreeBitmap(bmp, null);

				return new Glyph(alpha, w, h, xoff, yoff);
			}
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_cache.Clear();
	}
}
