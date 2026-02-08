// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides word boundary detection for text navigation and editing.
	/// Word characters: letters, digits, underscores. Everything else is a separator.
	/// </summary>
	public static class WordBoundaryHelper
	{
		private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

		/// <summary>
		/// Finds the start of the previous word (for Ctrl+Left, Ctrl+Backspace).
		/// </summary>
		public static int FindPreviousWordBoundary(string line, int position)
		{
			if (position <= 0) return 0;
			int pos = position - 1;

			// Skip any whitespace/separators going left
			while (pos > 0 && !IsWordChar(line[pos]))
				pos--;

			// Skip word characters going left
			while (pos > 0 && IsWordChar(line[pos - 1]))
				pos--;

			return pos;
		}

		/// <summary>
		/// Finds the position after the end of the next word (for Ctrl+Right, Ctrl+Delete).
		/// </summary>
		public static int FindNextWordBoundary(string line, int position)
		{
			if (position >= line.Length) return line.Length;
			int pos = position;

			// Skip word characters going right
			while (pos < line.Length && IsWordChar(line[pos]))
				pos++;

			// Skip any whitespace/separators going right
			while (pos < line.Length && !IsWordChar(line[pos]))
				pos++;

			return pos;
		}

		/// <summary>
		/// Finds the word boundaries (start, end) around a given position (for double-click select).
		/// Returns (position, position) if position is out of range.
		/// </summary>
		public static (int start, int end) FindWordAt(string line, int position)
		{
			if (position < 0 || position >= line.Length)
				return (position, position);

			if (!IsWordChar(line[position]))
			{
				// Click on separator - select the separator run
				int start = position, end = position;
				while (start > 0 && !IsWordChar(line[start - 1])) start--;
				while (end < line.Length && !IsWordChar(line[end])) end++;
				return (start, end);
			}

			int s = position, e = position;
			while (s > 0 && IsWordChar(line[s - 1])) s--;
			while (e < line.Length && IsWordChar(line[e])) e++;
			return (s, e);
		}
	}
}
