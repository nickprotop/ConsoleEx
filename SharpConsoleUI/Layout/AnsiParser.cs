// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Parses ANSI escape sequences and converts them to cells.
	/// Used to bridge Spectre.Console markup output to the character buffer.
	/// </summary>
	public static partial class AnsiParser
	{
		// ANSI escape sequence pattern: ESC [ (params) (command)
		[GeneratedRegex(@"\x1b\[([0-9;]*)([A-Za-z])", RegexOptions.Compiled)]
		private static partial Regex AnsiEscapeRegex();

		// Standard ANSI colors (0-7)
		private static readonly Color[] StandardColors =
		[
			Color.Black,       // 0
			Color.Maroon,      // 1 (Red)
			Color.Green,       // 2
			Color.Olive,       // 3 (Yellow)
			Color.Navy,        // 4 (Blue)
			Color.Purple,      // 5 (Magenta)
			Color.Teal,        // 6 (Cyan)
			Color.Silver       // 7 (White)
		];

		// Bright ANSI colors (8-15)
		private static readonly Color[] BrightColors =
		[
			Color.Grey,        // 8 (Bright Black)
			Color.Red,         // 9 (Bright Red)
			Color.Lime,        // 10 (Bright Green)
			Color.Yellow,      // 11 (Bright Yellow)
			Color.Blue,        // 12 (Bright Blue)
			Color.Fuchsia,     // 13 (Bright Magenta)
			Color.Aqua,        // 14 (Bright Cyan)
			Color.White        // 15 (Bright White)
		];

		/// <summary>
		/// Parses an ANSI-formatted string and yields cells with color information.
		/// </summary>
		public static IEnumerable<Cell> Parse(string ansiString, Color defaultForeground, Color defaultBackground)
		{
			if (string.IsNullOrEmpty(ansiString))
				yield break;

			var currentFg = defaultForeground;
			var currentBg = defaultBackground;
			var isBold = false;

			int i = 0;
			while (i < ansiString.Length)
			{
				// Check for escape sequence
				if (ansiString[i] == '\x1b' && i + 1 < ansiString.Length && ansiString[i + 1] == '[')
				{
					// Find the end of the escape sequence
					int start = i;
					i += 2; // Skip ESC[

					// Collect parameters
					var paramsBuilder = new StringBuilder();
					while (i < ansiString.Length && (char.IsDigit(ansiString[i]) || ansiString[i] == ';'))
					{
						paramsBuilder.Append(ansiString[i]);
						i++;
					}

					// Get the command character
					if (i < ansiString.Length)
					{
						char command = ansiString[i];
						i++;

						// Only process 'm' (SGR - Select Graphic Rendition)
						if (command == 'm')
						{
							var paramsStr = paramsBuilder.ToString();
							if (string.IsNullOrEmpty(paramsStr))
							{
								// ESC[m = reset
								currentFg = defaultForeground;
								currentBg = defaultBackground;
								isBold = false;
							}
							else
							{
								ProcessSgrParams(paramsStr, ref currentFg, ref currentBg, ref isBold, defaultForeground, defaultBackground);
							}
						}
					}
				}
				else
				{
					// Regular character
					yield return new Cell(ansiString[i], currentFg, currentBg);
					i++;
				}
			}
		}

		/// <summary>
		/// Parses an ANSI-formatted string using default colors.
		/// </summary>
		public static IEnumerable<Cell> Parse(string ansiString) =>
			Parse(ansiString, Color.White, Color.Black);

		/// <summary>
		/// Returns the visible length of an ANSI string (excluding escape sequences).
		/// </summary>
		public static int VisibleLength(string ansiString)
		{
			if (string.IsNullOrEmpty(ansiString))
				return 0;

			int length = 0;
			int i = 0;

			while (i < ansiString.Length)
			{
				if (ansiString[i] == '\x1b' && i + 1 < ansiString.Length && ansiString[i + 1] == '[')
				{
					// Skip escape sequence
					i += 2;
					while (i < ansiString.Length && (char.IsDigit(ansiString[i]) || ansiString[i] == ';'))
						i++;
					if (i < ansiString.Length)
						i++; // Skip command character
				}
				else
				{
					length++;
					i++;
				}
			}

			return length;
		}

		/// <summary>
		/// Strips ANSI escape sequences from a string, returning only visible text.
		/// </summary>
		public static string StripAnsi(string ansiString)
		{
			if (string.IsNullOrEmpty(ansiString))
				return string.Empty;

			var result = new StringBuilder();
			int i = 0;

			while (i < ansiString.Length)
			{
				if (ansiString[i] == '\x1b' && i + 1 < ansiString.Length && ansiString[i + 1] == '[')
				{
					// Skip escape sequence
					i += 2;
					while (i < ansiString.Length && (char.IsDigit(ansiString[i]) || ansiString[i] == ';'))
						i++;
					if (i < ansiString.Length)
						i++; // Skip command character
				}
				else
				{
					result.Append(ansiString[i]);
					i++;
				}
			}

			return result.ToString();
		}

		/// <summary>
		/// Truncates an ANSI string to the specified visible length, preserving color codes.
		/// </summary>
		public static string TruncateVisible(string ansiString, int maxVisibleLength)
		{
			if (string.IsNullOrEmpty(ansiString) || maxVisibleLength <= 0)
				return string.Empty;

			var result = new StringBuilder();
			int visibleLength = 0;
			int i = 0;

			while (i < ansiString.Length && visibleLength < maxVisibleLength)
			{
				if (ansiString[i] == '\x1b' && i + 1 < ansiString.Length && ansiString[i + 1] == '[')
				{
					// Copy escape sequence entirely
					result.Append(ansiString[i]);
					i++;
					result.Append(ansiString[i]);
					i++;
					while (i < ansiString.Length && (char.IsDigit(ansiString[i]) || ansiString[i] == ';'))
					{
						result.Append(ansiString[i]);
						i++;
					}
					if (i < ansiString.Length)
					{
						result.Append(ansiString[i]);
						i++;
					}
				}
				else
				{
					result.Append(ansiString[i]);
					visibleLength++;
					i++;
				}
			}

			return result.ToString();
		}

		private static void ProcessSgrParams(string paramsStr, ref Color fg, ref Color bg, ref bool isBold, Color defaultFg, Color defaultBg)
		{
			var codes = paramsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
			int codeIndex = 0;

			while (codeIndex < codes.Length)
			{
				if (!int.TryParse(codes[codeIndex], out int code))
				{
					codeIndex++;
					continue;
				}

				switch (code)
				{
					case 0: // Reset
						fg = defaultFg;
						bg = defaultBg;
						isBold = false;
						break;

					case 1: // Bold
						isBold = true;
						break;

					case 22: // Normal intensity
						isBold = false;
						break;

					// Foreground colors (30-37)
					case >= 30 and <= 37:
						fg = isBold ? BrightColors[code - 30] : StandardColors[code - 30];
						break;

					case 38: // Extended foreground
						codeIndex++;
						fg = ParseExtendedColor(codes, ref codeIndex) ?? fg;
						continue; // codeIndex already incremented

					case 39: // Default foreground
						fg = defaultFg;
						break;

					// Background colors (40-47)
					case >= 40 and <= 47:
						bg = StandardColors[code - 40];
						break;

					case 48: // Extended background
						codeIndex++;
						bg = ParseExtendedColor(codes, ref codeIndex) ?? bg;
						continue; // codeIndex already incremented

					case 49: // Default background
						bg = defaultBg;
						break;

					// Bright foreground colors (90-97)
					case >= 90 and <= 97:
						fg = BrightColors[code - 90];
						break;

					// Bright background colors (100-107)
					case >= 100 and <= 107:
						bg = BrightColors[code - 100];
						break;
				}

				codeIndex++;
			}
		}

		private static Color? ParseExtendedColor(string[] codes, ref int index)
		{
			if (index >= codes.Length)
				return null;

			if (!int.TryParse(codes[index], out int mode))
				return null;

			index++;

			switch (mode)
			{
				case 5: // 256-color mode
					if (index < codes.Length && int.TryParse(codes[index], out int colorIndex))
					{
						index++;
						return Get256Color(colorIndex);
					}
					break;

				case 2: // 24-bit RGB mode
					if (index + 2 < codes.Length &&
						int.TryParse(codes[index], out int r) &&
						int.TryParse(codes[index + 1], out int g) &&
						int.TryParse(codes[index + 2], out int b))
					{
						index += 3;
						return new Color((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
					}
					break;
			}

			return null;
		}

		private static Color Get256Color(int index)
		{
			// 0-15: Standard and bright colors
			if (index < 8)
				return StandardColors[index];
			if (index < 16)
				return BrightColors[index - 8];

			// 16-231: 6x6x6 color cube
			if (index < 232)
			{
				int cubeIndex = index - 16;
				int r = cubeIndex / 36;
				int g = (cubeIndex % 36) / 6;
				int b = cubeIndex % 6;

				// Convert to RGB (0, 95, 135, 175, 215, 255)
				byte ToComponent(int v) => v == 0 ? (byte)0 : (byte)(55 + v * 40);
				return new Color(ToComponent(r), ToComponent(g), ToComponent(b));
			}

			// 232-255: Grayscale (24 shades)
			if (index < 256)
			{
				int gray = 8 + (index - 232) * 10;
				byte component = (byte)Math.Clamp(gray, 0, 255);
				return new Color(component, component, component);
			}

			return Color.White;
		}

		/// <summary>
		/// Converts a list of lines with ANSI codes to a 2D array of cells.
		/// </summary>
		public static Cell[,] ParseLines(IReadOnlyList<string> lines, int width, int height, Color defaultForeground, Color defaultBackground)
		{
			var cells = new Cell[width, height];

			// Initialize with blanks
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					cells[x, y] = Cell.BlankWithBackground(defaultBackground);
				}
			}

			// Parse each line
			for (int y = 0; y < Math.Min(lines.Count, height); y++)
			{
				int x = 0;
				foreach (var cell in Parse(lines[y], defaultForeground, defaultBackground))
				{
					if (x >= width)
						break;
					cells[x, y] = cell;
					x++;
				}
			}

			return cells;
		}
	}
}
