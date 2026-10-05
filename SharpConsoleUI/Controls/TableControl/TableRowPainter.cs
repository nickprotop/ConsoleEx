// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Draws the lines a <see cref="TableControl"/> is made of: borders and separators, header and data
/// rows, the title and the filter status bar.
/// </summary>
/// <remarks>
/// Split out of the control so that what a row LOOKS like lives apart from which rows are shown,
/// in what order, and where the pointer is. The painter keeps no state between calls: everything
/// that depends on the table's configuration arrives in a <see cref="TableRowStyle"/> built once
/// per paint, and the filter footer's inputs in a <see cref="TableFilterStatus"/>.
/// </remarks>
internal static class TableRowPainter
{
	/// <summary>
	/// How far each of a truncated cell's last cells blends toward the background when
	/// <see cref="TableControl.TruncationFade"/> is on, nearest the text first. Its length is the
	/// number of cells that fade.
	/// </summary>
	private static readonly float[] TruncationFadeSteps = { 0.10f, 0.35f, 0.65f, 0.90f };

	/// <summary>
	/// Draws a horizontal border line (top, header separator, row separator, or bottom).
	/// </summary>
	internal static void DrawHorizontalLine(CharacterBuffer buffer, in TableRowStyle style, int x, int y,
		int[] colWidths, LayoutRect clipRect, char left, char middle, char right, char fill,
		int hScrollOffset = 0, int viewportWidth = int.MaxValue)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		int writeX = x;
		bool hasBorder = style.HasBorder;
		Color borderColor = style.BorderColor;
		Color bgColor = style.Background;
		int maxX = viewportWidth == int.MaxValue ? int.MaxValue : x + viewportWidth;

