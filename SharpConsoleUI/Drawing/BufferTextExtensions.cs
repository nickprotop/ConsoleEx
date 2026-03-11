// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Drawing
{
	/// <summary>
	/// Extension methods for text alignment and word wrapping on CharacterBuffer.
	/// </summary>
	public static class BufferTextExtensions
	{
		/// <summary>
		/// Writes horizontally centered text within the clip region at the specified Y position.
		/// </summary>
		public static void WriteStringCentered(this CharacterBuffer buffer, int y, string text,
			Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (string.IsNullOrEmpty(text))
				return;

			var clip = clipRect ?? buffer.Bounds;
			int displayWidth = UnicodeWidth.GetStringWidth(text);
			int x = clip.X + (clip.Width - displayWidth) / 2;
			buffer.WriteStringClipped(x, y, text, fg, bg, clip);
		}

		/// <summary>
		/// Writes right-aligned text within the clip region at the specified Y position.
		/// </summary>
		public static void WriteStringRight(this CharacterBuffer buffer, int y, string text,
			Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (string.IsNullOrEmpty(text))
				return;

			var clip = clipRect ?? buffer.Bounds;
			int displayWidth = UnicodeWidth.GetStringWidth(text);
			int x = clip.X + clip.Width - displayWidth;
			buffer.WriteStringClipped(x, y, text, fg, bg, clip);
		}

		/// <summary>
		/// Draws a box and writes centered text inside it.
		/// The text is centered both horizontally and vertically within the box interior.
		/// </summary>
		public static void WriteStringInBox(this CharacterBuffer buffer, LayoutRect rect, string text,
			BoxChars boxChars, Color fg, Color bg, Color boxFg, Color boxBg, LayoutRect? clipRect = null)
		{
			if (rect.Width < 2 || rect.Height < 2)
				return;

			var clip = clipRect ?? buffer.Bounds;

			// Draw the box border
			buffer.DrawBox(rect, boxChars, boxFg, boxBg);

			// Calculate inner area (excluding border)
			var inner = new LayoutRect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
			if (inner.IsEmpty || string.IsNullOrEmpty(text))
				return;

			// Clear the inner area
			buffer.FillRect(inner, ' ', fg, bg);

			// Center text vertically
			int textY = inner.Y + (inner.Height - 1) / 2;

			// Center text horizontally, truncate if needed
			int textDisplayWidth = UnicodeWidth.GetStringWidth(text);
			string displayText = text;
			if (textDisplayWidth > inner.Width)
			{
				// Truncate by display width
				var sb = new System.Text.StringBuilder();
				int accWidth = 0;
				foreach (var rune in text.EnumerateRunes())
				{
					int cw = UnicodeWidth.GetRuneWidth(rune);
					if (accWidth + cw > inner.Width)
						break;
					sb.AppendRune(rune);
					accWidth += cw;
				}
				displayText = sb.ToString();
				textDisplayWidth = accWidth;
			}
			int textX = inner.X + (inner.Width - textDisplayWidth) / 2;

			buffer.WriteStringClipped(textX, textY, displayText, fg, bg, clip);
		}

		/// <summary>
		/// Writes word-wrapped text starting at the given position within the specified width.
		/// Words that exceed the available width are broken across lines.
		/// </summary>
		public static void WriteWrappedText(this CharacterBuffer buffer, int x, int y, int width,
			string text, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (string.IsNullOrEmpty(text) || width < DrawingConstants.MinWordWrapWidth)
				return;

			var clip = clipRect ?? buffer.Bounds;
			var lines = WrapText(text, width);

			for (int i = 0; i < lines.Count; i++)
			{
				int lineY = y + i;
				if (lineY >= clip.Y + clip.Height)
					break;

				buffer.WriteStringClipped(x, lineY, lines[i], fg, bg, clip);
			}
		}

		private static List<string> WrapText(string text, int width)
		{
			var lines = new List<string>();
			var words = text.Split(' ');
			int currentWidth = 0;
			var currentLine = new System.Text.StringBuilder();

			foreach (var word in words)
			{
				if (word.Length == 0)
					continue;

				int wordWidth = UnicodeWidth.GetStringWidth(word);

				if (currentWidth == 0)
				{
					// First word on line - handle words wider than width
					if (wordWidth > width)
					{
						BreakLongWord(word, width, lines);
						continue;
					}

					currentLine.Append(word);
					currentWidth = wordWidth;
				}
				else if (currentWidth + 1 + wordWidth <= width)
				{
					// Word fits on current line
					currentLine.Append(' ');
					currentLine.Append(word);
					currentWidth += 1 + wordWidth;
				}
				else
				{
					// Start new line
					lines.Add(currentLine.ToString());
					currentLine.Clear();

					if (wordWidth > width)
					{
						BreakLongWord(word, width, lines);
						currentWidth = 0;
						continue;
					}

					currentLine.Append(word);
					currentWidth = wordWidth;
				}
			}

			if (currentLine.Length > 0)
				lines.Add(currentLine.ToString());

			return lines;
		}

		private static void BreakLongWord(string word, int width, List<string> lines)
		{
			var chunk = new System.Text.StringBuilder();
			int chunkWidth = 0;

			foreach (var rune in word.EnumerateRunes())
			{
				int cw = UnicodeWidth.GetRuneWidth(rune);
				if (chunkWidth + cw > width && chunkWidth > 0)
				{
					lines.Add(chunk.ToString());
					chunk.Clear();
					chunkWidth = 0;
				}
				chunk.AppendRune(rune);
				chunkWidth += cw;
			}

			if (chunk.Length > 0)
				lines.Add(chunk.ToString());
		}
	}
}
