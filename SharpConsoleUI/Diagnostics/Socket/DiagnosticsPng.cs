// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;
using StbImageWriteSharp;

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// Renders a rectangle of cells to a PNG, one solid block of colour per cell.
/// </summary>
/// <remarks>
/// <para>
/// GLYPHS ARE NOT DRAWN, deliberately. Drawing text would mean shipping a bitmap font, and a
/// built-in ASCII atlas would misrender exactly the things this library works hardest to get
/// right: CJK and other wide characters, combining marks, and Nerd Font glyphs. A screenshot
/// that quietly draws those wrongly is worse than one that never claimed to draw text. What
/// this produces is an honest colour map - layout, borders, focus, theme and window stacking
/// are all faithful, and that is what a layout bug looks like.
/// </para>
/// <para>
/// The per-window source has real typed colours. The composed source does not: it stores the
/// escape the renderer emitted, and that escape may say <c>39</c>/<c>49</c>, meaning "whatever
/// the terminal's default is" - a colour this process genuinely does not know. Those cells are
/// filled from the theme and the substitution is reported, rather than silently invented.
/// </para>
/// </remarks>
internal static class DiagnosticsPng
{
	/// <summary>Smallest cell that still shows structure; below this a border vanishes.</summary>
	private const int MinCellSize = 1;

	/// <summary>
	/// Guards against a request that would allocate an absurd buffer. A 400x120 terminal at
	/// 16x32 is already ~24 MPx; beyond this the caller wants a smaller cell, not more pixels.
	/// </summary>
	private const int MaxPixels = 40_000_000;

	/// <summary>A cell's colours, and whether they were known or substituted.</summary>
	/// <summary>A cell: what to draw, in what colours, and whether they were known.</summary>
	/// <param name="Background">The cell background.</param>
	/// <param name="DefaultedBackground">True when the background was substituted, not measured.</param>
	/// <param name="Glyph">The character, or null for a blank cell.</param>
	/// <param name="Foreground">The glyph colour.</param>
	internal readonly record struct CellColor(
		Color Background,
		bool DefaultedBackground,
		Rune? Glyph = null,
		Color Foreground = default);

	/// <summary>
	/// Renders cells to PNG bytes.
	/// </summary>
	/// <param name="width">Columns in the region.</param>
	/// <param name="height">Rows in the region.</param>
	/// <param name="cellWidth">Pixels per cell horizontally.</param>
	/// <param name="cellHeight">Pixels per cell vertically.</param>
	/// <param name="at">Supplies the colour of the cell at a region-relative position.</param>
	/// <param name="font"></param>
	/// <param name="error">Why nothing was produced.</param>
	/// <returns>The PNG bytes, or null.</returns>
	internal static byte[]? Render(
		int width, int height, int cellWidth, int cellHeight,
		Func<int, int, CellColor> at, DiagnosticsFont? font, out string? error)
	{
		error = null;

		if (cellWidth < MinCellSize || cellHeight < MinCellSize)
		{
			error = $"cell must be at least {MinCellSize}x{MinCellSize}; got {cellWidth}x{cellHeight}";
			return null;
		}

		long pxW = (long)width * cellWidth;
		long pxH = (long)height * cellHeight;
		if (pxW * pxH > MaxPixels)
		{
			error = $"{pxW}x{pxH} exceeds the {MaxPixels:N0} pixel limit; use a smaller cell or a region";
			return null;
		}

		int imgW = (int)pxW, imgH = (int)pxH;
		var rgb = new byte[imgW * imgH * 3];

		// Backgrounds first, then glyphs. A wide glyph overhangs into the next cell, so
		// painting both in one pass would let the neighbour's background erase it.
		var cells = new CellColor[width * height];

		for (int cy = 0; cy < height; cy++)
		{
			for (int cx = 0; cx < width; cx++)
			{
				var cell = at(cx, cy);
				cells[cy * width + cx] = cell;
				var bg = cell.Background;

				// Fill the cell's block. Written row by row so the inner loop stays
				// sequential in the destination buffer.
				for (int py = 0; py < cellHeight; py++)
				{
					int rowStart = ((cy * cellHeight + py) * imgW + cx * cellWidth) * 3;
					for (int px = 0; px < cellWidth; px++)
					{
						int i = rowStart + px * 3;
						rgb[i] = bg.R;
						rgb[i + 1] = bg.G;
						rgb[i + 2] = bg.B;
					}
				}
			}
		}

		if (font != null)
		{
			for (int cy = 0; cy < height; cy++)
			{
				for (int cx = 0; cx < width; cx++)
				{
					var cell = cells[cy * width + cx];
					if (cell.Glyph is not { } rune) continue;

					font.DrawRune(rgb, imgW, imgH,
						cx * cellWidth, cy * cellHeight,
						rune, cell.Foreground);
				}
			}
		}

		using var ms = new MemoryStream();
		new ImageWriter().WritePng(rgb, imgW, imgH, ColorComponents.RedGreenBlue, ms);
		return ms.ToArray();
	}