		// Left border char
		if (hasBorder && writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
		{
			Color bg = bgColor;
			buffer.SetNarrowCell(writeX, y, left, borderColor, bg);
		}
		writeX++;

		int colOffset = 0;
		for (int c = 0; c < colWidths.Length; c++)
		{
			int colEnd = colOffset + colWidths[c];

			// Fill column width with fill char
			for (int i = 0; i < colWidths[c]; i++)
			{
				int charPos = colOffset + i;
				if (charPos >= hScrollOffset && writeX < maxX)
				{
					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						Color bg = bgColor;
						buffer.SetNarrowCell(writeX, y, fill, borderColor, bg);
					}
					writeX++;
				}
				else if (charPos < hScrollOffset)
				{
					// Skip chars before scroll offset
				}
			}

			// Column separator
			if (c < colWidths.Length - 1)
			{
				if (colEnd >= hScrollOffset && writeX < maxX)
				{
					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						Color bg = bgColor;
						buffer.SetNarrowCell(writeX, y, middle, borderColor, bg);
					}
					writeX++;
				}
				colOffset = colEnd + (hasBorder ? 1 : 0);
			}
			else
			{
				colOffset = colEnd;
			}
		}

		// Right border char
		if (hasBorder && writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
		{
			Color bg = bgColor;
			buffer.SetNarrowCell(writeX, y, right, borderColor, bg);
		}
	}

	/// <summary>
	/// Draws a merged horizontal line (no column separators) — used for status bar borders.
	/// </summary>
	internal static void DrawMergedHorizontalLine(CharacterBuffer buffer, in TableRowStyle style, int x, int y,
		int[] colWidths, LayoutRect clipRect, char left, char right, char fill)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		Color borderColor = style.BorderColor;
		Color bgColor = style.Background;

		// Total inner width: all columns + inner borders
		int innerWidth = 0;
		foreach (int w in colWidths) innerWidth += w;
		innerWidth += colWidths.Length - 1; // inner column separators become fill chars

		int writeX = x;

		// Left border char
		if (writeX >= clipRect.X && writeX < clipRect.Right)
		{
			Color bg = bgColor;
			buffer.SetNarrowCell(writeX, y, left, borderColor, bg);
		}
		writeX++;

		// Fill the entire inner width
		for (int i = 0; i < innerWidth; i++)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = bgColor;
				buffer.SetNarrowCell(writeX, y, fill, borderColor, bg);
			}
			writeX++;
		}

		// Right border char
		if (writeX >= clipRect.X && writeX < clipRect.Right)
		{
			Color bg = bgColor;
			buffer.SetNarrowCell(writeX, y, right, borderColor, bg);
		}
	}

	/// <summary>
	/// Draws a data row with vertical borders and aligned cell text.
	/// </summary>
	internal static void DrawDataRow(CharacterBuffer buffer, in TableRowStyle style, int x, int y,
		int[] colWidths, LayoutRect clipRect, IList<string> cells, List<TableColumn>? cols,
		Color rowFg, Color rowBg,
		int hScrollOffset = 0, int viewportWidth = int.MaxValue,
		bool isSelected = false, int selectedCellIndex = -1, Color? selectedCellBg = null, Color? selectedCellFg = null,
		int editCellIndex = -1, int editCursorPos = -1,
		List<(int Column, int Start, int Length)>? filterMatches = null,
		int trailingFillWidth = 0)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		bool hasBorder = style.HasBorder;
		BoxChars box = style.Box;
		Color borderColor = style.BorderColor;
		Color borderBg = style.Background;

		int writeX = x;
		int maxX = viewportWidth == int.MaxValue ? int.MaxValue : x + viewportWidth;

		// Logical position along the row's full (unscrolled) content stream, counted from just after
		// the left border. Mirrors DrawHorizontalLine's colOffset/charPos scheme so inter-column
		// separators pan together with cell content; only characters at or past hScrollOffset are
		// actually written (and advance writeX) - earlier ones are skipped, shifting the visible
		// window left. The outer left/right border chars are intentionally NOT gated by this (kept
		// fixed), matching DrawHorizontalLine's treatment of its own border chars.
		int logicalPos = 0;

		void DrawChar(char ch, Color fg, Color bg)
		{
			if (logicalPos >= hScrollOffset)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
					buffer.SetNarrowCell(writeX, y, ch, fg, bg);
				writeX++;
			}
			logicalPos++;
		}

		void DrawFullCell(Cell cell)
		{
			if (logicalPos >= hScrollOffset)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
					buffer.SetCell(writeX, y, cell);
				writeX++;
			}
			logicalPos++;
		}

		if (hasBorder)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
			{
				Color bg = borderBg;
				buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
			}
			writeX++;
		}

		for (int c = 0; c < colWidths.Length; c++)
		{
			int colW = colWidths[c];
			string cellText = c < cells.Count ? cells[c] : string.Empty;
			bool isLastColumn = c == colWidths.Length - 1;

			TextJustification align = TextJustification.Left;
			if (cols != null && c < cols.Count)
				align = cols[c].Alignment;

			// Determine cell colors
			Color cellFg = rowFg;
			Color cellBg = rowBg;
			bool isEditCell = editCellIndex == c;
			if (isEditCell)
			{
				// Edit cell: use distinct edit colors
				cellBg = Color.White;
				cellFg = Color.Black;
			}
			else if (selectedCellIndex == c && selectedCellBg.HasValue)
			{
				cellBg = selectedCellBg.Value;
				cellFg = selectedCellFg ?? rowFg;
			}

			int cellLogicalStart = logicalPos;

			if (isEditCell)
			{
				// Render edit buffer as plain text with cursor using Unicode-aware width
				var editCells = MarkupParser.Parse(cellText, cellFg, cellBg);
				int visLen = editCells.Count;
				int cursorPos = editCursorPos;

				for (int i = 0; i < colW; i++)
				{
					Color fg = cellFg;
					Color bg = cellBg;
					// Draw cursor with inverted colors
					if (i == cursorPos)
					{
						fg = Color.White;
						bg = Color.Black;
					}
					if (i < visLen)
					{
						var srcCell = editCells[i];
						// If this is a wide base char at the last column position,
						// replace with space to avoid rendering half a glyph
						if (i == colW - 1 && !srcCell.IsWideContinuation
							&& Helpers.UnicodeWidth.IsWideRune(srcCell.Character))
						{
							DrawChar(' ', fg, bg);
						}
						else
						{
							var editCell = new Cell(srcCell.Character, fg, bg, srcCell.Decorations)
							{
								IsWideContinuation = srcCell.IsWideContinuation,
								Combiners = srcCell.Combiners
							};
							DrawFullCell(editCell);
						}
					}
					else
					{
						DrawChar(' ', fg, bg);
					}
				}
			}
			else
			{
				var cellCells = MarkupParser.Parse(cellText, cellFg, cellBg);
				int visLen = cellCells.Count;
				bool wasTruncated = visLen > colW;

				if (visLen > colW)
				{
					// Wide-character-aware truncation: if the last kept cell
					// is the base of a wide character (next cell is continuation),
					// replace it with a space to avoid rendering half a glyph.
					cellCells = cellCells.GetRange(0, colW);
					if (colW > 0 && colW < visLen)
					{
						var lastKept = cellCells[colW - 1];
						if (!lastKept.IsWideContinuation && visLen > colW)
						{
							// Check if original list had a continuation cell after this one
							// A wide base char always has IsWideContinuation on the next cell
							// We can detect it: if this cell's character is wide, it was split
							if (Helpers.UnicodeWidth.IsWideRune(lastKept.Character))
							{
								cellCells[colW - 1] = new Cell(' ', lastKept.Foreground, lastKept.Background);
							}
						}
					}
					visLen = colW;
				}

				int padLeft = 0;
				int padRight = colW - visLen;
				if (align == TextJustification.Center)
				{
					padLeft = (colW - visLen) / 2;
					padRight = colW - visLen - padLeft;
				}
				else if (align == TextJustification.Right)
				{
					padLeft = colW - visLen;
					padRight = 0;
				}


				int cellStartX = writeX;

				// Left padding
				for (int i = 0; i < padLeft; i++)
				{
					DrawChar(' ', cellFg, cellBg);
				}

				// Cell content - build match ranges for this column
				HashSet<int>? highlightIndices = null;
				if (filterMatches != null && !isSelected)
				{
					highlightIndices = new HashSet<int>();
					foreach (var match in filterMatches)
					{
						if (match.Column == c)
						{
							for (int hi = match.Start; hi < match.Start + match.Length; hi++)
								highlightIndices.Add(hi);
						}
					}
					if (highlightIndices.Count == 0) highlightIndices = null;
				}

				int charIdx = 0;
				foreach (var cell in cellCells)
				{
					// Override background for selected/hovered rows
					Color bg = isSelected ? cellBg : cell.Background;
					Color fg = isSelected ? cellFg : cell.Foreground;

					// Apply filter match highlight
					if (highlightIndices != null && highlightIndices.Contains(charIdx))
					{
						bg = Color.DarkYellow;
					}

					var bufCell = new Cell(cell.Character, fg, bg, cell.Decorations)
					{
						IsWideContinuation = cell.IsWideContinuation,
						Combiners = cell.Combiners
					};
					DrawFullCell(bufCell);
					charIdx++;
				}

				// Apply truncation fade if enabled and this cell was truncated. Skipped when the scroll
				// offset cuts into the middle of this same cell (cellStartX/colW would no longer bound
				// its actually-drawn span in the buffer, which could bleed the fade into the next column).
				int fadeCells = TruncationFadeSteps.Length;
				if (style.TruncationFade && wasTruncated && colW > fadeCells && cellLogicalStart >= hScrollOffset)
				{
					int fadeStart = cellStartX + colW - fadeCells;
					for (int fi = 0; fi < fadeCells; fi++)
					{
						int fx = fadeStart + fi;
						if (fx >= clipRect.X && fx < clipRect.Right)
						{
							var existing = buffer.GetCell(fx, y);
							var fadedFg = ColorBlendHelper.BlendColor(existing.Foreground, existing.Background, TruncationFadeSteps[fi]);
							buffer.SetCellColors(fx, y, fadedFg, existing.Background);
						}
					}
				}

				// Right padding
				for (int i = 0; i < padRight; i++)
				{
					DrawChar(' ', cellFg, cellBg);
				}
			}

			// Column separator / right border. The final column's trailing border char is the table's
			// right edge and, like the left border, stays fixed rather than panning with scroll.
			if (hasBorder)
			{
				if (isLastColumn)
				{
					if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
					{
						Color bg = borderBg;
						buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
					}
					writeX++;
				}
				else
				{
					DrawChar(box.Vertical, borderColor, borderBg);
				}
			}
			else if (style.ColumnSeparator.HasValue && c < colWidths.Length - 1
				&& !(style.CheckboxMode && c == 0))
			{
				var sepColor = style.ColumnSeparatorColor ?? borderColor;
				// Padded separators get a leading and trailing space (" │ ") for breathing room;
				// flush separators are just the glyph. SeparatorWidth keeps the width budget in sync.
				if (style.ColumnSeparatorPadded)
				{
					DrawChar(' ', cellFg, rowBg);
				}
				DrawChar(style.ColumnSeparator.Value, sepColor, rowBg);
				if (style.ColumnSeparatorPadded)
				{
					DrawChar(' ', cellFg, rowBg);
				}
			}
		}

		// Trailing scrollbar gutter: blank cells in the row's own background so a selected/hovered
		// row extends cleanly up to (but not under) the scrollbar instead of stopping at the last column.
		for (int i = 0; i < trailingFillWidth; i++)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
				buffer.SetNarrowCell(writeX, y, ' ', rowFg, rowBg);
			writeX++;
		}
	}

	/// <summary>
	/// Draws a title row centered above the table.
	/// </summary>
	internal static void DrawTitleRow(CharacterBuffer buffer, int x, int y, int totalWidth, LayoutRect clipRect,
		string? title, TextJustification titleAlignment, Color fgColor, Color bgColor)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom || string.IsNullOrEmpty(title)) return;

		var titleCells = MarkupParser.Parse(title, fgColor, bgColor);
		int titleLen = titleCells.Count;

		for (int i = 0; i < totalWidth; i++)
		{
			int px = x + i;
			if (px >= clipRect.X && px < clipRect.Right)
			{
				Color bg = bgColor;
				buffer.SetNarrowCell(px, y, ' ', fgColor, bg);
			}
		}

		int offset = 0;
		switch (titleAlignment)
		{
			case TextJustification.Center:
				offset = Math.Max(0, (totalWidth - titleLen) / 2);
				break;
			case TextJustification.Right:
				offset = Math.Max(0, totalWidth - titleLen);
				break;
		}

		for (int i = 0; i < titleLen && offset + i < totalWidth; i++)
		{
			int px = x + offset + i;
			if (px >= clipRect.X && px < clipRect.Right)
			{
				Color bg = titleCells[i].Background;
				var titleCell = new Cell(titleCells[i].Character, titleCells[i].Foreground, bg, titleCells[i].Decorations)
				{
					IsWideContinuation = titleCells[i].IsWideContinuation,
					Combiners = titleCells[i].Combiners
				};
				buffer.SetCell(px, y, titleCell);
			}
		}
	}

	/// <summary>
	/// Draws the filter status bar row with colored segments.
	/// </summary>
	internal static void DrawFilterStatusBar(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect,
		in TableFilterStatus status, Color fgColor, Color bgColor, BoxChars box, Color borderColor, bool hasBorder)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		// Build segments: (text, foreground color)
		var segments = new List<(string Text, Color Fg)>();

		switch (status.Mode)
		{
			case FilterMode.Typing:
				segments.Add((" ⌕ ", Color.Cyan1));                          // ⌕ filter icon
				segments.Add((status.Buffer, Color.White));
				segments.Add(("▁", Color.White));                             // cursor block
				segments.Add(("  ", fgColor));
				segments.Add(("Enter", Color.Yellow));
				segments.Add((" confirm  ", Color.Grey));
				segments.Add(("Esc", Color.Yellow));
				segments.Add((" cancel", Color.Grey));
				break;

			case FilterMode.Confirmed:
				// RowCount, not the display map's length. A source that filters itself narrows its own
				// RowCount and leaves the display map null by design, so counting the map reported
				// "No matches" while the matching rows were on screen right above this footer.
				// RowCount already resolves all three cases: display map, data source, own rows.
				int filteredCount = status.RowCount;
				segments.Add((" ⌕ ", Color.Cyan1));
				segments.Add((status.FilterText, Color.White));
				segments.Add(("  ", fgColor));
				if (filteredCount == 0)
				{
					segments.Add(("No matches", Color.Red));
				}
				else
				{
					segments.Add(($"{filteredCount}", Color.Green));
					segments.Add(($"/{status.TotalRows} rows", Color.Grey));
				}
				segments.Add(("  ", fgColor));
				segments.Add(("Esc", Color.Yellow));
				segments.Add((" clear", Color.Grey));
				break;

			default:
				int selRow = status.SelectedRowIndex >= 0 ? status.SelectedRowIndex + 1 : 0;
				int rowCount = status.RowCount;
				segments.Add(($" Row {selRow}/{rowCount}", Color.Grey50));
				segments.Add(("  ", fgColor));
				segments.Add(("/", Color.Yellow));
				segments.Add((" filter", Color.Grey50));
				break;
		}

		// Render: left border, content, right border
		int writeX = x;

		if (hasBorder)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = bgColor;
				buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
			}
			writeX++;
		}

		// Content area
		int contentWidth = width - (hasBorder ? 2 : 0);
		int charPos = 0;

		// Track cursor position for typing mode highlight
		int cursorCharPos = status.Mode == FilterMode.Typing ? UnicodeWidth.GetStringWidth(" ⌕ ") + status.CursorPosition : -1;

		foreach (var (text, fg) in segments)
		{
			foreach (var rune in text.EnumerateRunes())
			{
				int runeWidth = UnicodeWidth.GetRuneWidth(rune);
				if (runeWidth == 0) continue; // skip zero-width characters
				if (charPos >= contentWidth) break;
				if (writeX >= clipRect.X && writeX < clipRect.Right)
				{
					Color cellFg = fg;
					Color cellBg = bgColor;

					// Highlight cursor position in typing mode
					if (charPos == cursorCharPos)
					{
						cellFg = Color.Black;
						cellBg = Color.White;
					}

					buffer.SetNarrowCell(writeX, y, rune, cellFg, cellBg);

					// Wide character: mark continuation cell
					if (runeWidth == 2 && charPos + 1 < contentWidth)
					{
						var cont = new Cell(' ', cellFg, cellBg) { IsWideContinuation = true };
						if (writeX + 1 >= clipRect.X && writeX + 1 < clipRect.Right)
							buffer.SetCell(writeX + 1, y, cont);
						writeX++;
						charPos++;
					}
				}
				writeX++;
				charPos++;
			}
			if (charPos >= contentWidth) break;
		}

		// Fill remaining space
		while (charPos < contentWidth)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = bgColor;
				buffer.SetNarrowCell(writeX, y, ' ', fgColor, bg);
			}
			writeX++;
			charPos++;
		}

		if (hasBorder)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = bgColor;
				buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
			}
		}
	}
}

/// <summary>
/// The table settings every line the <see cref="TableRowPainter"/> draws depends on, captured once
/// per paint.
/// </summary>
internal readonly record struct TableRowStyle(
	bool HasBorder,
	BoxChars Box,
	Color BorderColor,
	Color Background,
	char? ColumnSeparator,
	Color? ColumnSeparatorColor,
	bool ColumnSeparatorPadded,
	bool CheckboxMode,
	bool TruncationFade);

/// <summary>
/// What the filter status bar shows, captured from the table at paint time.
/// </summary>
/// <param name="Mode">The filter mode, which picks the bar's layout.</param>
/// <param name="Buffer">The text typed so far.</param>
/// <param name="CursorPosition">The typing cursor's position in <paramref name="Buffer"/>.</param>
/// <param name="FilterText">The confirmed filter's text.</param>
/// <param name="RowCount">The rows displayed now.</param>
/// <param name="TotalRows">The rows there are before filtering.</param>
/// <param name="SelectedRowIndex">The cursor's display index, or -1.</param>
internal readonly record struct TableFilterStatus(
	FilterMode Mode,
	string Buffer,
	int CursorPosition,
	string FilterText,
	int RowCount,
	int TotalRows,
	int SelectedRowIndex);
