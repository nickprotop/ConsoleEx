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
/// Renders a rectangle of cells to an image: a block of colour per cell, and glyphs on top.
/// </summary>
/// <remarks>
/// <para>
/// Glyphs are drawn through a real TrueType face rather than a built-in ASCII atlas, which is
/// what makes the output honest about the things this library works hardest to get right: CJK
/// and other wide characters, combining marks, and Nerd Font glyphs. A face that cannot draw a
/// codepoint is reported rather than left to pass as a row of boxes. Passing no font gives a
/// pure colour map, which still carries layout, borders, focus, theme and window stacking -
/// and that is what a layout bug looks like.
/// </para>
/// <para>
/// The per-window source has real typed colours. The composed source does not: it stores the
/// escape the renderer emitted, and that escape may say <c>39</c>/<c>49</c>, meaning "whatever
/// the terminal's default is" - a colour this process genuinely does not know. Those cells are
/// filled from the theme and the substitution is reported, rather than silently invented.
/// </para>
/// </remarks>
internal static class DiagnosticsImage
{
	/// <summary>Smallest cell that still shows structure; below this a border vanishes.</summary>
	private const int MinCellSize = 1;

	/// <summary>
	/// Guards against a request that would allocate an absurd buffer. A 400x120 terminal at
	/// 16x32 is already ~24 MPx; beyond this the caller wants a smaller cell, not more pixels.
	/// </summary>
	private const int MaxPixels = 40_000_000;

	/// <summary>Default JPEG quality: high enough that glyph edges stay legible.</summary>
	private const int DefaultJpegQuality = 90;

	/// <summary>Lowest JPEG quality the encoder accepts.</summary>
	private const int MinJpegQuality = 1;

	/// <summary>Highest JPEG quality the encoder accepts.</summary>
	private const int MaxJpegQuality = 100;


	/// <summary>
	/// Renders cells to encoded image bytes.
	/// </summary>
	/// <param name="surface"></param>
	/// <param name="cellWidth">Pixels per cell horizontally.</param>
	/// <param name="cellHeight">Pixels per cell vertically.</param>
	/// <param name="font"></param>
	/// <param name="error">Why nothing was produced.</param>
	/// <param name="format">Which encoding to produce.</param>
	/// <param name="jpegQuality">Quality 1-100, used only by <see cref="Surfaces.ImageFormat.Jpeg"/>.</param>
	/// <returns>The encoded bytes, or null.</returns>
	internal static byte[]? Render(
		Surfaces.ICellSurface surface, int cellWidth, int cellHeight,
		DiagnosticsFont? font, out string? error,
		Surfaces.ImageFormat format = Surfaces.ImageFormat.Png, int jpegQuality = DefaultJpegQuality)
	{
		error = null;

		if (jpegQuality is < MinJpegQuality or > MaxJpegQuality)
		{
			error = $"jpeg quality must be {MinJpegQuality}-{MaxJpegQuality}; got {jpegQuality}";
			return null;
		}
		int width = surface.Width, height = surface.Height;

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
		var cells = new Surfaces.SurfaceCell[width * height];

		for (int cy = 0; cy < height; cy++)
		{
			for (int cx = 0; cx < width; cx++)
			{
				var cell = surface[cx, cy];
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

					// A wide glyph is drawn from its base cell and overhangs into this one;
					// drawing it again here would double it.
					if (cell.IsWideContinuation) continue;

					font.DrawRune(rgb, imgW, imgH,
						cx * cellWidth, cy * cellHeight,
						cell.Character, cell.Foreground);

					// Combining marks share the base glyph's cell - that is what makes them
					// combining. Drawing them at the same origin puts the accent over the
					// letter, as a terminal would; omitting them would quietly render "é"
					// as "e" in the one export that claims to show what the screen looks
					// like.
					if (cell.Combiners == null) continue;
					foreach (var mark in cell.Combiners.EnumerateRunes())
					{
						font.DrawRune(rgb, imgW, imgH,
							cx * cellWidth, cy * cellHeight,
							mark, cell.Foreground);
					}
				}
			}
		}

		using var ms = new MemoryStream();
		var writer = new ImageWriter();

		// Only the encoding differs; everything above is format-agnostic.
		switch (format)
		{
			case Surfaces.ImageFormat.Tga:
				writer.WriteTga(rgb, imgW, imgH, ColorComponents.RedGreenBlue, ms);
				break;

			case Surfaces.ImageFormat.Jpeg:
				writer.WriteJpg(rgb, imgW, imgH, ColorComponents.RedGreenBlue, ms, jpegQuality);
				break;

			default:
				writer.WritePng(rgb, imgW, imgH, ColorComponents.RedGreenBlue, ms);
				break;
		}

		return ms.ToArray();
	}

	/// <summary>The wire and file name for a format.</summary>
	internal static string NameOf(Surfaces.ImageFormat format) => format switch
	{
		Surfaces.ImageFormat.Tga => "tga",
		Surfaces.ImageFormat.Jpeg => "jpeg",
		_ => "png",
	};

	/// <summary>Builds the reply, attaching the bytes or writing them out.</summary>
	/// <remarks>
	/// Bytes are the default because the reply is then self-contained: it needs no second
	/// step, leaves nothing behind, and crosses a container or user boundary that a path does
	/// not. A path is the explicit opt-in, since writing a file is the one thing the consent
	/// prompt does not already cover.
	/// </remarks>
	internal static JsonObject Reply(byte[] png, int imgW, int imgH, string? path,
		Surfaces.ImageFormat format = Surfaces.ImageFormat.Png)
	{
		var o = new JsonObject
		{
			["w"] = imgW,
			["h"] = imgH,
			["format"] = NameOf(format),
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
