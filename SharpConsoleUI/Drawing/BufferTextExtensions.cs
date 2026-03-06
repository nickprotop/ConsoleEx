// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using Spectre.Console;

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
			int x = clip.X + (clip.Width - text.Length) / 2;
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
			int x = clip.X + clip.Width - text.Length;
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
			string displayText = text.Length > inner.Width ? text[..inner.Width] : text;
			int textX = inner.X + (inner.Width - displayText.Length) / 2;

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
			int currentLength = 0;
			var currentLine = new System.Text.StringBuilder();

			foreach (var word in words)
			{
				if (word.Length == 0)
					continue;

				if (currentLength == 0)
				{
					// First word on line - handle words longer than width
					if (word.Length > width)
					{
						for (int i = 0; i < word.Length; i += width)
						{
							int chunkLength = Math.Min(width, word.Length - i);
							lines.Add(word.Substring(i, chunkLength));
						}
						continue;
					}

					currentLine.Append(word);
					currentLength = word.Length;
				}
				else if (currentLength + 1 + word.Length <= width)
				{
					// Word fits on current line
					currentLine.Append(' ');
					currentLine.Append(word);
					currentLength += 1 + word.Length;
				}
				else
				{
					// Start new line
					lines.Add(currentLine.ToString());
					currentLine.Clear();

					if (word.Length > width)
					{
						for (int i = 0; i < word.Length; i += width)
						{
							int chunkLength = Math.Min(width, word.Length - i);
							lines.Add(word.Substring(i, chunkLength));
						}
						currentLength = 0;
						continue;
					}

					currentLine.Append(word);
					currentLength = word.Length;
				}
			}

			if (currentLine.Length > 0)
				lines.Add(currentLine.ToString());

			return lines;
		}
	}
}
