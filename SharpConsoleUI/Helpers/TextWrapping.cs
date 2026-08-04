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
	/// Soft-wrapping of one logical line into visual segments, measured in DISPLAY COLUMNS rather
	/// than characters so CJK and emoji wrap where they actually render.
	/// <para>
	/// This is shared deliberately. <see cref="Controls.MultilineEditControl"/> and
	/// <see cref="Controls.PromptControl"/> both wrap text, and two implementations of the same rule
	/// drift: the visible symptom would be a textarea and a prompt breaking the same string at
	/// different points. Each control still owns its own cache and its own segment record — this
	/// helper only decides WHERE the breaks fall, as (offset, length) pairs in character indices
	/// into the line it was given.
	/// </para>
	/// </summary>
	internal static class TextWrapping
	{
		/// <summary>
		/// One visual segment of a wrapped line: a character range into the source line.
		/// Callers map this onto whatever record they already use.
		/// </summary>
		internal readonly record struct Segment(int Offset, int Length);

		/// <summary>
		/// Hard-wraps at the column limit, breaking mid-word when a word is longer than the width.
		/// An empty line yields one empty segment, so an empty line still occupies a row.
		/// </summary>
		/// <param name="into">Destination list; segments are appended.</param>
		/// <param name="line">The source line.</param>
		/// <param name="width">Available width in display columns; values below 1 are treated as 1.</param>
		public static void WrapCharacters(List<Segment> into, string line, int width)
		{
			ArgumentNullException.ThrowIfNull(into);
			line ??= string.Empty;
			int safeWidth = Math.Max(1, width);

			if (line.Length == 0)
			{
				into.Add(new Segment(0, 0));
				return;
			}

			int j = 0;
			while (j < line.Length)
			{
				// TakeColumns advances at least one rune, so this always terminates.
				var (endChar, _) = UnicodeWidth.TakeColumns(line, j, safeWidth);
				into.Add(new Segment(j, endChar - j));
				j = endChar;
			}
		}

		/// <summary>
		/// Wraps at word boundaries where one exists within the fitting range, falling back to a hard
		/// break for a word longer than the width. Original spacing is preserved: the space a break
		/// happens at stays on the line it ended.
		/// </summary>
		/// <param name="into">Destination list; segments are appended.</param>
		/// <param name="line">The source line.</param>
		/// <param name="width">Available width in display columns; values below 1 are treated as 1.</param>
		public static void WrapWords(List<Segment> into, string line, int width)
		{
			ArgumentNullException.ThrowIfNull(into);
			line ??= string.Empty;
			int safeWidth = Math.Max(1, width);

			if (line.Length == 0)
			{
				into.Add(new Segment(0, 0));
				return;
			}

			int pos = 0;
			while (pos < line.Length)
			{
				// Compute how many whole runes fit within 'width' DISPLAY columns from pos.
				// fitEnd is always > pos (TakeColumns advances at least one rune) and <= line.Length,
				// so all indexing/substring below is in-bounds and the loop always advances.
				var (fitEnd, _) = UnicodeWidth.TakeColumns(line, pos, safeWidth);

				if (fitEnd >= line.Length)
				{
					into.Add(new Segment(pos, line.Length - pos));
					break;
				}

				// Find the last space within the column-fitting char range [pos, fitEnd) to break at.
				int breakAt = -1;
				for (int j = fitEnd - 1; j > pos; j--)
				{
					if (line[j] == ' ')
					{
						breakAt = j;
						break;
					}
				}

				// Break at the word boundary (the space stays on the line it ended), or force a break
				// at the column-fit boundary when a single word is longer than the width.
				int len = breakAt > pos ? breakAt - pos + 1 : fitEnd - pos;
				into.Add(new Segment(pos, len));
				pos += len;
			}
		}
	}
}
