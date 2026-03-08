// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Parsing
{
	/// <summary>
	/// Represents a parsed FIGlet font loaded from a .flf file.
	/// </summary>
	public class FigletFont
	{
		private readonly Dictionary<char, string[]> _characters = new();

		/// <summary>Height of each character in lines.</summary>
		public int Height { get; private set; }

		/// <summary>The hardblank character used in the font file.</summary>
		public char HardBlank { get; private set; }

		/// <summary>
		/// Gets the character bitmap lines for the given character.
		/// Returns the space character bitmap if the character is not defined.
		/// </summary>
		public string[] GetCharacter(char c)
		{
			if (_characters.TryGetValue(c, out var lines))
				return lines;
			// Fall back to space
			if (_characters.TryGetValue(' ', out var spaceLines))
				return spaceLines;
			// Return empty lines
			var empty = new string[Height];
			for (int i = 0; i < Height; i++) empty[i] = " ";
			return empty;
		}

		/// <summary>
		/// Loads a FIGlet font from a stream.
		/// </summary>
		public static FigletFont Load(Stream stream)
		{
			var font = new FigletFont();

			using var reader = new StreamReader(stream);

			// Parse header line: flf2aHARDBLANK height baseline maxLength oldLayout commentLines ...
			string? headerLine = reader.ReadLine();
			if (headerLine == null || !headerLine.StartsWith("flf2"))
				throw new FormatException("Invalid FIGlet font: missing flf2 header");

			// The hardblank is the character right after "flf2a"
			font.HardBlank = headerLine.Length > 5 ? headerLine[5] : '$';

			var headerParts = headerLine[6..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (headerParts.Length < 2)
				throw new FormatException("Invalid FIGlet font: incomplete header");

			font.Height = int.Parse(headerParts[0]);
			int commentLines = headerParts.Length >= 5 ? int.Parse(headerParts[4]) : 0;

			// Skip comment lines
			for (int i = 0; i < commentLines; i++)
				reader.ReadLine();

			// Read required characters (ASCII 32-126)
			for (int charCode = 32; charCode <= 126; charCode++)
			{
				var charLines = ReadCharacterLines(reader, font.Height, font.HardBlank);
				if (charLines != null)
					font._characters[(char)charCode] = charLines;
			}

			// Read optional extended characters (code tagged)
			while (!reader.EndOfStream)
			{
				string? tagLine = reader.ReadLine();
				if (tagLine == null || string.IsNullOrWhiteSpace(tagLine))
					continue;

				// Parse character code from tag line
				var tagParts = tagLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (tagParts.Length == 0) continue;

				if (TryParseCharCode(tagParts[0], out int code))
				{
					var charLines = ReadCharacterLines(reader, font.Height, font.HardBlank);
					if (charLines != null && code >= 0 && code <= 0xFFFF)
						font._characters[(char)code] = charLines;
				}
			}

			return font;
		}

		private static string[]? ReadCharacterLines(StreamReader reader, int height, char hardBlank)
		{
			var lines = new string[height];

			for (int i = 0; i < height; i++)
			{
				string? line = reader.ReadLine();
				if (line == null) return null;

				// Strip end markers (@ or @@)
				int endIdx = line.Length;
				while (endIdx > 0 && line[endIdx - 1] == '@')
					endIdx--;

				string cleaned = line[..endIdx];

				// Replace hardblank with space
				cleaned = cleaned.Replace(hardBlank, ' ');

				lines[i] = cleaned;
			}

			return lines;
		}

		private static bool TryParseCharCode(string token, out int code)
		{
			code = 0;

			// Handle negative codes (used for some special characters)
			if (token.StartsWith('-'))
			{
				if (int.TryParse(token, out code))
					return true;
				return false;
			}

			// Handle hex (0x...) and octal (0...) prefixes
			if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
				token.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
			{
				return int.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, null, out code);
			}

			if (token.StartsWith('0') && token.Length > 1)
			{
				// Octal
				try
				{
					code = Convert.ToInt32(token, 8);
					return true;
				}
				catch
				{
					return int.TryParse(token, out code);
				}
			}

			return int.TryParse(token, out code);
		}
	}

	/// <summary>
	/// Renders text using a FIGlet font into lines of plain characters.
	/// </summary>
	public static class FigletRenderer
	{
		/// <summary>
		/// Renders the given text using the specified FIGlet font.
		/// Returns one string per output line.
		/// </summary>
		/// <param name="text">The text to render.</param>
		/// <param name="font">The FIGlet font to use.</param>
		/// <param name="maxWidth">Maximum output width (0 = unlimited).</param>
		/// <returns>A list of output lines.</returns>
		public static List<string> Render(string text, FigletFont font, int maxWidth = 0)
		{
			if (string.IsNullOrEmpty(text) || font.Height <= 0)
				return new List<string>();

			int height = font.Height;
			var outputLines = new string[height];
			for (int i = 0; i < height; i++)
				outputLines[i] = string.Empty;

			// Concatenate each character's bitmap side by side
			foreach (char c in text)
			{
				var charLines = font.GetCharacter(c);
				for (int row = 0; row < height; row++)
				{
					string charLine = row < charLines.Length ? charLines[row] : string.Empty;
					outputLines[row] += charLine;
				}
			}

			// Trim trailing spaces from each line and apply maxWidth
			var result = new List<string>(height);
			for (int i = 0; i < height; i++)
			{
				string line = outputLines[i].TrimEnd();
				if (maxWidth > 0 && line.Length > maxWidth)
					line = line[..maxWidth];
				result.Add(line);
			}

			return result;
		}

		/// <summary>
		/// Renders text with justification applied.
		/// </summary>
		/// <param name="text">The text to render.</param>
		/// <param name="font">The FIGlet font to use.</param>
		/// <param name="maxWidth">Maximum output width for justification.</param>
		/// <param name="justification">Text justification.</param>
		/// <returns>A list of output lines.</returns>
		public static List<string> RenderJustified(string text, FigletFont font, int maxWidth, Layout.TextJustification justification)
		{
			var lines = Render(text, font, maxWidth);

			if (justification == Layout.TextJustification.Left || maxWidth <= 0)
				return lines;

			for (int i = 0; i < lines.Count; i++)
			{
				int lineLen = lines[i].Length;
				if (lineLen >= maxWidth) continue;

				int padding = justification == Layout.TextJustification.Center
					? (maxWidth - lineLen) / 2
					: maxWidth - lineLen; // Right

				if (padding > 0)
					lines[i] = new string(' ', padding) + lines[i];
			}

			return lines;
		}
	}
}
