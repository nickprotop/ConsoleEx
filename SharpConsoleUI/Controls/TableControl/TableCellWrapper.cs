// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Wraps one table cell's markup to a column width.
/// </summary>
/// <remarks>
/// <para>
/// The wrapping is <see cref="MarkupParser.ParseLines(string, int, Color, Color)"/>, which already
/// breaks on words, falls back to breaking a word only when it alone is wider than the column,
/// counts display columns rather than characters, and carries the active style stack across a
/// break so a colour opened on one line is still open on the next. None of that is reimplemented
/// here.
/// </para>
/// <para>
/// What this adds is the part a table needs and a paragraph does not: a ceiling on how many lines
/// one row may spend, and an ellipsis on the last line kept when content was dropped, so a
/// truncated cell is visibly truncated rather than quietly short.
/// </para>
/// </remarks>
internal static class TableCellWrapper
{
	/// <summary>
	/// Wraps a cell's markup into the lines it occupies at a column width.
	/// </summary>
	/// <param name="markup">The cell's value, which may carry markup.</param>
	/// <param name="width">The column's width in display columns.</param>
	/// <param name="maxLines">The most lines to return, or 0 for no limit.</param>
	/// <param name="fg">The colour text starts in.</param>
	/// <param name="bg">The colour text sits on.</param>
	/// <returns>
	/// One list of cells per line, always at least one line, never more than
	/// <paramref name="maxLines"/> when that is positive, and never more than
	/// <see cref="ControlDefaults.TableMaxWrapLines"/>.
	/// </returns>
	internal static List<List<Cell>> WrapCell(string markup, int width, int maxLines, Color fg, Color bg)
	{
		// A column with no room cannot show anything, but the row still occupies a line: returning
		// an empty list here would make the row zero lines tall and divide the scroll arithmetic
		// by nothing.
		if (width <= 0 || string.IsNullOrEmpty(markup))
			return new List<List<Cell>> { new List<Cell>() };

		int ceiling = Ceiling(maxLines);
		var lines = MarkupParser.ParseLines(markup, width, fg, bg);

		if (lines.Count == 0)
			return new List<List<Cell>> { new List<Cell>() };

		if (lines.Count <= ceiling)
			return lines;

		// Over the ceiling: keep what fits and mark the last kept line, so the reader can tell a
		// cell that happened to end there from one that was cut.
		var kept = lines.GetRange(0, ceiling);
		MarkTruncated(kept[ceiling - 1], width, fg, bg);
		return kept;
	}

	/// <summary>
	/// How many lines <see cref="WrapCell"/> would return for the same arguments.
	/// </summary>
	/// <remarks>
	/// Runs the same wrap rather than estimating. Two code paths that each answer "how tall is
	/// this" are two answers that will eventually disagree, and the one that disagrees with the
	/// paint is the one that corrupts the layout.
	/// </remarks>
	internal static int MeasureLines(string markup, int width, int maxLines)
	{
		if (width <= 0 || string.IsNullOrEmpty(markup))
			return 1;

		int count = MarkupParser.ParseLines(markup, width, Color.White, Color.Black).Count;
		return Math.Max(1, Math.Min(count, Ceiling(maxLines)));
	}

	/// <summary>The effective line ceiling: the column's limit, bounded by the library's own.</summary>
	private static int Ceiling(int maxLines) =>
		maxLines <= 0 ? ControlDefaults.TableMaxWrapLines : Math.Min(maxLines, ControlDefaults.TableMaxWrapLines);

	/// <summary>
	/// Puts the ellipsis at the end of a line that had more content after it.
	/// </summary>
	/// <remarks>
	/// Overwrites the line's last cell when the line is full, rather than appending, because
	/// appending would push the line one cell past the column and into its neighbour. The
	/// overwritten cell keeps the line's own colours so the mark does not read as a different
	/// value.
	/// </remarks>
	private static void MarkTruncated(List<Cell> line, int width, Color fg, Color bg)
	{
		var ellipsis = new Cell(ControlDefaults.TableWrapEllipsis, fg, bg);

		if (line.Count == 0)
		{
			line.Add(ellipsis);
			return;
		}

		// Match the colours already on the line so the ellipsis belongs to the text it truncates.
		var last = line[^1];
		ellipsis = new Cell(ControlDefaults.TableWrapEllipsis, last.Foreground, last.Background);

		if (line.Count < width)
			line.Add(ellipsis);
		else
			line[^1] = ellipsis;
	}
}
