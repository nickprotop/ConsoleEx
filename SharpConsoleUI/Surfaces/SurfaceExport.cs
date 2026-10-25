// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Surfaces;

/// <summary>How <see cref="SurfaceExport.ToText"/> renders a surface.</summary>
public enum TextFormat
{
	/// <summary>Characters only. For golden files, diffs and grepping a layout.</summary>
	Plain,

	/// <summary>SGR escapes, coalesced into runs. Pipe it to a terminal to see the real thing.</summary>
	Ansi,

	/// <summary>The library's own markup, which parses back into cells.</summary>
	Markup,
}

/// <summary>Controls how a surface is rendered to an image.</summary>
public sealed record ExportOptions
{
	/// <summary>Pixels per cell vertically.</summary>
	public int CellHeight { get; init; } = 16;

	/// <summary>Pixels per cell horizontally, or null to take the font's advance.</summary>
	public int? CellWidth { get; init; }

	/// <summary>Whether to draw text. False gives a colour map and needs no font.</summary>
	public bool Glyphs { get; init; } = true;

	/// <summary>A monospace .ttf to use, or null to follow <see cref="FontSource"/>.</summary>
	/// <remarks>Setting this implies <see cref="Surfaces.FontSource.File"/>.</remarks>
	public string? FontPath { get; init; }

	/// <summary>Where to look for the font. Defaults to installed fonts, then the embedded face.</summary>
	/// <remarks>
	/// <see cref="Surfaces.FontSource.Embedded"/> is the one to ask for when the output must
	/// be reproducible - golden-image testing, or any comparison across machines.
	/// </remarks>
	public FontSource FontSource { get; init; } = FontSource.Automatic;

	/// <summary>Which encoding <see cref="SurfaceExport.ToImage"/> produces.</summary>
	public ImageFormat Format { get; init; } = ImageFormat.Png;

	/// <summary>JPEG quality, 1-100. Ignored unless <see cref="Format"/> is JPEG.</summary>
	public int JpegQuality { get; init; } = 90;
}

/// <summary>
/// Turns a surface into something outside the application: an image, or text.
/// </summary>
/// <remarks>
/// Every exporter takes <see cref="ICellSurface"/> and nothing else, so each is written once
/// and works for a window, a snapshot or the composed desktop alike.
/// </remarks>
public static class SurfaceExport
{
	/// <summary>
	/// A view of part of a surface.
	/// </summary>
	/// <param name="surface">The surface to crop.</param>
	/// <param name="region">The rectangle to keep, in surface coordinates.</param>
	/// <returns>A view, clamped to what actually exists.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The region has a non-positive extent, or lies entirely outside the surface.
	/// </exception>
	/// <remarks>
	/// Clamps rather than throwing when the region only partly overlaps: a partly-offscreen
	/// request has a useful answer, and the result reports its own clamped size. A region
	/// entirely outside throws, because silently returning an empty surface would read as
	/// "that area is blank", which is a different answer.
	/// </remarks>
	public static ICellSurface Crop(this ICellSurface surface, LayoutRect region)
	{
		ArgumentNullException.ThrowIfNull(surface);

		if (region.Width <= 0 || region.Height <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(region),
				$"region must have a positive extent; got {region.Width}x{region.Height}");
		}

		int x = Math.Max(0, region.X);
		int y = Math.Max(0, region.Y);
		int right = Math.Min(surface.Width, region.X + region.Width);
		int bottom = Math.Min(surface.Height, region.Y + region.Height);

		if (right <= x || bottom <= y)
		{
			throw new ArgumentOutOfRangeException(nameof(region),
				$"region {region.X},{region.Y} {region.Width}x{region.Height} lies entirely "
				+ $"outside the {surface.Width}x{surface.Height} surface");
		}

