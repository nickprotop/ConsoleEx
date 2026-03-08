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
			int maxWidth = 0;

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
				if (cleaned.Length > maxWidth)
					maxWidth = cleaned.Length;
			}

			// Pad all lines to the same width so characters align when concatenated
			for (int i = 0; i < height; i++)
			{
				if (lines[i].Length < maxWidth)
					lines[i] = lines[i].PadRight(maxWidth);
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

			// Find the consistent width across all lines (before trimming)
			// so that justification alignment works correctly
			int consistentWidth = 0;
			for (int i = 0; i < height; i++)
			{
				if (outputLines[i].Length > consistentWidth)
					consistentWidth = outputLines[i].Length;
			}

			// Apply maxWidth constraint, then trim trailing spaces
			// but pad shorter lines to the trimmed max width for alignment
			if (maxWidth > 0 && consistentWidth > maxWidth)
				consistentWidth = maxWidth;

			var result = new List<string>(height);
			int trimmedMax = 0;
			for (int i = 0; i < height; i++)
			{
				string line = outputLines[i];
				if (maxWidth > 0 && line.Length > maxWidth)
					line = line[..maxWidth];
				string trimmed = line.TrimEnd();
				if (trimmed.Length > trimmedMax)
					trimmedMax = trimmed.Length;
				result.Add(trimmed);
			}

			// Pad all lines to the same trimmed max width for consistent alignment
			for (int i = 0; i < result.Count; i++)
			{
				if (result[i].Length < trimmedMax)
					result[i] = result[i].PadRight(trimmedMax);
			}

			return result;
		}

		/// <summary>
		/// Renders text with wrapping support. Each wrapped "row" consists of font.Height lines.
		/// </summary>
		/// <param name="text">The text to render.</param>
		/// <param name="font">The FIGlet font to use.</param>
		/// <param name="maxWidth">Maximum output width for wrapping.</param>
		/// <param name="wrapMode">The wrapping mode to apply.</param>
		/// <returns>A list of output lines (multiple rows of font.Height lines each).</returns>
		public static List<string> RenderWrapped(string text, FigletFont font, int maxWidth, Controls.WrapMode wrapMode)
		{
			if (string.IsNullOrEmpty(text) || font.Height <= 0 || maxWidth <= 0)
				return Render(text, font, maxWidth);

			if (wrapMode == Controls.WrapMode.NoWrap)
				return Render(text, font, maxWidth);

			if (wrapMode == Controls.WrapMode.WrapWords)
				return RenderWrapWords(text, font, maxWidth);

			return RenderWrapChars(text, font, maxWidth);
		}

		/// <summary>
		/// Renders text with wrapping and justification applied per row.
		/// </summary>
		public static List<string> RenderWrappedJustified(string text, FigletFont font, int maxWidth, Controls.WrapMode wrapMode, Layout.TextJustification justification)
		{
			if (wrapMode == Controls.WrapMode.NoWrap)
				return RenderJustified(text, font, maxWidth, justification);

			var lines = RenderWrapped(text, font, maxWidth, wrapMode);

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

		private static List<string> RenderWrapChars(string text, FigletFont font, int maxWidth)
		{
			int height = font.Height;
			var allLines = new List<string>();

			// Current row accumulator (one string per font line)
			var rowLines = new string[height];
			for (int i = 0; i < height; i++) rowLines[i] = string.Empty;
			int currentRowWidth = 0;

			foreach (char c in text)
			{
				var charBitmap = font.GetCharacter(c);
				int charWidth = 0;
				for (int r = 0; r < height; r++)
				{
					string cl = r < charBitmap.Length ? charBitmap[r] : string.Empty;
					if (cl.Length > charWidth) charWidth = cl.Length;
				}

				// If adding this char exceeds maxWidth, finalize current row
				if (currentRowWidth > 0 && currentRowWidth + charWidth > maxWidth)
				{
					FinalizeRow(allLines, rowLines, height);
					rowLines = new string[height];
					for (int i = 0; i < height; i++) rowLines[i] = string.Empty;
					currentRowWidth = 0;
				}

				// Append char to current row
				for (int r = 0; r < height; r++)
				{
					string cl = r < charBitmap.Length ? charBitmap[r] : string.Empty;
					rowLines[r] += cl;
				}
				currentRowWidth += charWidth;
			}

			// Finalize last row
			if (currentRowWidth > 0)
				FinalizeRow(allLines, rowLines, height);

			return allLines;
		}

		private static List<string> RenderWrapWords(string text, FigletFont font, int maxWidth)
		{
			int height = font.Height;
			var allLines = new List<string>();
			var words = SplitIntoWords(text);

			var rowLines = new string[height];
			for (int i = 0; i < height; i++) rowLines[i] = string.Empty;
			int currentRowWidth = 0;

			foreach (var word in words)
			{
				// Measure word width
				int wordWidth = MeasureTextWidth(word, font);

				// If a single word exceeds maxWidth, char-wrap it
				if (wordWidth > maxWidth)
				{
					// Flush current row first
					if (currentRowWidth > 0)
					{
						FinalizeRow(allLines, rowLines, height);
						rowLines = new string[height];
						for (int i = 0; i < height; i++) rowLines[i] = string.Empty;
						currentRowWidth = 0;
					}

					// Char-wrap this long word
					var charWrapped = RenderWrapChars(word, font, maxWidth);
					allLines.AddRange(charWrapped);
					continue;
				}

				// If adding this word exceeds maxWidth, wrap
				if (currentRowWidth > 0 && currentRowWidth + wordWidth > maxWidth)
				{
					FinalizeRow(allLines, rowLines, height);
					rowLines = new string[height];
					for (int i = 0; i < height; i++) rowLines[i] = string.Empty;
					currentRowWidth = 0;
				}

				// Append word to current row
				foreach (char c in word)
				{
					var charBitmap = font.GetCharacter(c);
					for (int r = 0; r < height; r++)
					{
						string cl = r < charBitmap.Length ? charBitmap[r] : string.Empty;
						rowLines[r] += cl;
					}
				}
				currentRowWidth += wordWidth;
			}

			if (currentRowWidth > 0)
				FinalizeRow(allLines, rowLines, height);

			return allLines;
		}

		private static int MeasureTextWidth(string text, FigletFont font)
		{
			int width = 0;
			foreach (char c in text)
			{
				var charBitmap = font.GetCharacter(c);
				int charWidth = 0;
				for (int r = 0; r < charBitmap.Length; r++)
				{
					if (charBitmap[r].Length > charWidth) charWidth = charBitmap[r].Length;
				}
				width += charWidth;
			}
			return width;
		}

		private static List<string> SplitIntoWords(string text)
		{
			// Split on spaces, keeping spaces attached to the following word
			// so that spacing is preserved when words are placed on the same row
			var words = new List<string>();
			int i = 0;
			while (i < text.Length)
			{
				// Skip leading spaces and attach them to the next word
				int start = i;
				while (i < text.Length && text[i] == ' ') i++;
				while (i < text.Length && text[i] != ' ') i++;
				if (i > start)
					words.Add(text[start..i]);
			}
			return words;
		}

		private static void FinalizeRow(List<string> allLines, string[] rowLines, int height)
		{
			// Trim trailing spaces and normalize widths within the row
			int trimmedMax = 0;
			var trimmed = new string[height];
			for (int i = 0; i < height; i++)
			{
				trimmed[i] = rowLines[i].TrimEnd();
				if (trimmed[i].Length > trimmedMax)
					trimmedMax = trimmed[i].Length;
			}

			for (int i = 0; i < height; i++)
			{
				if (trimmed[i].Length < trimmedMax)
					trimmed[i] = trimmed[i].PadRight(trimmedMax);
				allLines.Add(trimmed[i]);
			}
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
