// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	#region Rendering Helpers

	/// <summary>
	/// Draws a horizontal border line (top, header separator, row separator, or bottom).
	/// </summary>
	private void DrawHorizontalLine(CharacterBuffer buffer, int x, int y, int[] colWidths, LayoutRect clipRect,
		BoxChars box, Color borderColor, Color bgColor, char left, char middle, char right, char fill,
		int hScrollOffset = 0, int viewportWidth = int.MaxValue)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		int writeX = x;
		bool hasBorder = _borderStyle != BorderStyle.None;
		int contentStartX = x + (hasBorder ? 1 : 0);
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
	private void DrawMergedHorizontalLine(CharacterBuffer buffer, int x, int y, int[] colWidths, LayoutRect clipRect,
		BoxChars box, Color borderColor, Color bgColor, char left, char right, char fill)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

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
	private void DrawDataRow(CharacterBuffer buffer, int x, int y, int[] colWidths, LayoutRect clipRect,
		BoxChars box, Color borderColor, Color borderBg, IList<string> cells, List<TableColumn>? cols,
		Color rowFg, Color rowBg, bool hasBorder,
		int hScrollOffset = 0, int viewportWidth = int.MaxValue,
		bool isSelected = false, int selectedCellIndex = -1, Color? selectedCellBg = null, Color? selectedCellFg = null,
		int editCellIndex = -1, int editCursorPos = -1,
		List<(int Column, int Start, int Length)>? filterMatches = null,
		int trailingFillWidth = 0)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

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
				if (_truncationFade && wasTruncated && colW > 4 && cellLogicalStart >= hScrollOffset)
				{
					float[] fadeSteps = { 0.10f, 0.35f, 0.65f, 0.90f };
					int fadeStart = cellStartX + colW - 4;
					for (int fi = 0; fi < 4; fi++)
					{
						int fx = fadeStart + fi;
						if (fx >= clipRect.X && fx < clipRect.Right)
						{
							var existing = buffer.GetCell(fx, y);
							var fadedFg = ColorBlendHelper.BlendColor(existing.Foreground, existing.Background, fadeSteps[fi]);
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
			else if (_columnSeparator.HasValue && c < colWidths.Length - 1
				&& !(_checkboxMode && c == 0))
			{
				var sepColor = _columnSeparatorColor ?? borderColor;
				// Padded separators get a leading and trailing space (" │ ") for breathing room;
				// flush separators are just the glyph. SeparatorWidth keeps the width budget in sync.
				if (_columnSeparatorPadded)
				{
					DrawChar(' ', cellFg, rowBg);
				}
				DrawChar(_columnSeparator.Value, sepColor, rowBg);
				if (_columnSeparatorPadded)
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
	private void DrawTitleRow(CharacterBuffer buffer, int x, int y, int totalWidth, LayoutRect clipRect,
		Color fgColor, Color bgColor)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom || string.IsNullOrEmpty(_title)) return;

		var titleCells = MarkupParser.Parse(_title, fgColor, bgColor);
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
		switch (_titleAlignment)
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

	#endregion

	#region IDOMPaintable Implementation

	/// <inheritdoc/>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		int colCount;
		List<TableColumn>? colSnapshot = null;
		List<TableRow>? rowSnapshot = null;

		if (_dataSource != null)
		{
			colCount = _dataSource.ColumnCount;
		}
		else
		{
			lock (_tableLock)
			{
				colSnapshot = _columns.ToList();
				rowSnapshot = _rows.ToList();
				colCount = colSnapshot.Count;
			}
		}

		int targetWidth = Width ?? constraints.MaxWidth;
		int contentWidth = targetWidth - Margin.Left - Margin.Right;

		// Reserve space for vertical scrollbar (+ optional gutter before it)
		if (ShouldShowVerticalScrollbar())
			contentWidth = Math.Max(1, contentWidth - 1 - ScrollbarGutterWidth);

		// Reserve space for checkbox column
		int cbWidth = _checkboxMode ? 4 : 0;
		bool hasBorder = _borderStyle != BorderStyle.None;
		int dataContentWidth2 = contentWidth;
		if (_checkboxMode)
			dataContentWidth2 = Math.Max(1, contentWidth - cbWidth - (hasBorder ? 1 : 0));

		int[] colWidths;
		if (_dataSource != null)
			colWidths = ComputeColumnWidthsFromDataSource(dataContentWidth2, _scrollOffset, GetVisibleRowCount());
		else
			colWidths = ComputeColumnWidths(dataContentWidth2, colSnapshot!, rowSnapshot!, _scrollOffset, GetVisibleRowCount());

		int borderOverhead = hasBorder ? (colCount + 1)
			: (_columnSeparator.HasValue ? Math.Max(0, colCount - 1) * SeparatorWidth : 0);
		int measuredWidth = cbWidth + (cbWidth > 0 && hasBorder ? 1 : 0);
		foreach (int w in colWidths) measuredWidth += w;
		measuredWidth += borderOverhead;

		// Add scrollbar width (+ optional gutter before it)
		if (ShouldShowVerticalScrollbar())
			measuredWidth += 1 + ScrollbarGutterWidth;

		if (!string.IsNullOrEmpty(_title))
		{
			int titleWidth = _measurementCache.GetCachedLength(_title);
			if (titleWidth > measuredWidth)
				measuredWidth = titleWidth;
		}

		// Calculate height
		int rowCount = RowCount;
		int height = 0;
		if (!string.IsNullOrEmpty(_title)) height++;
		if (hasBorder) height++; // top border
		if (_showHeader) height++;
		if (_showHeader && hasBorder) height++; // header separator

		// Determine visible rows: explicit height > constraint-based > all rows
		int visibleRows;
		if (_height.HasValue)
		{
			visibleRows = CalculateVisibleRowsFromHeight(_height.Value);
		}
		else if (!_readOnly && constraints.MaxHeight < int.MaxValue)
		{
			// Interactive table: respect container constraint so internal scrolling works
			visibleRows = Math.Min(rowCount, CalculateVisibleRowsFromHeight(constraints.MaxHeight));
		}
		else
		{
			visibleRows = rowCount;
		}
		height += Math.Min(rowCount, visibleRows);
		if (_showRowSeparators && hasBorder)
		{
			int visibleDataRows = Math.Min(rowCount, visibleRows);
			if (visibleDataRows > 1) height += visibleDataRows - 1;
		}

		// Filter status bar
		if (_filteringEnabled && !_readOnly)
			height += 2; // separator + status row

		if (hasBorder) height++; // bottom border

		// Horizontal scrollbar
		if (ShouldShowHorizontalScrollbar())
			height++;

		int width;
		if (Width.HasValue)
			width = Width.Value + Margin.Left + Margin.Right;
		else if (HorizontalAlignment == HorizontalAlignment.Stretch)
			width = constraints.MaxWidth;
		else
			width = measuredWidth + Margin.Left + Margin.Right;

		height += Margin.Top + Margin.Bottom;

		// VerticalAlignment.Fill: take all offered vertical space (then scroll internally
		// for any overflow) instead of capping at content size. Only applies when a bounded
		// height is offered and no explicit Height was set — content-sized tables and tables
		// with an explicit Height keep their existing behavior.
		if (VerticalAlignment == VerticalAlignment.Fill
			&& !_height.HasValue
			&& constraints.MaxHeight < int.MaxValue
			&& height < constraints.MaxHeight)
		{
			height = constraints.MaxHeight;
		}

		return new LayoutSize(
			Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
		);
	}

	/// <inheritdoc/>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		SetActualBounds(bounds);

		Color bgColor = ResolveBackgroundColor(defaultBg);
		Color fgColor = ResolveForegroundColor(defaultFg);
		Color borderColor = ResolveBorderColor();
		Color headerBg = ResolveHeaderBackgroundColor();
		Color headerFg = ResolveHeaderForegroundColor();
		var effectiveBg = (_backgroundColorValue == null || _backgroundColorValue == Color.Default) ? Color.Transparent : bgColor;

		// Fill margins
		ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, bounds.Y + Margin.Top, fgColor, effectiveBg);

		int colCount;
		List<TableColumn>? colSnapshot = null;
		List<TableRow>? rowSnapshot = null;

		if (_dataSource != null)
		{
			colCount = _dataSource.ColumnCount;
		}
		else
		{
			lock (_tableLock)
			{
				colSnapshot = _columns.ToList();
				rowSnapshot = _rows.ToList();
				colCount = colSnapshot.Count;
			}
		}

		int targetWidth = bounds.Width - Margin.Left - Margin.Right;

		// Determine scrollbar visibility
		bool showVScrollbar = ShouldShowVerticalScrollbar();
		int contentWidth = targetWidth;
		if (showVScrollbar)
			contentWidth = Math.Max(1, contentWidth - 1);

		if (contentWidth <= 0 || colCount == 0)
		{
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, effectiveBg);
			return;
		}

		// Optional gutter between the column content and the scrollbar: the columns are laid out into
		// columnContentWidth (one cell narrower), leaving the cell at startX + columnContentWidth blank,
		// while the scrollbar stays at startX + contentWidth on the far right.
		int scrollbarGutter = ScrollbarGutterWidth;
		int columnContentWidth = Math.Max(1, contentWidth - scrollbarGutter);

		// Reserve space for checkbox column before computing data column widths
		int checkboxColWidth = _checkboxMode ? 4 : 0;
		int dataContentWidth = columnContentWidth;
		if (_checkboxMode)
		{
			bool hasBorderForCb = _borderStyle != BorderStyle.None;
			dataContentWidth = Math.Max(1, columnContentWidth - checkboxColWidth - (hasBorderForCb ? 1 : 0));
		}

		int[] dataColWidths;
		if (_dataSource != null)
			dataColWidths = ComputeColumnWidthsFromDataSource(dataContentWidth, _scrollOffset, GetVisibleRowCount());
		else
			dataColWidths = ComputeColumnWidths(dataContentWidth, colSnapshot!, rowSnapshot!, _scrollOffset, GetVisibleRowCount());

		// Prepend silent checkbox column to colWidths so all drawing infrastructure handles it
		int[] colWidths;
		if (_checkboxMode)
		{
			colWidths = new int[dataColWidths.Length + 1];
			colWidths[0] = checkboxColWidth;
			Array.Copy(dataColWidths, 0, colWidths, 1, dataColWidths.Length);
		}
		else
		{
			colWidths = dataColWidths;
		}

		int totalColumnsWidth = GetTotalColumnsWidth(colWidths);
		bool showHScrollbar = ShouldShowHorizontalScrollbar(totalColumnsWidth, contentWidth);

		// Clamp locally rather than mutating _horizontalScrollOffset here - the field is authoritatively
		// clamped by the mouse drag/wheel handlers; this just guards against a stale offset from before
		// a resize/data change made the content narrower than it used to be.
		int maxHScroll = Math.Max(0, totalColumnsWidth - contentWidth);
		int effectiveHScroll = Math.Min(_horizontalScrollOffset, maxHScroll);

		int startX = bounds.X + Margin.Left;
		int currentY = bounds.Y + Margin.Top;
		int maxY = bounds.Bottom - Margin.Bottom;
		if (showHScrollbar) maxY--; // reserve row for horizontal scrollbar

		bool hasBorder = _borderStyle != BorderStyle.None;
		var box = GetBoxChars();

		// Selection colors
		Color selBg = ResolveSelectionBackgroundColor();
		Color selFg = ResolveSelectionForegroundColor();
		Color unfocusedSelBg = ResolveUnfocusedSelectionBackgroundColor();
		Color unfocusedSelFg = ResolveUnfocusedSelectionForegroundColor();
		Color hoverBg = ResolveHoverBackgroundColor();
		Color hoverFg = ResolveHoverForegroundColor();

		void FillSideMargins(int y)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;
			if (Margin.Left > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, effectiveBg);
			if (Margin.Right > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, effectiveBg);
		}

		// Title row
		if (!string.IsNullOrEmpty(_title) && currentY < maxY)
		{
			FillSideMargins(currentY);
			DrawTitleRow(buffer, startX, currentY, contentWidth, clipRect, headerFg, effectiveBg);
			currentY++;
		}

		// Build render-time column list with dummy entry for checkbox column
		List<TableColumn>? renderCols = colSnapshot;
		if (_checkboxMode && colSnapshot != null)
		{
			renderCols = new List<TableColumn>(colSnapshot.Count + 1);
			renderCols.Add(new TableColumn { Header = "", Alignment = TextJustification.Left, Width = 4 });
			renderCols.AddRange(colSnapshot);
		}

		// Top border
		if (hasBorder && currentY < maxY)
		{
			FillSideMargins(currentY);
			DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
				box.TopLeft, box.TopTee, box.TopRight, box.Horizontal, hScrollOffset: effectiveHScroll);
			currentY++;
		}

		// Header row
		if (_showHeader && currentY < maxY)
		{
			FillSideMargins(currentY);

			List<string> headerCells;
			if (_dataSource != null)
			{
				headerCells = new List<string>();
				for (int c = 0; c < colCount; c++)
				{
					string header = _dataSource.GetColumnHeader(c);
					// Append sort indicator
					if (_sortingEnabled && _sortColumnIndex == c)
					{
						header += _sortDirection == SortDirection.Ascending ? " \u25b2" : " \u25bc";
					}
					headerCells.Add(header);
				}
			}
			else
			{
				headerCells = new List<string>();
				for (int c = 0; c < colSnapshot!.Count; c++)
				{
					string header = colSnapshot[c].Header;
					if (_sortingEnabled && _sortColumnIndex == c)
					{
						header += _sortDirection == SortDirection.Ascending ? " \u25b2" : " \u25bc";
					}
					headerCells.Add(header);
				}
			}

			// Prepend empty cell for checkbox column
			if (_checkboxMode)
				headerCells.Insert(0, "");

			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
				headerCells, renderCols, headerFg, headerBg, hasBorder,
				hScrollOffset: effectiveHScroll, trailingFillWidth: scrollbarGutter);

			// Update column rendered positions for hit testing — skip checkbox column
			{
				int colX = startX + (hasBorder ? 1 : 0);
				// Skip past checkbox column in colWidths
				if (_checkboxMode)
					colX += checkboxColWidth + (hasBorder ? 1 : 0);
				_renderedColumnX = new int[colCount];
				_renderedColumnWidths = new int[colCount];
				for (int c = 0; c < colCount; c++)
				{
					_renderedColumnX[c] = colX;
					_renderedColumnWidths[c] = dataColWidths[c];
					if (colSnapshot != null && c < colSnapshot.Count)
					{
						colSnapshot[c].RenderedX = colX;
						colSnapshot[c].RenderedWidth = dataColWidths[c];
					}
					bool addSep = hasBorder || (_columnSeparator.HasValue && c < colCount - 1);
					int sepW = hasBorder ? 1 : SeparatorWidth;
					colX += dataColWidths[c] + (addSep ? sepW : 0);
				}
			}

			currentY++;

			// Header separator
			if (hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal, hScrollOffset: effectiveHScroll);
				currentY++;
			}
		}

		// Track data row rendering area for scrollbar
		int dataStartY = currentY;

		// Data rows - virtual rendering (only visible rows)
		int rowCount = rowSnapshot?.Count ?? RowCount;
		int startRow = _scrollOffset;
		int endRow = Math.Min(rowCount, _scrollOffset + GetVisibleRowCount());

		for (int displayR = startRow; displayR < endRow && currentY < maxY; displayR++)
		{
			int dataR = MapDisplayToData(displayR);
			// Guard against snapshot/data race — dataR may be invalid if _rows changed after snapshot
			if (rowSnapshot != null && (dataR < 0 || dataR >= rowSnapshot.Count))
				continue;

			// Row separator (between rows, not before first)
			if (displayR > startRow && _showRowSeparators && hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal, hScrollOffset: effectiveHScroll);
				currentY++;
			}

			if (currentY >= maxY) break;

			// Get row data
			IList<string> rowCells;
			Color rowBg, rowFg;
			bool isEnabled = true;
			bool isChecked = false;

			if (_dataSource != null)
			{
				rowCells = new List<string>();
				for (int c = 0; c < colCount; c++)
					rowCells.Add(_dataSource.GetCellValue(dataR, c));
				rowBg = _dataSource.GetRowBackgroundColor(dataR) ?? bgColor;
				rowFg = _dataSource.GetRowForegroundColor(dataR) ?? fgColor;
				isEnabled = _dataSource.IsRowEnabled(dataR);
				if (_checkboxMode)
					isChecked = _selectedRowIndices.Contains(displayR);
			}
			else
			{
				var row = rowSnapshot![dataR];
				rowCells = row.Cells;
				rowBg = row.BackgroundColor ?? bgColor;
				rowFg = row.ForegroundColor ?? fgColor;
				isEnabled = row.IsEnabled;
				isChecked = row.IsChecked;
			}

			// Determine row colors based on state (cursor, checked, hover)
			bool isCursorRow = displayR == _selectedRowIndex;
			bool isMultiSelected = _multiSelectEnabled && _selectedRowIndices.Contains(displayR);
			bool isRowSel = isCursorRow || isMultiSelected;
			bool isHovered = _hoveredRowIndex == displayR;

			Color effectiveRowBg = rowBg;
			Color effectiveRowFg = rowFg;

			if (isCursorRow)
			{
				// Cursor row — brightest highlight
				if (HasFocus)
				{
					effectiveRowBg = selBg;
					effectiveRowFg = selFg;
				}
				else
				{
					effectiveRowBg = unfocusedSelBg;
					effectiveRowFg = unfocusedSelFg;
				}
			}
			else if (isMultiSelected)
			{
				// Checked but not cursor — subtle highlight
				effectiveRowBg = new Color(
					(byte)Math.Min(255, selBg.R / 3 + rowBg.R * 2 / 3),
					(byte)Math.Min(255, selBg.G / 3 + rowBg.G * 2 / 3),
					(byte)Math.Min(255, selBg.B / 3 + rowBg.B * 2 / 3));
				effectiveRowFg = rowFg;
			}
			else if (isHovered)
			{
				effectiveRowBg = hoverBg;
				effectiveRowFg = hoverFg;
			}

			if (!isEnabled)
			{
				effectiveRowFg = Color.Grey;
			}

			// Cell-level highlight
			int selectedCell = -1;
			Color? cellHighlightBg = null;
			Color? cellHighlightFg = null;
			if (_cellNavigationEnabled && displayR == _selectedRowIndex && _selectedColumnIndex >= 0)
			{
				selectedCell = _selectedColumnIndex;
				cellHighlightBg = HasFocus ? Color.Cyan1 : Color.Grey50;
				cellHighlightFg = Color.Black;
			}

			// Inline editing: replace cell content with edit buffer (use a copy to avoid mutating data)
			int editCellIndex = -1;
			int editCursorPos = -1;
			if (_isEditing && displayR == _selectedRowIndex && _selectedColumnIndex >= 0)
			{
				editCellIndex = _selectedColumnIndex;
				editCursorPos = _editCursorPosition;
				rowCells = new List<string>(rowCells);
				if (_selectedColumnIndex < rowCells.Count)
					rowCells[_selectedColumnIndex] = _editBuffer;
				else
					rowCells.Add(_editBuffer);
			}

			// Compute filter match positions for highlighting
			List<(int Column, int Start, int Length)>? filterMatches = null;
			if (_filterMode == FilterMode.Confirmed && _activeFilter != null)
				filterMatches = FindMatchPositions(dataR, _activeFilter);

			// Prepend checkbox cell and offset indices for silent column
			if (_checkboxMode)
			{
				string checkText = isChecked ? "[x] " : "[ ] ";
				rowCells = new List<string>(rowCells);
				rowCells.Insert(0, checkText);
				if (selectedCell >= 0) selectedCell++;
				if (editCellIndex >= 0) editCellIndex++;
				if (filterMatches != null)
					filterMatches = filterMatches.Select(m => (m.Column + 1, m.Start, m.Length)).ToList();
			}

			FillSideMargins(currentY);
			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
				rowCells, renderCols, effectiveRowFg, effectiveRowBg, hasBorder,
				hScrollOffset: effectiveHScroll,
				isSelected: isRowSel || isHovered,
				selectedCellIndex: selectedCell, selectedCellBg: cellHighlightBg, selectedCellFg: cellHighlightFg,
				editCellIndex: editCellIndex, editCursorPos: editCursorPos,
				filterMatches: filterMatches,
				trailingFillWidth: scrollbarGutter);

			// Update row rendered position for hit testing (for in-memory rows)
			if (_dataSource == null && rowSnapshot != null && dataR < rowSnapshot.Count)
			{
				rowSnapshot[dataR].RenderedY = currentY;
				rowSnapshot[dataR].RenderedHeight = 1;
			}

			currentY++;
		}

		// Pad with empty rows so the bottom border sits at the bottom of the allocated
		// bounds when the table has been given more height than its content needs (e.g.
		// VerticalAlignment.Fill, or an explicit Height larger than the data). Without this
		// the border would close right after the last data row, leaving blank space below
		// it inside the table's own slot. Reserve the rows the bottom chrome will consume.
		int bottomChromeHeight = (_filteringEnabled && !_readOnly && hasBorder ? 2 : 0) + (hasBorder ? 1 : 0);
		int paddingStopY = maxY - bottomChromeHeight;
		while (currentY < paddingStopY)
		{
			FillSideMargins(currentY);
			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
				new List<string>(), renderCols, fgColor, bgColor, hasBorder,
				hScrollOffset: effectiveHScroll, trailingFillWidth: scrollbarGutter);
			currentY++;
		}

		// Filter status bar (separator + status row + bottom border as one merged section)
		if (_filteringEnabled && !_readOnly && hasBorder)
		{
			// Separator: ├────────────────────────────┤ (no column crosses)
			if (currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawMergedHorizontalLine(buffer, startX, currentY, colWidths, clipRect,
					box, borderColor, effectiveBg, box.LeftTee, box.RightTee, box.Horizontal);
				currentY++;
			}

			// Status bar content row
			if (currentY < maxY)
			{
				int statusWidth = 0;
				foreach (int w in colWidths) statusWidth += w;
				statusWidth += colWidths.Length + 1; // border overhead

				FillSideMargins(currentY);
				DrawFilterStatusBar(buffer, startX, currentY, statusWidth, clipRect,
					fgColor, effectiveBg, box, borderColor, hasBorder);
				currentY++;
			}

			// Bottom border: ╰────────────────────────────╯ (no column tees)
			if (currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawMergedHorizontalLine(buffer, startX, currentY, colWidths, clipRect,
					box, borderColor, effectiveBg, box.BottomLeft, box.BottomRight, box.Horizontal);
				currentY++;
			}
		}
		else if (hasBorder && currentY < maxY)
		{
			// Standard bottom border with column tees
			FillSideMargins(currentY);
			DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
				box.BottomLeft, box.BottomTee, box.BottomRight, box.Horizontal, hScrollOffset: effectiveHScroll);
			currentY++;
		}

		// Fill remaining height (before scrollbar)
		while (currentY < maxY)
		{
			if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), fgColor, effectiveBg);
			currentY++;
		}

		// Draw vertical scrollbar
		if (showVScrollbar)
		{
			int scrollbarX = startX + contentWidth;
			int dataRowsHeight = currentY - dataStartY;
			if (dataRowsHeight > 0)
				DrawVerticalScrollbar(buffer, scrollbarX, dataStartY, dataRowsHeight, bgColor);
		}

		// Draw horizontal scrollbar
		if (showHScrollbar && currentY < bounds.Bottom - Margin.Bottom)
		{
			FillSideMargins(currentY);
			int hScrollWidth = contentWidth;
			if (showVScrollbar) hScrollWidth--; // don't overlap vertical scrollbar

			DrawHorizontalScrollbar(buffer, startX, currentY, hScrollWidth, totalColumnsWidth, bgColor);

			// Corner cell when both scrollbars visible
			if (showVScrollbar)
			{
				int cornerX = startX + contentWidth;
				if (cornerX >= clipRect.X && cornerX < clipRect.Right)
					buffer.SetNarrowCell(cornerX, currentY, ' ', fgColor, effectiveBg);
			}
			currentY++;
		}

		ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, effectiveBg);

		// Apply row animation overlays (flash, highlight, fade-out)
		if (HasActiveRowAnimations)
			ApplyRowAnimationOverlays(buffer);
	}

	#endregion
}
