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
		BoxChars box, Color borderColor, Color borderBg, List<string> cells, List<TableColumn>? cols,
		Color rowFg, Color rowBg, bool hasBorder,
		int hScrollOffset = 0, int viewportWidth = int.MaxValue,
		bool isSelected = false, int selectedCellIndex = -1, Color? selectedCellBg = null, Color? selectedCellFg = null,
		bool showCheckbox = false, bool isChecked = false,
		int editCellIndex = -1, int editCursorPos = -1,
		List<(int Column, int Start, int Length)>? filterMatches = null)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		int writeX = x;
		int maxX = viewportWidth == int.MaxValue ? int.MaxValue : x + viewportWidth;

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

			// Prepend checkbox for first column
			if (c == 0 && showCheckbox)
			{
				string checkPrefix = isChecked ? "[x] " : "[ ] ";
				cellText = checkPrefix + cellText;
			}

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

			if (isEditCell)
			{
				// Render edit buffer as plain text with cursor using Unicode-aware width
				var editCells = MarkupParser.Parse(cellText, cellFg, cellBg);
				int visLen = editCells.Count;
				int cursorPos = editCursorPos;

				// Fill entire cell with edit background first
				int cellStartX = writeX;
				for (int i = 0; i < colW; i++)
				{
					int cx = cellStartX + i;
					if (cx >= clipRect.X && cx < clipRect.Right && cx < maxX)
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
							var editCell = new Cell(editCells[i].Character, fg, bg)
							{
								IsWideContinuation = editCells[i].IsWideContinuation,
								Combiners = editCells[i].Combiners
							};
							buffer.SetCell(cx, y, editCell);
						}
						else
						{
							buffer.SetNarrowCell(cx, y, ' ', fg, bg);
						}
					}
				}
				writeX += colW;
			}
			else
			{
				var cellCells = MarkupParser.Parse(cellText, cellFg, cellBg);
				int visLen = cellCells.Count;

				if (visLen > colW)
				{
					cellCells = cellCells.GetRange(0, colW);
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


				// Left padding
				for (int i = 0; i < padLeft; i++)
				{
					if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
					{
						Color bg = cellBg;
						buffer.SetNarrowCell(writeX, y, ' ', cellFg, bg);
					}
					writeX++;
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
					if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
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
						buffer.SetCell(writeX, y, bufCell);
					}
					writeX++;
					charIdx++;
				}

				// Right padding
				for (int i = 0; i < padRight; i++)
				{
					if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
					{
						Color bg = cellBg;
						buffer.SetNarrowCell(writeX, y, ' ', cellFg, bg);
					}
					writeX++;
				}
			}

			// Column separator
			if (hasBorder)
			{
				if (writeX >= clipRect.X && writeX < clipRect.Right && writeX < maxX)
				{
					Color bg = borderBg;
					buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
				}
				writeX++;
			}
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

		// Reserve space for vertical scrollbar
		if (ShouldShowVerticalScrollbar())
			contentWidth = Math.Max(1, contentWidth - 1);

		int[] colWidths;
		if (_dataSource != null)
			colWidths = ComputeColumnWidthsFromDataSource(contentWidth, _scrollOffset, GetVisibleRowCount());
		else
			colWidths = ComputeColumnWidths(contentWidth, colSnapshot!, rowSnapshot!, _scrollOffset, GetVisibleRowCount());

		bool hasBorder = _borderStyle != BorderStyle.None;
		int borderOverhead = hasBorder ? (colCount + 1) : 0;
		int measuredWidth = 0;
		foreach (int w in colWidths) measuredWidth += w;
		measuredWidth += borderOverhead;

		// Add scrollbar width
		if (ShouldShowVerticalScrollbar())
			measuredWidth++;

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
		var effectiveBg = ((_backgroundColorValue == null || _backgroundColorValue == Color.Default) && Container?.HasGradientBackground == true) ? Color.Transparent : bgColor;

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

		int[] colWidths;
		if (_dataSource != null)
			colWidths = ComputeColumnWidthsFromDataSource(contentWidth, _scrollOffset, GetVisibleRowCount());
		else
			colWidths = ComputeColumnWidths(contentWidth, colSnapshot!, rowSnapshot!, _scrollOffset, GetVisibleRowCount());

		int totalColumnsWidth = GetTotalColumnsWidth(colWidths);
		bool showHScrollbar = ShouldShowHorizontalScrollbar(totalColumnsWidth, contentWidth);

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

		// Top border
		if (hasBorder && currentY < maxY)
		{
			FillSideMargins(currentY);
			DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				box.TopLeft, box.TopTee, box.TopRight, box.Horizontal);
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

			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				headerCells, colSnapshot, headerFg, headerBg, hasBorder);

			// Update column rendered positions for hit testing (always, including DataSource mode)
			{
				int colX = startX + (hasBorder ? 1 : 0);
				_renderedColumnX = new int[colCount];
				_renderedColumnWidths = new int[colCount];
				for (int c = 0; c < colCount; c++)
				{
					_renderedColumnX[c] = colX;
					_renderedColumnWidths[c] = colWidths[c];
					if (colSnapshot != null && c < colSnapshot.Count)
					{
						colSnapshot[c].RenderedX = colX;
						colSnapshot[c].RenderedWidth = colWidths[c];
					}
					colX += colWidths[c] + (hasBorder ? 1 : 0);
				}
			}

			currentY++;

			// Header separator
			if (hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal);
				currentY++;
			}
		}

		// Track data row rendering area for scrollbar
		int dataStartY = currentY;

		// Data rows - virtual rendering (only visible rows)
		int rowCount = RowCount;
		int startRow = _scrollOffset;
		int endRow = Math.Min(rowCount, _scrollOffset + GetVisibleRowCount());

		for (int displayR = startRow; displayR < endRow && currentY < maxY; displayR++)
		{
			int dataR = MapDisplayToData(displayR);

			// Row separator (between rows, not before first)
			if (displayR > startRow && _showRowSeparators && hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal);
				currentY++;
			}

			if (currentY >= maxY) break;

			// Get row data
			List<string> rowCells;
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

			// Determine row colors based on state (selection, hover)
			bool isRowSel = IsRowSelected(displayR);
			bool isHovered = _hoveredRowIndex == displayR;

			Color effectiveRowBg = rowBg;
			Color effectiveRowFg = rowFg;

			if (isRowSel)
			{
				if (_hasFocus)
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
				cellHighlightBg = _hasFocus ? Color.Cyan1 : Color.Grey50;
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

			FillSideMargins(currentY);
			DrawDataRow(buffer, startX, currentY, colWidths, clipRect, box, borderColor, effectiveBg,
				rowCells, colSnapshot, effectiveRowFg, effectiveRowBg, hasBorder,
				isSelected: isRowSel || isHovered,
				selectedCellIndex: selectedCell, selectedCellBg: cellHighlightBg, selectedCellFg: cellHighlightFg,
				showCheckbox: _checkboxMode, isChecked: isChecked,
				editCellIndex: editCellIndex, editCursorPos: editCursorPos,
				filterMatches: filterMatches);

			// Update row rendered position for hit testing (for in-memory rows)
			if (_dataSource == null && rowSnapshot != null && dataR < rowSnapshot.Count)
			{
				rowSnapshot[dataR].RenderedY = currentY;
				rowSnapshot[dataR].RenderedHeight = 1;
			}

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
					box, borderColor, bgColor, box.LeftTee, box.RightTee, box.Horizontal);
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
					box, borderColor, bgColor, box.BottomLeft, box.BottomRight, box.Horizontal);
				currentY++;
			}
		}
		else if (hasBorder && currentY < maxY)
		{
			// Standard bottom border with column tees
			FillSideMargins(currentY);
			DrawHorizontalLine(buffer, startX, currentY, colWidths, clipRect, box, borderColor, bgColor,
				box.BottomLeft, box.BottomTee, box.BottomRight, box.Horizontal);
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
					buffer.SetNarrowCell(cornerX, currentY, ' ', fgColor, bgColor);
			}
			currentY++;
		}

		ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, effectiveBg);
	}

	#endregion
}
