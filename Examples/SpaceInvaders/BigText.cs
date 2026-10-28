// -----------------------------------------------------------------------
// SpaceInvaders — big pixel lettering.
//
// Reuses the library's FIGlet engine (FigletFont / FigletRenderer and the
// fonts embedded in SharpConsoleUI) but rasterises the result into a pixel
// PixelMask instead of printing the ASCII art. Every FIGlet character becomes a
// column of two pixels: '_' and '.' light only the lower pixel, '\'' and '`'
// only the upper one, everything else both. The outline fonts then read as
// chunky solid lettering that can be gradient-lit and animated per pixel.
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;

using SharpConsoleUI.Drawing;

namespace SpaceInvaders;

internal static class BigText
{
	private static readonly Dictionary<string, FigletFont> Fonts = new();
	private static readonly Dictionary<(string, string), PixelMask> Cache = new();

	/// <summary>Renders <paramref name="text"/> with an embedded FIGlet font as a pixel sprite.</summary>
	public static PixelMask Render(string text, string font = "standard")
	{
		if (Cache.TryGetValue((text, font), out var cached)) return cached;

		var lines = FigletRenderer.Render(text, LoadFont(font));
		while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);

		int width = lines.Count == 0 ? 0 : lines.Max(l => l.TrimEnd().Length);
		var rows = new string[lines.Count * 2];
		for (int i = 0; i < lines.Count; i++)
		{
			var top = new char[width];
			var bottom = new char[width];
			for (int x = 0; x < width; x++)
			{
				char ch = x < lines[i].Length ? lines[i][x] : ' ';
				(bool up, bool down) = ch switch
				{
					' ' => (false, false),
					'_' or '.' or ',' => (false, true),
					'\'' or '`' or '^' or '"' or '~' => (true, false),
					_ => (true, true),
				};
				top[x] = up ? '#' : '.';
				bottom[x] = down ? '#' : '.';
			}
			rows[i * 2] = new string(top);
			rows[i * 2 + 1] = new string(bottom);
		}

		var sprite = new PixelMask(rows.Length == 0 ? new[] { "." } : rows);
		Cache[(text, font)] = sprite;
		return sprite;
	}

	private static FigletFont LoadFont(string name)
	{
		if (Fonts.TryGetValue(name, out var font)) return font;

		// The fonts ship as embedded resources of the SharpConsoleUI assembly.
		var assembly = typeof(FigletControl).Assembly;
		using var stream = assembly.GetManifestResourceStream($"SharpConsoleUI.Resources.Fonts.{name}.flf")
			?? throw new InvalidOperationException($"FIGlet font '{name}' is not embedded in SharpConsoleUI.");
		font = FigletFont.Load(stream);
		Fonts[name] = font;
		return font;
	}
}