	/// <summary>
	/// Reads the background out of an emitted escape.
	/// </summary>
	/// <remarks>
	/// Only <c>48;2;r;g;b</c> carries a real colour. <c>49</c> means terminal-default, which
	/// is reported as defaulted so the caller learns the image contains a substitution rather
	/// than a measurement.
	/// </remarks>
	internal static CellColor ParseBackground(string? ansi, Color fallback)
	{
		if (string.IsNullOrEmpty(ansi))
			return new CellColor(fallback, true);

		// Scan for the 48;2;r;g;b run. Hand-parsed rather than regex: this runs per cell,
		// and the grammar emitted by FormatCellAnsi is fixed and tiny.
		int i = ansi.IndexOf("48;2;", StringComparison.Ordinal);
		if (i < 0)
			return new CellColor(fallback, true);

		i += 5;
		if (TryByte(ansi, ref i, out byte r) &&
			TryByte(ansi, ref i, out byte g) &&
			TryByte(ansi, ref i, out byte b))
		{
			return new CellColor(new Color(r, g, b), false);
		}

		return new CellColor(fallback, true);
	}

	/// <summary>
	/// Reads the foreground out of an emitted escape, falling back to the theme.
	/// </summary>
	/// <remarks>
	/// <c>39</c> means terminal-default, a colour this process does not know. Unlike the
	/// background - where a wrong guess repaints whole regions - a defaulted foreground only
	/// affects glyph ink, so the theme's window foreground is a safe stand-in.
	/// </remarks>
	internal static Color ParseForeground(string? ansi, Color fallback)
	{
		if (string.IsNullOrEmpty(ansi)) return fallback;

		int i = ansi.IndexOf("38;2;", StringComparison.Ordinal);
		if (i < 0) return fallback;

		i += 5;
		if (TryByte(ansi, ref i, out byte r) &&
			TryByte(ansi, ref i, out byte g) &&
			TryByte(ansi, ref i, out byte b))
		{
			return new Color(r, g, b);
		}

		return fallback;
	}

	/// <summary>Reads one decimal byte, then steps over a single separator.</summary>
	private static bool TryByte(string s, ref int i, out byte value)
	{
		value = 0;
		int start = i;
		int n = 0;

		while (i < s.Length && s[i] >= '0' && s[i] <= '9')
		{
			n = n * 10 + (s[i] - '0');
			if (n > 255) return false;
			i++;
		}

		if (i == start) return false;
		value = (byte)n;

		if (i < s.Length && (s[i] == ';' || s[i] == 'm')) i++;
		return true;
	}

	/// <summary>Builds the reply, attaching the bytes or writing them out.</summary>
	/// <remarks>
	/// Bytes are the default because the reply is then self-contained: it needs no second
	/// step, leaves nothing behind, and crosses a container or user boundary that a path does
	/// not. A path is the explicit opt-in, since writing a file is the one thing the consent
	/// prompt does not already cover.
	/// </remarks>
	internal static JsonObject Reply(byte[] png, int imgW, int imgH, string? path)
	{
		var o = new JsonObject
		{
			["w"] = imgW,
			["h"] = imgH,
			["format"] = "png",
			["bytes"] = png.Length,
		};

		if (string.IsNullOrWhiteSpace(path))
		{
			// base64URL, not plain base64. System.Text.Json escapes '+' as \u002B, which
			// inflates the reply and breaks a client that slices the field out by hand
			// rather than parsing it. The URL alphabet avoids every escaped character.
			o["encoding"] = "base64url";
			o["data"] = Convert.ToBase64String(png).Replace('+', '-').Replace('/', '_');
		}
		else
		{
			File.WriteAllBytes(path, png);
			o["path"] = Path.GetFullPath(path);
		}

		return o;
	}
}
