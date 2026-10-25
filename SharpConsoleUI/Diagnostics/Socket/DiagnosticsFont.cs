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
		// Linux - Debian/Ubuntu layout
		"/usr/share/fonts/UbuntuMonoNerd/UbuntuMonoNerdFontMono-Regular.ttf",
		"/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
		"/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
		// Linux - Fedora/RHEL layout
		"/usr/share/fonts/dejavu-sans-mono-fonts/DejaVuSansMono.ttf",
		"/usr/share/fonts/liberation-mono/LiberationMono-Regular.ttf",
		"/usr/share/fonts/liberation/LiberationMono-Regular.ttf",
		// Linux - Arch/Alpine layout
		"/usr/share/fonts/TTF/DejaVuSansMono.ttf",
		"/usr/share/fonts/truetype/DejaVuSansMono.ttf",
		"/usr/share/fonts/noto/NotoSansMono-Regular.ttf",
		// macOS
		"/System/Library/Fonts/Menlo.ttc",
		"/Library/Fonts/Menlo.ttc",
		"/System/Library/Fonts/SFNSMono.ttf",
		// Windows
		"C:\\Windows\\Fonts\\consola.ttf",
		"C:\\Windows\\Fonts\\lucon.ttf",
		"C:\\Windows\\Fonts\\cour.ttf",
	};

	/// <summary>Fonts to try for a codepoint the primary face does not cover.</summary>
	/// <remarks>
	/// A Latin monospace face has no Han glyphs, so a CJK screen rendered with DejaVu comes
	/// out as a row of boxes - in a library whose wide-character handling is a selling point.
	/// Rather than draw a box and say nothing, fall back to whichever CJK face the system
	/// has. These are proportional faces used only for the glyphs the primary lacks, so the
	/// cell grid still comes from the monospace advance.
	/// </remarks>
	private static readonly string[] FallbackFonts =
	{
		// TrueType-outline faces first. stb_truetype reads 'glyf' outlines only, so a
		// CFF/PostScript face - every Noto CJK .ttc is 'OTTO' inside - loads and then
		// rasterises nothing. Listing one first would silently shadow a face that works.
		"/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf",
		"/usr/share/fonts/truetype/wqy/wqy-microhei.ttc",
		"/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc",
		"/usr/share/fonts/truetype/unfonts-core/UnDotum.ttf",
		"/usr/share/fonts/truetype/arphic/uming.ttc",
		"/System/Library/Fonts/PingFang.ttc",
		"/System/Library/Fonts/AppleSDGothicNeo.ttc",
		"/System/Library/Fonts/Hiragino Sans GB.ttc",
		"C:\\Windows\\Fonts\\msyh.ttc",
		"C:\\Windows\\Fonts\\malgun.ttf",
		"C:\\Windows\\Fonts\\simsun.ttc",
	};

	private readonly byte[] _ttf;
	private readonly StbTrueType.stbtt_fontinfo _font;
	private readonly Dictionary<int, Glyph> _cache = new();
	private readonly float _scaleX;
	private readonly float _scaleY;
	private readonly int _baseline;
	private bool _disposed;

	/// <summary>Faces consulted, in order, for codepoints the primary does not cover.</summary>
	/// <remarks>
	/// A chain rather than a single face: no one font covers every script. Droid Sans
	/// Fallback has Han but no Hangul, so a Chinese screen and a Korean one need different
	/// faces, and a mixed screen needs both.
	/// </remarks>
	private List<FallbackFace>? _fallbacks;
	private bool _fallbackTried;

	/// <summary>Whether installed fonts may be consulted for uncovered codepoints.</summary>
	private bool _allowSystemFallbacks = true;

	/// <summary>A secondary face, scaled and baseline-aligned to the primary.</summary>
	private sealed class FallbackFace
	{
		internal required byte[] Ttf { get; init; }

		internal required StbTrueType.stbtt_fontinfo Font { get; init; }

		internal required string Path { get; init; }

		internal required float ScaleX { get; init; }

		internal required float ScaleY { get; init; }

		internal required int Baseline { get; init; }
	}

	/// <summary>Codepoints no available face could draw.</summary>
	private readonly HashSet<int> _missing = new();

	/// <summary>Codepoints that no loaded font could render, in encounter order.</summary>
	/// <remarks>
	/// Reported rather than silently drawn as a box, so a caller learns the image contains
	/// substitutions instead of discovering it by eye.
	/// </remarks>
	internal IReadOnlyCollection<int> MissingCodepoints => _missing;

	/// <summary>The fallback faces actually used to draw something.</summary>
	internal IReadOnlyCollection<string> FallbacksUsed => _fallbacksUsed;

	private readonly HashSet<string> _fallbacksUsed = new();

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

	/// <summary>The embedded face's resource name, and the label it reports as.</summary>
	internal const string EmbeddedResourceName = "SharpConsoleUI.Resources.Fonts.DejaVuSansMono.ttf";

	/// <summary>What <see cref="Path"/> reads as when the embedded face was used.</summary>
	internal const string EmbeddedLabel = "(embedded) DejaVu Sans Mono";

	/// <summary>
	/// Loads a font, sized so a cell is <paramref name="cellHeight"/> pixels tall.
	/// </summary>
	/// <param name="explicitPath">A caller-supplied .ttf, or null.</param>
	/// <param name="cellHeight">Target cell height in pixels.</param>
	/// <param name="cellWidth">Target cell width, or 0 to take the font's advance.</param>
	/// <param name="error">Why no font could be loaded.</param>
	/// <param name="source">Where to look. Defaults to installed fonts, then the embedded face.</param>
	/// <returns>The font, or null.</returns>
	/// <remarks>
	/// <see cref="Surfaces.FontSource.Strict"/> and <see cref="Surfaces.FontSource.File"/>
	/// can fail; the others end at the always-present embedded face rather than at a blank
	/// image.
	/// </remarks>
	internal static DiagnosticsFont? Load(string? explicitPath, int cellHeight, int cellWidth,
		out string? error, Surfaces.FontSource source = Surfaces.FontSource.Automatic)
	{
		error = null;

		byte[]? ttf;
		string path;

		// An explicit path means File, whether or not the caller said so.
		if (!string.IsNullOrWhiteSpace(explicitPath))
			source = Surfaces.FontSource.File;

		switch (source)
		{
			case Surfaces.FontSource.File:
				// An explicit choice that was quietly ignored would make the image a lie
				// about what drew it, so this is the one mode that fails rather than falls back.
				if (!File.Exists(explicitPath))
				{
					error = $"font not found: {explicitPath}";
					return null;
				}

				path = explicitPath!;
				try { ttf = File.ReadAllBytes(path); }
				catch (Exception ex) { error = $"cannot read font {path}: {ex.Message}"; return null; }
				break;

			case Surfaces.FontSource.Embedded:
				ttf = LoadEmbedded();
				if (ttf == null)
				{
					error = "the embedded font resource is missing from this assembly";
					return null;
				}

				path = EmbeddedLabel;
				break;

			case Surfaces.FontSource.Strict:
				{
					// No safety net by request: the caller wants to see what this machine can
					// actually render, gaps included.
					string? installed = TryReadFirstInstalled(out byte[]? bytes);
					if (installed == null)
					{
						error = "no installed monospace font found. Use FontSource.Automatic to "
							  + "fall back to the embedded face, name one with FontPath, or set "
							  + "Glyphs to false for a colour-only image";
						return null;
					}

					ttf = bytes;
					path = installed;
					break;
				}

			case Surfaces.FontSource.Automatic:
			default:
				{
					// Prefer what this machine has, so the picture matches the developer's own
					// terminal - then fall back, because a container with no fonts installed
					// would otherwise render an image with no text at all, and a blank
					// screenshot still hashes consistently enough to pass a check it should fail.
					string? installed = TryReadFirstInstalled(out byte[]? bytes);
					if (installed != null)
					{
						ttf = bytes;
						path = installed;
						break;
					}

					ttf = LoadEmbedded();
					if (ttf == null)
					{
						error = "no installed font found and the embedded font resource is missing";
						return null;
					}

					path = EmbeddedLabel;
					break;
				}
		}

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
		return new DiagnosticsFont(path, ttf, font, cw, cellHeight, scaleX, scaleY, baseline)
		{
			// Embedded means EMBEDDED: consulting installed fonts for the glyphs it lacks
			// would reintroduce the machine dependence this mode exists to remove.
			_allowSystemFallbacks = source != Surfaces.FontSource.Embedded,
		};
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
			bool covered;
			fixed (byte* p = _ttf)
			{
				// Re-point the font at the pinned buffer: stbtt stores a raw pointer, and
				// the managed array may have moved since Load pinned it.
				_font.data = p;

				// Glyph index 0 is the "not in this font" slot - the box. Asking first
				// means a missing glyph can fall back rather than being drawn as a box.
				covered = StbTrueType.stbtt_FindGlyphIndex(_font, codepoint) != 0;

				if (covered)
				{
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

			return RasteriseFallback(codepoint);
		}
	}

	/// <summary>
	/// Draws a codepoint the primary face lacks, from a secondary face.
	/// </summary>
	/// <remarks>
	/// A Latin monospace font has no Han glyphs. Without this, a CJK screen renders as a row
	/// of boxes, which would be a poor advertisement for a library that works hard on
	/// wide-character handling. The fallback is loaded lazily - most screens never need it.
	/// </remarks>
	private Glyph RasteriseFallback(int codepoint)
	{
		if (!_fallbackTried)
		{
			_fallbackTried = true;
			if (_allowSystemFallbacks) LoadFallbacks();
		}

		if (_fallbacks != null)
		{
			foreach (var face in _fallbacks)
			{
				var glyph = TryRasteriseFrom(face, codepoint);
				if (glyph != null)
				{
					_fallbacksUsed.Add(face.Path);
					return glyph.Value;
				}
			}
		}

		// Nothing on this system can draw it. Record it so the caller is told, rather than
		// leaving a blank cell to pass for a rendering.
		_missing.Add(codepoint);
		return new Glyph(Array.Empty<byte>(), 0, 0, 0, 0);
	}

	/// <summary>Reads the first installed candidate that exists and can be read.</summary>
	/// <param name="bytes">The font file's contents, when one was read.</param>
	/// <returns>Its path, or null when none could be read.</returns>
	private static string? TryReadFirstInstalled(out byte[]? bytes)
	{
		foreach (string candidate in CandidateFonts)
		{
			if (!File.Exists(candidate)) continue;

			try
			{
				bytes = File.ReadAllBytes(candidate);
				return candidate;
			}
			catch
			{
				// Exists but unreadable - keep looking rather than giving up here.
			}
		}

		bytes = null;
		return null;
	}

	/// <summary>Reads the face embedded in this assembly, or null if it is absent.</summary>
	private static byte[]? LoadEmbedded()
	{
		try
		{
			using var stream = typeof(DiagnosticsFont).Assembly
				.GetManifestResourceStream(EmbeddedResourceName);
			if (stream == null) return null;

			using var ms = new MemoryStream();
			stream.CopyTo(ms);
			return ms.ToArray();
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Whether a font file carries CFF/PostScript outlines rather than TrueType ones.
	/// </summary>
	/// <remarks>
	/// Checks the sfnt version: 'OTTO' means CFF. For a collection ('ttcf') the first face's
	/// tag is read, which is representative - a collection does not mix outline formats.
	/// </remarks>
	private static bool IsPostScriptOutlines(byte[] data)
	{
		if (data.Length < 16) return false;

		if (data[0] == 'O' && data[1] == 'T' && data[2] == 'T' && data[3] == 'O')
			return true;

		if (data[0] != 't' || data[1] != 't' || data[2] != 'c' || data[3] != 'f')
			return false;

		// Collection: follow the offset table to the first face and read its tag.
		uint first = ((uint)data[12] << 24) | ((uint)data[13] << 16)
			| ((uint)data[14] << 8) | data[15];

		return first + 4 <= data.Length
			&& data[first] == 'O' && data[first + 1] == 'T'
			&& data[first + 2] == 'T' && data[first + 3] == 'O';
	}

	/// <summary>Rasterises from one fallback face, or null if it lacks the codepoint.</summary>
	private Glyph? TryRasteriseFrom(FallbackFace face, int codepoint)
	{
		unsafe
		{
			fixed (byte* p = face.Ttf)
			{
				face.Font.data = p;

				if (StbTrueType.stbtt_FindGlyphIndex(face.Font, codepoint) == 0)
					return null;

				int w, h, xoff, yoff;
				byte* bmp = StbTrueType.stbtt_GetCodepointBitmap(
					face.Font, face.ScaleX, face.ScaleY, codepoint, &w, &h, &xoff, &yoff);

				if (bmp == null || w <= 0 || h <= 0)
					return new Glyph(Array.Empty<byte>(), 0, 0, 0, 0);

				var alpha = new byte[w * h];
				new ReadOnlySpan<byte>(bmp, w * h).CopyTo(alpha);
				StbTrueType.stbtt_FreeBitmap(bmp, null);

				// Shift so the fallback sits on the SAME baseline as the primary: the two
				// faces have different vertical metrics, and without this a CJK glyph
				// floats above or below the Latin text beside it.
				return new Glyph(alpha, w, h, xoff, yoff + (face.Baseline - _baseline));
			}
		}
	}

	/// <summary>
	/// Loads every fallback face present on the system, in preference order.
	/// </summary>
	/// <remarks>
	/// All of them, not the first match: no single face covers every script, so a screen
	/// mixing Chinese and Korean needs more than one. Loading is lazy and happens once.
	/// </remarks>
	private void LoadFallbacks()
	{
		foreach (string path in FallbackFonts)
		{
			if (!File.Exists(path)) continue;

			byte[] ttf;
			try { ttf = File.ReadAllBytes(path); }
			catch { continue; }

			// stb_truetype rasterises 'glyf' outlines only. A CFF/PostScript face - tagged
			// 'OTTO' - initialises happily and then draws nothing, which would look like a
			// missing glyph rather than an unsupported font. Skip it so a usable face later
			// in the chain gets the chance.
			if (IsPostScriptOutlines(ttf)) continue;

			var font = new StbTrueType.stbtt_fontinfo();
			float scaleY;
			int baseline;

			unsafe
			{
				fixed (byte* p = ttf)
				{
					if (StbTrueType.stbtt_InitFont(font, p, 0) == 0) continue;

					// Match the primary's pixel height. A CJK face is typically designed on
					// a square em, so matching the height keeps it inside the cell.
					scaleY = StbTrueType.stbtt_ScaleForPixelHeight(font, CellHeight);

					int asc, desc, gap;
					StbTrueType.stbtt_GetFontVMetrics(font, &asc, &desc, &gap);
					baseline = (int)Math.Round(asc * scaleY);
				}
			}

			(_fallbacks ??= new List<FallbackFace>()).Add(new FallbackFace
			{
				Ttf = ttf,
				Font = font,
				Path = path,
				ScaleX = scaleY,
				ScaleY = scaleY,
				Baseline = baseline,
			});
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_cache.Clear();
	}
}
