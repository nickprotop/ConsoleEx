// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Lays a toast message out into rows: markup-aware word wrap, capped at a row count, with the last
	/// visible row ellipsized when text is left over.
	/// </summary>
	/// <remarks>
	/// Shared by the measuring side (<c>ToastService</c>, deciding how big the box is) and the painting
	/// side (<c>ToastContent</c>, filling it). Both must agree on the row count or the box is the wrong
	/// height for its text, so the wrap lives here once rather than in each.
	/// </remarks>
	internal static class ToastTextLayout
	{
		/// <summary>The character appended to a row whose text was cut short.</summary>
		internal const char Ellipsis = '…';

		/// <summary>
		/// Wraps <paramref name="markup"/> to <paramref name="width"/> display columns, returning at most
		/// <paramref name="maxLines"/> rows of cells.
		/// </summary>
		/// <param name="markup">The message, which may contain markup tags.</param>
		/// <param name="width">Content width in display columns (chrome already subtracted).</param>
		/// <param name="maxLines">Maximum rows to return; at least one row always comes back.</param>
		/// <returns>The rows to paint, each already in display columns.</returns>
		internal static List<List<Cell>> Wrap(string markup, int width, int maxLines)
		{
			width = Math.Max(1, width);
			maxLines = Math.Max(1, maxLines);

			var rows = MarkupParser.ParseLines(markup, width, Color.White, Color.Black, out _, out _);

			if (rows.Count <= maxLines)
				return rows;

			// More text than fits: keep what we can and mark the cut on the last kept row.
			var kept = rows.GetRange(0, maxLines);
			kept[maxLines - 1] = Ellipsize(kept[maxLines - 1], width);
			return kept;
		}

		/// <summary>
		/// Replaces the tail of a row with an ellipsis, keeping the result within
		/// <paramref name="width"/> display columns.
		/// </summary>
		private static List<Cell> Ellipsize(List<Cell> row, int width)
		{
			// Cells are already one per display column, so trimming by count trims by columns. Leave room
			// for the ellipsis itself; a width-1 box becomes just the ellipsis.
			int keep = Math.Max(0, Math.Min(row.Count, width - 1));
			var trimmed = row.GetRange(0, keep);

			// Never end on the leading half of a wide character — dropping its continuation would leave
			// half a glyph against the ellipsis.
			while (trimmed.Count > 0 && UnicodeWidth.GetRuneWidth(trimmed[trimmed.Count - 1].Character) == 2)
				trimmed.RemoveAt(trimmed.Count - 1);

			var last = row.Count > 0 ? row[row.Count - 1] : new Cell(' ', Color.White, Color.Black);
			trimmed.Add(new Cell(new System.Text.Rune(Ellipsis), last.Foreground, last.Background));
			return trimmed;
		}
	}
}
