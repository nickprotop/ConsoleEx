// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using AngleSharp.Dom;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Renders HTML table elements with box-drawing borders and proportional column widths.
	/// </summary>
	public static class HtmlTableLayout
	{
		private struct TableCell
		{
			public string Content;
			public bool IsHeader;
		}

		/// <summary>
		/// Lays out an HTML table element into LayoutLines with box-drawing borders.
		/// </summary>
		public static LayoutLine[] LayoutTable(
			IElement tableElement,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			Color? linkColor = null)
		{
			// 1. Extract rows and cells
			var rows = ExtractRows(tableElement);
			if (rows.Count == 0)
				return Array.Empty<LayoutLine>();

			// 2. Normalize column count
			int colCount = 0;
			foreach (var row in rows)
			{
				if (row.Count > colCount)
					colCount = row.Count;
			}

			if (colCount == 0)
				return Array.Empty<LayoutLine>();

			// Pad rows to same column count
			for (int r = 0; r < rows.Count; r++)
			{
				while (rows[r].Count < colCount)
					rows[r].Add(new TableCell { Content = "", IsHeader = false });
			}

			// 3. Calculate column widths
			int borderChars = colCount + 1; // │ at each edge and between columns
			int available = maxWidth - borderChars;
			if (available < colCount)
				available = colCount; // minimum 1 char per column

			var colWidths = CalculateColumnWidths(rows, colCount, available);

			// 4. Render
			var borderColor = HtmlConstants.DefaultTableBorderColor;
			var result = new List<LayoutLine>();

			// Top border: ┌─┬─┐
			result.Add(MakeBorderLine(colWidths, borderColor, defaultBg,
				HtmlConstants.TableTopLeft, HtmlConstants.TableHorizontal,
				HtmlConstants.TableTopTee, HtmlConstants.TableTopRight));

			for (int r = 0; r < rows.Count; r++)
			{
				// Wrap cell content for each column
				var wrappedCells = new List<string[]>();
				int maxLines = 1;

				for (int c = 0; c < colCount; c++)
				{
					var wrapped = WrapText(rows[r][c].Content, colWidths[c]);
					wrappedCells.Add(wrapped);
					if (wrapped.Length > maxLines)
						maxLines = wrapped.Length;
				}

				// Render each line of the row
				for (int line = 0; line < maxLines; line++)
				{
					var cells = new List<Cell>();

					for (int c = 0; c < colCount; c++)
					{
						// Left border or separator
						cells.Add(new Cell(HtmlConstants.TableVertical, borderColor, defaultBg));

						var text = line < wrappedCells[c].Length ? wrappedCells[c][line] : "";
						var isHeader = rows[r][c].IsHeader;
						var decorations = isHeader ? TextDecoration.Bold : TextDecoration.None;

						// Render exactly colWidths[c] columns of cells (with continuations for wide chars).
						// Skip zero-width runes (control chars, combining marks) — storing them as
						// cells would advance the buffer cursor without advancing column width,
						// mis-aligning the right border.
						int colsUsed = 0;
						foreach (var rune in text.EnumerateRunes())
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (rw <= 0) continue;
							if (colsUsed + rw > colWidths[c]) break;
							cells.Add(new Cell(rune, defaultFg, defaultBg, decorations));
							colsUsed += rw;
							if (rw == 2)
								cells.Add(new Cell(' ', defaultFg, defaultBg) { IsWideContinuation = true });
						}
						// Pad remaining columns with spaces
						while (colsUsed < colWidths[c])
						{
							cells.Add(new Cell(' ', defaultFg, defaultBg));
							colsUsed++;
						}
					}

					// Right border
					cells.Add(new Cell(HtmlConstants.TableVertical, borderColor, defaultBg));

					result.Add(new LayoutLine(0, 0, cells.Count, cells.ToArray(), TextAlignment.Left));
				}

				// Row separator (between rows, not after last)
				if (r < rows.Count - 1)
				{
					result.Add(MakeBorderLine(colWidths, borderColor, defaultBg,
						HtmlConstants.TableLeftTee, HtmlConstants.TableHorizontal,
						HtmlConstants.TableCross, HtmlConstants.TableRightTee));
				}
			}

			// Bottom border: └─┴─┘
			result.Add(MakeBorderLine(colWidths, borderColor, defaultBg,
				HtmlConstants.TableBottomLeft, HtmlConstants.TableHorizontal,
				HtmlConstants.TableBottomTee, HtmlConstants.TableBottomRight));

			return result.ToArray();
		}

		private static List<List<TableCell>> ExtractRows(IElement tableElement)
		{
			var rows = new List<List<TableCell>>();
			CollectRows(tableElement, rows, isRoot: true);
			return rows;
		}

		/// <summary>
		/// Recursively walks children of <paramref name="node"/> collecting &lt;tr&gt; rows that
		/// belong to this table, while skipping any descendant &lt;table&gt; (nested tables are
		/// rendered as text inside their parent cell instead of having their rows hoisted).
		/// </summary>
		private static void CollectRows(INode node, List<List<TableCell>> rows, bool isRoot)
		{
			foreach (var child in node.ChildNodes)
			{
				if (child is not IElement el)
					continue;

				var tag = el.LocalName.ToLowerInvariant();

				// Do not descend into a nested <table>; its rows belong to it, not us.
				if (!isRoot && tag == "table")
					continue;

				if (tag == "tr")
				{
					var row = new List<TableCell>();
					foreach (var cellEl in el.Children)
					{
						var cellTag = cellEl.LocalName.ToLowerInvariant();
						if (cellTag == "td" || cellTag == "th")
						{
							row.Add(new TableCell
							{
								Content = GetVisibleText(cellEl),
								IsHeader = cellTag == "th",
							});
						}
					}
					rows.Add(row);
					continue;
				}

				// Recurse into structural wrappers (thead, tbody, tfoot, caption, colgroup, etc.)
				CollectRows(el, rows, isRoot: false);
			}
		}

		/// <summary>
		/// Extracts visible text from an element, excluding script/style content, and
		/// collapses all ASCII whitespace runs (spaces, tabs, newlines, carriage returns,
		/// form feeds) into a single space — mirroring HTML's whitespace-collapsing rules.
		/// Control characters must never be stored in Cells because they corrupt the
		/// terminal output stream.
		/// </summary>
		private static string GetVisibleText(IElement element)
		{
			var sb = new System.Text.StringBuilder();
			CollectVisibleText(element, sb);
			return CollapseWhitespace(sb.ToString());
		}

		private static void CollectVisibleText(INode node, System.Text.StringBuilder sb)
		{
			foreach (var child in node.ChildNodes)
			{
				if (child is IText text)
				{
					sb.Append(text.Data);
				}
				else if (child is IElement el)
				{
					var tag = el.LocalName.ToLowerInvariant();
					if (tag is "script" or "style" or "noscript")
						continue;
					// <br> introduces a break — use a space so adjacent words don't concatenate.
					if (tag == "br")
					{
						sb.Append(' ');
						continue;
					}
					CollectVisibleText(el, sb);
				}
			}
		}

		/// <summary>
		/// Collapses runs of ASCII whitespace into a single space and trims edges.
		/// </summary>
		private static string CollapseWhitespace(string s)
		{
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			var sb = new System.Text.StringBuilder(s.Length);
			bool lastWasSpace = true; // trims leading whitespace
			foreach (var ch in s)
			{
				bool isWs = ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == '\f' || ch == '\v';
				if (isWs)
				{
					if (!lastWasSpace)
					{
						sb.Append(' ');
						lastWasSpace = true;
					}
				}
				else
				{
					sb.Append(ch);
					lastWasSpace = false;
				}
			}
			// Trim trailing space
			if (sb.Length > 0 && sb[^1] == ' ')
				sb.Length -= 1;
			return sb.ToString();
		}

		private static int[] CalculateColumnWidths(
			List<List<TableCell>> rows,
			int colCount,
			int available)
		{
			var minWidths = new int[colCount];
			var prefWidths = new int[colCount];

			for (int c = 0; c < colCount; c++)
			{
				int minW = 1;
				int prefW = 1;

				foreach (var row in rows)
				{
					var content = row[c].Content;
					if (string.IsNullOrEmpty(content))
						continue;

					// Min width = longest word (in column width, not chars)
					var words = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var word in words)
					{
						int wordCols = word.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
						if (wordCols > minW)
							minW = wordCols;
					}

					// Preferred width = full content column width
					int contentCols = content.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
					if (contentCols > prefW)
						prefW = contentCols;
				}

				minWidths[c] = minW;
				prefWidths[c] = prefW;
			}

			var widths = new int[colCount];
			int totalPref = 0;
			int totalMin = 0;
			for (int c = 0; c < colCount; c++)
			{
				totalPref += prefWidths[c];
				totalMin += minWidths[c];
			}

			if (totalPref <= available)
			{
				// Use preferred, distribute excess evenly
				int excess = available - totalPref;
				int perCol = excess / colCount;
				int remainder = excess % colCount;

				for (int c = 0; c < colCount; c++)
				{
					widths[c] = prefWidths[c] + perCol + (c < remainder ? 1 : 0);
				}
			}
			else if (totalMin >= available)
			{
				// Equal distribution
				int perCol = available / colCount;
				int remainder = available % colCount;
				for (int c = 0; c < colCount; c++)
				{
					widths[c] = perCol + (c < remainder ? 1 : 0);
				}
			}
			else
			{
				// Proportional from preferred, respecting minimums
				// First assign minimums
				for (int c = 0; c < colCount; c++)
					widths[c] = minWidths[c];

				int remaining = available - totalMin;

				// Distribute remaining proportionally based on (pref - min)
				int totalExcess = totalPref - totalMin;
				if (totalExcess > 0)
				{
					for (int c = 0; c < colCount; c++)
					{
						int extra = (int)Math.Round((double)(prefWidths[c] - minWidths[c]) / totalExcess * remaining);
						widths[c] += extra;
					}

					// Fix rounding: adjust to match available. Keep iterating until diff == 0
					// — a single pass can leave leftovers because the reduce path skips columns
					// already at their minimum, and the add path is capped by column count.
					int total = 0;
					for (int c = 0; c < colCount; c++)
						total += widths[c];

					int diff = available - total;
					int guard = colCount * 4; // safety: bounded iterations
					while (diff != 0 && guard-- > 0)
					{
						bool changed = false;
						for (int c = 0; c < colCount && diff != 0; c++)
						{
							if (diff > 0) { widths[c]++; diff--; changed = true; }
							else if (diff < 0 && widths[c] > minWidths[c]) { widths[c]--; diff++; changed = true; }
						}
						if (!changed) break; // cannot reduce further — all columns at min
					}
				}
			}

			// Ensure minimum of 1
			for (int c = 0; c < colCount; c++)
			{
				if (widths[c] < 1)
					widths[c] = 1;
			}

			return widths;
		}

		private static LayoutLine MakeBorderLine(
			int[] colWidths,
			Color borderColor,
			Color bg,
			char left,
			char horizontal,
			char junction,
			char right)
		{
			var cells = new List<Cell>();
			cells.Add(new Cell(left, borderColor, bg));

			for (int c = 0; c < colWidths.Length; c++)
			{
				for (int i = 0; i < colWidths[c]; i++)
					cells.Add(new Cell(horizontal, borderColor, bg));

				if (c < colWidths.Length - 1)
					cells.Add(new Cell(junction, borderColor, bg));
			}

			cells.Add(new Cell(right, borderColor, bg));

			return new LayoutLine(0, 0, cells.Count, cells.ToArray(), TextAlignment.Left);
		}

		private static int StringColumnWidth(string s)
		{
			return s.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
		}

		private static string[] WrapText(string text, int width)
		{
			if (string.IsNullOrEmpty(text))
				return new[] { "" };

			if (width <= 0)
				width = 1;

			var lines = new List<string>();
			var words = text.Split(' ');
			var currentLine = "";
			int currentCols = 0;

			foreach (var word in words)
			{
				int wordCols = StringColumnWidth(word);

				if (currentCols == 0)
				{
					if (wordCols > width)
					{
						// Break long word by column width
						var sb = new System.Text.StringBuilder();
						int cols = 0;
						foreach (var rune in word.EnumerateRunes())
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (cols + rw > width && sb.Length > 0)
							{
								lines.Add(sb.ToString());
								sb.Clear();
								cols = 0;
							}
							sb.Append(rune);
							cols += rw;
						}
						currentLine = sb.ToString();
						currentCols = cols;
					}
					else
					{
						currentLine = word;
						currentCols = wordCols;
					}
				}
				else if (currentCols + 1 + wordCols <= width)
				{
					currentLine += " " + word;
					currentCols += 1 + wordCols;
				}
				else
				{
					lines.Add(currentLine);
					if (wordCols > width)
					{
						var sb = new System.Text.StringBuilder();
						int cols = 0;
						foreach (var rune in word.EnumerateRunes())
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (cols + rw > width && sb.Length > 0)
							{
								lines.Add(sb.ToString());
								sb.Clear();
								cols = 0;
							}
							sb.Append(rune);
							cols += rw;
						}
						currentLine = sb.ToString();
						currentCols = cols;
					}
					else
					{
						currentLine = word;
						currentCols = wordCols;
					}
				}
			}

			if (currentLine.Length > 0)
				lines.Add(currentLine);

			if (lines.Count == 0)
				lines.Add("");

			return lines.ToArray();
		}

		private static string PadRight(string text, int width)
		{
			// Measure column width accounting for wide characters
			int colWidth = 0;
			int charCount = 0;
			foreach (var rune in text.EnumerateRunes())
			{
				int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
				if (colWidth + rw > width) break;
				colWidth += rw;
				charCount += rune.Utf16SequenceLength;
			}

			var truncated = charCount < text.Length ? text[..charCount] : text;
			int padding = width - colWidth;
			return padding > 0 ? truncated + new string(' ', padding) : truncated;
		}
	}
}