		return new CroppedSurface(surface, x, y, right - x, bottom - y);
	}

	/// <summary>
	/// Renders a surface to text.
	/// </summary>
	/// <param name="surface">The surface.</param>
	/// <param name="format">Which text form to produce.</param>
	/// <returns>One line per row, separated by newlines.</returns>
	public static string ToText(this ICellSurface surface, TextFormat format = TextFormat.Plain)
	{
		ArgumentNullException.ThrowIfNull(surface);

		var sb = new StringBuilder();
		for (int y = 0; y < surface.Height; y++)
		{
			if (y > 0) sb.Append('\n');
			AppendRow(sb, surface, y, format);
		}
		return sb.ToString();
	}

	/// <summary>
	/// Renders a surface to an image.
	/// </summary>
	/// <param name="surface">The surface.</param>
	/// <param name="options">
	/// Format, cell size, glyphs and font, or null for the defaults - which are PNG at 16
	/// pixels per cell with glyphs drawn.
	/// </param>
	/// <returns>The encoded bytes.</returns>
	/// <exception cref="InvalidOperationException">The image could not be produced.</exception>
	/// <remarks>
	/// <para>
	/// Returns bytes rather than writing a file: the caller decides where it goes, and bytes
	/// serve an HTTP response, a test assertion or a clipboard where a path does not.
	/// <c>File.WriteAllBytes(path, surface.ToImage())</c> is the one-line file case.
	/// </para>
	/// <para>
	/// <see cref="ImageFormat"/> documents what each encoding costs. The short version: keep
	/// PNG unless a frame is being streamed and discarded, in which case
	/// <see cref="ImageFormat.Tga"/> encodes in a fraction of the time.
	/// </para>
	/// </remarks>
	public static byte[] ToImage(this ICellSurface surface, ExportOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(surface);
		options ??= new ExportOptions();

		int cellHeight = options.CellHeight;
		int cellWidth = options.CellWidth ?? 0;

		Diagnostics.Socket.DiagnosticsFont? font = null;
		if (options.Glyphs)
		{
			font = Diagnostics.Socket.DiagnosticsFont.Load(
				options.FontPath, cellHeight, cellWidth, out string? fontError,
				options.FontSource);

			// The embedded face is always available, so a null here means the caller named
			// a font that could not be loaded - an explicit choice, and an error rather
			// than a silent substitution.
			if (font == null)
				throw new InvalidOperationException(fontError);
		}

		if (font != null) cellWidth = font.CellWidth;
		if (cellWidth <= 0) cellWidth = 8;

		try
		{
			var bytes = Diagnostics.Socket.DiagnosticsImage.Render(
				surface, cellWidth, cellHeight, font, out string? renderError,
				options.Format, options.JpegQuality);

			return bytes ?? throw new InvalidOperationException(renderError);
		}
		finally
		{
			font?.Dispose();
		}
	}

	#region Text rendering

	private static void AppendRow(StringBuilder sb, ICellSurface surface, int y, TextFormat format)
	{
		switch (format)
		{
			case TextFormat.Plain:
				AppendPlainRow(sb, surface, y);
				break;
			case TextFormat.Ansi:
				AppendAnsiRow(sb, surface, y);
				break;
			case TextFormat.Markup:
				AppendMarkupRow(sb, surface, y);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(format), format, "unknown text format");
		}
	}

	/// <summary>Characters only, with trailing blanks trimmed so diffs stay readable.</summary>
	private static void AppendPlainRow(StringBuilder sb, ICellSurface surface, int y)
	{
		int start = sb.Length;

		for (int x = 0; x < surface.Width; x++)
		{
			var c = surface[x, y];
			if (c.IsWideContinuation) continue;   // the glyph belongs to the previous cell
			sb.Append(c.Character);
			if (c.Combiners != null) sb.Append(c.Combiners);
		}

		// Trim trailing whitespace: in a plain dump the background is invisible, so trailing
		// blanks are noise that makes every diff wider than the change.
		while (sb.Length > start && sb[sb.Length - 1] == ' ') sb.Length--;
	}

	/// <summary>
	/// SGR escapes, emitted only where the style changes.
	/// </summary>
	/// <remarks>
	/// Coalescing matters: a 200-column row of unchanged colour costs one escape here rather
	/// than two hundred.
	/// </remarks>
	private static void AppendAnsiRow(StringBuilder sb, ICellSurface surface, int y)
	{
		bool styleOpen = false;
		Color curFg = default, curBg = default;
		var curDec = TextDecoration.None;
		bool curDefaulted = false;
		bool first = true;

		for (int x = 0; x < surface.Width; x++)
		{
			var c = surface[x, y];
			if (c.IsWideContinuation) continue;

			bool changed = first
				|| c.Foreground != curFg
				|| c.Background != curBg
				|| c.Decorations != curDec
				|| c.BackgroundWasDefaulted != curDefaulted;

			if (changed)
			{
				sb.Append("\u001b[0m");
				AppendSgr(sb, c);
				curFg = c.Foreground;
				curBg = c.Background;
				curDec = c.Decorations;
				curDefaulted = c.BackgroundWasDefaulted;
				styleOpen = true;
				first = false;
			}

			sb.Append(c.Character);
			if (c.Combiners != null) sb.Append(c.Combiners);
		}

		if (styleOpen) sb.Append("\u001b[0m");
	}

	private static void AppendSgr(StringBuilder sb, SurfaceCell c)
	{
		sb.Append("\u001b[");
		sb.Append("38;2;").Append(c.Foreground.R).Append(';')
			.Append(c.Foreground.G).Append(';').Append(c.Foreground.B);

		// A background the source never recorded is emitted as 49 - "the terminal's own
		// default" - rather than as the substituted theme colour. Round-tripping the original
		// intent renders correctly in a real terminal, where baking in a guess does not.
		if (c.BackgroundWasDefaulted)
		{
			sb.Append(";49");
		}
		else
		{
			sb.Append(";48;2;").Append(c.Background.R).Append(';')
				.Append(c.Background.G).Append(';').Append(c.Background.B);
		}

		AppendDecorationCodes(sb, c.Decorations);
		sb.Append('m');
	}

	private static void AppendDecorationCodes(StringBuilder sb, TextDecoration dec)
	{
		if ((dec & TextDecoration.Bold) != 0) sb.Append(";1");
		if ((dec & TextDecoration.Dim) != 0) sb.Append(";2");
		if ((dec & TextDecoration.Italic) != 0) sb.Append(";3");
		if ((dec & TextDecoration.Underline) != 0) sb.Append(";4");
		if ((dec & TextDecoration.Blink) != 0) sb.Append(";5");
		if ((dec & TextDecoration.Invert) != 0) sb.Append(";7");
		if ((dec & TextDecoration.Strikethrough) != 0) sb.Append(";9");
	}

	/// <summary>
	/// The library's own markup, which parses back into cells.
	/// </summary>
	/// <remarks>
	/// Uses the same run-coalescing writer as the soft-wrap path, so the two cannot drift
	/// apart on escaping, wide glyphs or combining marks. <c>alwaysTag</c> is true here: an
	/// exported screenshot has no ambient default style to inherit, so every run states its
	/// own colours.
	/// </remarks>
	private static void AppendMarkupRow(StringBuilder sb, ICellSurface surface, int y)
	{
		var writer = new Parsing.MarkupParser.MarkupRunWriter(
			sb, Color.White, Color.Black, alwaysTag: true);

		for (int x = 0; x < surface.Width; x++)
		{
			var c = surface[x, y];
			writer.Append(c.Character, c.Foreground, c.Background, c.Decorations,
				c.IsWideContinuation, c.Combiners);
		}

		writer.Close();
	}


	#endregion

	private sealed class CroppedSurface(ICellSurface inner, int originX, int originY, int width, int height)
		: ICellSurface
	{
		public int Width => width;

		public int Height => height;

		public SurfaceCell this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= height)
					throw new ArgumentOutOfRangeException($"({x}, {y}) is outside the {width}x{height} surface");

				return inner[originX + x, originY + y];
			}
		}
	}
}
